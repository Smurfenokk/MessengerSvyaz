using System;
using System.IO;
using System.Windows.Input;
using MessengerSvyaz.Services;
using Microsoft.Win32;

namespace MessengerSvyaz.ViewModels;

public class ProfileViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly MainViewModel _mainViewModel;
    
    private string _bio = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private string? _avatarUrl;

    public ProfileViewModel(AuthService authService, ApiService apiService, MainViewModel mainViewModel)
    {
        _authService = authService;
        _apiService = apiService;
        _mainViewModel = mainViewModel;
        
        SaveBioCommand = new RelayCommand(async _ => await SaveBioAsync());
        ChangePasswordCommand = new RelayCommand(async _ => await ChangePasswordAsync());
        UploadAvatarCommand = new RelayCommand(async _ => await UploadAvatarAsync());
        GoBackCommand = new RelayCommand(async _ => await _mainViewModel.NavigateToDashboardAsync());
        
        _ = LoadProfileAsync();
    }

    public string Username => _authService.CurrentUsername ?? "User";

    public string Bio
    {
        get => _bio;
        set => SetProperty(ref _bio, value);
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetProperty(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? AvatarUrl
    {
        get => _avatarUrl;
        set => SetProperty(ref _avatarUrl, value);
    }

    public ICommand SaveBioCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand UploadAvatarCommand { get; }
    public ICommand GoBackCommand { get; }

    private async Task LoadProfileAsync()
    {
        try
        {
            var response = await _apiService.GetAsync<ProfileResponse>($"/api/profile");
            
            if (response.Success && response.Data != null)
            {
                Bio = response.Data.Bio ?? "";
                if (response.Data.HasAvatar && !string.IsNullOrEmpty(response.Data.AvatarUrl))
                {
                    AvatarUrl = response.Data.AvatarUrl + "?t=" + DateTime.Now.Ticks;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load profile error: {ex.Message}");
        }
    }

    private async Task SaveBioAsync()
    {
        if (Bio.Length > 30)
        {
            StatusMessage = "Описание не должно превышать 30 символов";
            return;
        }

        IsLoading = true;
        
        try
        {
            var payload = new { bio = Bio };
            var response = await _apiService.PostAsync<object>("/api/profile/update", payload);
            
            StatusMessage = response.Success ? "Профиль обновлен" : $"Ошибка: {response.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "Заполните все поля";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "Пароли не совпадают";
            return;
        }

        if (NewPassword.Length < 8)
        {
            StatusMessage = "Новый пароль должен быть не менее 8 символов";
            return;
        }

        IsLoading = true;
        
        try
        {
            var payload = new
            {
                current_password = CurrentPassword,
                new_password = NewPassword
            };
            
            var response = await _apiService.PostAsync<object>("/api/profile/change_password", payload);
            
            if (response.Success)
            {
                StatusMessage = "Пароль изменен";
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            else
            {
                StatusMessage = $"Ошибка: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UploadAvatarAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp",
            Title = "Выберите аватар"
        };

        if (dialog.ShowDialog() != true) return;

        IsLoading = true;
        
        try
        {
            var response = await _apiService.UploadFileAsync("/api/upload/avatar", dialog.FileName, Path.GetFileName(dialog.FileName));
            
            if (response.Success && response.Data != null)
            {
                AvatarUrl = response.Data.Url + "?t=" + DateTime.Now.Ticks;
                StatusMessage = "Аватар обновлен";
            }
            else
            {
                StatusMessage = $"Ошибка: {response.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private class ProfileResponse
    {
        public string? Bio { get; set; }
        public bool HasAvatar { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
