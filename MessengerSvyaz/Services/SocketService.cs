using Microsoft.AspNetCore.SignalR.Client;
using MessengerSvyaz.Models;
using System;
using System.Threading.Tasks;

namespace MessengerSvyaz.Services;

public class P2PConnectionInfo
{
    public required string User { get; init; }
    public required string IpAddress { get; init; }
    public int Port { get; init; }
    public required byte[] PublicKey { get; init; }
}

public class SocketService
{
    private HubConnection? _connection;
    private string _baseUrl = "https://svyaz.darkforce-sl.ru";
    
    public event EventHandler<Message>? OnNewMessage;
    public event EventHandler<GroupMessage>? OnNewGroupMessage;
    public event EventHandler<string>? OnUserTyping;
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    
    public event EventHandler<P2PConnectionInfo>? OnP2PRequest;
    public event EventHandler<P2PConnectionInfo>? OnP2PResponse;

    public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

    public async Task ConnectAsync(string username)
    {
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_baseUrl}/socket.io/?EIO=4&transport=websocket")
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            _connection.On<Message>("new_message", msg => OnNewMessage?.Invoke(this, msg));
            _connection.On<GroupMessage>("new_group_message", msg => OnNewGroupMessage?.Invoke(this, msg));
            _connection.On<string>("user_typing", user => OnUserTyping?.Invoke(this, user));

            _connection.On<P2PConnectionInfo>("p2p_request", info => OnP2PRequest?.Invoke(this, info));
            _connection.On<P2PConnectionInfo>("p2p_response", info => OnP2PResponse?.Invoke(this, info));

            _connection.Reconnecting += error => 
            {
                OnDisconnected?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            };

            _connection.Reconnected += connectionId =>
            {
                OnConnected?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            };

            await _connection.StartAsync();
            OnConnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket connection error: {ex.Message}");
            throw;
        }
    }

    public async Task SendP2PRequestAsync(string targetUser, string ipAddress, int port, byte[] publicKey)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            var info = new P2PConnectionInfo { User = targetUser, IpAddress = ipAddress, Port = port, PublicKey = publicKey };
            await _connection.InvokeAsync("p2p_request", info);
        }
    }

    public async Task SendP2PResponseAsync(string targetUser, string ipAddress, int port, byte[] publicKey)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            var info = new P2PConnectionInfo { User = targetUser, IpAddress = ipAddress, Port = port, PublicKey = publicKey };
            await _connection.InvokeAsync("p2p_response", info);
        }
    }

    public async Task JoinChatAsync(string otherUser)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("join_chat", new { other_user = otherUser });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Join chat error: {ex.Message}");
            }
        }
    }

    public async Task JoinGroupAsync(string groupId)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("join_group", new { group_id = groupId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Join group error: {ex.Message}");
            }
        }
    }

    public async Task SendTypingAsync(string otherUser)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await _connection.InvokeAsync("typing", new { other_user = otherUser });
            }
            catch { }
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
            catch { }
            finally
            {
                _connection = null;
            }
        }
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;
}
