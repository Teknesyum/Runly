# Sözleşme — "Yorumlayıcı bulunamadı" penceresi: neon tema + Ayarlar'a derin bağlantı

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8)
**Dal:** main üzerinde çalış, worktree açma.

## Şikâyet

Kullanıcı ilişkilendirilmemiş bir dosyayı Runly ile açınca çıkan pencere **temasız** —
Windows'un kendi TaskDialog'u. İki isteği var:

1. Pencere Teknesyum neon temasına uysun.
2. "Ayarları aç" denince Ayarlar açılıp **o uzantıyı doğrudan seçili getirsin**. Uzantı
   listede yoksa listeye eklenip gösterilsin. Kullanıcı arama kutusunda uzantıyı elle
   aramak zorunda kalmasın.

## Bugünkü hâli

- `src/Runly.Launcher/LauncherHost.cs:247` `ReportMissingInterpreter` →
  `dialogs.AskOpenSettings("Yorumlayıcı bulunamadı", ...)`.
- `src/Runly.Launcher/Ui/TaskDialogInterop.cs:164` `AskOpenSettings` → `ShowSimple` →
  ham `TaskDialogIndirect`. Temasız pencerenin kaynağı burası.
- `src/Runly.Launcher/LauncherHost.cs:319` `TryStartSettings()` `RunlySettings.exe`'yi
  **argümansız** başlatıyor.
- `src/Runly.Settings/Program.cs` `Main()` argüman almıyor.
- `src/Runly.Settings/MainForm.cs:1209` `SelectExtensionRow(string)` var ama yalnız
  **o an ızgarada duran** satırlarda arıyor; kategori rayı başka kategoridiyse bulamaz.

## Yapılacaklar

### 1. Neon pencere — `src/Runly.Launcher/Ui/MissingHandlerDialog.cs` (yeni)

Launcher **NativeAOT** yayınlanıyor; WinForms/WPF kullanılamaz. Örneği yanında duruyor:
`src/Runly.Launcher/Ui/ArgumentPromptDialog.cs` — ham GDI, özel caption, hit-test, yuvarlak
köşe, `DWMWA_USE_IMMERSIVE_DARK_MODE`. **Aynı deseni ve aynı renk sabitlerini kullan**,
yeni renk veya ölçü uydurma. Ortak sabitler kopyalanacak kadar çoksa ikisinin paylaştığı
küçük bir yardımcıya çıkar, ama `ArgumentPromptDialog`'un davranışını bozma.

Pencere içeriği:

- Başlık: `Runly — Yorumlayıcı bulunamadı`
- Gövde: uzantı ve dosya adı görünsün. Bugünkü metin korunur:
  `"<uzantı>" için yorumlayıcı ayarlı değil ya da kurulu değil.`
- İki buton: **Ayarları aç** (birincil, neon mavi) ve **Vazgeç** (ikincil, mor kenar).
  `ArgumentPromptDialog`'daki buton çizimiyle aynı.
- `Esc` = Vazgeç, `Enter` = Ayarları aç. Pencere ekranın ortasında açılır.

`TaskDialogInterop.AskOpenSettings` artık bu pencereyi çağırsın; GUI yüzeyi yoksa
(konsol yüzeyi, pencere oluşturulamadı) bugünkü `Console.Error` düşüşü korunur —
kimse ekransız ortamda kilitlenmesin.

### 2. Derin bağlantı — launcher tarafı

- `TryStartSettings()` bir `string? extension` alsın ve varsa
  `RunlySettings.exe --select <.uzanti>` diye başlatsın.
- `ReportMissingInterpreter` ve `LauncherHost.cs:134`'teki diğer çağrı yeri uzantıyı geçsin.
  Uzantı boşsa (uzantısız dosya) argüman **eklenmez**.

### 3. Derin bağlantı — Ayarlar tarafı

- `Program.Main(string[] args)` yap. Yalnız `--select <değer>` tanınsın; tanınmayan argüman
  **sessizce yok sayılsın** (kısayoldan gelen çöp argüman uygulamayı düşürmesin).
- Değer normalleştirilsin: başındaki nokta yoksa eklensin, küçük harfe indirilsin,
  yalnız `[A-Za-z0-9_.-]` kabul edilsin, en fazla 24 karakter. Geçersizse yok sayılsın.
- `MainForm`'a bu uzantı taşınsın ve **`Shown` içinde**, ızgara ilk kez dolduktan sonra
  şu sıra uygulansın:
  1. Uzantı `_config.Extensions`'ta yoksa ve `IsBlocked` değilse:
     `MainForm.cs:1533` `OnAddExtensionClicked`'in yazdığı kaydın aynısını ekle —
     `Category = "special"`, `Interpreter`/`Args` boş, ancak **`Enabled = false`**
     (kullanıcı henüz uygulama seçmedi, açık göstermek yalan olur). `MarkDirty()`,
     `RefreshCategoryRail()`, `RefreshExtensionGrid()`.
  2. Uzantı engelliyse (`IsBlocked`) ekleme; ızgarada zaten görünüyorsa yalnız seç,
     görünmüyorsa durum şeridine engelli olduğunu yazan mevcut metni göster.
  3. Kategori rayını uzantının kategorisine geçir, aramayı temizle, sonra
     `SelectExtensionRow(extension)`. Satır **görünür** olsun (`FirstDisplayedScrollingRowIndex`).
  4. Durum şeridine tek satır bilgi: uzantı seçildi / listeye eklendi. Metin
     `locale/tr.json` ve `en.json`'a yeni anahtarla girsin, koda gömme.
- `SelectExtensionRow` bugünkü hâliyle yalnız görünen satırlara bakıyor. Kategori geçişi
  ve arama temizliği **çağırmadan önce** yapılırsa yeterli; metodun kendisini değiştirmen
  gerekmiyor, ama satır yine bulunamıyorsa bunu sessiz geçme, günlüğe yaz.

## Kabul kriterleri

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı** (`TreatWarningsAsErrors` açık).
2. `dotnet test Runly.sln -c Debug --no-build` → mevcut testlerin **hepsi** geçer.
3. `dotnet format --verify-no-changes` → temiz.
4. Launcher AOT yayını bozulmadı: `dotnet publish src/Runly.Launcher.Gui -c Release`
   uyarısız tamamlanır. Yeni pencere trim/AOT uyarısı üretmemeli.
5. **Canlı ölçüm — pencere.** Yeni GUI launcher'ı ilişkilendirilmemiş bir uzantıyla
   çalıştır (örnek dosya `%TEMP%` altında üret, örn. `deneme.zzq`), pencere açılınca
   `GetWindowLong(hwnd, -16)` oku ve raporda **style değerini** yaz: `WS_CAPTION` çıkmamalı.
   Ekran görüntüsü al (`PrintWindow`) ve `docs/reports/` altına koy.
6. **Canlı ölçüm — derin bağlantı.** `RunlySettings.exe --select .zzq` başlat, 6 sn bekle,
   UI Automation ya da ekran görüntüsüyle `.zzq` satırının **seçili** olduğunu göster.
   Sonra `%APPDATA%\Runly` yapılandırmasını **eski hâline getir** — makinede çöp bırakma.
7. `--select` çözümleyicisi için `tests/Runly.Core.Tests` altına birim testi yaz:
   nokta ekleme, küçültme, geçersiz karakter, uzunluk sınırı, argümansız çağrı.
   Çözümleyici test edilebilir bir yerde dursun (Core ya da paylaşılan sınıf), `Main`
   içine gömülü kalmasın.

## Kurallar

- **Kod yorumu yazma.** Bu depodaki mevcut yorumlar bir kısıtı anlatıyor; sen de ancak
  öyle bir kısıt varsa yaz, "ne yaptığını" anlatan satır ekleme.
- Renk, ölçü, yazı tipi uydurma — `Palette` / `ArgumentPromptDialog` sabitleri geçerli.
- Kullanıcıya görünen metin `locale/*.json` üzerinden; launcher tarafı bugünkü gibi
  gömülü Türkçe kalabilir (launcher'ın locale altyapısı yok), ama Ayarlar tarafı locale'e girer.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, kabul kriterlerinin ölçüm çıktıları
(5. ve 6. maddede gerçek değerler), takıldığın nokta varsa.
