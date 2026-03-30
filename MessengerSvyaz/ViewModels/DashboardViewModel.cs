using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MessengerSvyaz.Models;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly SocketService _socketService;
    private readonly MainViewModel _mainViewModel;
    
    private ObservableCollection<User> _users = new();
    private ObservableCollection<ChatPreview> _recentChats = new();
    private string _searchQuery = string.Empty;
    private bool _isLoading;

    public DashboardViewModel(AuthService authService, ApiService apiService, SocketService socketService, MainViewModel mainViewModel)
    {
        _authService = authService;
        _apiService = apiService;
        _socketService = socketService;
        _mainViewModel = mainViewModel;
        
        SearchCommand = new RelayCommand(async _ => await SearchUsersAsync());
        StartChatCommand = new RelayCommand(user => StartChat(user as User));
        OpenChatCommand = new RelayCommand(chat => OpenChat(chat as ChatPreview));
        OpenSavedCommand = new RelayCommand(_ => OpenSavedMessages());
        NavigateProfileCommand = new RelayCommand(_ => _mainViewModel.NavigateToProfile());
        NavigateGroupsCommand = new RelayCommand(_ => _mainViewModel.NavigateToGroups());
        NavigateSupportCommand = new RelayCommand(_ => _mainViewModel.NavigateToSupport());
        LogoutCommand = new RelayCommand(async _ => await _mainViewModel.LogoutAsync());
        
        _ = LoadDataAsync();
    }

    public ObservableCollection<User> Users
    {
        get => _users;
        set => SetProperty(ref _users, value);
    }

    public ObservableCollection<ChatPreview> RecentChats
    {
        get => _recentChats;
        set => SetProperty(ref _recentChats, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            SetProperty(ref _searchQuery, value);
            _ = SearchUsersAsync();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string CurrentUsername => _authService.CurrentUsername ?? "User";

    public ICommand SearchCommand { get; }
    public ICommand StartChatCommand { get; }
    public ICommand OpenChatCommand { get; }
    public ICommand OpenSavedCommand { get; }
    public ICommand NavigateProfileCommand { get; }
    public ICommand NavigateGroupsCommand { get; }
    public ICommand NavigateSupportCommand { get; }
    public ICommand LogoutCommand { get; }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        try
        {
            var response = await _apiService.GetAsync<RecentChatsResponse>("/api/chat/recent");
            
            if (response.Success && response.Data?.Chats != null)
            {
                var filtered = response.Data.Chats.Where(c => c.Username != _authService.CurrentUsername).ToList();
                RecentChats = new ObservableCollection<ChatPreview>(filtered);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading chats: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchUsersAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) 
        {
            Users.Clear();
            return;
        }

        try
        {
            var response = await _apiService.GetAsync<UsersListResponse>($"/api/users/search?query={SearchQuery.Trim()}");
            
            if (response.Success && response.Data?.Users != null)
            {
                var found = response.Data.Users
                    .Where(u => u.Username != _authService.CurrentUsername)
                    .ToList();
                Users = new ObservableCollection<User>(found);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error searching users: {ex.Message}");
        }
    }

    private void StartChat(User? user)
    {
        if (user != null)
        {
            _mainViewModel.NavigateToChat(user.Username);
        }
    }

    private void OpenChat(ChatPreview? chat)
    {
        if (chat != null)
        {
            _mainViewModel.NavigateToChat(chat.Username);
        }
    }

    private void OpenSavedMessages()
    {
        _mainViewModel.NavigateToChat(_authService.CurrentUsername!);
    }

    private class UsersListResponse
    {
        public List<User> Users { get; set; } = new();
    }

    private class RecentChatsResponse
    {
        public List<ChatPreview> Chats { get; set; } = new();
    }
}
