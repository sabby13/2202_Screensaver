using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace GlassButterfly;

/// <summary>
/// Full screensaver window (/s): borderless, topmost, no taskbar, cursor hidden,
/// spanning the entire virtual desktop (all monitors). Dismisses on real user
/// input via global low-level hooks. Hosts the unchanged renderer.
/// </summary>
internal sealed class ScreensaverForm : Form
{
    private readonly WebView2 _web = new();
    private readonly SettingsStore _store = new();
    private readonly string _rendererDir;
    private InputExitHook? _hook;
    private bool _exiting;

    public ScreensaverForm(string rendererDir)
    {
        _rendererDir = rendererDir;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        DoubleBuffered = true;

        _web.Dock = DockStyle.Fill;
        _web.DefaultBackgroundColor = Color.Black;
        Controls.Add(_web);

        Load += async (_, _) => await OnLoadAsync();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        CoverVirtualScreen();
        Cursor.Hide();
    }

    /// <summary>Place the window over the whole virtual desktop in physical
    /// pixels (robust across mixed-DPI multi-monitor setups).</summary>
    private void CoverVirtualScreen()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int w = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int h = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, x, y, w, h,
            NativeMethods.SWP_SHOWWINDOW);
    }

    private async Task OnLoadAsync()
    {
        BootstrapResult result = await WebViewBootstrapper.InitializeAsync(
            _web, _rendererDir, "index.html", _store, ExitScreensaver, () => !IsDisposed);
        if (!result.Ok)
        {
            // Diagnostics (if any) are written to the log file by the bootstrapper.
            ExitScreensaver();
            return;
        }

        // Arm dismissal only after the content is up, so the initial cursor
        // position at launch is captured as the grace baseline.
        _hook = new InputExitHook(threshold: 8);
        _hook.Triggered += (_, _) =>
        {
            if (!IsDisposed) BeginInvoke(new Action(ExitScreensaver));
        };
        _hook.Start();
    }

    private void ExitScreensaver()
    {
        if (_exiting) return;
        _exiting = true;
        try { _hook?.Dispose(); } catch { /* ignore */ }
        try { Cursor.Show(); } catch { /* ignore */ }
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        try { _web.Dispose(); } catch { /* ignore */ }
        Application.Exit();
    }
}
