using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class GroupInfoViewModel : BaseViewModel
{
    private readonly MainViewModel _mainViewModel;
    private readonly ApiService _apiService;
    private readonly string _groupId;
    
    private string _groupName = string.Empty;
    private string _creatorName = string.Empty;
    private int _membersCount;
    private bool _isCreator;
    private List<string> _members = new();

    public GroupInfoViewModel(MainViewModel mainViewModel, ApiService apiService, string groupId, string groupName, string creatorName, List<string> members, bool isCreator)
    {
        _mainViewModel = mainViewModel;
        _apiService = apiService;
        _groupId = groupId;
        _groupName = groupName;
        _creatorName = creatorName;
        _members = members;
        _membersCount = members.Count;
        _isCreator = isCreator;
        
        GoBackCommand = new RelayCommand(_ => _mainViewModel.NavigateToGroupChat(groupId, groupName));
        RemoveMemberCommand = new RelayCommand(async member => await RemoveMemberAsync(member as string));
    }

    public string GroupName
    {
        get => _groupName;
        set => SetProperty(ref _groupName, value);
    }

    public string CreatorName
    {
        get => _creatorName;
        set => SetProperty(ref _creatorName, value);
    }

    public int MembersCount
    {
        get => _membersCount;
        set => SetProperty(ref _membersCount, value);
    }

    public string GroupIdDisplay => _groupId;

    public bool IsCreator
    {
        get => _isCreator;
        set => SetProperty(ref _isCreator, value);
    }

    public List<string> Members
    {
        get => _members;
        set => SetProperty(ref _members, value);
    }

    public ICommand GoBackCommand { get; }
    public ICommand RemoveMemberCommand { get; }

    private async Task RemoveMemberAsync(string? username)
    {
        if (string.IsNullOrEmpty(username)) return;

        var payload = new { group_id = _groupId, username_to_remove = username };
        var response = await _apiService.PostAsync<object>("/api/groups/remove_member", payload);

        if (response.Success)
        {
            Members.Remove(username);
            MembersCount = Members.Count;
        }
    }
}
