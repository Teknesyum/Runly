# microsoft/PowerToys

## 1. Künye

- Depo: `microsoft/PowerToys`
- Lisans: **MIT** (SPDX `MIT`). Marka ayrı: "PowerToys"/Microsoft logoları lisans kapsamı
  dışında, kod alınabilir ad alınamaz.
- Yıldız: 137.958 · Fork: 8.497 · Açık issue: 7.514
- Son commit: 2026-08-22 · Son kararlı sürüm: `v0.100.2` (2026-06-26) · sürekli ön sürüm
  akışı da var: `v0.101.2323.0` (2026-08-21, prerelease)

## 2. Ne yapıyor

Windows için ~30 bağımsız yardımcı programı tek kurulum ve tek ayar penceresi altında
toplayan araç kutusu; her modül ayrı süreç/DLL, ortak `runner` başlatıyor.

## 3. Runly ile kesişimi

- **Tema dinleme:** `src/common/ManagedCommon/ThemeListener.cs` tema değişimini WMI
  `RegistryValueChangeEvent` sorgusuyla dinliyor (`HKEY_USERS\<SID>\...\Personalize`,
  `AppsUseLightTheme`). Runly'nin `WM_SETTINGCHANGE` dinlemesine alternatif.
- **Başlık çubuğu koyulaştırma:** `ThemeHelpers.SetImmersiveDarkMode` → `DwmSetWindowAttribute`.
  Runly `FormBorderStyle.None` kullandığı için bu doğrudan gerekmiyor, ama sahip olunan
  diyaloglar (`MessageBox`, dosya seçici) için hâlâ geçerli.
- **WinForms hâlâ var:** `Common.UI`, `PreviewHandlerCommon`, `PowerLauncher` projeleri
  `UseWindowsForms` ile derleniyor; ayar penceresi WinUI 3. Native ikizi
  `src/common/Themes/theme_helpers.cpp` + `windows_colors.cpp`.
- Kesişmeyen: dosya ilişkilendirme yönetimi yok; `NewPlus` yalnız "Yeni" menüsü (`06`'da geçti).

## 4. Alınacak fikir

1. **Tema değişimini registry olay aboneliğiyle dinlemek.** `ThemeListener` deseni: tek
   nokta, event, `IDisposable`. Runly'de tema kaynağı tek yerde toplanır.
2. **Tema mantığını UI'dan ayrı bir `Theme.cs` + `ThemeHelpers.cs` çiftine koymak.**
   PowerToys'da renk okuma ile renk uygulama ayrı dosyada; Runly'de `NeonControls.cs`
   şu an ikisini de taşıyor.
3. **Modül başına ayrı süreç + ortak runner sınırı.** Runly küçük ama ayar penceresi ile
   ilişkilendirme motorunu ayrı ikili tutmak aynı sınırın ucuz versiyonu.

## 5. Kaçınılacak hata

- 7.514 açık issue, birçoğu tema tutarsızlığı: `#16877` kurulum sihirbazına koyu tema
  uygulanmıyor, `#31813` görev çubuğu bağlam menüsü tema duyarlı değil, `#48403` koyudan
  açığa geçişte hata günlüğü. Ders: **süreç ömrü boyunca tema değişimi** senaryosu
  sonradan eklenirse her zaman bir yer sızdırır; Runly'de baştan tek geçiş yolu olmalı.
- `ThemeListener` `System.Management` (WMI) bağımlılığı getiriyor — küçük bir uygulama için
  ağır; Runly `WM_SETTINGCHANGE` ile aynı sonucu bağımlılıksız alır.

## 6. Doğrulama

- Künye ve sürüm etiketleri: `gh api repos/.../releases` — birincil kaynak, doğrulandı.
- `ThemeListener.cs`, `ThemeHelpers.cs`, `src/common/Themes/` listesi doğrudan okundu.
- Issue başlıkları arama API'sinden alındı, gövdeleri okunmadı — **ayrıntı doğrulanamadı**.
- İndirme/kullanıcı rakamı iddiası **doğrulanamadı**, rapora alınmadı.

## Kaynaklar

- https://github.com/microsoft/PowerToys · `gh api repos/microsoft/PowerToys`
- `src/common/ManagedCommon/ThemeListener.cs`, `ThemeHelpers.cs`
