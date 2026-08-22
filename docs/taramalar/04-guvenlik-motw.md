# 04 — Mark of the Web, script çalıştırma güvenliği ve güven kararı arayüzleri

Tarama tarihi: 2026-08-22. Depo sayıları `gh api repos/<owner>/<repo>` ve
`releases/latest` çıktısıdır; davranış iddiaları depo kaynak dosyasından veya resmî
belgeden alınmıştır. Üçüncü taraf blog sayısı kullanılmadı.

Soru etiketleri: **[S1]** MOTW okuma · **[S2]** güvenilen klasör modeli ·
**[S3]** onay diyaloğunun dili · **[S4]** "hiç sorma" modu · **[S5]** denetim izi.

---

## 1. microsoft/vscode — Workspace Trust

MIT · son push 2026-08-22 · son sürüm 1.134.0 (2026-08-19) · 20.115 açık issue ·
189.256 yıldız. Klasör açıldığında "bu klasördeki dosyaların yazarlarına güveniyor
musun" sorusunu soran, güvenilmeyen klasörde özellikleri kısan mekanizma.

- **[S2] Kapsam alt ağaç.** Güven kaydı URI listesidir; bir yol için karar verilirken
  `isEqualOrParent` ile eşleşen tüm kayıtlar taranır ve **en uzun yol kazanır**
  (`src/vs/workbench/services/workspaces/common/workspaceTrust.ts`). Kayıtlarda
  `trusted: true|false` alanı olduğu için güvenilen bir ağacın içindeki tek klasör
  ayrıca güvensiz işaretlenebilir. Runly'nin `trust.json` şeması için doğrudan model.
- **[S2] Üst klasörü güvenmek ayrı bir eylem.** `canSetParentFolderTrust` /
  `setParentFolderTrust`; diyalogda "Trust the authors of all files in the parent
  folder '{0}'" onay kutusu olarak çıkıyor. Kullanıcı yorgunluğunu tek tek klasör
  yerine ağaç güvenerek azaltıyorlar.
- **[S2] Symlink/junction normalizasyonu yok denecek kadar az.** `getCanonicalUri`
  yalnızca remote authority ve `vscode-vfs` şemasını normalize ediyor, query/fragment
  atıyor; **yerel dosya sisteminde symlink veya NTFS junction çözülmüyor**. Güvenilen
  klasörün içine junction koyup dışarıdaki bir ağacı güvenilir gösterme yüzeyi açık
  kalıyor (kaynak: aynı dosya, `getCanonicalUri`).
- **[S2] Güven listesi kullanıcı profilinde.** Klasörün kendi içinde değil, global
  storage anahtarında (`content.trust.model.key`) tutuluyor — güvenilmeyen içerik
  kendi güven kaydını yazamıyor.
- **[S3] Diyalog dili sahiplik üzerinden kurulmuş:** "Do you trust the authors of the
  files in this folder?" · düğmeler "Yes, I trust the authors" (alt satır "Trust
  folder and enable all features") ve "No, I don't trust the authors" (alt satır
  "Open folder in restricted mode"). Her düğmenin altında sonucu anlatan ikinci satır
  var; "Evet/Hayır" tek başına bırakılmamış. Her diyalogda "Learn more" bağlantısı da
  duruyor.
- **[S3] Tek dosya hâli ayrı ele alınmış.** Güvenilen alana güvenilmeyen dosya girerse
  ayrı diyalog: "Do you want to allow untrusted files in this workspace?" — düğmeler
  "Open" / "Open in Restricted Mode" ve **"Remember my decision for all workspaces"**
  onay kutusu. Runly'nin "ilk seferde sor sonra güven" modunun birebir karşılığı bu
  onay kutusu.
- **[S4] Varsayılan artık açılışta sormamak.** `security.workspace.trust.startupPrompt`
  varsayılanı bugün `never` (kaynak: `workspace.contribution.ts`, `default: 'never'`).
  Klasör kısıtlı modda açılıyor; soru ancak güven gerektiren bir özellik kullanılınca
  soruluyor. Kapatma anahtarları ayrı ayrı: `security.workspace.trust.enabled`,
  `...banner`, `...emptyWindow`, `...untrustedFiles`.
- **[S4]/[S5] Geri alma ve görünürlük.** Kısıtlı modda durum çubuğunda kalkan rozeti
  ("Restricted Mode"), üstte banner (`untilDismissed`), ve güvenilen/güvenilmeyen
  klasörlerin tamamını listeleyip silmeye yarayan ayrı bir editör
  (`workspaceTrustEditor`). Karar bir kere verilip kaybolmuyor.
- **Açık talep:** "Workspace Trust - Declared Untrusted Folders" (#126311, hâlâ açık,
  2021-06-14) — kullanıcılar `~/Downloads` gibi klasörleri kalıcı **güvensiz** ilan
  etmek istiyor. Runly'de kara liste, beyaz listenin ucuz eklentisi.

**Runly'ye alınacak fikir:** güven kaydını "yol + trusted bayrağı + kapsam" listesi
yap, kararı en uzun eşleşen yola göre ver, üst klasörü güvenmeyi ayrı onay kutusu
olarak sun, listeyi ayarlarda tam liste olarak göster.

**Kaçınılacak hata:** yolu ham hâliyle karşılaştırmak. Windows'ta junction/symlink ve
`8.3` kısa ad (`PROGRA~1`) yüzünden aynı klasör iki farklı yol gibi görünür; karar
öncesi son hedefi çözüp normalize et.

---

## 2. PowerShell/PowerShell — ExecutionPolicy, Unblock-File, zone kontrolü

MIT · son push 2026-08-20 · son sürüm v7.6.5 (2026-08-14) · 1.603 açık issue ·
55.056 yıldız. Belgeler ayrı depoda: MicrosoftDocs/PowerShell-Docs (lisans alanı
`NOASSERTION`, son push 2026-08-20).

- **[S1] MOTW okuma yolu.** `Unblock-File` "Zone.Identifier alternatif veri akışını
  siler; akışın değeri `3` dosyanın internetten indirildiğini gösterir" (Unblock-File
  belgesi). Tespit için `Get-Item -Stream Zone.Identifier` öneriliyor. MOTW okuması =
  ADS okuması, ZoneId=3 = internet.
- **[S1] Yanlış pozitifler belgelenmiş ve gerçek.**
  - UNC/ağ payı: belge notu, "UNC yollarını internet yolundan ayırt edemeyen
    sistemlerde UNC ile tanımlanan scriptler RemoteSigned altında çalışmayabilir"
    diyor. Issue #7458 (kapalı, 2018) tam bu: ana bilgisayar adında nokta olan bir
    paylaşımdaki profil internet bölgesi sayılıp güvenlik uyarısı üretiyor.
  - Issue #13869 (kapalı, 2020, `WG-Security` etiketli): yerel ağdaki sunucuda duran
    dosyalarda RemoteSigned davranışı sürümler arasında farklı, `Unblock-File`
    beklendiği gibi çalışmıyor.
  - Belge, `curl.exe`, `Invoke-RestMethod`, `Invoke-WebRequest` ile indirilen
    dosyaların **MOTW almadığını** açıkça yazıyor — MOTW yokluğu güvenlik kanıtı değil.
  - Zone kontrolü Windows kabuk API'lerine (`explorer.exe`) bağlı; kabuk yoksa veya
    hazır değilse (Server Core, oturum açılışındaki logon script) `AuthorizationManager
    check failed` hatası geliyor. Runly açılışta veya servis bağlamında çalışacaksa
    aynı tuzağa düşer.
- **[S3] Prompt dili ve seçenekleri** (`resources/Authenticode.resx`): başlık "Security
  warning", gövde "Run only scripts that you trust. While scripts from the internet can
  be useful, this script can potentially harm your computer..." ve **dört** seçenek:
  `Never run` / `Do not run` / `Run once` / `Always run` — her birinin altında ne
  olacağını anlatan bir cümle ("Run the script from this publisher now, and continue to
  prompt me..."). Runly'nin üç modu bu dörtlüyle neredeyse aynı; eksik olan "Never run"
  (kalıcı ret) ve seçenek başına açıklama.
- **[S4] Bypass'ın çerçevesi net.** Belge: `Bypass` = "hiçbir şey engellenmez, uyarı ve
  soru yoktur", amacı "PowerShell'in daha büyük bir uygulamaya gömülü olduğu ya da
  kendi güvenlik modeli olan" senaryolar. Üstte tek cümlelik dürüstlük: "The execution
  policy isn't a security system that restricts user actions" — kullanıcı script
  içeriğini komut satırına yazarak zaten atlatabilir. Runly'nin konumlanması da bu
  olmalı: kaza önleyici, saldırgan engelleyici değil.
- **[S5] Denetim izi ayrı bir özellik.** Script Block Logging, olay kimliği `4104`,
  Windows olay günlüğüne yazıyor; belge "tanılama dışı amaçlarla kullanacaksanız
  Protected Event Logging'i açın" diyor — çünkü **log scriptin içeriğini, yani sırları
  da kaydediyor**.

**Runly'ye alınacak fikir:** MOTW'yi tek karar girdisi sayma; "MOTW yok" durumunu
"temiz" değil "bilinmiyor" diye etiketle.

**Kaçınılacak hata:** zone kararını kabuk API'sine bağlayıp hata hâlini tanımsız
bırakmak. ADS okunamadıysa (ağ sürücüsü, FAT/exFAT, izin yok) sonuç "güvenli" değil
"kontrol edilemedi" olmalı ve kullanıcıya öyle görünmeli.

---

## 3. microsoft/winget-cli — MOTW'yi yazan taraf ve ek tarama

MIT · son push 2026-08-20 · son sürüm v1.29.280 (2026-06-24) · 1.305 açık issue ·
26.333 yıldız. İndirdiği kurulum dosyasına MOTW **uygulayan** paket yöneticisi;
Runly'nin okuduğu damgayı üreten tarafın referans uygulaması.

- **[S1] İki katmanlı yaklaşım** (`src/AppInstallerCommonCore/Downloader.cpp`):
  1. `ApplyMotwIfApplicable` — `CLSID_PersistentZoneIdentifier` COM nesnesiyle
     `URLZONE_INTERNET` yazıyor. Dosya sisteminin ADS destekleyip desteklemediğini
     önce `FileSupportsMotw` ile sınıyor; desteklemiyorsa **atlıyor ama logluyor**.
  2. `ApplyMotwUsingIAttachmentExecuteIfApplicable` — `CLSID_AttachmentServices` /
     `IAttachmentExecute` ile kaynak URL'i bildirip `Save()` çağırıyor; bu çağrı
     Windows'un ek tarama zincirini (SmartScreen/AV) tetikliyor. **Tarama başarısız
     olursa dosyaya istenen bölge damgası yine basılıyor** — hata hâlinde güvenli
     tarafa düşüyorlar.
- Ayrıntı: `Save()` öncesi mevcut MOTW temizleniyor, çünkü kaynak URL güvenilir
  sayılırsa servis mevcut damgayı silmiyor. Temizleme başarısız olursa iş iptal
  edilmiyor, uyarı loglanıp devam ediliyor.
- Attachment servisi STA istediği için ayrı iş parçacığı açılıp
  `CoInitializeEx(COINIT_APARTMENTTHREADED)` yapılıyor ve `join` ediliyor. Runly'nin
  .NET/WinForms tarafında aynı COM kısıtı geçerli; arayüz iş parçacığından çağırmak
  donmaya yol açar.
- **[S5]** Her adım tek satır log olarak yazılıyor ("Started applying motw...",
  "Finished... Result: <hr>"); kararın neden verildiği sonradan okunabiliyor.

**Runly'ye alınacak fikir:** MOTW'yi elle ADS ayrıştırarak değil `IZoneIdentifier` /
`IAttachmentExecute` COM arayüzleriyle oku; SmartScreen/AV verdikti bedavaya gelir.
Her adımı tek satır logla.

**Kaçınılacak hata:** ADS desteklemeyen dosya sistemini (FAT32 USB, bazı ağ payları)
hata gibi ele almak. winget bunu ayrı bir dal olarak tanıyor.

---

## 4. git-for-windows/git — safe.directory

Lisans `NOASSERTION` (üst proje GPL-2.0; GitHub karma lisansı çözemiyor) · son push
2026-08-21 · son sürüm v2.55.0.windows.5 (2026-08-20) · 231 açık issue · 9.365 yıldız.
CVE-2022-24765 sonrası eklenen "sahibi başkası olan depoya dokunma" kapısı.

- **[S2] Kapsam sözdizimi belgede net** (`Documentation/config/safe.adoc`): tek yol =
  o dizin; `/*` eklenmiş yol = **altındaki tüm depolar**; `*` = kontrolü tamamen kapat;
  **boş değer = listeyi sıfırla**. Runly'nin üç kapsam düzeyi (dosya / klasör / alt
  ağaç) ve bir "sıfırla" kaydı için hazır model.
- **[S2] Güven kaydı korumalı yapılandırmada tutuluyor.** "This config setting is only
  respected in protected configuration... This prevents untrusted repositories from
  tampering with this value" — güvenilmeyen artefaktın kendi güven kaydını yazamaması
  açık bir tasarım kuralı olarak yazılmış.
- **[S3] Hata mesajı doğrudan çözümü veriyor** (`setup.c`): "detected dubious ownership
  in repository at '<yol>' ... To add an exception for this directory, call: git config
  --global --add safe.directory <yol>". Kullanıcıya soru değil **kopyalanabilir tek
  komut** veriliyor, yol tırnaklanarak basılıyor.
- **[S4] "Hiç sorma"nın geri dönüşü belgede yazılı:** `safe.directory=*` sistem
  yapılandırmasındaysa korumayı geri açmak için listeyi boş değerle başlatman
  gerektiği anlatılıyor. Tehlikeli anahtarın yanına geri alma tarifi konmuş.
- **Yanlış pozitif dalgası gerçek:** #3798 "Windows User Groups not counted for new
  repository ownership rules" (51 yorum), #3786 "Cannot add network locations to
  safe.directory", #3809 "Disabling safe directory checks (safe.directory = *) is not
  working", #3955 "Administrators SID için beklenmedik ret" — hepsi 2022 Nisan-Temmuz,
  hepsi kapalı. Yol/sahiplik tabanlı bir kapı Windows'ta ilk sürümde ağ sürücüsü, grup
  SID'i ve joker karakter davranışından patlıyor.

**Runly'ye alınacak fikir:** engelleme ekranının içine "bu klasörü güvenilir yapmak
için şuna tıkla" satırını koy ve toptan kapatmanın nasıl geri alınacağını aynı ekranda
yaz.

**Kaçınılacak hata:** güven listesini korumasız bir dosyada tutup, ona yazma yetkisi
olan her scriptin kendini beyaz listeye eklemesine izin vermek.
`%APPDATA%\Runly\trust.json` tam olarak bu risk altında.

---

## 5. denoland/deno — izin modeli ve prompt tasarımı

MIT · son push 2026-08-22 · son sürüm v2.9.5 (2026-08-06) · 1.528 açık issue ·
108.275 yıldız. Varsayılanı "hiçbir şeye izin yok" olan runtime; izin sorusunu en çok
düşünmüş proje.

- **[S3] Prompt gövdesi** (`runtime/permissions/prompter.rs`): "Deno requests <x>
  access to ..." + "Run again with --allow-<name> to bypass this prompt." + "Learn more
  at: <bağlantı>" + seçenekler `[y/n/A] (y = yes, allow; n = no, deny; A = allow all
  <name> permissions)`. Soru, kalıcı çözüm ve öğrenme bağlantısı aynı ekranda.
- **[S3] Körlemesine "evet"e karşı üç somut önlem, hepsi kaynakta:**
  1. Prompt açılmadan önce **stdin boşaltılıyor** (`clear_stdin`) — "önceden
     tamponlanmış veri prompt'u etkilemesin diye"; boşaltma başarısız olursa izin
     **verilmiyor**.
  2. Mesajdaki kullanıcı denetimindeki metin (dosya yolu, env adı) kontrol
     karakterlerinden arındırılıyor (`escape_control_characters`) — terminal
     sahteciliğine karşı.
  3. Mesaj 10 KB'ı aşarsa istek reddediliyor: "This may indicate that code is trying to
     bypass or hide permission check requests."
- **[S3]/[S5] "Kim istedi" sorusu:** prompt, isteği tetikleyen çağrı yığınını
  gösterebiliyor; kapalıyken satır olarak "To see a stack trace for this prompt, set
  the DENO_TRACE_PERMISSIONS environmental variable." çıkıyor.
- **[S4] Kapalı kapı varsayılanı.** stdin/stderr terminal değilse prompt hiç
  gösterilmiyor, sonuç doğrudan `Deny`. stdin raw modda ise (bir kütüphane
  `setRawMode` çağırmışsa) donmak yerine açıklamalı hata verip `Deny`. Belirsizlikte
  reddetmek kural.
- **[S4] `-A` / `--allow-all` çerçevesi:** resmî belge "sandbox'ı tamamen kapatır, Node
  ile aynı erişimi verir" diyor; `--deny-*` bayrakları `--allow-*`'ı eziyor, yani "her
  şeye izin ver ama şurası hariç" ifade edilebiliyor.
- **[S5] Merkezî karar mercii:** belgede `DENO_PERMISSION_BROKER_PATH` ile dış bir
  sürecin izin isteklerini JSON şemasıyla alıp yanıtlaması anlatılıyor. Hangi sürümde
  geldiği ve kararlılık durumu bu taramada **doğrulanamadı** (yalnızca
  docs.deno.com sayfasından okundu, kaynak dosyayla eşleştirilmedi).

**Runly'ye alınacak fikir:** diyalog açılmadan önce girdi tamponunu temizle ve
diyalogda gösterilen dosya yolunu/eşleşen deseni kaçışla — hem "Enter'a basılı tutma"
refleksini hem yol sahteciliğini keser.

**Kaçınılacak hata:** karar verilemeyen durumda (arayüz açılamadı, oturum etkileşimsiz,
ayar dosyası bozuk) çalıştırmaya devam etmek. Deno bu hâllerin hepsinde reddediyor.

---

## 6. microsoft/terminal — tehlikeli işlemde uyarı diyaloğu

MIT · son push 2026-08-21 · son sürüm v1.24.11911.0 (2026-07-16) · 1.739 açık issue ·
104.668 yıldız. Güven listesi yok; ama "yapıştırdığın şey komut çalıştırabilir"
uyarısını en olgun kuran arayüz.

- **[S3] Uyarı metni sonucu anlatıyor, eylemi değil:** "You are about to paste text
  that contains multiple lines. If you paste this text into your shell, it may result
  in the unexpected execution of commands. Do you wish to continue?" Düğme "Paste
  anyway" — nötr "OK" değil, ne olacağını söyleyen fiil.
- **[S3] İçerik önizlemesi diyalogda:** "Clipboard contents (preview):" ve "More
  options" satırları var; kullanıcı neye onay verdiğini görüyor. Runly'nin "içerikte
  şüpheli desen bulundu" ekranı için birebir alınacak desen.
- **[S3] Boyut eşiği:** "You are about to paste text that is longer than 5 KiB." —
  uyarı her yapıştırmada değil, riskli eşikte çıkıyor.
- **[S4] Anahtarlar isimlendirilmiş ve ayrı:** `warning.multiLinePaste`,
  `warning.largePaste`, `warning.confirmOnClose`, `warning.inputService`
  (`MTSMSettings.h`). Çoklu satır uyarısının varsayılanı `Automatic` — tek bir aç/kapa
  değil, bağlama göre karar veren üçüncü bir değer var.

**Runly'ye alınacak fikir:** onay ekranında **kanıtı göster** — dosyanın ilk satırları
ve eşleşen şüpheli desen; düğme yazısı "Evet" değil "Yine de çalıştır".

**Kaçınılacak hata:** tek bir "uyarıları kapat" anahtarı. Uyarı türü başına ayrı
anahtar, kullanıcının hepsini birden kapatmasını zorlaştırır.

---

## 7. MicrosoftDocs/sysinternals — Streams (ADS okuma/silme aracı)

CC-BY-4.0 (belge deposu; aracın ikilisi kapalı kaynak, EULA ayrı) · son push
2026-08-19 · etiketli sürüm yok · 136 açık issue · 584 yıldız. `streams.exe`'nin resmî
belgesi; Runly'nin okuduğu akışı gözle doğrulamak için referans araç.

- **[S1] Ne yaptığı açık:** dosya **ve dizinlerin** alternatif akışlarını listeler,
  `-s` özyineli, `-d` akışları siler. Belge "Streams makes use of an undocumented
  native function for retrieving file stream information" diyor — akış
  *numaralandırmak* için belgelenmiş API yolu yok; Runly `<dosya>:Zone.Identifier`
  yolunu doğrudan açmalı, numaralandırmaya kalkışmamalı.
- **[S1] Dizinlerin de ADS'i olabiliyor.** Klasör güvenme kararında bu göz ardı
  edilirse klasöre yapıştırılmış bir damga kaçar.
- **[S5] Teşhis aracı olarak değerli:** kullanıcı şikâyetinde "`streams -s <klasör>`
  çıktısını gönder" demek, kendi teşhis ekranını yazmadan önceki en ucuz doğrulama.
- Araç v1.6, belge 2016 yayınlı ve 2020-09-17 güncellenmiş — MOTW tarafında yeni bir
  şey öğretmiyor, temeli doğruluyor.

**Runly'ye alınacak fikir:** "bu dosyada ne var" teşhis düğmesi — `Zone.Identifier`
akışının ham içeriğini (ZoneId, ReferrerUrl, HostUrl satırları) kullanıcıya göster;
kararın gerekçesi görünür olur.

**Kaçınılacak hata:** yalnızca dosyaya bakıp klasör ADS'ini ve `ReferrerUrl` /
`HostUrl` alanlarını yok saymak; ZoneId tek başına "nereden geldi" sorusunu
cevaplamıyor.

---

## 8. AutoHotkey/AutoHotkey — güvenlik duruşu olmayan çift tıklama hedefi

GPL-2.0 · son push 2026-08-16 · son sürüm v2.0.26 (2026-05-04) · 22 açık issue ·
12.992 yıldız. Belgeler ayrı depoda: AutoHotkey/AutoHotkeyDocs — **lisans alanı boş**,
metin kopyalamaya uygun değil. `.ahk` dosyası çift tıklanınca doğrudan çalışıyor; yani
Runly ile aynı konumdaki uygulama.

- **[S1]/[S3] Kendi güven kapısı yok.** AHK, script çalıştırmadan önce MOTW'ye bakan
  veya onay soran bir katman sunmuyor; tek engel Explorer'ın kendi ek dosya uyarısı.
  Runly'nin var olma sebebi tam olarak bu boşluk.
- **[S4] Yükseltme sorusu ayrı tuzak.** SSS, UAC engellerini aşmak için "Run with UI
  access" (AutoHotkey'in Program Files altına kurulu olmasını gerektiriyor) veya
  yönetici olarak çalıştırmayı öneriyor; ikincisi için "scriptin başlattığı tüm
  programlar da yönetici olarak çalışır" uyarısı var. "Run all administrators in Admin
  Approval Mode" politikasını kapatmak "önerilmez" notuyla listelenmiş. Runly
  yükseltilmiş bağlamı asla varsayılan yapmamalı.
- **Zararlı yazılım algılanması kronik:** SSS'de ayrı başlık — "çoğu zaman bu uyarılar
  yanlış pozitiftir", sıkıştırılmış (UPX/MPRESS) derlenmiş scriptlerde daha sık. AV
  motorları script çalıştıran araçları şüpheli görüyor; Runly imzasız dağıtılırsa aynı
  kaderi paylaşır.
- **[S5] Denetim izi yok.** Hangi scriptin ne zaman çalıştırıldığına dair yerleşik
  kayıt yok; sorun giderme "hata kutusuna bak" seviyesinde.

**Kaçınılacak hata:** güvenliği tamamen işletim sistemine bırakmak ve kullanıcıya tek
çıkış yolu olarak "yönetici olarak çalıştır" demek. Runly'nin tersini yapması
gerekiyor: hiç yükseltmeden karar ver, kararı kaydet.

---

## Runly için sonuç

1. **Karar girdisi tek değil üçlü olsun, gerekçe kayda geçsin.** MOTW (ZoneId,
   ReferrerUrl, HostUrl), içerik deseni ve klasör güveni ayrı değerlendirilip
   diyalogda hangi girdinin tetiklediği yazılmalı. `curl` / `Invoke-WebRequest` ile
   inen dosya MOTW almadığı için "damga yok = güvenli" kuralı yanlış.
2. **Okunamayan MOTW "temiz" değil "bilinmiyor".** FAT32/USB, bazı ağ payları, izin
   hatası ve kabuk API'sinin hazır olmadığı durumlar var (PowerShell'de
   `AuthorizationManager check failed`). Bu üçüncü durum arayüzde de üçüncü bir hâl
   olarak görünsün, sessizce izin verilmesin.
3. **`trust.json` şemasını VS Code + git karışımı yap:** her kayıt `{yol, trusted,
   kapsam}`; kapsam = dosya | klasör | alt ağaç; karar en uzun eşleşen yola göre;
   `trusted:false` kayıtları da desteklensin (Downloads'u kalıcı güvensiz ilan etme —
   VS Code'da hâlâ açık talep #126311).
4. **Yolu karar öncesi normalize et.** Symlink/junction hedefini çöz, `8.3` kısa adı
   uzun ada çevir, sondaki ayırıcıyı temizle. VS Code bunu yerel dosya sisteminde
   yapmıyor ve atlatma yüzeyi orada duruyor; aynı boşluğu kopyalama.
5. **Güven dosyasını kurcalamaya karşı koru.** git'in "protected configuration"
   kuralının karşılığı: `trust.json` yalnız kullanıcı ACL'iyle yazılabilir olsun,
   dosya bozuksa veya bütünlük kontrolü tutmuyorsa **listeyi yok say ve her şeyi sor**
   (fail-closed).
6. **Diyalog: kanıt + sonuç + kalıcı çözüm.** Üstte "şu dosya şu kaynaktan indirilmiş"
   satırı, ortada eşleşen satırın önizlemesi (Terminal deseni), düğmeler "Yine de
   çalıştır" / "Çalıştırma" (varsayılan odak: çalıştırma), altta "bu klasöre hep güven"
   onay kutusu ve tek satırlık açıklama bağlantısı. Diyalog açılırken girdi tamponunu
   temizle (Deno `clear_stdin`), gösterilen yolu kontrol karakterlerinden arındır.
7. **"Hiç sorma" modunu geri dönüşüyle birlikte sun.** Açıkken ana pencerede kalıcı
   rozet (VS Code kalkanı), ayarlarda tek tıkla kapatma, mod açılırken tek cümlelik
   dürüst çerçeve: bu bir güvenlik sınırı değil kaza önleyicidir (PowerShell'in
   "execution policy isn't a security system" cümlesinin karşılığı). "Tümünü kapat"
   yerine uyarı türü başına anahtar (Terminal deseni).
8. **Denetim izi yerel, sınırlı ve görünür olsun.** `%APPDATA%\Runly\` altında dönen
   bir log: zaman, dosya yolu, ZoneId, tetikleyen kural, kullanıcı kararı. Script
   **içeriğini yazma** — PowerShell belgeleri script block logging'in sır sızdırdığını
   ve şifreleme gerektirdiğini söylüyor. Ayarlarda "son kararlar" listesi ve tek tıkla
   ilgili güven kaydını silme.

---

## Kaynaklar

- microsoft/vscode — `gh api repos/microsoft/vscode`;
  `src/vs/workbench/services/workspaces/common/workspaceTrust.ts`;
  `src/vs/workbench/contrib/workspace/browser/workspace.contribution.ts`; issue #126311;
  https://code.visualstudio.com/docs/editing/workspaces/workspace-trust
- PowerShell/PowerShell — `gh api repos/PowerShell/PowerShell`;
  `src/System.Management.Automation/resources/Authenticode.resx`; issue #7458, #13869, #6114
- MicrosoftDocs/PowerShell-Docs — `about_Execution_Policies.md`,
  `about_Logging_Windows.md`, `Unblock-File.md` (7.6 dalı)
- microsoft/winget-cli — `gh api repos/microsoft/winget-cli`;
  `src/AppInstallerCommonCore/Downloader.cpp`
- git-for-windows/git — `gh api repos/git-for-windows/git`;
  `Documentation/config/safe.adoc`; `setup.c`; issue #3786, #3798, #3809, #3955
- denoland/deno — `gh api repos/denoland/deno`; `runtime/permissions/prompter.rs`;
  https://docs.deno.com/runtime/fundamentals/security/
- microsoft/terminal — `gh api repos/microsoft/terminal`;
  `src/cascadia/TerminalSettingsModel/MTSMSettings.h`;
  `src/cascadia/TerminalApp/Resources/en-US/Resources.resw`
- MicrosoftDocs/sysinternals — `sysinternals/downloads/streams.md`
- AutoHotkey/AutoHotkey — `gh api repos/AutoHotkey/AutoHotkey` ve `.../AutoHotkeyDocs`;
  https://www.autohotkey.com/docs/v2/FAQ.htm
