# velopack/velopack

## 1. Künye

- Depo: `velopack/velopack`
- Lisans: **MIT** (GitHub API `MIT`). Marka için depoda ayrı bir kayıt yok; `artwork/` klasöründeki logolar da MIT kapsamında görünüyor — ayrı marka koruması `doğrulanamadı`.
- Yıldız: 2.287 · Açık issue: 56 (bu listedeki en küçük issue yükü)
- Son commit: 2026-08-07 · Son etiketli sürüm: **1.2.0, 2026-06-03**

## 2. Ne yapıyor

Masaüstü uygulamaları için kurulum + otomatik güncelleme çerçevesi; Squirrel.Windows'un yerine geçmek için sıfırdan yazılmış. Uygulamanın kendi içine gömülen bir istemci kütüphanesi ile yanına konan `Update.exe` stub'ı birlikte çalışıyor.

## 3. Runly ile kesişimi

- **Kurulum yeri:** kullanıcı başına, `%LOCALAPPDATA%\<PackId>` altında; UAC istemeyen kurulum ve güncelleme Velopack'in ana satış noktası. Runly'nin `%LOCALAPPDATA%\Programs\Runly` tercihiyle aynı sınıf.
- **Yol değişince:** kısayolları kayıtlı listeden değil hedefe göre ters aramayla bulup güncelliyor (05'te var).
- **Kayıt yedeği:** yok. Velopack kendi yazdığı Uninstall anahtarını siliyor, üçüncü tarafın kaydına dokunmuyor.
- **Sürümleme:** `version.json` + SemVer; sürümler arasında **delta paket** üretiliyor, kullanıcı yalnız değişeni indiriyor.
- **Paket doğrulama:** bundle imzası doğrulanıyor (Windows'ta Authenticode; macOS tarafı için açık issue #975 var — imza doğrulaması güncelleme uygulanmadan önce yapılsın). Ayrıntısı 07'de.

## 4. Alınacak fikir

1. **Rust çekirdek + ince dil bağlamaları.** `src/lib-rust` gerçek işi yapıyor; `lib-csharp`, `lib-cpp`, `lib-nodejs`, `lib-python` yalnız aynı çekirdeğe geçiş katmanı ve `src/code-generator` bu bağlamaları üretiyor. Runly'nin karşılığı: registry/ilişkilendirme mantığı NativeAOT başlatıcıyla paylaşılan tek çekirdekte olmalı, WinForms ayarlar uygulaması o çekirdeğin bir yüzü. Maliyet: bugünkü `Runly.Core` zaten bu ayrımı taşıyorsa sıfır; taşımıyorsa sınırı çekmek orta iş.
2. **Kilit dosyası ve dönen log, birinci sınıf vatandaş.** `lib-rust/src` altında `lockfile.rs` ve `file_rotate.rs` ayrı modüller. Yani "aynı anda iki güncelleme çalışmasın" ve "log dosyası sınırsız büyümesin" sonradan yamanmış değil, tasarımın parçası. Runly registry'ye yazarken aynı sorun var: iki Runly örneği aynı anda ilişkilendirme yazarsa yedek tutarsız kalır. Maliyet: tek bir mutex/lock dosyası, düşük.
3. **`known_path.rs` / `locator.rs` ayrımı.** Bilinen Windows yolları (Başlat menüsü, Masaüstü, LocalAppData) tek bir modülde çözülüyor, kurulum kökünün nerede olduğu ayrı bir modülde. Yol hesabının tek yerde olması, §5'teki tür hataların yayılmasını engelliyor. Runly'de registry yolu üretimi de tek modülde toplanmalı — `HKCU\Software\Classes\...` dizeleri koda dağılmamalı.

## 5. Kaçınılacak hata

Velopack'in en sızdıran yeri **kendi doğal akışı değil, MSI sarmalayıcısı**. Üç açık issue aynı köke işaret ediyor:

- **#1004 (açık, 2026-07-17):** uygulama kendi güncelleyicisi yerine doğrudan yeni MSI çalıştırılarak güncellenirse, sonraki kaldırma tutarsız durum bırakıyor — disk temiz ama Windows hâlâ uygulamanın kurulu olduğunu düşünüyor: "Programlar ve Özellikler" girdisi, ikon ve Başlat menüsü kısayolları kalıyor.
- **#989 (açık, 2026-07-08):** `--msi` ile per-machine kurulumda uygulama `Program Files`'a gidiyor ama ayrıca `%LOCALAPPDATA%\<PackId>` klasörü oluşuyor ve kaldırmada **silinmiyor**.
- **#1040 (açık, 2026-08-18):** `--instLocation Either` ile per-machine seçilirse Windows Installer yükseliyor, fakat uygulamanın kurulum/kaldırma kancaları **yükseltilmemiş** kullanıcı bağlamında çalışıyor.

Ortak ders Runly için doğrudan: **iki farklı kurulum mekanizmasının aynı ürünü yönetmesine izin verme.** Velopack'te "kendi güncelleyicisi" ile "MSI" iki ayrı defter tutuyor ve biri diğerinin yazdığını bilmiyor. Runly'de `install.ps1` ile ayarlar uygulamasının kaldırma düğmesi aynı defteri okumalı; ayrıca per-user ile per-machine kapsamı aynı üründe karıştırılmamalı — #1040 tam olarak bu karışımın faturası.

İkinci ders: kısmi başarısızlıkta kullanıcıya yine "tamamlandı" demek (05'te var).

## 6. Doğrulama

- Kaynaktan okundu: `repos/velopack/velopack` metadata, `releases/latest`, `commits[0]`, kök dizin listesi, `src/`, `src/lib-rust/src/`, `src/vpk/` listeleri, README, açık issue listesi ve #1004 / #989 / #1040 gövdelerinin başı.
- Okunmadı / `doğrulanamadı`: Rust kaynak dosyalarının içeriği okunmadı; `lockfile.rs`, `file_rotate.rs`, `known_path.rs`, `locator.rs` hakkındaki çıkarımlar **dosya adlarına** dayanıyor.
- README'deki "Rust ile yazıldığı için şimşek hızında", "39 dile çevrildi" ve tüm "Testimonials" bölümü — hepsi Discord alıntısı veya proje kendi beyanı, bağımsız ölçüm yok: **`doğrulanamadı`**.
- Delta paket ve Squirrel'den otomatik göç iddiaları README beyanı; test edilmedi, `doğrulanamadı`.
