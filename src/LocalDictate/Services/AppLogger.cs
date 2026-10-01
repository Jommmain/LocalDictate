using System.IO;
using System.Text;

namespace LocalDictate.Services;

public sealed class AppLogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public AppLogger(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {Sanitize(message)}{Environment.NewLine}";
        lock (_gate)
        {
            File.AppendAllText(_path, line, Encoding.UTF8);
        }
    }

    /// <summary>Avoid dumping long transcript bodies into logs.</summary>
    private static string Sanitize(string message)
    {
        if (message.Length <= 400) return message;
        return message[..400] + "…";
    }
}
