namespace GlassButterfly;

internal enum ScreenSaverMode
{
    Configure,
    Screensaver,
    Preview,
    Password,
    Windowed
}

/// <summary>
/// Parses the standard Windows screensaver command line, case-insensitively and
/// tolerant of both colon-attached (/p:1234) and space-separated (/p 1234) HWND
/// forms. No recognized flag → Configure (Windows and Explorer both use that for
/// double-click / "Configure").
/// </summary>
internal static class ScreenSaverArgs
{
    public static (ScreenSaverMode Mode, IntPtr Hwnd) Parse(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (string.IsNullOrEmpty(a)) continue;

            if (a.Equals("--windowed", StringComparison.OrdinalIgnoreCase))
                return (ScreenSaverMode.Windowed, IntPtr.Zero);

            if (a[0] != '/' && a[0] != '-') continue;
            if (a.Length < 2) continue;

            char flag = char.ToLowerInvariant(a[1]);

            // HWND may be attached (/p:1234 or /p1234) or the next argument.
            string rest = a.Length > 2 ? a[2..].TrimStart(':') : string.Empty;
            IntPtr hwnd = ParseHwnd(rest);
            if (hwnd == IntPtr.Zero && i + 1 < args.Length) hwnd = ParseHwnd(args[i + 1]);

            switch (flag)
            {
                case 's': return (ScreenSaverMode.Screensaver, IntPtr.Zero);
                case 'p': return (ScreenSaverMode.Preview, hwnd);
                case 'c': return (ScreenSaverMode.Configure, hwnd);
                case 'a': return (ScreenSaverMode.Password, hwnd);
            }
        }

        return (ScreenSaverMode.Configure, IntPtr.Zero);
    }

    private static IntPtr ParseHwnd(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return IntPtr.Zero;
        return long.TryParse(s.Trim(), out long v) && v != 0 ? new IntPtr(v) : IntPtr.Zero;
    }
}
