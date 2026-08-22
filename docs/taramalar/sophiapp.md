# SophiApp

## 1. Künye

- **Depo:** `Sophia-Community/SophiApp`
- **Lisans:** MIT — LICENSE dosyası okundu (`MIT License · Copyright (c) 2023 Sophia Community`), GitHub API `spdx_id: MIT` ile uyumlu.
- **Yıldız:** 5.149 · **Açık issue:** 7
- **Son commit:** 2026-08-14 (varsayılan dal `dev-SophiApp2`)
- **Son etiketli sürüm:** `1.0.97` — 2023-07-27. Üç yıldır etiketsiz geliştirme.

## 2. Ne yapıyor

Windows 10/11 için ayar/tweak arayüzü; Sophia Script PowerShell projesinin WinUI + MSIX hâli.
Yüzlerce sistem ayarını registry, AppX paketleri ve grup ilkesi üzerinden okuyup yazıyor.

## 3. Runly ile kesişimi

Kesişim dar ama tam yerinden: `src/SophiApp/Customizations/Accessors.cs` içindeki
`CABInstallContext()` önce `Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.cab\UserChoice`
altındaki `ProgId` değerini okuyor; değer `CABFolder` değilse ve UserChoice doluysa
`InvalidOperationException("A third-party archiver is set as the default archiver")` atıp
ayarı hiç göstermiyor. Runly'nin `UserChoiceInspector` + `IEffectiveHandlerQuery` ikilisi
aynı soruyu soruyor: yabancı bir varsayılan varsa üstüne yazma, durumu "bilinmiyor" işaretle.

İkinci kesişim: `Shell Extensions\Blocked` altındaki CLSID okuması — bağlam menüsü öğesini
silmek yerine engelleyerek gizleme yolu (`EditWithClipchampContext`).

## 4. Alınacak fikir

1. **Accessor/Mutator ayrımı.** Okuma (`Accessors.cs`, 71 KB) ile yazma (`Mutators.cs`, 116 KB)
   ayrı dosyalarda, aynı ayarın iki yönü isim eşliğiyle bağlı. Runly'de `ShellRegistrar.cs`
   hem sorguluyor hem yazıyor; okuma yolunu `IEffectiveHandlerQuery` tarafına toplamak
   kaldırma/onarım akışını sadeleştirir.
2. **"Karar veremedim" üçüncü durumu.** Yabancı varsayılan görülünce `false` dönmek yerine
   istisna atmak, arayüzü açık/kapalı ikiliğinden çıkarıyor. Runly'nin uzantı ızgarası
   (`CatalogGridProjection`) için doğrudan uygulanabilir.
3. **CLSID engelleme.** Bağlam menüsü öğesini kaydı silmeden `Shell Extensions\Blocked` ile
   susturmak — geri alınabilir, HKCU'da kalır.

## 5. Kaçınılacak hata

- **Etiket ile dal ayrışması.** Varsayılan dal `dev-SophiApp2`, son etiket 2023'ten. GitHub'ın
  "latest release" bağlantısına giden kullanıcı üç yıl eski SophiApp 1'i indiriyor. Runly
  yayınlarken etiket ile varsayılan dalın uyuşmasını korumalı.
- **PowerShell'e kaçış.** `Services/PowerShellService.cs` var; ayar mantığının bir kısmı GUI'den
  script'e devrediliyor. Runly NativeAOT ve script çalıştırmayı güvenlik kapısının arkasına
  koyuyor — kendi ayarlarını uygulamak için PowerShell çağırmak o duruşu bozar.

## 6. Doğrulama

- Kaynaktan okundu: repo künyesi (GitHub API), LICENSE, `src/SophiApp` klasör yapısı,
  `Customizations/Accessors.cs` içindeki UserChoice ve Blocked okumaları, `Services/` listesi.
- Okunmadı / **doğrulanamadı:** `Mutators.cs` içeriği (116 KB, açılmadı — yazma tarafının
  UserChoice'a dokunup dokunmadığı bilinmiyor); README rozetlerindeki indirme sayıları;
  MSIX imzalama akışı; SophiApp 2'nin yayın tarihi.
