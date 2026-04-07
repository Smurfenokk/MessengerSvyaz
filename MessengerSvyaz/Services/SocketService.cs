using System.Collections.Generic;
using MessengerSvyaz.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SocketIOClient;
using SocketIOClient.Serializer.NewtonsoftJson;
using SocketIOClient.Transport;

namespace MessengerSvyaz.Services;

public class P2PConnectionInfo
{
    public string User { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public byte[] PublicKey { get; set; } = [];
}

public class SocketService
{
    private SocketIO? _socket;
    private string _baseUrl = "https://svyaz.darkforce-sl.ru";

    public event EventHandler<Message>? OnNewMessage;
    public event EventHandler<GroupMessage>? OnNewGroupMessage;
    public event EventHandler<string>? OnUserTyping;
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;

    public event EventHandler<P2PConnectionInfo>? OnP2PRequest;
    public event EventHandler<P2PConnectionInfo>? OnP2PResponse;

    public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

    /// <param name="sessionCookie">Тот же cookie сессии, что и у <see cref="ApiService"/> после логина.</param>
    public async Task ConnectAsync(string username, string? sessionCookie = null)
    {
        await DisconnectAsync();

        var uri = new Uri(_baseUrl);
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(sessionCookie))
            headers["Cookie"] = sessionCookie;

        var jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() }
        };

        var options = new SocketIOOptions
        {
            Path = "/socket.io",
            Transport = TransportProtocol.WebSocket,
            AutoUpgrade = false,
            ExtraHeaders = headers,
            Reconnection = true
        };

        _socket = new SocketIO(uri, options, services =>
        {
            services.AddNewtonsoftJson(jsonSettings);
        });

        _socket.OnConnected += (_, _) => OnConnected?.Invoke(this, EventArgs.Empty);
        _socket.OnDisconnected += (_, _) => OnDisconnected?.Invoke(this, EventArgs.Empty);

        _socket.On("new_message", OnNewMessagePacketAsync);
        _socket.On("new_group_message", OnNewGroupMessagePacketAsync);
        _socket.On("user_typing", OnUserTypingPacketAsync);
        _socket.On("p2p_request", OnP2PRequestPacketAsync);
        _socket.On("p2p_response", OnP2PResponsePacketAsync);

        await _socket.ConnectAsync();
    }

    private Task OnNewMessagePacketAsync(IEventContext ctx)
    {
        try
        {
            var msg = ctx.GetValue<Message>(0);
            if (msg != null)
                OnNewMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket new_message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnNewGroupMessagePacketAsync(IEventContext ctx)
    {
        try
        {
            var msg = ctx.GetValue<GroupMessage>(0);
            if (msg != null)
                OnNewGroupMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket new_group_message: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnUserTypingPacketAsync(IEventContext ctx)
    {
        try
        {
            var user = ctx.GetValue<string>(0);
            if (!string.IsNullOrEmpty(user))
                OnUserTyping?.Invoke(this, user);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket user_typing: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnP2PRequestPacketAsync(IEventContext ctx)
    {
        try
        {
            var info = ctx.GetValue<P2PConnectionInfo>(0);
            if (info != null)
                OnP2PRequest?.Invoke(this, info);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket p2p_request: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private Task OnP2PResponsePacketAsync(IEventContext ctx)
    {
        try
        {
            var info = ctx.GetValue<P2PConnectionInfo>(0);
            if (info != null)
                OnP2PResponse?.Invoke(this, info);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Socket p2p_response: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public async Task SendP2PRequestAsync(string targetUser, string ipAddress, int port, byte[] publicKey)
    {
        if (_socket?.Connected != true) return;

        var info = new P2PConnectionInfo { User = targetUser, IpAddress = ipAddress, Port = port, PublicKey = publicKey };
        await _socket.EmitAsync("p2p_request", new[] { info });
    }

    public async Task SendP2PResponseAsync(string targetUser, string ipAddress, int port, byte[] publicKey)
    {
        if (_socket?.Connected != true) return;

        var info = new P2PConnectionInfo { User = targetUser, IpAddress = ipAddress, Port = port, PublicKey = publicKey };
        await _socket.EmitAsync("p2p_response", new[] { info });
    }

    public async Task JoinChatAsync(string otherUser)
    {
        if (_socket?.Connected != true) return;

        try
        {
            await _socket.EmitAsync("join_chat", new[] { new { other_user = otherUser } });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Join chat error: {ex.Message}");
        }
    }

    public async Task JoinGroupAsync(string groupId)
    {
        if (_socket?.Connected != true) return;

        try
        {
            await _socket.EmitAsync("join_group", new[] { new { group_id = groupId } });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Join group error: {ex.Message}");
        }
    }

    public async Task SendTypingAsync(string otherUser)
    {
        if (_socket?.Connected != true) return;

        try
        {
            await _socket.EmitAsync("typing", new[] { new { other_user = otherUser } });
        }
        catch
        {
            // ignore
        }
    }

    public async Task DisconnectAsync()
    {
        if (_socket == null) return;

        try
        {
            await _socket.DisconnectAsync();
        }
        catch
        {
            // ignore
        }
        finally
        {
            _socket.Dispose();
            _socket = null;
        }
    }

    public bool IsConnected => _socket?.Connected == true;
}
