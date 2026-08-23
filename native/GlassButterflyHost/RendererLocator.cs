using Microsoft.Win32;

namespace GlassButterfly;

/// <summary>
/// Finds the built renderer folder (the one containing index.html). Order:
///   1. An explicit "--renderer &lt;path&gt;" argument (dev override).
///   2. A "renderer" folder next to the executable (portable layout).
///   3. The installed layout: HKLM\Software\GlassButterfly\InstallDir + \renderer.
///      This is how the installed .scr — which lives in System32, away from its
///      assets — locates the renderer, since Windows invokes a screensaver with
///      only /s /c /p and no room for a --renderer argument.
///   4. A fixed %ProgramFiles%\GlassButterfly\renderer fallback (in case the
///      registry value is missing but the default install path was used).
///   5. A dev fallback of ../../dist/web relative to the build output, so the
///      host can be run straight from Visual Studio / `dotnet run`.
/// </summary>
internal static class RendererLocator
{
    private const string RegistryPath = @"SOFTWARE\GlassButterfly";
    private const string RegistryValue = "InstallDir";

    public static string Resolve(string[] args)
    {
        // 1. Explicit dev override.
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--renderer", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        // 2. Portable layout: renderer sitting next to the executable.
        string beside = Path.Combine(AppContext.BaseDirectory, "renderer");
        if (HasRenderer(beside)) return beside;

        // 3. Installed layout: path recorded by the installer in the registry.
        string? installDir = ReadInstallDir();
        if (installDir is not null)
        {
            string fromRegistry = Path.Combine(installDir, "renderer");
            if (HasRenderer(fromRegistry)) return fromRegistry;
        }

        // 4. Fixed default install location (registry missing but default used).
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string fixedGuess = Path.Combine(programFiles, "GlassButterfly", "renderer");
        if (HasRenderer(fixedGuess)) return fixedGuess;

        // 5. Dev fallback: from native/GlassButterflyHost/bin/<cfg>/net8.0-windows/
        //    up to the repo root's dist/web.
        string devGuess = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dist", "web"));
        if (HasRenderer(devGuess)) return devGuess;

        // Nothing matched — return the portable guess so callers report a single,
        // sensible "renderer missing" path in diagnostics.
        return beside;
    }

    private static bool HasRenderer(string dir) =>
        File.Exists(Path.Combine(dir, "index.html"));

    private static string? ReadInstallDir()
    {
        // Prefer the machine-wide value written by the (admin) installer; fall
        // back to a per-user value should a future per-user install write one.
        foreach (RegistryKey root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(RegistryPath);
                if (key?.GetValue(RegistryValue) is string v && !string.IsNullOrWhiteSpace(v))
                    return v;
            }
            catch
            {
                // Registry unavailable/denied — fall through to the next source.
            }
        }
        return null;
    }
}
