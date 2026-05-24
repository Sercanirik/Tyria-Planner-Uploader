using TyriaUploader.Config;
using TyriaUploader.Gw2Ei;

namespace TyriaUploader.UI;

internal sealed class RuntimeInstallForm : Form
{
    private readonly Label _statusLabel;
    private readonly ProgressBar _progress;
    private readonly Label _detailLabel;
    private readonly Button _closeButton;
    private readonly CancellationTokenSource _cts = new();
    private readonly FileLogger _log;

    public bool Succeeded { get; private set; }

    public RuntimeInstallForm(FileLogger log)
    {
        _log = log;

        Text = "Tyria Uploader — Installing .NET 8 Runtime";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 180);
        ShowInTaskbar = true;
        try { Icon = Theme.LoadAppIcon(); } catch { }
        Theme.ApplyForm(this);

        var title = new Label
        {
            Text = "Installing .NET 8 Runtime",
            Font = Theme.HeaderFont,
            ForeColor = Theme.Accent,
            AutoSize = true,
            Location = new Point(20, 18),
            BackColor = Color.Transparent,
        };

        _statusLabel = new Label
        {
            Text = "Downloading…",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(20, 50),
            BackColor = Color.Transparent,
        };

        _progress = new ProgressBar
        {
            Location = new Point(20, 78),
            Size = new Size(420, 14),
            Style = ProgressBarStyle.Continuous,
            Minimum = 0,
            Maximum = 1000,
            Value = 0,
        };

        _detailLabel = new Label
        {
            Text = "",
            Font = Theme.LabelFont,
            ForeColor = Theme.TextMuted,
            AutoSize = false,
            Size = new Size(420, 28),
            Location = new Point(20, 100),
            BackColor = Color.Transparent,
        };

        _closeButton = Theme.PrimaryButton("Cancel");
        _closeButton.Size = new Size(96, 30);
        _closeButton.Location = new Point(ClientSize.Width - 116, ClientSize.Height - 46);
        _closeButton.Click += (_, _) => { _cts.Cancel(); Close(); };

        Controls.Add(title);
        Controls.Add(_statusLabel);
        Controls.Add(_progress);
        Controls.Add(_detailLabel);
        Controls.Add(_closeButton);

        Shown += async (_, _) => await RunAsync();
        FormClosing += (_, _) => _cts.Cancel();
    }

    private async Task RunAsync()
    {
        var progress = new Progress<(long downloaded, long? total)>(p =>
        {
            if (IsDisposed) return;
            if (p.total is { } total && total > 0)
            {
                var pct = (double)p.downloaded / total;
                _progress.Value = Math.Min(_progress.Maximum, (int)(pct * _progress.Maximum));
                _detailLabel.Text =
                    $"{FormatBytes(p.downloaded)} of {FormatBytes(total)}  ·  {pct:P0}";
            }
            else
            {
                _detailLabel.Text = FormatBytes(p.downloaded) + " downloaded";
            }
        });

        var result = await DotNetRuntimeInstaller.InstallAsync(progress, _log, _cts.Token);
        if (IsDisposed) return;

        if (result.Success)
        {
            Succeeded = true;
            _statusLabel.Text = "Installed.";
            _progress.Value = _progress.Maximum;
            _detailLabel.Text = result.Version is { } v
                ? $".NET Runtime {v} installed. Tyria Uploader is ready."
                : ".NET Runtime installed. Tyria Uploader is ready.";
        }
        else
        {
            Succeeded = false;
            _statusLabel.Text = "Install failed.";
            _statusLabel.ForeColor = Theme.Danger;
            _detailLabel.Text = result.Error ?? "Unknown error.";
        }

        _closeButton.Text = "Close";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _cts.Dispose();
        base.Dispose(disposing);
    }

    private static string FormatBytes(long n)
    {
        if (n < 1024) return $"{n} B";
        if (n < 1024 * 1024) return $"{n / 1024.0:0.0} KB";
        return $"{n / (1024.0 * 1024):0.0} MB";
    }
}
