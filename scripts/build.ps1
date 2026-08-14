param(
    [string]$Configuration = "Release",
    [string]$Output = "dist",
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Sürüm tek yerden gelir: Directory.Build.props. Buraya sabit yazılırsa paket adı ile içindeki
# ikilinin sürümü ayrışır ve yanlış isimli yayın çıkar.
if (-not $Version) {
    $propsPath = Join-Path (Split-Path $PSScriptRoot -Parent) "Directory.Build.props"
    $match = Select-String -Path $propsPath -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
    if (-not $match) {
        Write-Host "HATA: Directory.Build.props icinde <Version> bulunamadi." -ForegroundColor Red
        exit 1
    }
    $Version = $match.Matches[0].Groups[1].Value.Trim()
}

Write-Host "=== Runly Build Başladı ===" -ForegroundColor Green
Write-Host "Yapılandırma: $Configuration"
Write-Host "Sürüm: $Version"
Write-Host "Çıktı: $Output`n"

# 1. Testler
Write-Host "> dotnet test calistiriliyor..." -ForegroundColor Cyan
try {
    dotnet test --configuration $Configuration 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "HATA: Testler başarısız oldu." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "HATA: Test çalıştırması başarısız: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 2. Launcher publish (K8: Defender retry)
Write-Host "> Runly.Launcher AOT publish yapılıyor (3 deneme, 500ms bekleme)..." -ForegroundColor Cyan
$launcherPublished = $false
for ($i = 1; $i -le 3; $i++) {
    try {
        Write-Host "  Deneme $i/3..."
        dotnet publish "src/Runly.Launcher" -c $Configuration -r win-x64 /p:PublishAot=true 2>&1
        if ($LASTEXITCODE -eq 0) {
            $launcherPublished = $true
            Write-Host "  * Başarılı" -ForegroundColor Green
            break
        }
    } catch {
        Write-Host "  ✗ Hata: $_"
    }
    if ($i -lt 3) {
        Write-Host "  500ms bekleniyor..."
        Start-Sleep -Milliseconds 500
    }
}

if (-not $launcherPublished) {
    Write-Host "HATA: Runly.Launcher publish 3 deneme sonunda başarısız oldu." -ForegroundColor Red
    exit 1
}

Write-Host ""

# 3. Settings publish
Write-Host "> Runly.Settings publish yapılıyor..." -ForegroundColor Cyan
try {
    dotnet publish "src/Runly.Settings" -c $Configuration -r win-x64 --self-contained 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "HATA: Runly.Settings publish başarısız oldu." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "HATA: Runly.Settings publish başarısız: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# 4. Çıktıları dist/ altında topla
Write-Host "> Çıktılar $Output/ altında toplanıyor..." -ForegroundColor Cyan

if (Test-Path $Output) {
    Remove-Item $Output -Recurse -Force
}
New-Item -ItemType Directory $Output | Out-Null

# Launcher çıktıları
$launcherBin = "src/Runly.Launcher/bin/$Configuration/net8.0/win-x64/publish"
if (Test-Path $launcherBin) {
    Get-ChildItem $launcherBin -File | Where-Object { $_.Extension -ne ".pdb" } | Copy-Item -Destination "$Output/" -Force
    Write-Host "  * Launcher dosyaları kopyalandı"
} else {
    Write-Host "HATA: Launcher publish klasörü bulunamadı: $launcherBin" -ForegroundColor Red
    exit 1
}

# Settings çıktıları
$settingsBin = "src/Runly.Settings/bin/$Configuration/net8.0-windows/win-x64/publish"
if (Test-Path $settingsBin) {
    # Self-contained WinForms output: every file is needed except the framework-dependent Runly.exe
    # apphost that lands here through the Runly.Core reference (the AOT launcher is copied above).
    Get-ChildItem $settingsBin -File |
        Where-Object { $_.Extension -ne ".pdb" -and $_.Name -notin @("Runly.exe", "Runly.xml") } |
        Copy-Item -Destination "$Output/" -Force
    Write-Host "  * Settings dosyaları kopyalandı"
} else {
    Write-Host "HATA: Settings publish klasörü bulunamadı: $settingsBin" -ForegroundColor Red
    exit 1
}

# Assets
if (Test-Path "assets") {
    $destAssets = "$Output/assets"
    if (-not (Test-Path $destAssets)) {
        New-Item -ItemType Directory $destAssets | Out-Null
    }
    Copy-Item "assets/*" -Destination $destAssets -Force -Recurse
    Write-Host "  * Assets kopyalandı"
}

# README.md
if (Test-Path "README.md") {
    Copy-Item "README.md" -Destination "$Output/README.md" -Force
    Write-Host "  * README.md kopyalandı"
}

# License
if (Test-Path "LICENSE") {
    Copy-Item "LICENSE" -Destination "$Output/LICENSE" -Force
    Write-Host "  * LICENSE kopyalandı"
}

Write-Host ""

# 5. Son durumu yazdır
Write-Host "> Build tamamlandı." -ForegroundColor Green
Write-Host ""

Write-Host "$Output/ içeriği:" -ForegroundColor Yellow
$outputRoot = (Resolve-Path $Output).Path.TrimEnd('\') + '\'
Get-ChildItem $Output -Recurse -File | Select-Object @{
    Name = "Dosya"
    Expression = { $_.FullName.Replace($outputRoot, "") }
}, @{
    Name = "Boyut (KB)"
    Expression = { [math]::Round($_.Length / 1KB, 2) }
} | Format-Table -AutoSize

$runlyExe = "$Output/Runly.exe"
if (Test-Path $runlyExe) {
    $size = (Get-Item $runlyExe).Length
    Write-Host "Runly.exe boyutu: $([math]::Round($size / 1MB, 2)) MB" -ForegroundColor Cyan
}

Write-Host ""

# 6. Paketle
$zipName = "Runly-v$Version-win-x64.zip"
Write-Host "> $zipName oluşturuluyor..." -ForegroundColor Cyan
if (Test-Path $zipName) {
    Remove-Item $zipName -Force
}
Compress-Archive -Path "$Output\*" -DestinationPath $zipName -CompressionLevel Optimal
$zipInfo = Get-Item $zipName
$zipHash = (Get-FileHash $zipName -Algorithm SHA256).Hash
Write-Host "  * $zipName — $([math]::Round($zipInfo.Length / 1MB, 2)) MB"
Write-Host "  * SHA-256: $zipHash" -ForegroundColor Yellow

Write-Host ""
Write-Host "* İnşa başarılı." -ForegroundColor Green
