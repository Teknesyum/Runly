# ContextMenuForWindows11

## 1. Künye

- **Depo:** `ikas-mc/ContextMenuForWindows11`
- **Lisans:** **LGPL-3.0** — LICENSE dosyası okundu ("GNU LESSER GENERAL PUBLIC LICENSE
  Version 3"), API `spdx_id: LGPL-3.0` ile uyumlu. Runly MIT olduğu için **kod alınamaz**,
  yalnız desen alınır.
- **Yıldız:** 2.865 · **Açık issue:** 20 · Aktif proje, Microsoft Store'da da yayımlanıyor.
- **Son commit:** 2026-05-29 (`main`) · **Son etiketli sürüm:** `5.8.0.0` — 2026-05-29

## 2. Ne yapıyor

Windows 11'in yeni (modern) bağlam menüsüne JSON ile tanımlanmış özel komutlar ekliyor.
C# bir ayar uygulaması + C++ `IExplorerCommand` host DLL + MSIX paketi olarak dağıtılıyor
(`ContextMenuCustom/ContextMenuCustomHost`, `ContextMenuCustomPackage/Package.appxmanifest`).

## 3. Runly ile kesişimi

Windows 11'in yeni menüsüne meşru şekilde girmenin tek yolunu gösteriyor: registry verb'ü değil,
**imzalı MSIX paketi içinde `IExplorerCommand` uygulayan bir COM sunucusu**. Runly bugün klasik
menüye ProgID verb'leri yazıyor (`ShellRegistrar.WriteVerb`); Windows 11'de bu öğeler "Diğer
seçenekleri göster" altına düşüyor. Bu depo üst menüye çıkmanın bedelini (paket + imza +
Developer Mode) fiyatlandırıyor.

İkinci kesişim `menu.schema.json`: Runly'nin yapılandırmasında henüz adı olmayan alanları
adlandırmış — `acceptExts`, `acceptFileFlag` (Ext / Regex / ExtList / All), `acceptDirectoryFlag`
(Directory/Background/Desktop/Drive bit maskesi), `acceptMultipleFilesFlag` (Each / Join),
`paramForMultipleFiles`, `pathDelimiter`, `showWindowFlag`, `iconDark`.

## 4. Alınacak fikir

1. **`menu.schema.json` — yapılandırmayı JSON Schema ile belgelemek.** Runly'nin `RunlyConfig` +
   `RunlyJsonContext` yapısı zaten kaynak üretimli; şema dosyası editörde tamamlama ve doğrulama
   veriyor, kullanıcı config'i elle düzenlediğinde hata oranını düşürüyor. Maliyet: tek dosya,
   kodda değişiklik yok.
2. **Çoklu dosya semantiğini açıkça modellemek.** `Each` (her dosya için ayrı çalıştır) vs `Join`
   (tek çalıştırma, `pathDelimiter` ile birleştir) ayrımı. Runly bugün `"%1" %*` yazıyor; çoklu
   seçimde ne olacağı tanımsız. Maliyet: orta — `ProcessLauncher` tarafında iki kollu akış.
3. **`iconDark` — koyu tema için ayrı ikon alanı.** Runly'nin neon/koyu arayüzünde menü ikonu
   aynı sorunu yaşar. Maliyet: düşük.

## 5. Kaçınılacak hata

- **Kurulumun gizli maliyeti.** `install.md`: GitHub paketi **self-signed sertifika** ile imzalı,
  kullanıcıdan **Developer Mode** açmasını, `Set-ExecutionPolicy RemoteSigned` yapıp `Install.ps1`
  çalıştırmasını, sonra ayarı geri almasını istiyor (eski sürümlerde ayrıca VC++ redist); README
  bunu "use self-signed certificate" diye tek satırda geçiyor. Runly'nin kurulum dürüstlüğü
  çizgisinin ötesinde bir maliyet — Store'a girmeden yeni menü ucuz değil.
- **İmzalı paket de risksiz değil.** #127 "The program breaks the taskbar" (açık, 2024-05-06):
  görev çubuğu tıklanamaz hâle gelmiş. Menü uzantısı Gezgin yüzeyine dokunduğu an bu hata mümkün.
- **Lisans.** LGPL-3.0 — bu depodan satır alınamaz; `menu.schema.json` alan adları fikir olarak
  alınır, dosya kopyalanmaz.

## 6. Doğrulama

- Kaynaktan okundu: künye (API), LICENSE başlığı, README, `install.md`, `menu.schema.json`
  alanları, `ContextMenuCustom*` proje listesi, açık issue listesi ve #127 gövdesi.
- **Doğrulanamadı:** Store paketinin imza/yayın süreci; wiki içeriği; `IExplorerCommand`
  kaydının appxmanifest'te nasıl yapıldığı (manifest açılmadı); indirme sayıları.
