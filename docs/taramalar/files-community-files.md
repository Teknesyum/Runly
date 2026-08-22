# files-community/Files

## 1. Künye
- Depo: `files-community/Files`
- Lisans: **MIT** (GitHub API `license.spdx_id`)
- Yıldız: 44.705 · Açık issue: 462 (PR'ler dahil, GitHub sayacı)
- Son commit: 2026-08-21 (`Fix: Fixed crash on window close #18864`)
- Son etiketli sürüm: `v4.2.9`, 2026-08-19
- Ölçüm tarihi: 2026-08-22, `gh api repos/files-community/Files`

## 2. Ne yapıyor
WinUI 3 ile yazılmış, Explorer yerine geçmeyi hedefleyen dosya yöneticisi. Sekme, çift
panel, etiketleme gibi kabuk özelliklerini kendi UI'ında yeniden kuruyor.

## 3. Runly ile kesişimi
Üç yerde kesişiyor: (a) "varsayılan dosya yöneticisi olarak ayarla" — Runly'nin varsayılan
atama problemi, sadece uzantı yerine `Folder` sınıfı üzerinden; (b) `src/Files.App.Launcher`
— kabuğun çağırdığı küçük yerel exe, Runly'nin NativeAOT başlatıcısıyla aynı rol;
(c) kurulum/kaldırma — kayıtları `.reg` dosyasıyla uygulayıp geri alma. Uzantı kataloğu ve
güvenlik kapısı tarafında kesişim yok.

## 4. Alınacak fikir
1. **Durumu config'ten değil registry'den oku.** `AdvancedViewModel.cs:259-264`
   (`DetectIsSetAsDefaultFileManager`) `HKCR\Folder\shell\open\command` değerinin içinde
   launcher exe adını arıyor; her işlemden sonra `DetectResult()` (satır 102-113) UI
   anahtarını gerçek duruma geri çekiyor. Runly'nin "atandı" rozeti aynı şekilde
   registry'den doğrulanmalı, yazma işleminin dönüş değerine güvenilmemeli.
2. **UAC iptali hata değildir.** `regedit` çağrısı `try/catch` içinde, catch gövdesi boş ve
   yorumu `// Canceled UAC` (satır 91-97); ardından durum yeniden ölçülüyor. Kullanıcı
   vazgeçtiğinde hata diyaloğu değil, eski durumun korunması.
3. **Başlatıcıyı ayrı, bağımsız derlenen bir proje tut.** `src/Files.App.Launcher/FilesLauncher.cpp`
   yalnız argümanı alıp mevcut pencereye devrediyor (`OpenInExistingShellWindow`,
   `IsLaunchedByExplorer`, `WaitForProtocolActivation`); ana uygulamanın çalışma zamanına
   bağımlı değil. Runly'nin başlatıcısı için aynı sınır.

## 5. Kaçınılacak hata
Atama işlemi `LocalAppData`'ya kopyalanan sabit bir `.reg` dosyasının `regedit.exe /s` ile
`runas` altında çalıştırılmasıyla yapılıyor (satır 91), üstelik dosyaları taşımak için
gizli bir PowerShell `Copy-Item` zinciri kuruluyor (satır 74-86). Üç sonuç: yazılan şey
kodda görünmüyor, kullanıcıya ne değiştiği gösterilemiyor, antivirüs/kurumsal politika
yüzeyi büyüyor. Runly registry yazımını kendi sürecinde ve kendi doğrulamasıyla tutmalı.

## 6. Doğrulama
- Okundu: depo metadata (gh api), `AdvancedViewModel.cs` satır 26-115 ve 259-282,
  `Files.App.Launcher` klasör listesi ve `FilesLauncher.cpp` ilk 70 satırı.
- Okunmadı / `doğrulanamadı`: `SetFilesAsDefault.reg` içeriği (Assets altında, indirilmedi),
  bu yüzden hangi anahtarların yazıldığını satır düzeyinde doğrulayamadım.
- `doğrulanamadı`: "Files, Windows tarafından varsayılan dosya yöneticisi olarak resmen
  destekleniyor" — böyle bir kaynak görmedim; kod HKCR sınıf anahtarını ele geçiriyor.
- Açık issue sayısı GitHub'ın `open_issues_count` alanıdır ve PR'leri de sayar.
