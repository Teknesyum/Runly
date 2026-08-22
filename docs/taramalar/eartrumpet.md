# File-New-Project/EarTrumpet

## 1. Künye

- Depo: `File-New-Project/EarTrumpet` · Lisans **OSI onaylı değil**: GitHub `NOASSERTION`/"Other". `LICENSE`, MIT
  metninin **önüne** dışlama maddesi koyuyor: "Excluded Entities" olarak **Yellow Elephant Productions**,
  **Tidal Media Inc.**, **Articent Group LLC**; bu kuruluşların çoğaltma, dağıtma, türev üretme
  hakkı yok. Kuruluş ayrımcılığı OSI tanımıyla çelişir, bu **MIT değildir**. Runly bu üçünden
  biri olmasa da devredilebilirlik belirsiz — **kod alınamaz**, yalnız desen okunur.
- Yıldız: 11.300 · Açık issue: 110 · Son commit: 2026-08-16 (etkin bakım) · son etiketli
  sürüm `1.3.2.0` (**2016-06-23**); dağıtım Store/Chocolatey'e taşınmış, **güncel sürüm
  numarası Releases'ten doğrulanamadı**.

## 2. Ne yapıyor

Windows ses seviyesini uygulama başına yöneten görev çubuğu uygulaması (WPF); bildirim alanı
simgesi, uçan pencere (flyout) ve tam pencere olmak üzere üç yüzü var.

## 3. Runly ile kesişimi

Farklı çerçeve (WPF), farklı alan (ses); kesişim dar ama gerçek.

- **Tema motoru:** `UI/Themes/` — `Manager.cs`, `Rule.cs`, `Brush.cs`, `BrushValueParser.cs`,
  `AcrylicBrush.cs`, `OS.cs`. Tema XAML sözlüğüne gömülü değil; **kurallardan (`Rule`) oluşan
  ayrı katman**, çalışma anında yeniden uygulanıyor — Runly'deki dağılmış renk mantığının
  olgun karşılığı.
- **Simge çekme:** `Interop/IShellItemImageFactory.cs`, `Shell32.cs`, `UI/Helpers/IAppIconSource.cs`,
  `TaskbarIconSource.cs` — simgeyi exe'den ve paketli uygulamalardan çekmenin arayüzle soyut
  hâli; Runly'nin uygulama seçme diyaloğuyla aynı iş.
- `Interop/Uxtheme.cs`, `DwmApi.cs` var ama pencere WPF; owner-draw dersi **yok**.

## 4. Alınacak fikir

1. **Temayı "kural listesi" olarak modellemek** (`UI/Themes/Rule.cs` + `Manager.cs`). Renk
   sabiti yerine "şu koşulda şu fırça"; sistem teması, yüksek karşıtlık ve vurgu tek yerden
   akıyor. Runly'de token → kural katmanı ayrımı.
2. **Simge kaynağını arayüz arkasına almak** (`IAppIconSource`). Simgenin nereden geldiği
   çağıranı ilgilendirmez, test edilebilir olur. Maliyet: bir arayüz + iki uygulayıcı.
3. **`UI/Themes/OS.cs`.** OS tema durumu (vurgu rengi, şeffaflık, yüksek karşıtlık) tek
   dosyada; okuma kodun geneline dağılmıyor.

## 5. Kaçınılacak hata

- **Belgesiz COM arayüzüne bağlanmak.** `Interop/ApplicationResolver.cs`, `IApplicationResolver`
  (CLSID `660B90C8-…`) arayüzünü kullanıyor, vtable'ın ilk üç yuvasını adsız boş metotlarla
  geçiyor. Belgelenmemiş bir arayüzün yuva sırasına bahis: sıra bir Windows sürümünde kayarsa
  çağrı yanlış işleve gider. Runly `IAssocHandler` gibi **belgeli** arayüzlerde kalmalı.
- **Etiketsiz sürüm akışı.** Depo etkin ama Releases 2016'da donmuş. Runly'de dağıtım nereye
  giderse gitsin etiket depoda kalmalı; yoksa "hangi kod hangi kurulumda" cevapsız.

## 6. Doğrulama

- Künye ve `LICENSE` tam okundu; üç dışlanan kuruluş adı doğrudan alıntı.
- `UI/Themes/` ve `Interop/` listeleri API'den; `ApplicationResolver.cs` tam okundu.
- `Manager.cs`, `Rule.cs`, `Brush.cs` içerikleri okunmadı — tema motorunun işleyişi dosya
  adlarından çıkarıldı, **doğrulanamadı**. Store sürümü/indirme sayısı **doğrulanamadı**.

## Kaynaklar

- https://github.com/File-New-Project/EarTrumpet · `LICENSE` · `Interop/ApplicationResolver.cs`
