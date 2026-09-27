param(
    [switch]$SkipLaunch,
    [switch]$Silent,
    [switch]$Rehearsal,
    [switch]$AutoStart,
    [string]$InstallPath
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# This file stays ASCII so `irm | iex` never meets a byte-order mark; every visible sentence lives in
# the table below and is decoded with Regex.Unescape.
$T = if ((Get-UICulture).TwoLetterISOLanguageName -eq "tr") {
    @{
        title = "Runly"; accent = " Kurulum"
        step0 = "S\u00fcr\u00fcm bulunuyor"; step1 = "Paket indiriliyor"; step2 = "Paket do\u011frulan\u0131yor"
        step3 = "Dosyalar kopyalan\u0131yor"; step4 = "K\u0131sayol yaz\u0131l\u0131yor"
        place = "Kurulum yeri"; change = "De\u011fi\u015ftir"; install = "Kur"; installing = "Kuruluyor"
        close = "Kapat"; open = "Program\u0131 a\u00e7"; retry = "Yeniden dene"
        ready = "Kurmaya haz\u0131r. Y\u00f6netici izni gerekmez, yaln\u0131z senin hesab\u0131na kurulur."
        rehearsal = "Prova kipi: ge\u00e7ici klas\u00f6re kurulur, k\u0131sayol yaz\u0131lmaz."
        rehearsalIgnored = "Prova kipi -InstallPath de\u011ferini yok sayar ({0}); hedef ge\u00e7ici klas\u00f6r."
        done = "Kurulum tamamland\u0131."; failed = "Kurulum yar\u0131da kald\u0131: {0}"
        localBuild = "Yerel derleme kullan\u0131l\u0131yor: {0}"; found = "Son s\u00fcr\u00fcm: {0}"
        noPackage = "Son s\u00fcr\u00fcmde Windows x64 paketi yok."
        downloaded = "{0} indi ({1} MB)"; skipLocal = "Yerel derleme, indirme atland\u0131"
        verified = "SHA-256 do\u011fruland\u0131"; noChecksum = "Bu s\u00fcr\u00fcm .sha256 yay\u0131mlam\u0131yor, do\u011frulanamad\u0131"
        badChecksum = "Yay\u0131mlanan sha256 okunamad\u0131."
        mismatch = "sha256 uyu\u015fmad\u0131: beklenen {0}, inen {1}. Kurulum durdu."
        missing = "{0} pakette yok."
        running = "Runly \u015fu an a\u00e7\u0131k. Kapat\u0131p yeniden dene."
        notEmpty = "Se\u00e7ilen klas\u00f6r bo\u015f de\u011fil ve Runly kurulumu de\u011fil: {0}"
        copied = "Kuruldu: {0}"; shortcut = "Masa\u00fcst\u00fc k\u0131sayolu: {0}"
        noShortcut = "Prova: k\u0131sayol yaz\u0131lmad\u0131"
        guide = "K\u0131lavuz: https://github.com/Teknesyum/Runly/blob/main/README.tr.md"
        opened = "Runly Ayarlar\u0131 a\u00e7\u0131ld\u0131. Kullanaca\u011f\u0131n betik uzant\u0131lar\u0131n\u0131 se\u00e7."
        pick = "Runly'nin kurulaca\u011f\u0131 klas\u00f6r\u00fc se\u00e7"
    }
} else {
    @{
        title = "Runly"; accent = " Setup"
        step0 = "Finding the release"; step1 = "Downloading the package"; step2 = "Verifying the package"
        step3 = "Copying files"; step4 = "Writing the shortcut"
        place = "Install location"; change = "Change"; install = "Install"; installing = "Installing"
        close = "Close"; open = "Open Runly"; retry = "Try again"
        ready = "Ready to install. No administrator rights needed; installs for your account only."
        rehearsal = "Rehearsal mode: installs to a temporary folder, writes no shortcut."
        rehearsalIgnored = "Rehearsal mode ignores -InstallPath ({0}); the target is a temporary folder."
        done = "Installation complete."; failed = "Installation stopped: {0}"
        localBuild = "Using local build: {0}"; found = "Latest release: {0}"
        noPackage = "The latest release has no Windows x64 package."
        downloaded = "{0} downloaded ({1} MB)"; skipLocal = "Local build, download skipped"
        verified = "SHA-256 verified"; noChecksum = "This release publishes no .sha256; not verified"
        badChecksum = "The published sha256 could not be read."
        mismatch = "sha256 mismatch: expected {0}, downloaded {1}. Installation stopped."
        missing = "{0} is missing from the package."
        running = "Runly is running. Close it and try again."
        notEmpty = "The chosen folder is not empty and is not a Runly installation: {0}"
        copied = "Installed: {0}"; shortcut = "Desktop shortcut: {0}"
        noShortcut = "Rehearsal: no shortcut written"
        guide = "Guide: https://github.com/Teknesyum/Runly#readme"
        opened = "Runly Settings opened. Choose the script extensions you want to enable."
        pick = "Choose the folder to install Runly into"
    }
}
foreach ($key in @($T.Keys)) { $T[$key] = [regex]::Unescape($T[$key]) }

$S = [hashtable]::Synchronized(@{
    Repository = "Teknesyum/Runly"
    Target = if ($InstallPath) { $InstallPath } else { Join-Path $env:LOCALAPPDATA "Programs\Runly" }
    ScriptRoot = $PSScriptRoot
    Rehearsal = [bool]$Rehearsal -or $env:RUNLY_PROVA -eq "1"
    Silent = [bool]$Silent -or $env:RUNLY_OTOMATIK -eq "1"
    Echo = $false
    State = "idle"
    Step = -1
    Steps = @("pending", "pending", "pending", "pending", "pending")
    Percent = 0.0
    Ceiling = 0.0
    Log = [System.Collections.ArrayList]::Synchronized((New-Object System.Collections.ArrayList))
    Error = $null
    Launch = $null
    T = $T
})
if ($S.Rehearsal) {
    $S.Target = Join-Path ([IO.Path]::GetTempPath()) "Runly-prova\Runly"
    if ($InstallPath) { [void]$S.Log.Add(($T.rehearsalIgnored -f $InstallPath)) }
}

$work = {
    param($S)
    $ErrorActionPreference = "Stop"
    $ProgressPreference = "SilentlyContinue"
    $T = $S.T

    function Say([string]$line) {
        [void]$S.Log.Add($line)
        if ($S.Echo) { Write-Host $line }
    }

    function Begin([int]$index, [double]$from, [double]$ceiling) {
        $S.Step = $index
        $S.Steps[$index] = "running"
        $S.Percent = [math]::Max($S.Percent, $from)
        $S.Ceiling = $ceiling
        Say $T["step$index"]
    }

    function Finish([int]$index, [double]$to) {
        $S.Steps[$index] = "done"
        $S.Percent = $to
    }

    $temporary = $null
    try {
        $S.State = "running"
        $S.Error = $null
        $S.Percent = 0.0
        for ($i = 0; $i -lt 5; $i++) { $S.Steps[$i] = "pending" }
        $headers = @{ "User-Agent" = "Runly-Installer" }
        $localDist = if ($S.ScriptRoot) { Join-Path (Split-Path $S.ScriptRoot -Parent) "dist" } else { $null }
        $useLocal = $localDist -and (Test-Path (Join-Path $localDist "Runly.exe"))

        Begin 0 0 10
        if ($useLocal) {
            $package = $localDist
            Say ($T.localBuild -f $localDist)
        } else {
            try {
                $release = Invoke-RestMethod "https://api.github.com/repos/$($S.Repository)/releases/latest" -Headers $headers
                $asset = $release.assets | Where-Object { $_.name -match '^Runly-v.+-win-x64\.zip$' } | Select-Object -First 1
                if (-not $asset) { throw $T.noPackage }
                $checksumAsset = $release.assets | Where-Object { $_.name -eq ($asset.name + ".sha256") } | Select-Object -First 1
                $tag = $release.tag_name
            } catch [System.Net.WebException] {
                $probe = [System.Net.HttpWebRequest]::Create("https://github.com/$($S.Repository)/releases/latest")
                $probe.AllowAutoRedirect = $false
                $probe.UserAgent = "Runly-Installer"
                $answer = $probe.GetResponse()
                $location = $answer.Headers["Location"]
                $answer.Close()
                if ($location -notmatch '/tag/(v[^/]+)$') { throw }
                $tag = $Matches[1]
                $base = "https://github.com/$($S.Repository)/releases/download/$tag/Runly-$tag-win-x64.zip"
                $asset = [pscustomobject]@{ name = "Runly-$tag-win-x64.zip"; browser_download_url = $base }
                $checksumAsset = [pscustomobject]@{ name = "Runly-$tag-win-x64.zip.sha256"; browser_download_url = "$base.sha256" }
            }
            Say ($T.found -f $tag)
        }
        Finish 0 10

        Begin 1 10 80
        if (-not $useLocal) {
            $temporary = Join-Path ([IO.Path]::GetTempPath()) ("Runly-" + [Guid]::NewGuid().ToString("N"))
            $archive = Join-Path $temporary $asset.name
            $package = Join-Path $temporary "package"
            New-Item -ItemType Directory -Path $package -Force | Out-Null
            $request = [System.Net.HttpWebRequest]::Create($asset.browser_download_url)
            $request.UserAgent = "Runly-Installer"
            $response = $request.GetResponse()
            try {
                $total = $response.ContentLength
                $source = $response.GetResponseStream()
                $target = [IO.File]::Create($archive)
                try {
                    $buffer = New-Object byte[] 65536
                    $done = 0L
                    while (($read = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $target.Write($buffer, 0, $read)
                        $done += $read
                        if ($total -gt 0) { $S.Percent = 10 + (70.0 * $done / $total) }
                    }
                } finally {
                    $target.Dispose()
                    $source.Dispose()
                }
            } finally {
                $response.Dispose()
            }
            Say ($T.downloaded -f $asset.name, [math]::Round($done / 1MB, 1))
        } else {
            Say $T.skipLocal
        }
        Finish 1 80

        Begin 2 80 86
        if (-not $useLocal) {
            # Verify before extracting, never after: an altered archive must not reach the disk as files.
            if ($checksumAsset) {
                $checksumText = (Invoke-WebRequest $checksumAsset.browser_download_url -Headers $headers -UseBasicParsing).Content
                if ($checksumText -is [byte[]]) { $checksumText = [Text.Encoding]::ASCII.GetString($checksumText) }
                $expected = ($checksumText -split '\s+' | Where-Object { $_ }) | Select-Object -First 1
                if ($expected -notmatch '^[0-9a-fA-F]{64}$') { throw $T.badChecksum }
                $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
                if ($actual -ne $expected) { throw ($T.mismatch -f $expected.ToLowerInvariant(), $actual.ToLowerInvariant()) }
                Say $T.verified
            } else {
                Say $T.noChecksum
            }
            Expand-Archive -LiteralPath $archive -DestinationPath $package -Force
        }
        foreach ($required in @("Runly.exe", "RunlyConsole.exe", "RunlySettings.exe")) {
            if (-not (Test-Path (Join-Path $package $required))) { throw ($T.missing -f $required) }
        }
        Finish 2 86

        Begin 3 86 96
        $installPath = $S.Target
        if (Test-Path -LiteralPath $installPath) {
            $isRunly = Test-Path -LiteralPath (Join-Path $installPath "RunlySettings.exe")
            $isEmpty = -not (Get-ChildItem -LiteralPath $installPath -Force | Select-Object -First 1)
            if (-not $isRunly -and -not $isEmpty) { throw ($T.notEmpty -f $installPath) }
            $full = [IO.Path]::GetFullPath($installPath).TrimEnd('\') + '\'
            $busy = Get-Process -Name "Runly", "RunlyConsole", "RunlySettings" -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -and $_.Path.StartsWith($full, [StringComparison]::OrdinalIgnoreCase) }
            if ($busy) { throw $T.running }
            Remove-Item -LiteralPath $installPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path $installPath -Force | Out-Null
        Copy-Item (Join-Path $package "*") -Destination $installPath -Recurse -Force
        $uninstaller = Join-Path $installPath "uninstall.ps1"
        $localUninstaller = if ($S.ScriptRoot) { Join-Path $S.ScriptRoot "uninstall.ps1" } else { $null }
        if ($localUninstaller -and (Test-Path $localUninstaller)) {
            Copy-Item -LiteralPath $localUninstaller -Destination $uninstaller -Force
        } else {
            Invoke-WebRequest "https://raw.githubusercontent.com/$($S.Repository)/main/scripts/uninstall.ps1" -Headers $headers -UseBasicParsing -OutFile $uninstaller
        }
        Say ($T.copied -f $installPath)
        Finish 3 96

        Begin 4 96 99
        $settingsExe = Join-Path $installPath "RunlySettings.exe"
        if ($S.Rehearsal) {
            Say $T.noShortcut
        } else {
            $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
            $shortcutPath = Join-Path $desktop "Runly.lnk"
            $shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcutPath)
            $shortcut.TargetPath = $settingsExe
            $shortcut.WorkingDirectory = $installPath
            $shortcut.IconLocation = "$settingsExe,0"
            $shortcut.Description = "Runly Settings"
            $shortcut.Save()
            Say ($T.shortcut -f $shortcutPath)
        }
        $S.Launch = $settingsExe
        Finish 4 100
        Say $T.done
        Say $T.guide
        $S.State = "done"
    } catch {
        if ($S.Step -ge 0) { $S.Steps[$S.Step] = "error" }
        $S.Error = $_.Exception.Message
        Say ($T.failed -f $S.Error)
        $S.State = "error"
    } finally {
        if ($temporary -and (Test-Path $temporary)) {
            Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

$gui = -not $S.Silent -and [Environment]::UserInteractive -and
    [Threading.Thread]::CurrentThread.GetApartmentState() -eq [Threading.ApartmentState]::STA
if ($gui) {
    try {
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
    } catch {
        $gui = $false
    }
}

if ($gui) {
    try {
        $tokenFile = if ($S.ScriptRoot) { Join-Path (Split-Path $S.ScriptRoot -Parent) "teknesyum-ui/theme.tokens.json" } else { $null }
        if (-not ($tokenFile -and (Test-Path $tokenFile))) {
            $tokenFile = Join-Path ([IO.Path]::GetTempPath()) "runly-theme.tokens.json"
            Invoke-WebRequest "https://raw.githubusercontent.com/$($S.Repository)/main/teknesyum-ui/theme.tokens.json" -UseBasicParsing -TimeoutSec 5 -OutFile $tokenFile
        }
        $K = [IO.File]::ReadAllText($tokenFile, [Text.Encoding]::UTF8) | ConvertFrom-Json
    } catch {
        $gui = $false
    }
}

if (-not $gui) {
    $S.Echo = $true
    Write-Host ($T.title + $T.accent) -ForegroundColor Cyan
    if ($S.Rehearsal) {
        Write-Host $T.rehearsal
        if ($InstallPath) { Write-Host ($T.rehearsalIgnored -f $InstallPath) }
    }
    & $work $S
    if ($S.State -ne "done") { throw $S.Error }
    if (-not $SkipLaunch -and -not $S.Silent -and -not $S.Rehearsal) {
        Start-Process -FilePath $S.Launch
        Write-Host $T.opened -ForegroundColor Cyan
    }
    return
}

function Rgb([string]$hex, [int]$alpha = 255) { [Drawing.Color]::FromArgb($alpha, [Drawing.ColorTranslator]::FromHtml($hex)) }
function Tok([string]$group, [string]$name) { $K.$group.$name.value }
function Alpha([double]$a) { [int][math]::Round($a * 255) }
$C = @{
    Bg = Rgb (Tok brand black); Surface = Rgb (Tok brand surface); Renk1 = Rgb (Tok brand renk-1); Renk2 = Rgb (Tok brand renk-2)
    Renk3 = Rgb (Tok brand renk-3); Renk2Text = Rgb (Tok brand renk-2-text); Renk3Text = Rgb (Tok brand renk-3-text)
    Success = Rgb (Tok role success); Text = Rgb (Tok role text); Disabled = Rgb (Tok role disabled)
    Border = Rgb (Tok brand renk-1) (Alpha $K.derived.border.alpha); BorderStrong = Rgb (Tok brand renk-1) (Alpha $K.derived.'border-strong'.alpha)
    Track = Rgb (Tok brand renk-1) (Alpha 0.1)
}
$Radius = Tok shape r
$WindowRadius = Tok shape r-window
$BorderW = Tok shape border-w
$FocusW = Tok shape focus-w
function Family([string[]]$names) {
    $installed = (New-Object Drawing.Text.InstalledFontCollection).Families | ForEach-Object { $_.Name }
    foreach ($name in $names) { if ($installed -contains $name) { return $name } }
    return $names[-1]
}
function Pt([string]$step) { (Tok size $step) * 0.75 }
$sans = Family @($K.font.sans.chain)
$mono = Family @($K.font.mono.chain)
$F = @{
    Title = New-Object Drawing.Font($sans, (Pt fs-3), [Drawing.FontStyle]::Bold)
    Body = New-Object Drawing.Font($sans, (Pt fs-2))
    Help = New-Object Drawing.Font($sans, (Pt fs-1))
    Label = New-Object Drawing.Font($sans, (Pt fs-1), [Drawing.FontStyle]::Bold)
    Mono = New-Object Drawing.Font($mono, (Pt fs-1))
    MonoBold = New-Object Drawing.Font($mono, (Pt fs-1), [Drawing.FontStyle]::Bold)
    Glyph = New-Object Drawing.Font($sans, (Pt fs-2))
}

$reducedMotion = -not [Windows.Forms.SystemInformation]::UIEffectsEnabled
$Anim = @{ Shown = 0.0; Scan = 0.0; Drag = $null; Hover = $null; Icon = $null }
$iconPath = if ($S.ScriptRoot) { Join-Path (Split-Path $S.ScriptRoot -Parent) "assets\runly.ico" } else { $null }
try {
    if ($iconPath -and (Test-Path $iconPath)) {
        $Anim.Icon = New-Object Drawing.Icon($iconPath, 48, 48)
    } else {
        $iconFile = Join-Path ([IO.Path]::GetTempPath()) "runly-setup.ico"
        Invoke-WebRequest "https://raw.githubusercontent.com/$($S.Repository)/main/assets/runly.ico" -UseBasicParsing -TimeoutSec 5 -OutFile $iconFile
        $Anim.Icon = New-Object Drawing.Icon($iconFile, 48, 48)
    }
} catch {
    $Anim.Icon = $null
}

$form = New-Object Windows.Forms.Form
$form.Text = $T.title + $T.accent
$form.FormBorderStyle = "None"
$form.StartPosition = "CenterScreen"
$IW = [int](Tok metric installer-w)
$IH = [int](Tok metric installer-h)
$DX = $IW - 560
$DY = $IH - 476
$form.ClientSize = New-Object Drawing.Size($IW, $IH)
$form.BackColor = $C.Bg
$form.ForeColor = $C.Text
$form.KeyPreview = $true
$form.ShowInTaskbar = $true
if ($Anim.Icon) { $form.Icon = $Anim.Icon }
$form.GetType().GetProperty("DoubleBuffered", [Reflection.BindingFlags]"Instance,NonPublic").SetValue($form, $true, $null)

$R = @{
    Close = New-Object Drawing.Rectangle(($IW - 48), 0, 48, 40)
    Change = New-Object Drawing.Rectangle((436 + $DX), (374 + $DY), 100, 32)
    Secondary = New-Object Drawing.Rectangle((292 + $DX), (420 + $DY), 116, 36)
    Primary = New-Object Drawing.Rectangle((420 + $DX), (420 + $DY), 116, 36)
}

function Round-Rect([Drawing.Rectangle]$r, [int]$radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $path.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $path.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $path
}

function Buttons {
    switch ($S.State) {
        "idle" { @{ Primary = $T.install; Secondary = $T.close; Busy = $false } }
        "running" { @{ Primary = $T.installing; Secondary = $null; Busy = $true } }
        "done" { if ($S.Rehearsal) { @{ Primary = $T.close; Secondary = $null; Busy = $false } } else { @{ Primary = $T.open; Secondary = $T.close; Busy = $false } } }
        default { @{ Primary = $T.retry; Secondary = $T.close; Busy = $false } }
    }
}

function Draw-Button($g, [Drawing.Rectangle]$r, [string]$text, [bool]$primary, [bool]$busy, [bool]$hover) {
    $path = Round-Rect (New-Object Drawing.Rectangle($r.X, $r.Y, ($r.Width - 1), ($r.Height - 1))) $Radius
    $width = if ($hover) { $FocusW } else { $BorderW }
    if ($busy) {
        $g.DrawPath((New-Object Drawing.Pen($C.Disabled, $BorderW)), $path)
        $fore = $C.Disabled
    } elseif ($primary) {
        $g.FillPath((New-Object Drawing.SolidBrush($C.Renk1)), $path)
        $g.DrawPath((New-Object Drawing.Pen($(if ($hover) { $C.Renk1 } else { $C.BorderStrong }), $width)), $path)
        $fore = $C.Bg
    } else {
        $g.DrawPath((New-Object Drawing.Pen([Drawing.Color]::FromArgb($(if ($hover) { 255 } else { $C.BorderStrong.A }), $C.Renk3), $width)), $path)
        $fore = $C.Renk3Text
    }
    [Windows.Forms.TextRenderer]::DrawText($g, $text, $F.Body, $r, $fore, [Windows.Forms.TextFormatFlags]"HorizontalCenter, VerticalCenter, EndEllipsis")
}

$form.Add_Paint({
    $g = $_.Graphics
    $g.SmoothingMode = "AntiAlias"
    $w = $form.ClientSize.Width
    $left = [Windows.Forms.TextFormatFlags]"Left, VerticalCenter, EndEllipsis, NoPadding"
    $right = [Windows.Forms.TextFormatFlags]"Right, VerticalCenter, NoPadding"

    $outline = Round-Rect (New-Object Drawing.Rectangle(0, 0, ($w - 1), ($form.ClientSize.Height - 1))) $WindowRadius
    $g.DrawPath((New-Object Drawing.Pen($C.Border, $BorderW)), $outline)
    $closeFore = if ($Anim.Hover -eq "Close") { $C.Renk2 } else { $C.Renk2Text }
    [Windows.Forms.TextRenderer]::DrawText($g, [string][char]0x00D7, $F.Glyph, $R.Close, $closeFore, [Windows.Forms.TextFormatFlags]"HorizontalCenter, VerticalCenter")

    $titleLeft = 24
    if ($Anim.Icon) {
        $g.DrawIcon($Anim.Icon, (New-Object Drawing.Rectangle(24, 24, 48, 48)))
        $titleLeft = 84
    }
    $titleWidth = [Windows.Forms.TextRenderer]::MeasureText($g, $T.title, $F.Title, [Drawing.Size]::Empty, [Windows.Forms.TextFormatFlags]::NoPadding).Width
    [Windows.Forms.TextRenderer]::DrawText($g, $T.title, $F.Title, (New-Object Drawing.Rectangle($titleLeft, 22, $titleWidth, 28)), $C.Text, $left)
    [Windows.Forms.TextRenderer]::DrawText($g, $T.accent, $F.Title, (New-Object Drawing.Rectangle(($titleLeft + $titleWidth), 22, 300, 28)), $C.Renk2Text, $left)
    $sub = if ($S.Rehearsal) { $T.rehearsal } else { $T.ready }
    [Windows.Forms.TextRenderer]::DrawText($g, $sub, $F.Help, (New-Object Drawing.Rectangle($titleLeft, 50, ($w - $titleLeft - 24), 22)), $C.Text, $left)

    for ($i = 0; $i -lt 5; $i++) {
        $y = 92 + ($i * 26)
        $state = $S.Steps[$i]
        $mark = New-Object Drawing.Rectangle(24, ($y + 2), 20, 20)
        switch ($state) {
            "done" { $markColor = $C.Success; $glyph = [string][char]0x2713; $labelColor = $C.Success }
            "error" { $markColor = $C.Renk2Text; $glyph = "!"; $labelColor = $C.Renk2Text }
            "running" { $markColor = $C.Renk1; $glyph = [string]($i + 1); $labelColor = $C.Renk1 }
            default { $markColor = $C.Border; $glyph = [string]($i + 1); $labelColor = $C.Text }
        }
        $g.DrawEllipse((New-Object Drawing.Pen($markColor, 1.5)), $mark)
        $glyphColor = if ($state -eq "pending") { $C.Text } else { $markColor }
        [Windows.Forms.TextRenderer]::DrawText($g, $glyph, $F.MonoBold, $mark, $glyphColor, [Windows.Forms.TextFormatFlags]"HorizontalCenter, VerticalCenter, NoPadding")
        [Windows.Forms.TextRenderer]::DrawText($g, $T["step$i"], $F.Body, (New-Object Drawing.Rectangle(54, $y, ($w - 78), 24)), $labelColor, $left)
    }

    $percentText = "%" + [string][math]::Floor($Anim.Shown)
    $sentence = switch ($S.State) {
        "idle" { "" }
        "done" { $T.done }
        "error" { $T.failed -f $S.Error }
        default { $T["step$($S.Step)"] }
    }
    $sentenceColor = switch ($S.State) { "done" { $C.Success } "error" { $C.Renk2Text } default { $C.Text } }
    [Windows.Forms.TextRenderer]::DrawText($g, $sentence, $F.Body, (New-Object Drawing.Rectangle(24, 226, ($w - 110), 24)), $sentenceColor, $left)
    $percentColor = if ($S.State -eq "done") { $C.Success } else { $C.Renk1 }
    [Windows.Forms.TextRenderer]::DrawText($g, $percentText, $F.MonoBold, (New-Object Drawing.Rectangle(($w - 84), 226, 60, 24)), $percentColor, $right)

    $track = New-Object Drawing.Rectangle(24, 258, ($w - 48), 8)
    $g.FillPath((New-Object Drawing.SolidBrush($C.Track)), (Round-Rect $track $Radius))
    $filled = [int]($track.Width * [math]::Min(100, $Anim.Shown) / 100)
    if ($filled -gt 8) {
        $bar = New-Object Drawing.Rectangle(24, 258, $filled, 8)
        $barPath = Round-Rect $bar $Radius
        if ($S.State -eq "error") {
            $brush = New-Object Drawing.SolidBrush($C.Renk2Text)
        } elseif ($S.State -eq "done") {
            $brush = New-Object Drawing.SolidBrush($C.Success)
        } else {
            $brush = New-Object Drawing.Drawing2D.LinearGradientBrush($track, $C.Renk1, $C.Renk2, 0.0)
        }
        $g.FillPath($brush, $barPath)
        if ($S.State -eq "running" -and -not $reducedMotion) {
            $light = [math]::Max(24, [int]($filled / 5))
            $x = [int](($Anim.Scan % 1.0) * ($filled + $light)) - $light
            $band = New-Object Drawing.Rectangle((24 + $x), 258, $light, 8)
            $shine = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Rectangle(($band.X - 1), 258, ($light + 2), 8)), [Drawing.Color]::FromArgb(0, $C.Text), [Drawing.Color]::FromArgb(0, $C.Text), 0.0)
            $blend = New-Object Drawing.Drawing2D.ColorBlend(3)
            $blend.Colors = @([Drawing.Color]::FromArgb(0, $C.Text), [Drawing.Color]::FromArgb((Alpha 0.3), $C.Text), [Drawing.Color]::FromArgb(0, $C.Text))
            $blend.Positions = @(0.0, 0.5, 1.0)
            $shine.InterpolationColors = $blend
            $g.SetClip($barPath)
            $g.FillRectangle($shine, $band)
            $g.ResetClip()
        }
    }

    # Older log lines fade, but never below 7:1 on black: white at alpha 150 is still 7.3:1.
    $lines = $S.Log.ToArray()
    $count = $lines.Count
    $cell = [Windows.Forms.TextRenderer]::MeasureText($g, ("M" * 40), $F.Mono, [Drawing.Size]::Empty, [Windows.Forms.TextFormatFlags]::NoPadding).Width / 40.0
    $perRow = [math]::Max(10, [math]::Floor(($w - 48) / $cell))
    $rows = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $count; $i++) {
        $text = [string]$lines[$i]
        do {
            $take = [math]::Min($perRow, $text.Length)
            [void]$rows.Add(@($text.Substring(0, $take), ($count - 1 - $i)))
            $text = $text.Substring($take)
        } while ($text.Length -gt 0)
    }
    $first = [math]::Max(0, $rows.Count - (5 + [math]::Floor($DY / 18)))
    for ($n = $first; $n -lt $rows.Count; $n++) {
        $age = $rows[$n][1]
        $color = if ($age -eq 0) { $C.Renk1 } else { [Drawing.Color]::FromArgb([math]::Max(150, 255 - ($age * 26)), $C.Text) }
        $y = 280 + (($n - $first) * 18)
        [Windows.Forms.TextRenderer]::DrawText($g, $rows[$n][0], $F.Mono, (New-Object Drawing.Rectangle(24, $y, ($w - 48), 18)), $color, $left)
    }

    $g.DrawLine((New-Object Drawing.Pen($C.Track, 1)), 24, (372 + $DY), ($w - 24), (372 + $DY))
    [Windows.Forms.TextRenderer]::DrawText($g, $T.place, $F.Label, (New-Object Drawing.Rectangle(24, (374 + $DY), 120, 32)), $C.Renk1, $left)
    [Windows.Forms.TextRenderer]::DrawText($g, $S.Target, $F.Mono, (New-Object Drawing.Rectangle(144, (374 + $DY), (280 + $DX), 32)), $C.Text, ($left -bor [Windows.Forms.TextFormatFlags]::PathEllipsis))
    if (-not $S.Rehearsal -and ($S.State -eq "idle" -or $S.State -eq "error")) {
        Draw-Button $g $R.Change $T.change $false $false ($Anim.Hover -eq "Change")
    }

    $b = Buttons
    Draw-Button $g $R.Primary $b.Primary $true $b.Busy ($Anim.Hover -eq "Primary")
    if ($b.Secondary) { Draw-Button $g $R.Secondary $b.Secondary $false $false ($Anim.Hover -eq "Secondary") }
})

function Hit([Drawing.Point]$p) {
    $b = Buttons
    if ($R.Close.Contains($p)) { return "Close" }
    if ($R.Primary.Contains($p) -and -not $b.Busy) { return "Primary" }
    if ($R.Secondary.Contains($p) -and $b.Secondary) { return "Secondary" }
    if (-not $S.Rehearsal -and $R.Change.Contains($p) -and ($S.State -eq "idle" -or $S.State -eq "error")) { return "Change" }
    return $null
}

$runspace = $null
$shell = $null
function Start-Work {
    if ($S.State -eq "running") { return }
    $S.State = "running"
    $S.Step = 0
    for ($i = 0; $i -lt 5; $i++) { $S.Steps[$i] = "pending" }
    $Anim.Shown = 0.0
    $S.Log.Clear()
    $script:runspace = [runspacefactory]::CreateRunspace()
    $script:runspace.ApartmentState = "STA"
    $script:runspace.Open()
    $script:shell = [powershell]::Create()
    $script:shell.Runspace = $script:runspace
    [void]$script:shell.AddScript($work).AddArgument($S)
    [void]$script:shell.BeginInvoke()
}

function Act([string]$what) {
    switch ($what) {
        "Close" { $form.Close() }
        "Secondary" { $form.Close() }
        "Change" {
            $dialog = New-Object Windows.Forms.FolderBrowserDialog
            $dialog.Description = $T.pick
            $dialog.SelectedPath = Split-Path $S.Target -Parent
            if ($dialog.ShowDialog($form) -eq "OK") {
                $chosen = $dialog.SelectedPath
                $S.Target = if ((Split-Path $chosen -Leaf) -eq "Runly") { $chosen } else { Join-Path $chosen "Runly" }
                $form.Invalidate()
            }
        }
        "Primary" {
            switch ($S.State) {
                "idle" { Start-Work }
                "error" { Start-Work }
                "done" {
                    if (-not $S.Rehearsal -and -not $SkipLaunch) { Start-Process -FilePath $S.Launch }
                    $form.Close()
                }
            }
        }
    }
}

$form.Add_MouseDown({
    if ($_.Button -ne "Left") { return }
    $hit = Hit $_.Location
    if ($hit) { Act $hit } else { $Anim.Drag = $_.Location }
})
$form.Add_MouseMove({
    if ($Anim.Drag) {
        $form.Location = New-Object Drawing.Point(($form.Location.X + $_.X - $Anim.Drag.X), ($form.Location.Y + $_.Y - $Anim.Drag.Y))
        return
    }
    $hit = Hit $_.Location
    if ($hit -ne $Anim.Hover) {
        $Anim.Hover = $hit
        $form.Cursor = if ($hit) { [Windows.Forms.Cursors]::Hand } else { [Windows.Forms.Cursors]::Default }
        $form.Invalidate()
    }
})
$form.Add_MouseUp({ $Anim.Drag = $null })
$form.Add_KeyDown({
    if ($_.KeyCode -eq "Escape" -and $S.State -ne "running") { $form.Close() }
    if ($_.KeyCode -eq "Enter" -and $S.State -ne "running") { Act "Primary" }
})
$form.Add_FormClosing({ if ($S.State -eq "running") { $_.Cancel = $true } })

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 16
$timer.Add_Tick({
    $target = [double]$S.Percent
    if ($reducedMotion) {
        $Anim.Shown = [math]::Max($Anim.Shown, $target)
    } elseif ($Anim.Shown -lt $target) {
        $Anim.Shown = [math]::Min($target, $Anim.Shown + [math]::Max(0.2, ($target - $Anim.Shown) * 0.08))
    } elseif ($S.State -eq "running" -and $Anim.Shown -lt $S.Ceiling) {
        $Anim.Shown += ($S.Ceiling - $Anim.Shown) * 0.006
    }
    $Anim.Scan += 0.012
    $form.Invalidate()
})
$form.Add_Shown({
    $timer.Start()
    if ($AutoStart) { Start-Work }
})

[void][Windows.Forms.Application]::Run($form)
$timer.Stop()
if ($script:shell) { $script:shell.Dispose() }
if ($script:runspace) { $script:runspace.Close() }
