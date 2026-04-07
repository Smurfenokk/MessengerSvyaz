using Newtonsoft.Json;

namespace MessengerSvyaz.Models;

public class GroupMessage
{
    public string Id { get; set; } = string.Empty;

    [JsonProperty("group_id")]
    public string? GroupId { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = "user";
    public bool Edited { get; set; }
    public bool Deleted { get; set; }
    public string? ReplyTo { get; set; }
    public string? FileType { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? FileUrl { get; set; }
    public Dictionary<string, List<string>>? Reactions { get; set; }
}