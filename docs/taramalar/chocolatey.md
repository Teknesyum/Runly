# chocolatey/choco

## 1. Künye

- Depo: `chocolatey/choco` (Chocolatey CLI)
- Lisans: **Apache-2.0** (LICENSE dosyasından okundu; GitHub API `NOASSERTION` döndürüyor). OSI onaylı. **Marka ayrı korunuyor:** NOTICE dosyası telif sahibi olarak Chocolatey Software, Inc.'i gösteriyor ve ek hukuki bilgi için `docs/legal/CREDITS.md` dosyasına yönlendiriyor; "Chocolatey" adı ve logosu Apache lisansının kapsamında değil.
- Yıldız: 11.490 · Açık issue: 514
- Son commit: 2026-08-19 · Son etiketli sürüm: **2.7.4, 2026-08-19** (aktif ve düzenli)

## 2. Ne yapıyor

NuGet paket biçiminin üstüne PowerShell kancaları koyarak Windows'ta MSI/EXE/zip kurulumlarını otomatikleştiren paket yöneticisi. Başkasının yazdığı kurucuyu sarmalayıp, o kurucunun sisteme ne yaptığını kendi defterinde tutuyor.

## 3. Runly ile kesişimi

- **Kurulum yeri:** `%ChocolateyInstall%` (varsayılan `C:\ProgramData\chocolatey`) — makine kapsamlı, Runly'nin HKCU/per-user tercihinin tersi. Bu fark bilinçli: choco yönetici hakkı ister.
- **Kurulum defteri:** her paket için `%ChocolateyInstall%\.chocolatey\<id>.<sürüm>\` klasörü — kurulum öncesi/sonrası registry farkı (`.registry`), ortam değişkeni farkı ve hatırlanan argümanlar burada. **Runly'ye en yakın nokta bu:** yedek dosyası tek başına değil, kurulumu tanımlayan klasörün içinde duruyor.
- **Kayıt yedeği ve kaldırma dürüstlüğü:** 05'te var (`RegistryService` fark alma, `AutomaticUninstallerService`'in her atlama kararını gerekçelendirmesi, `.registry.bad`).
- **Sürümleme:** `GitVersion.yml` ile sürüm türetiliyor; paket klasör adı `<id>.<sürüm>` olduğu için defter sürüm bazlı.
- **Paket doğrulama:** nupkg indirme + `checksum`/`checksumType` (paket betiği içinde `Install-ChocolateyPackage` çağrısında), ayrıca `chocolatey.snk` ile derlenen assembly imzalanıyor. Ayrıntısı 07'de.

## 4. Alınacak fikir

1. **Hatırlanan argümanlar (remembered arguments).** Choco, kurulumda verilen paket parametrelerini defterde saklıyor ve yükseltmede otomatik yeniden uyguluyor; böylece "yükselttim, ayarlarım gitti" olmuyor. Runly'nin karşılığı: kullanıcının seçtiği uzantı listesi ve özel uzantılar, güncelleme sırasında config'ten okunup yeniden uygulanmalı — ama §5'teki tuzağa dikkat.
2. **`chocolateyBeforeModify.ps1` kancası.** Yükseltme veya kaldırma sırasında **eski sürümün** betiği, yeni sürüm dokunmadan önce çalışıyor (`ChocolateyBeforeModifyTemplate.cs` deponun şablonlarında mevcut). Runly'de karşılığı: yeni sürüm kurulmadan önce **eski sürümün kendi bildiği** ilişkilendirmeleri geri alma fırsatı bulması. Sürümler arası şema değişince eski hâli sadece eski kod doğru temizleyebilir.
3. **Kurulum defteri = klasör, dosya değil.** Yedek XML'i, uygulanan seçimler, kurulum zamanı ve sürüm tek bir `<id>.<sürüm>` klasöründe. Runly şu an registry yedeğini tek dosya olarak alıyor; klasöre çevirmek yükseltmede "hangi yedek hangi kuruluma ait" sorusunu ortadan kaldırıyor. Maliyet: yol üretimi + eski tek-dosya biçimi için geriye dönük okuma.

## 5. Kaçınılacak hata

**Issue #2761 (açık, 2022-07-11'den beri): "Cannot override remembered install arguments on upgrade."** Hatırlanan argümanlar iyi fikir ama choco'da **geçersiz kılınamıyor**: kullanıcı `choco upgrade` sırasında `--params` verse bile ilk kurulumda kaydedilen değerler kazanıyor. Dört yıldır açık. Ders: kalıcı hâle getirilen kullanıcı tercihi, açık bir "bu sefer şunu kullan" yolu olmadan kaydedilmemeli. Runly'de aynısı, kullanıcı yükseltmede uzantı seçimini değiştiremediğinde ortaya çıkar.

**Issue #2150 (açık): "Incorrect install path displayed following successful installation."** Kurulumun bildirdiği yol ile gerçek yolun ayrışması. Runly açısından: kullanıcıya gösterilen yol, kayıt yazılan yolun kendisinden okunmalı; iki ayrı yerde hesaplanmamalı.

## 6. Doğrulama

- Kaynaktan okundu: `repos/chocolatey/choco` metadata, `releases/latest`, `commits[0]`, `contents/LICENSE` (Apache-2.0 başlığı), `contents/NOTICE`, kök + `src/` dizin listesi, issue #2761 gövdesi, `gh search code` ile `AutomaticUninstallerService.cs`, `RegistryService.cs`, `ChocolateyPackageInformationService.cs`, `ChocolateyBeforeModifyTemplate.cs` yollarının varlığı.
- Okunmadı / `doğrulanamadı`: bu dosyaların içerikleri bu taramada okunmadı (05'te okunmuştu); `.chocolatey\<id>.<sürüm>\` klasör düzeni 05'teki bulguya ve `ChocolateyPackageInformationService` adına dayanıyor.
- `docs/legal/CREDITS.md` içeriği okunmadı; marka koruması iddiası NOTICE'ın yönlendirmesine dayanıyor, metnin kendisi `doğrulanamadı`.
- Ücretli "Chocolatey for Business" ayrımı depoda `CHANGELOG_LICENSED.md` dosyasının varlığından görülüyor; kapsamı ve hangi özelliklerin CLI'dan çıkarıldığı `doğrulanamadı`.
