using TyriaUploader.Api;
using TyriaUploader.Config;
using TyriaUploader.Gw2Ei;
using TyriaUploader.OAuth;
using TyriaUploader.Watcher;

namespace TyriaUploader.UI;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Settings _settings;
    private readonly FileLogger _log;
    private readonly NotifyIcon _tray;
    private readonly HttpClient _http;
    private readonly ApiClient _api;
    private readonly OAuthClient _oauth;
    private readonly RecentUploads _recent;

    private readonly ContextMenuStrip _menu;
    private readonly ToolStripLabel _recentHeader;
    private readonly ToolStripSeparator _afterRecentSep;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _pause1h;
    private readonly ToolStripMenuItem _pause4h;
    private readonly ToolStripMenuItem _pauseIndef;
    private readonly ToolStripMenuItem _resumeItem;
    private readonly ToolStripMenuItem _rescanItem;
    private readonly ToolStripMenuItem _openHistoryItem;
    private readonly ToolStripMenuItem _openLogFileItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _quitItem;
    private readonly List<ToolStripItem> _recentEntries = new();

    private readonly Control _uiAnchor;
    private LogWatcher? _watcher;
    private SettingsForm? _settingsForm;
    private System.Threading.Timer? _resumeTimer;
    private string _activity = "Idle";

    public RecentUploads Recent => _recent;
    public bool IsPaused => _settings.PausedUntilUtc is { } u && (u == DateTime.MaxValue || DateTime.UtcNow < u);
    public string Activity => _activity;
    public event Action<string>? ActivityChanged;
    public event Action? PauseStateChanged;

    public TrayApplicationContext(Settings settings, FileLogger log)
    {
        _settings = settings;
        _log = log;

        _uiAnchor = new Control();
        _ = _uiAnchor.Handle;

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _api = new ApiClient(settings.ApiBaseUrl, _http);
        _oauth = new OAuthClient(settings.ApiBaseUrl, _http, settings.WebBaseUrl);
        _recent = new RecentUploads();

        var existingToken = TokenStore.Load();
        if (!string.IsNullOrEmpty(existingToken))
            _api.SetAccessToken(existingToken);

        _menu = new ContextMenuStrip
        {
            Renderer = new ToolStripProfessionalRenderer(new Theme.DarkMenuColors()) { RoundedEdges = false },
            BackColor = Theme.Surface,
            ForeColor = Theme.TextPrimary,

            ShowImageMargin = true,
        };

        _recentHeader = new ToolStripLabel("RECENT")
        {
            ForeColor = Theme.TextFaint,
            Font = new Font(Theme.LabelFont.FontFamily, 7.5f, FontStyle.Bold),
            Margin = new Padding(0, 4, 0, 2),
            Enabled = false,
        };

        _afterRecentSep = new ToolStripSeparator();

        _pauseItem   = MakeItem("Pause uploads", null, MenuIcons.Pause(Theme.TextPrimary));
        _pause1h     = MakeItem("Pause for 1 hour",  (_, _) => PauseFor(TimeSpan.FromHours(1)), null);
        _pause4h     = MakeItem("Pause for 4 hours", (_, _) => PauseFor(TimeSpan.FromHours(4)), null);
        _pauseIndef  = MakeItem("Pause until I resume", (_, _) => PauseFor(null), null);
        _resumeItem  = MakeItem("Resume now", (_, _) => ResumeNow(), MenuIcons.Power(Theme.Success));
        _pauseItem.DropDownItems.AddRange(new ToolStripItem[] { _pause1h, _pause4h, _pauseIndef });

        _rescanItem      = MakeItem("Rescan now",       (_, _) => DoRescan(),    MenuIcons.Refresh(Theme.TextPrimary));
        _openHistoryItem = MakeItem("Open log history", (_, _) => OpenHistory(), MenuIcons.List(Theme.TextPrimary));
        _openLogFileItem = MakeItem("Open log file",    (_, _) => OpenLogFile(), null);
        _settingsItem    = MakeItem("Settings…",        (_, _) => OpenSettings(),MenuIcons.Gear(Theme.TextPrimary));
        _quitItem        = MakeItem("Quit",             (_, _) => Quit(),        null);

        _recent.Changed += () => BeginInvokeOnMain(RebuildRecentInMenu);
        RebuildMenu();

        Icon trayIcon;
        try { trayIcon = Theme.LoadAppIcon(); }
        catch { trayIcon = SystemIcons.Application; }

        _tray = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "Tyria Uploader",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _tray.DoubleClick += (_, _) => OpenSettings();

        if (settings.StartWithWindows && !Autostart.IsEnabled())
            Autostart.SetEnabled(true);

        if (_settings.PausedUntilUtc is { } until && until != DateTime.MaxValue && DateTime.UtcNow >= until)
        {
            _settings.PausedUntilUtc = null;
            SettingsStore.Save(_settings);
        }
        if (IsPaused) SchedulePauseResumeIfDated();

        StartWatcherIfReady();

        if (string.IsNullOrEmpty(existingToken))
        {
            BeginInvokeOnMain(() => OpenSettings());
        }
    }

    private ToolStripMenuItem MakeItem(string text, EventHandler? handler, Image? image)
    {
        var item = new ToolStripMenuItem(text)
        {
            ForeColor = Theme.TextPrimary,
            Image = image,
        };
        if (handler != null) item.Click += handler;
        return item;
    }

    private void RebuildMenu()
    {
        _menu.SuspendLayout();
        _menu.Items.Clear();

        _menu.Items.Add(_recentHeader);
        RebuildRecentInMenu();
        _menu.Items.Add(_afterRecentSep);

        if (IsPaused)
        {

            _pauseItem.Text = PauseStatusText();
            _menu.Items.Add(_pauseItem);
            _menu.Items.Add(_resumeItem);
        }
        else
        {
            _pauseItem.Text = "Pause uploads";
            _menu.Items.Add(_pauseItem);
        }

        _menu.Items.Add(_rescanItem);
        _menu.Items.Add(_openHistoryItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_openLogFileItem);
        _menu.Items.Add(_quitItem);

        _menu.ResumeLayout();
    }

    private void RebuildRecentInMenu()
    {

        foreach (var item in _recentEntries)
        {
            _menu.Items.Remove(item);
            item.Dispose();
        }
        _recentEntries.Clear();

        var snapshot = _recent.Snapshot();
        int insertAt = _menu.Items.IndexOf(_recentHeader) + 1;
        if (insertAt <= 0)
        {

            return;
        }

        if (snapshot.Count == 0)
        {
            var empty = new ToolStripMenuItem("(no uploads yet)")
            {
                ForeColor = Theme.TextMuted,
                Enabled = false,
            };
            _menu.Items.Insert(insertAt, empty);
            _recentEntries.Add(empty);
            return;
        }

        int i = 0;
        foreach (var entry in snapshot.Take(6))
        {
            var label = !string.IsNullOrEmpty(entry.Boss)
                ? $"{entry.Boss}    {RelativeTime(entry.AtUtc)}"
                : $"{entry.FileName}    {RelativeTime(entry.AtUtc)}";

            bool ignorable = !string.IsNullOrEmpty(entry.Hash);
            var item = new ToolStripMenuItem
            {
                Text = label,
                ForeColor = OutcomeColor(entry.Outcome),
                Image = OutcomeIcon(entry.Outcome),
                Enabled = ignorable,
                ToolTipText = ignorable ? "Click to ignore this log on future scans" : null,
            };
            if (ignorable)
            {
                var hash = entry.Hash!;
                var boss = entry.Boss ?? Path.GetFileNameWithoutExtension(entry.FileName);
                item.Click += (_, _) => PromptIgnoreLog(boss, entry.AtUtc, hash);
            }
            _menu.Items.Insert(insertAt + i, item);
            _recentEntries.Add(item);
            i++;
        }
    }

    private void PromptIgnoreLog(string label, DateTime atUtc, string hash)
    {
        var when = atUtc.ToLocalTime().ToString("MMM d, HH:mm");
        var msg  = $"Stop uploading this log on future scans?\n\n{label} · {when}\n\n" +
                   "It will be silently skipped even if the file is moved or renamed. " +
                   "You can clear the ignore list by deleting ignored.json from %APPDATA%\\TyriaUploader\\.";
        var dr = MessageBox.Show(msg, "Tyria Uploader", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (dr != DialogResult.OK) return;
        _watcher?.IgnoreHash(hash);
    }

    public void PauseFor(TimeSpan? duration)
    {
        var until = duration.HasValue ? DateTime.UtcNow + duration.Value : DateTime.MaxValue;
        _settings.PausedUntilUtc = until;
        SettingsStore.Save(_settings);

        _watcher?.Stop();
        _watcher?.Dispose();
        _watcher = null;
        SetActivity(PauseStatusText());

        SchedulePauseResumeIfDated();
        RebuildMenu();
        PauseStateChanged?.Invoke();
    }

    public void ResumeNow()
    {
        _settings.PausedUntilUtc = null;
        SettingsStore.Save(_settings);
        _resumeTimer?.Dispose();
        _resumeTimer = null;
        RebuildMenu();
        PauseStateChanged?.Invoke();
        StartWatcherIfReady();
    }

    public void TogglePause()
    {
        if (IsPaused) ResumeNow();
        else PauseFor(null);
    }

    private void SchedulePauseResumeIfDated()
    {
        _resumeTimer?.Dispose();
        _resumeTimer = null;
        if (_settings.PausedUntilUtc is not { } until || until == DateTime.MaxValue) return;

        var remaining = until - DateTime.UtcNow;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        _resumeTimer = new System.Threading.Timer(_ =>
        {
            BeginInvokeOnMain(() =>
            {
                if (_settings.PausedUntilUtc is { } u && u != DateTime.MaxValue && DateTime.UtcNow >= u)
                    ResumeNow();
            });
        }, null, remaining, Timeout.InfiniteTimeSpan);
    }

    private string PauseStatusText()
    {
        if (_settings.PausedUntilUtc is not { } u) return "Pause uploads";
        if (u == DateTime.MaxValue) return "Paused";
        var local = u.ToLocalTime();
        return $"Paused until {local:HH:mm}";
    }

    private void StartWatcherIfReady()
    {
        if (IsPaused) { SetActivity(PauseStatusText()); return; }
        if (string.IsNullOrEmpty(TokenStore.Load()))
        {
            SetActivity("Not signed in");
            return;
        }
        var ei = new Gw2EiRunner(_settings.Gw2EiCliPath, _log);
        if (!ei.IsConfigured)
        {
            SetActivity("GW2EI not configured");
            return;
        }
        _watcher?.Dispose();
        _watcher = new LogWatcher(_settings, _log, ei, _api, _recent);
        _watcher.StatusChanged += SetActivity;
        _watcher.Start();
    }

    private void SetActivity(string status)
    {
        BeginInvokeOnMain(() =>
        {
            _activity = status;
            var text = $"Tyria Uploader, {status}";
            if (text.Length > 63) text = text[..63];
            _tray.Text = text;
            ActivityChanged?.Invoke(status);
        });
    }

    private void DoRescan()
    {
        if (_watcher == null) StartWatcherIfReady();
        _watcher?.Rescan();
    }

    public void RestartWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        StartWatcherIfReady();
    }

    public void ResetUploaderState()
    {
        _watcher?.ResetLocalCaches();
        _recent.Clear();
        RestartWatcher();
    }

    private void OpenSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(_settings, _log, _api, _oauth, this);
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        }
        _settingsForm.Show();
        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    private void OpenLogFile()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = SettingsStore.LogFilePath,
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private void OpenHistory()
    {
        try
        {
            var web = !string.IsNullOrWhiteSpace(_settings.WebBaseUrl)
                ? _settings.WebBaseUrl!
                : DeriveWebBase(_settings.ApiBaseUrl);
            var url = web.TrimEnd('/') + "/logs?tab=history";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private static string DeriveWebBase(string apiBaseUrl)
    {

        if (apiBaseUrl.Contains("localhost") || apiBaseUrl.Contains("127.0.0.1"))
            return "http://localhost:5173";
        return apiBaseUrl;
    }

    private void Quit()
    {
        _watcher?.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    private static Image? OutcomeIcon(RecentUploads.Outcome o) => o switch
    {
        RecentUploads.Outcome.Uploaded     => MenuIcons.Check(Theme.Success),
        RecentUploads.Outcome.Wipe         => MenuIcons.Cross(Theme.Danger),
        RecentUploads.Outcome.Deduplicated => MenuIcons.Dot(Theme.TextMuted),
        RecentUploads.Outcome.Skipped      => MenuIcons.Dot(Theme.TextMuted),
        RecentUploads.Outcome.Failed       => MenuIcons.Cross(Theme.Danger),
        _ => null,
    };

    internal static string OutcomeGlyph(RecentUploads.Outcome o) => o switch
    {
        RecentUploads.Outcome.Uploaded     => "✓",
        RecentUploads.Outcome.Wipe         => "✗",
        RecentUploads.Outcome.Deduplicated => "•",
        RecentUploads.Outcome.Skipped      => "⊘",
        RecentUploads.Outcome.Failed       => "✗",
        _ => "·",
    };

    internal static Color OutcomeColor(RecentUploads.Outcome o) => o switch
    {
        RecentUploads.Outcome.Uploaded     => Theme.Success,
        RecentUploads.Outcome.Wipe         => Theme.Danger,
        RecentUploads.Outcome.Deduplicated => Theme.TextMuted,
        RecentUploads.Outcome.Skipped      => Theme.TextMuted,
        RecentUploads.Outcome.Failed       => Theme.Danger,
        _ => Theme.TextMuted,
    };

    internal static string RelativeTime(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalSeconds < 30)  return "just now";
        if (diff.TotalMinutes < 1)   return $"{(int)diff.TotalSeconds}s";
        if (diff.TotalMinutes < 60)  return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalHours < 24)    return $"{(int)diff.TotalHours}h";
        if (diff.TotalDays < 7)      return $"{(int)diff.TotalDays}d";
        return utc.ToLocalTime().ToString("MMM d");
    }

    internal static string FightTimeFromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (name.Length >= 15 && name[8] == '-'
            && int.TryParse(name.AsSpan(0, 4), out var y)
            && int.TryParse(name.AsSpan(4, 2), out var mo)
            && int.TryParse(name.AsSpan(6, 2), out var d)
            && int.TryParse(name.AsSpan(9, 2), out var h)
            && int.TryParse(name.AsSpan(11, 2), out var mi))
        {
            try
            {
                var dt = new DateTime(y, mo, d, h, mi, 0);
                return dt.Date == DateTime.Today
                    ? dt.ToString("HH:mm")
                    : dt.ToString("MMM d, HH:mm");
            }
            catch { }
        }
        return fileName;
    }

    private void BeginInvokeOnMain(Action a)
    {
        if (_uiAnchor.IsHandleCreated && !_uiAnchor.IsDisposed)
            _uiAnchor.BeginInvoke(a);
        else
            a();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _resumeTimer?.Dispose();
            _watcher?.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _uiAnchor.Dispose();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
