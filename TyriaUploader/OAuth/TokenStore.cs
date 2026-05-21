using System.Security.Cryptography;
using System.Text;
using TyriaUploader.Config;

namespace TyriaUploader.OAuth;

public static class TokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TyriaUploader/v1");
    private static FileLogger? _log;

    public static void SetLogger(FileLogger log) => _log = log;

    public static void Save(string token)
    {
        var data = Encoding.UTF8.GetBytes(token);
        var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(SettingsStore.AppDataDir);
        File.WriteAllBytes(SettingsStore.TokenFile, encrypted);
    }

    public static string? Load()
    {
        if (!File.Exists(SettingsStore.TokenFile)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(SettingsStore.TokenFile);
            var data = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (Exception ex)
        {

            _log?.Warn($"Token decrypt failed, signing out: {ex.Message}");
            return null;
        }
    }

    public static void Clear()
    {
        if (File.Exists(SettingsStore.TokenFile))
        {
            try { File.Delete(SettingsStore.TokenFile); } catch { }
        }
    }
}
