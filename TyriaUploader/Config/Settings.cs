using System.Text.Json;
using System.Text.Json.Serialization;

namespace TyriaUploader.Config;

public sealed class Settings
{
    [JsonPropertyName("apiBaseUrl")]
    public string ApiBaseUrl { get; set; } = "https://tyriaplanner.com";

    [JsonPropertyName("webBaseUrl")]
    public string? WebBaseUrl { get; set; }

    [JsonPropertyName("logFolder")]
    public string LogFolder { get; set; } = DefaultLogFolder();

    [JsonPropertyName("gw2EiCliPath")]
    public string Gw2EiCliPath { get; set; } = "";

    [JsonPropertyName("rescanIntervalSeconds")]
    public int RescanIntervalSeconds { get; set; } = 60;

    [JsonPropertyName("uploadOnlyIfGw2Running")]
    public bool UploadOnlyIfGw2Running { get; set; } = false;

    [JsonPropertyName("uploadWipes")]
    public bool UploadWipes { get; set; } = false;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = true;

    [JsonPropertyName("signedInUsername")]
    public string? SignedInUsername { get; set; }

    [JsonPropertyName("signedInDisplayName")]
    public string? SignedInDisplayName { get; set; }

    [JsonPropertyName("pausedUntilUtc")]
    public DateTime? PausedUntilUtc { get; set; }

    private static string DefaultLogFolder()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(docs, "Guild Wars 2", "addons", "arcdps", "arcdps.cbtlogs");
    }
}
