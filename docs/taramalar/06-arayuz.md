# 06 — Arayüz taraması: WinForms koyu tema, büyük liste, native sızıntı

Tarama tarihi: 2026-08-22. Tüm sayılar `gh api repos/<owner>/<repo>` ve
`.../releases/latest` çıktısından, o gün alındı. GitHub'ın `open_issues` alanı açık PR'ları
da sayar — "açık iş" olarak okunmalı, "açık hata" olarak değil.

## Künye

| Depo | Lisans | Yıldız | Açık iş | Son push | Son etiketli sürüm |
|---|---|---|---|---|---|
| microsoft/PowerToys | MIT | 137.958 | 7.514 | 2026-08-22 | v0.100.2 (2026-06-26) |
| files-community/Files | MIT | 44.705 | 460 | 2026-08-21 | v4.2.9 (2026-08-19) |
| BluePointLilac/ContextMenuManager | GPL-3.0 | 19.859 | 175 | 2024-08-17 | 3.3.3.1 (2021-08-28) |
| File-New-Project/EarTrumpet | NOASSERTION | 11.300 | 110 | 2026-08-16 | 1.3.2.0 (2016-06-23) |
| mRemoteNG/mRemoteNG | GPL-2.0 | 11.064 | 864 | 2026-08-21 | v1.76.20 (2019-04-12) |
| dotnet/winforms | MIT | 4.847 | 861 | 2026-08-22 | v10.0.8 (2026-05-12) |
| Tyrrrz/LightBulb | MIT | 2.756 | 15 | 2026-08-03 | 2.7.1 (2026-05-19) |
| dahall/Vanara | MIT | 2.090 | 7 | 2026-08-17 | v5.0.7 (2026-08-15) |
| ysc3839/win32-darkmode | MIT | 521 | 12 | 2023-03-28 | sürüm yok |
| BlueMystical/Dark-Mode-Forms | MIT | 188 | 18 | 2025-07-21 | v1.8.18 (2025-04-07) |

`ysc3839/win32-darkmode` hiç sürüm etiketlememiş (`releases/latest` → 404); referans kod,
kütüphane değil. EarTrumpet'in lisansı GitHub'a göre tanınmıyor — aşağıda §EarTrumpet.

---

## Beş soruya cevap

### 1. .NET 9/10 yerleşik dark mode — Runly .NET 8'den çıkmalı mı?

**API var, olgun değil.** `Application.SetColorMode(SystemColorMode)` .NET 9'da geldi ve
hâlâ `[Experimental("WFO5001")]`. Microsoft Learn'deki API sayfası (13.08.2026 güncel)
`windowsdesktop-9.0`, `10.0` ve `11.0` monikerlerinin üçünde de Experimental imzayı
gösteriyor; `dotnet/winforms/docs/list-of-diagnostics.md` dosyasında WFO5001 satırının
"Removed" sütunu boş. Yani .NET 11'de bile deneysel.

Belgelenen sınırlar, doğrudan Learn sayfasından: koyu mod **yalnızca Windows 11 ve
sonrasında** çalışır; sistem yüksek kontrast modundaysa devre dışıdır; `System` seçilse
bile **uygulama sistem ayarı değişince kendiliğinden uyum sağlamaz**; UI kurulmadan önce
çağrılmalıdır.

Olgunluk ölçüsü, depodaki iş yükü: başlığında "dark mode" geçen **61 açık, 82 kapalı**
konu; başlığında "dark" geçen **20 açık PR**. Runly'yi doğrudan ilgilendiren ikisi:

- **DataGridView**: `#14267` "Enable DataGridView dark mode theming via AppContext switch"
  bir PR ve **04.02.2026'dan beri açık**, son güncelleme 22.04.2026, etiketi
  `waiting-review`. Yani DataGridView'ün koyu paleti daha framework'e girmedi.
- **MessageBox**: `#11896` "Control pops up dialogs and MessageBox window are not in Dark
  mode when DarkMode enabled" — 16.08.2024'ten beri açık.

Ayrıca `#12420` ToolTip'in koyu modda yeterince koyu olmaması (04.11.2024'ten beri açık),
`#14107`/`#14578` ComboBox'ın ilk TabPage'de temayı reddetmesi.

**Sonuç:** yükseltme, elle çizilen temanın yarısını gereksiz kılmaz. Runly'nin en çok
emek verdiği iki yer — ızgara ve MessageBox — tam olarak framework'ün bitmemiş kısmı.
Üstelik Runly'nin istediği şey "sistem koyu"su değil, kendi neon paleti; `SetColorMode`
palet vermez, yalnızca native sızıntıları kapatır. .NET 10'a geçmek başka gerekçelerle
(destek süresi, `LibraryImport`, performans) savunulabilir; "temayı silelim" gerekçesiyle
savunulamaz.

### 2. Native sızıntılar — kim neyi nasıl kapatmış

| Yüzey | Çözüm | Kırılganlık |
|---|---|---|
| Başlık çubuğu | `DwmSetWindowAttribute(hwnd, 20, TRUE)` | Attribute numarası 19041 öncesi Win10'da **19**'du; Runly `20` sabitini kullanıp dönüş değerini yok sayıyor → eski derlemede sessizce beyaz kalır |
| Süreç geneli | `uxtheme` ordinal **135** | ysc3839'a göre 135, build < 18362'de `AllowDarkModeForApp(bool)`, ≥ 18362'de `SetPreferredAppMode(enum)` — **aynı ordinal, farklı imza** |
| Kontrol scrollbar'ı | `SetWindowTheme(h, "DarkMode_Explorer", null)` | Yalnızca native scrollbar'lı kontrollerde işe yarar |
| Non-client scrollbar | comctl32'nin uxtheme ordinal 49 (`OpenNcThemeData`) IAT hook'u, "ScrollBar" → "Explorer::ScrollBar" | ysc3839'un yöntemi; C#'ta pratikte erişilemez, kabul edilecek eksik |
| ComboBox açılır listesi | Sub-app adı `"DarkMode_CFD"`; liste ayrı bir pencere | Framework'ün kendi çözümü bile TabPage'de bozuluyor (`#14107`) |
| DataGridView scrollbar | Dark-Mode-Forms bunlara `typeof(DataGridView).GetProperty("HorizontalScrollBar", NonPublic)` **reflection**'ıyla ulaşıyor | Private üye adına bağlı; .NET sürümü değişirse sessizce çöker |
| MessageBox | Temalanamaz. Hem ContextMenuManager hem Dark-Mode-Forms kendi Form'unu yazmış ("Window's default MessageBox can not be themed" — Dark-Mode-Forms README) | Yok; doğru yol bu. Runly'nin `NeonMessageBox`'ı isabetli |
| ToolTip | `OwnerDraw` | `#12420` açık — framework'ün kendi koyu tooltip'i de yetersiz |
| OpenFileDialog | **doğrulanamadı** — `dotnet/winforms`'ta konuyla ilgili açık kayıt bulamadım (tek arama sonucu `#12305`, alakasız bir çökme). Kabuk diyaloğu süreç tercihine değil sistem temasına bakıyor olabilir; Runly'de elle test gerekiyor |

Tema değişimini yakalama: ysc3839 `WM_SETTINGCHANGE` + `lParam == "ImmersiveColorSet"`
karşılaştırması yapıp `RefreshImmersiveColorPolicyState()` çağırıyor. PowerToys ise
`ThemeListener.cs` içinde **WMI `RegistryValueChangeEvent`** ile `AppsUseLightTheme`
anahtarını dinliyor — bir WinForms uygulaması için ağır (WMI aboneliği + arka plan
iş parçacığı); mesaj tabanlı yol bedava.

### 3. Büyük liste — 400+ satırı akıcı tutmak

PowerToys Ayarlar penceresi (`ShellPage.xaml.cs`) birincil kaynak:
`SearchIndexService.BuildIndex()` açılışta **bir kez** indeks kurar; `SearchDebounceMs = 500`
sabiti ve `CancellationToken` ile her tuşta önceki arama iptal edilir; eşleştirme
`Task.Run` içinde, UI iş parçacığı dışında koşar (`Common.Search.FuzzSearch`); aynı sorgu
tekrar gelirse `_lastSearchResults` yeniden kullanılır. 500 ms bir ayar penceresi için
uzun — yerel 400 satırlık bir tabloda 120-150 ms yeterli, ama **desen** doğru:
indeks + debounce + iptal + UI dışı eşleştirme.

Files tarafında liste zaten sanallaştırılmış (WinUI liste kontrolleri) ve asıl maliyet
ikonlarda; onlar `STATask.Run` ile ayrı iş parçacığında, `ReturnOnlyIfCached` seçeneğiyle
önce hızlı boyanıp sonra tamamlanıyor.

Runly'nin bugünkü hâli (`MainForm.cs:262`, `ChooseApplicationDialog.cs:96`): `TextChanged`
doğrudan `RefreshExtensionGrid()`/`ApplyFilter()` çağırıyor — debounce yok, iptal yok,
filtreleme UI iş parçacığında. `MainForm.cs:665` zaten yenileme süresini `Stopwatch` ile
ölçüp logluyor; ölçüm var, frenleme yok.

### 4. Uygulama simgesi — hangi API

Üç depo da `Icon.ExtractAssociatedIcon`'u **kullanmıyor**:

- **PowerToys** (`Wox.Infrastructure/Image/WindowsThumbnailProvider.cs`):
  `IShellItemImageFactory.GetImage`. `.exe`/klasör için `ThumbnailOptions.IconOnly`,
  belgeler için `ThumbnailOnly` ve `HResult.ExtractionFailed` dönerse `IconOnly`'ye düşüş.
  Boyut çağrıda piksel olarak veriliyor, yani istenen boyutta net çıkıyor.
- **Files** (`FileThumbnailHelper.cs`): aynı API, üstüne
  `size = requestedSize * App.AppModel.AppWindowDPI` — ikon **fiziksel piksel** cinsinden
  isteniyor. Kabuk COM'u STA gerektirdiği için `STATask.Run`.
- **EarTrumpet** (`Interop/Helpers/IconHelper.cs`): en düşük seviye yol —
  `LoadLibraryEx(..., LOAD_LIBRARY_AS_DATAFILE)` + `FindResource(RT_GROUP_ICON)` +
  `LookupIconIdFromDirectoryEx(cx, cy)` + `CreateIconFromResourceEx`. `cx/cy`
  `GetSystemMetricsForDpi(SM_CXSMICON, dpi)`'den geliyor; yani ikon grubundan **o DPI'ya
  en yakın kare** seçiliyor, ölçekleme yok. Ayrıca `PathParseIconLocationW` ile
  `"C:\app.exe,3"` biçimli kayıt defteri değerlerini ayrıştırıyor.

Önbellek: PowerToys `ImageCache.cs` — `MaxCached = 50`, `PermissibleFactor = 2`
(sözlük 100'ü aşınca temizlik, her eklemede değil), kullanım sayacı `Usage` diske
yazılıyor, sonraki açılışta sık kullanılan ikonlar önce yükleniyor.

`ExtractAssociatedIcon` neden yetmiyor: sabit 32×32 verir; %150'de 48 piksel gerekir,
32'yi büyütmek bulanık sonuç üretir. Runly şu an tam bunu yapıyor
(`ChooseApplicationDialog.cs:301`).

### 5. DPI — koddan kurulan yerleşimde ne kırılıyor

`dotnet/winforms`'ta başlığında "dpi" geçen **37 açık konu** var. Runly'ye birebir denk
gelen: `#6382` "ItemHeight of ListBox is not scaled on high DPI in **OwnerDrawFixed**
mode" — 21.12.2021'den beri açık. Runly'nin owner-draw `ListBox`'ı bu tuzağın içinde.
Yanında `#9293` (GroupBox `AutoScaleMode=Dpi`), `#10402` (`Control.Margin` değerleri),
`#13194` (TreeView ikonları ölçeklenmiyor), `#14784` (NumericUpDown okları).

En sık kırılan üç şey, taranan kodlardan çıkan ortak desen:
1. Ölçek katsayısını **açılışta bir kez** hesaplayıp sabitlemek (bkz. ContextMenuManager).
2. Owner-draw ölçülerini (satır yüksekliği, ikon kutusu, padding) piksel sabiti yazmak —
   font vektör olduğu için büyür, kutu büyümez, yazı taşar.
3. İkonu mantıksal boyutta isteyip GDI+'a büyüttürmek.

---

## Depo depo

### microsoft/PowerToys — MIT, C#/C++, WinUI 3 ayarlar kabuğu
Onlarca modülün ayarlarını tek pencerede toplayan kabuk: NavigationView + sayfa başına
ViewModel, `NavigationParams` ile derin bağlantı, `AutoSuggestBox` ile genel arama.

**Runly'ye alınacak fikir:** `ShellPage` arama boru hattı — açılışta bir kez kurulan
indeks, `CancellationToken`'lı debounce, `Task.Run` içinde eşleştirme, aynı sorguda önbellek.
Runly'nin 400+ satırlık uzantı tablosu için birebir uyar; tek fark debounce süresi
(500 ms yerine ~150 ms).

**İkinci fikir:** `Wox.Infrastructure/Image` üçlüsü (`ImageLoader` + `ImageCache` +
`WindowsThumbnailProvider`) — ikon yükleme UI'dan tamamen ayrılmış, kullanım sayacına
göre budanan ve diske yazılan önbellek.

**Kaçınılacak hata:** `ThemeListener` tema değişimini WMI kayıt defteri izleyicisiyle
yakalıyor. Bir WinForms uygulaması için `WM_SETTINGCHANGE`/`SystemEvents.UserPreferenceChanged`
bedava; WMI ek süreç ve gecikme demek.

### BluePointLilac/ContextMenuManager — GPL-3.0, WinForms, birebir aynı teknoloji
Windows sağ tık menüsü yöneticisi. Tamamen koddan kurulmuş özel kontrol seti
(`BluePointLilac.Controls`: `MyMainForm`, `MySideBar`, `MyListBox`, `MyToolBar`) —
Runly'nin `NeonControls`/`NeonForm` yapısının kardeşi.

**Runly'ye alınacak fikir:** `MessageBoxEx.cs` — MessageBox'ı kendi Form'uyla değiştirirken
ikonları **işletim sisteminden** çekiyor (`MessageBoxImage.Error = GetImage(-98)` gibi stok
ikon kimlikleri). Kendi glif çizmek yerine sistem ikonunu almak hem DPI'da net kalır hem
Windows sürümüyle birlikte güncellenir. `ResourceString.cs` de aynı mantığın devamı:
`@dll,-id` biçimli kayıt defteri dizelerini sistemden çözüyor — Runly zaten Explorer tür
adlarını gösterdiği için bu yol tanıdık olmalı.

**Kaçınılacak hata:** `HighDpi.cs`. Ölçek katsayısı `static readonly double DpiScale`
olarak **birincil ekrandan, süreç ömrü boyunca bir kez** hesaplanıyor; üstelik bunu almak
için WinForms projesine `PresentationFramework` (WPF) referansı ekleniyor. Runly
`PerMonitorV2` hedefliyor — ikinci monitöre taşınan pencerede bu yaklaşım yanlış sayı
üretir. Doğrusu `control.DeviceDpi` ve `OnDpiChangedAfterParent`.

**Ayrıca:** son etiketli sürüm 2021, son push 2024-08; 175 açık iş. Okunur, bağımlılık
kurulmaz. GPL-3.0 olduğu için kod alınamaz — desen alınır.

### dotnet/winforms — MIT, birincil kaynak
Framework'ün kendisi. Koyu mod durumu §1'de.

**Runly'ye alınacak fikir:** `#14267` PR'ının yaklaşımı — koyu palet DataGridView içinde
**tek bir `ApplyDarkModeTheming` noktasında** toplanıyor ve `AppContext` anahtarıyla
kapatılabiliyor. Runly'de de ızgara renkleri tek yerde toplanmalı (`Palette.cs`), hücre
çizen kodun içine dağılmamalı. PR'ın kapsam listesi ayrıca kontrol listesi işlevi görüyor:
checkbox hücresi, combobox hücresi, **sıralama gliflerini** taşıyan sütun başlığı ve
link hücresi — koyu temada ayrı ayrı ele alınması gereken dört yer.

**Kaçınılacak hata:** deneysel API'ye yerleşim kurmak. WFO5001 iki büyük sürümdür
deneysel; `SetColorMode` üzerine yaslanan bir tasarım .NET 11'de imza değişirse taşınmak
zorunda kalır.

### ysc3839/win32-darkmode — MIT, C++, referans
Belgelenmemiş dark mode ordinal'lerinin kanonik kaynağı. `DarkMode.h` tek dosya.

**Runly'ye alınacak fikir:** üç savunma. (a) `ShouldAppsUseDarkMode()` **ve**
`IsHighContrast()` birlikte kontrol ediliyor — yüksek kontrast açıkken koyu mod
zorlanmıyor. (b) `WM_SETTINGCHANGE` + `"ImmersiveColorSet"` ile tema değişimi yakalanıyor.
(c) Ordinal'ler tek tek `GetProcAddress` ile alınıp **hepsi bulunduysa** `g_darkModeSupported`
açılıyor; kısmi başarı diye bir durum yok.

**Kaçınılacak hata (iki tane):**
1. `CheckBuildNumber` yalnız 17763 / 18362 / 18363 / 19041'e izin veriyor. Depo
   28.03.2023'ten beri güncellenmemiş; bu liste Windows 11'de (22621, 26100) **hiç
   eşleşmez** ve koyu mod tamamen kapanır. Sürüm allowlist'i yerine `>= 17763` eşiği.
2. Ordinal 135'in iki farklı imzası: build < 18362'de `AllowDarkModeForApp(bool)`,
   sonrasında `SetPreferredAppMode(PreferredAppMode)`. Runly `NeonControls.cs:29`'da
   koşulsuz `SetPreferredAppMode(2)` çağırıyor. Win10 1809'da bu `AllowDarkModeForApp(true)`
   olarak yorumlanır — zararsız görünüyor ama kasıtlı değil; hangi Windows'tan itibaren
   destekleneceği yazılı bir karar olmalı.

### BlueMystical/Dark-Mode-Forms — MIT, WinForms, en yakın komşu
Bir Form'un tüm kontrollerini gezip koyu moda çeviren tek sınıf (`DarkModeCS.cs`) +
temalanabilir MessageBox/InputBox (`Messenger.cs`).

**Runly'ye alınacak fikir:** sızıntı kontrol listesi olarak kullanılmaya değer. Kontrol
türü başına ayrı dal açıyor: `NumericUpDown`, `ComboBox`, `Panel`, `TableLayoutPanel`,
`TabControl` (→ `TabDrawMode.OwnerDrawFixed`), `ToolStripPanel`, `ListView` (→ `OwnerDraw`
+ header handle'ı ayrıca alınıyor), `DataGridView`, `FlowLayoutPanel`. Kullandığı
`SetWindowTheme` alt uygulama adları da burada: `DarkMode_Explorer`, `DarkMode_CFD`,
`DarkMode_ItemsView`. Runly şu an yalnızca `DarkMode_Explorer` kullanıyor
(`NeonControls.cs:69`) — ComboBox ve metin kutuları için `DarkMode_CFD` denenmeli.

**Kaçınılacak hata:** DataGridView'ün scrollbar'larına
`typeof(DataGridView).GetProperty("HorizontalScrollBar", NonPublic)` reflection'ıyla
ulaşıyor. Çalışır ama private üye adına bağlı ve derleyici uyarısı vermeden .NET
yükseltmesinde kırılır. Runly ızgarayı zaten kendi çiziyor; buraya gitmeye gerek yok,
çözüm gerekiyorsa `ScrollBars = None` + kendi çizilmiş kaydırıcı daha dürüst.

**Şüpheli yan:** 188 yıldızlı, tek geliştiricili, son push 21.07.2025 — bağımlılık olarak
almak yerine okunacak kaynak.

### File-New-Project/EarTrumpet — lisansı temiz değil, WPF
Windows 11 görsel diline en yakın duran üçüncü parti ses karıştırıcı.

**Runly'ye alınacak fikir:** `IconHelper.LoadIconResource` — ikonu DPI'ya göre hesaplanan
`cx/cy` ile ikon grubundan seçmek (§4). Ayrıca `ImmersiveSystemColors.cs`: sistem
vurgu renkleri sabit yazılmıyor, işletim sisteminden okunuyor. Runly'nin neon paleti
kendi rengi olsa da vurgu/seçim renginde sistemle uyum bir seçenek.

**İkinci fikir:** `AudioPolicyConfigFactory` + `...ImplFor21H2` / `...ImplForDownlevel`
ikilisi — Windows sürümüne göre değişen native davranış, arayüz + fabrika arkasına
saklanmış. Runly'nin `SetPreferredAppMode` / `DwmSetWindowAttribute` sürüm farkları için
aynı kalıp kullanılabilir: sürüm kontrolü tek yerde, çağrı yerinde `if` yok.

**Kaçınılacak hata / risk:** LICENSE dosyası MIT metnine üç şirketi adıyla dışlayan bir
madde eklemiş ("Excluded Entities ... may not exercise any of the rights"). Bu, OSI
onaylı MIT **değildir**; GitHub da lisansı `NOASSERTION` olarak raporluyor. Koddan tek
satır alınmamalı. Ayrıca GitHub'daki son sürüm etiketi 2016 — dağıtım Store üzerinden
yapıldığı için etiket tarihi terk edilmişlik göstergesi değil (son push 16.08.2026).

### files-community/Files — MIT, WinUI 3, büyük liste
Modern dosya yöneticisi; on binlerce öğelik klasörleri sanallaştırılmış listede gösteriyor.

**Runly'ye alınacak fikir:** `FileThumbnailHelper.GetIconAsync` — istenen boyut pencere
DPI'sıyla **çarpılıyor**, `Math.Max(1, size)` ile sıfır boyut korunuyor, çağrı `STATask.Run`
ile STA iş parçacığına atılıyor ve `IconOptions.ReturnOnlyIfCached` bayrağı ilk boyamayı
beklemeden yapmayı mümkün kılıyor. Runly'nin uygulama seçme diyaloğu bu üç davranışın
üçünü de kaçırıyor.

**Kaçınılacak hata:** WinUI 3 / Windows App SDK bağımlılık yüzeyi Runly'nin problemine
göre çok büyük. Files'ın sürüm hızı (v4.2.9, 19.08.2026) ve 460 açık işi, bu yüzeyin
sürekli bakım istediğini gösteriyor. Alınacak olan desen, teknoloji değil.

### mRemoteNG/mRemoteNG — GPL-2.0, WinForms, koyu tema motoru
Uzak masaüstü yöneticisi; WinForms üstünde çalışma zamanında yüklenen tema motoru var.

**Kaçınılacak hata (bu depo esas olarak bir uyarı):** `mRemoteNG/Themes` klasöründe
`darcula.vstheme` **416 KB**, `vs2015dark.vstheme` 311 KB, yanında 39 KB'lık
`ColorMapTheme.Designer.cs`, bir `ThemeSerializer` ve bir `ThemeManager` (15,8 KB). Tek bir
koyu tema için Visual Studio tema dosyası biçimini ayrıştıran bir motor kurulmuş. Runly tek
palet kullanıyor — `Palette.cs` içinde sabit token'lar doğru karar, tema dosyası formatına
geçmek net kayıp.

**Sağlık işareti:** son etiketli kararlı sürüm v1.76.20, **12.04.2019**; 864 açık iş; depo
hâlâ aktif (son push 21.08.2026). Yani geliştirme sürüyor ama sürüm çıkarma durmuş.

### dahall/Vanara — MIT, P/Invoke sarmalayıcıları
Windows API'lerinin en geniş .NET sarmalayıcı seti; `PInvoke/UxTheme`, `PInvoke/DwmApi`,
`PInvoke/Shell32`, `PInvoke/ComCtl32` ve ayrıca `Windows.Forms` yardımcı paketi.

**Bulgu (Runly için önemli):** Vanara **belgelenmemiş dark mode ordinal'lerini
sarmalamıyor**. `PInvoke/UxTheme/UXTHEME.cs` (166 KB) içinde `SetPreferredAppMode`,
`AllowDarkModeForWindow`, `ShouldAppsUseDarkMode` geçen **sıfır** satır var. Yani "Vanara'yı
alırsak elle yazdığımız native katmanı silebiliriz" beklentisi karşılanmıyor; koyu modun
kırılgan kısmı yine elde kalır.

**Runly'ye alınacak fikir:** `Windows.Forms/DesktopWindowManager.cs` — DWM çağrıları
`DWMWINDOWATTRIBUTE` enum'u üstünde tek bir `SetWindowAttribute` yardımcısına indirgenmiş,
`Cloak`, `DisableTransitions`, `AllowNonClientPainting` gibi okunur uzantılar olarak
sunulmuş ve HRESULT `ThrowIfFailed()` ile denetleniyor. Runly'nin `DwmSetWindowAttribute`
çağrısı dönüş değerini hiç okumuyor. Sarmalama fikri alınmalı, paketin kendisi şart değil.

**Şüpheli yan:** paket sayısı yüzlerce; yanlış paketi alıp bağımlılık yüzeyini şişirmek
kolay. Yalnızca gerekli olan (`Vanara.PInvoke.UxTheme` gibi) alınmalı. Depo sağlıklı:
7 açık iş, v5.0.7 (15.08.2026).

### Tyrrrz/LightBulb — MIT, Avalonia (WPF değil)
Ekran gama ayarlayıcı. **Aday listesinde "WPF" diye geçiyordu; artık değil** — `csproj`
`Avalonia`, `Avalonia.Desktop`, `Material.Avalonia`, `DialogHost.Avalonia` referansları
içeriyor. Runly'nin teknolojisine uzak, bu yüzden dar bir katkı sunuyor.

**Runly'ye alınacak fikir:** uygulama kendi tema motorunu **hiç yazmıyor**;
`Material.Avalonia` + `Material.Icons.Avalonia` hazır alınıyor, ayar kalıcılığı `Cogwheel`,
güncelleme `Onova` gibi tek işlik küçük kütüphanelere devredilmiş. WinForms'ta böyle bir
tema kütüphanesi olmadığı için Runly elle çizmek zorunda — ama **ayar kalıcılığı ve
güncelleme** için aynı ayrıştırma yapılabilir.

**Kaçınılacak hata:** yok denecek kadar az örtüşme; bu depoyu koyu tema referansı olarak
kullanmak yanıltıcı olur.

---

## Runly için sonuç

1. **.NET 8'de kal, temayı silme.** `SetColorMode` iki sürümdür `Experimental`, Windows 11
   şartı var, DataGridView koyu paleti (`#14267`) hâlâ birleştirilmemiş ve MessageBox
   (`#11896`) koyu değil. Yükseltme kararı destek süresiyle verilir, tema borcuyla değil.
2. **Arama kutusuna debounce + iptal koy.** `MainForm.cs:262` ve
   `ChooseApplicationDialog.cs:96` her tuşta tüm ızgarayı yeniden kuruyor. PowerToys deseni:
   ~150 ms gecikme, `CancellationTokenSource` ile önceki aramayı iptal, eşleştirmeyi UI
   dışında yap. Satır başına önceden hesaplanmış küçük harfli arama anahtarı tut.
3. **Simge yolunu değiştir.** `Icon.ExtractAssociatedIcon` (`ChooseApplicationDialog.cs:301`)
   sabit 32 piksel veriyor; %150'de bulanık. `IShellItemImageFactory.GetImage`'a geç,
   boyutu `DeviceDpi` ile çarparak **fiziksel piksel** olarak iste, çağrıyı UI iş parçacığı
   dışına al. Kayıt defterinden gelen `"yol,index"` biçimi için `PathParseIconLocationW`.
4. **İkon önbelleği ekle.** PowerToys `ImageCache` ölçüsü: kapak 50, temizliği ancak
   2× kapağı aşınca yap (her eklemede değil), anahtar tam yol. Diske yazmak Runly için
   şart değil; süreç ömrü yeterli.
5. **Ordinal 135 ve DWM attribute'ünü sürüm bilinçli yap.** Windows derleme numarasını
   tek bir yerde oku; 18362 altında ordinal 135'in imzası farklı, 19041 altında DWM
   attribute'ü 19. `DwmSetWindowAttribute`'ün HRESULT'ını yut ama **logla** — bugün sessizce
   başarısız oluyor (`NeonControls.cs:19`).
6. **`SetWindowTheme`'i alt uygulama adına göre ayır.** Bugün her kontrole
   `DarkMode_Explorer` veriliyor (`NeonControls.cs:69`). ComboBox/TextBox ailesi için
   `DarkMode_CFD`, liste öğeleri için `DarkMode_ItemsView` denenmeli; ComboBox'ın açılır
   listesi ayrı bir pencere olduğu için tek başına yetmezse owner-draw'a düşülmeli.
7. **Owner-draw ölçülerini DPI'dan türet.** `#6382` (owner-draw `ListBox.ItemHeight`
   ölçeklenmiyor, 2021'den beri açık) Runly'yi doğrudan vuruyor. Satır yüksekliği, ikon
   kutusu ve padding `DeviceDpi`'dan hesaplanmalı ve `OnDpiChangedAfterParent`'ta yeniden
   hesaplanmalı; açılışta bir kez alınan tek bir ölçek katsayısı (ContextMenuManager'ın
   hatası) `PerMonitorV2`'de yanlıştır.
8. **Yüksek kontrast ve tema değişimini ele al.** ysc3839'un iki savunması bedava:
   `SystemParametersInfo(SPI_GETHIGHCONTRAST)` açıkken koyu modu zorlamamak ve
   `WM_SETTINGCHANGE`/`"ImmersiveColorSet"` geldiğinde temayı tazelemek. PowerToys'un WMI
   izleyicisine gerek yok.

## Kaynaklar

- `gh api repos/<owner>/<repo>` ve `repos/<owner>/<repo>/releases/latest`, 22.08.2026 —
  künye tablosundaki tüm sayılar.
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode
  (sayfa güncelleme tarihi 13.08.2026) — `Experimental("WFO5001")`, Windows 11 şartı,
  yüksek kontrast ve otomatik uyum sınırları.
- `dotnet/winforms` → `docs/list-of-diagnostics.md` (WFO5001/5002/5003 satırları).
- `dotnet/winforms` konu/PR aramaları: `#14267`, `#14266`, `#11896`, `#12420`, `#14107`,
  `#14578`, `#6382`, `#9293`, `#10402`, `#13194`, `#14784`.
- `ysc3839/win32-darkmode` → `win32-darkmode/DarkMode.h`.
- `microsoft/PowerToys` → `src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml.cs`,
  `src/modules/launcher/Wox.Infrastructure/Image/{ImageLoader,ImageCache,WindowsThumbnailProvider}.cs`,
  `src/common/ManagedCommon/{ThemeListener,ThemeHelpers}.cs`.
- `BluePointLilac/ContextMenuManager` → `BluePointLilac.Methods/{HighDpi,MessageBoxEx}.cs`,
  `BluePointLilac.Controls/` dizin listesi.
- `File-New-Project/EarTrumpet` → `EarTrumpet/Interop/Helpers/IconHelper.cs`, `LICENSE`.
- `files-community/Files` → `src/Files.App/Utils/Storage/Helpers/FileThumbnailHelper.cs`.
- `BlueMystical/Dark-Mode-Forms` → `README.md`, `src/DarkModeForms/DarkModeCS.cs`.
- `mRemoteNG/mRemoteNG` → `mRemoteNG/Themes/` dizin listesi ve dosya boyutları.
- `dahall/Vanara` → `PInvoke/UxTheme/UXTHEME.cs` (arama sonucu: 0 eşleşme),
  `Windows.Forms/DesktopWindowManager.cs`.
- `Tyrrrz/LightBulb` → `LightBulb/LightBulb.csproj`.
- Runly kaynakları (yalnızca okundu): `src/Runly.Settings/NeonControls.cs`,
  `src/Runly.Settings/MainForm.cs`, `src/Runly.Settings/Dialogs/ChooseApplicationDialog.cs`,
  `src/Runly.Settings/Runly.Settings.csproj`.
