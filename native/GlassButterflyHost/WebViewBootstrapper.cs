using System.Drawing;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GlassButterfly;

/// <summary>Result of a bootstrap attempt. On failure, <see cref="Diagnostics"/>
/// carries the full exception + environment context (empty when the attempt was
/// simply aborted because the host went away).</summary>
internal sealed record BootstrapResult(bool Ok, string Diagnostics)
{
    public static readonly BootstrapResult Success = new(true, string.Empty);
    public static BootstrapResult Aborted => new(false, string.Empty);
}

/// <summary>
/// Shared WebView2 setup for all host modes. NOTE (diagnostic build): failures no
/// longer return a bare false — the exact exception (type, HRESULT, full
/// ToString) and environment context are captured, written to
/// %LOCALAPPDATA%\GlassButterfly\webview-init-error.log, and returned so the
/// config window can show them.
/// </summary>
internal static class WebViewBootstrapper
{
    public const string VirtualHost = "glassbutterfly.assets";

    public static async Task<BootstrapResult> InitializeAsync(
        WebView2 web,
        string rendererDir,
        string entry,
        SettingsStore store,
        Action quit,
        Func<bool> stillValid)
    {
        // A per-process user-data folder. The Screen Saver control panel runs
        // several host processes at once (a /p preview thumbnail alongside the /c
        // settings window, etc.); if they share one WebView2 user-data folder,
        // controller creation fails with 0x8007139F ("resource not in the correct
        // state"). A unique folder per process avoids all cross-process contention.
        string userData = PrepareUserDataFolder();

        if (!File.Exists(Path.Combine(rendererDir, "index.html")))
            return Fail("renderer-missing (index.html not found)", null, rendererDir, userData);

        try { Directory.CreateDirectory(userData); }
        catch (Exception ex) { return Fail("create userDataFolder", ex, rendererDir, userData); }

        CoreWebView2Environment env;
        try
        {
            var glassScheme = new CoreWebView2CustomSchemeRegistration("glass-asset")
            {
                TreatAsSecure = true,
                HasAuthorityComponent = true,
                AllowedOrigins = new() { $"https://{VirtualHost}" }
            };
            // CustomSchemeRegistrations is get-only in this SDK; the registrations
            // must be supplied through the constructor (5th argument).
            var options = new CoreWebView2EnvironmentOptions(
                "", "", "", false,
                new List<CoreWebView2CustomSchemeRegistration> { glassScheme });

            env = await CoreWebView2Environment.CreateAsync(null, userData, options);
        }
        catch (Exception ex)
        {
            return Fail("CoreWebView2Environment.CreateAsync", ex, rendererDir, userData);
        }

        if (!stillValid()) return BootstrapResult.Aborted;

        try
        {
            await web.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            return Fail("EnsureCoreWebView2Async", ex, rendererDir, userData);
        }

        if (!stillValid()) return BootstrapResult.Aborted;

        try
        {
            web.DefaultBackgroundColor = Color.Black;
            CoreWebView2 core = web.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping(
                VirtualHost, rendererDir, CoreWebView2HostResourceAccessKind.Allow);

            _ = new WallpaperResponder(core, env);
            _ = new HostBridge(core, store, quit);

            await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildBridgeScript(store));
            if (!stillValid()) return BootstrapResult.Aborted;

            // Serve fresh renderer assets. WebView2 otherwise caches the mapped
            // virtual-host files (notably index.html, whose URL never changes), so
            // a rebuilt renderer can keep loading a stale hashed stylesheet. The
            // renderer is local, so there is no real caching benefit to lose.
            try { await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache); }
            catch { /* older runtime without Profile/ClearBrowsingData: ignore */ }
            if (!stillValid()) return BootstrapResult.Aborted;

            core.Navigate($"https://{VirtualHost}/{entry}");
        }
        catch (Exception ex)
        {
            return Fail("post-init setup / navigate", ex, rendererDir, userData);
        }

        return BootstrapResult.Success;
    }

    /// <summary>Returns a unique WebView2 user-data folder for this process and
    /// arranges for it to be removed on exit. Also best-effort sweeps folders left
    /// behind by host processes that are no longer running (folders belonging to a
    /// live process stay locked and are skipped, so a running host is never
    /// disturbed).</summary>
    private static string PrepareUserDataFolder()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GlassButterfly", "WebView2");

        try
        {
            Directory.CreateDirectory(root);
            foreach (string dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);
                int dash = name.IndexOf('-');
                string pidPart = dash > 0 ? name[..dash] : name;
                if (int.TryParse(pidPart, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out int pid)
                    && IsProcessAlive(pid))
                    continue; // owned by a running host — leave it alone
                try { Directory.Delete(dir, recursive: true); } catch { /* in use — skip */ }
            }
        }
        catch { /* best-effort */ }

        string folder = Path.Combine(root, $"{Environment.ProcessId:x}-{Guid.NewGuid():N}");
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort */ }
        };
        return folder;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch { return false; }
    }

    private static BootstrapResult Fail(string stage, Exception? ex, string rendererDir, string userData)
    {
        var sb = new StringBuilder();
        sb.AppendLine("GlassButterfly WebView2 initialization FAILED");
        sb.AppendLine("Stage: " + stage);
        sb.AppendLine();
        sb.AppendLine("AppContext.BaseDirectory   : " + AppContext.BaseDirectory);
        sb.AppendLine("Environment.CurrentDirectory: " + Environment.CurrentDirectory);
        sb.AppendLine("WebView2 userDataFolder    : " + userData);
        sb.AppendLine("renderer root              : " + rendererDir);
        sb.AppendLine("exists(renderer root)      : " + Directory.Exists(rendererDir));
        sb.AppendLine("exists(index.html)         : " + File.Exists(Path.Combine(rendererDir, "index.html")));
        sb.AppendLine("exists(settings.html)      : " + File.Exists(Path.Combine(rendererDir, "settings.html")));

        string assets = Path.Combine(rendererDir, "assets");
        sb.AppendLine("exists(assets/)            : " + Directory.Exists(assets));
        if (Directory.Exists(assets))
        {
            try { sb.AppendLine("assets/ file count         : " + Directory.GetFiles(assets).Length); }
            catch { /* ignore */ }
        }

        if (ex is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Exception type : " + ex.GetType().FullName);
            sb.AppendLine("HRESULT        : 0x" + ex.HResult.ToString("X8"));
            if (ex.InnerException is not null)
            {
                sb.AppendLine("Inner type     : " + ex.InnerException.GetType().FullName);
                sb.AppendLine("Inner HRESULT  : 0x" + ex.InnerException.HResult.ToString("X8"));
            }
            sb.AppendLine();
            sb.AppendLine("Full exception:");
            sb.AppendLine(ex.ToString());
        }

        string text = sb.ToString();

        try
        {
            string dir = Path.GetDirectoryName(userData) ?? userData; // %LOCALAPPDATA%\GlassButterfly
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "webview-init-error.log"), text);
        }
        catch { /* best-effort logging */ }

        return new BootstrapResult(false, text);
    }

    private static string BuildBridgeScript(SettingsStore store)
    {
        string template = ReadEmbedded("GlassButterfly.Bridge.bridge.js");
        return template.Replace("__GLASS_INITIAL_JSON__", store.Serialize(store.Current));
    }

    private static string ReadEmbedded(string logicalName)
    {
        using Stream? stream = typeof(WebViewBootstrapper).Assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {logicalName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
