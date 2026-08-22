# Nilesoft Shell

## 1. Künye

- **Depo:** `moudey/Shell`
- **Lisans:** MIT (GitHub API `spdx_id: MIT`, kökte LICENSE). Marka ayrı: ürün adı ve dağıtım
  `nilesoft.org` üzerinden, depo yalnız kaynağı taşıyor.
- **Yıldız:** 6.784 · **Açık issue:** 245
- **Son commit:** 2026-02-09 (`main`)
- **Son etiketli sürüm:** `v1.9.15` — 2024-02-14. İki yıldır etiket yok, geliştirme sürüyor.

## 2. Ne yapıyor

Windows Gezgini bağlam menüsünü baştan çizen bir kabuk uzantısı; kendi öğelerini ekler,
sistemin ve üçüncü tarafların öğelerini gizler ya da değiştirir. Yapılandırma tek düz metin
dosyası: `src/bin/shell.nss` (kendi ifade dili, değişkenler ve fonksiyonlar).

## 3. Runly ile kesişimi

**Bağlam menüsü kaydı:** tek DLL kaydediliyor, öğeler registry'ye tek tek yazılmıyor — Runly'nin
uzantı başına ProgID + verb yazan modelinin karşıtı. **Kurulum yolu:** `C:\Program Files\Nilesoft
Shell\`, yönetici gerekiyor, HKLM'ye yazıyor. **Kaldırma:** `shell -unregister -restart` tek
komut; belge Gezgin'i yeniden başlatma gereğini saklamıyor (`docs/installation.html`).

Runly'nin var oluş gerekçesine değen issue: **#290 "Always have Open With option for any file"**
— kullanıcı `.bat` dosyasını `cmd`'ye bağlamak istiyor, Windows'un "Birlikte aç" listesi Store
dışı uygulamaları göstermiyor ve tüm ilişkilendirmeleri sıfırlamadan çözemiyor. Runly'nin
`Dialogs/ChooseApplicationDialog.cs` + `Discovery/ApplicationFinder.cs` ikilisi bu boşluğu dolduruyor.

## 4. Alınacak fikir

1. **Tek düz metin yapılandırma + belgelenmiş dil.** `shell.nss` ve `docs/configuration/`
   (properties, modify-items, new-items ayrı sayfalar). Runly'nin `RunlyConfig` JSON'u zaten
   sparse; eksik olan aynı ayrıntıdaki alan belgesi. Maliyet: tek belge dosyası.
2. **`-register` / `-unregister` simetrisi.** Kaldırma komutu kurulumun birebir tersi ve
   "Gezgin'i yeniden başlatmayı kabul edin" adımı belgede açık. Runly `IShellNotifier` ile daha
   ucuz çözüyor ama mesajı aynı netlikte vermeli.
3. **`Issue700-Fix.md` deseni.** Windows 11 Canary'de tema API'si değişince `FlightRing` registry
   değerini okuyup Insider kanalını tespit ediyorlar. Ders: sürüm numarasına değil, kanalı
   söyleyen değere bak.

## 5. Kaçınılacak hata

- **Kaynak ile ikili arasındaki boşluk.** #139 "Why closed-source?" (açık, 2023-01-27) — depo MIT
  olsa da kullanıcının indirdiği ikili nilesoft.org'dan geliyor; o dosyanın bu kaynaktan
  üretildiği doğrulanamıyor. #441 "Virus Total Report!" aynı güven açığının sonucu. Runly imzasız
  dağıtacaksa en azından kaynak-ikili eşlemesini (hash / reproducible build) yayınlamalı.
- **Gizli kurulum maliyeti.** #680 "Error: Microsoft Visual C++ Runtime Library" — VC++ yeniden
  dağıtılabilir bağımlılığı; Runly'nin NativeAOT tercihinin karşılığı.
- **Yönetici + HKLM + süreç içi uzantı.** Gezgin sürecine giriliyor: #198 Stardock Fences ile,
  #669 QTTabBar ile çakışıyor. Runly'nin HKCU-only ve süreç-dışı duruşu bu çakışma sınıfını
  tümden dışarıda bırakıyor — sapmamak gerek.

## 6. Doğrulama

- Kaynaktan okundu: künye (API), README, `docs/installation.html`, `docs/configuration/` listesi,
  `src/` ağacı (`dll`, `exe`, `setup/wix`, `bin/shell.nss`), `Issue700-Fix.md`, issue #139 ve #290
  gövdeleri, açık issue listesi.
- **Doğrulanamadı:** nilesoft.org indirme sayıları ve "minimal resource usage" iddiası; VirusTotal
  bulgusunun bugünkü durumu; DLL kaydının kodda nerede yapıldığı (`src/exe` içine inilmedi).
