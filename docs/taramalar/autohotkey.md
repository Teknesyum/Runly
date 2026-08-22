# AutoHotkey/AutoHotkey (+ AutoHotkeyUX)

## 1. Künye
`AutoHotkey/AutoHotkey` (yorumlayıcı) · **Lisans: GPL-2.0** (`license.txt` = GNU GPL v2) ·
12.995 yıldız · 22 açık issue · son commit 2026-08-16 · son sürüm `v2.0.26` (2026-05-04).
`AutoHotkey/AutoHotkeyUX` (kurulum + **başlatıcı**, asıl ilgi çeken) · **Lisans: YOK** —
depoda `LICENSE`/`COPYING` yok, API `license` = `null`, README de yok; kod hukuken "tüm
hakları saklı" · 45 yıldız · 3 açık issue · son commit 2026-04-17 · etiketli sürüm yok.

## 2. Ne yapıyor
AutoHotkey, Windows'ta kısayol tuşu ve otomasyon script'i (`.ahk`) çalıştıran bir
yorumlayıcıdır. AutoHotkeyUX kurulumu, `.ahk` ilişkilendirmesini ve **hangi sürümün
(v1 mi v2 mi) bir script'i çalıştıracağını seçen başlatıcıyı** barındırır.

## 3. Runly ile kesişimi
Altı deponun **Runly'ye mimari olarak en yakın olanı**: AutoHotkeyUX tam olarak
`Runly.Launcher` işini yapıyor — çift tıklananı yakala, yorumlayıcıyı seç, çalıştır,
seçemezsen sor.
**Shim sınırı.** `launcher.ahk` başlığındaki kendi ifadesi: script `install.ahk`'nin
kaydettiği komutlar üzerinden dolaylı kullanılmak üzeredir, ama **`AutoHotkey.exe` yerine
geçecek şekilde derlenebilir**, böylece `AutoHotkey.exe` çağıran araçlar da otomatik sürüm
seçiminden yararlanır. Runly'nin shim'i de ilişkilendirmeden bağımsız çağrılabilmeli.

**İçerik deseni taraması.** `inc/identify.ahk` içindeki `IdentifyBySyntax`, metni regex'le
tarayıp v1'e ve v2'ye özgü işaretleri sayıyor. Kritik karar kendi yorumunda yazılı:
"basit ve **temkinli** bir yaklaşım kullan: yalnızca tek bir sürüme ait eşleşme varsa
sürüm seç." İkisi de eşleşirse `v: 0` dönüp **karar vermeyi reddediyor**. Runly'nin
`SecurityGate` desen taraması için aynı ilke geçerli. **Dry-run:** `/Which` anahtarı,
çalıştırmadan hangi yorumlayıcının seçileceğini söylüyor; Runly'de karşılığı yok.

**UserChoice çakışması.** `reset-assoc.ahk`, "birlikte aç" diyaloğuyla kurulan
`HKCU\...\FileExts\.ahk\UserChoice` kaydını tespit edip bunun bağlam menüsünü ve sürüm
algılamayı **bozduğunu** anlatıyor, `.reg` yazıp anahtarı silerek sıfırlamayı öneriyor.
Runly'nin `RegFileParser` + `RegistryBackup` + `ShellRegistrar` üçlüsüyle aynı arazi.

**Yanlış pozitif.** README'nin "False positives" bölümü, resmî kaynaktan inen dosyaların
antivirüsçe işaretlenmesini beklenen durum sayıp ayrı sayfaya yönlendiriyor. Runly'nin
imzasız, script çalıştıran launcher'ı da aynı sonuca hazırlıklı olmalı.
**MOTW / güven listesi / onay diyaloğu:** yok, `.ahk` sorgusuz çalışır. Kesişmiyor — ve
bu bir eksiklik, örnek değil.

## 4. Alınacak fikir
1. **Belirsizlikte karar verme** — desen taraması iki sonuca eşit kanıt bulduğunda "en
   olası"yı seçmesin, kararsız dönüp sorsun. Neden: yanlış yorumlayıcı seçimi Runly'de
   sessiz bir güvenlik hatası. Maliyet: düşük, tarayıcı sonucuna `Belirsiz` durumu.
2. **`/Which` benzeri dry-run** — "bu dosyaya çift tıklarsam ne olur": seçilecek uygulama,
   güven kaydı, MOTW durumu; çalıştırmadan. Neden: güven listesini denetlenebilir kılan
   en ucuz araç. Maliyet: orta, karar yolunun yan etkisiz çağrılabilmesi gerekir.
3. **UserChoice çakışmasını tespit edip onarmayı öner** — kullanıcı "birlikte aç" ile
   Runly'yi devre dışı bıraktığında fark et, tek tıkla düzeltmeyi sun. Neden: AutoHotkey
   bu senaryoya ayrı bir script ayırmış, yani gerçek ve sık. Maliyet: orta.

## 5. Kaçınılacak hata
- **AutoHotkeyUX lisanssız.** Tasarımı okumak serbest; hiçbir satır alınamaz, bağımlılık
  kurulamaz. Alınan şey yalnız desendir.
- **Sürüm bölünmesi.** README'nin kendi ifadesi: "AutoHotkey v1 bakımda değil, destek
  topluluk üyelerince sağlanıyor." Başlatıcının tüm karmaşıklığı bu bölünmenin bedeli;
  Runly'de tek karar noktası, tek katalog kalmalı.
- **Ayrı depo, ayrı hız.** Yorumlayıcı 2026-08-16'da, başlatıcı 2026-04-17'de güncellenmiş
  ve başlatıcının hiç etiketli sürümü yok. `Runly.Launcher` ile `Runly.Settings` aynı
  sürümde ilerlemeli.

## 6. Doğrulama
Okudum: `gh api` ile iki deponun künyesi + `releases/latest`; `license.txt`; `README.md`;
`AutoHotkeyUX` kök dosya listesi (lisans dosyası **yok**, API `license: null` ile teyitli);
`launcher.ahk`, `inc/identify.ahk`, `reset-assoc.ahk` baş kısımları. Okuyamadım:
`autohotkey.com/docs/v2/Program.htm` **HTTP 403** döndü — `#Requires` ile sürüm seçimi ve
"Run with UI Access" resmî belgeden **doğrulanamadı**; anlatım depo kaynağının kendi
yorum satırlarına dayanıyor. Kullanım sayısına dair rakam kullanılmadı.
