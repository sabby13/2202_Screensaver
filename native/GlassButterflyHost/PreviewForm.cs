using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace GlassButterfly;

/// <summary>
/// Preview window (/p &lt;HWND&gt;): a borderless child window parented into the
/// preview pane HWND that Windows supplies, sized to its client area, hosting the
/// unchanged renderer. Resilient to the preview parent being destroyed during or
/// after async WebView2 init (guarded init + a liveness watchdog), so it never
/// crashes or leaves a zombie process.
/// </summary>
internal sealed class PreviewForm : Form
{
    private readonly WebView2 _web = new();
    private readonly SettingsStore _store = new();
    private readonly string _rendererDir;
    private readonly IntPtr _parent;
    private System.Windows.Forms.Timer? _watchdog;

    public PreviewForm(string rendererDir, IntPtr parentHwnd)
    {
        _rendererDir = rendererDir;
        _parent = parentHwnd;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;

        _web.Dock = DockStyle.Fill;
        _web.DefaultBackgroundColor = Color.Black;
        Controls.Add(_web);

        Load += async (_, _) => await OnLoadAsync();
    }

    // Create the window as a child of the preview pane from the outset.
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.Style |= NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE;
            cp.Parent = _parent;
            return cp;
        }
    }

    private async Task OnLoadAsync()
    {
        if (!NativeMethods.IsWindow(_parent))
        {
            Close();
            return;
        }

        if (NativeMethods.GetClientRect(_parent, out NativeMethods.RECT rc))
            SetBounds(0, 0, rc.Width, rc.Height);

        BootstrapResult result = await WebViewBootstrapper.InitializeAsync(
            _web, _rendererDir, "index.html", _store, Close,
            () => !IsDisposed && NativeMethods.IsWindow(_parent));

        if (!result.Ok || !NativeMethods.IsWindow(_parent))
        {
            // Diagnostics (if any) are written to the log file by the bootstrapper.
            Close();
            return;
        }

        // Exit when Windows tears down the preview pane.
        _watchdog = new System.Windows.Forms.Timer { Interval = 500 };
        _watchdog.Tick += (_, _) =>
        {
            if (!NativeMethods.IsWindow(_parent)) Close();
        };
        _watchdog.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        try { _watchdog?.Dispose(); } catch { /* ignore */ }
        try { _web.Dispose(); } catch { /* ignore */ }
        Application.Exit();
    }
}
