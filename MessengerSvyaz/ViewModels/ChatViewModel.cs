using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private readonly string _otherUser;
    private readonly bool _isSavedMessages;

    private ObservableCollection<Message> _messages = new();
    private string _newMessage = string.Empty;
    private bool _isLoading;
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

        SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(NewMessage));
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

        if (_isSavedMessages) return;

        _socketService.OnNewMessage += OnNewMessageReceived;
        _socketService.OnMessageEdited += OnMessageEditedReceived;
        _socketService.OnMessageDeleted += OnMessageDeletedReceived;
        _socketService.OnMessageRead += OnMessageReadReceived;
        _socketService.OnUserTyping += OnUserTyping;

        _typingTimer = new Timer(state => IsOtherUserTyping = false, null, -1, -1);

        await _socketService.JoinChatAsync(_otherUser);
    }

    private void OnUserTyping(object? sender, string username)
    {
        if (username == _otherUser)
        {
            IsOtherUserTyping = true;
            _typingTimer?.Change(3000, -1);
        }
    }

    private void OnNewMessageReceived(object? sender, Message msg)
    {
        if (msg.Sender != _otherUser && msg.Receiver != _otherUser) return;
        if (msg.Sender == CurrentUser) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            msg.IsSentByCurrentUser = false;
            msg.Status = MessageStatus.Delivered;
            Messages.Add(msg);
        });
    }

    private void OnMessageEditedReceived(object? sender, (string messageId, string newContent) data)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == data.messageId);
            if (msg != null)
            {
                msg.Content = data.newContent;
                msg.Edited = true;
            }
        });
    }

    private void OnMessageDeletedReceived(object? sender, string messageId)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null) Messages.Remove(msg);
        });
    }

    private void OnMessageReadReceived(object? sender, (string otherUser, string messageId) data)
    {
        if (data.otherUser != _otherUser) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var msg = Messages.FirstOrDefault(m => m.Id == data.messageId);
            if (msg != null) msg.Status = MessageStatus.Read;
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
                System.Diagnostics.Debug.WriteLine("Save message error: " + ex.Message);
            }
        }
        else
        {
            try
            {
                var payload = new { receiver = _otherUser, message = messageContent, type = "text" };
                var response = await _apiService.PostAsync<object>("/api/chat/send", payload);
                if (response.Success)
                {
                    message.Status = MessageStatus.Sent;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Send message error: " + ex.Message);
                message.Status = MessageStatus.Sending;
            }
        }
    }

    private async Task GoBackAndCleanupAsync()
    {
        if (_socketService != null)
        {
            _socketService.OnNewMessage -= OnNewMessageReceived;
            _socketService.OnMessageEdited -= OnMessageEditedReceived;
            _socketService.OnMessageDeleted -= OnMessageDeletedReceived;
            _socketService.OnMessageRead -= OnMessageReadReceived;
            _socketService.OnUserTyping -= OnUserTyping;
        }
        _typingTimer?.Dispose();
        _typingTimer = null;
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
                ? "/api/chat/messages/saved?page=" + _currentPage
                : "/api/chat/messages/" + _otherUser + "?page=" + _currentPage;

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
            System.Diagnostics.Debug.WriteLine("Load messages error: " + ex.Message);
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

        var payload = new { message_id = message.Id, new_content = newContent };
        await _apiService.PostAsync<object>("/api/chat/edit", payload);
    }

    private async Task DeleteMessageAsync(Message? message)
    {
        if (message == null) return;

        Messages.Remove(message);

        var payload = new { message_id = message.Id };
        await _apiService.PostAsync<object>("/api/chat/delete", payload);
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
            Content = "Uploading " + fileName + "...",
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

            var payload = new { receiver = _otherUser, type = "file", file_id = response.Data.FileId, message = "" };
            await _apiService.PostAsync<object>("/api/chat/send", payload);
        }
        else
        {
            message.Content = "Failed to upload " + fileName;
        }
    }

    private class MessagesResponse { public List<Message> Messages { get; set; } = new(); }
}
