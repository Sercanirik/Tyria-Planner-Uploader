using System.Collections.Concurrent;
using System.Text.Json;
using TyriaUploader.Api;
using TyriaUploader.Config;
using TyriaUploader.Gw2Ei;

namespace TyriaUploader.Watcher;

public sealed class LogWatcher : IDisposable
{
    private readonly Settings _settings;
    private readonly FileLogger _log;
    private readonly Gw2EiRunner _ei;
    private readonly ApiClient _api;
    private readonly RecentUploads _recent;

    private FileSystemWatcher? _fsw;
    private System.Threading.Timer? _rescanTimer;

    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private CancellationTokenSource? _cts;
    private Task? _worker;

    private readonly object _stateLock = new();

    private Dictionary<string, DateTime> _uploadedHashes = new();

    private HashSet<string> _ignoredHashes = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, long> _seenPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _seenPathsSaveLock = new();

    public event Action<string>? StatusChanged;

    public LogWatcher(Settings settings, FileLogger log, Gw2EiRunner ei, ApiClient api, RecentUploads recent)
    {
        _settings = settings;
        _log = log;
        _ei = ei;
        _api = api;
        _recent = recent;
        LoadState();
        LoadSeenPaths();
        LoadIgnoredHashes();
    }

    public bool IsRunning => _worker is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning) return;
        if (!Directory.Exists(_settings.LogFolder))
        {
            _log.Warn($"Log folder does not exist: {_settings.LogFolder}");
            StatusChanged?.Invoke("Log folder not found");
            return;
        }

        _cts = new CancellationTokenSource();

        _fsw = new FileSystemWatcher(_settings.LogFolder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _fsw.Created += (_, e) => Enqueue(e.FullPath);
        _fsw.Renamed += (_, e) => Enqueue(e.FullPath);

        var interval = TimeSpan.FromSeconds(Math.Max(15, _settings.RescanIntervalSeconds));
        _rescanTimer = new System.Threading.Timer(_ => Rescan(), null, interval, interval);

        _worker = Task.Run(() => WorkerLoopAsync(_cts.Token));

        StatusChanged?.Invoke("Watching");
        _log.Info($"Started watching {_settings.LogFolder}");

        _ = Task.Run(() => PrimeKnownHashesAsync(_cts.Token), _cts.Token);

        _ = Task.Run(() => PrimeIgnoredHashesAsync(_cts.Token), _cts.Token);

        Rescan();
    }

    private async Task PrimeKnownHashesAsync(CancellationToken ct)
    {
        try
        {
            var hashes = await _api.GetKnownHashesAsync(ct);
            if (hashes == null || hashes.Count == 0) return;
            var stamp = DateTime.UtcNow;
            int added = 0;
            lock (_stateLock)
            {
                foreach (var h in hashes)
                {
                    if (_uploadedHashes.ContainsKey(h)) continue;
                    _uploadedHashes[h] = stamp;
                    added++;
                }
                if (added > 0) SaveState();
            }
            if (added > 0) _log.Info($"Primed dedup cache with {added} hashes from server");
        }
        catch (Exception ex)
        {
            _log.Warn($"Known-hashes prime failed: {ex.Message}");
        }
    }

    private async Task PrimeIgnoredHashesAsync(CancellationToken ct)
    {
        try
        {
            var hashes = await _api.GetIgnoredHashesAsync(ct);
            if (hashes == null || hashes.Count == 0) return;
            int added = 0;
            lock (_stateLock)
            {
                foreach (var h in hashes)
                {
                    if (_ignoredHashes.Add(h)) added++;
                }
                if (added > 0) SaveIgnoredHashes();
            }
            if (added > 0) _log.Info($"Primed ignore list with {added} hashes from server");
        }
        catch (Exception ex)
        {
            _log.Warn($"Ignored-hashes prime failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_fsw != null)
        {
            _fsw.EnableRaisingEvents = false;
            _fsw.Dispose();
            _fsw = null;
        }
        _rescanTimer?.Dispose();
        _rescanTimer = null;
        try { _worker?.Wait(2000); } catch { }
        _worker = null;
        StatusChanged?.Invoke("Stopped");
    }

    private void Enqueue(string path)
    {
        if (!IsLogFile(path)) return;
        _queue.Enqueue(path);
        try { _signal.Release(); } catch { }
    }

    public void Rescan()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(_settings.LogFolder, "*.*", SearchOption.AllDirectories))
            {
                if (!IsLogFile(f)) continue;

                try
                {
                    var fi = new FileInfo(f);
                    if (_seenPaths.TryGetValue(f, out var seenLen) && seenLen == fi.Length)
                        continue;
                }
                catch {  }
                Enqueue(f);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Rescan failed: {ex.Message}");
        }
    }

    private static string? ExtractBoss(string logPath)
    {
        var dir = Path.GetDirectoryName(logPath);
        if (string.IsNullOrEmpty(dir)) return null;
        var folder = Path.GetFileName(dir);
        if (string.IsNullOrEmpty(folder)) return null;
        var paren = folder.IndexOf(" (", StringComparison.Ordinal);
        return paren > 0 ? folder[..paren] : folder;
    }

    private static bool IsLogFile(string path)
    {
        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".zevtc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".evtc", StringComparison.OrdinalIgnoreCase);
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {

        var inFlight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException) { break; }

            while (_queue.TryDequeue(out var path))
            {
                if (!inFlight.Add(path)) continue;
                try
                {
                    if (_settings.UploadOnlyIfGw2Running && !IsGw2Running())
                    {

                        continue;
                    }

                    try
                    {
                        var fi = new FileInfo(path);
                        if (fi.Exists
                            && _seenPaths.TryGetValue(path, out var seenLen)
                            && seenLen == fi.Length)
                        {
                            continue;
                        }
                    }
                    catch {  }

                    bool stable;
                    try
                    {
                        var fi = new FileInfo(path);
                        stable = fi.Exists
                            && fi.Length > 0
                            && (DateTime.UtcNow - fi.LastWriteTimeUtc) > TimeSpan.FromMinutes(2);
                    }
                    catch { stable = false; }

                    if (!stable && !await WaitUntilStableAsync(path, ct)) continue;

                    var fileName = Path.GetFileName(path);
                    var boss = ExtractBoss(path);
                    var hash = await LogHash.ComputeAsync(path, ct);
                    lock (_stateLock)
                    {
                        if (_ignoredHashes.Contains(hash))
                        {

                            _log.Info($"Skip (user-ignored): {fileName}");
                            RememberPath(path);
                            continue;
                        }
                        if (_uploadedHashes.ContainsKey(hash))
                        {
                            _log.Info($"Skip (already uploaded): {fileName}");
                            _recent.Add(fileName, boss, RecentUploads.Outcome.Deduplicated, hash);
                            RememberPath(path);
                            continue;
                        }
                    }

                    StatusChanged?.Invoke($"Parsing {fileName}");
                    var parsed = await _ei.ParseAsync(path, ct);
                    if (!parsed.Success || parsed.JsonText == null)
                    {
                        _log.Warn($"Parse failed for {path}: {parsed.Error}");
                        _recent.Add(fileName, boss, RecentUploads.Outcome.Failed, hash);

                        RememberPath(path);
                        continue;
                    }

                    var slimJson = EiJsonSlimmer.Slim(parsed.JsonText);
                    if (slimJson.Length < parsed.JsonText.Length / 2)
                        _log.Info($"Slimmed EI JSON {parsed.JsonText.Length / 1024} KB → {slimJson.Length / 1024} KB for {fileName}");

                    StatusChanged?.Invoke($"Uploading {fileName}");
                    var result = await _api.UploadEiJsonAsync(slimJson, hash, ct);
                    if (result.Success)
                    {

                        if (result.Skipped && result.Reason == "ignored")
                        {
                            _log.Info($"Server-ignored: {fileName}");
                            IgnoreHash(hash);
                            RememberPath(path);
                            continue;
                        }
                        RecentUploads.Outcome outcome;
                        if (result.Wipe)              { _log.Info($"Uploaded (wipe): {fileName}"); outcome = RecentUploads.Outcome.Wipe; }
                        else if (result.Skipped)      { _log.Info($"Skipped: {fileName}");          outcome = RecentUploads.Outcome.Skipped; }
                        else if (result.Deduplicated) { _log.Info($"Deduplicated: {fileName}");     outcome = RecentUploads.Outcome.Deduplicated; }
                        else                          { _log.Info($"Uploaded: {fileName}");         outcome = RecentUploads.Outcome.Uploaded; }
                        _recent.Add(fileName, boss, outcome, hash);
                        lock (_stateLock)
                        {
                            _uploadedHashes[hash] = DateTime.UtcNow;
                            SaveState();
                        }
                        RememberPath(path);
                    }
                    else
                    {
                        _log.Warn($"Upload failed for {fileName}: {result.Error}");
                        _recent.Add(fileName, boss, RecentUploads.Outcome.Failed, hash);

                        if (result.StatusCode is >= 400 and < 500)
                            RememberPath(path);
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _log.Error($"Processing {path} failed", ex);
                }
                finally
                {
                    inFlight.Remove(path);
                }
            }
        }
    }

    private static async Task<bool> WaitUntilStableAsync(string path, CancellationToken ct)
    {
        long lastSize = -1;
        int stableTicks = 0;
        for (int i = 0; i < 60 && !ct.IsCancellationRequested; i++)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return false;
                if (fi.Length > 0 && fi.Length == lastSize)
                {
                    if (++stableTicks >= 3) return true;
                }
                else
                {
                    stableTicks = 0;
                    lastSize = fi.Length;
                }
            }
            catch { }
            await Task.Delay(1000, ct);
        }
        return false;
    }

    private static bool IsGw2Running()
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName("Gw2-64").Length > 0
                || System.Diagnostics.Process.GetProcessesByName("Gw2").Length > 0;
        }
        catch { return false; }
    }

    private void LoadState()
    {
        try
        {
            if (!File.Exists(SettingsStore.StateFile)) return;
            var json = File.ReadAllText(SettingsStore.StateFile);

            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
                if (dict != null)
                {
                    _uploadedHashes = dict;
                    return;
                }
            }
            catch (JsonException)
            {

            }

            var arr = JsonSerializer.Deserialize<string[]>(json);
            if (arr != null)
            {
                var stamp = File.GetLastWriteTimeUtc(SettingsStore.StateFile);
                _uploadedHashes = arr.ToDictionary(h => h, _ => stamp);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to load uploaded state: {ex.Message}");
        }
    }

    private void SaveState()
    {
        try
        {

            const int maxEntries = 5000;
            IEnumerable<KeyValuePair<string, DateTime>> entries = _uploadedHashes;
            if (_uploadedHashes.Count > maxEntries)
                entries = _uploadedHashes.OrderByDescending(kv => kv.Value).Take(maxEntries);

            var snapshot = entries.ToDictionary(kv => kv.Key, kv => kv.Value);
            var json = JsonSerializer.Serialize(snapshot);

            var target = SettingsStore.StateFile;
            var tmp = target + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, target, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to save uploaded state: {ex.Message}");
        }
    }

    private void RememberPath(string path)
    {
        try
        {
            var len = new FileInfo(path).Length;
            _seenPaths[path] = len;
            SaveSeenPaths();
        }
        catch {  }
    }

    private void LoadSeenPaths()
    {
        try
        {
            if (!File.Exists(SettingsStore.SeenPathsFile)) return;
            var json = File.ReadAllText(SettingsStore.SeenPathsFile);
            var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
            if (dict == null) return;
            foreach (var kv in dict) _seenPaths[kv.Key] = kv.Value;
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to load seen-paths cache: {ex.Message}");
        }
    }

    private void SaveSeenPaths()
    {

        Dictionary<string, long> snapshot;
        lock (_seenPathsSaveLock)
        {
            snapshot = _seenPaths.ToArray()
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        try
        {
            var json = JsonSerializer.Serialize(snapshot);
            var target = SettingsStore.SeenPathsFile;
            var tmp = target + ".tmp";
            lock (_seenPathsSaveLock)
            {
                File.WriteAllText(tmp, json);
                File.Move(tmp, target, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to save seen-paths cache: {ex.Message}");
        }
    }

    public bool IsIgnored(string hash)
    {
        lock (_stateLock) return _ignoredHashes.Contains(hash);
    }

    public void IgnoreHash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return;
        lock (_stateLock)
        {
            if (!_ignoredHashes.Add(hash)) return;
            SaveIgnoredHashes();
        }
        _log.Info($"Ignored hash {hash[..Math.Min(12, hash.Length)]}…");
    }

    public void UnignoreHash(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return;
        lock (_stateLock)
        {
            if (!_ignoredHashes.Remove(hash)) return;
            SaveIgnoredHashes();
        }
    }

    private void LoadIgnoredHashes()
    {
        try
        {
            if (!File.Exists(SettingsStore.IgnoredHashesFile)) return;
            var json = File.ReadAllText(SettingsStore.IgnoredHashesFile);
            var arr = JsonSerializer.Deserialize<string[]>(json);
            if (arr == null) return;
            _ignoredHashes = new HashSet<string>(arr, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to load ignored hashes: {ex.Message}");
        }
    }

    private void SaveIgnoredHashes()
    {
        try
        {
            var target = SettingsStore.IgnoredHashesFile;
            var tmp = target + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_ignoredHashes.ToArray()));
            File.Move(tmp, target, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to save ignored hashes: {ex.Message}");
        }
    }

    public void ResetLocalCaches()
    {
        lock (_stateLock)
        {
            _uploadedHashes.Clear();
            _ignoredHashes.Clear();
            TryDelete(SettingsStore.StateFile);
            TryDelete(SettingsStore.IgnoredHashesFile);
        }
        _seenPaths.Clear();
        TryDelete(SettingsStore.SeenPathsFile);
        _log.Info("Local upload caches cleared by user request");
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch {  }
    }

    public void Dispose() => Stop();
}
