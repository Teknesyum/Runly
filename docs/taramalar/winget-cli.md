# microsoft/winget-cli

## 1. Künye

- Depo: `microsoft/winget-cli`
- Lisans: **MIT** (GitHub API `MIT` döndürdü; ayrıca `NOTICE` dosyası üçüncü taraf bileşenler için, `cgmanifest.json` bağımlılık envanteri). Marka ayrı: "Windows Package Manager" ve WinGet adı Microsoft markası, MIT lisansı marka hakkı vermiyor.
- Yıldız: 26.334 · Açık issue: **1.303**
- Son commit: 2026-08-22 · Son etiketli sürüm: **v1.29.280, 2026-06-24**

## 2. Ne yapıyor

Windows'un resmî paket yöneticisi: CLI, PowerShell modülü ve COM API'si aynı çekirdeği kullanıyor. Paket tanımları ayrı depoda (`microsoft/winget-pkgs`) YAML manifest olarak duruyor; bu depo yalnız istemci.

## 3. Runly ile kesişimi

En yakın model **portable installer** akışı ve `doc/specs/#182` belgesi.

- **Kurulum yeri:** kullanıcı kurulumu `%LOCALAPPDATA%\Microsoft\WinGet\Packages\<PackageIdentifier>\`, makine kurulumu `Program Files\WinGet\Packages`. Ayarlarla değiştirilebilir (`PortablePackageUserRoot` / `PortablePackageMachineRoot`). Runly'nin `%LOCALAPPDATA%\Programs\Runly` seçimiyle aynı mantık.
- **İki ayrı kayıt yüzeyi, tek amaç:** spec açıkça yazıyor ki yalnız **App Paths** anahtarına yazmak yetmiyor — komut satırı App Paths'e bakmıyor — bu yüzden ayrıca `WinGet\Links` altında symlink oluşturulup o dizin PATH'e ekleniyor. Runly için ders: bir hedefe ulaşmak için tek anahtar yetmeyebilir, hangi tüketicinin hangi anahtarı okuduğu önce çıkarılmalı.
- **Kaldırma dürüstlüğü ve `VerifyExpectedState`:** 05'te var.
- **Kayıt yedeği:** yok; winget yedek almıyor, kendi yazdığını kendi kaydından doğruluyor.
- **Sürümleme:** portable paketlerde yan yana sürüm **yok** (spec'te açık karar); `UninstallPrevious` alanı varsa yükseltmeden önce eskisi kaldırılıyor, sonra yeni ARP kaydı yazılıyor.
- **Paket doğrulama:** manifest'te `InstallerSha256`; indirilen dosya hash'i tutmazsa kurulum durur.

## 4. Alınacak fikir

1. **Numaralı, issue'ya bağlı spec dosyaları.** `doc/specs/#182 - Support for installation of portable standalone apps.md` gibi: her özellik için `#<issue> - <başlık>.md`, başında `author / created on / last updated / issue id` frontmatter'ı, içinde Abstract / Solution Design / Capabilities / Reliability başlıkları. Runly'de `docs/` zaten var; kararların gerekçesini issue numarasına bağlamak sonradan "bu neden böyle" sorusunu ucuza cevaplıyor. Maliyet: özellik başına bir dosya, sıfır kod.
2. **Kurulum dizinini paket kimliğinden türetmek.** winget her paketi `Packages\<PackageIdentifier>\` altına koyuyor, çakışma imkânsız. Runly'nin yedekleri ve kurulum defteri de düz dosya adı yerine kimlik + sürüm klasörü altında durmalı.
3. **"Kalan dosya var" bildirimi + `--wait`.** Apps & Features üzerinden kaldırma yapıldığında pencere hemen kapanacağı için, kalan dosyaların kullanıcıya duyurulabilmesi adına spec ayrı bir `--wait` argümanı tanımlıyor. Runly'nin kaldırma akışında da "şu kayıtlara dokunmadım, sebebi şu" mesajının kullanıcı görene kadar ekranda kalması gerekiyor.

## 5. Kaçınılacak hata

**Issue #6215 (açık, 2026-05-07): "Portable uninstall removes symlinks belonging to a different package when binary names collide."** İki portable paket aynı dosya adını (`ffmpeg.exe`, `ffprobe.exe`) sağladığında ve biri `ArchiveBinariesDependOnPath: true` ile symlink oluşturmadığında, winget diğer paketin symlink'lerini kaldırılan pakete ait sanıyor; sahte "package has been modified" uyarısı çıkıyor ve `--force` ile **hâlâ kurulu başka bir paketin** bağlantıları siliniyor. Kök neden: sahiplik **dosya adına** göre çıkarılıyor, kayıtlı hedefe göre değil. Runly'de birebir karşılığı var — `.txt` gibi popüler bir uzantıda ProgID sahipliğini ada bakarak çıkarmak, başka uygulamanın kaydını silmek demek. Sahiplik yalnız kendi yazdığımız kayıttaki hedef yolla doğrulanmalı.

**Issue #3601 (açık): "`PATH` variable eventually becomes too long when installing many CLI apps."** Her kurulumun ortak bir ortam değişkenini şişirmesi, geri alınamayan birikimli hasar. Runly'nin `OpenWithProgids` gibi listeye ekleyen yerlerde aynı risk var: eklemek kolay, temizlemek kimsenin işi değil.

## 6. Doğrulama

- Kaynaktan okundu: `repos/microsoft/winget-cli` metadata, `releases/latest`, `commits[0]`, `contents/LICENSE` (API MIT), kök dizin listesi, `doc/` ve `doc/specs/` listesi, `doc/specs/#182` tam metni, issue #6215 gövdesi, #3601 başlığı.
- Okunmadı / `doğrulanamadı`: `src/` altındaki C++ uygulaması okunmadı; spec'te yazan davranışın koddaki güncel hâliyle birebir aynı olduğu doğrulanamadı — spec 2022-04-07 tarihli, istemci o zamandan beri 1.29'a geldi.
- 1.303 açık issue'nun portable/ilişkilendirme payı `doğrulanamadı`.
- Marka iddiası (WinGet adının Microsoft markası olması) LICENSE dosyasında yazmıyor, genel MIT+marka ayrımından çıkarıldı — `doğrulanamadı`.
