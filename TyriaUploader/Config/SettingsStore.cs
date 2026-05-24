using System.Text.Json;

namespace TyriaUploader.Config;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TyriaUploader");

    public static string SettingsFile  => Path.Combine(AppDataDir, "settings.json");
    public static string TokenFile     => Path.Combine(AppDataDir, "token.bin");
    public static string LogFilePath   => Path.Combine(AppDataDir, "uploader.log");
    public static string StateFile     => Path.Combine(AppDataDir, "uploaded.json");

    public static string SeenPathsFile => Path.Combine(AppDataDir, "seen_paths.json");

    public static string IgnoredHashesFile => Path.Combine(AppDataDir, "ignored.json");

    public static string? BundledGw2EiCliPath
    {
        get
        {
            var p = Path.Combine(AppContext.BaseDirectory, "GW2EI", "GuildWars2EliteInsights-CLI.exe");
            return File.Exists(p) ? p : null;
        }
    }

    public static Settings Load()
    {
        Directory.CreateDirectory(AppDataDir);
        Settings s;
        if (!File.Exists(SettingsFile))
        {
            s = new Settings();
            ApplyBundledGw2EiDefault(s);
            Save(s);
            return s;
        }
        try
        {
            var json = File.ReadAllText(SettingsFile);
            s = JsonSerializer.Deserialize<Settings>(json, JsonOptions) ?? new Settings();
        }
        catch
        {

            try { File.Move(SettingsFile, SettingsFile + ".broken", overwrite: true); } catch { }
            s = new Settings();
            ApplyBundledGw2EiDefault(s);
            Save(s);
            return s;
        }

        ApplyBundledGw2EiDefault(s);
        return s;
    }

    private static void ApplyBundledGw2EiDefault(Settings s)
    {
        var bundled = BundledGw2EiCliPath;
        if (bundled == null) return;
        // No configured path, or the configured path no longer exists (uploader
        // folder moved/renamed) — fall back to the GW2EI shipped next to the exe.
        if (string.IsNullOrWhiteSpace(s.Gw2EiCliPath) || !File.Exists(s.Gw2EiCliPath))
            s.Gw2EiCliPath = bundled;
    }

    public static void Save(Settings s)
    {
        Directory.CreateDirectory(AppDataDir);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, JsonOptions));
    }
}
