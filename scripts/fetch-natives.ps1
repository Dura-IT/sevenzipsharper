# fetch-natives.ps1 - Build the 7-Zip native DLLs for Windows (x64, arm64, x86) from the official source.
# Places them in src/SevenZipSharper.Native/runtimes/<RID>/native/.
#
# The official installers cannot be used: their 7z.dll always contains the unRAR code, whose licence
# restricts it to decompression and is not LGPL. This script builds Format7zF with nmake and removes the
# RAR objects from 7-Zip's Arc.mak first (the Windows makefile has no DISABLE_RAR switch, unlike
# Arc_gcc.mak that fetch-natives.sh uses). It fails when any RAR object is still referenced.
#
# Usage:
#   .\scripts\fetch-natives.ps1           # uses version from scripts\7zip-version
#   .\scripts\fetch-natives.ps1 -Version 2601
#
# Requirements: Visual Studio 2022 with the C++ build tools for x64, arm64 and x86 (installed on GitHub
# Actions windows-latest), and tar with xz support (Windows 10 or later).

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

# --- Download and verify the source ------------------------------------------------------------

$TarballName = "7z$Version-src.tar.xz"
$TarballPath = Join-Path $WorkDir $TarballName
$SrcDir      = Join-Path $WorkDir "7z$Version-src"

if (-not (Test-Path $TarballPath)) {
    Write-Output "--> Downloading $TarballName..."
    Invoke-WebRequest -Uri "$BaseUrl/$TarballName" -OutFile $TarballPath
}

Write-Output "--> Verifying checksum..."
$HashFile    = Join-Path $ScriptDir "7zip-sha256"
$MatchedLine = Get-Content $HashFile | Where-Object { $_ -match "^([0-9a-f]+)\s+$([regex]::Escape($TarballName))$" } | Select-Object -First 1
if (-not $MatchedLine) {
    throw "No checksum entry for $TarballName in scripts/7zip-sha256"
}
$ExpectedHash = ($MatchedLine -split '\s+')[0]
$ActualHash   = (Get-FileHash $TarballPath -Algorithm SHA256).Hash.ToLower()
if ($ActualHash -ne $ExpectedHash) {
    Remove-Item $TarballPath -Force
    throw "Checksum mismatch for ${TarballName}: expected $ExpectedHash, got $ActualHash"
}
Write-Output "--> Checksum OK."

if (Test-Path $SrcDir) {
    Remove-Item $SrcDir -Recurse -Force   # always start from pristine source, the patch below edits it
}
New-Item -ItemType Directory -Force -Path $SrcDir | Out-Null
Write-Output "--> Extracting source..."
& tar -xf $TarballPath -C $SrcDir
if ($LASTEXITCODE -ne 0) {
    throw "tar failed to extract $TarballName"
}

# --- Remove the RAR code from the Windows makefile ---------------------------------------------

$FormatDir = Join-Path $SrcDir "CPP\7zip\Bundles\Format7zF"
$ArcMak    = Join-Path $FormatDir "Arc.mak"
if (-not (Test-Path $ArcMak)) {
    throw "Arc.mak not found at $FormatDir - the source layout may have changed, check the 7-Zip release."
}

Write-Output "--> Removing RAR objects from Arc.mak..."
$Lines   = Get-Content $ArcMak
$Patched = New-Object System.Collections.Generic.List[string]
$Skip    = $false
foreach ($Line in $Lines) {
    if ($Line -match '^RAR_OBJS\s*=') {
        $Skip = $true            # drop the whole RAR_OBJS block; 7zip.mak only uses it when it is defined
        continue
    }
    if ($Skip) {
        if ($Line -match '^\s+\$O\\') { continue }
        $Skip = $false
    }
    if ($Line -match '^\s+\$O\\(Rar\w*|Rar20Crypto|Rar5Aes|RarAes)\.obj\s*\\') {
        continue
    }
    $Patched.Add($Line)
}
Set-Content -Path $ArcMak -Value $Patched

$Leftover = $Patched | Where-Object { $_ -match 'Rar' }
if ($Leftover) {
    throw "RAR references remain in Arc.mak after patching: $($Leftover -join '; ')"
}

# --- Build each architecture -------------------------------------------------------------------

$VsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$VsPath  = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $VsPath) {
    throw "Visual Studio with the C++ build tools was not found."
}
$VcVars = Join-Path $VsPath "VC\Auxiliary\Build\vcvarsall.bat"

function Build-WindowsDll {
    param(
        [string]$Rid,       # "win-x64"
        [string]$VcArch,    # vcvarsall argument: "x64", "x64_arm64" or "x64_x86"
        [string]$Platform,  # nmake PLATFORM value (vcvarsall also sets it in the environment)
        [string]$OutDir     # nmake output directory under Format7zF
    )

    Write-Output "==> 7-Zip $VersionDotted  |  target: $Rid"

    $OutPath = Join-Path $FormatDir $OutDir
    if (Test-Path $OutPath) {
        Remove-Item $OutPath -Recurse -Force
    }

    $PlatformArg = if ($Platform) { "PLATFORM=$Platform" } else { "" }
    $Command     = "`"$VcVars`" $VcArch && cd /d `"$FormatDir`" && nmake $PlatformArg"
    & cmd.exe /c $Command
    if ($LASTEXITCODE -ne 0) {
        throw "nmake failed for $Rid"
    }

    $Dll = Join-Path $OutPath "7z.dll"
    if (-not (Test-Path $Dll)) {
        throw "7z.dll not found at $Dll after building $Rid"
    }

    $DestDir = Join-Path $RuntimesDir "$Rid\native"
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
    $Dest = Join-Path $DestDir "7z.dll"
    Copy-Item $Dll $Dest -Force
    Write-Output "==> Installed: $Dest"
}

Build-WindowsDll -Rid "win-x64"   -VcArch "x64"       -Platform "x64"   -OutDir "x64"
Build-WindowsDll -Rid "win-arm64" -VcArch "x64_arm64" -Platform "arm64" -OutDir "arm64"
Build-WindowsDll -Rid "win-x86"   -VcArch "x64_x86"   -Platform "x86"   -OutDir "x86"
