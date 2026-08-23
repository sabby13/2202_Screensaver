# Packaging (downloadable ZIP)

Builds a single ZIP for distribution on a website. When the user extracts it,
they get exactly two files:

```
GlassButterfly.scr   <- self-contained; the entire renderer is embedded inside
README.txt           <- end-user install instructions (packaging/README.txt)
```

No installer, no System32, no registry. The user right-clicks
`GlassButterfly.scr` → **Install**, which opens the Windows Screen Saver panel
where **Preview** and **Settings** work directly.

## Why the renderer is embedded

The screensaver ships as a lone `.scr` file, so there is no asset folder beside
it. `packaging/build-package.ps1` zips the built renderer (`dist/web`) into
`native/GlassButterflyHost/renderer.zip`, which the project embeds into the
assembly. At runtime `RendererAssets.EnsureExtracted()` unpacks it once to a
content-versioned cache (`%LOCALAPPDATA%\GlassButterfly\renderer\<hash>`) and
WebView2 loads from there. A plain `dotnet run` (no `renderer.zip`) falls back to
`dist/web`, so development is unchanged.

## Prerequisites

- Node.js + `npm install` (renderer build)
- .NET 8 SDK (host publish)
- Windows x64 (for `dotnet publish -r win-x64`)

Inno Setup is **no longer required** — the previous installer approach was
replaced by this ZIP.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File packaging\build-package.ps1
```

Output: `packaging\Output\GlassButterfly-Screensaver.zip`.
Use `-SkipWebBuild` to reuse the existing `dist/web`.

## How Windows drives the .scr

| User action (Screen Saver panel) | Windows runs | Host mode |
| --- | --- | --- |
| Right-click `.scr` → Install | opens the panel, sets this file active | — |
| Preview (full screen) | `GlassButterfly.scr /s` | `ScreensaverForm` |
| Small monitor thumbnail | `GlassButterfly.scr /p <hwnd>` | `PreviewForm` |
| Settings button | `GlassButterfly.scr /c` | `ConfigForm` (settings.html) |
| Idle timeout fires | `GlassButterfly.scr /s` | `ScreensaverForm` |

## Notes

- The `.scr` is self-contained .NET 8 (no runtime prerequisite). Only the
  Evergreen **WebView2 Runtime** is required at runtime; it's present on Win11
  and most updated Win10. `README.txt` tells users where to get it if missing.
- Expect a ~70–150 MB `.scr` (self-contained runtime + embedded renderer).
- On each cold start the single-file exe extracts native libraries to `%TEMP%`;
  this adds a small, one-time-per-launch delay that is fine for a screensaver.
