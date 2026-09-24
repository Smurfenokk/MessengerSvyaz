using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MessengerSvyaz.Services
{
    public class P2PService
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly EncryptionService _encryptionService;

        public event EventHandler<string>? MessageReceived;

        public P2PService(EncryptionService encryptionService)
        {
            _encryptionService = encryptionService;
        }

        public void StartListening(int port)
        {
            Task.Run(async () =>
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Any, port);
                    _listener.Start();
                    Console.WriteLine($"P2P: Listening on port {port}...");
                    _client = await _listener.AcceptTcpClientAsync();
                    _stream = _client.GetStream();
                    Console.WriteLine("P2P: Client connected.");
                    ReceiveMessages();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"P2P Listener Error: {ex.Message}");
                }
            });
        }

        public async Task ConnectAsync(string ipAddress, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(IPAddress.Parse(ipAddress), port);
                _stream = _client.GetStream();
                Console.WriteLine("P2P: Connected to peer.");
                ReceiveMessages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"P2P Connection Error: {ex.Message}");
            }
        }

        private void ReceiveMessages()
        {
            Task.Run(async () =>
            {
                if (_stream == null) return;
                try
                {
                    var buffer = new byte[4096];
                    while (true)
                    {
                        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;
                        var encryptedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        var decryptedMessage = _encryptionService.Decrypt(encryptedMessage);
                        MessageReceived?.Invoke(this, decryptedMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"P2P Receive Error: {ex.Message}");
                }
                finally
                {
                    Disconnect();
                }
            });
        }

        public async Task SendMessageAsync(string message)
        {
            if (_stream != null && _stream.CanWrite)
            {
                try
                {
                    var encryptedMessage = _encryptionService.Encrypt(message);
                    var buffer = Encoding.UTF8.GetBytes(encryptedMessage);
                    await _stream.WriteAsync(buffer, 0, buffer.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"P2P Send Error: {ex.Message}");
                }
            }
        }

        public void Disconnect()
        {
            _stream?.Dispose();
            _client?.Dispose();
            _listener?.Stop();
            Console.WriteLine("P2P: Disconnected.");
        }
    }
}
