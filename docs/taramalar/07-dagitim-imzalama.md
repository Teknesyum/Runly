# 07 — Dağıtım, imzalama, SmartScreen

Tarama tarihi: 2026-08-22. Tüm depo rakamları `gh api repos/<owner>/<repo>` ve
`.../releases/latest` çıktısından, o tarihte alındı. İşaretsiz her sayı bu çağrılardan gelir;
üçüncü taraf iddialar `doğrulanamadı` etiketiyle yazıldı.

---

## velopack/velopack

.NET/C++/Rust/JS masaüstü uygulamaları için kurulum + otomatik güncelleme çerçevesi; `vpk`
adlı tek komutla kurucu, delta paket ve kendini güncelleyen taşınabilir paket üretiyor.

MIT · 2.286 yıldız · 56 açık issue · son push 2026-08-21 · son etiketli sürüm **1.2.0
(2026-06-03)** · varsayılan dal `develop`.

**Alınacak fikir:** güncelleme akışını üç ayrı adıma bölmüş — `CheckForUpdatesAsync` /
`DownloadUpdatesAsync` / `ApplyUpdatesAndRestart`. Bu ayrım, "bildir ama indirme",
"indir ama kurma" gibi ara davranışları çerçeveyi değiştirmeden mümkün kılıyor. Runly
kendi bildirim mekanizmasını yazsa bile bu üç adımlı sınırı taklit etmeli: kontrol,
indirme ve uygulama aynı fonksiyonda birleşmemeli.

**İkinci fikir:** imzalamayı çerçeveye gömmemiş, dışarı almış. `--signParams` ile ham
signtool argümanı, `--signTemplate` ile `{{file}}` yer tutuculu keyfi bir imza aracı
(AzureSignTool vb.) çalıştırılıyor. İmzalama kararını sertifika sağlayıcısından bağımsız
tutan doğru sınır bu.

**Kaçınılacak hata:** Velopack, uygulamanın `Main` fonksiyonunun en başında
`VelopackApp.Build().Run()` çağrılmasını şart koşuyor — yani süreç yaşam döngüsünün
sahibi çerçeve oluyor. Runly'nin başlatıcısı NativeAOT ve ~3 MB; süreç başlangıcını bir
güncelleme çerçevesine devretmek hem AOT uyumluluk riski hem de boyut riski. Velopack'in
Runly ölçeğindeki asıl maliyeti lisans değil, **giriş noktasının sahipliği**.

**Doğrulanamayan iddia:** Velopack belgeleri EV sertifikanın "anında SmartScreen itibarı"
verdiğini, OV'nin ise "kısa bir itibar biriktirme dönemi" gerektirdiğini söylüyor
(docs.velopack.io/packaging/signing). Microsoft'un kendi SmartScreen belgesinde EV'ye
böyle bir ayrıcalık tanıyan bir cümle yok — aşağıya bakın. İddia `doğrulanamadı`.

---

## gerardog/gsudo

Windows için `sudo` eşdeğeri; küçük, tek amaçlı, yaygın dağıtılmış bir Windows aracı —
Runly'ye ölçek olarak en yakın örnek.

MIT · 6.029 yıldız · 50 açık issue · son push 2026-08-12 · son etiketli sürüm
**v2.6.1 (2025-10-06)**.

**Alınacak fikir — dağıtım matrisi.** README kurulumu tek bir kanala bağlamamış, beş
kanal sunuyor: Scoop, WinGet, Chocolatey, elle MSI indirme, tek satırlık PowerShell
kurulum betiği, artı `gsudo.portable.zip`. Aynı sürümden hem MSI hem taşınabilir zip
üretiliyor; paket yöneticileri zip'i, elle kuran kullanıcı MSI'ı alıyor. Runly'nin
elindeki tek zip'i ikiye ayırması (taşınabilir zip + kurucu) bu matrisin ön koşulu.

**Alınacak fikir — etiketten sürüme giden temiz CI.** `release.yml` yalnızca `v*`
etiketinde tetikleniyor; derleme işi ayrı bir `ci.yml`'den `workflow_call` ile geliyor;
sürüm numarası GitVersion ile üretilip iş çıktısı olarak taşınıyor. Sonra: sertifikayı
secret'tan base64 çözüp diske yaz → imzala → **pfx'i sil** → kurucuyu üret → önce
**taslak** release, ayrı bir job'da yayınlama. Release notları
`generateReleaseNotes: true` ile otomatik. Chocolatey ve WinGet gönderimi ayrı bir
job'da, `environment:` koruması arkasında.

**Alınacak fikir — WinGet gönderimini elle yapmamak.** gsudo, `winget-releaser`
action'ını kullanıyor ve ayrıca `workflow_dispatch` ile elle tetiklenen bir yedek
akış (`winget-solo.yml`) tutuyor. Otomatik gönderim bozulduğunda sürüm yayınını
bloklamayan bu ikinci yol pratik.

**Kaçınılacak hata / dikkat:** README'de SmartScreen'den, imzadan, "bilinmeyen yayımcı"
uyarısından **tek kelime yok**. Depoda SmartScreen konulu açık issue de bulunamadı
(`gh search issues` ile arandı; yalnızca bir antivirüs uyumluluk issue'su, #87, kapalı).
gsudo bu sorunu README'de anlatarak değil, **sessizce imzalayarak** çözmüş: derleme
akışında `signtool.exe` ile pfx tabanlı Authenticode imzası var. Yani "uyarıyı geçin"
yazan projeler değil, imzalayan projeler bu ölçeğe ulaşmış. Sertifikanın tipi (OV mu
EV mi) depodan anlaşılmıyor — `doğrulanamadı`.

---

## ScoopInstaller/Scoop + ScoopInstaller/Main

Scoop: yönetici hakkı istemeyen, UAC göstermeyen, zip + JSON manifestle çalışan komut
satırı kurucusu. Main: varsayılan manifest kovası.

Scoop — `UNLICENSE or MIT` (LICENSE dosyasından doğrulandı; GitHub API `NOASSERTION`
döndürüyor) · 24.577 yıldız · 538 açık issue · son push 2026-08-21 · son etiketli sürüm
**v0.5.3 (2025-08-12)**.
Main — Unlicense · 1.866 yıldız · 122 açık issue · son push 2026-08-22.

**Alınacak fikir — `.sha256` yan dosyası.** gsudo'nun Main kovasındaki manifesti
(`bucket/gsudo.json`) `checkver` + `autoupdate` blokları içeriyor ve autoupdate hash'i
`"hash": { "url": "$url.sha256" }` ile alıyor. Yani release'e zip'in yanına
`<zip-adı>.sha256` koyduğun anda, Scoop'un Excavator botu yeni sürümü **sen hiçbir şey
yapmadan** manifeste işliyor. Runly'nin `build.ps1`'i zaten SHA-256 hesaplıyor ama
yayınlamıyor — bu tek satırlık eksik, bakım maliyeti sıfıra yakın bir kanalı kapatıyor.

**Alınacak fikir — mimari boşluk:** manifest `env_add_path`, `bin` (takma ad dahil),
`extract_dir` (mimariye göre farklı klasör) ve `post_install` ile kurulumu tarif ediyor;
kurulum mantığı uygulamanın içinde değil, dışarıda veri olarak duruyor. Runly'nin kurulum
adımlarını (PATH, kısayol, dosya ilişkilendirme) betikten çıkarıp bildirimsel bir yapıya
taşıması aynı sınırı verir.

**Kaçınılacak hata — kovayı yanlış seçmek.** Main kovasının kriterleri açık: **GUI
olmayan** araç, "makul ölçüde bilinen ve yaygın kullanılan geliştirici aracı", **en az
500 yıldız ve 150 fork**, sürüme özgü indirme URL'i, ayrıntılı pre/post betik yok
(Scoop wiki, "Criteria for including apps in the main bucket"). Runly bir GUI başlatıcı
ve 1 yıldız — Main'e PR açmak reddedilir. Doğru hedef `ScoopInstaller/Extras`; Extras
tanımı "Main kriterlerine uymayan manifestler".

---

## microsoft/winget-pkgs

Windows Package Manager'ın topluluk manifest deposu; winget'e paket "göndermek"
buraya PR açmak demek.

MIT · 10.991 yıldız · **3.337 açık issue** · son push 2026-08-22 · release yok
(`releases/latest` 404 — manifest deposu, ürün deposu değil).

**Alınacak fikir — gerçek şartlar listesi** (`doc/FirstContribution.md` ve
`doc/Policies.md`, birincil kaynak):

- Desteklenen kurucu tipleri: MSIX, APPX, MSI, exe tabanlı kurucular, font dosyaları;
  bunlar `.zip` içine gömülü de olabilir. **Betikler açıkça yasak** (`.bat`, `.ps1`).
  Runly'nin `install.ps1`'i winget'e giremez — zip veya kurucu gerekir.
- Kurulum **etkileşimsiz** tamamlanabilmeli ("silent with progress"): UI gösterebilir,
  ama kullanıcı tıklamak zorunda kalmamalı.
- Tek PR = tek paket sürümü. Manifest dışında hiçbir dosya (README, doc, imla düzeltmesi)
  aynı PR'da olamaz.
- Singleton manifest yasak; çok dosyalı manifest seti ve şema başlığı (`# yaml-language-server:`)
  zorunlu.
- Kurulum URL'i resmî, kalıcı ve **sürüme özgü** olmalı; "vanity" URL altında binary
  değiştirmek hash uyuşmazlığı üretir.
- Doğrulama hattında 10 adım ve çoklu güvenlik taraması var; **herhangi biri PUA
  bayrağı kaldırırsa paket kabul edilmiyor** — uygulamanın meşru olması durumu
  değiştirmiyor.

**Kaçınılacak hata:** imza şartı listede yok; winget imzasız binary'yi reddetmiyor. Ama
imzasız + yeni + az indirilen bir dosyanın Defender/PUA taramasına takılma olasılığı
yüksek ve o noktada itiraz mekanizması yok. Yani winget "imza istemiyor" ama pratikte
imzasızlığı cezalandırıyor.

**İkinci dikkat:** `doc/Policies.md`'deki tip listesinde `portable` geçmiyor; winget
şemasının `portable` installerType'ı desteklediği biliniyor ama bu belgeden
`doğrulanamadı`. Runly zip'i gönderilecekse `nestedInstallerType` seçimi PR öncesi
şema belgesinden teyit edilmeli.

---

## vedantmgoyal9/winget-releaser (gsudo'nun kullandığı action)

GitHub release'ini otomatik olarak winget-pkgs PR'ına çeviren action.

**AGPL-3.0** · 307 yıldız · 11 açık issue · son push 2026-07-28 · son etiketli sürüm
**v2 (2025-01-27)**.

**Alınacak fikir:** `identifier` + `installers-regex` ile hangi asset'in gönderileceğini
tek satırda belirliyorsun; manifest üretimi ve fork/PR akışı gizleniyor. winget bakımının
asıl yükü olan "her sürümde elle YAML güncelleme" böyle ortadan kalkıyor.

**Kaçınılacak hata / risk:** action'ın kendi lisansı AGPL-3.0 — CI'da çalıştırmak
Runly'nin MIT lisansını etkilemez, ama fork'lanıp değiştirilirse kaynak açma yükümlülüğü
doğar. Ayrıca çalışması için `winget-pkgs` fork'una yazma yetkisi olan bir **kişisel
erişim token'ı** gerekiyor (gsudo bunu `WINGET_RELEASER_TOKEN` secret'ında tutuyor);
bu, `GITHUB_TOKEN`'dan daha geniş bir gizli anahtar yüzeyi. Son etiketli sürüm
2025-01'de kalmış, ana dal hâlâ hareketli — gsudo `@main` referansı kullanıyor,
yani sabitlenmemiş bir üçüncü taraf action'a güveniyor. Runly bunu yaparsa SHA'ya
sabitlemeli.

---

## chocolatey/choco

Windows paket yöneticisi; topluluk deposu (community.chocolatey.org) **insan
moderasyonlu**.

Apache-2.0 (LICENSE dosyasından doğrulandı; GitHub API `NOASSERTION` döndürüyor,
marka kısıtlarının ayrı tutulduğu belirtiliyor) · 11.489 yıldız · 514 açık issue ·
son push 2026-08-19 · son etiketli sürüm **2.7.4 (2026-08-19)**.

**Alınacak fikir — VERIFICATION.txt deseni.** Chocolatey, pakete binary gömüyorsan
—**yazılımın üreticisi sen olsan bile**— `VERIFICATION.txt` koymanı şart koşuyor
(kural CPMR0006); içinde binary'nin resmî kaynağı ve checksum'ı yer alır, moderatör
karşılaştırır (docs.chocolatey.org, moderation/package-validator). Bu, "indirileni
kaynakla eşleştirme" sözleşmesinin en yalın hâli ve Runly kendi release'inde aynı
şeyi bedavaya yapabilir: zip yanında hash + hangi commit'ten derlendiği.

**Kaçınılacak hata:** Chocolatey topluluk deposu, üç kanal arasında **en yüksek
sürtünmeli** olanı: paket doğrulama + insan moderasyonu, her yeni sürümde tekrar.
Runly ölçeğinde bu kanal en son sıraya konur. Ayrıca choco'nun kendi kurulumu yönetici
hakkı ve `C:\ProgramData\chocolatey` gerektirir — Runly'nin "yönetici istemeyen kurulum"
duruşuyla çelişir.

---

## sigstore/cosign

Konteyner ve artefakt imzalama aracı; Fulcio (kısa ömürlü sertifika) + Rekor (şeffaflık
günlüğü) ile anahtarsız imzalama.

Apache-2.0 · 6.227 yıldız · 162 açık issue · son push 2026-08-19 · son etiketli sürüm
**v3.1.3 (2026-08-06)**.

**Alınacak fikir:** anahtarsız imzalama modeli — imzalayanın kimliği bir OIDC kimliğine
(GitHub Actions iş akışı) bağlanıyor, uzun ömürlü özel anahtar hiç var olmuyor. Runly
için doğrudan değeri: **saklanacak sır yok**, dolayısıyla sızacak sır da yok.

**Kaçınılacak hata — kategori hatası.** cosign **Authenticode üretmez**. Ürettiği imza
Windows kabuğunun, SmartScreen'in veya "Dijital İmzalar" sekmesinin baktığı yerde
değildir. `cosign` imzası SmartScreen uyarısını **kaldırmaz**; yalnızca komut satırından
doğrulama yapabilen kullanıcıya hitap eder. Bunu SmartScreen çözümü sanmak bu alandaki
en yaygın yanılgı. Ayrıca README, geleceğin `sigstore-go` üzerine kurulacağını ve
cosign 2.x'in "kararlı" olduğunu söylüyor — oysa yayınlanmış son sürüm 3.1.3; README
kendi sürüm gerçeğinin gerisinde.

---

## actions/attest-build-provenance (+ actions/attest)

İş akışı çıktısına SLSA derleme kanıtı (provenance attestation) üretip imzalayan action;
imza kısa ömürlü Sigstore sertifikasıyla atılıyor, kanıt GitHub attestations API'sine
yükleniyor.

attest-build-provenance — MIT · 1.025 yıldız · 10 açık issue · son push 2026-08-21 ·
son sürüm **v4.2.2 (2026-08-06)**.
actions/attest — MIT · 157 yıldız · 11 açık issue · son sürüm **v4.2.2 (2026-08-04)**.

**Alınacak fikir:** imzasız güvenin kurulma biçimi. Kanıt, artefaktın adını ve digest'ini
bir derleme kaynağına (repo, commit, iş akışı) bağlıyor; kullanıcı
`gh attestation verify` ile "bu zip gerçekten bu repodan, bu commit'ten, bu iş akışıyla
üretildi mi" sorusunu doğrulayabiliyor. Runly'nin elle derleyip yüklediği zip'in şu an
hiçbir bağı yok — kanıt eklemek, imza satın almadan **kaynak-artefakt bağını** kurar.

**İki uyarı, ikisi de README'de açık:**
1. Attestation'lar **genel (public) depolarda** tüm güncel GitHub planlarında var; özel
   depolarda GitHub Enterprise Cloud gerekiyor. Runly deposu şu an public (`gh repo view`
   ile doğrulandı) — yani bedava.
2. v4 itibarıyla bu action artık `actions/attest`'in ince bir sarmalayıcısı; README yeni
   uygulamalar için doğrudan `actions/attest` kullanılmasını söylüyor. Yeni kurulumun
   `attest-build-provenance` üzerine yapılması, bir yıl sonra taşınacak bir bağımlılık
   demek.

**Kaçınılacak hata:** provenance, SmartScreen'e hiçbir şey ifade etmez. Kanıt
doğrulaması `gh` CLI kurulu bir kullanıcı gerektirir; son kullanıcının çift tıkladığı
anda hiçbir etkisi yoktur. Değeri güvenlik denetimine ve ileri düzey kullanıcıya
karşıdır, uyarı ekranına karşı değil.

---

## Azure Artifact Signing (eski adıyla Trusted Signing) — depo değil, birincil belge

Microsoft'un yönetilen imzalama servisi; sertifika FIPS 140-3 seviye 3 HSM içinde
duruyor, dosya uç noktadan çıkmıyor (digest imzalama).

**Alınacak fikir:** anahtar saklamayı tamamen ortadan kaldırıyor — gsudo'nun yaptığı gibi
pfx'i base64 secret olarak taşımaya, diske yazıp silmeye gerek kalmıyor.

**Kaçınılacak hata — coğrafi uygunluk duvarı.** Microsoft Learn quickstart sayfasındaki
not aynen şunu diyor: Public Trust sertifikaları **kuruluşlara** ABD, Kanada, AB,
Birleşik Krallık, Avustralya, Yeni Zelanda, Japonya, Güney Kore, Singapur, İsviçre,
Norveç ve İsrail'de veriliyor; **bireysel geliştiriciler ABD veya Kanada'da bulunmak
zorunda**. Bu kısıtlar Private Trust için geçerli değil — ama Private Trust sertifikası
SmartScreen'e hitap etmez, yalnızca kendi güven deponuza yüklediğiniz makinelerde
anlamlıdır. **Türkiye bu listede yok.** Runly için Azure Artifact Signing, fiyatı ne
olursa olsun büyük olasılıkla erişilebilir değil; bu, karar ağacının ilk düğümü olmalı.

Fiyat: 9,99 USD/ay (5.000 imza, 1 sertifika profili) ve 99,99 USD/ay (100.000 imza,
10 profil) — kaynak Azure fiyatlandırma sayfası üzerinden arama sonucu, birincil sayfadan
satır satır `doğrulanamadı`. Kimlik doğrulama süresi 1–20 iş günü olarak belirtiliyor
(quickstart, "Processing time").

---

## Beş sorunun cevabı

**1. SmartScreen duvarı.** Microsoft'un kendi SmartScreen belgesi (Learn, güncelleme
2026-04-23) mekanizmayı şöyle anlatıyor: indirilen program **ve onu imzalayan dijital
imza** için itibar kontrolü yapılır; "bir URL, dosya, uygulama veya **sertifika**
yerleşik bir itibara sahipse kullanıcı hiçbir uyarı görmez; itibar yoksa öğe daha
yüksek riskli işaretlenir ve uyarı gösterilir." Buradaki kritik cümle: itibar
**sertifikaya** birikir, dosyaya değil. Yani imzalamanın değeri "her yeni sürümde
sıfırdan başlamamak"; ilk gün uyarıyı sihirli şekilde kaldırmak değil. Microsoft
belgesinde EV'ye ayrıcalık tanıyan bir ifade yok. Velopack belgeleri EV'nin "anında
itibar" verdiğini söylüyor (`doğrulanamadı`); bir sertifika satıcısı sayfası ise
Microsoft'un 2026 güncellemeleriyle EV'nin bu avantajını kaldırdığını iddia ediyor
(sslcertshop.com, ticari kaynak, `doğrulanamadı`). İki iddia birbiriyle çelişiyor;
ikisine de para harcamadan güvenilmez.

2026 itibarıyla en ucuz gerçekçi yol, ucuzdan pahalıya:
(a) **Hiç imzalamamak + itibar biriktirmek** — 0 TL; sürüm başına aynı URL şemasını
korumak, indirmeleri tek kaynakta toplamak, yanlış pozitif çıkarsa Microsoft'un
dosya gönderim formuna (`microsoft.com/wdsi/filesubmission`, SmartScreen ürünü
seçilerek) bildirmek. Bu form belgede açıkça "yanlışlıkla uyarı gösterildiğini
düşünüyorsanız" diye tarif ediliyor ve bedava.
(b) **Scoop/WinGet üzerinden dağıtmak** — 0 TL; paket yöneticisiyle inen dosya
kullanıcının indirme klasöründen çift tıklanmadığı için tipik SmartScreen akışını
büyük ölçüde atlar (MOTW ve kabuk akışı farklıdır; Runly'de bizzat ölçülmeli).
(c) **OV/açık kaynak Authenticode sertifikası** — Certum'un açık kaynak geliştirici
sertifikası için kart+okuyucu setiyle ~69 EUR ilk yıl, ~29 EUR yenileme rakamları
üçüncü taraf bloglarda geçiyor (`doğrulanamadı`, piers.rocks / sslcertshop). Ayrıca
2026-02-27'den itibaren bir kod imzalama sertifikasının azami geçerliliğinin 459 güne
indiği belirtiliyor (`doğrulanamadı`, aynı ticari kaynak) — yani yenileme sıklığı artıyor.
(d) **Azure Artifact Signing** — 9,99 USD/ay, ama yukarıdaki coğrafi uygunluk duvarı
nedeniyle Türkiye'den erişilemiyor.

**2. Otomatik güncelleme.** Velopack Runly ölçeğinde aşırı: giriş noktasının sahipliğini
istiyor (AOT başlatıcıyla sürtüşme), kurucu/delta/güncelleme altyapısını birlikte
getiriyor ve **imzasız otomatik güncelleme güvenlik açısından bir dağıtım kanalıdır** —
imzan yoksa kendi kendini güncelleyen bir binary, saldırgan için hedeftir. gsudo bu
ölçekte otomatik güncelleyici yazmamış; güncellemeyi paket yöneticilerine (scoop/winget/
choco) ve elle indirmeye bırakmış. Runly için doğru karar aynısı: **"yeni sürüm var"
bildirimi + release sayfasına bağlantı**, indirmeyi ve kurmayı kullanıcıya/paket
yöneticisine bırakmak. Otomatik güncelleme, imza alındıktan sonra yeniden değerlendirilir.

**3. Paket yöneticileri.** Gerçek şartlar, çabaya göre sıralı:
- **Scoop (Extras kovası):** imza gerekmiyor, lisans şartı yok, tek JSON manifest.
  `checkver` + `autoupdate` + release'te `<zip>.sha256` yan dosyası varsa bakım maliyeti
  ~sıfır (Excavator botu günceller). Main kovası GUI olmayan araç ve 500 yıldız/150 fork
  istediği için Runly'ye kapalı. **Değer/çaba oranı en iyi kanal budur.**
- **WinGet:** imza gerekmiyor, ama betik kurucu yasak, kurulum etkileşimsiz olmalı, çok
  dosyalı YAML manifest seti, tek PR = tek sürüm, sürüme özgü kalıcı URL ve 10 adımlı
  doğrulama hattı. PUA bayrağı = ret. `winget-releaser` ile her sürümde tekrar eden yük
  otomatikleşiyor. Görünürlük en yüksek burada; ikinci sıraya konur.
- **Chocolatey:** binary gömülüyse VERIFICATION.txt, checksum eşleştirmesi ve insan
  moderasyonu — her sürümde. Üstelik choco'nun kendisi yönetici hakkı ister. Runly için
  değeri en düşük, çabası en yüksek kanal; şimdilik atlanır.

**4. Kurulum yeri.** `%LOCALAPPDATA%\Programs\Runly` doğru seçim ve korunmalı: yönetici
hakkı istemez, UAC göstermez, kurulum betiği basit kalır, Scoop'un felsefesiyle
(UAC bildirimlerini ortadan kaldırma) hizalıdır. `Program Files` yalnızca makine geneli
kurulum ve `HKLM` yazımı gerektiğinde anlamlı — Runly'nin böyle bir ihtiyacı yok.
Dosya ilişkilendirmesine etkisi: yönetici olmadan `HKCU\Software\Classes` altına ProgID
ve `Applications\Runly.exe` girdileri **yazılabilir**; yani "Birlikte aç" listesinde
görünmek ve `OpenWithProgids` üzerinden aday olmak yönetici gerektirmez. Ama
**varsayılan uygulama olmak** gerektirmez değil, imkânsıza yakındır:
`HKCU\...\Explorer\FileExts\.uzantı\UserChoice` anahtarı hash korumalıdır, uygulamaların
oraya yazması Microsoft tarafından yasaklanmıştır ve yazılırsa Windows ilişkilendirmeyi
sıfırlar. Üstelik 2025'te eklendiği bildirilen `UserChoiceLatest` mekanizmasının
hash'i doğru hesaplayan üçüncü taraf araçları da işlevsiz bıraktığı yazılıyor
(kolbi.cz blog, `doğrulanamadı`). Sonuç: Runly ilişkilendirmeyi **teklif eder**
(ProgID kaydı + Windows'un "Varsayılan uygulamalar" ekranını açması), **zorla kuramaz**.
Bu, yönetici hakkı meselesi değil, Windows'un tasarım kararıdır — `Program Files`'a
kurmak da bunu değiştirmez.

**5. Sürüm ve CI.** En temiz örnek gsudo: etiket → sürüm üretimi → derleme (ayrı,
yeniden kullanılabilir iş akışı) → imza → kurucu → **taslak** release → ayrı onaylı
job'da yayınlama → paket yöneticilerine gönderim. Release notları
`generateReleaseNotes: true` ile otomatik. Runly'nin şu anki durumu bunun tam tersi:
`.github/workflows` klasörü **yok** (CI hiç yok), sürüm `Directory.Build.props`
içindeki `<Version>` etiketinden okunuyor (etiketten değil), `build.ps1` SHA-256'yı
hesaplayıp ekrana basıyor ama yayınlamıyor, `scripts/install.ps1` indirdiği zip'in
hash'ini **doğrulamıyor**, ve depo kökünde altı adet yayınlanmış zip dosyası duruyor
(`Runly-v0.1.0…v0.2.0-win-x64.zip`) — artefaktlar sürüm kontrolüne sızmış.

---

## Runly için sonuç

1. **Azure Artifact Signing'i karar ağacından çıkar.** Public Trust sertifikası
   Türkiye'de bulunanlara verilmiyor (Microsoft Learn quickstart, uygunluk notu). Bu
   yolu araştırmaya daha fazla saat harcanmasın; imzalama kararı OV/açık kaynak
   sertifika (Certum tipi) ile "şimdilik imzalama" arasında verilecek.

2. **0.2.0'ı imzasız yayınla, ama itibarı biriktirmeye bugün başla.** SmartScreen itibarı
   sertifikaya birikiyor; sertifika yoksa biriktirilecek bir şey de yok — bu yüzden imza
   alınana kadar enerji, kullanıcıyı indirme klasöründen uzaklaştırmaya harcanmalı
   (madde 3). README'ye "uyarıyı geçin" tarifi yazmak yerine, uyarıyı görmeyecek bir
   kurulum yolu sun. Yanlış pozitif çıkarsa `microsoft.com/wdsi/filesubmission`
   üzerinden SmartScreen ürünü seçilerek bildir — bedava ve belgelenmiş yol.

3. **İlk paket yöneticisi kanalı Scoop Extras olsun.** Runly GUI olduğu ve 1 yıldızı
   olduğu için Main kriterlerini karşılamıyor; Extras'a tek JSON manifest yeterli.
   Ön koşul: her release'e zip'in yanına `<zip-adı>.sha256` dosyası koymak — `build.ps1`
   hash'i zaten hesaplıyor, sadece dosyaya yazıp release'e eklenecek. Manifeste
   `checkver` + `autoupdate` konursa sonraki sürümler kendiliğinden güncellenir.

4. **`.github/workflows/release.yml` yaz; sürümü etiketten üret.** Tetikleyici `v*`
   etiketi; sürüm numarası etiketten okunur ve `Directory.Build.props`'a derleme
   sırasında yazılır (iki yerde elle sürüm tutma sorunu böyle biter). Release önce
   **taslak** açılsın, notlar `generateReleaseNotes` ile üretilsin, yayınlama ayrı bir
   adımda onaylansın — gsudo'nun taslak/yayın ayrımı.

5. **`install.ps1` indirdiği zip'in SHA-256'sını doğrulasın.** Şu an doğrulamıyor;
   `Invoke-WebRequest` ile inen dosya doğrudan `Expand-Archive`'e gidiyor. Beklenen hash
   release'teki `.sha256` dosyasından okunur, uyuşmazsa kurulum durur ve indirilen dosya
   silinir. İmza olmadığı sürece kullanıcıya verilebilecek tek bütünlük garantisi budur.

6. **`actions/attest` ile derleme kanıtı ekle** (yeni `attest-build-provenance` değil —
   o artık ince bir sarmalayıcı). Depo public olduğu için bedava. Bu SmartScreen'i
   çözmez; çözdüğü şey "bu zip gerçekten bu commit'ten mi derlendi" sorusudur ve
   README'de `gh attestation verify` satırıyla gösterilebilir. cosign'a gerek yok:
   Authenticode üretmez, Windows kabuğunun baktığı yere yazmaz.

7. **Otomatik güncelleyici yazma; "yeni sürüm var" bildirimi yeter.** Velopack'i alma —
   `Main`'in başında çalışmayı şart koşuyor, bu NativeAOT başlatıcının sahipliğiyle
   çakışır. Velopack'ten alınacak tek şey desen: kontrol / indirme / uygulama üç ayrı
   adım. İmzasız bir uygulamada otomatik güncelleme zaten yeni bir saldırı yüzeyidir.
   Karar imza alındıktan sonra yeniden açılır.

8. **Kurulum yeri değişmesin; dosya ilişkilendirmesini "teklif" olarak tasarla.**
   `%LOCALAPPDATA%\Programs\Runly` kalsın. `HKCU\Software\Classes` altına ProgID ve
   `OpenWithProgids` girdileri yazılabilir — "Birlikte aç" listesinde görünmek için
   yeterli. `UserChoice` anahtarına yazmaya kalkışma: hash korumalı, Microsoft yasaklıyor
   ve Windows ilişkilendirmeyi sıfırlar. Kullanıcıyı Windows'un "Varsayılan uygulamalar"
   ekranına yönlendiren bir buton doğru çözümdür. Ayrıca depo kökündeki altı release
   zip'ini sürüm kontrolünden çıkar — artefakt release'te durur, git'te değil.

---

## Kaynaklar

Depo verileri (2026-08-22, `gh api`):
- https://github.com/velopack/velopack — MIT, 2286★, 56 açık issue, son push 2026-08-21, son sürüm 1.2.0 (2026-06-03)
- https://github.com/gerardog/gsudo — MIT, 6029★, 50 açık issue, son push 2026-08-12, son sürüm v2.6.1 (2025-10-06)
- https://github.com/ScoopInstaller/Scoop — UNLICENSE or MIT, 24577★, 538 açık issue, son sürüm v0.5.3 (2025-08-12)
- https://github.com/ScoopInstaller/Main — Unlicense, 1866★, 122 açık issue, son push 2026-08-22
- https://github.com/microsoft/winget-pkgs — MIT, 10991★, 3337 açık issue, release yok
- https://github.com/vedantmgoyal9/winget-releaser — AGPL-3.0, 307★, son etiket v2 (2025-01-27)
- https://github.com/chocolatey/choco — Apache-2.0, 11489★, 514 açık issue, son sürüm 2.7.4 (2026-08-19)
- https://github.com/sigstore/cosign — Apache-2.0, 6227★, 162 açık issue, son sürüm v3.1.3 (2026-08-06)
- https://github.com/actions/attest-build-provenance — MIT, 1025★, son sürüm v4.2.2 (2026-08-06)
- https://github.com/actions/attest — MIT, 157★, son sürüm v4.2.2 (2026-08-04)

Depo içi belgeler:
- gsudo: `.github/workflows/release.yml`, `.github/workflows/winget-solo.yml`, `build/03-sign.ps1`, README kurulum bölümü
- winget-pkgs: `doc/FirstContribution.md`, `doc/Policies.md`
- Scoop: `ScoopInstaller/Main/bucket/gsudo.json`, Scoop wiki "Criteria for including apps in the main bucket"
- velopack: README, `src/` üst düzey yapısı

Microsoft birincil belgeleri:
- https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/ (güncelleme 2026-04-23) — itibar mekanizması, dosya gönderim formu
- https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart (güncelleme 2026-08-11) — coğrafi uygunluk, kimlik doğrulama süresi
- https://learn.microsoft.com/en-us/azure/artifact-signing/overview (güncelleme 2026-08-03) — HSM, Public/Private Trust

Doğrulanamayan / ticari kaynaklar:
- https://docs.velopack.io/packaging/signing — "EV = anında SmartScreen itibarı" iddiası
- https://azure.microsoft.com/en-in/pricing/details/trusted-signing/ — 9,99 / 99,99 USD aylık fiyatlar (arama sonucundan, sayfadan satır satır doğrulanmadı)
- https://sslcertshop.com/certum-open-source-code-signing ve https://piers.rocks/2025/10/30/certum-open-source-code-sign.html — Certum fiyatları, 459 günlük azami sertifika ömrü, "Microsoft 2026'da EV avantajını kaldırdı" iddiası
- https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator/rules/cpmr0006/ — VERIFICATION.txt kuralı (sayfa doğrudan 403 döndü, içerik arama özetinden alındı)
- https://kolbi.cz/blog/2025/04/20/userchoicelatest-microsofts-new-protection-for-file-type-associations/ — UserChoiceLatest iddiası
