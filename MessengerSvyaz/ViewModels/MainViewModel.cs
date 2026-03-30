using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly SocketService _socketService;
    
    private BaseViewModel? _currentViewModel;
    private bool _isAuthenticated;

    public MainViewModel()
    {
        _authService = new AuthService();
        _apiService = new ApiService();
        _socketService = new SocketService();
        
        NavigateToLogin();
    }

    public BaseViewModel? CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    public void NavigateToLogin()
    {
        CurrentViewModel = new LoginViewModel(_authService, _apiService, this);
        IsAuthenticated = false;
    }

    public async Task NavigateToDashboardAsync()
    {
        if (!_authService.IsAuthenticated) return;
        
        CurrentViewModel = new DashboardViewModel(_authService, _apiService, _socketService, this);
        IsAuthenticated = true;
        
        try
        {
            if (_authService.CurrentUsername != null)
            {
                await _socketService.ConnectAsync(_authService.CurrentUsername);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket connection failed: {ex.Message}");
        }
    }

    public void NavigateToChat(string otherUser)
    {
        if (!_authService.IsAuthenticated) return;
        CurrentViewModel = new ChatViewModel(_authService, _apiService, _socketService, this, otherUser);
    }

    public void NavigateToProfile()
    {
        if (!_authService.IsAuthenticated) return;
        CurrentViewModel = new ProfileViewModel(_authService, _apiService, this);
    }

    public void NavigateToGroups()
    {
        if (!_authService.IsAuthenticated) return;
        CurrentViewModel = new GroupsViewModel(_authService, _apiService, _socketService, this);
    }

    public void NavigateToGroupChat(string groupId, string groupName)
    {
        if (!_authService.IsAuthenticated) return;
        CurrentViewModel = new GroupChatViewModel(_authService, _apiService, _socketService, this, groupId, groupName);
    }

    public void NavigateToGroupInfo(string groupId, string groupName, string creatorName, List<string> members)
    {
        if (!_authService.IsAuthenticated) return;
        var isCreator = creatorName == _authService.CurrentUsername;
        CurrentViewModel = new GroupInfoViewModel(this, _apiService, groupId, groupName, creatorName, members, isCreator);
    }

    public void NavigateToSupport()
    {
        if (!_authService.IsAuthenticated) return;
        CurrentViewModel = new SupportViewModel(_authService, _apiService, this);
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _socketService.DisconnectAsync();
        }
        catch { }
        _authService.Logout();
        NavigateToLogin();
    }
}
