using System.IO;
using Microsoft.Win32;

namespace MessengerSvyaz.Services;

public class FileService
{
    public string? SelectFileToUpload(string filter = "All Files|*.*")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            CheckPathExists = true
        };
        
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectSaveLocation(string defaultFileName, string filter = "All Files|*.*")
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter
        };
        
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public async Task SaveFileAsync(byte[] data, string path)
    {
        await File.WriteAllBytesAsync(path, data);
    }

    public string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {sizes[order]}";
    }

    public bool IsImageFile(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLower();
        return new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" }.Contains(ext);
    }

    public bool IsAudioFile(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLower();
        return new[] { ".mp3", ".wav", ".ogg", ".webm", ".m4a", ".flac" }.Contains(ext);
    }

    public bool IsVideoFile(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLower();
        return new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm", ".flv" }.Contains(ext);
    }
}