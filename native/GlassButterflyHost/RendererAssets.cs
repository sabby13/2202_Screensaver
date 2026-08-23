using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

namespace GlassButterfly;

/// <summary>
/// Makes the embedded renderer available on disk. The distributed .scr is a
/// single self-contained file with the whole built renderer (dist/web) embedded
/// as <c>renderer.zip</c>; WebView2's virtual-host mapping needs a real folder,
/// so on first use we extract it to a per-user, content-versioned cache under
/// %LOCALAPPDATA%\GlassButterfly\renderer\&lt;hash&gt;. Subsequent launches reuse it,
/// and a new build (new hash) extracts into a fresh folder automatically.
/// </summary>
internal static class RendererAssets
{
    private const string ResourceName = "GlassButterfly.renderer.zip";

    /// <summary>Extracts the embedded renderer (if any) and returns the folder
    /// containing index.html. Returns null when no renderer is embedded — i.e. a
    /// plain dev build, where the caller falls back to dist/web.</summary>
    public static string? EnsureExtracted()
    {
        Assembly asm = typeof(RendererAssets).Assembly;
        using Stream? res = asm.GetManifestResourceStream(ResourceName);
        if (res is null) return null;

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            res.CopyTo(ms);
            bytes = ms.ToArray();
        }

        // Content hash → cache folder name, so different builds never collide and
        // the same build is only ever extracted once.
        string key = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GlassButterfly", "renderer");
        string finalDir = Path.Combine(baseDir, key);
        string indexPath = Path.Combine(finalDir, "index.html");

        if (File.Exists(indexPath)) return finalDir;

        try
        {
            Directory.CreateDirectory(baseDir);

            // Extract to a unique temp dir, then atomically move into place. This
            // keeps a half-written cache from ever being served if two host
            // processes (e.g. preview + settings) start at once.
            string tempDir = Path.Combine(baseDir, "." + key + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                    zip.ExtractToDirectory(tempDir, overwriteFiles: true);

                if (!File.Exists(indexPath))
                {
                    try { Directory.Move(tempDir, finalDir); }
                    catch (IOException) { /* another process won the race — fine */ }
                }
            }
            finally
            {
                TryDelete(tempDir);
            }
        }
        catch
        {
            // Fall back to any previously extracted good copy; otherwise give up
            // and let the caller report a missing renderer.
        }

        return File.Exists(indexPath) ? finalDir : null;
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
