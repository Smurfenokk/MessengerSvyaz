namespace MessengerSvyaz.Models;

public class RegistrationKey
{
    public string Key { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public string? UsedBy { get; set; }
    public string? UsedAt { get; set; }
}