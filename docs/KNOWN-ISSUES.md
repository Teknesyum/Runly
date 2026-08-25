# Runly — Bilinen Sınırlar ve Açık Sorunlar

Version 0.2.0 · Last updated: 2026-08-15

## 1. Windows kısıtları (çözümü yok)

- **Hiçbir uzantı tek tıkla bağlanamaz (K19 + K23).** Kurulum ProgID'leri ve `.ext` varsayılanını
  yazar, ama Windows 11 çift tıkta `FileExts\<ext>\UserChoice`'a bakar. Bağlamanın tek meşru yolu:
  **sağ tık → Birlikte aç → Başka bir uygulama seç → Runly → "Her zaman"**. Hash programatik
  olarak üretilemez; üretmeye çalışmak yasaktır (SPEC §2).
- **`SHOpenWithDialog` varsayılanı bağlayamıyor (K23).** R1 ekranda ölçtü: pencerede yalnız
  "Yalnızca bir kez" düğmesi çıkıyor; `OAIF_FORCE_REGISTRATION` eklenince Windows doğrudan
  reddediyor. GUI bu yüzden kullanıcıyı Ayarlar → Varsayılan uygulamalar'a ya da Explorer'ın
  "Başka bir uygulama seç" akışına yönlendiriyor.
- **`UserChoice` anahtarı bazı makinelerde silinemez.** Windows bu anahtara DELETE'i reddeden bir
  ACE koyabilir. R1'in turunda **engellenmedi** (kendi yazdığımız kayıt sorunsuz silindi), T7'de
  engellenmişti. Kod iki durumu da ele alıyor; silinemezse kaldırma diyaloğu uzantıyı öksüz
  olarak listeliyor ve `ms-settings:defaultapps` kısayolu sunuyor (K20).
- **SmartScreen:** `Runly.exe` imzasız olduğu için ilk çalıştırmada "Bilinmeyen yayımcı" uyarısı
  çıkabilir. Kod imzalama sertifikası olmadan çözümü yok.
- **Antivirüs:** Script çalıştıran imzasız bir launcher heuristik olarak işaretlenebilir.
- **pwsh 7 kurulu değil;** `.ps1` Windows PowerShell 5.1 ile çalışır.

## 2. Açık sorunlar

| # | Sorun | Etki | Sahibi |
|---|---|---|---|
| — | Açık kayıt yok. | | |

### Kapatılanlar

- **B1** (kurulum "bağlandı" diye yanlış iddiada bulunuyordu) — R1'de kapatıldı; `Install` artık
  yalnız `UserChoice` bizi gösterirken `Bound` diyor, `AssocQueryString` ile ikinci görüş alıyor.
- **B2** (kaldırmada öksüz `UserChoice`) — R1'de kapatıldı; kaldırma önce tespit ediyor, silmeyi
  deniyor, sonucu registry'ye tekrar bakarak ölçüyor, silemezse açıkça listeliyor.
- **B3** (junction ile güven atlatma) — R2'de kapatıldı; `TrustMatching` artık
  `GetFinalPathNameByHandleW` ile reparse point çözüyor, 4 junction testi yeşil.
- **B4** (`install.ps1 -Silent` ölü kod) — R2'de kaldırıldı.
- **B5** (`samples/hello.ps1` BOM'suz) — R2'de BOM eklendi.
- **B6** (dışarıdan değişen config'in sessizce geri yazılması) — 0.1.1'de kapatıldı; pencere
  config dosyasının zaman damgasını tutuyor, kaydederken damga ilerlemişse üzerine yazmadan
  önce onay soruyor.
- **B7** (bağlama değişikliğinin canlı yakalanmaması) — R5'te kapatıldı; `RefreshStatusOnly`
  pencere öne geldiğinde yalnız "Bulundu"/"Durum" sütunlarını tazeliyor, düzenlenmekte olan
  hücreye dokunmuyor.
- **B8** (README'deki `install.ps1 -Silent` örneği) — README İngilizceye taşınırken kaldırıldı.
- **B10** (kenardan boyutlandırma, Aero Snap ve çift tıkla ekranı kaplama çalışmıyordu) —
  0.1.3'te kapatıldı. Üç sebep vardı: `FormBorderStyle.None` pencereden `WS_THICKFRAME` ve
  `WS_MAXIMIZEBOX` stillerini alıyor, Windows bunlar olmadan doğru bildirilen HTLEFT/HTCAPTION
  kodlarına tepki vermiyor; stiller eklenince bu kez 7px'lik görünür bir çerçeve doğuyor, bu da
  `WM_NCCALCSIZE`'a sıfır dönerek yok ediliyor; son olarak sol/sağ/alt kenarları `Dock=Fill`
  çocuk denetimler kaplıyordu, pencerenin hit-test'i oralara hiç ulaşmıyordu — `DisplayRectangle`
  artık üç kenarda 7px'lik bir tutamak payı bırakıyor. Dördü de otomasyonla ölçüldü.
- **B9** (bağlı satırın hücre zemini beyaza dönüyordu) — 0.1.1'de kapatıldı; `DataGridView`
  hücre dolgusu alfa kanalını yok sayıyor, yarı saydam tint'ler yüzey rengiyle önceden
  karıştırılıp opak veriliyor.

### Kapatılanlar (devam)

- **B12** ("Varsayılan yap" düğmesine basınca açılan Windows sayfasında uzantı yok) — kapatıldı.
  İki ayrı sebep vardı. Birincisi: `Kaydet` registry'ye hiçbir şey yazmıyor, yalnız `Kur / Güncelle`
  yazıyor; uygulama seçip kaydeden kullanıcının uzantısı `Capabilities\FileAssociations` altına hiç
  girmiyordu. İkincisi ve asıl olanı: `registeredAppUser=Runly` sayfası, uygulamanın **bildirdiği**
  dosya türlerini değil **hâlihazırda varsayılanı olduğu** türleri listeliyor. 22.08.2026'da bu
  makinede ölçüldü — sayfa `.pl` ve `.sh` gösteriyordu (yalnızca `UserChoice` olarak varlar,
  capabilities'te yoklar), `.md`'yi göstermiyordu (capabilities, `SupportedTypes`, `OpenWithProgids`
  ve geçerli ProgID komutu hepsi yerindeydi). Yani o sayfa bağlanmamış bir uzantıyı hiçbir zaman
  gösteremez. Tek uzantılık bağlama artık `ms-settings:defaultapps?ftfilter=<uzantı>` adresine
  gidiyor ve düğme, uzantı tanıtılmamışsa önce kurulum öneriyor.

- **B11** (uzantı listesinde arama kutusu bulunamıyor, satıra çift tıklayınca uygulama seçilemiyordu)
  — kapatıldı. Üç ayrı sebep vardı: arama kutusu `WrapContents = false` bir düğme şeridinin
  sonundaydı ve pencere kenarının dışına itiliyordu; ızgaranın `EditMode` değeri
  `EditOnKeystrokeOrF2` olduğu için çift tık hiçbir şey yapmıyordu; işleyici hücresi de yalnız elle
  yazılan mutlak `.exe` yolunu kabul ediyordu. Arama kendi satırına alındı, satıra çift tıklamak
  `ChooseApplicationDialog`'u açıyor, boş işleyici hücresi "Çift tıklayın" ipucunu gösteriyor.

## 3. Doğrulanamayanlar — kullanıcı tarafından doğrulanmalı

- **The 0.2.0 catalog UI was visually checked at 100% DPI only.** The category rail, localized names,
  icons, selected cyan strip, enabled/total badges, seven-column table, and TR/EN live switch were
  exercised on the real Windows desktop. 125% and 150% DPI remain unverified.
- **The complete custom-extension flow was not completed through desktop automation.** The Add Extension
  dialog opened, but the automation driver could not enter text into its owned modal input. The exact
  disabled `.foo` → Special projection is covered by `CatalogGridProjectionTests` and does not throw;
  manual entry through that modal remains to be checked.
- **The `.md` no-handler explanation was visually verified, but its enable-checkbox transition was not.**
  The Text category displayed `.md` and the localized "No application is selected" reason. Desktop
  automation could not conclusively toggle that checkbox, so the enabled state needs one manual click.
- **Start-menu shortcut discovery was exercised on this machine, not every installed application.** An
  access-denied folder found during the live run is now skipped with `IgnoreInaccessible`; broken or
  vendor-specific shortcuts may still be absent until the user browses to the executable.
- **Permanent default-app selection still requires user interaction in Windows Settings.** Automated
  verification deliberately does not click or forge `UserChoice`; binding counts are only truthful after
  Windows reports Runly as the protected choice and `AssocQueryString` agrees.

- **Yeni arama satırı ve uygulama seçme diyaloğu yalnız Türkçe arayüzde ve %100 DPI'da gözle
  doğrulandı.** Metinler `locale/*.json` üzerinden geliyor ve anahtar eşliği testle korunuyor, ama
  İngilizce arayüzde taşma kontrolü canlı yapılmadı.

- **`ftfilter` yalnız Ayarlar kapalıyken açılışta doluyor.** 22.08.2026'da iki kez ölçüldü.
  `SystemSettings` süreci öldürülüp `ms-settings:defaultapps?ftfilter=.md` açıldığında UI Automation
  ağacında kutunun değeri `.md` oluyor. Ayarlar zaten açıkken aynı bağlantı doğru sayfaya gidiyor ama
  kutuyu **boş** bırakıyor. Runly başka bir uygulamanın penceresini kapatmaz; ayrıntılar panelindeki
  metin bu yüzden "kutuya uzantıyı yazın" diyor. Bağlantı yine de doğru bölüme gidiyor — eski
  `registeredAppUser` sayfası bağlanmamış uzantıyı hiç gösteremiyordu.

- **125% / 150% DPI ölçeklemede Ayarlar penceresi ve TaskDialog görünümü.** Sistem ölçeklemesini
  değiştirmek oturum açma/kapama gerektirdiği için ne T5, ne T7, ne R3 bunu deneyebildi.
  **Kullanıcı doğrulamalı:** Ayarlar → Sistem → Ekran → Ölçek %125 ve %150 yapıp
  `RunlySettings.exe`'yi ve bir güvenlik diyaloğunu açın; kırpılan metin/buton var mı bakın.
- **`--verb runas` (UAC yükseltme).** UAC istemi otomasyonla onaylanamadığı için (Windows UIPI)
  uçtan uca denenemedi; sağ tık menüsünde fiilin **var olduğu** T7'de doğrulandı.
- **Farklı bir kullanıcı hesabında kurulum.** Tek hesapta test edildi.
- ~~Başlık çubuğu fiziksel etkileşimleri~~ — 0.1.3'te ölçüldü ve kapatıldı, bkz. B10.
- **R3'ün 6 maddesi (S1, S7, S12, B3 regresyonu, B5, dürüstlük denetimi)** — bu oturumda gerçek
  makinede çalıştırılamadı, bkz. `docs/reports/R3-COMPLETE.md`.

- **Ayarlar penceresi bazen klasik Windows başlık çubuğuyla görünüyor (25.08.2026,
  kullanıcı ekran görüntüsü).** Neon başlık bandı yerinde duruyor, sistem çubuğu onun
  **üstüne** biniyor. Ölçülenler:

  - Güncel `dist\RunlySettings.exe` (v0.2.0) canlı ölçüldü: `WS_CAPTION` yok,
    `GetWindowRect` ile `GetClientRect` farkı 0px. Maximize / restore / minimize
    turlarında ve Explorer üzerinden kısayolla başlatmada da bozulmuyor.
    **Yeniden üretilemedi.**
  - `dist-e2e\RunlySettings.exe` (v0.1.0, 13.08 14:14 — bu klasör 25.08'de silindi)
    klasik çubuk veriyor —
    `WS_CAPTION` var, 39px başlık. İçinde `NeonForm` sınıfı hiç yok, çünkü o sınıf
    ağaca 13.08 **20:48**'de girdi. Ama içerik düzeni 0.1.0; kullanıcının gördüğü
    ekranda kategori kenar çubuğu ve uygulama seçici var, bunlar 22.08 tarihli.
    **Suçlu bu ikili değil.**
  - `MainForm` 13.08 20:48'den beri `NeonForm`'dan türüyor, yani 0.2.0 içeriği
    taşıyan her yapı çerçevesiz. Ekran görüntüsünde içerik aşağı kaymamış —
    konumlar güncel yapıyla birebir — dolayısıyla çubuk istemci alanını
    daraltmıyor, üzerine çiziliyor.

  En tutarlı açıklama pencereye **dışarıdan** `WS_CAPTION` eklenmesi. Makinede
  `StartAllBack 3.9.23` kurulu (pencere çerçevesi yeniden çizen araç) ve sürece
  `RTSSHooks64.dll` enjekte oluyor. Kaynağı kesinleşmediği için savunma kodun kendi
  tarafına kondu: `NeonForm.OnHandleCreated` ve `WM_STYLECHANGED` stili denetleyip
  kirli bayrağı düşürüyor, `ApplyDarkTitleBar` da yedek olarak bağlandı.

  **Tekrarlarsa:** pencere açıkken `GetWindowLong(hwnd, -16)` okunup style değeri
  kaydedilmeli, ayrıca sürecin yüklü modül listesi alınmalı — hangi DLL'in enjekte
  olduğu orada görünür.

- **DPI zinciri kurulu değil (25.08.2026'da ölçüldü, bilinçli olarak açılmadı).**
  `Runly.Settings.csproj` `ApplicationHighDpiMode=PerMonitorV2` diyordu ama bu ayar yalnız
  üretilen `ApplicationConfiguration.Initialize()` üzerinden uygulanır ve o metot projede
  hiç çağrılmıyor — süreç SystemAware koşuyor. Yalan yapılandırma bırakmamak için satır
  silindi; PerMonitorV2 açılmadı. Açılabilmesi için önce `Metrics` DPI'ya göre yeniden
  hesaplanabilir olmalı (`Metrics.Initialize` şu an tek seferlik, `s_initialized`
  bayrağıyla) ve formlara `OnDpiChanged` override'ı girmeli. Test makinesi 96 dpi tek
  monitör olduğu için değişiklik canlı doğrulanamıyordu; doğrulanamayan riskli değişiklik
  yerine ölü ayar kaldırıldı.
