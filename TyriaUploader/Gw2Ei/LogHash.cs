using System.Security.Cryptography;

namespace TyriaUploader.Gw2Ei;

public static class LogHash
{

    public static async Task<string> ComputeAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return "evtc|" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
