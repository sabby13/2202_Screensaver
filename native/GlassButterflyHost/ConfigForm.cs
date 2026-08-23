using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace GlassButterfly;

/// <summary>
/// Configuration window (/c) — a normal dialog-sized window hosting the existing
/// settings UI. Also used as the developer "--windowed" host (loads the
/// screensaver page in a resizable window) for quick iteration outside Electron.
/// </summary>
internal sealed class ConfigForm : Form
{
    private readonly WebView2 _web = new();
    private readonly SettingsStore _store = new();
    private readonly string _rendererDir;
    private readonly string _entry;

    public ConfigForm(
        string rendererDir,
        string entry = "settings.html",
        int width = 460,
        int height = 640,
        string title = "GlassButterfly Settings")
    {
        _rendererDir = rendererDir;
        _entry = entry;

        Text = title;
        Width = width;
        Height = height;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.Black;
        // Show the app icon (from ApplicationIcon) on the title bar and taskbar.
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is not null) Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
        }
        catch { /* fall back to the default icon */ }
        // Settings dialog is fixed-size; the dev windowed host stays resizable.
        if (entry == "settings.html")
        {
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
        }

        _web.Dock = DockStyle.Fill;
        _web.DefaultBackgroundColor = Color.Black;
        Controls.Add(_web);

        Load += async (_, _) => await OnLoadAsync();
    }

    private async Task OnLoadAsync()
    {
        BootstrapResult result = await WebViewBootstrapper.InitializeAsync(
            _web, _rendererDir, _entry, _store, Close, () => !IsDisposed);

        if (!result.Ok)
        {
            ShowStartupError();
            Close();
            return;
        }

        // DIAGNOSTIC BUILD: auto-open DevTools for the windowed dev host so the
        // page console/network is visible (helps diagnose a blank render).
        if (_entry == "index.html" && !IsDisposed && _web.CoreWebView2 is not null)
        {
            try { _web.CoreWebView2.OpenDevToolsWindow(); } catch { /* ignore */ }
        }
    }

    /// <summary>Friendly, actionable message when the display engine can't start
    /// (almost always a missing WebView2 Runtime). The full technical detail is
    /// still written to the log file by the bootstrapper.</summary>
    private static void ShowStartupError()
    {
        const string url = "https://developer.microsoft.com/microsoft-edge/webview2/";
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GlassButterfly", "webview-init-error.log");

        string message =
            "GlassButterfly couldn't start its display engine.\n\n" +
            "This usually means the Microsoft Edge WebView2 Runtime isn't installed. " +
            "It's included with Windows 11 and most up-to-date Windows 10 PCs.\n\n" +
            "Open the free download page now?\n\n" +
            "(Technical details were saved to:\n" + logPath + ")";

        DialogResult choice = MessageBox.Show(
            message, "GlassButterfly",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (choice == DialogResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { /* nothing more we can do */ }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        try { _web.Dispose(); } catch { /* ignore */ }
    }
}
