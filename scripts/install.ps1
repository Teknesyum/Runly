$ErrorActionPreference = "Stop"

$installPath = "$env:LOCALAPPDATA\Programs\Runly"
$distPath = "dist"

Write-Host "=== Runly Kurulumu ===" -ForegroundColor Green

# Kontrol: dist/ var mı?
if (-not (Test-Path $distPath)) {
    Write-Host "HATA: $distPath klasörü bulunamadı. Önce build.ps1 çalıştırın." -ForegroundColor Red
    exit 1
}

# Kontrol: Runly.exe var mı?
$runlyExe = "$distPath/Runly.exe"
if (-not (Test-Path $runlyExe)) {
    Write-Host "HATA: $runlyExe bulunamadı." -ForegroundColor Red
    exit 1
}

Write-Host "Kurulum yolu: $installPath" -ForegroundColor Cyan
Write-Host ""

# Eski kurulumu kaldır
if (Test-Path $installPath) {
    Write-Host "▸ Eski kurulum siliniyor..." -ForegroundColor Yellow
    Remove-Item $installPath -Recurse -Force
}

# Yeni klasör oluştur ve dosyaları kopyala
Write-Host "▸ Dosyalar kopyalanıyor..." -ForegroundColor Cyan
New-Item -ItemType Directory $installPath | Out-Null
Copy-Item "$distPath/*" -Destination $installPath -Recurse -Force

Write-Host "  ✓ Dosyalar $installPath altına kopyalandı" -ForegroundColor Green
Write-Host ""

# Kurulum RunlySettings.exe üzerinden yapılır
Write-Host "▸ RunlySettings.exe başlatılıyor (GUI kurulum)..." -ForegroundColor Cyan

$settingsExe = "$installPath\RunlySettings.exe"
if (-not (Test-Path $settingsExe)) {
    Write-Host "HATA: $settingsExe bulunamadı." -ForegroundColor Red
    exit 1
}

try {
    & $settingsExe
} catch {
    Write-Host "HATA: RunlySettings.exe başlatılamadı: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✓ Runly kuruldu ve kullanıma hazır." -ForegroundColor Green
Write-Host ""
Write-Host "Sonraki adımlar:" -ForegroundColor Cyan
Write-Host "  • Bir .js, .ps1 veya .py dosyasına sağ tık yapın"
Write-Host "  • 'Runly ile çalıştır' seçeneğini tıklayın"
Write-Host "  • Ayarlar için: $installPath\RunlySettings.exe" -ForegroundColor Gray
