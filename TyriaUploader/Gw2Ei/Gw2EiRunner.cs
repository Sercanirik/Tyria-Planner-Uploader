using System.Diagnostics;
using TyriaUploader.Config;

namespace TyriaUploader.Gw2Ei;

public sealed class Gw2EiRunner
{
    private readonly FileLogger _log;
    private readonly string _cliPath;
    private const int CliTimeoutMs = 90_000;

    public Gw2EiRunner(string cliPath, FileLogger log)
    {
        _cliPath = cliPath;
        _log = log;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_cliPath) && File.Exists(_cliPath);

    public sealed record ParseResult(bool Success, string? JsonText, string? Error, string? JsonPath);

    public async Task<ParseResult> ParseAsync(string evtcPath, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new ParseResult(false, null, "GW2EI CLI not configured", null);
        if (!File.Exists(evtcPath))
            return new ParseResult(false, null, "Log file not found", null);

        var outDir = Path.Combine(Path.GetTempPath(), "TyriaUploader", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var configPath = WriteEiConfig(outDir);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _cliPath,
                ArgumentList =
                {
                    "-c", configPath,
                    evtcPath,
                },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_cliPath) ?? Environment.CurrentDirectory,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start GW2EI process");

            using var timeout = new CancellationTokenSource(CliTimeoutMs);
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, ct);

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(combined.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(combined.Token);

            try
            {
                await proc.WaitForExitAsync(combined.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                try { await Task.WhenAll(stdoutTask, stderrTask); } catch { }
                return new ParseResult(false, null, "GW2EI parse timed out", null);
            }

            string stderr = "";
            string stdout = "";
            try { stderr = await stderrTask; } catch { }
            try { stdout = await stdoutTask; } catch { }

            if (proc.ExitCode != 0)
            {
                return new ParseResult(false, null, $"GW2EI exit {proc.ExitCode}: {Truncate(stderr)}", null);
            }

            var startedAtUtc = DateTime.UtcNow.AddMinutes(-5);
            var jsonFile = Directory.GetFiles(outDir, "*.json", SearchOption.AllDirectories)
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                .FirstOrDefault();
            bool foundInSourceDir = false;
            if (jsonFile == null)
            {
                var srcDir = Path.GetDirectoryName(evtcPath);
                if (!string.IsNullOrEmpty(srcDir))
                {
                    jsonFile = Directory.GetFiles(srcDir, "*.json", SearchOption.TopDirectoryOnly)
                        .Where(f => new FileInfo(f).LastWriteTimeUtc >= startedAtUtc)
                        .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                        .FirstOrDefault();
                    if (jsonFile != null) foundInSourceDir = true;
                }
            }
            if (jsonFile == null)
            {

                var diag = $"stderr={Truncate(stderr)} stdout={Truncate(stdout)}";
                return new ParseResult(false, null, $"GW2EI produced no JSON output. {diag}", null);
            }

            var jsonText = await File.ReadAllTextAsync(jsonFile, ct);
            if (foundInSourceDir)
            {

                _log.Info($"GW2EI wrote JSON next to source: {Path.GetFileName(jsonFile)} ({new FileInfo(jsonFile).Length / 1024} KB)");

                try { File.Delete(jsonFile); } catch { }
            }
            return new ParseResult(true, jsonText, null, jsonFile);
        }
        catch (Exception ex)
        {
            _log.Error($"GW2EI parse failed for {evtcPath}", ex);
            return new ParseResult(false, null, ex.Message, null);
        }
        finally
        {

            try { Directory.Delete(outDir, recursive: true); } catch { }
        }
    }

    private static string WriteEiConfig(string outDir)
    {
        var configPath = Path.Combine(outDir, "ei.conf");
        var lines = new[]
        {
            $"OutLocation={outDir}",
            "SaveOutJSON=true",
            "IndentJSON=false",
            "SaveOutHTML=false",
            "SaveOutCSV=false",
            "SaveOutTrace=false",
            "ParseCombatReplay=false",
            "ComputeDamageModifiers=false",
            "SaveAtOut=false",
            "AutoAdd=false",
            "Anonymous=false",
            "AutoParse=false",
        };
        File.WriteAllLines(configPath, lines);
        return configPath;
    }

    private static string Truncate(string s, int max = 600)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > max ? s[..max] + "..." : s;
    }
}
