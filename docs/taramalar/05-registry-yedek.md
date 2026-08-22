# 05 — Kurulum/kaldırma dürüstlüğü, registry yedeği ve geri alma

Tarama tarihi: 2026-08-22. Künye rakamları (yıldız, açık issue, son push, son etiketli sürüm)
`gh api repos/<owner>/<repo>` ve `.../releases/latest` çıktısındandır; başka bir kaynaktan
gelen sayı ayrıca `doğrulanamadı` diye işaretlenmiştir.

Aranan beş soru: (1) yolu değişen/ölen kurulum ve self-heal, (2) kurulum öncesi doğrulama,
(3) yedek formatı ve kısmi geri yükleme, (4) dürüst kaldırma, (5) döngüyü doğrulayan test.


## chocolatey/choco

Windows paket yöneticisi; kurduğu MSI/EXE kurulumlarını sonradan kendi başına kaldırabilmek
için registry anlık görüntüsü tutuyor.

Künye: Apache-2.0 (LICENSE dosyası; GitHub API lisansı `NOASSERTION` döndürüyor, marka
`chocolatey` ayrıca korunuyor — README lisans + NOTICE'a yönlendiriyor). 11.489 yıldız,
514 açık issue, son push 2026-08-19, son etiketli sürüm 2.7.4 (2026-08-19).

Kritik mekanizma: kurulumdan önce ve sonra HKLM/HKCU `...\CurrentVersion\Uninstall` ağacının
anlık görüntüsü alınıyor, `GetInstallerKeysChanged(before, after)` farkı = paketin yazdığı
anahtar. Fark, paket başına XML olarak `%ChocolateyInstall%\.chocolatey\<id>.<sürüm>\.registry`
altında saklanıyor. Aynı desen ortam değişkenleri için de var: yeni/değişen ve silinen
değerler ayrı ayrı hesaplanıyor.

Bozuk yedek hâli: XML okunamazsa dosya `.registry.bad` adına taşınıyor, log'a "düzeltip
adını geri koy" talimatı basılıyor ve işlem hata yerine uyarıyla sürüyor. Yani bozuk durum
sessizce yutulmuyor ama tüm işlemi de düşürmüyor.

Kaldırmada dürüstlük: `AutomaticUninstallerService` her atlama kararını gerekçesiyle
yazıyor — snapshot yok, snapshot boş, `UninstallString` yok, `InstallLocation` dizini yok
veya Uninstall anahtarı yok ("başka bir yolla zaten kaldırılmış görünüyor"), uninstaller
exe dosyası artık diskte yok, sessiz kaldırma tespit edilemedi (30 sn zaman aşımlı soru,
cevap gelmezse atla + "Programlar ve Özellikler'den elle kaldırın" uyarısı). Çıkış kodu
beklenen listede değilse `FailOnAutoUninstaller` ayarına göre hata veya uyarı.

**Runly'ye alınacak fikir:** kayıt yazmadan önce/sonra fark almak ve farkı diskte tutmak;
ve her "yapmadım" kararını gerekçesiyle loglamak. Sessiz başarı yok.

**Kaçınılacak hata:** snapshot yalnız Uninstall ağacını kapsıyor, ilişkilendirme anahtarlarını
görmüyor. Fark tabanlı tespit, aynı anda başka bir kurulum çalışıyorsa yabancı anahtarı
sahiplenebilir.


## ScoopInstaller/Scoop

Komut satırı kurucu; uygulamayı sürüm klasörüne açıp dışarıya (shim, kısayol, PATH, env)
bildirimsel olarak bağlıyor.

Künye: Unlicense veya MIT (ikili lisans, sürüm 0.2.0'dan beri). 24.577 yıldız, 538 açık
issue, son push 2026-08-21, son etiketli sürüm v0.5.3 (2025-08-12) — bir yıldır etiket yok,
geliştirme sürüyor.

Kritik mekanizma: `scoop reset` dış dünyaya yazılan her şeyi manifest'ten yeniden üretiyor —
`current` junction'ı tazele, shim'leri yaz, başlat menüsü kısayollarını oluştur, PATH ve env
girdilerini önce kaldır sonra tekrar ekle, persist bağlarını çöz ve yeniden kur. Kaldırma
aynı listeyi ters yönde çalıştırıyor. Self-heal ayrı bir mekanizma değil: kurulumun
idempotent olarak yeniden çalıştırılması.

Yol sorunu çözümü: uygulama `app\<sürüm>` altında duruyor ama dışarıya verilen yol
`app\current` junction'ı. Sürüm değişince kayıtlı yol değişmiyor.

Kurulum öncesi doğrulama: shim hedefi diskte yoksa `abort` ("File doesn't exist"); manifest'te
belirtilen installer dosyası uygulama klasörünün dışına çıkıyorsa veya yoksa `abort`. Yani
dış kayıt yazılmadan önce hedefin hem varlığı hem konumu denetleniyor.

Kaldırma: `scoop uninstall --purge` kalıcı veriyi de siler, varsayılan bırakır. Silinemeyen
dizinde açık hata: "may be in use", "Access denied ... You might need to restart". Ayrı bir
tanı komutu var: `scoop checkup` (Defender istisnası, long paths, developer mode).

**Runly'ye alınacak fikir:** kurulum durumunu bildirimsel tutup "yeniden uygula" komutu
vermek; ve dış kayıt yazmadan önce hedefi doğrulamak.

**Kaçınılacak hata:** Scoop hiç yedek almıyor, yalnız yeniden üretiyor. Runly'nin dokunduğu
alanda (başka uygulamaların ProgID'leri, kullanıcının önceki tercihleri) yeniden üretilemeyen
veri var; yedek şart.


## microsoft/winget-cli

Windows Paket Yöneticisi. Portable paket akışı, Runly'nin problemine en yakın modeli
içeriyor: registry'ye yazılan kayıt aynı zamanda doğrulanabilir bir durum bildirimi.

Künye: MIT. 26.333 yıldız, 1.305 açık issue, son push 2026-08-20, son etiketli sürüm
v1.29.280 (2026-06-24).

Kritik mekanizma: portable kurulumda ARP (Uninstall) anahtarına yalnız görüntü verisi değil,
hedefin tam yolu, dosyanın SHA256'sı, symlink hedefi ve "kurulum dizinini ben oluşturdum",
"PATH'e ben ekledim" bayrakları yazılıyor. Kaldırma ve güncelleme öncesi `VerifyExpectedState()`
diskteki gerçekle kaydı karşılaştırıyor: hash tutmuyorsa veya symlink başka yeri gösteriyorsa
işlem duruyor ve `--force` isteniyor.

Çakışma kuralı: yazılacak ARP anahtarında başka bir ürün varsa kayıt reddediliyor
(`PortablePackageAlreadyExists`), `--force` ile üzerine yazılırsa uyarı basılıyor.

Kurulum öncesi doğrulama: ayrılmış Windows dosya adları, reparse point, taban dizinin dışına
çıkan göreli yol — üçü de kurulum başlamadan reddediliyor.

Kaldırma: `--purge` kurulum dizinini de siler, `--preserve` bırakır; varsayılan davranış
kullanıcı ayarıyla (`uninstallBehavior`) seçilebiliyor. PATH girdisi yalnız "ben ekledim"
bayrağı varsa geri alınıyor. Ayrıca bağımsız bir `winget repair` komutu var.

**Runly'ye alınacak fikir:** yazdığın kaydın içine kendi imzanı koy (hedef yol + hash + zaman)
ve her yıkıcı işlemden önce "hâlâ benim mi" diye sor. Bu, Runly'nin bugünkü hatasını hem
önler hem sonradan tespit eder.

**Kaçınılacak hata:** 1.305 açık issue; kaldırma artıklarının bu yığın içindeki payı
doğrulanamadı.


## velopack/velopack

Masaüstü uygulamaları için kurulum + otomatik güncelleme çerçevesi; Squirrel.Windows'un
yerine geçmek için yazılmış, Squirrel kurulumlarından otomatik göç iddiası README'de.

Künye: MIT. 2.286 yıldız, 56 açık issue, son push 2026-08-21, son etiketli sürüm 1.2.0
(2026-06-03). README'deki hız ve memnuniyet ifadeleri Discord alıntısı — `doğrulanamadı`.

Kritik mekanizma (yol değişimi): kısayolları kayıtlı bir listeden değil, taramayla buluyor.
Masaüstü, Startup, Başlat menüsü ve sabitlenmiş kısayol klasörleri geziliyor; hedefi **veya**
çalışma dizini eski kök altında olan `.lnk` dosyaları seçiliyor ve hedef yeni yola
*güncelleniyor* — sil/yeniden oluştur değil. Böylece kullanıcının yeniden adlandırdığı veya
taşıdığı kısayol korunuyor. Çözülemeyen kısayol için ayrı uyarı loglanıyor.

Kaldırma sırası: MSI ile kurulduysa reddet ("msiexec ile kaldır"); çalışan süreçleri durdur;
uninstall hook'unu 60 sn sınırla çalıştır; köke işaret eden tüm kısayolları sil; kök dizini
boşalt; `%TEMP%\velopack_<id>` dizinini sil; Uninstall registry anahtarını sil; kendini
silmeyi zamanla.

**Runly'ye alınacak fikir:** dış dünyadaki kayıtları "kimin yazdığı listesi"nden değil,
**hedefe göre ters aramayla** bul. Runly için karşılığı: ProgID/OpenWithProgids ağacında
değeri bizim exe'mize işaret eden girdileri tarayarak bulmak — eski kurulumdan kalanlar dahil.

**Kaçınılacak hata:** registry anahtarı silinemezse `error!` loglanıyor ama kullanıcıya yine
"kaldırma tamamlandı" diyaloğu gösteriliyor. Kısmi başarısızlık kullanıcıya değil log'a
gidiyor. Runly'de tersi gerekiyor.


## Squirrel/Squirrel.Windows

Eski nesil kurulum/güncelleme çerçevesi. "Registry'de eski yol kaldı" sınıfının ders kitabı
örneği.

Künye: MIT. 7.977 yıldız, 423 açık issue, son push 2024-07-24, son etiketli sürüm 2.0.1
(2020-09-27). Arşivlenmemiş ama fiilen durmuş — bağımlılık olarak alınmaz, tasarımı okunur.

Tasarım hatası: uygulama `%LocalAppData%\<App>\app-<sürüm>\` altına kuruluyordu. Dışarıya
verilen her yol (kısayol, ilişkilendirme, "Birlikte aç") sürümlü klasörü gösterdiğinde
güncellemeden sonra o klasör siliniyor ve yol ölüyor. Sonradan kökteki sabit `Update.exe`
stub'ı (`--processStart`) ile kapatılmaya çalışıldı; yani sorun "sabit giriş noktası" katmanı
eklenerek örtüldü, kaynağı düzeltilmedi.

Runly'yi doğrudan ilgilendiren açık issue: **#1628** (2020-06-16, hâlâ açık) — exe bir dosya
türüne varsayılan yapıldığında, güncellemeden sonra dosyaya çift tıklamak eski sürümü açmaya
devam ediyor; uygulama normal yolla açılıp kapatılmadan düzelmiyor. İlişkilendirme kaydı
sürümlü yola çakılmış durumda.

Artık şikâyetleri (hepsi açık): #1028 başlat menüsü klasörü kalıyor, #1586 `%LocalAppData%`
altında kalan `.dead` dosyası yeni kurulumu engelliyor, #267 kullanımdaki shell extension
yüzünden kök dizin kalıyor, #197 "temiz kaldırma" isteği 2015'ten beri açık.

**Kaçınılacak hata:** registry'ye sürüme, derleme çıktısına veya geçici konuma bağlı bir yol
yazmak. Runly'nin bugün yaşadığı olayla aynı sınıf: kaydedilen yol, kaydı yazan sürecin
ömründen kısa ömürlü.


## wixtoolset/wix (ve arkasındaki MSI kuralları)

MSI paketleri üreten araç zinciri. Buradaki değer WiX kodu değil, MSI'ın kurulum/kaldırma
muhasebesi.

Künye: MS-RL (OSI onaylı, dosya bazında karşılıklı/reciprocal — koddan alıntı yapılmaz).
1.124 yıldız; kod deposunda yalnız 3 açık issue çünkü sorun takibi ayrı depoda:
`wixtoolset/issues`, 666 açık issue (son push 2025-06-09). Son push 2026-08-18, son etiketli
sürüm v7.0.0 (2026-04-06).

Kritik mekanizma: her bileşenin tek bir **KeyPath**'i var; Installer bileşenin kurulu olup
olmadığını bu tek noktaya bakarak anlıyor. Kaldırma bileşen referans sayımıyla yapılıyor —
aynı bileşeni başka bir ürün de kullanıyorsa silinmiyor.

Self-heal: reklamlı giriş noktaları (kısayol, Class tablosu üzerinden dosya ilişkilendirmesi,
COM kaydı) tetiklendiğinde gömülü Darwin descriptor çözülüyor; ilgili bileşen eksik veya
bozuksa Windows Installer onarımı başlatıyor (Microsoft Learn, Resiliency). Yani
"ilişkilendirmeye tıklamak" onarımı tetikleyen olayın kendisi — Runly'nin istediği davranışın
işletim sistemindeki karşılığı.

Kurulum öncesi kural denetimi: ICE43 reklamsız kısayol içeren bileşenin KeyPath'inin bir HKCU
kaydı olmasını şart koşuyor; ICE57 aynı bileşende kullanıcı başına ve makine başına veriyi
karıştırmayı hata sayıyor. Paket daha üretilirken kural motoru çalışıyor.

**Runly'ye alınacak fikir:** "tek anahtar = kurulu mu" göstergesi (KeyPath karşılığı) ve
kayıt yazılmadan önce çalışan kural denetimi (HKCU/HKLM karıştırma, eksik ProgID, ölü yol).

**Kaçınılacak hata:** MSI'da custom action ile yazılan kayıtlar bileşen muhasebesinin dışında
kalır ve kaldırmada silinmez — orphan kaydın klasik kaynağı. Runly'nin doğrudan
`RegSetValue` ile yazdığı her şey aynı sınıfta: sahiplik kaydı tutulmazsa artık kalır.


## NSIS (NSIS-Dev/nsis aynası)

Betikle installer üreten sistem; kaldırma bölümünü geliştirici elle yazıyor.

Künye: zlib/libpng lisansı (sıkıştırma modülleri ayrı). 855 yıldız, GitHub'da 7 açık issue —
ama depo bir ayna ve README "sorunları SourceForge'a bildirin" diyor, dolayısıyla GitHub
sayıları projenin gerçek yükünü temsil etmiyor. Son push 2026-08-22; GitHub'da etiketli
sürüm yok (`releases/latest` 404 döndü), sürümler SourceForge'da.

Kritik mekanizma: `DeleteRegKey` üç kipli — `/ifempty`, `/ifnosubkeys`, `/ifnovalues`. Yani
"yalnız benden başka kimse kalmadıysa sil" semantiği dilin içine gömülü. Bayrak verilmezse
tüm alt ağaç gidiyor; paylaşılan anahtarlarda bu tek satırlık bir felaket.

`SetRegView 32|64` ve `SHCTX` (SetShellVarContext'e göre HKLM ya da HKCU'ya çözülür) hangi
görünüme ve hangi kovana yazıldığını açıkça seçtiriyor.

**Runly'ye alınacak fikir:** silme çağrılarına "koşullu sil" kipi. Runly `OpenWithProgids`
altında yalnız kendi değerini kaldırmalı, anahtar boşaldıysa anahtarı silmeli, dolu ise
dokunmamalı.

**Kaçınılacak hata:** NSIS'te her registry işlemi başarısızlıkta yalnız error flag set eder;
betik bayrağı okumazsa hata sessizce geçer. Varsayılan davranış sessizlik — Runly'de tersi
olmalı.


## PowerShell/PowerShell

MSI ile kurulan büyük bir uygulama; ilgi çeken kısım ürünün kendisi değil, kur/kaldır
döngüsünü doğrulayan test paketi.

Künye: MIT. 55.056 yıldız, 1.603 açık issue, son push 2026-08-20, son etiketli sürüm v7.6.5
(2026-08-14).

Kritik mekanizma: `test/packaging/windows/msi.tests.ps1` — gerçek MSI'ı `msiexec` ile kuran
ve kaldıran Pester paketi. Özellik matrisi (`ADD_PATH`, `USE_MU`, `DISABLE_TELEMETRY`) her
kombinasyon için: kur → registry değerini/PATH'i/env değişkenini doğrula → kaldır.

Üç ayrıntı değerli: (a) her bağlamın ilk testi "bu ürün şu an kurulu değil" iddiası
(`Win32_Property` üzerinden UpgradeCode sorgusu) — kirli makinede yanlış yeşil vermesin;
(b) `BeforeAll` kaydedilmiş kurulum özellikleri anahtarını yedek bir ada taşıyor, `AfterAll`
geri koyuyor — test kendi çalıştığı makinenin durumunu koruyor; (c) PATH doğrulaması mutlak
değil farksal: kurulum öncesi PATH anlık görüntüsü alınıp yeni girdi ona göre aranıyor.

**Runly'ye alınacak fikir:** kur/kaldır döngüsünü gerçek registry üzerinde koşan, öncesinde
"temiz mi" iddiası olan ve makinenin durumunu geri koyan test paketi.

**Kaçınılacak hata:** testler kaldırmayı yalnız "hata fırlatmadı" diye doğruluyor; kurulumun
eklediği PATH girdisinin kaldırma sonrası gittiği ayrıca iddia edilmiyor. Runly'de asıl
değerli iddia tam olarak budur.


## DanysysTeam/PS-SFTA (ek — sınırın nerede olduğu)

`.ext` → ProgID varsayılanını `FileExts\<ext>\UserChoice` altına yazan PowerShell betiği.

Künye: **lisans yok** (GitHub `license` alanı null) → bağımlılık olarak alınamaz, kod
kopyalanamaz. 379 yıldız, 11 açık issue, etiketli sürüm yok, son push 2022-10-10 (terk
edilmiş sayılır).

Ne öğretiyor: `UserChoice` yazmak için Windows'un kullanıcı SID'i + uzantı + ProgID + zaman
damgasından türettiği hash'in yeniden üretilmesi ve mevcut anahtarın izinleri değiştirilerek
silinmesi gerekiyor. Bu, işletim sisteminin kasıtlı olarak kapattığı bir kapı; her Windows
sürümünde kırılabilir.

**Runly'ye alınacak fikir:** yedeklenebilir ama güvenilir biçimde geri yazılamayan bir alan
olduğunu kabul et. Runly'nin `UserChoiceInspector`'ı okumak ve kullanıcıya durumu bildirmekle
sınırlı kalmalı; hash üretip yazmaya çalışmak dürüst kaldırma vaadini ilk Windows
güncellemesinde çöpe atar.


## Runly için sonuç

1. **Kurulum öncesi başlatıcı doğrulaması (zorunlu geçit).** Kaydedilecek exe: diskte var mı,
   dosya mı, imzalı/beklenen ad mı, ve yasak konumlarda mı — `\bin\Debug\`, `\bin\Release\`,
   `\obj\`, `%TEMP%`, ağ yolu (UNC), çıkarılabilir sürücü. Biri tutuyorsa kurulum başlamadan
   reddet ve nedeni söyle. Bugünkü hatayı doğrudan kapatan tek madde budur.
   (Kaynak: Scoop `abort`, winget yol/ayrılmış ad denetimleri.)

2. **Sürümden ve derlemeden bağımsız sabit başlatıcı yolu.** Registry'ye yalnız kurulum ömrü
   boyunca değişmeyecek tek bir yol yazılsın (kurulu konum), geliştirme çıktısı asla.
   (Kaynak: Squirrel #1628 vs Velopack/Scoop `current`.)

3. **Yazdığın kaydın içine kendi imzanı koy.** Runly ProgID'si altında ayrı bir değer bloğu:
   hedef exe tam yolu, dosya SHA256'sı, yazım zamanı, Runly sürümü, kaynak yedek dosyası.
   Kaldırma, onarım ve durum ekranı önce bunu okuyup diskle karşılaştırsın; uymuyorsa
   kullanıcıya sor, sessizce üzerine yazma. (Kaynak: winget `VerifyExpectedState`.)

4. **Sağlık taraması + "Onar" düğmesi.** Her ilişkilendirme için: hedef dosya var mı, ProgID
   bize mi ait, `OpenWithProgids` girdisi duruyor mu, `RegisteredApplications` işaretçisi
   sağlam mı. Onar = kaydı manifest'ten yeniden üret, kısmi durumu düzelt.
   (Kaynak: `scoop reset` + `scoop checkup`, `winget repair`, MSI resiliency.)

5. **Ters aramayla artık avı.** Kaldırmada yalnız kayıtlı listeye güvenme; ProgID ve
   `OpenWithProgids` ağacında değeri Runly exe'sine (veya eski Runly yollarına) işaret eden
   girdileri tarayıp bul. Önceki bozuk kurulumun bıraktıkları ancak böyle temizlenir.
   (Kaynak: Velopack kısayol taraması.)

6. **Silmede sahiplik ve koşul kuralı.** Yalnız Runly imzalı anahtarı sil; başkasına ait
   anahtarda yalnız kendi değerimizi kaldır; anahtar boşaldıysa anahtarı sil, dolu ise
   dokunma. Mevcut `IsRunlyOwned` kontrolü silme yolunun tamamına uygulanmalı.
   (Kaynak: NSIS `/ifempty`, MSI bileşen referans sayımı.)

7. **Yedek formatı: `.reg` + yanında JSON manifest.** `.reg` kalsın (Windows yerlisi, Runly
   çalışmasa da elle geri yüklenebilir); yanına manifest koy: yedeklenen anahtar listesi,
   Runly sürümü, hedef exe, zaman, `.reg` dosyasının hash'i. Manifest okunamazsa dosyayı
   `.bad` uzantısıyla kenara al, ne yapılacağını yaz, sessizce devam etme.
   (Kaynak: choco `.registry` / `.registry.bad`.)

8. **Kur → doğrula → kaldır → artık yok, otomatik test.** Test önce "Runly kurulu değil"
   iddiasıyla başlasın, kurulum öncesi HKCU anlık görüntüsü alsın, kaldırmadan sonra fark
   **boş** olsun; test makinenin önceki durumunu `AfterAll`'da geri koysun. Geri yükleme
   testi kısmi başarısızlığı da kapsasın: N/M anahtar geri yüklendi raporu üretilmeli,
   kullanıcıya "tamamlandı" denmemeli. (Kaynak: PowerShell `msi.tests.ps1`; Velopack'in
   kısmi başarısızlığı loga gömme davranışının tersi.)


## Kaynaklar

- Künye verileri: `gh api repos/<owner>/<repo>` ve `gh api repos/<owner>/<repo>/releases/latest`,
  2026-08-22.
- chocolatey/choco — `src/chocolatey/infrastructure.app/services/RegistryService.cs`,
  `ChocolateyPackageInformationService.cs`, `AutomaticUninstallerService.cs`;
  https://github.com/chocolatey/choco
- ScoopInstaller/Scoop — `libexec/scoop-reset.ps1`, `libexec/scoop-uninstall.ps1`,
  `lib/install.ps1`, `lib/diagnostic.ps1`; https://github.com/ScoopInstaller/Scoop
- microsoft/winget-cli — `src/AppInstallerCLICore/PortableInstaller.cpp`,
  `src/AppInstallerCLICore/Workflows/PortableFlow.cpp`,
  `src/AppInstallerCLICore/Commands/RepairCommand.cpp`; https://github.com/microsoft/winget-cli
- velopack/velopack — `src/bins/src/commands/uninstall.rs`, `src/bins/src/windows/shortcuts.rs`,
  `src/lib-rust/src/locator.rs`; https://github.com/velopack/velopack
- Squirrel/Squirrel.Windows — issue #1628, #1028, #1586, #267, #197;
  https://github.com/Squirrel/Squirrel.Windows/issues/1628
- wixtoolset/wix — https://github.com/wixtoolset/wix, sorun takibi
  https://github.com/wixtoolset/issues; MSI davranışı:
  https://learn.microsoft.com/en-us/windows/win32/msi/resiliency ,
  https://learn.microsoft.com/en-us/windows/win32/msi/component-table ,
  https://learn.microsoft.com/en-us/windows/win32/msi/ice43 ,
  https://learn.microsoft.com/en-us/windows/win32/msi/ice57
- NSIS (ayna) — `Docs/src/registry.but`, `Docs/src/uninstall.but`;
  https://github.com/NSIS-Dev/nsis
- PowerShell/PowerShell — `test/packaging/windows/msi.tests.ps1`;
  https://github.com/PowerShell/PowerShell
- DanysysTeam/PS-SFTA — `SFTA.ps1`; https://github.com/DanysysTeam/PS-SFTA
