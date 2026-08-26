param(
    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repository = "Teknesyum/Runly"
$installPath = Join-Path $env:LOCALAPPDATA "Programs\Runly"
$temporaryPath = $null

Write-Host "=== Runly Installer ===" -ForegroundColor Cyan

try {
    # Use a local build when the script runs from a repository checkout. When invoked through
    # `irm ... | iex`, download the latest published Windows package from GitHub instead.
    $localDist = if ($PSScriptRoot) {
        Join-Path (Split-Path $PSScriptRoot -Parent) "dist"
    } else {
        $null
    }

    if ($localDist -and (Test-Path (Join-Path $localDist "Runly.exe"))) {
        $packagePath = $localDist
        Write-Host "Using local Release build." -ForegroundColor DarkGray
    } else {
        Write-Host "Downloading the latest Runly release..." -ForegroundColor Cyan
        $headers = @{ "User-Agent" = "Runly-Installer" }
        $release = Invoke-RestMethod "https://api.github.com/repos/$repository/releases/latest" -Headers $headers
        $asset = $release.assets |
            Where-Object { $_.name -match '^Runly-v.+-win-x64\.zip$' } |
            Select-Object -First 1

        if (-not $asset) {
            throw "The latest release does not contain a Windows x64 package."
        }

        $checksumAsset = $release.assets |
            Where-Object { $_.name -eq ($asset.name + ".sha256") } |
            Select-Object -First 1

        $temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ("Runly-" + [Guid]::NewGuid().ToString("N"))
        $archivePath = Join-Path $temporaryPath $asset.name
        $packagePath = Join-Path $temporaryPath "package"
        New-Item -ItemType Directory -Path $packagePath -Force | Out-Null

        Invoke-WebRequest $asset.browser_download_url -Headers $headers -OutFile $archivePath

        # Verify before extracting, never after: an altered archive must not reach the disk as files.
        # Releases published before checksums existed have no .sha256 asset, so warn instead of failing.
        if ($checksumAsset) {
            $checksumPath = "$archivePath.sha256"
            Invoke-WebRequest $checksumAsset.browser_download_url -Headers $headers -OutFile $checksumPath

            $checksumLine = Get-Content -LiteralPath $checksumPath -TotalCount 1
            $expectedHash = ($checksumLine -split '\s+' | Where-Object { $_ }) | Select-Object -First 1
            if ($expectedHash -notmatch '^[0-9a-fA-F]{64}$') {
                throw "The published checksum for $($asset.name) could not be read."
            }

            $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
            if ($actualHash -ne $expectedHash) {
                throw "Checksum mismatch for $($asset.name). Expected $($expectedHash.ToLowerInvariant()) but the download is $($actualHash.ToLowerInvariant()). Installation stopped."
            }

            Write-Host "SHA-256 checksum verified." -ForegroundColor DarkGray
        } else {
            Write-Warning "This release does not publish a .sha256 checksum, so the download could not be verified."
        }

        Expand-Archive -LiteralPath $archivePath -DestinationPath $packagePath -Force
    }

    # Runly ships two launchers: Runly.exe opens files in an application, RunlyConsole.exe runs them.
    # A package missing either one installs associations that point at a file that is not there.
    foreach ($required in @("Runly.exe", "RunlyConsole.exe", "RunlySettings.exe")) {
        if (-not (Test-Path (Join-Path $packagePath $required))) {
            throw "$required was not found in the installation package."
        }
    }

    Write-Host "Installing to $installPath..." -ForegroundColor Cyan
    if (Test-Path $installPath) {
        Remove-Item -LiteralPath $installPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    Copy-Item (Join-Path $packagePath "*") -Destination $installPath -Recurse -Force

    $uninstallerPath = Join-Path $installPath "uninstall.ps1"
    $localUninstaller = if ($PSScriptRoot) { Join-Path $PSScriptRoot "uninstall.ps1" } else { $null }
    if ($localUninstaller -and (Test-Path $localUninstaller)) {
        Copy-Item -LiteralPath $localUninstaller -Destination $uninstallerPath -Force
    } else {
        Invoke-WebRequest "https://raw.githubusercontent.com/$repository/main/scripts/uninstall.ps1" `
            -Headers $headers -OutFile $uninstallerPath
    }

    $settingsExe = Join-Path $installPath "RunlySettings.exe"
    $desktopPath = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    $shortcutPath = Join-Path $desktopPath "Runly.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $settingsExe
    $shortcut.WorkingDirectory = $installPath
    $shortcut.IconLocation = "$settingsExe,0"
    $shortcut.Description = "Runly Settings"
    $shortcut.Save()

    Write-Host "Runly installed successfully." -ForegroundColor Green
    Write-Host "Desktop shortcut: $shortcutPath" -ForegroundColor Green

    # The repository front page is always English. The machine has already stated its language, so
    # the guide offered here follows it instead of asking the reader to find the other file.
    if ((Get-UICulture).TwoLetterISOLanguageName -eq "tr") {
        Write-Host "Kilavuz: https://github.com/Teknesyum/Runly/blob/main/README.tr.md" -ForegroundColor Green
    } else {
        Write-Host "Guide: https://github.com/Teknesyum/Runly#readme" -ForegroundColor Green
    }

    if (-not $SkipLaunch) {
        Start-Process -FilePath $settingsExe
        Write-Host "Runly Settings opened. Choose the script extensions you want to enable." -ForegroundColor Cyan
    }
} finally {
    if ($temporaryPath -and (Test-Path $temporaryPath)) {
        Remove-Item -LiteralPath $temporaryPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}
