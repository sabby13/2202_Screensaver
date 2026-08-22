using System.Windows.Forms;

namespace GlassButterfly;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        (ScreenSaverMode mode, IntPtr hwnd) = ScreenSaverArgs.Parse(args);
        string rendererDir = RendererLocator.Resolve(args);

        switch (mode)
        {
            case ScreenSaverMode.Password:
                // /a <HWND> — legacy "change password"; nothing to do on modern Windows.
                return 0;

            case ScreenSaverMode.Preview:
                if (hwnd == IntPtr.Zero) return 0;
                Application.Run(new PreviewForm(rendererDir, hwnd));
                return 0;

            case ScreenSaverMode.Screensaver:
                Application.Run(new ScreensaverForm(rendererDir));
                return 0;

            case ScreenSaverMode.Windowed:
                // Developer convenience: the screensaver page in a normal window.
                Application.Run(new ConfigForm(rendererDir, "index.html", 1280, 720,
                    "GlassButterfly (windowed dev host)"));
                return 0;

            case ScreenSaverMode.Configure:
            default:
                Application.Run(new ConfigForm(rendererDir));
                return 0;
        }
    }
}
