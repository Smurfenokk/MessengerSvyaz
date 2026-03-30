using System.Collections.ObjectModel;
using System.Windows.Input;
using MessengerSvyaz.Models;
using MessengerSvyaz.Services;

namespace MessengerSvyaz.ViewModels;

public class SupportViewModel : BaseViewModel
{
    private readonly AuthService _authService;
    private readonly ApiService _apiService;
    private readonly MainViewModel _mainViewModel;
    
    private ObservableCollection<SupportMessage> _messages = new();
    private string _newMessage = string.Empty;
    private bool _isLoading;

    public SupportViewModel(AuthService authService, ApiService apiService, MainViewModel mainViewModel)
    {
        _authService = authService;
        _apiService = apiService;
        _mainViewModel = mainViewModel;
        
        SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !string.IsNullOrWhiteSpace(NewMessage));
        GoBackCommand = new RelayCommand(async _ => await _mainViewModel.NavigateToDashboardAsync());
        RefreshCommand = new RelayCommand(async _ => await LoadMessagesAsync());
        
        _ = LoadMessagesAsync();
    }

    public ObservableCollection<SupportMessage> Messages
    {
        get => _messages;
        set => SetProperty(ref _messages, value);
    }

    public string NewMessage
    {
        get => _newMessage;
        set
        {
            SetProperty(ref _newMessage, value);
            ((RelayCommand)SendCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand SendCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand RefreshCommand { get; }

    private async Task LoadMessagesAsync()
    {
        IsLoading = true;
        
        try
        {
            var response = await _apiService.GetAsync<SupportMessagesResponse>("/api/support/messages");
            
            if (response.Success && response.Data?.Messages != null)
            {
                Messages = new ObservableCollection<SupportMessage>(response.Data.Messages);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load support messages error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessage)) return;

        try
        {
            var payload = new { message = NewMessage.Trim() };
            var response = await _apiService.PostAsync<object>("/api/support/send", payload);
            
            if (response.Success)
            {
                NewMessage = string.Empty;
                await LoadMessagesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Send support message error: {ex.Message}");
        }
    }

    private class SupportMessagesResponse
    {
        public List<SupportMessage> Messages { get; set; } = new();
    }
}