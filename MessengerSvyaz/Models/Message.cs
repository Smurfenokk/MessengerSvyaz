using System.Collections.Generic;

namespace MessengerSvyaz.Models;

public enum MessageStatus
{
    Sending,
    Sent,
    Delivered,
    Read
}

public class Message
{
    public string Id { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Receiver { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public bool Edited { get; set; }
    public bool Deleted { get; set; }
    public bool Read { get; set; }
    public string? ReplyTo { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? FileUrl { get; set; }
    public Dictionary<string, List<string>>? Reactions { get; set; }
    
    public bool IsSentByCurrentUser { get; set; }
    public MessageStatus Status { get; set; }
}
