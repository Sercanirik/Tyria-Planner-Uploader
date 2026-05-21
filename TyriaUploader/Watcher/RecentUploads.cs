using System.Text.Json;
using System.Text.Json.Serialization;
using TyriaUploader.Config;

namespace TyriaUploader.Watcher;

public sealed class RecentUploads
{
    public enum Outcome { Uploaded, Deduplicated, Skipped, Failed, Wipe }

    public sealed record Entry(
        [property: JsonPropertyName("file")] string FileName,
        [property: JsonPropertyName("boss")] string? Boss,
        [property: JsonPropertyName("outcome")] Outcome Outcome,
        [property: JsonPropertyName("atUtc")] DateTime AtUtc,

        [property: JsonPropertyName("hash")] string? Hash = null);

    private const int MaxEntries = 20;
    private readonly object _lock = new();
    private readonly LinkedList<Entry> _entries = new();
    private readonly string _file;

    public event Action? Changed;

    public RecentUploads()
    {
        _file = Path.Combine(SettingsStore.AppDataDir, "recent.json");
        Load();
    }

    public IReadOnlyList<Entry> Snapshot()
    {
        lock (_lock) return _entries.ToArray();
    }

    public void Add(string fileName, string? boss, Outcome outcome, string? hash = null)
    {
        lock (_lock)
        {
            _entries.AddFirst(new Entry(fileName, boss, outcome, DateTime.UtcNow, hash));
            while (_entries.Count > MaxEntries) _entries.RemoveLast();
            Save();
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            try { if (File.Exists(_file)) File.Delete(_file); } catch {  }
        }
        Changed?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var json = File.ReadAllText(_file);
            var arr = JsonSerializer.Deserialize<Entry[]>(json);
            if (arr == null) return;
            foreach (var e in arr) _entries.AddLast(e);
        }
        catch {  }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.AppDataDir);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries.ToArray()));
            File.Move(tmp, _file, overwrite: true);
        }
        catch {  }
    }
}
