<#
.SYNOPSIS
  Builds the GlassButterfly screensaver installer end to end.

.DESCRIPTION
  1. Builds the renderer (npm run build:web -> dist/web).
  2. Publishes the native host as a self-contained, single-file x64 executable.
  3. Stages GlassButterfly.scr (the renamed exe) + the renderer assets.
  4. Compiles installer\GlassButterfly.iss with Inno Setup (iscc.exe).

  The finished installer lands in installer\Output\GlassButterfly-Setup-x64.exe.

.PARAMETER Configuration
  Build configuration for dotnet publish. Default: Release.

.PARAMETER SkipWebBuild
  Skip "npm run build:web" (use the existing dist/web).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipWebBuild
)

$ErrorActionPreference = 'Stop'

$InstallerDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot     = Split-Path -Parent $InstallerDir
$HostProject  = Join-Path $RepoRoot 'native\GlassButterflyHost\GlassButterflyHost.csproj'
$DistWeb      = Join-Path $RepoRoot 'dist\web'
$PublishDir   = Join-Path $InstallerDir 'obj-publish'
$StageDir     = Join-Path $InstallerDir 'stage'
$IssFile      = Join-Path $InstallerDir 'GlassButterfly.iss'

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# 1. Renderer -------------------------------------------------------------------
if (-not $SkipWebBuild) {
    Write-Step 'Building renderer (npm run build:web)'
    Push-Location $RepoRoot
    try { npm run build:web } finally { Pop-Location }
}
if (-not (Test-Path (Join-Path $DistWeb 'index.html'))) {
    throw "Renderer build missing: $DistWeb\index.html"
}

# 2. Publish self-contained single-file x64 ------------------------------------
Write-Step 'Publishing native host (self-contained, single-file, win-x64)'
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish $HostProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=none `
    /p:DebugSymbols=false `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

$exe = Join-Path $PublishDir 'GlassButterfly.exe'
if (-not (Test-Path $exe)) { throw "Publish did not produce $exe" }

# 3. Stage ----------------------------------------------------------------------
Write-Step 'Staging .scr + renderer'
if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir | Out-Null

Copy-Item $exe (Join-Path $StageDir 'GlassButterfly.scr')

$stageRenderer = Join-Path $StageDir 'renderer'
New-Item -ItemType Directory -Path $stageRenderer | Out-Null
Copy-Item (Join-Path $DistWeb '*') $stageRenderer -Recurse -Force
if (-not (Test-Path (Join-Path $stageRenderer 'index.html'))) {
    throw "Staging failed: renderer\index.html not present"
}

# 4. Compile the installer ------------------------------------------------------
Write-Step 'Compiling Inno Setup installer'
$iscc = (Get-Command 'iscc.exe' -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\iscc.exe'),
        (Join-Path $env:ProgramFiles         'Inno Setup 6\iscc.exe')
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    throw "iscc.exe (Inno Setup 6) not found. Install it from https://jrsoftware.org/isdl.php"
}

& $iscc $IssFile
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed (exit $LASTEXITCODE)" }

Write-Step 'Done'
Write-Host ("Installer: {0}" -f (Join-Path $InstallerDir 'Output\GlassButterfly-Setup-x64.exe')) -ForegroundColor Green
