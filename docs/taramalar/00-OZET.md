# Runly ön araştırma — özet

22.08.2026. Sekiz scout ajanı, **78 depo/kaynak**. Ayrıntılar `01`–`08` dosyalarında.

Bu dosya yalnız kararı taşır: neyi yapacağız, neyi yapmayacağız, hangi iddia doğrulandı.
Ajan raporundaki her iddia burada tekrar edilmedi — **kodda doğruladıklarım** işaretli.

---

## 1. Şimdi düzeltilecek — kodda doğrulandı

| # | Bulgu | Kanıt | Kaynak |
|---|---|---|---|
| A1 | `.hta`, `.vbs`, `.wsf`, `.jar` katalogda `Run` + `blocked:false` + `riskNote` yok. `.bat`/`.cmd` ise engelli. HTA tam güvenle çalışır, `.bat`'tan tehlikelidir. | `catalog.json` sorgulandı | `08` |
| A2 | `SHOpenWithDialog` varsayılanı değiştiremez — bu Windows **10**'dan beri belgeli, Win11'e özgü değil. `OAIF_ALLOW_REGISTRATION` ve `OAIF_FORCE_REGISTRATION` yok sayılıyor. Koddaki K23 yorumu kapsamı yanlış anlatıyor. | Microsoft `OPENASINFO` belgesi | `02` |
| A3 | Sıfır bayt Store-takozu sezgisi yetersiz; kodun kendi yorumu itiraf ediyor: "çalışan bir alias (`py.exe`) bayt bayt aynı". Doğrusu reparse point'ten `APPEXECLINK` okumak. | `PathSearcher.cs:103-125` | `03` |
| A4 | Arama kutusunda debounce yok, her tuşta tam yenileme. 400+ satırda hissedilir. | `MainForm.cs`, `ChooseApplicationDialog.cs` | `06` |
| A5 | `DwmSetWindowAttribute` dönüş değeri okunmuyor; 19041 öncesi Win10'da attribute 20 yerine 19 idi, sessizce başarısız oluyor. | `NeonControls.cs:19` | `06` |
| A6 | `Runly.Launcher` tek `OutputType=Exe`. Konsol/GUI ayrımı yok — `Kind=Open` eşlemelerinde konsol penceresi yanıp sönüyor. | `Runly.Launcher.csproj:4` | `03` |
| A7 | `.github` yok, CI hiç kurulmamış. `install.ps1` indirdiği zip'in SHA-256'sını doğrulamıyor; `build.ps1` hash'i hesaplayıp yalnız ekrana basıyor. | dosya sistemi + `scripts/*.ps1` | `07` |

**Bu turda kapatılanlar:** kurulum artık `Runly.exe` yanında yoksa reddediyor (bugünkü ölü yol
hatası); uygulama seçme listesi Runly'nin kendisini eliyor (özyineleme).

---

## 2. Değerlendirilecek — karar bekliyor

- **`IAssocHandler` / `SHAssocEnumHandlers`** — Windows'un kendi "birlikte aç" listesi.
  `GetUIName`, `GetIconLocation`, `IsRecommended` hazır geliyor. Mevcut `ApplicationFinder`'ın
  **yerine değil yanına** konur; `NoOpenWith` işaretli uygulamalar elenmeli. (`02`)

- **Simge kalitesi** — `Icon.ExtractAssociatedIcon` sabit 32px veriyor, %150 ölçekte bulanık.
  `IShellItemImageFactory` DPI'ya göre doğru kareyi seçiyor. Ayrıca `ExtractAssociatedIcon`
  `"yol,indeks"` biçimini çözemiyor. (`02`, `06`)

- **Güven listesi modeli** — VS Code Workspace Trust deseni: `yol + trusted + kapsam`, en uzun
  eşleşen yol kazanır, `trusted:false` kayıtları da tutulur. Runly'nin junction açığı burada
  kapanır. Okunamayan MOTW "temiz" değil **"bilinmiyor"** sayılmalı. (`04`)

- **Kurulum kaydına kendi kimliğini yazmak** — winget portable deseni: kayda hedef yol + SHA-256
  yaz, her yıkıcı işlemden önce "hâlâ benim mi" diye doğrula. Bugünkü ölü yol hatasını hem önler
  hem tespit eder. (`05`)

- **Onay diyaloğu sertleştirme** — Deno'nun üç önlemi: prompt öncesi stdin'i boşalt, yolu kontrol
  karakterlerinden arındır, etkileşimsiz ortamda doğrudan reddet. (`04`)

- **Dağıtım** — Scoop Extras'a girmek en ucuz gerçekçi yol. Release'e `.sha256` koy,
  `install.ps1` doğrulasın. (`07`)

---

## 3. Yapılmayacak — gerekçesiyle

- **UserChoice hash'ini hesaplayıp yazmak.** Teknik olarak mümkün, ama Windows'un kasten
  koruduğu bir anahtarı taklit etmek demek. Runly'nin dürüstlük duruşuyla çelişir ve her Windows
  güncellemesinde kırılır. Kullanıcıyı Ayarlar'a göndermeye devam. (`01`)

- **`shared-mime-info` çeviri havuzunu almak.** 78 dil, Türkçe ~1008 kayıt, çok cazip — ama
  **GPL-2.0**, Runly MIT. Alınamaz. `mime-db` MIT ve 1239 uzantı taşıyor ama görünen ad ve
  kategori taşımıyor. (`08`)

- **EarTrumpet'ten kod almak.** Lisansı OSI-MIT değil, üç şirketi dışlayan madde var,
  GitHub `NOASSERTION` diyor. Fikir alınır, kod alınmaz. (`06`)

- **.NET 9/10 yerleşik dark mode'a geçmek.** `Application.SetColorMode` hâlâ
  `[Experimental("WFO5001")]`; DataGridView koyu paleti PR açık, MessageBox koyu değil. Runly'nin
  en çok emek verdiği iki katman framework'ün bitmemiş kısmı. .NET 8'de kalınır. (`06`)

- **Azure Artifact Signing.** Public Trust sertifikası Türkiye'ye verilmiyor — Microsoft'un
  önkoşul listesi bireysel geliştirici için ABD/Kanada, kuruluş için 12 ülke sayıyor. Fiyattan
  bağımsız olarak bu yol kapalı. (`07`)

- **Windows 11 ön bağlam menüsü (`IExplorerCommand`).** Ayrı C++ DLL + imzalı sparse package
  gerekiyor; sparse kaydın `ExternalLocation` DACL'i AppContainer SID'leriyle kirletmesi
  PowerToys'da önizleme işleyicilerini bozmuş. Runly'nin kazancına değmez. (`02`)

---

## 4. Yanlış çıkan iddia

- **"Release zip'leri git'e girmiş."** Tutmuyor. `.gitignore` içinde `*.zip` var,
  `git ls-files` hiçbir zip döndürmüyor, `.git` klasörü 2 MB. Zip'ler yalnız diskte duruyor.

---

## 5. Ajanların bildirdiği sapmalar

- `DanysysTeam/SetUserFTA` GitHub'da yok (kapalı kaynak); yerine `PS-SFTA` ve `SFTA` incelendi.
- `AveYo/fox` dosya ilişkilendirmeyle ilgisiz; yerine `sumatrapdf` ve `ImageGlass` kondu.
- `nilesoft/shell` → doğrusu `moudey/Shell`; `MortenChristiansen/OpenWithPlusPlus` → `stax76/…`;
  `sylveon/windows-context-menu-tools` hiç yok.
- Apple UTI belgeleri istemci tarafında derlendiği için gövde çekilemedi — `08` içinde
  `doğrulanamadı` işaretli.
- ScoopInstaller/Shim benchmark'ında C#'ın native'lerden hızlı görünmesinin nedeni depoda
  açıklanmamış — `03` içinde `doğrulanamadı` işaretli.
