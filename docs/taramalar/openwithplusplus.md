# Open with++

## 1. Künye

- **Depo:** `stax76/OpenWithPlusPlus`
- **Lisans:** MIT (GitHub API `spdx_id: MIT`; kökte `License.txt`)
- **Yıldız:** 437 · **Açık issue:** 10
- **Son commit:** 2025-11-26 (`master`) · **Son etiketli sürüm:** `v4.0` — 2022-08-13
- **Durum:** issue **#33 "This project is now unmaintained"** (açık, 2025-04-30) — yazar sağlık
  nedeniyle bakımı bıraktı. Depo arşivlenmemiş.

## 2. Ne yapıyor

Klasik Win32 bağlam menüsüne komut satırı temelli özel öğeler ekleyen bir kabuk uzantısı.
VB.NET bir GUI (`OpenWithPPGUI`) ayarları XML'de tutuyor, C++ bir in-proc DLL
(`OpenWithPPShellExtension`) menüyü Gezgin sürecinin içinde çiziyor.

## 3. Runly ile kesişimi

Runly'ye en yakın akraba: aynı problem (bir dosyayı istediğin programa, istediğin argümanlarla
açtırmak), farklı giriş noktası (bağlam menüsü vs. çift tık). Karşılıklar: File Types alanı ↔
`Models/ExtensionMapping`, `%paths%` genişletme ↔ `Cli/ArgumentSplitter`, "Run as admin" ↔
`--verb runas`, Working Directory ve Icon ↔ `ShellRegistrar` verb yazımı.

Kesişimi tamamlayan issue: **#15 "Request: OpenWith++ as a direct file opener"** (açık,
2020-10-27) — kullanıcı, "Birlikte aç" penceresinde OW++'ı seçebilmeyi, seçince de o uzantı için
tanımlı uygulamaların listesinin çıkmasını istiyor. Runly'nin bugün yaptığı tam olarak bu;
OW++ mimarisi gereği (menü uzantısı, handler değil) yapamadı.

## 4. Alınacak fikir

1. **Adlandırılmış uzantı grupları.** `%video%`, `%audio%`, `%subtitle%`, `%image%` makroları
   Options diyaloğunda tanımlanıp File Types alanında kullanılıyor. Runly katalogunda kategori
   zaten var; kullanıcının kendi grubunu tanımlaması ucuz bir ek — config'e tek sözlük alanı.
2. **`%OpenWithPPDir%` ortam değişkeni ve göreli yol desteği.** Path/Arguments/WorkingDir/Icon
   alanlarında kurulum klasörü değişkenle anılıyor. Runly'nin registry'ye mutlak yol yazan
   sorununa doğrudan ilaç. Maliyet: %-genişletmenin `REG_EXPAND_SZ` ile mi yoksa uygulama
   içinde mi yapılacağına karar vermek.
3. **Shift = yükseltilmiş çalıştırma.** Ayrı "yönetici olarak" öğesi eklemek yerine mevcut öğeyi
   değiştirici tuşla yükseltmek; Runly'nin dört verb'lü menüsünü kısaltabilir.

## 5. Kaçınılacak hata

- **Mutlak yola çakılı kurulum.** README: "Start the application and click on the Install button.
  **Don't move the folder after installation.**" DLL `regsvr32` ile mutlak yolundan kaydediliyor;
  klasör taşınınca menü sessizce ölüyor. #5 "DLL not found" (20 yorum) ve #2 (kayıt başarısız)
  aynı sınıf. **Runly bugün aynı tuzağı taşıyor:** `ShellRegistrar.WriteVerb` ve
  `WriteApplicationRegistration` her ProgID komutuna mutlak exe yolu yazıyor.
- **In-proc DLL + .NET Framework bağımlılığı.** Menü kodu Gezgin sürecinde çalışıyor; çökerse
  Gezgin'i götürüyor, ayrıca .NET 4.8 + VC++ 2019 redist şartı var. Runly'nin süreç-dışı
  launcher modeli bu riski hiç almıyor — geri dönme.
- **Tek kişilik bakım.** #33 ile proje durdu; bağımlılık kurulacak kod değil, okunacak tasarım.

## 6. Doğrulama

- Kaynaktan okundu: künye (API), README'nin tamamı, `OpenWithPPShellExtension/` dosya listesi,
  issue #33 / #5 / #15 gövdeleri ve #5 yorumları, issue listesi.
- **Doğrulanamadı:** ayar XML şeması ve GUI'nin Install düğmesinin registry'ye tam olarak ne
  yazdığı (VB.NET kaynağına inilmedi); yıldız dışında kullanım rakamı yok.
