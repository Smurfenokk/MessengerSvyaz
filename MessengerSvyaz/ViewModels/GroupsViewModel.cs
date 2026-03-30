using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MessengerSvyaz.Models;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class GroupsViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly SocketService _socketService;
    private readonly MainViewModel _mainViewModel;
    
    private ObservableCollection<Group> _groups = new();
    private string _newGroupName = string.Empty;
    private string _newGroupDescription = string.Empty;
    private bool _allowReactions = true;
    private string _joinGroupId = string.Empty;
    private bool _isLoading;

    public GroupsViewModel(AuthService authService, ApiService apiService, SocketService socketService, MainViewModel mainViewModel)
    {
        _authService = authService;
        _apiService = apiService;
        _socketService = socketService;
        _mainViewModel = mainViewModel;
        
        RefreshCommand = new RelayCommand(async _ => await LoadGroupsAsync());
        CreateGroupCommand = new RelayCommand(async _ => await CreateGroupAsync());
        JoinGroupCommand = new RelayCommand(async _ => await JoinGroupAsync());
        OpenGroupCommand = new RelayCommand(group => OpenGroup(group as Group));
        GoBackCommand = new RelayCommand(async _ => await _mainViewModel.NavigateToDashboardAsync());
        
        _ = LoadGroupsAsync();
    }

    public ObservableCollection<Group> Groups
    {
        get => _groups;
        set => SetProperty(ref _groups, value);
    }

    public string NewGroupName
    {
        get => _newGroupName;
        set => SetProperty(ref _newGroupName, value);
    }

    public string NewGroupDescription
    {
        get => _newGroupDescription;
        set => SetProperty(ref _newGroupDescription, value);
    }

    public bool AllowReactions
    {
        get => _allowReactions;
        set => SetProperty(ref _allowReactions, value);
    }

    public string JoinGroupId
    {
        get => _joinGroupId;
        set => SetProperty(ref _joinGroupId, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand CreateGroupCommand { get; }
    public ICommand JoinGroupCommand { get; }
    public ICommand OpenGroupCommand { get; }
    public ICommand GoBackCommand { get; }

    private async Task LoadGroupsAsync()
    {
        IsLoading = true;
        
        try
        {
            var response = await _apiService.GetAsync<GroupsResponse>("/api/groups/my");
            
            if (response.Success && response.Data?.Groups != null)
            {
                foreach (var group in response.Data.Groups)
                {
                    var infoResponse = await _apiService.GetAsync<GroupInfoResponse>($"/api/group/info/{group.Id}");
                    if (infoResponse.Success && infoResponse.Data != null)
                    {
                        group.Members = infoResponse.Data.Members;
                    }
                }
                Groups = new ObservableCollection<Group>(response.Data.Groups);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load groups error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
        {
            return;
        }

        IsLoading = true;
        
        try
        {
            var payload = new
            {
                name = NewGroupName.Trim(),
                description = NewGroupDescription.Trim(),
                settings = new { allow_reactions = AllowReactions }
            };
            
            var response = await _apiService.PostAsync<CreateGroupResponse>("/api/groups/create", payload);
            
            if (response.Success)
            {
                NewGroupName = string.Empty;
                NewGroupDescription = string.Empty;
                AllowReactions = true;
                await LoadGroupsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Create group error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task JoinGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinGroupId))
        {
            return;
        }

        IsLoading = true;
        
        try
        {
            var payload = new { group_id = JoinGroupId.Trim() };
            var response = await _apiService.PostAsync<object>("/api/groups/join", payload);
            
            if (response.Success)
            {
                JoinGroupId = string.Empty;
                await LoadGroupsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Join group error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenGroup(Group? group)
    {
        if (group != null)
        {
            _mainViewModel.NavigateToGroupChat(group.Id, group.Name);
        }
    }

    private class GroupsResponse
    {
        public List<Group> Groups { get; set; } = new();
    }

    private class CreateGroupResponse
    {
        public string GroupId { get; set; } = string.Empty;
    }
    
    private class GroupInfoResponse
    {
        public List<string> Members { get; set; } = new();
    }
}
