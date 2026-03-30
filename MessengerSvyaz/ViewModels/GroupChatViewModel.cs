using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MessengerSvyaz.Models;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class GroupChatViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly SocketService _socketService;
    private readonly MainViewModel _mainViewModel;
    private readonly string _groupId;
    private readonly string _groupName;
    
    private ObservableCollection<GroupMessage> _messages = new();
    private ObservableCollection<string> _members = new();
    private string _newMessage = string.Empty;
    private bool _isLoading;
    private bool _isCreator;
    private bool _allowReactions;
    private int _currentPage = 0;
    private bool _allMessagesLoaded = false;

    public GroupChatViewModel(AuthService authService, ApiService apiService, SocketService socketService,
        MainViewModel mainViewModel, string groupId, string groupName)
    {
        _authService = authService;
        _apiService = apiService;
        _socketService = socketService;
        _mainViewModel = mainViewModel;
        _groupId = groupId;
        _groupName = groupName;
        
        SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(NewMessage));
        GoBackCommand = new RelayCommand(_ => _mainViewModel.NavigateToGroups());
        LoadMoreMessagesCommand = new RelayCommand(async _ => await LoadMessagesAsync(true), _ => !_isLoading && !_allMessagesLoaded);
        AddReactionCommand = new RelayCommand(async param => await AddReactionAsync(param));
        OpenInfoCommand = new RelayCommand(_ => OpenGroupInfo());
        
        _ = InitializeAsync();
    }

    public ObservableCollection<GroupMessage> Messages { get => _messages; set => SetProperty(ref _messages, value); }
    public ObservableCollection<string> Members { get => _members; set => SetProperty(ref _members, value); }
    public string NewMessage { get => _newMessage; set { SetProperty(ref _newMessage, value); ((RelayCommand)SendCommand).RaiseCanExecuteChanged(); } }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public bool IsCreator { get => _isCreator; set => SetProperty(ref _isCreator, value); }
    public bool AllowReactions { get => _allowReactions; set => SetProperty(ref _allowReactions, value); }
    public string GroupName => _groupName;
    public string GroupId => _groupId;
    public string CurrentUser => _authService.CurrentUsername ?? string.Empty;
    public int MembersCount => Members.Count;
    public string CreatorName { get; private set; } = string.Empty;

    public ICommand SendCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand LoadMoreMessagesCommand { get; }
    public ICommand AddReactionCommand { get; }
    public ICommand OpenInfoCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            await _socketService.JoinGroupAsync(_groupId);
            await LoadMessagesAsync(false);
            await LoadGroupInfoAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Group chat init error: {ex.Message}");
        }
    }

    private async Task LoadMessagesAsync(bool loadMore)
    {
        if (IsLoading || (loadMore && _allMessagesLoaded)) return;

        IsLoading = true;
        if (loadMore) _currentPage++;
        else _currentPage = 0;
        
        try
        {
            var response = await _apiService.GetAsync<GroupMessagesResponse>($"/api/group/messages/{_groupId}?page={_currentPage}");
            
            if (response.Success && response.Data != null)
            {
                var newMessages = response.Data.Messages;

                if (loadMore)
                {
                    foreach (var msg in newMessages.AsEnumerable().Reverse())
                    {
                        Messages.Insert(0, msg);
                    }
                }
                else
                {
                    Messages = new ObservableCollection<GroupMessage>(newMessages);
                }

                if (newMessages.Count == 0)
                {
                    _allMessagesLoaded = true;
                }

                AllowReactions = response.Data.AllowReactions;
                IsCreator = response.Data.IsAdmin;
                Members = new ObservableCollection<string>(response.Data.Members);
                CreatorName = response.Data.Creator;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load group messages error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            ((RelayCommand)LoadMoreMessagesCommand).RaiseCanExecuteChanged();
        }
    }

    private async Task LoadGroupInfoAsync()
    {
        try
        {
            var response = await _apiService.GetAsync<GroupInfoResponse>($"/api/group/info/{_groupId}");
            if (response.Success && response.Data != null)
            {
                CreatorName = response.Data.Creator;
                Members = new ObservableCollection<string>(response.Data.Members);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load group info error: {ex.Message}");
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessage)) return;

        try
        {
            var payload = new
            {
                group_id = _groupId,
                message = NewMessage.Trim()
            };

            var response = await _apiService.PostAsync<object>("/api/group/send", payload);
            
            if (response.Success)
            {
                NewMessage = string.Empty;
                await LoadMessagesAsync(false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Send group message error: {ex.Message}");
        }
    }

    private async Task AddReactionAsync(object? param)
    {
        if (!AllowReactions || param is not object[] arr || arr.Length != 2) return;
        var messageId = arr[0] as string;
        var emoji = arr[1] as string;
        
        if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(emoji)) return;

        try
        {
            var payload = new
            {
                group_id = _groupId,
                message_id = messageId,
                emoji = emoji
            };

            await _apiService.PostAsync<object>("/api/group/reaction", payload);
            await LoadMessagesAsync(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Add reaction error: {ex.Message}");
        }
    }

    private void OpenGroupInfo()
    {
        _mainViewModel.NavigateToGroupInfo(_groupId, _groupName, CreatorName, Members.ToList());
    }

    private class GroupMessagesResponse
    {
        public List<GroupMessage> Messages { get; set; } = new();
        public bool IsAdmin { get; set; }
        public bool AllowReactions { get; set; }
        public List<string> Members { get; set; } = new();
        public string Creator { get; set; } = string.Empty;
    }

    private class GroupInfoResponse
    {
        public string Creator { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new();
    }
}
