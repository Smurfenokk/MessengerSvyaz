#!/usr/bin/env python3
import argparse
import sys
import os
import uuid
import hashlib
import json
from datetime import datetime
from flask import Flask, request, jsonify, session, send_file
from flask_socketio import SocketIO, emit, join_room, leave_room
from flask_cors import CORS
from flask_limiter import Limiter
from werkzeug.utils import secure_filename

app = Flask(__name__)
app.secret_key = os.environ.get('SECRET_KEY', 'dev-secret-key')
app.config['MAX_CONTENT_LENGTH'] = 16 * 1024 * 1024

CORS(app, supports_credentials=True)
socketio = SocketIO(app, cors_allowed_origins="*", async_mode='eventlet')
limiter = Limiter(app)

DATA_DIR = os.environ.get('DATA_DIR', '/app/server_data')
AVATARS_DIR = os.path.join(DATA_DIR, 'avatars')
os.makedirs(DATA_DIR, exist_ok=True)
os.makedirs(AVATARS_DIR, exist_ok=True)

USERS_FILE = os.path.join(DATA_DIR, 'users.json')
MESSAGES_FILE = os.path.join(DATA_DIR, 'messages.json')
GROUPS_FILE = os.path.join(DATA_DIR, 'groups.json')
KEYS_FILE = os.path.join(DATA_DIR, 'keys.json')
SUPPORT_FILE = os.path.join(DATA_DIR, 'support.json')

ALLOWED_EXTENSIONS = {'png', 'jpg', 'jpeg', 'gif', 'webp'}

def load_data(filename, default=None):
    if not os.path.exists(filename):
        return default if default is not None else {}
    with open(filename, 'r', encoding='utf-8') as f:
        return json.load(f)

def save_data(filename, data):
    with open(filename, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

def hash_password(password):
    return hashlib.sha256(password.encode()).hexdigest()

def generate_id():
    return str(uuid.uuid4())[:8]

def generate_key():
    return str(uuid.uuid4())[:16]

def allowed_file(filename):
    return '.' in filename and filename.rsplit('.', 1)[1].lower() in ALLOWED_EXTENSIONS

def init_files():
    if not os.path.exists(USERS_FILE):
        save_data(USERS_FILE, {})
    if not os.path.exists(MESSAGES_FILE):
        save_data(MESSAGES_FILE, [])
    if not os.path.exists(GROUPS_FILE):
        save_data(GROUPS_FILE, [])
    if not os.path.exists(KEYS_FILE):
        save_data(KEYS_FILE, [])
    if not os.path.exists(SUPPORT_FILE):
        save_data(SUPPORT_FILE, [])

@app.route('/login', methods=['POST'])
def login():
    data = request.get_json() or request.form
    username = data.get('username', '').strip().lower()
    password = data.get('password', '')

    users = load_data(USERS_FILE, {})

    if username not in users:
        return jsonify({'status': 'error', 'message': 'Неверное имя пользователя или пароль'})

    user = users[username]
    if user['password_hash'] != hash_password(password):
        return jsonify({'status': 'error', 'message': 'Неверное имя пользователя или пароль'})

    if user.get('is_blocked'):
        return jsonify({'status': 'error', 'message': 'Аккаунт заблокирован'})

    session['username'] = username
    session['is_admin'] = user.get('is_admin', False)
    user['is_online'] = True
    user['last_login'] = datetime.now().isoformat()
    save_data(USERS_FILE, users)

    return jsonify({
        'status': 'success',
        'data': {
            'is_admin': user.get('is_admin', False),
            'session_id': session.get('_id')
        }
    })

@app.route('/register', methods=['POST'])
def register():
    data = request.get_json() or request.form
    username = data.get('username', '').strip().lower()
    password = data.get('password', '')
    key = data.get('key', '').strip()
    first_name = data.get('first_name', '').strip()
    last_name = data.get('last_name', '').strip()
    middle_name = data.get('middle_name', '').strip()
    display_name = data.get('display_name', first_name).strip()

    keys = load_data(KEYS_FILE, [])
    users = load_data(USERS_FILE, {})

    valid_key = next((k for k in keys if k['key'] == key and k.get('is_active', True) and not k.get('used_by')), None)

    if not valid_key:
        return jsonify({'status': 'error', 'message': 'Неверный или использованный ключ'})

    if username in users:
        return jsonify({'status': 'error', 'message': 'Имя пользователя уже занято'})

    if len(username) < 3 or len(username) > 32:
        return jsonify({'status': 'error', 'message': 'Имя пользователя должно быть от 3 до 32 символов'})

    if len(password) < 8:
        return jsonify({'status': 'error', 'message': 'Пароль должен быть не менее 8 символов'})

    if not first_name:
        return jsonify({'status': 'error', 'message': 'Имя обязательно'})

    account_id = generate_id()
    users[username] = {
        'username': username,
        'account_id': account_id,
        'password_hash': hash_password(password),
        'first_name': first_name,
        'last_name': last_name,
        'middle_name': middle_name,
        'display_name': display_name,
        'is_admin': False,
        'is_online': False,
        'is_blocked': False,
        'created_at': datetime.now().isoformat(),
        'bio': '',
        'has_avatar': False,
        'avatar_filename': None
    }

    valid_key['used_by'] = username
    valid_key['used_at'] = datetime.now().isoformat()
    valid_key['is_active'] = False

    save_data(USERS_FILE, users)
    save_data(KEYS_FILE, keys)

    return jsonify({'status': 'success', 'message': 'Регистрация успешна'})

@app.route('/api/users/list', methods=['GET'])
def get_users():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    users = load_data(USERS_FILE, {})
    exclude_admins = request.args.get('exclude_admins', '0') == '1'
    search = request.args.get('search', '').strip().lower()

    result = []
    for uname, user in users.items():
        if exclude_admins and user.get('is_admin'):
            continue
        if search and uname != search:
            continue
        result.append({
            'username': user['username'],
            'account_id': user['account_id'],
            'display_name': user.get('display_name', user['username']),
            'is_admin': user.get('is_admin', False),
            'is_online': user.get('is_online', False),
            'is_blocked': user.get('is_blocked', False),
            'bio': user.get('bio', ''),
            'has_avatar': user.get('has_avatar', False),
            'is_visible': True
        })

    return jsonify({'status': 'success', 'data': {'users': result}})

@app.route('/api/profile', methods=['GET'])
def get_profile():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    username = session['username']
    users = load_data(USERS_FILE, {})
    user = users.get(username, {})

    avatar_url = None
    if user.get('has_avatar'):
        avatar_url = f"/api/avatar/{username}"

    return jsonify({
        'status': 'success',
        'data': {
            'username': username,
            'display_name': user.get('display_name', username),
            'bio': user.get('bio', ''),
            'has_avatar': user.get('has_avatar', False),
            'avatar_url': avatar_url
        }
    })

@app.route('/api/avatar/<username>', methods=['GET'])
def get_avatar(username):
    users = load_data(USERS_FILE, {})
    user = users.get(username)

    if not user or not user.get('has_avatar') or not user.get('avatar_filename'):
        return jsonify({'status': 'error', 'message': 'Avatar not found'}), 404

    avatar_path = os.path.join(AVATARS_DIR, user['avatar_filename'])
    if not os.path.exists(avatar_path):
        return jsonify({'status': 'error', 'message': 'Avatar file not found'}), 404

    return send_file(avatar_path)

@app.route('/api/upload/avatar', methods=['POST'])
def upload_avatar():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    if 'file' not in request.files:
        return jsonify({'status': 'error', 'message': 'No file provided'}), 400

    file = request.files['file']
    if file.filename == '':
        return jsonify({'status': 'error', 'message': 'No file selected'}), 400

    if file and allowed_file(file.filename):
        username = session['username']
        ext = file.filename.rsplit('.', 1)[1].lower()
        filename = f"{username}_{generate_id()}.{ext}"
        filepath = os.path.join(AVATARS_DIR, filename)

        file.save(filepath)

        users = load_data(USERS_FILE, {})
        if username in users:
            old_filename = users[username].get('avatar_filename')
            if old_filename:
                old_path = os.path.join(AVATARS_DIR, old_filename)
                if os.path.exists(old_path):
                    os.remove(old_path)

            users[username]['has_avatar'] = True
            users[username]['avatar_filename'] = filename
            save_data(USERS_FILE, users)

        return jsonify({
            'status': 'success',
            'data': {
                'url': f"/api/avatar/{username}",
                'filename': filename
            }
        })

    return jsonify({'status': 'error', 'message': 'Invalid file type'}), 400

@app.route('/api/profile/update', methods=['POST'])
def update_profile():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']

    users = load_data(USERS_FILE, {})
    if current_user in users:
        users[current_user]['bio'] = data.get('bio', '')[:30]
        save_data(USERS_FILE, users)
        return jsonify({'status': 'success'})

    return jsonify({'status': 'error', 'message': 'User not found'})

@app.route('/api/profile/change_password', methods=['POST'])
def change_password():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']
    current_password = data.get('current_password', '')
    new_password = data.get('new_password', '')

    users = load_data(USERS_FILE, {})
    user = users.get(current_user)

    if not user or user['password_hash'] != hash_password(current_password):
        return jsonify({'status': 'error', 'message': 'Неверный текущий пароль'})

    if len(new_password) < 8:
        return jsonify({'status': 'error', 'message': 'Новый пароль должен быть не менее 8 символов'})

    user['password_hash'] = hash_password(new_password)
    save_data(USERS_FILE, users)

    return jsonify({'status': 'success'})

@app.route('/api/chat/messages/<other_user>', methods=['GET'])
def get_chat_messages(other_user):
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    messages = load_data(MESSAGES_FILE, [])

    chat_messages = [
        m for m in messages
        if (m['sender'] == current_user and m['receiver'] == other_user) or
           (m['sender'] == other_user and m['receiver'] == current_user)
    ]

    chat_messages.sort(key=lambda x: x['timestamp'])

    return jsonify({'status': 'success', 'data': {'messages': chat_messages}})

@app.route('/api/chat/messages/saved', methods=['GET'])
def get_saved_messages():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    messages = load_data(MESSAGES_FILE, [])

    saved_messages = [
        m for m in messages
        if m.get('type') == 'saved' and m['sender'] == current_user
    ]

    saved_messages.sort(key=lambda x: x['timestamp'])

    return jsonify({'status': 'success', 'data': {'messages': saved_messages}})

@app.route('/api/chat/send', methods=['POST'])
def send_message():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']

    message = {
        'id': generate_id(),
        'sender': current_user,
        'receiver': data.get('receiver'),
        'content': data.get('message', ''),
        'timestamp': datetime.now().isoformat(),
        'type': data.get('type', 'text'),
        'edited': False,
        'deleted': False,
        'read': False,
        'reply_to': data.get('reply_to')
    }

    messages = load_data(MESSAGES_FILE, [])
    messages.append(message)
    save_data(MESSAGES_FILE, messages)

    socketio.emit('new_message', message, room=data.get('receiver'))

    return jsonify({'status': 'success', 'data': {'message_id': message['id']}})

@app.route('/api/chat/save', methods=['POST'])
def save_message():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']

    message = {
        'id': generate_id(),
        'sender': current_user,
        'receiver': current_user,
        'content': data.get('message', ''),
        'timestamp': datetime.now().isoformat(),
        'type': 'saved',
        'edited': False,
        'deleted': False,
        'read': True,
        'reactions': {}
    }

    messages = load_data(MESSAGES_FILE, [])
    messages.append(message)
    save_data(MESSAGES_FILE, messages)

    return jsonify({'status': 'success', 'data': {'message_id': message['id']}})

@app.route('/api/chat/recent', methods=['GET'])
def get_recent_chats():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    messages = load_data(MESSAGES_FILE, [])
    users = load_data(USERS_FILE, {})

    chats = {}
    for m in messages:
        if m.get('type') == 'saved':
            continue
        if m['sender'] == current_user or m['receiver'] == current_user:
            other = m['receiver'] if m['sender'] == current_user else m['sender']
            if other not in chats or m['timestamp'] > chats[other]['timestamp']:
                chats[other] = m

    result = []
    for other, last_msg in sorted(chats.items(), key=lambda x: x[1]['timestamp'], reverse=True):
        user = users.get(other, {})
        result.append({
            'username': other,
            'display_name': user.get('display_name', other),
            'last_message': last_msg['content'][:50] + '...' if len(last_msg['content']) > 50 else last_msg['content'],
            'last_time': last_msg['timestamp'],
            'unread_count': sum(1 for m in messages if m['sender'] == other and m['receiver'] == current_user and not m.get('read', False)),
            'online': user.get('is_online', False)
        })

    return jsonify({'status': 'success', 'data': {'chats': result}})

@app.route('/api/groups/my', methods=['GET'])
def get_my_groups():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    groups = load_data(GROUPS_FILE, [])

    my_groups = []
    for g in groups:
        if current_user in g.get('members', []) or g['creator'] == current_user:
            my_groups.append({
                'id': g['id'],
                'name': g['name'],
                'description': g.get('description', ''),
                'creator': g['creator'],
                'members_count': len(g.get('members', [])),
                'role': 'creator' if g['creator'] == current_user else 'member',
                'settings': g.get('settings', {'allow_reactions': True})
            })

    return jsonify({'status': 'success', 'data': {'groups': my_groups}})

@app.route('/api/groups/create', methods=['POST'])
def create_group():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']

    group = {
        'id': generate_id(),
        'name': data.get('name', ''),
        'description': data.get('description', ''),
        'creator': current_user,
        'created_at': datetime.now().isoformat(),
        'members': [current_user],
        'settings': data.get('settings', {'allow_reactions': True})
    }

    groups = load_data(GROUPS_FILE, [])
    groups.append(group)
    save_data(GROUPS_FILE, groups)

    return jsonify({'status': 'success', 'data': {'group_id': group['id']}})

@app.route('/api/groups/join', methods=['POST'])
def join_group():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']
    group_id = data.get('group_id')

    groups = load_data(GROUPS_FILE, [])

    for g in groups:
        if g['id'] == group_id:
            if current_user not in g['members']:
                g['members'].append(current_user)
                save_data(GROUPS_FILE, groups)
            return jsonify({'status': 'success'})

    return jsonify({'status': 'error', 'message': 'Группа не найдена'})

@app.route('/api/group/messages/<group_id>', methods=['GET'])
def get_group_messages(group_id):
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    groups = load_data(GROUPS_FILE, [])
    messages = load_data(MESSAGES_FILE, [])

    group = next((g for g in groups if g['id'] == group_id), None)
    if not group or (current_user not in group['members'] and group['creator'] != current_user):
        return jsonify({'status': 'error', 'message': 'Доступ запрещен'}), 403

    group_messages = [m for m in messages if m.get('group_id') == group_id]
    group_messages.sort(key=lambda x: x['timestamp'])

    return jsonify({
        'status': 'success',
        'data': {
            'messages': group_messages,
            'is_admin': group['creator'] == current_user,
            'allow_reactions': group.get('settings', {}).get('allow_reactions', True),
            'members': group.get('members', []),
            'creator': group['creator']
        }
    })

@app.route('/api/group/info/<group_id>', methods=['GET'])
def get_group_info(group_id):
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    groups = load_data(GROUPS_FILE, [])

    group = next((g for g in groups if g['id'] == group_id), None)
    if not group or (current_user not in group['members'] and group['creator'] != current_user):
        return jsonify({'status': 'error', 'message': 'Доступ запрещен'}), 403

    return jsonify({
        'status': 'success',
        'data': {
            'id': group['id'],
            'name': group['name'],
            'description': group.get('description', ''),
            'creator': group['creator'],
            'members': group.get('members', []),
            'members_count': len(group.get('members', [])),
            'created_at': group.get('created_at', ''),
            'settings': group.get('settings', {'allow_reactions': True})
        }
    })

@app.route('/api/group/send', methods=['POST'])
def send_group_message():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']
    group_id = data.get('group_id')

    message = {
        'id': generate_id(),
        'sender': current_user,
        'group_id': group_id,
        'content': data.get('message', ''),
        'timestamp': datetime.now().isoformat(),
        'type': 'group',
        'edited': False,
        'deleted': False,
        'reply_to': data.get('reply_to'),
        'reactions': {}
    }

    messages = load_data(MESSAGES_FILE, [])
    messages.append(message)
    save_data(MESSAGES_FILE, messages)

    socketio.emit('new_group_message', message, room=group_id)

    return jsonify({'status': 'success'})

@app.route('/api/group/reaction', methods=['POST'])
def add_group_reaction():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']
    group_id = data.get('group_id')
    message_id = data.get('message_id')
    emoji = data.get('emoji')

    messages = load_data(MESSAGES_FILE, [])

    for m in messages:
        if m['id'] == message_id and m.get('group_id') == group_id:
            if 'reactions' not in m:
                m['reactions'] = {}

            if emoji not in m['reactions']:
                m['reactions'][emoji] = []

            if current_user in m['reactions'][emoji]:
                m['reactions'][emoji].remove(current_user)
                if not m['reactions'][emoji]:
                    del m['reactions'][emoji]
            else:
                m['reactions'][emoji].append(current_user)

            save_data(MESSAGES_FILE, messages)
            return jsonify({'status': 'success'})

    return jsonify({'status': 'error', 'message': 'Сообщение не найдено'})

@app.route('/api/support/messages', methods=['GET'])
def get_support_messages():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    current_user = session['username']
    messages = load_data(SUPPORT_FILE, [])

    user_messages = [m for m in messages if m.get('user') == current_user]

    return jsonify({'status': 'success', 'data': {'messages': user_messages}})

@app.route('/api/support/send', methods=['POST'])
def send_support_message():
    if 'username' not in session:
        return jsonify({'status': 'error', 'message': 'Unauthorized'}), 401

    data = request.get_json()
    current_user = session['username']

    message = {
        'id': generate_id(),
        'user': current_user,
        'sender': current_user,
        'content': data.get('message', ''),
        'timestamp': datetime.now().isoformat(),
        'type': 'user',
        'answered': False
    }

    messages = load_data(SUPPORT_FILE, [])
    messages.append(message)
    save_data(SUPPORT_FILE, messages)

    return jsonify({'status': 'success'})

@socketio.on('connect')
def handle_connect():
    if 'username' in session:
        users = load_data(USERS_FILE, {})
        if session['username'] in users:
            users[session['username']]['is_online'] = True
            save_data(USERS_FILE, users)

@socketio.on('disconnect')
def handle_disconnect():
    if 'username' in session:
        users = load_data(USERS_FILE, {})
        if session['username'] in users:
            users[session['username']]['is_online'] = False
            save_data(USERS_FILE, users)

@socketio.on('join_chat')
def handle_join_chat(data):
    room = data.get('other_user')
    if room:
        join_room(room)

@socketio.on('join_group')
def handle_join_group(data):
    room = data.get('group_id')
    if room:
        join_room(room)

@socketio.on('typing')
def handle_typing(data):
    emit('user_typing', session.get('username'), room=data.get('other_user'), broadcast=True)

def cmd_generate(count=1):
    init_files()
    keys = load_data(KEYS_FILE, [])

    generated = []
    for _ in range(count):
        key = generate_key()
        key_data = {
            'key': key,
            'created_at': datetime.now().isoformat(),
            'is_active': True,
            'used_by': None,
            'used_at': None
        }
        keys.append(key_data)
        generated.append(key)

    save_data(KEYS_FILE, keys)

    print(f"Generated {len(generated)} key(s):")
    for key in generated:
        print(f"  {key}")

def cmd_list_keys():
    init_files()
    keys = load_data(KEYS_FILE, [])

    if not keys:
        print("No keys found")
        return

    print(f"{'Key':<20} {'Status':<10} {'Created':<20} {'Used By':<15}")
    print("-" * 70)

    for k in keys:
        status = "Used" if k.get('used_by') else "Active" if k.get('is_active', True) else "Inactive"
        created = k.get('created_at', 'N/A')[:19]
        used_by = k.get('used_by') or 'N/A'
        print(f"{k['key']:<20} {status:<10} {created:<20} {used_by:<15}")

def cmd_run(host='0.0.0.0', port=5000):
    init_files()
    print(f"Starting server on {host}:{port}")
    socketio.run(app, host=host, port=port, debug=False)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Messenger Svyaz Server')
    subparsers = parser.add_subparsers(dest='command', help='Commands')

    generate_parser = subparsers.add_parser('generate', help='Generate registration key(s)')
    generate_parser.add_argument('-n', '--count', type=int, default=1, help='Number of keys to generate')

    list_parser = subparsers.add_parser('list-keys', help='List all registration keys')

    run_parser = subparsers.add_parser('run', help='Run the server')
    run_parser.add_argument('-H', '--host', default='0.0.0.0', help='Host to bind to')
    run_parser.add_argument('-p', '--port', type=int, default=5000, help='Port to bind to')

    args = parser.parse_args()

    if args.command == 'generate':
        cmd_generate(args.count)
    elif args.command == 'list-keys':
        cmd_list_keys()
    elif args.command == 'run':
        cmd_run(args.host, args.port)
    else:
        parser.print_help()
        sys.exit(1)