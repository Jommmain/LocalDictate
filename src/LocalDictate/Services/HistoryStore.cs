using System.IO;
using System.Text.Json;

namespace LocalDictate.Services;

public sealed class HistoryEntry
{
    public DateTimeOffset At { get; set; }
    public string Text { get; set; } = string.Empty;
    public double AudioSeconds { get; set; }
}

public sealed class HistoryStore
{
    private readonly string _path;
    private readonly int _limit;
    private readonly object _gate = new();
    private readonly List<HistoryEntry> _entries = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public HistoryStore(string path, int limit)
    {
        _path = path;
        _limit = Math.Clamp(limit, 1, 500);
        Load();
    }

    public IReadOnlyList<HistoryEntry> Items
    {
        get
        {
            lock (_gate) return _entries.ToList();
        }
    }

    public void Add(string text, double audioSeconds)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        lock (_gate)
        {
            _entries.Insert(0, new HistoryEntry
            {
                At = DateTimeOffset.Now,
                Text = text.Trim(),
                AudioSeconds = audioSeconds,
            });
            while (_entries.Count > _limit)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
            SaveUnlocked();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions);
            if (items is null) return;
            lock (_gate)
            {
                _entries.Clear();
                _entries.AddRange(items.Take(_limit));
            }
        }
        catch
        {
            // ignore corrupt history
        }
    }

    private void SaveUnlocked()
    {
        var json = JsonSerializer.Serialize(_entries, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
