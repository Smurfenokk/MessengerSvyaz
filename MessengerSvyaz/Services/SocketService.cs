using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MessengerSvyaz.Models;
using SocketIOClient;

namespace MessengerSvyaz.Services;

public class SocketService : IDisposable
{
    private SocketIOClient.SocketIO? _client;
    private string _baseUrl = "http://svyaz.darkforce-sl.ru";
    private bool _disposed;

    public event EventHandler<Message>? OnNewMessage;
    public event EventHandler<GroupMessage>? OnNewGroupMessage;
    public event EventHandler<string>? OnUserTyping;
    public event EventHandler<string>? OnUserOnline;
    public event EventHandler<string>? OnUserOffline;
    public event EventHandler<(string messageId, string newContent)>? OnMessageEdited;
    public event EventHandler<string>? OnMessageDeleted;
    public event EventHandler<(string otherUser, string messageId)>? OnMessageRead;
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;

    public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

    public async Task ConnectAsync(string username)
    {
        try
        {
            _client = new SocketIOClient.SocketIO(_baseUrl, new SocketIOClient.SocketIOOptions
            {
                Query = new Dictionary<string, string>
                {
                    { "EIO", "4" },
                    { "transport", "websocket" }
                },
                Reconnection = true,
                ReconnectionAttempts = 5,
                ReconnectionDelay = 2000
            });

            _client.OnConnected += (sender, e) => 
            {
                OnConnected?.Invoke(this, EventArgs.Empty);
            };

            _client.OnDisconnected += (sender, e) => 
            {
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            };

            _client.OnError += (sender, e) => 
            {
                System.Diagnostics.Debug.WriteLine("Socket error: " + e);
            };

            _client.On("new_message", response => 
            {
                try
                {
                    var msg = response.GetValue<Message>();
                    OnNewMessage?.Invoke(this, msg);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing new_message: " + ex.Message);
                }
            });

            _client.On("new_group_message", response => 
            {
                try
                {
                    var msg = response.GetValue<GroupMessage>();
                    OnNewGroupMessage?.Invoke(this, msg);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing new_group_message: " + ex.Message);
                }
            });

            _client.On("user_typing", response => 
            {
                try
                {
                    var user = response.GetValue<string>();
                    OnUserTyping?.Invoke(this, user);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing user_typing: " + ex.Message);
                }
            });

            _client.On("user_online", response => 
            {
                try
                {
                    var user = response.GetValue<string>();
                    OnUserOnline?.Invoke(this, user);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing user_online: " + ex.Message);
                }
            });

            _client.On("user_offline", response => 
            {
                try
                {
                    var user = response.GetValue<string>();
                    OnUserOffline?.Invoke(this, user);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing user_offline: " + ex.Message);
                }
            });

            _client.On("message_edited", response => 
            {
                try
                {
                    var json = response.GetValue<System.Text.Json.JsonElement>();
                    var msgId = json.GetProperty("message_id").GetString() ?? "";
                    var content = json.GetProperty("new_content").GetString() ?? "";
                    OnMessageEdited?.Invoke(this, (msgId, content));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing message_edited: " + ex.Message);
                }
            });

            _client.On("message_deleted", response => 
            {
                try
                {
                    var msgId = response.GetValue<string>();
                    OnMessageDeleted?.Invoke(this, msgId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing message_deleted: " + ex.Message);
                }
            });

            _client.On("message_read", response => 
            {
                try
                {
                    var json = response.GetValue<System.Text.Json.JsonElement>();
                    var otherUser = json.GetProperty("other_user").GetString() ?? "";
                    var msgId = json.GetProperty("message_id").GetString() ?? "";
                    OnMessageRead?.Invoke(this, (otherUser, msgId));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error parsing message_read: " + ex.Message);
                }
            });

            await _client.ConnectAsync();

            await _client.EmitAsync("join_chat", new { other_user = username });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Socket connection error: " + ex.Message);
            throw;
        }
    }

    public async Task JoinChatAsync(string otherUser)
    {
        if (_client?.Connected == true)
        {
            await _client.EmitAsync("join_chat", new { other_user = otherUser });
        }
    }

    public async Task JoinGroupAsync(string groupId)
    {
        if (_client?.Connected == true)
        {
            await _client.EmitAsync("join_group", new { group_id = groupId });
        }
    }

    public async Task SendTypingAsync(string otherUser)
    {
        if (_client?.Connected == true)
        {
            await _client.EmitAsync("typing", new { other_user = otherUser });
        }
    }

    public async Task SendReadReceiptAsync(string otherUser, string messageId)
    {
        if (_client?.Connected == true)
        {
            await _client.EmitAsync("read_receipt", new { other_user = otherUser, message_id = messageId });
        }
    }

    public async Task DisconnectAsync()
    {
        if (_client != null)
        {
            try
            {
                await _client.DisconnectAsync();
                _client.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Socket disconnect error: " + ex.Message);
            }
            finally
            {
                _client = null;
            }
        }
    }

    public bool IsConnected => _client?.Connected == true;

    public void Dispose()
    {
        if (!_disposed)
        {
            _ = DisconnectAsync();
            _disposed = true;
        }
    }
}
