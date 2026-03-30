namespace MessengerSvyaz.Services;

public class AuthService
{
    private string? _currentUsername;
    private bool _isAdmin;
    private string? _sessionId;

    public string? CurrentUsername => _currentUsername;
    public bool IsAdmin => _isAdmin;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_currentUsername);

    public void Login(string username, bool isAdmin, string? sessionId = null)
    {
        _currentUsername = username;
        _isAdmin = isAdmin;
        _sessionId = sessionId;
    }

    public void Logout()
    {
        _currentUsername = null;
        _isAdmin = false;
        _sessionId = null;
    }

    public void UpdateAdminStatus(bool isAdmin) => _isAdmin = isAdmin;
}