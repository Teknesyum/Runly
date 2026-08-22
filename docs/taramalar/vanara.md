# dahall/Vanara

## 1. Künye

- Depo: `dahall/Vanara` · Lisans: **MIT**, tek geliştirici (David Hall) telifli; marka iddiası
  yok. Pratikte NuGet paketi olarak referanslanır, kod kopyalanmaz.
- Yıldız: 2.090 · Açık issue: **7** (bu boyuttaki bir depo için dikkat çekecek kadar az) ·
  son commit 2026-08-17 · son sürüm `v5.0.7` (2026-08-15), önceki `v5.0.6` (2026-08-12).

## 2. Ne yapıyor

Windows yerel API'lerine .NET'ten erişim için P/Invoke bildirimleri ve nesne yönelimli
sarmalayıcılar; 100'ün üzerinde alt kütüphane, her biri ayrı NuGet paketi.

## 3. Runly ile kesişimi

- **Dosya ilişkilendirme (asıl kesişim):** `Registration/ShellAssociation.cs` — `IQueryAssociations`
  sarmalayıcısı. Statik `FileAssociations` sözlüğü sistemdeki tüm ilişkilendirmeleri veriyor;
  `FriendlyAppName`, `FriendlyDocName`, `ContentType`, `AppId`, `AppPublisher`, `Handlers`,
  `DefaultIcon` katalog satırının neredeyse tamamını karşılıyor; `DefaultIcon` zaten
  `IconLocation.TryParse` ile ayrıştırılmış geliyor.
- **Açma işleyicileri:** `PInvoke/Shell32/ShObjIdl.IAssocHandler.cs`. Kayıt yazma:
  `Registration/ShellRegistrar.cs`, `RegBasedDictionary.cs`.
- **WinForms tarafı:** `Windows.Forms/` altında `CustomDrawBase.cs`, `VisualTheme.cs`,
  `Styles.cs`, `Themed*` kontroller, `DesktopWindowManager.cs` — hepsi **uxtheme tabanlı**,
  Windows görsel stilini kullanıyor; koyu neon tema üretmiyor, elle çizim yoluna uymuyor.

## 4. Alınacak fikir

1. **`ShellAssociation` özellik kümesini şema olarak almak.** `FriendlyAppName`,
   `FriendlyDocName`, `ContentType`, `DefaultIcon`, `AppId`, `Handlers` — katalog satırında
   hangi alanların olması gerektiğinin doğrulanmış listesi. Maliyet: sıfır, yalnız hizalama.
2. **Alt kütüphane başına ayrı paket.** `Vanara.PInvoke.Shell32` alanken `Direct3D12` gelmiyor;
   Runly kütüphane çıkarırsa aynı sınır: ilişkilendirme motoru ile UI ayrı.
3. **`IconLocation` gibi küçük değer tipleri.** `"yol,indeks"` dizesini `TryParse` ile tipe
   çevirip her yerde dize taşımamak. Maliyet: ~30 satır.

## 5. Kaçınılacak hata

- **Bağımlılık yüzeyi.** `Vanara.Windows.Shell` tek başına gelmiyor: `Vanara.PInvoke.Shell32`,
  `.Ole`, `.ShlwApi`, `.User32`, `.Kernel32`, `Vanara.Core` zinciri geliyor. İhtiyaç birkaç
  imzaysa bu ağır — kendi P/Invoke'unu yazmak daha ucuz olabilir.
- **Tek bakımcı riski.** Depo neredeyse tek kişiye bağlı; 7 açık issue yalnız etkinlik değil,
  dar kullanıcı tabanı işareti de olabilir (**doğrulanamadı**).
- **`Windows.Forms/_InProgress_`** yarım kontroller içeriyor; her tip aynı olgunlukta değil.

## 6. Doğrulama

- Künye, lisans, sürüm, issue sayısı `gh api` ile doğrulandı.
- `Registration/ShellAssociation.cs` özellik listesi doğrudan okundu; `PInvoke/`,
  `Windows.Forms/`, `Windows.Shell/` klasör listeleri API'den alındı.
- **Doğrulanamadı:** paket bağımlılık zinciri (`.csproj`/nuspec okunmadı, zincir isim
  örüntüsünden çıkarıldı) ve NuGet indirme sayıları. `IApplicationAssociationRegistration`
  aramasında yalnız `readme` eşleşti — sarmalayıcı **bulunamadı**.

## Kaynaklar

- https://github.com/dahall/Vanara · `Windows.Shell.Common/Registration/ShellAssociation.cs` ·
  `PInvoke/Shell32/ShObjIdl.IAssocHandler.cs`
