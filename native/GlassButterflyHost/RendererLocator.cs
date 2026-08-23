namespace GlassButterfly;

/// <summary>
/// Finds the built renderer folder (the one containing index.html). Order:
///   1. An explicit "--renderer &lt;path&gt;" argument (dev override).
///   2. A "renderer" folder next to the executable (portable/unzipped layout).
///   3. A dev fallback of ../../dist/web relative to the build output, so the
///      host runs straight from `dotnet run` during development (this path only
///      exists in the source tree).
///   4. The renderer embedded in the .scr, extracted to a per-user cache. This
///      is the normal path for the distributed single-file screensaver, which
///      carries no external asset folder.
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

        // 3. Dev fallback: walk up from the build output looking for dist/web.
        //    Done as a search (not a fixed number of "../") so it works whether or
        //    not a runtime-identifier subfolder (…/net8.0-windows/win-x64/) is in
        //    the output path — that off-by-one was making the dev host silently
        //    load the stale embedded renderer instead of freshly built dist/web.
        //    This only resolves inside the source tree, so a distributed .scr
        //    still falls through to the embedded copy below. Checked before the
        //    embedded renderer so live edits always win during development.
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "dist", "web");
            if (HasRenderer(candidate)) return candidate;
        }

        // 4. Embedded renderer (the distributed .scr), extracted on demand.
        string? extracted = RendererAssets.EnsureExtracted();
        if (extracted is not null && HasRenderer(extracted)) return extracted;

        // Nothing matched — return the portable guess so diagnostics report a
        // single, sensible "renderer missing" path.
        return beside;
    }

    private static bool HasRenderer(string dir) =>
        File.Exists(Path.Combine(dir, "index.html"));
}
