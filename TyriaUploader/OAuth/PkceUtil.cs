using System.Security.Cryptography;
using System.Text;

namespace TyriaUploader.OAuth;

public static class PkceUtil
{
    public static string CreateCodeVerifier()
    {

        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    public static string ComputeChallenge(string verifier)
    {
        var bytes = Encoding.UTF8.GetBytes(verifier);
        var hash = SHA256.HashData(bytes);
        return Base64Url(hash);
    }

    public static string CreateState()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(16));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
