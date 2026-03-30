namespace MessengerSvyaz.Models;

public class User
{
    public string Username { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsOnline { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public string? BlockedBy { get; set; }
    public string? BlockedAt { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string? LastLogin { get; set; }
    public string? Bio { get; set; }
    public bool HasAvatar { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsVisible { get; set; } = true;
}