# Runly Test Örnekleri

Kabul senaryoları için örnek script dosyaları.

## Test Dosyaları

| Dosya | Dil | Senaryo | Beklenen Davranış |
|-------|-----|---------|-------------------|
| `hello.js` | Node.js | Temel çalıştırma | Türkçe mesaj + argümanları yazdırır, exit 0 |
| `fail.js` | Node.js | Hata yönetimi | Hata mesajı yazıp exit 1 |
| `slow.js` | Node.js | Bekleme davranışı | 3 saniye bekleyip tamamlanır |
| `args.js` | Node.js | Argüman işleme | Aldığı argümanları numaralandırarak listeler |
| `hello.ps1` | PowerShell | `.ps1` çalıştırma | Türkçe mesaj + parametreleri yazdırır |
| `hello.py` | Python | Python çalıştırma | Türkçe mesaj + argümanları yazdırır |
| `shebang-test` | Node.js (uzantısız) | Shebang tespiti | Shebang satırından Node.js bulup çalıştırır |
| `Türkçe klasör/boşluklu ad.js` | Node.js | Yol kaçışı | Türkçe adlı klasör ve boşluklu dosya adını işler |

## Kullanım

```bash
# Doğrudan çalıştırma (Runly olmadan)
node hello.js argüman1 argüman2
python hello.py argüman1
powershell hello.ps1 argüman1

# Runly ile çalıştırma (Windows Explorer'da sağ tık)
# İlgili dosyaya sağ tıkla → "Runly ile çalıştır" → argümanları gir → OK

# Komut satırından Runly ile
Runly.exe hello.js argüman1 argüman2
Runly.exe hello.ps1 argüman1
```

## Notlar

- `shebang-test` dosyasının uzantısı yoktur; Runly shebang satırını okumalıdır.
- `Türkçe klasör/boşluklu ad.js` yolunun doğru kaçışlanması test edilir.
- Tüm dosyalar UTF-8 kodlanmıştır.
- `.ps1` dosyaları çalıştırırken Windows ExecutionPolicy denetimi uygulanır (SecurityGate).
