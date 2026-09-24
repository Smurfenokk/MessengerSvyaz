using System;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using MessengerSvyaz.Models;
using Newtonsoft.Json;

namespace MessengerSvyaz.Services;

public class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://svyaz.darkforce-sl.ru";
    private string? _sessionCookie;
    private bool _disposed;

    public ApiService()
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');
    
    public void SetSessionCookie(string cookie) => _sessionCookie = cookie;

    private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object? content = null)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{endpoint}");
        
        if (_sessionCookie != null)
        {
            request.Headers.Add("Cookie", _sessionCookie);
        }
        
        if (content != null)
        {
            var json = JsonConvert.SerializeObject(content);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        
        return request;
    }

    public async Task<(bool Success, string Message, T? Data)> PostAsync<T>(string endpoint, object content) where T : class
    {
        try
        {
            var request = CreateRequest(HttpMethod.Post, endpoint, content);
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            System.Diagnostics.Debug.WriteLine($"Response status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"Response content: {responseContent}");
            
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _sessionCookie = string.Join("; ", cookies);
            }
            
            if (responseContent.TrimStart().StartsWith("<"))
            {
                return (false, $"Server returned HTML instead of JSON. Status: {response.StatusCode}. Content: {responseContent.Substring(0, Math.Min(200, responseContent.Length))}", null);
            }
            
            var result = JsonConvert.DeserializeObject<ApiResponse<T>>(responseContent);
            
            if (result?.Status == "success")
            {
                return (true, result.Message ?? "Success", result.Data);
            }
            
            return (false, result?.Message ?? "Unknown error", null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostAsync error: {ex.Message}");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string Message, T? Data)> GetAsync<T>(string endpoint) where T : class
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, endpoint);
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            var result = JsonConvert.DeserializeObject<ApiResponse<T>>(content);
            
            if (result?.Status == "success")
            {
                return (true, result.Message ?? "Success", result.Data);
            }

            return (false, result?.Message ?? "Unknown error", null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, byte[]? Data, string? FileName)> DownloadFileAsync(string url)
    {
        try
        {
            var request = CreateRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, null);
            }
            
            var data = await response.Content.ReadAsByteArrayAsync();
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "download";
            
            return (true, data, fileName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Download file error: " + ex.Message);
            return (false, null, null);
        }
    }

    public async Task<(bool Success, string Message, UploadResult? Data)> UploadFileAsync(string endpoint, string filePath, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(File.OpenRead(filePath));
            content.Add(fileContent, "file", fileName);
            
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{endpoint}")
            {
                Content = content
            };
            
            if (_sessionCookie != null)
            {
                request.Headers.Add("Cookie", _sessionCookie);
            }
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            var result = JsonConvert.DeserializeObject<ApiResponse<UploadResult>>(responseContent);
            
            if (result?.Status == "success")
            {
                return (true, result.Message ?? "Success", result.Data);
            }
            
            return (false, result?.Message ?? "Upload failed", null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }

    public class ApiResponse<T>
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
        public T? Data { get; set; }
        [JsonProperty("redirect")]
        public string? Redirect { get; set; }
    }

    public class UploadResult
    {
        public string FileId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
