# fetch-natives.ps1 — Download 7-Zip native DLLs for Windows (x64, arm64, x86).
# Places them in src/SevenZipSharper.Native/runtimes/<RID>/native/.
# The suffix-less installer (7z<version>.exe) is the 32-bit x86 build.
#
# Usage:
#   .\scripts\fetch-natives.ps1           # uses version from scripts\7zip-version
#   .\scripts\fetch-natives.ps1 -Version 2601
#
# Requirements: 7z.exe must be on PATH (used to extract DLLs from the installers).
# 7-Zip is pre-installed on GitHub Actions windows-latest runners.
# Install locally: winget install 7zip.7zip

param([string]$Version = "")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir

if (-not $Version) {
    $Version = (Get-Content (Join-Path $ScriptDir "7zip-version") -Raw).Trim()
}

$VersionDotted = "$($Version.Substring(0,2)).$($Version.Substring(2))"   # "2601" -> "26.01"
$BaseUrl       = "https://github.com/ip7z/7zip/releases/download/$VersionDotted"
$RuntimesDir   = Join-Path $RepoRoot "src\SevenZipSharper.Native\runtimes"
$WorkDir       = Join-Path $RepoRoot "artifacts\native-build"
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

function Fetch-WindowsDll {
    param(
        [string]$Arch,           # "x64" or "arm64"
        [string]$InstallerSuffix # "-x64" or "-arm64"
    )

    $RID     = "win-$Arch"
    $DestDir = Join-Path $RuntimesDir "$RID\native"
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

    Write-Output "==> 7-Zip $VersionDotted  |  target: $RID"

    $InstallerName = "7z$Version$InstallerSuffix.exe"
    $InstallerPath = Join-Path $WorkDir $InstallerName

    if (-not (Test-Path $InstallerPath)) {
        Write-Output "--> Downloading $InstallerName..."
        Invoke-WebRequest -Uri "$BaseUrl/$InstallerName" -OutFile $InstallerPath
    }

    Write-Output "--> Verifying checksum..."
    $HashFile = Join-Path $ScriptDir "7zip-sha256"
    $MatchedLine = Get-Content $HashFile | Where-Object { $_ -match "^([0-9a-f]+)\s+$([regex]::Escape($InstallerName))$" } | Select-Object -First 1
    if (-not $MatchedLine) {
        throw "No checksum entry for $InstallerName in scripts/7zip-sha256"
    }
    $ExpectedHash = ($MatchedLine -split '\s+')[0]
    $ActualHash   = (Get-FileHash $InstallerPath -Algorithm SHA256).Hash.ToLower()
    if ($ActualHash -ne $ExpectedHash) {
        Remove-Item $InstallerPath -Force
        throw "Checksum mismatch for ${InstallerName}: expected $ExpectedHash, got $ActualHash"
    }
    Write-Output "--> Checksum OK."

    $ExtractDir = Join-Path $WorkDir "7z$Version$InstallerSuffix"
    New-Item -ItemType Directory -Force -Path $ExtractDir | Out-Null

    Write-Output "--> Extracting 7z.dll..."
    & 7z e $InstallerPath "7z.dll" "-o$ExtractDir" -y | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "7z extraction failed for $InstallerName"
    }

    $DllSource = Join-Path $ExtractDir "7z.dll"
    if (-not (Test-Path $DllSource)) {
        throw "7z.dll not found in $ExtractDir after extraction"
    }

    $Dest = Join-Path $DestDir "7z.dll"
    Copy-Item $DllSource $Dest -Force
    Write-Output "==> Installed: $Dest"
}

Fetch-WindowsDll -Arch "x64"   -InstallerSuffix "-x64"
Fetch-WindowsDll -Arch "arm64" -InstallerSuffix "-arm64"
Fetch-WindowsDll -Arch "x86"   -InstallerSuffix ""      # suffix-less installer is the 32-bit x86 build
