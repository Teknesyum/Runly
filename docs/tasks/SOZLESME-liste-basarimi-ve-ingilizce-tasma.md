# Sözleşme — Büyük liste başarımı (U7) ve İngilizce arayüzde taşma (U6)

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma.
**Kaynak:** `docs/UI-PLAN.md` U6 ve U7.

## U7 · Büyük liste

Arama kutusuna 180 ms debounce eklendi (`MainForm.cs`, `_searchDebounce`) ama filtreleme
hâlâ **tam yeniden projeksiyon**: her tuşta 408 uzantı yeniden eşleniyor, ızgara baştan
doldu ruluyor.

- Bir kez kurulan indeks: uzantı + görünen ad + kategori adı küçük harfe indirilmiş hâlde
  önceden hazırlanır, her tuşta yeniden üretilmez. Katalog değişince (uzantı eklendi/silindi)
  indeks tazelenir.
- Filtreleme `CancellationToken` ile iptal edilebilir olsun; kullanıcı yazmaya devam
  ederken önceki tarama düşsün.
- Izgara doldurma `SuspendLayout`/`ResumeLayout` ya da eşdeğeriyle tek seferde yapılsın.
- **Davranış değişmeyecek:** arama sonuçları, sıralama, sonuç sayacı ve boş sonuç metni
  bugünküyle birebir aynı kalır.

**Kabul:** 400+ satırda yazarken ölçülen yenileme süresi kaydedilir. Bugünkü taban
**15–47 ms**; ölçümü aynı yöntemle tekrarla, öncesi/sonrası tabloyu raporda ver.
Ölçüm `Stopwatch` ile kodun içinden alınabilir, ama ölçüm kodu üründe kalmasın.

## U6 · İngilizce arayüzde taşma

Bugüne kadar yalnız Türkçe arayüzde gözle bakıldı. "Yerleşimi en uzun dil belirler" kuralı
doğrulanmadı — Türkçe genelde daha uzun ama bu varsayım ölçülmedi.

- Dil İngilizceye alınır, **her panel ve her diyalog** gezilir: ana pencere, kategori rayı,
  ayrıntılar paneli, uygulama seçici, uzantı ekleme, kaldırma onayı, yedekten geri yükleme,
  sonuç diyaloğu, mesaj kutusu.
- Kırpılan etiket, üç noktaya düşen buton, taşan sütun başlığı düzeltilir. Düzeltme
  **ölçüden** gelir: `TextRenderer.MeasureText` ile genişlik hesapla, sabit piksel ekleme.
- Türkçe arayüzde bozulmadığı aynı turla doğrulanır.

**Kabul:** iki dilin ekran görüntüleri `docs/reports/` altına konur, kırpılan tek etiket
kalmaz. Kalan varsa neden kalamadığı yazılır.

## Kabul kriterleri (ortak)

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı**.
2. `dotnet test Runly.sln -c Debug --no-build` → hepsi geçer.
3. `dotnet format --verify-no-changes` → temiz.
4. Uygulama gerçekten açılır, iki dilde gezilir, süreç kapatılır ve `%APPDATA%\Runly`
   yapılandırması eski hâline getirilir.
5. `docs/UI-PLAN.md`'de U6 ve U7 altına tek satır "yapıldı" notu.
6. `docs/KNOWN-ISSUES.md`'deki "İngilizce arayüzde taşma kontrolü canlı yapılmadı"
   maddesi kapatılır.

## Kurallar

- **Kod yorumu yazma** — mevcut yorumlar bir kısıtı anlatıyor, sen de ancak öyle bir kısıt
  varsa yaz.
- Renk, ölçü, yazı tipi uydurma; `Palette` ve `Metrics` dışına çıkma.
- Çeviri metnini kısaltarak taşmayı çözme — önce yerleşimi ölçüye bağla. Metni ancak
  gerçekten yanlış/uzun bir çeviriyse düzelt.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, U7 öncesi/sonrası ölçüm tablosu,
U6'da düzeltilen taşmaların listesi, kabul çıktıları.
