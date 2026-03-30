namespace MessengerSvyaz.Models;

public class SupportMessage
{
    public string Id { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Answered { get; set; }
    public bool IsGuest { get; set; }
    public string? GuestId { get; set; }
}