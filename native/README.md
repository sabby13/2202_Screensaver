# GlassButterfly — Native Windows host (Phase 6)

This folder holds the **native Windows integration layer** that will become the
production `GlassButterfly.scr`. It hosts the existing React + Three.js renderer
inside **WebView2** — the renderer is unchanged; this is only the Windows shell.

Electron is intentionally **still present** and unchanged (`npm run dev`,
`npm run build`). The native host is introduced alongside it and will replace
Electron in the production path only once proven.

## Milestones delivered so far

- **M1 — Native host loads renderer.** A resizable WebView2 test window shows the
  live renderer. ✅ (verified on Windows)
- **M2 — Real native bridge + settings + wallpapers.** `window.glass` is now
  backed by C#: settings persist to `%APPDATA%\GlassButterfly\settings.json`,
  the native image picker works, and custom (`glass-asset://`) wallpapers are
  served by the host. No Electron, no arbitrary filesystem access.

Still to come: `/s /c /p` screensaver lifecycle, installer + WebView2 runtime
handling, `.scr` registration.

## Prerequisites (Windows 10/11)

- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- **Evergreen WebView2 Runtime** — present on virtually all up-to-date Win10/11;
  otherwise https://developer.microsoft.com/microsoft-edge/webview2/
  (M2 uses the custom-scheme API, which needs a reasonably recent Evergreen
  runtime — any auto-updated Win10/11 is fine).
- **Node.js** — dev-time only, to build the renderer. End users won't need it.

## Build & run

From the **repo root**:

```powershell
npm install
npm run build:web            # builds the renderer -> dist/web

cd native\GlassButterflyHost
dotnet run                   # screensaver page (index.html)
dotnet run -- --settings     # configuration UI (settings.html) for testing persistence
```

Point at an explicit renderer folder if needed:
`dotnet run -- --renderer C:\path\to\dist\web`

## What to verify for M2 (report back)

Screensaver page (`dotnet run`):
- [ ] Renders as before (butterflies, clock/date, wallpaper).
- [ ] Reflects the **persisted** settings on first paint (see below).

Configuration page (`dotnet run -- --settings`):
- [ ] The GlassButterfly settings UI opens (no browser/menu chrome).
- [ ] Toggle 24-hour / seconds, change butterfly count (0–3), pick a built-in
      thumbnail — each change is saved.
- [ ] "Choose image…" opens the native Windows file picker; selecting a JPG/PNG
      shows it as the wallpaper (custom wallpaper served via `glass-asset://`).
- [ ] Close and reopen (either page) → your choices persisted.
- [ ] Inspect `%APPDATA%\GlassButterfly\settings.json` — it contains
      `backgroundImage`, `use24Hour`, `showSeconds`, `butterflyCount`, e.g.
      `"backgroundImage": "builtin:uwu"` or a custom path.

Fallbacks:
- [ ] Set a custom wallpaper, then delete/rename that file, then relaunch →
      it falls back to **Rome** (not a blank/black background).
- [ ] Corrupt `settings.json` (put garbage in it) → relaunch loads defaults
      without crashing.

## How it works (M2)

- `Bridge/bridge.js` (injected before page scripts) implements `window.glass`
  over WebView2 `postMessage`. The host substitutes the real settings JSON into
  it, so `initialSettings` is correct on the first render.
- `Bridge/HostBridge.cs` handles the messages: `getSettings`, `saveSettings`
  (persist + broadcast a live `settingsChanged`), `selectBackgroundImage`
  (native `OpenFileDialog`), `quit`.
- `Settings/SettingsStore.cs` owns `%APPDATA%\GlassButterfly\settings.json`
  (same schema as the renderer), with graceful missing/corrupt handling and the
  missing-custom-wallpaper → `builtin:rome` fallback.
- `Web/WallpaperResponder.cs` serves the renderer's `glass-asset://local/?src=…`
  URLs, restricted to existing image files (jpg/jpeg/png/webp) — no arbitrary
  filesystem access is exposed to JavaScript. Built-in JPGs don't use this path;
  they load as same-origin assets from the virtual host.

## Notes / limits

- Cross-window live sync (changing settings in one window updating another that's
  open simultaneously) isn't wired — not needed for a screensaver, where `/c` and
  `/s` aren't open at once. Same-window live updates work.
- WebView2 per-user data lives under `%LOCALAPPDATA%\GlassButterfly\WebView2`.
- If NuGet can't restore `Microsoft.Web.WebView2 1.0.2792.45`, bump to the latest
  `1.0.*` in `GlassButterflyHost.csproj`.

## Cannot be verified off-Windows

C#/.NET compilation and WebView2 execution could not be run in the authoring
environment. All build/run/verify steps above must be performed on Windows.
