using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessengerSvyaz.Models;
using MessengerSvyaz.Services;
using Microsoft.Win32;

namespace MessengerSvyaz.ViewModels;

public class ChatViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly SocketService _socketService;
    private readonly MainViewModel _mainViewModel;
    private readonly P2PService? _p2pService;
    private readonly EncryptionService? _encryptionService;
    private readonly string _otherUser;
    private readonly bool _isSavedMessages;

    private ObservableCollection<Message> _messages = new();
    private string _newMessage = string.Empty;
    private bool _isLoading;
    private bool _isP2PReady = false;
    private int _currentPage = 0;
    private bool _allMessagesLoaded = false;
    private bool _isOtherUserTyping;
    private Timer? _typingTimer;
    private bool _isEditing;
    private string? _editingMessageId;

    public ChatViewModel(AuthService authService, ApiService apiService, SocketService socketService, 
        MainViewModel mainViewModel, string otherUser)
    {
        _authService = authService;
        _apiService = apiService;
        _socketService = socketService;
        _mainViewModel = mainViewModel;
        _otherUser = otherUser;
        _isSavedMessages = otherUser == _authService.CurrentUsername;

        if (!_isSavedMessages)
        {
            _encryptionService = new EncryptionService();
            _p2pService = new P2PService(_encryptionService);
        }

        SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(NewMessage) && (_isSavedMessages || _isP2PReady));
        GoBackCommand = new RelayCommand(async _ => await GoBackAndCleanupAsync());
        LoadMoreMessagesCommand = new RelayCommand(async _ => await LoadMessagesAsync(true), _ => !_isLoading && !_allMessagesLoaded);
        EditMessageCommand = new RelayCommand(message => StartEdit(message as Message), message => message is Message);
        DeleteMessageCommand = new RelayCommand(async message => await DeleteMessageAsync(message as Message), message => message is Message);
        CancelEditCommand = new RelayCommand(_ => CancelEdit());
        SendFileCommand = new RelayCommand(async _ => await SendFileAsync());
        
        _ = InitializeAsync();
    }

    public ObservableCollection<Message> Messages { get => _messages; set => SetProperty(ref _messages, value); }
    public string NewMessage 
    { 
        get => _newMessage; 
        set 
        { 
            SetProperty(ref _newMessage, value); 
            ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
            if (!_isSavedMessages)
            {
                _ = _socketService.SendTypingAsync(_otherUser);
            }
        } 
    }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public string OtherUser => _isSavedMessages ? "Избранное (вы)" : _otherUser;
    public bool IsSavedMessages => _isSavedMessages;
    public string CurrentUser => _authService.CurrentUsername ?? string.Empty;
    public bool IsOtherUserTyping { get => _isOtherUserTyping; set => SetProperty(ref _isOtherUserTyping, value); }
    public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

    public ICommand SendCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand LoadMoreMessagesCommand { get; }
    public ICommand EditMessageCommand { get; }
    public ICommand DeleteMessageCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SendFileCommand { get; }

    private async Task InitializeAsync()
    {
        await LoadMessagesAsync(false);

        if (_isSavedMessages)
            return;

        _socketService.OnNewMessage += OnSocketNewMessage;
        await _socketService.JoinChatAsync(_otherUser);

        if (_p2pService == null || _encryptionService == null)
            return;

        _socketService.OnP2PRequest += HandleP2PRequest;
        _socketService.OnP2PResponse += HandleP2PResponse;
        _socketService.OnUserTyping += OnUserTyping;
        _p2pService.MessageReceived += OnP2PMessageReceived;

        _typingTimer = new Timer(_ => IsOtherUserTyping = false, null, -1, -1);

        if (string.Compare(CurrentUser, _otherUser, StringComparison.Ordinal) > 0)
        {
            var port = new Random().Next(10000, 20000);
            _p2pService.StartListening(port);
            
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
            
            await _socketService.SendP2PRequestAsync(_otherUser, localIp, port, _encryptionService.PublicKey);
        }
    }

    private void OnSocketNewMessage(object? sender, Message msg)
    {
        if (Application.Current?.Dispatcher.CheckAccess() != true)
        {
            Application.Current?.Dispatcher.Invoke(() => OnSocketNewMessage(sender, msg));
            return;
        }

        if (_isSavedMessages) return;

        var involvesUs = (msg.Sender == CurrentUser && msg.Receiver == _otherUser)
                         || (msg.Sender == _otherUser && msg.Receiver == CurrentUser);
        if (!involvesUs) return;

        if (msg.Deleted)
        {
            var toRemove = Messages.FirstOrDefault(m => m.Id == msg.Id);
            if (toRemove != null)
                Messages.Remove(toRemove);
            return;
        }

        msg.IsSentByCurrentUser = msg.Sender == CurrentUser;
        msg.Status = msg.Read ? MessageStatus.Read : MessageStatus.Delivered;

        var existing = Messages.FirstOrDefault(m => m.Id == msg.Id);
        if (existing != null)
        {
            var idx = Messages.IndexOf(existing);
            Messages.RemoveAt(idx);
            Messages.Insert(idx, msg);
        }
        else
        {
            Messages.Add(msg);
        }
    }

    private void OnUserTyping(object? sender, string username)
    {
        if (username == _otherUser)
        {
            IsOtherUserTyping = true;
            _typingTimer?.Change(3000, -1);
        }
    }

    private async void HandleP2PRequest(object? sender, P2PConnectionInfo info)
    {
        if (info.User == CurrentUser && _p2pService != null && _encryptionService != null)
        {
            _encryptionService.DeriveSharedKey(info.PublicKey);
            await _p2pService.ConnectAsync(info.IpAddress, info.Port);
            
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
            
            await _socketService.SendP2PResponseAsync(_otherUser, localIp, 0, _encryptionService.PublicKey);
            _isP2PReady = true;
            ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
        }
    }

    private void HandleP2PResponse(object? sender, P2PConnectionInfo info)
    {
        if (info.User == CurrentUser && _encryptionService != null)
        {
            _encryptionService.DeriveSharedKey(info.PublicKey);
            _isP2PReady = true;
            ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
            System.Diagnostics.Debug.WriteLine("P2P Handshake complete.");
        }
    }

    private void OnP2PMessageReceived(object? sender, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Messages.Add(new Message
            {
                Sender = _otherUser,
                Content = message,
                Timestamp = DateTime.UtcNow.ToString("o"),
                IsSentByCurrentUser = false
            });
        });
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessage)) return;

        var messageContent = NewMessage.Trim();
        NewMessage = string.Empty;

        if (IsEditing)
        {
            await EditMessageAsync(messageContent);
            return;
        }

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            Sender = CurrentUser,
            Content = messageContent,
            Timestamp = DateTime.UtcNow.ToString("o"),
            IsSentByCurrentUser = true,
            Status = MessageStatus.Sending
        };
        
        Messages.Add(message);

        if (_isSavedMessages)
        {
            try
            {
                var payload = new { message = messageContent, type = "saved" };
                var response = await _apiService.PostAsync<object>("/api/chat/save", payload);
                if (response.Success)
                {
                    message.Status = MessageStatus.Sent;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save message error: {ex.Message}");
            }
        }
        else if (_isP2PReady && _p2pService != null)
        {
            await _p2pService.SendMessageAsync(messageContent);
            message.Status = MessageStatus.Sent;
        }
    }
    
    private async Task GoBackAndCleanupAsync()
    {
        _p2pService?.Disconnect();
        if (_socketService != null)
        {
            _socketService.OnNewMessage -= OnSocketNewMessage;
            _socketService.OnP2PRequest -= HandleP2PRequest;
            _socketService.OnP2PResponse -= HandleP2PResponse;
            _socketService.OnUserTyping -= OnUserTyping;
        }
        _typingTimer?.Dispose();
        await _mainViewModel.NavigateToDashboardAsync();
    }

    private async Task LoadMessagesAsync(bool loadMore)
    {
        if (IsLoading || (loadMore && _allMessagesLoaded)) return;

        IsLoading = true;
        if (loadMore) _currentPage++;
        else _currentPage = 0;

        try
        {
            var endpoint = _isSavedMessages 
                ? $"/api/chat/messages/saved?page={_currentPage}" 
                : $"/api/chat/messages/{_otherUser}?page={_currentPage}";
            
            var response = await _apiService.GetAsync<MessagesResponse>(endpoint);
            
            if (response.Success && response.Data?.Messages != null)
            {
                var newMessages = response.Data.Messages.Select(m => {
                    m.IsSentByCurrentUser = m.Sender == CurrentUser;
                    m.Status = m.Read ? MessageStatus.Read : MessageStatus.Delivered;
                    return m;
                }).ToList();

                if (loadMore)
                {
                    foreach (var msg in newMessages.AsEnumerable().Reverse())
                    {
                        Messages.Insert(0, msg);
                    }
                }
                else
                {
                    Messages = new ObservableCollection<Message>(newMessages);
                }

                if (newMessages.Count == 0)
                {
                    _allMessagesLoaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load messages error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            ((RelayCommand)LoadMoreMessagesCommand).RaiseCanExecuteChanged();
        }
    }

    private void StartEdit(Message? message)
    {
        if (message == null || message.Sender != CurrentUser) return;
        IsEditing = true;
        _editingMessageId = message.Id;
        NewMessage = message.Content;
    }

    private void CancelEdit()
    {
        IsEditing = false;
        _editingMessageId = null;
        NewMessage = string.Empty;
    }

    private async Task EditMessageAsync(string newContent)
    {
        var message = Messages.FirstOrDefault(m => m.Id == _editingMessageId);
        if (message == null) return;

        message.Content = newContent;
        message.Edited = true;
        
        CancelEdit();

        if (!_isP2PReady)
        {
            var payload = new { message_id = message.Id, new_content = newContent };
            await _apiService.PostAsync<object>("/api/chat/edit", payload);
        }
    }

    private async Task DeleteMessageAsync(Message? message)
    {
        if (message == null) return;

        Messages.Remove(message);

        if (!_isP2PReady)
        {
            var payload = new { message_id = message.Id };
            await _apiService.PostAsync<object>("/api/chat/delete", payload);
        }
    }

    private async Task SendFileAsync()
    {
        var dialog = new OpenFileDialog();
        if (dialog.ShowDialog() != true) return;

        var filePath = dialog.FileName;
        var fileName = Path.GetFileName(filePath);

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            Sender = CurrentUser,
            Content = $"Uploading {fileName}...",
            Timestamp = DateTime.UtcNow.ToString("o"),
            IsSentByCurrentUser = true,
            Status = MessageStatus.Sending
        };
        
        Messages.Add(message);

        var response = await _apiService.UploadFileAsync("/api/upload/file", filePath, fileName);

        if (response.Success && response.Data != null)
        {
            message.Content = "";
            message.FileId = response.Data.FileId;
            message.FileName = response.Data.FileName;
            message.FileType = response.Data.FileType;
            message.FileSize = response.Data.FileSize;
            message.FileUrl = response.Data.Url;
            message.Status = MessageStatus.Sent;

            if (!_isP2PReady)
            {
                var payload = new { receiver = _otherUser, type = "file", file_id = response.Data.FileId };
                await _apiService.PostAsync<object>("/api/chat/send", payload);
            }
        }
        else
        {
            message.Content = $"Failed to upload {fileName}";
        }
    }

    private class MessagesResponse { public List<Message> Messages { get; set; } = new(); }
}
