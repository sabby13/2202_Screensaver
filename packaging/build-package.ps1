<#
.SYNOPSIS
  Builds the downloadable GlassButterfly screensaver ZIP.

.DESCRIPTION
  Produces a single ZIP that extracts to exactly two files:
      GlassButterfly.scr   (self-contained; the whole renderer is embedded)
      README.txt           (end-user install instructions)

  Steps:
    1. npm run build:web            -> dist/web
    2. zip dist/web                 -> native/GlassButterflyHost/renderer.zip (embedded)
    3. dotnet publish               -> self-contained, single-file, win-x64 exe
    4. stage GlassButterfly.scr + README.txt
    5. zip them                     -> packaging/Output/GlassButterfly-Screensaver.zip

.PARAMETER Configuration
  Build configuration for dotnet publish. Default: Release.

.PARAMETER SkipWebBuild
  Reuse the existing dist/web instead of rebuilding the renderer.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File packaging\build-package.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$SkipWebBuild
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$PackagingDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot     = Split-Path -Parent $PackagingDir
$HostDir      = Join-Path $RepoRoot 'native\GlassButterflyHost'
$HostProject  = Join-Path $HostDir  'GlassButterflyHost.csproj'
$DistWeb      = Join-Path $RepoRoot 'dist\web'
$RendererZip  = Join-Path $HostDir  'renderer.zip'
$PublishDir   = Join-Path $PackagingDir 'obj-publish'
$PackageDir   = Join-Path $PackagingDir 'package'
$OutputDir    = Join-Path $PackagingDir 'Output'

# Version comes from the .csproj (single source of truth) and names the ZIP.
$Version = '0.0.0'
if ((Get-Content $HostProject -Raw) -match '<Version>(.*?)</Version>') { $Version = $Matches[1] }
$FinalZip = Join-Path $OutputDir "GlassButterfly-Screensaver-v$Version.zip"

function Write-Step($m) { Write-Host "`n=== $m ===" -ForegroundColor Cyan }

# 1. Renderer -------------------------------------------------------------------
if (-not $SkipWebBuild) {
    Write-Step 'Building renderer (npm run build:web)'
    Push-Location $RepoRoot
    try { npm run build:web } finally { Pop-Location }
}
if (-not (Test-Path (Join-Path $DistWeb 'index.html'))) {
    throw "Renderer build missing: $DistWeb\index.html"
}

# 2. Zip renderer for embedding (contents at archive root) ----------------------
Write-Step 'Packing renderer for embedding'
if (Test-Path $RendererZip) { Remove-Item $RendererZip -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $DistWeb, $RendererZip,
    [System.IO.Compression.CompressionLevel]::Optimal, $false)

# 3. Publish self-contained single-file x64 ------------------------------------
Write-Step 'Publishing self-contained single-file .scr (win-x64)'
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

# 4. Stage the two shipped files -----------------------------------------------
Write-Step 'Staging GlassButterfly.scr + README.txt'
if (Test-Path $PackageDir) { Remove-Item $PackageDir -Recurse -Force }
New-Item -ItemType Directory -Path $PackageDir | Out-Null
Copy-Item $exe (Join-Path $PackageDir 'GlassButterfly.scr')
Copy-Item (Join-Path $PackagingDir 'README.txt') (Join-Path $PackageDir 'README.txt')

# 5. Zip for download (extracts to exactly the two files) ----------------------
Write-Step 'Building download ZIP'
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
if (Test-Path $FinalZip) { Remove-Item $FinalZip -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $PackageDir, $FinalZip,
    [System.IO.Compression.CompressionLevel]::Optimal, $false)

$sizeMB = [math]::Round((Get-Item $FinalZip).Length / 1MB, 1)
Write-Step 'Done'
Write-Host ("Download ZIP: {0}  ({1} MB)" -f $FinalZip, $sizeMB) -ForegroundColor Green
