using TyriaUploader.Config;
using TyriaUploader.OAuth;
using TyriaUploader.UI;

namespace TyriaUploader;

internal static class Program
{

    private const string MutexName = "Global\\TyriaUploader.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new System.Threading.Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {

            return;
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settings = SettingsStore.Load();
        var logger = new FileLogger(SettingsStore.LogFilePath);
        TokenStore.SetLogger(logger);
        logger.Info($"Tyria Uploader {Application.ProductVersion} starting");

        // GW2EI is framework-dependent on .NET 8 — prompt + install the runtime
        // before the tray starts watching so we don't spam parse-fail warnings.
        RuntimeBootstrap.EnsureRuntimeReady(settings, logger);

        try
        {
            using var ctx = new TrayApplicationContext(settings, logger);
            Application.Run(ctx);
        }
        catch (Exception ex)
        {
            logger.Error("Fatal error", ex);
            MessageBox.Show(
                $"Tyria Uploader crashed: {ex.Message}\n\nSee log at: {SettingsStore.LogFilePath}",
                "Tyria Uploader",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
