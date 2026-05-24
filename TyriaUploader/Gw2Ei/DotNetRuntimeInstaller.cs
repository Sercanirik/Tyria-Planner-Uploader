using System.Diagnostics;
using System.Text.Json;
using TyriaUploader.Config;

namespace TyriaUploader.Gw2Ei;

public static class DotNetRuntimeInstaller
{
    // Microsoft-maintained release metadata for .NET 8.0. Schema:
    // { "releases": [ { "release-version": "8.0.x",
    //                   "runtime": { "files": [ { "name": "...", "rid": "win-x64", "url": "..." }, ... ] } } ] }
    // First release in the array is the newest.
    private const string ReleasesJsonUrl =
        "https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json";

    public sealed record InstallResult(bool Success, string? Error, string? Version);

    public static async Task<InstallResult> InstallAsync(
        IProgress<(long downloaded, long? total)>? progress,
        FileLogger log,
        CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TyriaUploader/" + Application.ProductVersion);

            log.Info("Fetching .NET 8 release metadata");
            string installerUrl;
            string version;
            try
            {
                var json = await http.GetStringAsync(ReleasesJsonUrl, ct);
                (installerUrl, version) = FindLatestRuntimeInstaller(json)
                    ?? throw new InvalidOperationException(
                        "Could not find a win-x64 .NET 8 runtime installer in release metadata");
            }
            catch (Exception ex)
            {
                log.Error("Could not resolve .NET 8 runtime installer URL", ex);
                return new InstallResult(false,
                    "Could not look up the .NET 8 installer (no internet?). " +
                    "Install manually from https://dotnet.microsoft.com/download/dotnet/8.0",
                    null);
            }

            log.Info($"Downloading .NET 8 Runtime {version} from {installerUrl}");
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"dotnet-runtime-installer-{Guid.NewGuid():N}.exe");

            using (var resp = await http.GetAsync(installerUrl,
                HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength;
                using var src = await resp.Content.ReadAsStreamAsync(ct);
                using var dst = File.Create(tempPath);
                var buf = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    read += n;
                    progress?.Report((read, total));
                }
            }

            log.Info($"Launching .NET runtime installer at {tempPath}");
            ProcessStartInfo psi = new()
            {
                FileName = tempPath,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true,
                Verb = "runas",
            };
            int exit;
            try
            {
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Process.Start returned null");
                await proc.WaitForExitAsync(ct);
                exit = proc.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception wex)
            {
                // 1223 = ERROR_CANCELLED — user clicked No on UAC.
                log.Warn($"Installer launch failed (likely UAC declined): {wex.Message}");
                try { File.Delete(tempPath); } catch { }
                return new InstallResult(false, "Installation was cancelled.", version);
            }
            try { File.Delete(tempPath); } catch { }

            // 0 = success; 1638 = newer version already installed; 3010 = success, reboot needed.
            bool ok = exit is 0 or 1638 or 3010;
            log.Info($"Installer exit code: {exit} ({(ok ? "ok" : "fail")})");
            return ok
                ? new InstallResult(true, null, version)
                : new InstallResult(false, $"Installer returned exit code {exit}.", version);
        }
        catch (OperationCanceledException)
        {
            return new InstallResult(false, "Cancelled.", null);
        }
        catch (Exception ex)
        {
            log.Error(".NET runtime install failed", ex);
            return new InstallResult(false, ex.Message, null);
        }
    }

    private static (string Url, string Version)? FindLatestRuntimeInstaller(string releasesJson)
    {
        using var doc = JsonDocument.Parse(releasesJson);
        if (!doc.RootElement.TryGetProperty("releases", out var releases)) return null;
        foreach (var rel in releases.EnumerateArray())
        {
            if (!rel.TryGetProperty("runtime", out var runtime)) continue;
            if (!runtime.TryGetProperty("files", out var files)) continue;
            var version = rel.TryGetProperty("release-version", out var rv)
                ? rv.GetString() ?? "?" : "?";
            foreach (var f in files.EnumerateArray())
            {
                var name = f.TryGetProperty("name", out var n) ? n.GetString() : null;
                var rid  = f.TryGetProperty("rid",  out var r) ? r.GetString() : null;
                var url  = f.TryGetProperty("url",  out var u) ? u.GetString() : null;
                if (name == null || url == null) continue;
                if (rid != "win-x64") continue;
                if (!name.StartsWith("dotnet-runtime-", StringComparison.OrdinalIgnoreCase)) continue;
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                return (url, version);
            }
        }
        return null;
    }
}
