namespace MessengerSvyaz.Models;

public class Group
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Creator { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();
    public int MembersCount => Members.Count;
    public string Role { get; set; } = "member";
    public GroupSettings Settings { get; set; } = new();
}

public class GroupSettings
{
    public bool AllowReactions { get; set; } = true;
}