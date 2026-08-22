using Microsoft.Web.WebView2.Core;

namespace GlassButterfly;

/// <summary>
/// Serves custom (user-provided) wallpapers for the existing renderer's
/// `glass-asset://local/?src=&lt;absolute path&gt;` URLs, so the renderer's
/// wallpaper logic works unchanged on the native host. Security: only files
/// with an allowed image extension that actually exist are served; nothing else
/// on disk is reachable from JavaScript. Built-in JPGs never use this path —
/// they load as same-origin assets from the virtual host.
/// </summary>
internal sealed class WallpaperResponder
{
    private readonly CoreWebView2Environment _env;

    public WallpaperResponder(CoreWebView2 core, CoreWebView2Environment env)
    {
        _env = env;
        core.AddWebResourceRequestedFilter("glass-asset://*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnRequest;
    }

    private void OnRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var uri = new Uri(e.Request.Uri);
            string? src = ExtractSrc(uri.Query);
            if (src is not null && IsServable(src))
            {
                var stream = File.OpenRead(src);
                e.Response = _env.CreateWebResourceResponse(
                    stream, 200, "OK", "Content-Type: " + MimeFor(src));
                return;
            }
        }
        catch
        {
            // fall through to 403
        }

        e.Response = _env.CreateWebResourceResponse(null, 403, "Forbidden", string.Empty);
    }

    private static string? ExtractSrc(string query)
    {
        foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0 && part[..eq] == "src")
                return Uri.UnescapeDataString(part[(eq + 1)..]);
        }
        return null;
    }

    private static bool IsServable(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(AppSettings.ImageExtensions, ext) >= 0 && File.Exists(path);
    }

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
