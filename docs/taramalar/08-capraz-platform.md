# 08 — Çapraz platform ilişkilendirme modelleri ve tür katalogları

Tarama tarihi: 2026-08-22. Kapsam: Windows dışı dosya ilişkilendirme modelleri ve
MIME/uzantı veritabanları. Amaç, Runly'nin 408 kayıtlık gömülü kataloğunu ve
"birlikte aç" arayüzünü kıyaslamak.

Runly'nin bugünkü durumu (depodan doğrulandı, `src/Runly.Settings/Catalog/catalog.json`):
408 kayıt, 14 kategori, kayıt başına alanlar `extension`, `category`, `displayName{tr,en}`,
`defaultKind`, `suggestedApps`, `blocked`, `riskNote`. 16 kayıt `blocked: true` ve
`locked` kategorisinde; `riskNote` yalnız o 16 kayıtta dolu. `defaultKind` dağılımı:
378 `Open`, 30 `Run`. Depo lisansı MIT (`LICENSE`).

---

## 1. freedesktop.org — shared-mime-info

Linux/BSD dünyasının ortak tür veritabanı: MIME türü, dosya adı deseni, içerik imzası,
insan okunur açıklama ve ikon adı tek XML'de.

**Künye (doğrulandı, GitLab API):** gitlab.freedesktop.org/xdg/shared-mime-info, proje id
1205. Son etkinlik 2026-08-11. Son etiket **2.5.1 (2026-06-29)**, ondan önceki 2.4
(2023-11-12) — yani iki buçuk yıl sessizlikten sonra sürüm çıkmış. Açık issue **62**.
**Lisans: GPL-2.0** (`COPYING` dosyası GNU GPL v2 metni).

**Veri hacmi (doğrulandı, `data/freedesktop.org.xml.in` indirilip sayıldı):** 1040
`mime-type` kaydı, 1443 `glob` deseni, 658 `magic` bloğu, 578 `sub-class-of` bağı,
505 `generic-icon`, 351 `alias`. `po/` altında 78 dil dosyası; `tr.po` içinde 1019
`msgstr`, bunlardan yalnız 11'i boş — yani ~1008 tür açıklaması Türkçeye çevrilmiş.

**Alınacak fikir — `sub-class-of` ile tür hiyerarşisi.** `application/x-dosexec`,
`application/x-msdownload`'un alt türü; o da `application/x-executable`'ın alt türü.
Runly'de `blocked` tek tek uzantıya yazılmış bayrak; bunun yerine "yürütülebilir",
"betik", "kısayol" gibi bir risk sınıfı ağacı olsa yeni bir uzantı eklendiğinde koruma
otomatik miras alınırdı.

**Alınacak fikir — `glob` üzerinde ağırlık (`weight`).** Aynı uzantıya birden fazla tür
talip olduğunda ağırlık kararı veriyor (dosyada 17 adet `weight="10"`, 29 adet
`weight="40"` vb.). Runly'de `.js` hem betik hem web olabilir; kategori çakışmasını
ağırlıkla çözmek elle sıralamaktan sağlam.

**Kaçınılacak hata — lisans.** GPL-2.0. Runly MIT. `freedesktop.org.xml` içindeki
`<comment>` metinlerini veya `po/tr.po` çevirilerini kopyalayıp MIT bir ikiliye gömmek
lisans uyumsuzluğu üretir. Türkçe tür adlarını buradan **almayın**. (Ham olgu verisinin
telif kapsamı ayrı bir tartışma; ama metin ve çeviri açıkça telifli. Hukuki değerlendirme
yapılmadı — `doğrulanamadı`.)

## 2. freedesktop.org — mime-apps ve shared-mime-info spesifikasyonları

İlişkilendirmenin nasıl saklanacağını ve bir dosyanın türünün nasıl bulunacağını tarif
eden iki ayrı belge.

**Alınacak fikir — üç ayrı bölüm: varsayılan, eklenen, kaldırılan.** `mimeapps.list`
içinde `[Default Applications]`, `[Added Associations]` ve `[Removed Associations]`
ayrı gruplar. Ekleme/kaldırma "sanki .desktop dosyası bu türü baştan listeliyormuş/
listelemiyormuş gibi" davranıyor; varsayılan ise ayrı bir kayıt. Runly bugün tek bir
"bu uzantı → bu uygulama" eşlemesi tutuyor; kullanıcının *gizlediği* uygulamayı ayrı
tutmak, "önerilenler" listesini kirletmeden kişiselleştirme sağlar.

**Alınacak fikir — varsayılan bir liste, tek değer değil.** `[Default Applications]`
noktalı virgülle ayrılmış sıralı liste. İlk aday kurulu değilse veya türü desteklemiyorsa
sıradaki denenir; hiçbiri tutmazsa ilişkilendirme listesinden en uygunu seçilir.
Runly'de kullanıcının atadığı uygulama silindiğinde bugün ne olduğu belirsiz; sıralı
yedek listesi bu boşluğu kapatır.

**Alınacak fikir — kurulu olmayan hedefe yapılan atama sessizce yok sayılır.** Spec:
ekleme/kaldırma, o öncelik seviyesinde veya altında var olmayan bir .desktop dosyasına
işaret ediyorsa yok sayılır. Yani "bozuk atama" hata değil, görmezden gelinen kayıt.

**Alınacak fikir — tür tespitinde sıra.** shared-mime-info spec'i: açık bir tür bilgisi
varsa (HTTP başlığı, genişletilmiş öznitelik) tahmin etme, onu kullan. Yoksa önce ad
deseni, sonra içerik imzası — çünkü "magic sniffing çok pahalı, dosya içeriğini okumak
çok sayıda seek üretir". Ad deseni birden çok tür veriyorsa içeriğe bak. İçerikten çıkan
tür, ad deseninden çıkanlardan birine eşit veya onun üst sınıfıysa onu kullan.

## 3. xdg-utils (`xdg-mime`, `xdg-open`)

Masaüstü ortamından bağımsız komut satırı katmanı: türü sorgular, varsayılanı okur/yazar,
dosyayı varsayılanla açar.

**Künye (doğrulandı, GitLab API):** proje id 1212, son etkinlik 2026-08-10. Son etiket
**v1.2.1 (2024-02-06)**. Açık issue **115**. **Lisans: MIT** (`LICENSE` dosyası MIT metni).

**Alınacak fikir — tek arayüz, çok arka uç.** `xdg-mime` içeride masaüstü ortamını
tespit edip KDE ise `kmimetypefinder`, değilse başka yol izliyor; sonuçta çağıran taraf
tek komut görüyor. Runly'de aynı ayrım var: `HKCU\...\FileExts` UserChoice mı,
`OpenWithProgids` mi, Runly'nin kendi katmanı mı — dışarı tek bir "ata/oku/kaldır"
yüzeyi çıkarmak test edilebilirliği artırır.

**Alınacak fikir — yazma işleminden sonra önbellek tazeleme adımı ayrı.**
`update_mime_database` ve KDE için `kbuildsycoca` çağrısı, atama işleminin ayrılmaz
son adımı. Windows'ta karşılığı `SHChangeNotify`; Runly'de atama sonrası kabuk bildirimi
atlanırsa kullanıcı "olmadı" der. Ayrı ve her yolda çağrılan bir adım olmalı.

**Kaçınılacak hata — 115 açık issue ve 2024'ten beri etiketsiz sürüm.** Kabuk betiği
tabanlı, her masaüstü için ayrı dal içeren yapı bakımı zor hale getirmiş. Runly'de her
Windows sürümü için ayrı kod dalı açmak aynı çukurdur.

## 4. moretension/duti (macOS, UTI tabanlı atama)

macOS'ta belge türleri ve URL şemaları için varsayılan uygulamayı komut satırından
ayarlayan araç; uygulamayı bundle id, türü UTI ile adresliyor.

**Künye (doğrulandı, GitHub API):** 2056 yıldız, açık issue **27**, son commit
**2023-07-09**, son etiket `duti-1.5.4` (GitHub'da yayımlanmış release yok — `releases/latest`
404 döndü). **Lisans: kamu malı beyanı** (`COPYRIGHT`: "released into the public domain",
GitHub bunu `NOASSERTION` olarak işaretliyor). OSI onaylı bir lisans **değil**; marka
koruması yok.

**Alınacak fikir — rol ayrımı.** Atama üçlü: bundle id + UTI + rol (`all`, `viewer`,
`editor`). "Görüntüleyici" ile "düzenleyici" farklı uygulamalar olabiliyor. Runly'nin
`defaultKind` alanı (`Open`/`Run`) bunun ilkel hali; `Open` ile `Edit` ayrımı eklemek
düşük maliyetli ve kullanıcıların gerçekten istediği ayrım.

**Alınacak fikir — toplu atama girdi dosyası.** duti ayarları stdin'den, düz metin
dosyadan, plist'ten veya argümandan okuyabiliyor; dizin verilirse içindeki tüm ayar
dosyalarını uyguluyor. Runly için "profil dosyası" (bir geliştirici makinesindeki tüm
atamaları tek dosyayla kur) bunun doğrudan karşılığı.

**Kaçınılacak hata — işletim sistemi sürümüne sabitlenmiş kurulum.** Açık issue
başlıkları doğrudan bunu gösteriyor: #58 "darwin23.2.0 is not a supported system"
(2024-08-31), #63 "darwin24.5.0 is not a supported system" (2025-06-26), #64
"Add support for macOS 14, 15, and future darwin versions" (2025-11-07). `configure`
betiği yeni sürümü tanımayınca araç derlenmiyor. Runly'de Windows build numarasına
sert bağlanan hiçbir kontrol olmamalı; bilinmeyen sürümde çalışmayı reddetmek yerine
uyarıp devam etmeli.

**Kaçınılacak hata — sessiz başarısızlık.** #38 "Finder still shows old default",
#34/#29 "What is error -54?", #56 "Reassociation does not work properly". LaunchServices
hata kodunu geçiriyor ama kullanıcıya ne yapacağını söylemiyor. Runly'nin
`open-handler configuration failures` raporlaması (son commit'lerde var) doğru yönde;
hata kodunu değil eylemi söyleyin.

## 5. Apple Uniform Type Identifiers (model, belgeler)

macOS/iOS'ta tür kimliği; ters alan adı biçiminde bir dize (`com.microsoft.word.doc`),
uzantıya değil içeriğin biçimine ad veriyor.

**Not:** Apple'ın belge sayfaları istemci tarafında derleniyor; otomatik çekimde gövde
metni alınamadı. Aşağıdaki genel model duti README'sinin UTI tanımı ve Apple belge
başlıklarıyla uyumlu; **belirli conformance zincirleri (`public.executable` neye
conform ediyor gibi) bu taramada birincil kaynaktan doğrulanamadı**.

**Alınacak fikir — türün kimliği uzantıdan ayrı.** Bir UTI'ye birden çok uzantı ve
birden çok MIME türü etiket olarak bağlanıyor; biri "tercih edilen uzantı" olarak
işaretleniyor. Runly'de `.jpg`/`.jpeg`/`.jfif` bugün üç ayrı satır; üçünü tek bir tür
kimliğine bağlamak hem katalog bakımını hem "bu türün tüm uzantılarına uygula"
davranışını mümkün kılar.

**Alınacak fikir — yerelleştirilmiş açıklama türün özelliği.** Görünen ad, uzantının
değil türün alanı. Runly'de `displayName{tr,en}` uzantı başına tekrarlanıyor; tür
başına taşınırsa 408 satırlık çeviri yükü belirgin şekilde düşer.

**Kaçınılacak hata — kapalı ve merkezi kayıt.** UTI tanımları uygulama bundle'ları ve
sistem tarafından beyan ediliyor; üçüncü taraf araçların dinamik UTI kaydını doğrulaması
sorun olmuş (duti #65 "Add validation for dynamic UTI registration", 2026-07-27).
Runly'nin kataloğu gömülü olduğu için bu tuzağın hafif bir biçimine açık: katalogda
olmayan uzantı için davranış tanımlı olmalı.

## 6. jshttp/mime-db

Tek bir public JSON dosyası: MIME türü → uzantılar, kaynak, charset, sıkıştırılabilirlik.
Mantık yok, sadece veri.

**Künye (doğrulandı, GitHub API):** 1248 yıldız, açık issue **54**, son push
2026-08-01, son etiket **v1.54.0 (2025-03-18)**. **Lisans: MIT** — Runly ile uyumlu.

**Veri hacmi (doğrulandı, v1.54.0 `db.json` indirilip sayıldı):** 2522 MIME türü;
bunlardan 1015'inde uzantı alanı var; toplam 1239 benzersiz uzantı. Kaynak dağılımı:
2136 `iana`, 275 `apache`, 13 `nginx`, 98 kaynaksız (özel). Üst tür dağılımı:
1886 `application`, 187 `audio`, 132 `text`, 110 `video`, 108 `image`, gerisi kuyruk.

**Alınacak fikir — her kaydın kaynağı işaretli.** `source` alanı kaydın IANA'dan mı,
Apache'ten mi, nginx'ten mi, elle mi geldiğini söylüyor. Runly kataloğunda hiçbir kaydın
nereden geldiği yazmıyor; bir uzantının doğruluğu tartışıldığında dayanak yok.
Kayıt başına `source` alanı, katalog bakımını tartışmadan çıkarıp veriye bağlar.

**Alınacak fikir — veri semver'e dahil değil, açıkça yazılmış.** README: paket
"programmatic api"yi semver kapsamında sayıyor, MIME çözümlemesini saymıyor; veriyi
sabitlemek isteyen tüketici kendi tarafında sabitlemeli. Runly kataloğu da böyle
davranmalı: katalog sürümü ile uygulama sürümü ayrı numaralanmalı.

**Kaçınılacak hata — görünen ad yok.** mime-db'de insan okunur tür adı, kategori,
ikon yok. 1239 uzantı verisi Runly'ye MIT lisansıyla girebilir ama Türkçe/İngilizce
görünen adları ve 14 kategoriyi üretmez. Katalog boşluğunun *yalnız uzantı tarafını*
kapatır.

## 7. sindresorhus/file-type

İçerikten (magic number) tür tespiti yapan kütüphane; dosya, akış veya tampon üzerinde
çalışıyor, `{ext, mime}` döndürüyor.

**Künye (doğrulandı, GitHub API):** 4320 yıldız, açık issue **0**, son push 2026-08-15,
son etiket **v22.0.2 (2026-08-15)**. **Lisans: MIT.** README'deki desteklenen tür
listesinde 186 madde sayıldı (readme'den sayım — kesin API yüzeyi değil).

**Alınacak fikir — kapsam sınırı açıkça yazılı.** README: "bu paket ikili biçimler
içindir, `.txt`, `.csv`, `.svg` gibi metin tabanlı biçimler için değil" ve "yalnız
yaygın modern biçimler kabul edilir, tarihsel veya karanlık olanlar değil". Runly'nin
408 kayıtlık kataloğu için de yazılı bir kabul ölçütü gerekiyor; yoksa katalog her
issue'da büyür.

**Alınacak fikir — tespit sonucu "ipucu" olarak etiketlenmiş.** README, imza tabanlı
tespitin "best-effort hint" olduğunu, dosyanın gerçekten o tür olduğunu veya bozuk
olmadığını garanti etmediğini açıkça yazıyor. Runly içerik tespiti eklerse aynı dili
kullanmalı: kullanıcıya "bu dosya PNG" değil "bu dosya PNG'ye benziyor" denmeli.

**Kaçınılacak hata — güvenlik sınırının dışarı atılması.** README, güvenilmeyen
dosyalarda boyut sınırı ve zaman aşımlı ayrı iş parçacığı kullanılmasını çağırana
bırakıyor ve bunları "bu pakette güvenlik sorunu sayılmaz" diyor. Runly bir masaüstü
uygulaması; sniffing eklerse okunan bayt sayısını kendi sınırlamalı (shared-mime-info
spec'inin "offset olabildiğince küçük olmalı" kuralı aynı yöne bakıyor).

## 8. KDE/kio — `KOpenWithDialog` ve `ExecutableFileOpenDialog`

KDE'nin "birlikte aç" diyaloğu ve yürütülebilir dosya açılırken çıkan onay diyaloğu.

**Künye (doğrulandı, GitHub aynası `KDE/kio`):** son push 2026-08-22, 86 yıldız (ayna
deposu; gerçek geliştirme invent.kde.org'da, GitHub'da issue kapalı — açık issue 0
sayısı anlamlı değil). Depo düzeyinde lisans alanı boş; okunan iki başlık dosyasının
SPDX satırı **LGPL-2.0-or-later**.

**Alınacak fikir — yürütülebilir dosyada üç ayrı mod.** `ExecutableFileOpenDialog`
üç mod tanımlıyor: betikler için "aç veya çalıştır", yerel ikili dosyalar için "yalnız
çalıştır", `.exe` için "aç = çalıştır" (bu durumda "Aç" düğmesi gizleniyor). Bu, Runly'nin
`defaultKind` alanının olması gereken hali: `.ps1`, `.bat`, `.py` gibi 30 `Run` kaydı
için kullanıcıya her seferinde "düzenleyicide aç" ile "çalıştır" arasında seçim
sunulmalı, varsayılan "aç" olmalı.

**Alınacak fikir — politika ile kilitleme (Kiosk `shell_access`).** Başlık dosyasının
sınıf açıklaması: `shell_access` yetkisi verilmemişse diyalogda serbest komut girmek
yasak, kullanıcı yalnız var olan bir çalıştırılabiliri seçebiliyor. Runly'de kurumsal
kullanım düşünülüyorsa "kullanıcı elle komut satırı giremez" tek bir politika anahtarı
olmalı; her diyalogda ayrı kontrol değil.

**Alınacak fikir — "bu tür için hatırla" onayı yeni bir kayıt üretir.**
`setSaveNewApplications` ve sınıf açıklaması: kullanıcı ilişkilendirmeyi hatırlamayı
seçtiğinde, seçtiği uygulama için .desktop dosyası yoksa yeni bir tane oluşturuluyor.
Yani "her zaman kullan" onayı kalıcı ve görünür bir kayıt yaratıyor; sonradan
listelenebiliyor ve geri alınabiliyor. Runly'de "her zaman kullan" işaretinin ürettiği
kayıt ayrı bir listede görünmeli, ayarların içinde kaybolmamalı.

## 9. GNOME — `nautilus` ve GTK/GIO uygulama seçici

Nautilus'un "birlikte aç" akışı ve altındaki `GtkAppChooserWidget` / `GAppInfo` katmanı.

**Künye (doğrulandı, GitHub API):** `GNOME/nautilus` yalnızca okunur ayna
(gitlab.gnome.org/GNOME/nautilus'un aynası), son push 2026-08-21, **lisans GPL-3.0**.
GitHub'da açık issue 0 — ayna olduğu için anlamsız; gerçek sayı GitLab'de,
bu taramada **doğrulanamadı**.

**Alınacak fikir — dört kademeli uygulama listesi.** `GtkAppChooserWidget` uygulamaları
ayrı bölümlere ayırıyor: varsayılan, önerilen, yedek (fallback), diğer. GIO belgesi
ayrımı net yapıyor: `g_app_info_get_recommended_for_type` "içerik türünü tam olarak
destekleyen, MIME alt sınıflaması üzerinden değil" uygulamaları döndürüyor ve
"listenin ilk uygulaması en son kullanılandır". Yedek liste ise alt sınıflama üzerinden
gelenler. Runly'nin `suggestedApps` dizisi tek düz liste; bu dört kademe doğrudan
uygulanabilir: **atanmış → tam eşleşen → kategori üzerinden gelen → tüm uygulamalar**.

**Alınacak fikir — çalıştırılabilir metin dosyası asla otomatik çalıştırılmaz.**
Nautilus'ta `get_default_executable_text_file_action` bugün koşulsuz olarak
"uygulamada aç" döndürüyor (kaynak: `src/nautilus-mime-actions.c`). Eski
"çalıştır / sor / göster" tercihi kaldırılmış. GNOME'un vardığı sonuç: dosya
yöneticisinden çift tıkla betik çalıştırma özelliği, sağladığı kolaylıktan daha çok
risk taşıyor.

**Kaçınılacak hata — elle bakımlı MIME grubu tablosu.** `nautilus-mime-actions.c`
içinde bir `mimetype_groups` tablosu var: "Anything", "Files", "Folders", "Documents",
"Illustration", "Music", "PDF / PostScript", "Picture", "Presentation", "Spreadsheet",
"Text File", "Video" adlı gruplar ve her birine sabit kodlanmış MIME listesi — üstelik
grup başına en fazla 75 tür alan sabit boyutlu dizi ve kaynakta MSDN blog bağlantısına
düşen bir yorum. Bu, shared-mime-info'daki gerçek hiyerarşi varken tutulan ikinci,
elle güncellenen ve kayan bir sınıflandırma. Runly'nin 14 kategorisi tam olarak aynı
riski taşıyor: kategori üyeliği katalogda tek tek yazılı.

**Kaçınılacak hata — API kademesine yaslanmak.** GTK belgesi `GtkAppChooserWidget`
sınıfını GTK 4.10 itibarıyla **kullanımdan kaldırılmış** olarak işaretliyor. On yıldır
"doğru" olan seçici bileşen bile emekliye ayrılıyor; Runly'nin seçim diyaloğu kendi
verisiyle çalışmalı, platformun hazır seçicisine bağlanmamalı.

## 10. AppImage/AppImageKit (masaüstü entegrasyonu ve MIME kaydı)

Uygulamayı tek dosyada paketleyen biçim; masaüstüne kayıt (ikon, .desktop, MIME) isteğe
bağlı ve ayrı bir daemon'un işi.

**Künye (doğrulandı, GitHub API):** 9412 yıldız, açık issue **237**, son etiket
**13 (2020-12-31)** — beş yıl sekiz aydır etiketli sürüm yok. **Lisans: MIT**
(LICENSE metni MIT; GitHub `NOASSERTION` diyor çünkü dosyada "bu lisans AppImage'ların
içeriğine uygulanmaz" şeklinde ek metin var). Depo kökünde bugün yalnız `README.md`,
`LICENSE` ve `motivation.md` var; kod kaldırılmış, son üç commit 2026-07-26 (README
yazım düzeltmesi), 2024-11-28 ×2. **Bağımlılık kurulacak bir proje değil; okunacak bir
tasarım.** Masaüstü entegrasyonu README'de `probonopd/go-appimage` içindeki `appimaged`e
devredilmiş (MIT, son push 2026-06-07, 134 açık issue).

**Alınacak fikir — entegrasyon isteğe bağlı ve geri alınabilir.** README: masaüstü
entegrasyonu "optional". `appimaged` README'sinin kaldırma bölümü tam olarak hangi
dosyaların silineceğini sayıyor: `~/.local/share/applications/appimagekit*.desktop`,
kullanıcı systemd birimi, ikili. Yazılan her kayıt **ön ekle adlandırılmış**, bu yüzden
kaldırma tek desenle yapılabiliyor. Runly'nin kayıt defterine yazdığı her ProgID aynı
ön eke sahip olmalı ki tek geçişte temizlensin.

**Alınacak fikir — taşınabilir kip.** AppImage yanında `.home` veya `.config` klasörü
varsa uygulama ayarlarını oraya yazıyor. Runly için "USB'den çalıştır, kayıt defterine
dokunma" kipi aynı desen: yan klasör varsa yapılandırma oraya.

**Kaçınılacak hata — çekirdek işi başka depoya taşıyıp eski depoyu yaşatmak.** 9412
yıldızlı depo bugün üç dosya; 237 açık issue duruyor; son sürüm 2020. Kullanıcı hâlâ
buraya geliyor. Runly bir bileşeni ayırırsa eski yolun kendisi durum bildirmeli.

---

## Beş soruya cevap

**1. Uzantı mı, tür mü?** Üç sistem de sonunda uzantıya bakıyor — fark, uzantının
*doğrudan hedef* mi yoksa *tür kimliğine giden etiket* mi olduğu. macOS'ta uzantı UTI'ye
çözülür, Linux'ta glob deseni MIME türüne çözülür, atama tür üzerinden yapılır.
Windows'ta ve Runly'de uzantının kendisi hedeftir. Bunun bedeli somut: (a) `.jpg`,
`.jpeg`, `.jfif` üç ayrı kayıt ve üç ayrı atama; (b) "tüm resimlere şunu ata" işlemi
408 satır üzerinde tarama gerektiriyor; (c) aynı uzantıyı paylaşan farklı biçimler
ayrılamıyor. İçerik tespiti yalnız üç yerde gerekli: uzantısız dosya, uzantısı yanlış
dosya, ve tehlikeli tür kontrolü (uzantısı `.txt` ama içeriği PE ikilisi olan dosya).
İlk iki durum Runly'nin ana akışında yok; üçüncüsü güvenlik gerekçesiyle değerli.
shared-mime-info spec'inin sırası doğru sıradır: açık bilgi → ad deseni → yalnız
belirsizlikte içerik.

**2. Katalog bakımı ve lisans.** `shared-mime-info`: 1040 tür, GPL-2.0, katkı
GitLab merge request ile, kurallar `CONTRIBUTING.md`'de yazılı (mümkünse IANA kayıtlı
tür kullan, eski tür kayıt olunca alias ekle, magic offset olabildiğince küçük olsun,
her yeni kayıt test dosyası getirsin), çeviriler Transifex üzerinden.
`mime-db`: 2522 tür / 1239 uzantı, MIT, veri IANA + Apache + nginx listelerinden
otomatik toplanıyor ve her kaydın kaynağı işaretli. **Sonuç: uzantı→MIME eşlemesini
`mime-db`'den türetmek lisans açısından uygundur (MIT, atıf şartıyla).
`shared-mime-info`'dan metin veya çeviri almak Runly'nin MIT lisansıyla uyumsuzdur;
oradan yalnız *tasarım* alınmalı, veri değil.**

**3. Kategori ve görünen ad.** İyi yapan `shared-mime-info`: görünen ad türün alanı,
78 dile çevrilmiş (Türkçe dahil ~1008 kayıt), gruplama kategori listesiyle değil
`sub-class-of` hiyerarşisiyle yapılıyor — yani `image/png`'nin "resim" olduğu ayrı bir
kategori alanından değil, üst türünden geliyor. Kötü yapan Nautilus: 12 elle yazılmış
grup ve grup başına sabit MIME listesi. Runly'nin 14 kategorisi (`special` 43,
`code` 38, `data` 36, `images` 32, `design` 31, `web` 30, `scripts` 30, `office` 30,
`text` 29, `archive` 28, `audio` 26, `video` 24, `locked` 16, `fonts` 15) Nautilus
modeline yakın — yani bakımı elle. Dikkat çeken iki nokta: `special` en kalabalık
kategori (43) ve bu genelde "sınıflandıramadım" anlamına gelir; `locked` bir kategori
değil bir *durum* (16 kayıt hem `blocked: true` hem `locked`), yani aynı bilgi iki
yerde tutuluyor.

**4. "Birlikte aç" arayüzü.** GNOME'un dört kademesi (varsayılan / önerilen / yedek /
diğer) ve "önerilen"in tanımı — türü *tam* destekleyenler, alt sınıflama üzerinden
gelenler değil — en net model. Üstüne GIO'nun "listenin ilki en son kullanılandır"
kuralı arama yapmadan doğru sonucu üste taşıyor. KDE tarafı iki şeyi ekliyor:
serbest komut girme alanı (politikayla kapatılabilir) ve "hatırla" onayının kalıcı,
görünür, geri alınabilir bir kayıt üretmesi. Nautilus'un diyalog başlığı da seçime göre
değişiyor ("Open File" / "Open Folder" / "Open Items") — küçük ama Runly'nin
`ChooseApplicationDialog`'una doğrudan uygulanabilir.

**5. Tehlikeli tür koruması.** Üç ayrı yaklaşım görüldü:
KDE — dosyayı açmadan önce mod bazlı onay diyaloğu (betikte "aç veya çalıştır",
ikilide "yalnız çalıştır", `.exe`'de "Aç" düğmesi gizli) artı Kiosk politikasıyla
serbest komut girişini kapatma. GNOME — özelliği tamamen kaldırma: çalıştırılabilir
metin dosyası için eylem koşulsuz "uygulamada aç". freedesktop veri katmanı — koruma
yok, ama `application/x-executable` altında toplanan hiyerarşi koruma yazmak için
gereken sınıfı veriyor. macOS — koruma LaunchServices/Gatekeeper tarafında, veri
modelinde değil. Runly bugün 16 uzantıyı listeye yazarak koruyor; `.ps1`, `.vbs`,
`.wsf`, `.wsh`, `.hta`, `.jar`, `.js` `defaultKind: Run` ile ve `blocked: false`,
`riskNote` boş. `.hta` ve `.wsf` en az `.scr` kadar tehlikeli; liste tabanlı korumanın
sınırı tam burada görünüyor.

---

## Runly için sonuç

1. **`blocked` bayrağını risk sınıfına çevirin.** Uzantı başına boolean yerine
   `riskClass` (`executable`, `script`, `shortcut`, `installer`, `none`) alanı; koruma
   davranışı sınıftan türesin. Bugün `.exe` korumalı ama `.hta` değil; sınıf tabanlı
   modelde yeni bir betik uzantısı eklendiğinde koruma kendiliğinden gelir.
   Kaynak deseni: shared-mime-info `sub-class-of`.

2. **`defaultKind`'ı üç değere çıkarın: `Open`, `Edit`, `Run` — ve `Run` için onay
   zorunlu olsun.** KDE'nin üç modu (betik → aç/çalıştır sor, ikili → yalnız çalıştır,
   `.exe` → aç=çalıştır) doğrudan karşılığı. Bugünkü 30 `Run` kaydının çoğu (`.ps1`,
   `.py`, `.vbs`, `.hta`) çift tıkla çalışmamalı; varsayılan `Edit` olmalı.
   GNOME bu özelliği tamamen kaldıracak kadar riskli buldu.

3. **`suggestedApps` düz listesini dört kademeye ayırın:** atanmış → türü tam
   destekleyen → kategori üzerinden gelen → tüm uygulamalar. `ChooseApplicationDialog`
   bu bölümleri ayrı başlıklarla göstersin, en son kullanılan üste gelsin.
   Kaynak: `GtkAppChooserWidget` bölümleri + `g_app_info_get_recommended_for_type`.

4. **Atamayı tek değer değil sıralı yedek listesi yapın, kurulu olmayan hedefi sessizce
   atlayın.** Kullanıcının atadığı uygulama silindiğinde Runly bugün ne yapıyor
   belirsiz; mime-apps spec'inin kuralı hazır: listedeki ilk *geçerli* aday kazanır,
   geçersiz kayıt hata değil atlanan kayıttır.

5. **Katalogda `locked` kategorisini kaldırın, `special` kategorisini bölün.**
   `locked` bir durum (`blocked`/`riskClass`), kategori değil — aynı bilgi iki yerde.
   `special` 43 kayıtla en büyük kategori; "sınıflandırılamayan" kutusu büyüdükçe
   kategori rafının değeri düşer. Nautilus'un sabit 12 grubu bu yolun sonudur.

6. **Kayıt başına `source` alanı ekleyin ve katalog sürümünü uygulama sürümünden
   ayırın.** `iana`, `mime-db`, `runly` gibi. mime-db'nin iki dersi: verinin nereden
   geldiği yazılı olmalı, ve veri değişikliği uygulama semver'ine bağlanmamalı.

7. **Uzantı listesini genişletmek gerekirse `jshttp/mime-db`'den türetin — MIT,
   atıfla kullanılabilir; `shared-mime-info`'dan veri veya çeviri almayın — GPL-2.0,
   Runly MIT.** mime-db 1239 benzersiz uzantı taşıyor ama görünen ad, kategori ve ikon
   taşımıyor; Türkçe/İngilizce adlar ve 14 kategori Runly'nin kendi işi olarak kalır.
   Katalog kabul ölçütünü yazıya dökün (file-type'ın "yaygın ve modern biçimler,
   tarihsel olanlar değil" kuralı gibi), yoksa katalog her istekte büyür.

8. **Kayıt defterine yazılan her şey tek ön ekle adlandırılsın ve tek geçişte
   temizlenebilsin; atama sonrası kabuk bildirimi ayrı ve zorunlu adım olsun.**
   AppImage'ın `appimagekit*` deseniyle kaldırma yaklaşımı ve `xdg-mime`'ın
   `update-mime-database`/`kbuildsycoca` adımı aynı dersi veriyor: yazma işlemi,
   önbellek tazeleme ve geri alma birlikte tasarlanır.

---

## Kaynaklar

- shared-mime-info: https://gitlab.freedesktop.org/xdg/shared-mime-info —
  API: `/api/v4/projects/1205`, `COPYING`, `CONTRIBUTING.md`,
  `data/freedesktop.org.xml.in`, `po/tr.po`
- shared-mime-info spec (tespit algoritması):
  https://specifications.freedesktop.org/shared-mime-info/latest/ar01s02.html
- mime-apps spec: https://specifications.freedesktop.org/mime-apps/latest/ —
  `associations.html`, `default.html`
- xdg-utils: https://gitlab.freedesktop.org/xdg/xdg-utils —
  API: `/api/v4/projects/1212`, `LICENSE`, `scripts/xdg-mime.in`,
  `scripts/desc/xdg-mime.xml`
- moretension/duti: https://github.com/moretension/duti — `README.md`, `COPYRIGHT`,
  açık issue listesi (#34, #38, #56, #58, #63, #64, #65)
- Apple Uniform Type Identifiers:
  https://developer.apple.com/documentation/uniformtypeidentifiers —
  gövde metni otomatik çekilemedi, model duti README'sindeki tanımdan alındı
- jshttp/mime-db: https://github.com/jshttp/mime-db — `README.md`, `LICENSE`,
  `db.json` v1.54.0
- sindresorhus/file-type: https://github.com/sindresorhus/file-type — `readme.md`
- KDE/kio: https://github.com/KDE/kio —
  `src/widgets/kopenwithdialog.h`, `src/widgets/executablefileopendialog_p.h`
- GNOME/nautilus: https://github.com/GNOME/nautilus —
  `src/nautilus-mime-actions.c`, `src/nautilus-app-chooser.c`
- GTK/GIO belgeleri: https://docs.gtk.org/gtk4/class.AppChooserWidget.html,
  https://docs.gtk.org/gio/type_func.AppInfo.get_recommended_for_type.html
- AppImage/AppImageKit: https://github.com/AppImage/AppImageKit — `README.md`,
  `LICENSE`; appimaged: https://github.com/probonopd/go-appimage
- Runly kataloğu: `src/Runly.Settings/Catalog/catalog.json` (408 kayıt, 2026-08-22)
