# BlueMystical/Dark-Mode-Forms

## 1. Künye

- Depo: `BlueMystical/Dark-Mode-Forms` · Lisans: **MIT**. Marka koruması yok, kod alınabilir.
- Yıldız: 188 · Açık issue: 18 · Son commit: 2025-07-21 · son sürüm `v1.8.18` (2025-04-07),
  önceki `v1.8.17` (2025-01-23) — bir yıldır yeni etiket yok, depo arşivlenmemiş.
- Yapı: `src/DarkModeForms/DarkModeCS.cs` **tek dosya, ~78 KB** + `DarkControls/` ve örnek proje.

## 2. Ne yapıyor

Var olan bir WinForms formunu ve içindeki tüm kontrolleri çalışma anında koyu temaya çeviren
tek sınıflık kütüphane; formun kontrol ağacını gezip her tipe uygun renk ve tema uyguluyor.

## 3. Runly ile kesişimi

Runly ile **aynı sorunu ters yönden** çözüyor: Runly her kontrolü kendi çiziyor, bu kütüphane
Windows kontrollerini boyamaya çalışıyor. Karşılaştırma değeri burada.

- **Native tema iğnesi:** `SetWindowTheme(control.Handle, Mode, null)` dosyada beş ayrı yerde
  (kaydırma çubukları, `ListView`, `TreeView`, metin kutuları) — Runly'nin koyulaştırılmış
  scrollbar'ı için aynı API.
- **Pencere çerçevesi:** `DwmSetWindowAttribute` ile `DWMWA_USE_IMMERSIVE_DARK_MODE` (eski 19
  ve yeni 20 sabitlerinin ikisi de tanımlı), `..._WINDOW_CORNER_PREFERENCE`, `..._BORDER_COLOR`,
  `..._CAPTION_COLOR`.
- **Sistem rengi okuma:** `GetWindowsColorMode()`, `GetWindowsAccentColor()`,
  `GetSystemColors(int)` → `OSThemeColors`.
- **Owner-draw'a düşme:** `ListView.OwnerDraw` ve `TabControl.DrawMode = OwnerDrawFixed` —
  native temalama yetmediği yerde bu kütüphane de elle çizime düşüyor.
- **Kaçış kapısı:** `ExcludeFromProcessing(Control)`.

## 4. Alınacak fikir

1. **`ExcludeFromProcessing` gibi bir kaçış kapısı.** Genel tema uygulayıcısı ne kadar iyi
   olsun bir kontrol istisna ister; baştan API'ye koymak sonradan `if` yığmaktan ucuz.
   Maliyet: bir `HashSet<Control>` + tek kontrol.
2. **`OSThemeColors` — sistem renklerini tek nesnede toplamak.** `GetSystemColors(mode)` tüm
   paleti tek çağrıda veriyor; Runly'de palet sabit ama "sistemi izle" modu eklenirse aynı
   nesne sınırı gerekir.
3. **Eski/yeni DWM sabitini birlikte denemek.** `DWMWA_USE_IMMERSIVE_DARK_MODE` eski
   yapılarda 19, sonrasında 20; ikisini de sırayla denemek sürüm kontrolünden ucuz. İki satır.

## 5. Kaçınılacak hata

Açık issue listesi tek dosyalık "her şeyi boya" yaklaşımının sınırını gösteriyor:

- `#106` — `View = Details` olan `ListView` **açılır hata penceresi üretiyor** (çökme sınıfı).
- `#111` — koyu ana form altında açık temalı alt form; çalışma anında oluşturulan kontroller
  yine koyu geliyor. Tema kapsamı form ağacıyla değil, örnekle sınırlı olmalı.
- `#112`, `#110` — `RoundedPanels = true` iken `FlowLayoutPanel`/`TableLayoutPanel` kaydırma
  çubuğu kayboluyor, yeniden boyutlandırmada düzen bozuluyor. `SetRoundBorders`,
  `CreateRoundRectRgn` ile pencere bölgesini kırpıyor; kırpılan bölge çubuğu yiyor.
- `#105`, `#121`, `#122` — `UserControl` içi düğme kenarlığı, `FixedSingle`/`Fixed3D` panel
  kenarlığı, `ListView` sütun başlığı. Hepsi aynı desen: **kenarlık ve başlık gibi çerçevenin
  kendi çizdiği parçalar boyanamıyor.**

Runly için doğrulayıcı: 400+ satırlık tabloda native kontrolü boyamak yerine baştan owner-draw
seçmek doğru karardı. Ayrıca **78 KB tek dosya** bakım karşıtı örnek — `NeonControls.cs` aynı
yöne gitmemeli.

## 6. Doğrulama

- Künye, lisans, sürüm ve issue sayıları `gh api` ile doğrulandı.
- `DarkModeCS.cs` **imza düzeyinde** tarandı (P/Invoke bildirimleri, `public static` üyeler,
  `SetWindowTheme`/`OwnerDraw` satırları); dosya bütünüyle okunmadı.
- Issue **gövdeleri okunmadı**; `#121`/`#122` başlığa göre düzeltme PR'ı ama açık görünüyor —
  durumları **doğrulanamadı**.
- `RoundedPanels` ↔ kaydırma çubuğu kaybı arasındaki `CreateRoundRectRgn` bağlantısı imzadan
  çıkarılan **bir yorum**, koddan **doğrulanmadı**.

## Kaynaklar

- https://github.com/BlueMystical/Dark-Mode-Forms · `src/DarkModeForms/DarkModeCS.cs`
