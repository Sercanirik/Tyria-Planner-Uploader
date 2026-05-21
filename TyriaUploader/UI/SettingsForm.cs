using TyriaUploader.Api;
using TyriaUploader.Config;
using TyriaUploader.OAuth;
using TyriaUploader.Watcher;

namespace TyriaUploader.UI;

public sealed class SettingsForm : Form
{
    private readonly Settings _settings;
    private readonly FileLogger _log;
    private readonly ApiClient _api;
    private readonly TrayApplicationContext _ctx;

    private readonly Theme.StatusDot _statusDot = new();
    private readonly Label _statusTitle;
    private readonly Label _statusSub;
    private readonly Theme.RoundedButton _signInButton;
    private readonly Theme.RoundedButton _signOutButton;
    private readonly Theme.RoundedButton _pauseButton;
    private FlowLayoutPanel _statusActions = null!;
    private readonly Theme.FieldHost _logFolderField;
    private readonly Theme.FieldHost _gw2EiField;
    private readonly CheckBox _autostartCheck;
    private readonly CheckBox _onlyIfGw2Check;
    private readonly CheckBox _uploadWipesCheck;
    private FlowLayoutPanel _recentList = null!;

    public SettingsForm(Settings settings, FileLogger log, ApiClient api, OAuthClient _, TrayApplicationContext ctx)
    {
        _settings = settings;
        _log = log;
        _api = api;
        _ctx = ctx;

        Text = "Tyria Uploader";
        ClientSize = new Size(700, 790);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        Theme.ApplyForm(this);

        Padding = new Padding(0);
        try { Icon = Theme.LoadAppIcon(); } catch {  }

        _statusTitle = new Label
        {
            Font = Theme.HeaderFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            AutoEllipsis = true,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _statusSub = new Label
        {
            Font = Theme.SubtitleFont,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            AutoEllipsis = true,
            Height = 18,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _signInButton = Theme.PrimaryButton("Sign in");
        _signInButton.Click += async (_, _) => await DoSignInAsync();
        _signOutButton = Theme.SecondaryButton("Sign out");
        _signOutButton.Click += (_, _) => DoSignOut();
        _pauseButton = Theme.SecondaryButton("Pause");
        _pauseButton.Click += (_, _) => _ctx.TogglePause();

        _logFolderField = Theme.TextField();
        _logFolderField.TextValue = settings.LogFolder;

        _gw2EiField = Theme.TextField();
        _gw2EiField.TextValue = settings.Gw2EiCliPath;

        _autostartCheck = Theme.Toggle("Start with Windows", Autostart.IsEnabled());
        _onlyIfGw2Check = Theme.Toggle("Upload only while GW2 is running", settings.UploadOnlyIfGw2Running);
        // UI presents this inverted from how the server / settings.json stores
        // it · users think in terms of "keep wipes out of my history", so the
        // checkbox reads "Exclude wipe logs" and defaults ON (wipes excluded).
        // Internally we still write Settings.UploadWipes = !Checked so the
        // existing API contract (PUT /api/users/me { uploadWipes }) is unchanged.
        _uploadWipesCheck = Theme.Toggle("Exclude wipe logs from upload", !settings.UploadWipes);
        _uploadWipesCheck.Click += async (_, _) =>
        {
            await OnToggleUploadWipesAsync();
        };

        BuildLayout();
        RefreshStatus();
        RefreshRecent();

        _ctx.ActivityChanged   += OnActivityChanged;
        _ctx.PauseStateChanged += OnPauseStateChanged;
        _ctx.Recent.Changed    += OnRecentChanged;
        FormClosed += (_, _) =>
        {
            _ctx.ActivityChanged   -= OnActivityChanged;
            _ctx.PauseStateChanged -= OnPauseStateChanged;
            _ctx.Recent.Changed    -= OnRecentChanged;
        };
    }

    private void OnActivityChanged(string _)
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)RefreshStatus);
    }
    private void OnPauseStateChanged()
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)RefreshStatus);
    }
    private void OnRecentChanged()
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)RefreshRecent);
    }

    private void BuildLayout()
    {
        var stripe = new Theme.AccentStripe
        {
            Dock = DockStyle.Top,
            Height = 2,
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Color.Transparent,
            AutoSize = false,
            Padding = new Padding(28, 0, 28, 20),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);
        Controls.Add(stripe);
        stripe.BringToFront();

        root.Controls.Add(BuildHeader());
        root.Controls.Add(BuildStatusCard());
        root.Controls.Add(BuildWatcherCard());
        root.Controls.Add(BuildRecentCard());
        root.Controls.Add(new Panel { Height = 1, BackColor = Color.Transparent });
        root.Controls.Add(BuildFooter());

        for (int i = 0; i < root.Controls.Count; i++)
            root.Controls[i].Dock = DockStyle.Top;
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            BackColor = Color.Transparent,
            Padding = new Padding(0, 22, 0, 14),
            Height = 92,
        };

        var logo = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(56, 56),
            Location = new Point(0, 20),
            BackColor = Color.Transparent,
        };
        try { logo.Image = Theme.LoadLogo(); } catch {  }

        var title = new Label
        {
            Text = "Tyria Uploader",
            Font = Theme.TitleFont,
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(72, 24),
        };
        var subtitle = new Label
        {
            Text = $"v{Application.ProductVersion}  ·  arcdps log uploader",
            Font = Theme.SubtitleFont,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(74, 56),
        };

        panel.Controls.Add(logo);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        return panel;
    }

    private Control BuildStatusCard()
    {
        var card = new Theme.Card
        {
            Margin = new Padding(0, 0, 0, 12),
            Height = 100,
        };
        var heading = Theme.Heading("STATUS");
        heading.Font = Theme.LabelFont;
        heading.ForeColor = Theme.TextFaint;
        heading.Location = new Point(20, 12);

        _statusDot.Location = new Point(20, 44);
        _statusTitle.Location = new Point(44, 40);
        _statusSub.Location = new Point(44, 62);

        _signOutButton.Margin = new Padding(0);
        _signInButton.Margin  = new Padding(0);

        _pauseButton.Margin   = new Padding(0, 0, 10, 0);
        _statusActions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        _statusActions.Controls.Add(_signOutButton);
        _statusActions.Controls.Add(_signInButton);
        _statusActions.Controls.Add(_pauseButton);

        card.Controls.Add(heading);
        card.Controls.Add(_statusDot);
        card.Controls.Add(_statusTitle);
        card.Controls.Add(_statusSub);
        card.Controls.Add(_statusActions);

        card.Resize         += (_, _) => LayoutStatusButtons();
        _statusActions.Resize += (_, _) => LayoutStatusButtons();
        LayoutStatusButtons();
        return card;
    }

    private Control BuildWatcherCard()
    {
        var card = new Theme.Card
        {
            Margin = new Padding(0, 0, 0, 12),
            Height = 314,
        };
        var heading = Theme.Heading("WATCHER");
        heading.Font = Theme.LabelFont;
        heading.ForeColor = Theme.TextFaint;
        heading.Location = new Point(20, 12);

        var folderLabel = Theme.Body("arcdps log folder", muted: false);
        folderLabel.Font = Theme.BodyBoldFont;
        folderLabel.Location = new Point(20, 38);

        _logFolderField.Location = new Point(20, 60);
        _logFolderField.Height = 36;

        var browseLogs = Theme.GhostButton("Browse");
        browseLogs.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = _logFolderField.TextValue };
            if (fbd.ShowDialog(this) == DialogResult.OK) _logFolderField.TextValue = fbd.SelectedPath;
        };

        var eiLabel = Theme.Body("GW2EI CLI executable", muted: false);
        eiLabel.Font = Theme.BodyBoldFont;
        eiLabel.Location = new Point(20, 110);

        _gw2EiField.Location = new Point(20, 132);
        _gw2EiField.Height = 36;

        var browseEi = Theme.GhostButton("Browse");
        browseEi.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "GuildWars2EliteInsights-CLI.exe|GuildWars2EliteInsights-CLI.exe|All exe|*.exe",
                Title = "Select GW2EI CLI executable",
            };
            if (ofd.ShowDialog(this) == DialogResult.OK) _gw2EiField.TextValue = ofd.FileName;
        };

        var eiHelp = Theme.Link("Don't have GW2EI? Download GuildWars2EliteInsights-CLI from GitHub.");

        eiHelp.LinkArea = new LinkArea(18, 36);
        eiHelp.LinkClicked += (_, _) => OpenUrl("https://github.com/baaron4/GW2-Elite-Insights-Parser/releases");
        eiHelp.Location = new Point(20, 180);

        var divider = new Panel
        {
            Height = 1,
            BackColor = Theme.Border,
            Location = new Point(20, 214),
        };

        _autostartCheck.Location = new Point(20, 230);
        _onlyIfGw2Check.Location = new Point(20, 254);
        _uploadWipesCheck.Location = new Point(20, 282);

        card.Controls.Add(heading);
        card.Controls.Add(folderLabel);
        card.Controls.Add(_logFolderField);
        card.Controls.Add(browseLogs);
        card.Controls.Add(eiLabel);
        card.Controls.Add(_gw2EiField);
        card.Controls.Add(browseEi);
        card.Controls.Add(eiHelp);
        card.Controls.Add(divider);
        card.Controls.Add(_autostartCheck);
        card.Controls.Add(_onlyIfGw2Check);
        card.Controls.Add(_uploadWipesCheck);

        void Layout()
        {
            const int pad = 20;
            const int gap = 10;
            _logFolderField.Width = card.ClientSize.Width - 2 * pad - gap - browseLogs.Width;
            browseLogs.Location = new Point(_logFolderField.Right + gap, _logFolderField.Top + 3);
            _gw2EiField.Width = card.ClientSize.Width - 2 * pad - gap - browseEi.Width;
            browseEi.Location = new Point(_gw2EiField.Right + gap, _gw2EiField.Top + 3);
            divider.Width = card.ClientSize.Width - 2 * pad;
        }
        card.Resize += (_, _) => Layout();
        Layout();
        return card;
    }

    private Control BuildRecentCard()
    {
        var card = new Theme.Card
        {
            Margin = new Padding(0, 0, 0, 12),
            Height = 190,
        };
        var heading = Theme.Heading("RECENT UPLOADS");
        heading.Font = Theme.LabelFont;
        heading.ForeColor = Theme.TextFaint;
        heading.Location = new Point(20, 12);

        _recentList = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Location = new Point(20, 36),
            Size = new Size(card.ClientSize.Width - 40, card.ClientSize.Height - 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };

        card.Controls.Add(heading);
        card.Controls.Add(_recentList);
        card.Resize += (_, _) =>
        {
            _recentList.Size = new Size(card.ClientSize.Width - 40, card.ClientSize.Height - 48);

            foreach (Control row in _recentList.Controls)
                row.Width = _recentList.ClientSize.Width;
        };
        return card;
    }

    private void RefreshStatus()
    {
        var signedIn = !string.IsNullOrEmpty(TokenStore.Load());
        _signInButton.Visible = !signedIn;
        _signOutButton.Visible = signedIn;

        _pauseButton.Text = _ctx.IsPaused ? "Resume" : "Pause";
        _pauseButton.Enabled = signedIn;

        var activity = _ctx.Activity ?? "Idle";
        Color dotColor;
        string title;
        string sub;

        if (!signedIn)
        {
            dotColor = Theme.Danger;
            title = "Not signed in";
            sub = "Sign in to start uploading logs to Tyria Planner.";
        }
        else if (_ctx.IsPaused)
        {
            dotColor = Theme.TextMuted;
            title = "Paused";
            sub = $"Signed in as {DisplayName()}. Watcher is paused — press Resume to continue.";
        }
        else if (activity.StartsWith("Uploading", StringComparison.OrdinalIgnoreCase) ||
                 activity.StartsWith("Parsing",   StringComparison.OrdinalIgnoreCase))
        {
            dotColor = Theme.Accent;
            title = activity;
            sub = $"Signed in as {DisplayName()}.";
        }
        else if (activity == "Watching")
        {
            dotColor = Theme.Success;
            title = "Watching for new logs";
            sub = $"Signed in as {DisplayName()}. Logs land in {ShortPath(_settings.LogFolder)}.";
        }
        else if (activity.Contains("not configured", StringComparison.OrdinalIgnoreCase) ||
                 activity.Contains("not found",      StringComparison.OrdinalIgnoreCase))
        {
            dotColor = Theme.Danger;
            title = activity;
            sub = "Set the GW2EI CLI path below and save to start watching.";
        }
        else
        {
            dotColor = Theme.TextMuted;
            title = activity;
            sub = signedIn ? $"Signed in as {DisplayName()}." : "";
        }

        _statusDot.DotColor = dotColor;
        _statusTitle.Text = title;
        _statusSub.Text = sub;

        LayoutStatusButtons();
    }

    private void LayoutStatusButtons()
    {
        if (_statusActions?.Parent is not Control card) return;
        const int pad = 20;
        int y = (card.ClientSize.Height - _statusActions.Height) / 2;
        _statusActions.Location = new Point(
            Math.Max(pad, card.ClientSize.Width - pad - _statusActions.Width), y);

        int textWidth = Math.Max(40, _statusActions.Left - 12 - _statusTitle.Left);
        _statusTitle.Width = textWidth;
        _statusSub.Width = textWidth;
    }

    private string DisplayName() =>
        _settings.SignedInDisplayName ?? _settings.SignedInUsername ?? "(unknown)";

    private static string ShortPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return "(no folder set)";
        if (p.Length <= 56) return p;
        return p[..28] + "…" + p[^25..];
    }

    private void RefreshRecent()
    {
        _recentList.SuspendLayout();
        _recentList.Controls.Clear();

        var snap = _ctx.Recent.Snapshot();
        if (snap.Count == 0)
        {
            var empty = new Label
            {
                Text = "No uploads yet. New arcdps logs will appear here as they're processed.",
                Font = Theme.BodyFont,
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = _recentList.ClientSize.Width,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _recentList.Controls.Add(empty);
        }
        else
        {
            foreach (var entry in snap.Take(5))
                _recentList.Controls.Add(BuildRecentRow(entry));
        }

        _recentList.ResumeLayout();
    }

    private Control BuildRecentRow(RecentUploads.Entry entry)
    {
        var row = new Panel
        {
            Width = _recentList.ClientSize.Width,
            Height = 28,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 2),
        };

        var dot = new Theme.StatusDot
        {
            DotColor = TrayApplicationContext.OutcomeColor(entry.Outcome),
            Location = new Point(0, 7),
            Size = new Size(12, 12),
        };

        var hasBoss = !string.IsNullOrEmpty(entry.Boss);
        var boss = new Label
        {
            Text = hasBoss ? entry.Boss : Path.GetFileNameWithoutExtension(entry.FileName),
            Font = Theme.BodyBoldFont,
            ForeColor = TrayApplicationContext.OutcomeColor(entry.Outcome) == Theme.Danger
                ? Theme.Danger
                : Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(20, 5),
        };

        var outcomeText = entry.Outcome switch
        {
            RecentUploads.Outcome.Uploaded     => "uploaded",
            RecentUploads.Outcome.Wipe         => "wipe",
            RecentUploads.Outcome.Deduplicated => "already uploaded",
            RecentUploads.Outcome.Skipped      => "skipped",
            RecentUploads.Outcome.Failed       => "failed",
            _ => "",
        };
        var sub = hasBoss
            ? $"· {TrayApplicationContext.FightTimeFromFileName(entry.FileName)} · {outcomeText}"
            : $"· {outcomeText}";
        var fightTime = new Label
        {
            Text = sub,
            Font = Theme.BodyFont,
            ForeColor = TrayApplicationContext.OutcomeColor(entry.Outcome) == Theme.Danger
                ? Theme.Danger
                : Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(0, 6),
        };

        var time = new Label
        {
            Text = TrayApplicationContext.RelativeTime(entry.AtUtc),
            Font = Theme.SubtitleFont,
            ForeColor = Theme.TextMuted,
            BackColor = Color.Transparent,
            AutoSize = false,
            Location = new Point(row.Width - 95, 6),
            Width = 95,
            Height = 18,
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };

        row.Controls.Add(dot);
        row.Controls.Add(boss);
        row.Controls.Add(fightTime);
        row.Controls.Add(time);

        void PositionFightTime() => fightTime.Location = new Point(boss.Right + 6, 6);
        boss.Resize += (_, _) => PositionFightTime();
        PositionFightTime();
        row.Resize += (_, _) =>
        {
            time.Location = new Point(row.Width - 95, 6);
        };
        return row;
    }

    private Control BuildFooter()
    {
        var panel = new Panel
        {
            BackColor = Color.Transparent,
            Height = 56,
            Padding = new Padding(0, 12, 0, 0),
        };

        var logsLink = Theme.Link("Open uploader log file");
        logsLink.LinkClicked += (_, _) => OpenLogFile();
        logsLink.Location = new Point(2, 22);

        var sep = new Label
        {
            Text = "·",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextFaint,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(2, 22),
        };

        var resetLink = Theme.Link("Re-upload everything");
        resetLink.LinkColor = Theme.TextMuted;
        resetLink.ActiveLinkColor = Theme.Danger;
        resetLink.LinkClicked += (_, _) => DoResetUploaderState();
        resetLink.Location = new Point(2, 22);

        var saveButton = Theme.PrimaryButton("Save");
        saveButton.Click += (_, _) => SaveAndClose();

        var closeButton = Theme.SecondaryButton("Close");
        closeButton.Click += (_, _) => Close();

        panel.Controls.Add(logsLink);
        panel.Controls.Add(sep);
        panel.Controls.Add(resetLink);
        panel.Controls.Add(saveButton);
        panel.Controls.Add(closeButton);
        panel.Resize += (_, _) =>
        {
            int y = (panel.ClientSize.Height - saveButton.Height) / 2 + 4;
            closeButton.Location = new Point(panel.ClientSize.Width - closeButton.Width, y);
            saveButton.Location  = new Point(closeButton.Left - 10 - saveButton.Width, y);
            sep.Location       = new Point(logsLink.Right + 8, logsLink.Top);
            resetLink.Location = new Point(sep.Right + 8, logsLink.Top);
        };
        return panel;
    }

    private void DoResetUploaderState()
    {
        var msg = "Clear the local upload cache and re-scan every log in your watch folder?\n\n" +
                  "All logs will be parsed and re-sent. Tyria Planner's server-side dedup keeps " +
                  "duplicates out of your account, so no data is lost — this just costs CPU and " +
                  "bandwidth while the catch-up runs.";
        var dr = MessageBox.Show(this, msg, "Tyria Uploader",
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
        if (dr != DialogResult.OK) return;
        _ctx.ResetUploaderState();
    }

    private async Task OnToggleUploadWipesAsync()
    {
        // Checkbox is the "exclude" view · checked = wipes excluded =
        // server-side uploadWipes false.
        var nextExclude = _uploadWipesCheck.Checked;
        var nextUpload = !nextExclude;
        var verb = nextExclude ? "exclude" : "include";
        var intent = nextExclude
            ? "Wipe logs will be rejected by the server from now on."
            : "Wipe logs will be uploaded alongside kills from now on.";
        var confirm = MessageBox.Show(this,
            $"{char.ToUpper(verb[0]) + verb.Substring(1)} wipes from your history?\n\n{intent}",
            "Tyria Uploader", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (confirm != DialogResult.OK)
        {
            _uploadWipesCheck.Checked = !nextExclude;
            return;
        }

        var deleteAsk = MessageBox.Show(this,
            "Also delete every existing wipe from your Tyria Planner history?\n\n" +
            "Wipes never count toward DPS aggregates anyway · this only cleans " +
            "your history list and Recent Streak card.",
            "Tyria Uploader", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        _uploadWipesCheck.Enabled = false;
        try
        {
            if (deleteAsk == DialogResult.Yes)
            {
                var deleted = await _api.DeleteWipesAsync();
                _log.Info($"Deleted {deleted ?? 0} existing wipes from server");
            }
            var ok = await _api.SetUploadWipesPrefAsync(nextUpload);
            if (!ok)
            {
                MessageBox.Show(this, "Could not update preference on the server. Try again later.",
                    "Tyria Uploader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _uploadWipesCheck.Checked = !nextExclude;
                return;
            }
            _settings.UploadWipes = nextUpload;
            SettingsStore.Save(_settings);
        }
        finally
        {
            _uploadWipesCheck.Enabled = true;
        }
    }

    private async Task DoSignInAsync()
    {
        _signInButton.Enabled = false;
        _statusDot.DotColor = Theme.Accent;
        _statusTitle.Text = "Opening browser…";
        _statusSub.Text = "Complete sign-in in your browser, this window will update when done.";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var oauth = new OAuthClient(_settings.ApiBaseUrl, http, _settings.WebBaseUrl);
            var result = await oauth.SignInAsync();
            TokenStore.Save(result.AccessToken);
            _api.SetAccessToken(result.AccessToken);

            if (result.User != null)
            {
                _settings.SignedInUsername = result.User.Username;
                _settings.SignedInDisplayName = result.User.DisplayName;
            }
            SettingsStore.Save(_settings);
            _log.Info($"Signed in as {_settings.SignedInUsername}");
            _ctx.RestartWatcher();
        }
        catch (Exception ex)
        {
            _log.Error("Sign-in failed", ex);
            MessageBox.Show(this, $"Sign-in failed: {ex.Message}", "Tyria Uploader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _signInButton.Enabled = true;
            RefreshStatus();
        }
    }

    private void DoSignOut()
    {
        TokenStore.Clear();
        _api.SetAccessToken(null);
        _settings.SignedInUsername = null;
        _settings.SignedInDisplayName = null;
        SettingsStore.Save(_settings);
        _ctx.RestartWatcher();
        RefreshStatus();
    }

    private void SaveAndClose()
    {
        _settings.LogFolder = _logFolderField.TextValue.Trim();
        _settings.Gw2EiCliPath = _gw2EiField.TextValue.Trim();
        _settings.UploadOnlyIfGw2Running = _onlyIfGw2Check.Checked;
        _settings.StartWithWindows = _autostartCheck.Checked;
        SettingsStore.Save(_settings);

        try { Autostart.SetEnabled(_autostartCheck.Checked); } catch { }

        _ctx.RestartWatcher();
        Close();
    }

    private static void OpenLogFile()
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

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
