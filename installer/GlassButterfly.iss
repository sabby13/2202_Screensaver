; GlassButterfly screensaver installer (Inno Setup 6.1+)
; -----------------------------------------------------------------------------
; Installs a self-contained x64 build:
;   * GlassButterfly.scr  -> %WINDIR%\System32   (so it lists in the Windows
;                            "Screen Saver" dropdown for the user to pick)
;   * renderer\**         -> %ProgramFiles%\GlassButterfly\renderer
;   * HKLM\SOFTWARE\GlassButterfly\InstallDir     (so the System32 .scr can find
;                            the renderer folder without command-line arguments)
; Ensures the Evergreen WebView2 runtime is present, downloading Microsoft's
; bootstrapper on demand if it is not.
;
; Build it via build-installer.ps1, which stages files into .\stage first.
; -----------------------------------------------------------------------------

#define AppName "GlassButterfly"
#define AppVersion "1.0.0"
#define AppPublisher "GlassButterfly"
#define ScrName "GlassButterfly.scr"
; Evergreen WebView2 Runtime product code (used to detect an existing install).
#define WV2Guid "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

[Setup]
AppId={{B7E9F1C2-3A4D-4E5F-9A1B-2C3D4E5F6A7B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
; System32, HKLM and a machine-wide WebView2 install all require elevation.
PrivilegesRequired=admin
; x64-only, and install in native 64-bit mode so {sys} is the real System32
; (not the 32-bit SysWOW64 that a 32-bit installer would otherwise see).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=GlassButterfly-Setup-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={sys}\{#ScrName}
UninstallDisplayName={#AppName} Screensaver

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "setactive"; Description: "Set {#AppName} as the current screen saver"; Flags: unchecked

[Files]
; The screensaver executable. ignoreversion because a .scr carries no file
; version resource we want Inno to compare; restartreplace covers the rare case
; where the file is momentarily locked (e.g. a running preview).
Source: "stage\{#ScrName}"; DestDir: "{sys}"; Flags: ignoreversion restartreplace uninsrestartdelete
; Renderer assets (HTML/JS/CSS + butterfly.glb + wallpapers).
Source: "stage\renderer\*"; DestDir: "{app}\renderer"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Let the System32 .scr locate its renderer folder.
Root: HKLM; Subkey: "SOFTWARE\{#AppName}"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; Flags: uninsdeletekey
; Optional: make it the active screensaver for the installing user.
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "SCRNSAVE.EXE"; ValueData: "{sys}\{#ScrName}"; Tasks: setactive
Root: HKCU; Subkey: "Control Panel\Desktop"; ValueType: string; ValueName: "ScreenSaveActive"; ValueData: "1"; Tasks: setactive

[UninstallDelete]
; Best-effort cleanup of the per-user WebView2 data folder created at runtime.
Type: filesandordirs; Name: "{localappdata}\{#AppName}"

[Code]
var
  DownloadPage: TDownloadWizardPage;

function IsWebView2RuntimeInstalled: Boolean;
var
  pv: String;
begin
  Result := False;
  // Per-machine (native 64-bit view).
  if RegQueryStringValue(HKLM64,
       'SOFTWARE\Microsoft\EdgeUpdate\Clients\{#WV2Guid}', 'pv', pv) then
    if (pv <> '') and (pv <> '0.0.0.0') then Result := True;
  // Per-machine (32-bit registry view).
  if (not Result) and RegQueryStringValue(HKLM32,
       'SOFTWARE\Microsoft\EdgeUpdate\Clients\{#WV2Guid}', 'pv', pv) then
    if (pv <> '') and (pv <> '0.0.0.0') then Result := True;
  // Per-user install.
  if (not Result) and RegQueryStringValue(HKCU,
       'SOFTWARE\Microsoft\EdgeUpdate\Clients\{#WV2Guid}', 'pv', pv) then
    if (pv <> '') and (pv <> '0.0.0.0') then Result := True;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsWebView2RuntimeInstalled then
    Exit;

  // Fetch Microsoft's Evergreen WebView2 bootstrapper and run it silently.
  DownloadPage.Clear;
  DownloadPage.Add('https://go.microsoft.com/fwlink/p/?LinkId=2124703',
    'MicrosoftEdgeWebview2Setup.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := 'The Microsoft Edge WebView2 Runtime is required but could not be '
        + 'downloaded automatically:' + #13#10 + GetExceptionMessage + #13#10#13#10
        + 'Install the "Evergreen Standalone Installer" from '
        + 'https://developer.microsoft.com/microsoft-edge/webview2/ and run this setup again.';
      Exit;
    end;
  finally
    DownloadPage.Hide;
  end;

  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'),
       '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Result := 'Failed to launch the WebView2 Runtime installer.'
  else if ResultCode <> 0 then
    Result := Format('The WebView2 Runtime installer exited with code %d.', [ResultCode]);
end;
