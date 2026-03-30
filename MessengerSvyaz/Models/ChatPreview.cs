namespace MessengerSvyaz.Models;

public class ChatPreview
{
    public string Username { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public string LastTime { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public bool Online { get; set; }
}
