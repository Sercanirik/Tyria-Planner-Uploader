using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TyriaUploader.Updates;

public sealed class UpdateChecker
{
    private const string UploaderRepo = "Sercanirik/Tyria-Planner-Uploader";
    private const string Gw2EiRepo    = "baaron4/GW2-Elite-Insights-Parser";

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient http) { _http = http; }

    public sealed record UpdateInfo(Version Latest, string HtmlUrl);

    public Task<UpdateInfo?> CheckUploaderAsync(Version current, CancellationToken ct = default)
        => CheckLatestAsync(UploaderRepo, current, ct);

    public Task<UpdateInfo?> CheckGw2EiAsync(Version current, CancellationToken ct = default)
        => CheckLatestAsync(Gw2EiRepo, current, ct);

    public static Version? GetGw2EiInstalledVersion(string exePath)
    {
        try
        {
            if (!File.Exists(exePath)) return null;
            var fvi = FileVersionInfo.GetVersionInfo(exePath);

            foreach (var candidate in new[] { fvi.FileVersion, fvi.ProductVersion })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (Version.TryParse(candidate, out var v)) return v;
            }
        }
        catch { }
        return null;
    }

    private async Task<UpdateInfo?> CheckLatestAsync(string repo, Version current, CancellationToken ct)
    {
        var url = $"https://api.github.com/repos/{repo}/releases/latest";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);

        req.Headers.UserAgent.ParseAdd("TyriaUploader-UpdateChecker");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        using var resp = await _http.SendAsync(req, cts.Token);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync(cts.Token);
        var rel = JsonSerializer.Deserialize<GitHubRelease>(body);
        if (rel == null || string.IsNullOrWhiteSpace(rel.TagName)) return null;

        var tag = rel.TagName.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var latest)) return null;
        return latest > current ? new UpdateInfo(latest, rel.HtmlUrl ?? $"https://github.com/{repo}/releases") : null;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    }
}
