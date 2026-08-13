$ErrorActionPreference = "Stop"

$programPath = "$env:LOCALAPPDATA\Programs\Runly"
$dataPath = "$env:APPDATA\Runly"

Write-Host "=== Runly Kaldırılması ===" -ForegroundColor Yellow
Write-Host ""

# Kontrol: kurulum var mı?
if (-not (Test-Path $programPath)) {
    Write-Host "Runly kurulu değil." -ForegroundColor Gray
    exit 0
}

Write-Host "▸ RunlySettings.exe kaldırma modunda çalıştırılıyor..." -ForegroundColor Cyan

$settingsExe = "$programPath\RunlySettings.exe"
if (Test-Path $settingsExe) {
    try {
        & $settingsExe --uninstall 2>&1
    } catch {
        Write-Host "Uyarı: RunlySettings.exe çalıştırılamadı (devam ediliyor): $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "Uyarı: RunlySettings.exe bulunamadı (devam ediliyor)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "▸ Program klasörü kaldırılıyor: $programPath" -ForegroundColor Cyan

try {
    Remove-Item $programPath -Recurse -Force
    Write-Host "  ✓ Program klasörü silindi" -ForegroundColor Green
} catch {
    Write-Host "HATA: Program klasörü silinemedi: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Veri klasörü sorusu
if (Test-Path $dataPath) {
    Write-Host "Runly verileri bulundu: $dataPath" -ForegroundColor Cyan
    Write-Host "  (Ayarlar, oturum geçmişi, güven deposu)" -ForegroundColor Gray
    Write-Host ""

    $response = Read-Host "Bu verileri de silmek istiyor musunuz? (E/H, varsayılan: H)"

    if ($response -eq "E" -or $response -eq "e") {
        Write-Host "▸ Veri klasörü kaldırılıyor..." -ForegroundColor Yellow
        try {
            Remove-Item $dataPath -Recurse -Force
            Write-Host "  ✓ Veri klasörü silindi" -ForegroundColor Green
        } catch {
            Write-Host "HATA: Veri klasörü silinemedi: $_" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  ℹ Veriler tutuldu (gerekirse manuel silme: $dataPath)" -ForegroundColor Gray
    }
} else {
    Write-Host "Runly veri klasörü bulunamadı (daha önce temizlenmiş olabilir)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✓ Runly kaldırıldı." -ForegroundColor Green
