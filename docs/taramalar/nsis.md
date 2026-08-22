# NSIS-Dev/nsis

## 1. Künye

- Depo: `NSIS-Dev/nsis` — **SourceForge Subversion deposunun GitHub aynası**. README açıkça yazıyor: "This is just a mirror of https://sf.net/projects/nsis — please report issues there". Ayna, `copy-svn.yml` iş akışıyla besleniyor.
- Lisans: **zlib/libpng** (COPYING dosyasından okundu; GitHub API `NOASSERTION` döndürüyor). Kaynak kod, eklentiler, belgeler, örnekler ve grafikler zlib/libpng; **sıkıştırma modülleri ayrı lisanslı** (bzip2, LZMA). OSI onaylı, atıf zorunluluğu hafif. Telif "Copyright (C) 1999-2026 Contributors".
- Yıldız: 855 · Açık issue: **7** — ayna olduğu için bu sayı anlamsız, gerçek takip SourceForge'da.
- Son commit: 2026-07-28 (depo push'u 2026-08-22, ayna senkronu) · **GitHub'da yayımlanmış sürüm yok** (`releases/latest` 404). En yeni etiket: **`v312` (3.12), 2026-04-19**; ondan önceki `v311`, 2025-03-08. Sürümler SourceForge'da dağıtılıyor.

## 2. Ne yapıyor

Betikle Windows kurucusu üreten, çıktısı çok küçük olan derleyici. Kurulum ve kaldırma adımlarının tamamını geliştirici kendisi yazıyor; sistem hiçbir muhasebeyi otomatik tutmuyor.

## 3. Runly ile kesişimi

- **Kurulum yeri:** betiğin kararı. `SetShellVarContext current|all` ile kapsam seçiliyor ve `SHCTX` sembolü buna göre HKCU ya da HKLM'e çözülüyor; `$INSTDIR` de aynı mantıkla `$LOCALAPPDATA` ya da `$PROGRAMFILES` olabiliyor. `Include/Win` ve `x64.nsh` ile registry görünümü (`SetRegView 32|64`) seçiliyor. 05'te var.
- **Kaldırma dürüstlüğü:** `DeleteRegKey /ifempty` gibi koşullu silme kipleri — 05'te var.
- **Yol değişince:** yerleşik bir çözüm yok; `$INSTDIR` Uninstall anahtarına yazılıyor ve kaldırıcı oradan okuyor. Kullanıcı klasörü taşırsa kaldırıcı yanlış yeri siler — bu, Velopack'in ters aramayla çözdüğü problemin çözülmemiş hâli.
- **Kayıt yedeği:** yok, hiçbir düzeyde.
- **Paket doğrulama:** yok; imzalama derleme sonrası dışarıdan (signtool) yapılıyor.

## 4. Alınacak fikir

1. **`Include/Integration.nsh` → `NotifyShell_AssocChanged`.** İlişkilendirme yazıldıktan sonra kabuğa `SHChangeNotify(SHCNE_ASSOCCHANGED, ...)` gönderiliyor; NSIS bunu tek satırlık isimlendirilmiş bir sabit hâline getirmiş, böylece "yazdım ama Explorer görmedi" hatası betik yazarına kalmıyor. Runly'de karşılığı: her ilişkilendirme yazma/geri alma işleminin sonunda bu bildirimi göndermek **ve bunu tek bir yardımcıya bağlamak**, çağrı yerlerine dağıtmamak. Aynı dosyada `UnpinShortcut` var — kaldırmada görev çubuğuna sabitlenmiş kısayolun ayrıca çözülmesi gerektiğini hatırlatıyor.
2. **`Include/Memento.nsh` — kullanıcı seçimlerini registry'de hatırlama.** `MementoSection`, kullanıcının hangi bileşenleri seçtiğini registry'ye yazıyor (`MementoSectionWriteInt` / `...WriteMarker`) ve bir sonraki kurulumda okuyup varsayılan olarak geri yüklüyor. Runly'nin yükseltmede "kullanıcı hangi uzantıları seçmişti" sorusunun aynısı. Chocolatey'nin "remembered arguments"ının hafif hâli — ve NSIS'te değer **geçersiz kılınabilir** olduğu için choco'nun #2761 tuzağına düşmüyor. Maliyet: config'e bir bölüm, düşük.
3. **Tek kaynaktan simetrik kaldırıcı.** Kurulum ve kaldırma aynı betikte tanımlanıyor; derleyici kaldırıcıyı aynı kaynaktan üretip kurucunun içine gömüyor (`WriteUninstaller`). Runly'nin karşılığı: kaldırma listesinin elle yazılmış ikinci bir liste olmaması — kurulumda uygulanan bildirimsel listenin tersine çevrilmesi. Maliyet: kurulum adımlarının veri olarak ifade edilmesi; orta iş, ama sapma riskini tamamen kaldırıyor.

## 5. Kaçınılacak hata

Registry işlemlerinin başarısızlıkta yalnız error flag set edip sessizce geçmesi — 05'te var.

Bu taramada yeni: **issue #18 (açık, 2021-10-15'ten beri): "Add RequestUninstallerExecutionLevel".** NSIS'te kurucunun talep ettiği yetki seviyesi ayarlanabiliyor ama **kaldırıcı için ayrı bir seviye belirlenemiyor**; isteyen ya fork tutuyor ya "büyük bir hack" yazıyor. Beş yıldır açık. Ders: kurulum ve kaldırmanın yetki bağlamı **ayrı ayrı** ifade edilebilmeli. Runly per-user yazıyor, ama gelecekte HKLM'e uzanırsa kaldırma yolunun kendi yetki gereksinimi ayrı tanımlanmalı — kurulumdan miras alınmamalı.

**Issue #19 (açık, 2021-11-01): "NSIS Error on Windows 11"** — Windows Defender bazen kurucunun kendi exe dosyasını okumayı bloke ediyor ve kurucu "Error launching installer" verip ölüyor. Öneri: pes etmeden önce birkaç kez yeniden dene. Runly'nin `install.ps1` ve başlatıcısı için aynı sınıf risk: **antivirüs kaynaklı geçici dosya erişim hatası kalıcı hata sanılmamalı**, kısa bir yeniden deneme döngüsü gerekiyor.

## 6. Doğrulama

- Kaynaktan okundu: `repos/NSIS-Dev/nsis` metadata, `releases/latest` (404 döndü), `commits[0]`, `tags` ve her etiketin commit tarihi, `contents/COPYING`, README, kök + `Include/` dizin listeleri, `Include/Integration.nsh` ve `Include/Memento.nsh` makro/sabit adları, açık issue listesi ile #18 / #19 gövdeleri.
- Okunmadı / `doğrulanamadı`: `Source/` altındaki derleyici kodu okunmadı. `WriteUninstaller`'ın kaldırıcıyı üretme mekanizması NSIS kullanıcı kılavuzundan bilinen davranış, bu depodan doğrulanmadı — `doğrulanamadı`.
- `v312` etiketinin SourceForge'daki 3.12 sürümüyle aynı içerik olduğu `doğrulanamadı`; ayna gecikmesi olabilir.
- SourceForge'daki gerçek açık hata/istek sayısı bu taramada okunmadı — `doğrulanamadı`. GitHub'daki 7 sayısı projenin yükünü temsil etmiyor.
- `Memento.nsh`'in değer geçersiz kılma davranışı makro adlarından (`MementoSectionReadInt`/`WriteInt`) çıkarıldı, gövdesi okunmadı — `doğrulanamadı`.
