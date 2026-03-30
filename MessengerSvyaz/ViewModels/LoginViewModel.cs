using System.Windows.Input;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly MainViewModel _mainViewModel;
    
    private string _username = string.Empty;
    private string _password = string.Empty;
    
    private string _regFirstName = string.Empty;
    private string _regLastName = string.Empty;
    private string _regMiddleName = string.Empty;
    private string _regUsername = string.Empty;
    private string _regPassword = string.Empty;
    private string _regKey = string.Empty;
    
    private string _errorMessage = string.Empty;
    private bool _isRegistering;
    private bool _isLoading;

    public LoginViewModel(AuthService authService, ApiService apiService, MainViewModel mainViewModel)
    {
        _authService = authService;
        _apiService = apiService;
        _mainViewModel = mainViewModel;
        
        LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsLoading);
        RegisterCommand = new RelayCommand(async _ => await RegisterAsync(), _ => !IsLoading);
        ToggleModeCommand = new RelayCommand(_ => ToggleMode());
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string RegFirstName
    {
        get => _regFirstName;
        set => SetProperty(ref _regFirstName, value);
    }

    public string RegLastName
    {
        get => _regLastName;
        set => SetProperty(ref _regLastName, value);
    }

    public string RegMiddleName
    {
        get => _regMiddleName;
        set => SetProperty(ref _regMiddleName, value);
    }

    public string RegUsername
    {
        get => _regUsername;
        set => SetProperty(ref _regUsername, value);
    }

    public string RegPassword
    {
        get => _regPassword;
        set => SetProperty(ref _regPassword, value);
    }

    public string RegKey
    {
        get => _regKey;
        set => SetProperty(ref _regKey, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsRegistering
    {
        get => _isRegistering;
        set => SetProperty(ref _isRegistering, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RegisterCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand ToggleModeCommand { get; }

    private void ToggleMode()
    {
        IsRegistering = !IsRegistering;
        ErrorMessage = string.Empty;
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите имя пользователя и пароль";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var formData = new Dictionary<string, string>
            {
                ["username"] = Username.Trim(),
                ["password"] = Password
            };

            var response = await _apiService.PostAsync<LoginResponse>("/login", formData);
            
            if (response.Success)
            {
                _authService.Login(Username.Trim(), response.Data?.IsAdmin ?? false, response.Data?.SessionId);
                await _mainViewModel.NavigateToDashboardAsync();
            }
            else
            {
                ErrorMessage = response.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка подключения: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(RegFirstName) || string.IsNullOrWhiteSpace(RegUsername) || 
            string.IsNullOrWhiteSpace(RegPassword) || string.IsNullOrWhiteSpace(RegKey))
        {
            ErrorMessage = "Заполните обязательные поля (Имя, Имя пользователя, Пароль, Ключ)";
            return;
        }

        if (RegUsername.Length < 3 || RegUsername.Length > 32)
        {
            ErrorMessage = "Имя пользователя должно быть от 3 до 32 символов";
            return;
        }

        if (RegPassword.Length < 8)
        {
            ErrorMessage = "Пароль должен быть не менее 8 символов";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var displayName = RegFirstName;
            if (!string.IsNullOrWhiteSpace(RegLastName))
                displayName += " " + RegLastName;

            var formData = new Dictionary<string, string>
            {
                ["username"] = RegUsername.Trim(),
                ["password"] = RegPassword,
                ["key"] = RegKey.Trim(),
                ["first_name"] = RegFirstName.Trim(),
                ["last_name"] = RegLastName?.Trim() ?? "",
                ["middle_name"] = RegMiddleName?.Trim() ?? "",
                ["display_name"] = displayName
            };

            var response = await _apiService.PostAsync<RegisterResponse>("/register", formData);
            
            if (response.Success)
            {
                ErrorMessage = "Регистрация успешна! Войдите в систему.";
                IsRegistering = false;
                Username = RegUsername.Trim();
                Password = string.Empty;
                RegFirstName = string.Empty;
                RegLastName = string.Empty;
                RegMiddleName = string.Empty;
                RegUsername = string.Empty;
                RegPassword = string.Empty;
                RegKey = string.Empty;
            }
            else
            {
                ErrorMessage = response.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка подключения: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private class LoginResponse
    {
        public bool IsAdmin { get; set; }
        public string? SessionId { get; set; }
    }

    private class RegisterResponse
    {
        public string? Key { get; set; }
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}