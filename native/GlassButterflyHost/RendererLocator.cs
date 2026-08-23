namespace GlassButterfly;

/// <summary>
/// Finds the built renderer folder (the one containing index.html). Order:
///   1. An explicit "--renderer &lt;path&gt;" argument (dev override).
///   2. A "renderer" folder next to the executable (portable/unzipped layout).
///   3. The renderer embedded in the .scr, extracted to a per-user cache. This
///      is the normal path for the distributed single-file screensaver, which
///      carries no external asset folder.
///   4. A dev fallback of ../../dist/web relative to the build output, so the
///      host runs straight from `dotnet run` during development.
/// </summary>
internal static class RendererLocator
{
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

        // 3. Embedded renderer (the distributed .scr), extracted on demand.
        string? extracted = RendererAssets.EnsureExtracted();
        if (extracted is not null && HasRenderer(extracted)) return extracted;

        // 4. Dev fallback: from native/GlassButterflyHost/bin/<cfg>/net8.0-windows/
        //    up to the repo root's dist/web.
        string devGuess = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dist", "web"));
        if (HasRenderer(devGuess)) return devGuess;

        // Nothing matched — return the portable guess so diagnostics report a
        // single, sensible "renderer missing" path.
        return beside;
    }

    private static bool HasRenderer(string dir) =>
        File.Exists(Path.Combine(dir, "index.html"));
}
