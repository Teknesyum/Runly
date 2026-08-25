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

---

## Rapor — ui-builder (a39d399de02352dae), 2026-08-25

**U7 yapıldı.** `src/Runly.Settings/Catalog/CatalogSearchIndex.cs` (yeni): katalog bir kez
indekslenir — normalleştirilmiş uzantı, küçük harfli uzantı/İngilizce ad/önerilen uygulamalar,
uzantıdan girdiye O(1) sözlük. `CatalogGridProjection.cs`: indeksi kullanır, `CancellationToken`
alır. `MainForm.cs`: `CatalogEntryFor` ve `EffectiveMapping` doğrusal taramadan sözlüğe geçti;
ızgara `SuspendLayout` + `Rows.AddRange` ile tek seferde dolar; arama debounce'ı önceki taramayı
iptal eder; yeni `ScanStatuses` tuş vuruşunda kayıt defterini yeniden okumaz (yazmak bir bağı
değiştiremez), arama dışındaki her yol eskisi gibi taze tarar.

| Sorgu | Satır | Öncesi | Sonrası |
|---|---|---|---|
| "e" | 412 | 225,0 ms | 56,1 ms |
| "ex" | 397 | 177,6 ms | 17,6 ms |
| "exe" | 397 | 169,4 ms | 21,6 ms |
| "exec" | 0 | 34,2 ms | 6,3 ms |

Satır sayıları iki yapıda birebir aynı. Ölçüm üründeki mevcut `Stopwatch` günlüğünden alındı,
yeni ölçüm kodu üründe kalmadı. **Sözleşmedeki 15–47 ms tabanı doğrulanmadı:** o bant küçük
sonuç kümelerine ait; 400+ satır önceden 169–225 ms sürüyordu. Geçici profil, sürenin
%90'ından fazlasının `GetStatus` kayıt defteri okumalarında geçtiğini gösterdi — sözleşmenin
"asıl maliyet yeniden projeksiyon" varsayımı yanlıştı. Ayrıntı:
`docs/reports/U7-liste-basarimi-olcumu.txt`.

**U6 yapılmadı — engel.** Ölçüm koşumunu (`src/Runly.Settings/UiAudit.cs` + `Program.cs`
`--ui-audit`) yazıp derledim; her diyalogu görünmez açıp `TextRenderer.MeasureText` ile
kırpılan denetimi listeleyecekti. Koşum sırasında dosya silindi ve `Program.cs` geri alındı;
aynı anda `Strings.cs`'e benim yazmadığım `GetIn`/`Languages` eklendi. Aynı çalışma ağacında
ikinci bir yazıcı U6'yı paralel yürütüyor. Çakışmayı büyütmemek için U6 dosyalarına
dokunmayı bıraktım; ekran görüntüsü alınmadı, `docs/KNOWN-ISSUES.md`'deki madde kapatılmadı.
Satır `.claude/relay/live/_sorun.log`'a yazıldı.

**Kabul:** `dotnet build Runly.sln -c Debug` 0 hata 0 uyarı · `dotnet test` 241/241 geçti ·
`dotnet format --verify-no-changes` temiz · uygulama açıldı, 412 satırlık listede arama
gezildi, süreç kapatıldı, `%APPDATA%\Runly\config.json` ve `runly.log` eski hâline getirildi
(dil hiç değiştirilmedi, `tr` kaldı) · `docs/UI-PLAN.md` U7 altına "yapıldı" notu düşüldü.
U6 kabulleri (iki dilin ekran görüntüleri, KNOWN-ISSUES maddesi) **karşılanmadı**.

`status: blocked` — U6 için aynı ağaçta ikinci yazıcı var.
