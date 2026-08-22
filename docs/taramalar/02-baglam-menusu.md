# 02 — Bağlam menüsü ve "Birlikte aç" yöneticileri

Tarama tarihi: 2026-08-22. Tüm depo rakamları (yıldız, açık issue, lisans, son push, son
etiketli sürüm) o gün `gh api repos/<owner>/<repo>` ve `.../releases/latest` ile alındı.
Blogdan gelen her sayı ayrıca işaretlendi. Kod okunmadı, yalnız klasör yapısı, giriş
dosyaları ve kayıt defteri yolları incelendi.

Aday listesindeki iki depo bulunamadı: `nilesoft/shell` gerçekte **`moudey/Shell`**
(nilesoft.org projesinin kaynağı), `MortenChristiansen/OpenWithPlusPlus` gerçekte
**`stax76/OpenWithPlusPlus`**. `sylveon/windows-context-menu-tools` hiç bulunamadı;
yerine `microsoft/vscode-explorer-command` ve `ikas-mc/ContextMenuForWindows11` kondu.
Toplam 10 depo tarandı.

---

## A. Dört sorunun cevabı

### A1. `SHOpenWithDialog` gerçekte ne veriyor

Runly'deki yorum **sonuç olarak doğru, gerekçe olarak eksik.** Bu bir Windows 11
davranışı değil, Microsoft'un **Windows 10'dan beri belgelediği** bir kısıtlama.
`OPENASINFO` sayfasındaki Remarks bölümü aynen şunu diyor:

> "Starting in Windows 10, the **OAIF_ALLOW_REGISTRATION**, **OAIF_FORCE_REGISTRATION**,
> and **OAIF_HIDE_REGISTRATION** flags will be ignored by SHOpenWithDialog. The Open With
> dialog box can no longer be used to change the default program used to open a file
> extension. **You can only use SHOpenWithDialog to open a single file.**"

Bayrakların belgelenmiş anlamı ve bugünkü gerçek karşılığı:

| Bayrak | Değer | Belgelenen anlam | Windows 10+ gerçeği |
|---|---|---|---|
| `OAIF_ALLOW_REGISTRATION` | 0x1 | "always use this program" kutusunu **etkinleştir** | **Yok sayılıyor** (belgeli) |
| `OAIF_REGISTER_EXT` | 0x2 | Kullanıcı OK'e basınca kaydı yap | Kayıt yolu tamamen kapalı olduğu için pratikte ölü bayrak |
| `OAIF_EXEC` | 0x4 | Kayıttan sonra dosyayı çalıştır | **Tek çalışan iş bu.** Verilmezse Windows "Ayarlar'dan değiştirin" bilgi penceresi gösteriyor (belgeli) |
| `OAIF_FORCE_REGISTRATION` | 0x8 | Kutuyu zorla işaretli getir | **Yok sayılıyor** (belgeli) |
| `OAIF_HIDE_REGISTRATION` | 0x20 | Kutuyu gizle | **Yok sayılıyor** (belgeli) |
| `OAIF_URL_PROTOCOL` | 0x40 | Uzantı değil protokol listesi göster | Geçerli, protokol işleyicileri için |
| `OAIF_FILE_IS_URI` | 0x80 | `pcszFile` bir URI | Geçerli |

Runly'nin `OpenWithDialog.cs`'i şu an `ALLOW_REGISTRATION | REGISTER_EXT | EXEC` gönderiyor.
İlk ikisi belge gereği etkisiz; API yüzeyi `OAIF_EXEC` tek başına ile aynı davranıyor.
R1'in "`OAIF_FORCE_REGISTRATION` eklenince Windows doğrudan reddediyor" ölçümü belgelenmiş
metinle birebir eşleşmiyor (belge "yok sayılır" diyor, "reddedilir" demiyor) ama **K23'ün
sonucu artık birincil kaynakla destekli**: bu API varsayılan bağlayamaz, nokta.

Kimler ne kullanmış:
- **`microsoft/PowerToys`** — bu API'yi varsayılan bağlamak için hiç kullanmıyor.
- **`ramensoftware/windhawk-mods` PR #5103** ("Windows 7 Open with restorer") modern
  diyaloğun eksiğini açıkça "**'Always use this app' seçeneği yok**" diye tanımlıyor ve
  çözüm olarak `IContextMenu`, `SHOpenWithDialog`, `ShellExecuteEx/W` ve `OpenWith.exe`
  giriş noktasını **hook'layarak** kendi diyaloğunu çiziyor. Yani "Her zaman"ı geri
  getirmenin bilinen tek yolu explorer'a kod enjekte etmek — Runly için kabul edilemez.
- `reactos/reactos` bu API'yi yeniden yazıyor (PR #8634 `DefaultIcon` yazma düzeltmesi),
  yani davranışın kaynağı shell32 içi, dışarıdan bayrakla değiştirilemiyor.

**Sonuç:** K23 doğru, yalnız metni düzeltilmeli — "Windows 11'de" değil, "Windows 10'dan
beri, Microsoft'un belgelediği şekilde". Bu, iddiayı ölçüme dayalı bir gözlemden
belgelenmiş bir gerçeğe yükseltiyor; kullanıcıya gösterilen metin daha güçlü olur.

### A2. Windows 11 bağlam menüsü — ön menüye çıkmanın bedeli

Klasik `HKCR\*\shell` girdileri Win11'de "Diğer seçenekleri göster" altına düşüyor;
Microsoft'un 2021 tarihli geliştirici blogu bunu tasarım olarak anlatıyor: "'Show more
options' loads the Windows 10 context menu as-is". Ön menüye çıkmak için gereken iki şey
**`IExplorerCommand` uygulaması + paket kimliği (package identity)**. Paketlenmemiş Win32
uygulamaları bu kimliği **sparse package** ile alıyor.

Gerçek maliyet, üç bağımsız kaynağın kesişimi:

1. **Ayrı bir C++ DLL.** `IExplorerCommand`'ı barındıran in-proc COM sunucusu explorer.exe
   içine yükleniyor. Runly'nin .NET 8 / NativeAOT dünyasında bu **yeni bir dil ve yeni bir
   derleme hattı** demek. `vscode-explorer-command` deposu tam olarak bunu yapıyor: VS Code
   ana deposundan ayrı, sadece x86/x64/arm64 shell uzantısı DLL'i ve **imzasız** sparse
   paketi üreten bir depo. İmzalama ve kurulum (Inno) ana depoda.
2. **İmza.** Sparse paket imzasız kurulamıyor. `ikas-mc/ContextMenuForWindows11` bu sorunu
   üç ayrı paket dağıtarak çözmüş: Store paketi, GitHub paketi (**kendinden imzalı sertifika**
   — kullanıcı sertifikayı elle güvenmek zorunda) ve dev paketi. xplorer² blogu ise
   "kendinden imzalı sertifika son kullanıcıda **çalışmaz**, CA sertifikası şart" diyor
   (üçüncü taraf iddiası, **doğrulanamadı** — pratikte ikisi de doğru: sertifika kullanıcı
   makinesinde güvenilir depoya girerse çalışır, girmezse çalışmaz; fark kurulum adımında).
3. **Yan etkiler.** `PowerToys` PR #47177 ölçülmüş bir hasar anlatıyor: sparse paket kaydı,
   `ExternalLocation` klasörünün **DACL'ine AppContainer SID'leri ekliyor**; kurulum kökü
   `ExternalLocation` olarak verildiği için düşük bütünlük seviyesinde çalışan `prevhost.exe`
   aynı klasördeki önizleme işleyici DLL'lerini yükleyemez oldu (.txt, .md, .pdf, .svg).
   Çözüm `ExternalLocation`'ı alt klasöre taşımak olmuş. Yani sparse paket **kurulum
   klasörünün izinlerini sessizce değiştiriyor.**

Kaçış yolu var mı: Evet, **fallback**. PowerToys PR #19195 tam olarak bunu yapıyor — sparse
paket kaydı başarısız olursa modül yok olmuyor, **eski klasik menü girdilerine düşüyor**.
Test yöntemi de öğretici: geliştirici kasten geçersiz imza kullanıp paketi reddettirerek
fallback yolunu doğrulamış.

### A3. Kaldırma dürüstlüğü — ne unutuluyor

- **Asimetrik kayıt.** `PowerToys`'un `src/common/utils/shell_ext_registration.h` dosyası
  tek bir "spec" nesnesinde kaydın **tüm** yüzeyini sayıyor: CLSID kökü, `InprocServer32`,
  `ThreadingModel`, `ContextMenuOptIn`, handler anahtar yolları, ekstra ilişki yolları
  (drag&drop handler'ları), **uzantı başına** `SystemFileAssociations\<ext>\ShellEx\
  ContextMenuHandlers\<ad>` ve bir sentinel değeri. Kaldırma aynı listeyi ters sırayla
  siliyor: önce handler yolları, sonra ekstra ilişkiler, sonra uzantı başına anahtarlar,
  sonra CLSID dalı, en son sentinel. **Unutulan tipik şey uzantı başına yazılan yollar** —
  CLSID silinince görünürde temiz olur ama `SystemFileAssociations` altında öksüz anahtarlar
  kalır. Runly'nin K20'deki öksüz `UserChoice` derdi bunun aynısının varsayılan uygulama
  tarafındaki hali.
- **Sentinel'i tek değer olarak silmek.** PowerToys sentinel **anahtarını** değil sadece
  **değerini** siliyor; aynı anahtarda başka modüllerin verisi olabilir. Runly'nin kaldırma
  kodunda aynı disiplin gerekli.
- **Onarım yolu.** Aynı dosya "kayıt var mı" ile "kayıt eksiksiz mi"yi ayırıyor: temsili bir
  uzantı (`representativeSystemExt`) üzerinden ilişkilerin bozulup bozulmadığına bakıp
  **repaired** durumunu ayrı logluyor. Yarım kalmış kurulum, hiç olmamış kurulumdan farklı
  bir durum.
- **Silmek yerine gizlemek.** `ContextMenuManager` README'sinde projenin temel duruşu şu:
  "çoğu program basit ve kaba bir yedekle-sil yöntemi kullanıyor, bu program mümkün olduğunca
  **sistemin sunduğu anahtar/değerleri kullanarak gizliyor**". Sonucu da açıkça yazıyor:
  başka bir araçla gizlenmiş girdi bu araçta görünmez, bu yüzden **aynı anda iki bağlam
  menüsü yöneticisi kullanmayın**. Örnek gizleme değerleri: `Applications\<exe>` altındaki
  `NoOpenWith` (varsa uygulama Birlikte aç listesinde çıkmaz), klasik verb'ler için
  `LegacyDisable` / `ProgrammaticAccessOnly`.
- **Explorer'ı yeniden başlatmak.** CMM'de bunun için ayrı bir kontrol var
  (`ExplorerRestarter.cs`); `OpenWithPlusPlus` README'si de "kaldırma yeniden başlatma,
  oturum kapatma veya ilgili süreçleri elle kapatmayı gerektirir" diyor ve Explorer dışında
  **Everything**'i de bağlam menüsü uzantısı yükleyen süreç olarak sayıyor. Kaldırma
  "temizlendi" derken hâlâ bellekte olan DLL'i hesaba katmıyor.
- **Antivirüs gürültüsü.** CMM README'si "program çok sayıda kayıt defteri ve dosya
  işlemi yaptığı için Windows Defender tarafından yanlışlıkla virüs sayılabilir, beyaz
  listeye ekleyin" uyarısını doğrudan README'ye koymuş. Runly de HKCU'ya toplu yazan bir
  araç; bu uyarıyı kullanıcıdan önce biz söylemeliyiz.

### A4. Uygulama seçtirme arayüzü — nasıl buluyorlar, nasıl gösteriyorlar

Runly'nin bugünkü `ApplicationFinder` üç kaynak tarıyor: `App Paths` (HKCU+HKLM),
`Software\Classes\Applications` (HKCU+HKLM) ve Başlat menüsü `.lnk` dosyaları; ikon için
`Icon.ExtractAssociatedIcon` kullanıyor, liste "önerilen önce, sonra alfabetik".

Taranan depoların yaptığı fazladan şeyler:

- **`ContextMenuManager`** "Birlikte aç" listesini `Applications\<exe>\shell\<verb>\command`
  üzerinden kuruyor; görünen adı `FriendlyAppName` değerinden okuyor ve yazıyor, `NoOpenWith`
  değerinin varlığını görünürlük anahtarı olarak kullanıyor. Silerken önce verb dalını,
  sonra **`shell` altında başka verb kalmadıysa** uygulama anahtarını siliyor.
- **`ContextMenuManager` — dolaylı dizeler.** `ResourceString.cs` `SHLoadIndirectString`
  sarıyor; `@shell32.dll,-9752` ve `@{paket?ms-resource://...}` biçimindeki adları çözüyor.
  Bu biçim kayıt defterinde çok yaygın; çözülmezse listede ham dize görünür.
- **`ContextMenuManager` — ikon.** `ExtractIconEx` + `SHGetFileInfo` (SHGFI_SYSICONINDEX)
  kullanıyor ve `"yol,indeks"` biçimini elle ayrıştırıyor. `Icon.ExtractAssociatedIcon`
  bunu yapamaz: indeks alamaz ve büyük boy ikon vermez.
- **`dahall/Vanara`** — asıl bulgu burada. `Shell32` paketi `SHAssocEnumHandlers`,
  `SHAssocEnumHandlersForProtocolByApplication`, `IAssocHandler`, `IAssocHandlerInvoker` ve
  `SHOpenWithDialog`'un hepsini sarıyor. `IAssocHandler` **Windows'un kendi "Birlikte aç"
  listesini** verir: uzantıya kayıtlı işleyiciler, `GetUIName` ile görünen ad,
  `GetIconLocation` ile "yol,indeks", `IsRecommended` ile Windows'un kendi önerilen/diğer
  ayrımı, `CreateInvoker` ile çalıştırma. Yani sıralama ve "önerilen" rozeti tahmin edilmek
  yerine sistemden alınabilir.
- **`stax76/OpenWithPlusPlus`** uygulama listesi çıkarmıyor; kullanıcı komutu elle
  tanımlıyor ama **uzantı grupları makro** olarak veriliyor (`%video%`, `%audio%`,
  `%image%`, `%subtitle%`). Runly'nin uzantı listesi için doğrudan uygulanabilir fikir.

---

## B. Depolar

### 1. `BluePointLilac/ContextMenuManager`
GPL-3.0 · 19.859★ · 175 açık issue · son push 2024-08-17 · son etiketli sürüm **3.3.3.1
(2021-08-28)** · C# / WinForms. Windows'un tüm bağlam menüsü sahnelerini (dosya, klasör,
Yeni, Gönder, **Birlikte aç**, IE, WinX) tek arayüzde açıp kapatan yönetici.

**Runly'ye alınacak fikir:** "silme, gizle" duruşu ve bunun README'de açıkça beyan edilmesi.
Sistemin kendi değerleriyle (`NoOpenWith`, `LegacyDisable`, `ProgrammaticAccessOnly`)
gizlemek, kaldırmayı geri alınabilir ve başka araçlarla uyumlu yapıyor. Ayrıca her öğe için
"kayıt defteri konumuna git", "dosya konumuna git" ve **".reg olarak dışa aktar"** menüsü
var (`ITsiRegExportItem`) — kullanıcıya kendi yedeğini verme deseni.

**Kaçınılacak hata:** README'nin uyumluluk listesi hâlâ "Win10, 8.1, 8, 7, Vista" diyor;
Windows 11 hiç yazmıyor ve son etiketli sürüm 2021'den. Beş yıllık bir menü modeline göre
yazılmış bir araç, Win11'in ön menüsünü hiç görmüyor. Runly de "hangi Windows sürümünde
neyin geçerli olduğunu" belgelemezse aynı yere düşer.

### 2. `moudey/Shell` (nilesoft.org)
MIT · 6.782★ · 244 açık issue · son push 2026-02-09 · son etiketli sürüm **v1.9.15
(2024-02-14)** · C++. Bağlam menüsünü kendi `.nss` betik diliyle baştan kuran, ifade
sözdizimi / iç içe menü / çok sütun / SVG ikon destekleyen genişletici.

**Runly'ye alınacak fikir:** menü tanımının **düz metin yapılandırma dosyası** olması.
Kullanıcı GUI'den de düzenleyebiliyor, dosyayı da elle düzenleyip paylaşabiliyor; ekosistem
bunun üstünde büyümüş (catppuccin/nilesoft-shell teması 292★, RubicBG snippet deposu 180★
— sayılar 2026-08-22'de doğrulandı). Runly'nin sparse `config.json`'ı bu yönde bir varlık,
"paylaşılabilir profil" olarak konumlanabilir.

**Kaçınılacak hata:** son etiketli sürüm ile son commit arasında **iki yıl** var; kullanıcıya
"nightly.link" üzerinden CI çıktısı indirtiyor. İmzasız/etiketsiz dağıtım Runly için
kabul edilemez, K'ler bunun tersini söylüyor.

### 3. `microsoft/PowerToys`
MIT · 137.958★ · 7.514 açık issue · son push 2026-08-22 · son sürüm **v0.100.2
(2026-06-26)** · çoklu dil. Windows 11 ön menüsüne çıkan modüllerin (PowerRename, File
Locksmith, New+, Image Resizer) referans uygulaması.

**Runly'ye alınacak fikir (üç tane, hepsi doğrudan kullanılabilir):**
(a) `src/common/utils/shell_ext_registration.h` — kaydın tüm yüzeyini **tek bir bildirim
nesnesinde** toplayıp kayıt ve kaldırmayı aynı listeden türetmek; ayrıca "kayıtlı",
"eksik → onarıldı", "kayıtsız" olarak üç durum. (b) PR #19195 — sparse paket kaydı
başarısız olursa **klasik menüye düşmek**, ve bunu kasten bozuk imzayla test etmek.
(c) `src/PackageIdentity/` — sparse manifest'in ana koddan ayrı, kendi `BuildSparsePackage`
betiğiyle üretilmesi.

**Kaçınılacak hata:** PR #47177 — sparse paketin `ExternalLocation`'ını kurulum köküne
vermek, o klasörün DACL'ini AppContainer SID'leriyle kirletip aynı klasördeki düşük
bütünlükteki tüketicileri (önizleme işleyicileri) bozdu. Ayrıca issue #48951: Win10
modüllerinin çalışma zamanı kaydına geçirilmesi, bir yıldan uzun süredir kapanmayan
"bağlam menüsü girdisi yok" şikâyetleri üretiyor. **Çalışma zamanı kaydı, kurulum
zamanı kaydından daha kırılgan.**

### 4. `microsoft/vscode-explorer-command`
MIT · 60★ · 2 açık issue · son push 2026-01-27 · son sürüm **v8.0.0-398351 (2026-01-27)**
· C++. VS Code'un "Code ile aç" Win11 ön menü girdisini üreten, **yalnız** shell uzantısı
DLL'i ve imzasız sparse paketi çıkaran ayrı depo.

**Runly'ye alınacak fikir:** sınır çizimi. README aynen "This repository is only responsible
for creating the shell extensions and unsigned sparse package, `microsoft/vscode` is
responsible for **code signing and installing it through Inno**" diyor. Yani shell uzantısı
ayrı bir derleme birimi, imzalama ve kurulum ana ürünün işi. Runly bu yola girerse aynı
sınır kurulmalı: `Runly.ShellExt` ayrı proje, imza ve kurulum `Runly.Settings` tarafında.
İkinci fikir: `GetState` çağrısı, girdi kullanıcı tarafından kapatılmışsa `ECS_HIDDEN`
döndürüyor — **kaydı silmeden gizlemek**, A3'teki CMM duruşunun ön menü karşılığı.
`GetIcon` modülün kendi yolunu döndürüyor, yani ikon DLL kaynağından geliyor.

**Kaçınılacak hata:** yok denecek kadar az issue ve 60★, ama bu deponun tek kullanıcısı
VS Code. Örnek olarak mükemmel, bağımlılık olarak anlamsız — kopyalanacak olan desen.

### 5. `ikas-mc/ContextMenuForWindows11`
LGPL-3.0 · 2.865★ · 20 açık issue · son push 2026-07-25 · son sürüm **5.8.0.0
(2026-05-29)** · C#. Kullanıcının JSON dosyalarıyla Win11 **ön menüsüne** kendi komutlarını
eklemesini sağlayan MSIX uygulaması.

**Runly'ye alınacak fikir:** dağıtım kanalını üçe ayırmış — Store paketi, GitHub paketi
(kendinden imzalı sertifika ile), dev paketi — ve v3.8'den beri **üçü aynı anda kurulabiliyor**.
Sertifika sorununu gizlemek yerine kanal seçimine dönüştürmüş. Ayrıca menü tanımı JSON ve
depo/discussions altında paylaşılan örnek menü kütüphanesi var.

**Kaçınılacak hata:** LGPL-3.0. Runly'nin lisansı ne olursa olsun bu depodan kod alınamaz;
alınacak olan yalnızca "üç kanal" fikri. Store dağıtımı ayrıca geliştirici hesabı ve
sertifika maliyeti demek — README'nin "free, no limit, buy = coffee" cümlesi kullanım
sayısı hakkında bir şey söylemiyor, **kullanıcı sayısı iddiası doğrulanamadı**.

### 6. `stax76/OpenWithPlusPlus`
MIT · 437★ · 10 açık issue · son push 2025-11-26 · son sürüm **v4.0 (2022-08-13)** ·
VB.NET + C++ shell uzantısı. Bağlam menüsüne komut satırı tabanlı özel "Birlikte aç"
girdileri ekleyen kabuk uzantısı; GUI'den kur/kaldır düğmesiyle yönetiliyor.

**Runly'ye alınacak fikir:** uzantı gruplarını **makro** olarak tanımlaması
(`%video%`, `%audio%`, `%image%`, `%subtitle%`) ve kullanıcının bir kuralı "mp4 mkv avi"
yerine `%video%` yazarak kurabilmesi. Runly'nin uzantı kataloğu zaten kategorili;
aynı makroyu "Uygulama seç" ve kural ekranında kullanıcıya açmak ucuz bir kazanç.
İkinci fikir: README'nin kaldırma bölümü **"yeniden başlatma gerekir ve şu süreçler DLL'i
tutar"** diye açıkça yazıyor — dürüst kaldırma metni örneği.

**Kaçınılacak hata:** README'nin kendi tavsiyesi bile "Win11 kullanıcısı başka araç
kullansın" — proje klasik Win32 menüsüne bağlı ve Win11'de kayıt defteri hilesiyle klasik
menüyü geri getirmeyi öneriyor. **Kullanıcıdan sistem davranışını değiştirmesini istemek
çözüm değil.** Runly asla "önce şu regedit hilesini uygula" dememeli.

### 7. `std-microblock/breeze-shell`
**AGPL-3.0** · 3.293★ · 68 açık issue · son push 2026-08-04 · son sürüm **0.1.34
(2026-04-12)** · C++. Win10/11 için bağlam menüsünü tamamen değiştiren, animasyonlu,
gömülü JavaScript API'siyle genişletilebilen alternatif menü.

**Runly'ye alınacak fikir:** eklenti API'sinin **menü dinleyicisi** modeli — menü açılırken
olay yayınlanıyor, betik öğe ekliyor/sıralıyor. Runly'nin "güvenlik kapısı" da benzer bir
karar noktası; kural değerlendirmesini olay tabanlı ve tek yerde tutmak doğru desen.

**Kaçınılacak hata:** bu araç explorer'ın kendi menüsünü **enjeksiyonla** ele geçiriyor ve
README'nin en üstünde "hâlâ aktif geliştirmede, hata bulursanız bildirin" uyarısı var.
AGPL-3.0 ayrıca ticari/kapalı bir ürüne bulaşıcı. Runly için hem hukuki hem teknik olarak
kapalı yol; **"Win11 menüsünü hook'la" seçeneği ciddiye alınmadan elenmeli.**

### 8. `cjee21/IExplorerCommand-Examples`
MIT · 20★ · 0 açık issue · son push 2026-08-20 · sürüm etiketi `latest` (hareketli etiket)
· C++. `IExplorerCommand`'ın WRL ve C++/WinRT ile iki ayrı minimal uygulaması + demo.

**Runly'ye alınacak fikir:** karar vermeden önce **maliyeti ölçmenin en ucuz yolu**. İki
uygulama arasındaki fark, demo uygulaması ve gerçek dünya örneklerine bağlantılar
(MediaInfo, NanaZip, VS Code) tek yerde. Runly'nin "ön menüye çıkmalı mıyız" sorusunu bir
hafta sonu içinde deneyle cevaplamak için doğru başlangıç.

**Kaçınılacak hata:** sürüm etiketi `latest` — sabit sürüm yok, tarih olarak son push ile
aynı. Referans olarak okunur, bağımlılık olarak alınmaz.

### 9. `dahall/Vanara`
MIT · 2.090★ · **7 açık issue** · son push 2026-08-17 · son sürüm **v5.0.7 (2026-08-15)**
· C#. Windows yerel API'lerinin geniş P/Invoke + sarmalayıcı kütüphanesi.

**Runly'ye alınacak fikir:** `Vanara.PInvoke.Shell32` `SHAssocEnumHandlers` /
`IAssocHandler` / `IAssocHandlerInvoker` üçlüsünü sarıyor. Bu, "Uygulama seç" penceresinin
**önerilenler bölümünü Windows'un kendisinden almasını** sağlar: ad (`GetUIName`), ikon
konumu (`GetIconLocation`, "yol,indeks"), önerilen mi (`IsRecommended`), çalıştırma
(`CreateInvoker`). Runly'nin elle App Paths + Start menu taraması bunun yerine değil,
**yanında** durmalı ("Tüm uygulamalar" sekmesi).

**Kaçınılacak hata:** bağımlılık yüzeyi. Vanara çok sayıda küçük NuGet paketine bölünmüş
ama yine de büyük; `Runly.exe` NativeAOT ise trimming ve COM interop davranışı ayrıca
sınanmalı. Alternatif: yalnız `IAssocHandler` için elle P/Invoke yazıp Vanara'yı **referans
belge** olarak kullanmak. Bu, Runly'nin mevcut `OpenWithDialog.cs` tarzıyla da tutarlı.

### 10. `File-New-Project/EarTrumpet`
Lisans **NOASSERTION** (GitHub OSI olarak tanımıyor) · 11.300★ · 110 açık issue · son push
2026-08-16 · GitHub'daki son sürüm **1.3.2.0 (2016-06-23)**, toplam 7 release · C#/WPF.
Windows ses karıştırıcısı; kabuk entegrasyonu ve Store paketleme deseni için tarandı.

**Runly'ye alınacak fikir:** dağıtım kanalı olarak Store + Chocolatey kullanıp GitHub
release'i bırakması, "paketli uygulama olarak dağıtılan ama masaüstü davranışı sergileyen"
bir ürünün mümkün olduğunu gösteriyor. Runly'nin sparse paket kararında "tam MSIX'e geç"
seçeneği de masada olmalı.

**Kaçınılacak hata (ve bir uyarı):** GitHub'ın "latest release" verisi **on yıl eski**;
gerçek dağıtım Store üzerinden. Bir deponun canlılığını yalnız `releases/latest` ile
ölçmek yanıltıcı — bu taramada da son push ile birlikte bakıldı. Ayrıca README'deki
"2022 Store Community Choice ödülü" ve basın alıntıları ürün kalitesi hakkında bir kanıt
değil; **kullanım/başarı iddiası olarak doğrulanamadı.** Lisansın OSI onaylı olmaması
(NOASSERTION) kod alınmasını ayrıca engelliyor.

---

## Runly için sonuç

1. **K23'ün metnini güçlendir, kapsamını düzelt.** "Windows 11'de" yerine "Windows 10'dan
   beri, Microsoft'un belgelediği davranış" yaz ve `OPENASINFO` Remarks alıntısını kaynak
   göster. `MainForm.cs:1221` ve `OpenWithDialog.cs` içindeki gerekçe cümlesi de aynı
   şekilde düzeltilmeli. Bu, ölçüme dayalı bir iddiayı birincil kaynağa dayandırır.
2. **`OpenWithDialog.cs`'in bayraklarını `OAIF_EXEC`'e indir.** `OAIF_ALLOW_REGISTRATION`
   ve `OAIF_REGISTER_EXT` belge gereği etkisiz; taşımak "belki çalışır" izlenimi veriyor.
   Belgedeki tek gerçek davranış farkı şu: `OAIF_EXEC` **verilmezse** Windows kullanıcıya
   "varsayılanları Ayarlar'dan değiştirin" bilgi penceresi gösteriyor — bu, K27'deki deep
   link akışı için sıfır maliyetli bir alternatif olarak sınanabilir.
3. **"Uygulama seç" penceresini iki bölmeye ayır: Önerilenler = `IAssocHandler`, Tümü =
   mevcut `ApplicationFinder`.** `SHAssocEnumHandlers` uzantıya kayıtlı işleyicileri,
   Windows'un kendi görünen adı, ikon konumu ve `IsRecommended` bayrağıyla veriyor. Sıralama
   ve "önerilen" rozeti tahmin edilmek yerine sistemden gelir; Explorer'ın gösterdiği liste
   ile Runly'nin listesi arasındaki fark kapanır. Vanara'yı bağımlılık yapmak yerine
   referans alıp tek arayüz için P/Invoke yazmak mevcut kod tarzıyla tutarlı.
4. **İkon ve ad çözümlemesini düzelt.** `Icon.ExtractAssociatedIcon` "yol,indeks" biçimini
   ve büyük boy ikonu veremiyor; `ExtractIconEx` + `SHGetFileInfo` yolu gerekiyor
   (`ContextMenuManager/ResourceIcon.cs` deseni). Görünen adlar için `@dosya,-id` ve
   `@{paket?ms-resource://...}` biçimleri `SHLoadIndirectString` ile çözülmeli, aksi hâlde
   listede ham dize çıkar. Ayrıca `Applications\<exe>` altındaki `FriendlyAppName`
   okunmalı ve **`NoOpenWith` değeri olan uygulamalar listeden çıkarılmalı** — Windows
   onları kasten gizliyor.
5. **`Runly.exe`'yi seçilebilir listeden çıkar.** `ProcessLauncher.IsRunlyExecutable`
   döngüyü çalışma anında yakalıyor ama `ApplicationFinder`/`ChooseApplicationDialog`
   içinde kendini eleyen bir filtre yok; kullanıcı Runly'yi seçebiliyor ve hatayı ancak
   dosyayı açtığında görüyor. Hatayı seçim anına taşı.
6. **Kaydın tüm yüzeyini tek bildirimden türet, kaldırmayı aynı listeden çalıştır.**
   PowerToys'un `shell_ext_registration.h` deseni: ProgID kökü, `Capabilities\
   FileAssociations`, `RegisteredApplications`, `Applications\Runly.exe\SupportedTypes`,
   uzantı başına `OpenWithProgids` — hepsi tek yerde sayılsın, kaldırma ters sırayla aynı
   listeyi dolaşsın ve **üç durum** raporlasın: kayıtlı / eksik→onarıldı / kayıtsız.
   K20'nin öksüz `UserChoice` kuralı bu raporun bir satırı olur.
7. **Win11 ön menüsü kararını ertele ama deneyle kapat.** Bugün gerekli değil: Runly'nin
   akışı çift tıklama, sağ tık değil. Karar verileceği zaman maliyet üç kalem — ayrı C++
   `IExplorerCommand` DLL'i (`vscode-explorer-command` sınırıyla ayrı proje), imzalı sparse
   paket (kanal seçimi: Store / CA sertifikası / kendinden imzalı, `ContextMenuForWindows11`
   deseni) ve **sparse paketin `ExternalLocation` klasörünün DACL'ini değiştirmesi**
   (PowerToys #47177 — bu klasör kurulum kökü olmamalı). Karar öncesi
   `cjee21/IExplorerCommand-Examples` ile bir hafta sonu prototipi yeterli. Ne yapılırsa
   yapılsın klasik menüye **fallback** korunmalı (PowerToys #19195).
8. **Kaldırma metnini dürüstleştir: silme değil gizleme + hâlâ açık süreçler.**
   Geri alınabilir gizleme için sistemin kendi değerlerini kullan (`NoOpenWith`,
   `LegacyDisable`), kalıcı silmeden önce etkilenen anahtarların **`.reg` yedeğini
   kullanıcıya ver** (CMM deseni). Kaldırma ekranı Explorer'ın yeniden başlatılması
   gerekebileceğini ve Everything gibi üçüncü taraf süreçlerin girdileri hâlâ gösterebileceğini
   söylesin (`OpenWithPlusPlus` deseni). CMM'in Defender uyarısı da eklenmeli: toplu kayıt
   defteri yazımı yanlış pozitif üretebilir.

---

## Kaynaklar

Birincil (GitHub API, 2026-08-22):
- `gh api repos/{BluePointLilac/ContextMenuManager, moudey/Shell, microsoft/PowerToys,
  microsoft/vscode-explorer-command, ikas-mc/ContextMenuForWindows11,
  stax76/OpenWithPlusPlus, std-microblock/breeze-shell, cjee21/IExplorerCommand-Examples,
  dahall/Vanara, File-New-Project/EarTrumpet}` ve her biri için `/releases/latest`.
- Depo ağaçları ve README'ler: `git/trees?recursive=1`, `raw.githubusercontent.com`.

Microsoft belgeleri:
- OPENASINFO — https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/ns-shlobj_core-openasinfo
- SHOpenWithDialog — https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenwithdialog
- Extending the Context Menu and Share Dialog in Windows 11 (Windows Developer Blog,
  2021-07-19) — https://blogs.windows.com/windowsdeveloper/2021/07/19/extending-the-context-menu-and-share-dialog-in-windows-11/

Depo içi kanıt:
- PowerToys PR #19195 (tier-1 menü fallback), PR #47177 (sparse paket DACL kirlenmesi),
  issue #48951 (Win10 çalışma zamanı kaydı regresyonu),
  `src/common/utils/shell_ext_registration.h`, `src/PackageIdentity/AppxManifest.xml`.
- vscode-explorer-command README ve `src/explorer_command.cc` (GetTitle/GetIcon/GetState).
- ContextMenuManager `Controls/OpenWithList.cs`, `Controls/OpenWithItem.cs`,
  `BluePointLilac.Methods/{ResourceString,ResourceIcon}.cs`, README (Çince).
- windhawk-mods PR #5103 (Windows 7 Open With restorer) — modern diyaloğun "Her zaman"
  eksikliğinin bağımsız teyidi.

Üçüncü taraf, **doğrulanamadı** olarak işaretli:
- xplorer² blogu, sparse paket imzalama maliyeti —
  https://www.zabkat.com/blog/win11-explorer-menu-package.htm
- EarTrumpet README'sindeki ödül ve basın alıntıları (kullanım/başarı kanıtı değil).
