# GlassButterfly installer

Builds a single Windows installer that ships the screensaver as a self-contained
x64 build, registers it, and ensures the WebView2 runtime is present.

## What it installs

| Item | Destination | Why |
| --- | --- | --- |
| `GlassButterfly.scr` | `%WINDIR%\System32` | So it appears in the Windows **Screen Saver** dropdown for the user to select. |
| `renderer\**` (HTML/JS/CSS + `butterfly.glb` + wallpapers) | `%ProgramFiles%\GlassButterfly\renderer` | The UI the `.scr` loads through WebView2 at runtime. |
| `HKLM\SOFTWARE\GlassButterfly\InstallDir` | registry | Lets the System32 `.scr` locate the renderer folder — Windows launches a screensaver with only `/s`, `/c`, or `/p <hwnd>`, so there is no way to pass a path. `RendererLocator` reads this key. |

The `.scr` is a **self-contained** .NET 8 build, so the target PC needs no .NET
runtime installed. The only external dependency is the **Evergreen WebView2
Runtime**, which the installer detects and installs automatically if missing.

## Prerequisites (build machine)

- Node.js + project deps (`npm install`) — to build the renderer.
- .NET 8 SDK — to publish the host.
- [Inno Setup 6.3 or newer](https://jrsoftware.org/isdl.php) — `iscc.exe` must be
  on `PATH` or installed to the default `Program Files` location. (6.3+ is
  required for the `x64compatible` architecture keyword; 6.1+ for the download
  page.)

## Build

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

Steps performed: `npm run build:web` → `dotnet publish` (self-contained,
single-file, `win-x64`) → stage `GlassButterfly.scr` + `renderer\` → compile
`GlassButterfly.iss`. Pass `-SkipWebBuild` to reuse the existing `dist/web`.

Output: `installer\Output\GlassButterfly-Setup-x64.exe`.

## How WebView2 is handled

`PrepareToInstall` checks the Evergreen runtime's `pv` value under the
`EdgeUpdate\Clients\{F3017226-...}` key (per-machine 64-bit, per-machine 32-bit,
and per-user). If it is absent, the installer downloads Microsoft's official
bootstrapper (`https://go.microsoft.com/fwlink/p/?LinkId=2124703`) and runs it
`/silent /install`. If the download fails, setup stops with instructions to
install the runtime manually.

## Testing after install

1. Right-click the desktop → **Personalize → Lock screen → Screen saver**, or run
   `control desk.cpl,,@screensaver`.
2. Pick **GlassButterfly** from the dropdown.
3. **Preview** renders it in the small monitor thumbnail (`/p <hwnd>`),
   **Settings** opens the config window (`/c`), and **Wait + OK** then idling
   starts the full screensaver (`/s`). Any real mouse move / key press exits.

## Uninstall

Standard **Apps & features** entry ("GlassButterfly Screensaver"). Removes the
`.scr`, the renderer folder, the registry key, and best-effort clears the
per-user WebView2 data folder (`%LOCALAPPDATA%\GlassButterfly`).

## Troubleshooting

- **"WebView2Loader.dll could not be loaded"** — extremely rare with
  single-file self-extract. If it happens, add
  `/p:IncludeAllContentForSelfExtract=true` to the `dotnet publish` line in
  `build-installer.ps1` and rebuild.
- **Not in the dropdown** — confirm the installer ran elevated and landed the
  `.scr` in real `System32` (not `SysWOW64`); the script sets
  `ArchitecturesInstallIn64BitMode` to guarantee this.
- **"Set as current screen saver" checkbox** applies to the *installing* user's
  `HKCU`. If you install elevated as a different account, select GlassButterfly
  from the dropdown instead.
