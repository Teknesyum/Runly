# ScoopInstaller/Shim

## 1. Künye

| Alan | Değer (`gh api`, 2026-08-22) |
|---|---|
| Depo | `ScoopInstaller/Shim` |
| **Lisans** | **MIT** (API). Depoda ayrıca `UNLICENSE` dosyası var — çift lisans ihtimali, içerik okunmadı, `doğrulanamadı`. |
| Yıldız / açık issue | 116 / 4 |
| Son push | 2026-08-04 |
| Son etiketli sürüm | `cpp/v0.1.1` · 2026-07-20 (etiketler uygulama başına: `cpp/`, `cs/`, …) |

## 2. Ne yapıyor

Yanındaki `<ad>.shim` metin dosyasını okuyup `path`/`args`/`cwd`/`elevate` alanlarına göre hedef
süreci başlatan Scoop yardımcısı. Aynı format için dört uygulama: C# (.NET Fx 4.5), C++, Rust, Zig.

## 3. Runly ile kesişimi

**Konsol/GUI ayrımı — üçüncü yol: tek ikili, çalışma zamanında kendi PE başlığını okuma.**
İki uygulama da `GetModuleHandleW(nullptr)` ile kendi modülünü alıp DOS/PE imzasını doğruluyor ve
`OptionalHeader.Subsystem == IMAGE_SUBSYSTEM_WINDOWS_GUI` mi diye bakıyor: `cpp/shim.cpp:283-303`,
`zig/shim.zig:360-374` (ofset `base + pe_offset + 0x5C`, elle). Karar derleme zamanı define'ı
(CPython) veya fonksiyon parametresi (uv) değil, **ikilinin kendi başlığı**.

Zig uygulaması bunu ileri götürüyor: giriş noktası `wWinMainCRTStartup` (`shim.zig:826`); GUI ise
ve kullanıcı argümanı **yoksa** `FreeConsole()`, **varsa** `AttachConsole(ATTACH_PARENT_PROCESS)` (`:866-874`).

Diğer kesişimler: çıkış kodu `WaitForSingleObject` + `GetExitCodeProcess` ile aynen aktarılıyor
(`shim.cpp:681-684`); job object `KILL_ON_JOB_CLOSE | SILENT_BREAKAWAY_OK` (`:666-671`);
`SetConsoleCtrlHandler` iki spawn yolunda da (`:557`, `:617`). Boyut tablosu, `.shim` formatı: 03.

## 4. Alınacak fikir

1. **GUI ikilisi argüman varsa parent konsola bağlansın, yoksa konsolu bıraksın**
   (`zig/shim.zig:866-874`). GUI başlatıcı `cmd`'den çağrıldığında hata metnini göstermeli,
   çift tıklamada hiç pencere çakmamalı. Maliyet: iki API çağrısı.
2. **Kendi PE subsystem'ini okuma desenini yedekte tut** (`cpp/shim.cpp:283-303`, ~20 satır).
   İki ayrı ikili üretilse bile "yanlış ikili çağrıldı" hâlini tespit etmenin en ucuz yolu.
   Maliyet: 20 satır + hata hâlinde `false`'a düşme.
3. **Sözleşmeyi ortak `test/` kümesine bağla** — dört dil tek `.shim` formatına uyuyor.
   Runly'nin `"{script}" {args}` şablonu için dilden bağımsız uyum testi aynı işi görür.
   Maliyet: test altyapısı.

## 5. Kaçınılacak hata

**Ölü dal tuzağı:** hem `cpp/shim.vcxproj:112` hem `cpp/build.zig:71` yalnız `Console` subsystem
üretiyor; yani `IsGuiSubsystem()` yayımlanan ikililerde **her zaman `false`**. Dal, subsystem
baytı sonradan yamanan kopyalar için duruyor ama depoda bunu yapan kod yok — test edilmeyen yol.
Kapalı **#4** (2021): shim çocuğu her zaman bekliyordu, konsoldan `notepad` çalıştırınca kabuk
dönmüyordu. Kapalı **#2** (2020): shim'in konsol penceresi hedefinkine ek açılıyordu. Açık **#9**:
ARM64'te x64 shim emülasyonla çalışıyor — `win-arm64` RID yayımlanmazsa Runly'de aynı sonuç.
Açık **#10**: göreli ikili yolu shim'lenemiyor.

## 6. Doğrulama

Okundu: `README.md`; `cpp/shim.cpp` (687 satır, ilgili bölümler); `zig/shim.zig` (aynı bölümler);
`cpp/shim.vcxproj` + `cpp/build.zig` subsystem satırları; issue #2/#4/#9/#10; künye.
`doğrulanamadı`: `cs/shim.cs` ve `rust/` okunmadı; subsystem baytını yamayan Scoop tarafı kod
aranmadı; README boyut ve `benchmark/` gecikme rakamları depo iddiası, ölçülmedi.
