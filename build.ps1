# Builds NetworkSpeedTray.exe with the in-box .NET Framework 4.8 C# compiler.
# No SDK required. Run:  powershell -ExecutionPolicy Bypass -File build.ps1

$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "In-box C# compiler not found at $csc"
}

$root     = Split-Path -Parent $MyInvocation.MyCommand.Definition
$out      = Join-Path $root "NetworkSpeedTray.exe"
$manifest = Join-Path $root "app.manifest"
$sources  = Get-ChildItem (Join-Path $root "src") -Filter *.cs | ForEach-Object { $_.FullName }

Write-Host "Compiling $($sources.Count) source files..." -ForegroundColor Cyan

& $csc `
    /nologo `
    /target:winexe `
    /optimize+ `
    /out:$out `
    /win32manifest:$manifest `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded -> $out" -ForegroundColor Green
