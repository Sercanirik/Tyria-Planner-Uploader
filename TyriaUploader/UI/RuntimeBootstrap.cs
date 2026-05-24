using TyriaUploader.Config;
using TyriaUploader.Gw2Ei;

namespace TyriaUploader.UI;

internal static class RuntimeBootstrap
{
    // Returns true once the .NET 8 runtime is available (either already installed
    // or freshly installed via the prompt). Returns false if the user declined
    // or the install failed — the tray UI still starts so the user can retry
    // from Settings, but parsing will fail until they install the runtime.
    public static bool EnsureRuntimeReady(Settings settings, FileLogger log)
    {
        // No bundled GW2EI and no user override — nothing to worry about here.
        // The Settings screen will guide the user to point at their own copy.
        if (string.IsNullOrWhiteSpace(settings.Gw2EiCliPath) || !File.Exists(settings.Gw2EiCliPath))
            return true;

        if (DotNetRuntimeCheck.IsDotNet8RuntimeInstalled())
            return true;

        log.Info(".NET 8 Runtime not found — prompting user to install");
        var msg =
            "Tyria Uploader needs the .NET 8 Runtime to parse arcdps logs.\n\n" +
            "Download and install it now? (~30 MB · about a minute)\n\n" +
            "Windows will show a UAC prompt asking for permission to install.";
        var dr = MessageBox.Show(
            msg,
            "Tyria Uploader — .NET 8 Runtime required",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1);
        if (dr != DialogResult.OK)
        {
            log.Info("User declined .NET runtime install");
            return false;
        }

        using var form = new RuntimeInstallForm(log);
        Application.Run(form);

        if (form.Succeeded && DotNetRuntimeCheck.IsDotNet8RuntimeInstalled())
        {
            log.Info(".NET 8 Runtime installed successfully");
            return true;
        }

        log.Warn(".NET 8 Runtime install did not complete; uploader will start but parsing will fail");
        return false;
    }
}
