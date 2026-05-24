namespace TyriaUploader.Gw2Ei;

public static class DotNetRuntimeCheck
{
    // GW2EI 3.22 CLI is framework-dependent on Microsoft.NETCore.App 8.0.
    // We detect the runtime by looking at the canonical install location
    // %ProgramFiles%\dotnet\shared\Microsoft.NETCore.App\8.x.y. This is
    // how Microsoft's apphost finds runtimes, so a positive result here
    // means GW2EI's bundled apphost will resolve a runtime too.
    public static bool IsDotNet8RuntimeInstalled()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(pf)) return false;
        var sharedDir = Path.Combine(pf, "dotnet", "shared", "Microsoft.NETCore.App");
        if (!Directory.Exists(sharedDir)) return false;
        foreach (var d in Directory.EnumerateDirectories(sharedDir))
        {
            var name = Path.GetFileName(d);
            if (name.StartsWith("8.", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
