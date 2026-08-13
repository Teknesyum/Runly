$ErrorActionPreference = "Stop"

$programPath = Join-Path $env:LOCALAPPDATA "Programs\Runly"
$dataPath = Join-Path $env:APPDATA "Runly"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)) "Runly.lnk"

Write-Host "=== Runly Uninstaller ===" -ForegroundColor Yellow

if (-not (Test-Path -LiteralPath $programPath)) {
    if (Test-Path -LiteralPath $desktopShortcut) {
        Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Runly is not installed." -ForegroundColor DarkGray
    exit 0
}

$settingsExe = Join-Path $programPath "RunlySettings.exe"
if (Test-Path -LiteralPath $settingsExe) {
    Write-Host "Removing Runly file associations..." -ForegroundColor Cyan
    try {
        & $settingsExe --uninstall 2>&1
    } catch {
        Write-Warning "Runly Settings could not complete its uninstall step: $_"
    }
}

if (Test-Path -LiteralPath $desktopShortcut) {
    Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
}

Write-Host "Removing $programPath..." -ForegroundColor Cyan
Remove-Item -LiteralPath $programPath -Recurse -Force

if (Test-Path -LiteralPath $dataPath) {
    $response = Read-Host "Remove Runly settings, logs, trust data, and backups too? (y/N)"
    if ($response -match '^(y|yes)$') {
        Remove-Item -LiteralPath $dataPath -Recurse -Force
        Write-Host "Runly user data removed." -ForegroundColor Green
    } else {
        Write-Host "Runly user data was kept at $dataPath." -ForegroundColor DarkGray
    }
}

Write-Host "Runly was uninstalled." -ForegroundColor Green
