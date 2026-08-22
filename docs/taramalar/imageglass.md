# d2phap/ImageGlass

> Doğru ad `d2phap/ImageGlass` (`d2phag` yazımı hatalı). Depo mevcut, değiştirme yapılmadı.

## 1. Künye
- Depo: `d2phap/ImageGlass`
- Lisans: **GPL-3.0, yalnız "Classic" kaynak kodu**. `LICENSE`: ücretli **Pro** sürümü ayrı
  ticari şartlara tabi, resmi ikili paketler (Store dahil) o şartlarla dağıtılıyor. GitHub
  API `NOASSERTION` döndürüyor.
- Yıldız: 14.101 · Açık issue: 224 (PR'ler dahil)
- Son commit: 2026-08-21 (`msi: added pro message`)
- Son etiketli sürüm: `10.0.4.819`, 2026-08-21
- Ölçüm tarihi: 2026-08-22, `gh api repos/d2phap/ImageGlass`

## 2. Ne yapıyor
90+ görüntü biçimini açan Windows görüntüleyici; v10 ile çapraz platforma açılmış
(`source/ImageGlass.Lib/App.axaml.cs`). "Varsayılan fotoğraf görüntüleyici yap" akışı
ürünün öne çıkan özelliklerinden.

## 3. Runly ile kesişimi
Runly'ye en yakın ikinci depo ve teknoloji olarak en yakını (.NET, `Microsoft.Win32.Registry`).
`source/ImageGlass.Win32/Common/WinAPI/Win32DefaultAppApi.cs` şunları yapıyor:
`HKCU\Software\ImageGlass\Capabilities\FileAssociations`, `Software\RegisteredApplications`,
uzantı başına `ImageGlass.AssocFile.<EXT>` ProgId, `OpenWithProgids`, `UserChoice`
temizliği, `SHChangeNotify`. Yönetici gerektiren işler ayrı `igcmd.exe` sürecine
devrediliyor (`v9/igcmd/Functions.cs:71` `SetAppExtensions`).

## 4. Alınacak fikir
1. **Kapsamı kurulum yerinden türet.** `Win32DefaultAppApi.cs:94-118` (`GetScope`): exe
   `Program Files`/`Program Files (x86)` altındaysa HKLM, değilse HKCU; paketliyse her
   zaman HKCU çünkü paketli exe yükseltilmiş yeniden başlatılamıyor. Runly'nin taşınabilir
   ve kurulu dağıtımı aynı tek fonksiyonla ayrılabilir; kapsam ayrı bir ayar olmamalı.
2. **Yükseltmeyi yalnız yetki hatası geldiğinde iste, döngüyü kapat.** Satır 61-70:
   `UnauthorizedAccessException` / `SecurityException` yakalanınca yükseltilmiş yeniden
   başlatma; ama paketliyse veya süreç zaten yükseltilmişse `throw` — sessiz yeniden
   başlatma döngüsü engelleniyor. Ayrıca sanallaştırılmış Store paketinde işlem hiç
   denenmiyor, `null` dönüp "desteklenmiyor" deniyor (satır 42-43).
3. **Kaydı onarma yolu bulundur.** `RepairDefaultViewerRegistration` (satır 130-200)
   `Capabilities\FileAssociations` altından önceki uzantı listesini geri okuyup,
   `shell\open\command` içindeki exe yolu artık yoksa ProgId'leri yeniden yazıyor. Runly'nin
   exe'si güncellenip taşındığında aynı kırılma oluşur.

## 5. Kaçınılacak hata
Üç somut iz: (a) #1966 "ImageGlass causes incorrect WSL icons" (2024-07-21, açık) —
varsayılan görüntüleyici yapıldığında ilgisiz türlerin Explorer ikonları bozuluyor; geniş
ProgId/ikon kaydının yan etkisi. (b) #2181 (2025-07-23, açık) Windows 11 24H2 preview'da
"Make default" düğmesi çalışmıyor, yönetici olsa da olmasa da; yani HKCU'ya doğru yazmak
tek başına "atandı" demek için yetmiyor. (c) #1569 ve #546 (kapalı) kaldırıcının artık
registry bırakması. Runly için sonuç: yazdıktan sonra oku-doğrula, doğrulanmayan durumda
kullanıcıya "başarılı" deme; ikon kaydını yalnız seçilen uzantılarla sınırla.

## 6. Doğrulama
- Okundu: depo metadata, `LICENSE` ilk 25 satırı, `Win32DefaultAppApi.cs` (435 satır;
  33-130 aralığı satır satır, kalanı anahtar/yol düzeyinde), `v9/igcmd/Functions.cs:60-115`,
  issue #1966 ve #2181 başlık + gövde (gh api).
- `doğrulanamadı`: `igcmd.exe`'nin ana uygulamadan hangi verb ile başlatıldığı — çağıran
  taraf okunmadı. `ExplorerApi.RegisterAppAndExtensions` (v9 yolu) ile
  `Win32DefaultAppApi` (v10 yolu) arasındaki hangisinin güncel olduğu da teyit edilmedi;
  iki ağaç depoda yan yana duruyor.
- `doğrulanamadı`: "90+ biçim" iddiası depo açıklamasından alındı, listeyi saymadım.
