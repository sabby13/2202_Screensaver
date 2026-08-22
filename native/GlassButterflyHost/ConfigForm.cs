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
            // DIAGNOSTIC BUILD: show the full failure detail so it can be copied.
            // (Windows message boxes support Ctrl+C to copy their contents.)
            if (result.Diagnostics.Length > 0)
            {
                MessageBox.Show(
                    result.Diagnostics,
                    "GlassButterfly — WebView2 init failed (press Ctrl+C to copy)",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        try { _web.Dispose(); } catch { /* ignore */ }
    }
}
