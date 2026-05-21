using System.Text.Json;
using System.Text.Json.Serialization;
using TyriaUploader.Config;

namespace TyriaUploader.Watcher;

public sealed class RetryQueue
{
    public sealed record Entry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("attempts")] int Attempts,
        [property: JsonPropertyName("nextAttemptUtc")] DateTime NextAttemptUtc,
        [property: JsonPropertyName("lastError")] string? LastError);

    private const int MaxAttempts = 10;
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _file;

    public RetryQueue()
    {
        _file = Path.Combine(SettingsStore.AppDataDir, "retry_queue.json");
        Load();
    }

    public bool ShouldAttemptNow(string path)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(path, out var e)) return true;
            if (e.Attempts >= MaxAttempts) return false;
            return DateTime.UtcNow >= e.NextAttemptUtc;
        }
    }

    public IReadOnlyList<string> GetDuePaths()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            return _entries.Values
                .Where(e => e.Attempts < MaxAttempts && now >= e.NextAttemptUtc)
                .Select(e => e.Path)
                .ToArray();
        }
    }

    public void RecordFailure(string path, string? error)
    {
        lock (_lock)
        {
            _entries.TryGetValue(path, out var prev);
            var attempts = (prev?.Attempts ?? 0) + 1;
            var backoffSeconds = Math.Min(MaxBackoff.TotalSeconds, 30.0 * Math.Pow(2, attempts - 1));
            var next = DateTime.UtcNow.AddSeconds(backoffSeconds);
            _entries[path] = new Entry(path, attempts, next, error);
            Save();
        }
    }

    public void RecordSuccess(string path)
    {
        lock (_lock)
        {
            if (_entries.Remove(path)) Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var json = File.ReadAllText(_file);
            var arr = JsonSerializer.Deserialize<Entry[]>(json);
            if (arr == null) return;
            foreach (var e in arr) _entries[e.Path] = e;
        }
        catch {  }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.AppDataDir);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries.Values.ToArray()));
            File.Move(tmp, _file, overwrite: true);
        }
        catch { }
    }
}
