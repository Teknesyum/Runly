# sumatrapdfreader/sumatrapdf

## 1. Künye
- Depo: `sumatrapdfreader/sumatrapdf`
- Lisans: **GPL-3.0** (GitHub API `license.spdx_id`)
- Yıldız: 17.373 · Açık issue: 88 (PR'ler dahil) — bu listedeki en düşük issue yığını
- Son commit: 2026-08-22 (`list Navigate Files in Folder off the UI thread`)
- Son etiketli sürüm: `3.6.1rel`, 2026-04-06 — commit ile sürüm arası ~4,5 ay
- Ölçüm tarihi: 2026-08-22, `gh api repos/sumatrapdfreader/sumatrapdf`

## 2. Ne yapıyor
PDF, ePub, CBZ, DjVu, CHM gibi çok sayıda belge biçimini açan hafif Windows okuyucusu.
On küsur uzantıyı kendi üzerine ilişkilendirmek zorunda olduğu için, dosya ilişkilendirme
kodu ayrı bir dosyada olgunlaşmış.

## 3. Runly ile kesişimi
Listedeki en yakın depo. `src/RegistryInstaller.cpp` neredeyse Runly'nin yaptığı işin
aynısını yapıyor: uzantı başına ProgID, `Software\<App>\Capabilities\FileAssociations`,
`Software\Classes\.<ext>\OpenWithProgids`, `RegisteredApplications`, HKCU/HKLM ayrımı,
kaldırmada geriye dönük temizlik ve "varsayılan yap" akışı.

## 4. Alınacak fikir
1. **Uzantı başına ayrı ProgID ve ayrı ikon indeksi.** `RegistryInstaller.cpp:206-275`:
   ProgID adı `SumatraPDF.<ext>`, ikon `exe,-3` (.epub), `-4` (.cb*), `-8` (.pdf) biçiminde
   uzantıya göre seçiliyor. Gerekçesi kodun kendi yorumunda (satır ~292-299): tek ProgID
   kullanılırsa Explorer bütün türleri "PDF Document" olarak gösterir. Runly'nin uzantı
   kataloğu tür adı ve ikonu zaten tutuyor; ProgID granülerliği bununla eşleşmeli.
2. **"Varsayılan yap" düğmesini registry'yi zorlamak yerine sisteme devret.**
   Satır 675-704 (`LaunchDefaultAppDialogForExtension`): var olmayan `document.<ext>`
   yoluyla `SHOpenWithDialog`, `OAIF_FORCE_REGISTRATION | OAIF_REGISTER_EXT |
   OAIF_ALLOW_REGISTRATION` bayrakları; başarısızsa
   `ms-settings:defaultapps?registeredAppUser=<AppName>` derin bağlantısına, o da yoksa
   `registeredAppMachine`e, en sonda düz `ms-settings:defaultapps`e düşüyor. Runly'nin
   UserChoice'a dokunmadan varsayılan atayabilmesi için hazır ve kademeli bir çıkış yolu.
3. **Kaldırmada şema tarihçesini de temizle.** `RemoveInstallRegistryKeys` (satır 464-525)
   önce "3.4 öncesi yazılan" anahtarları, sonra "3.4'te gelen" ProgID'leri siliyor; ayrıca
   `DeleteEmptyRegKey` + `RegKeyParent` ile boşalan ata anahtarları geriye doğru topluyor.
   Runly şema değiştirdiğinde eski şemayla yazılmış kayıtları da silmeli.

## 5. Kaçınılacak hata
Kodun kendi belge yorumu (satır ~338-342) `Explorer\FileExts\.pdf\UserChoice\Progid` için
"bu anahtara yazılamaz, o yüzden onu siliyoruz" diyor. UserChoice'ı silmek varsayılanı
"seçilmemiş"e düşürür: kullanıcı bir sonraki çift tıklamada sebepsiz bir seçim ekranıyla
karşılaşır ve bunu Runly yapmış olur. Silinecekse hem kullanıcıya söylenmeli hem de
yalnızca değer bize aitken yapılmalı. İkinci uyarı: aynı dosyada Windows XP/7 dönemine ait
`#if 0` bloğu ve TODO yorumları duruyor (`OpenWithList` bölümü, satır ~288-312) — belge
niteliğindeki yorumlar ile çalışan kod arasında güncelliği doğrulanmamış bölümler var.

## 6. Doğrulama
- Okundu: depo metadata, `src/RegistryInstaller.cpp` (704 satır; 20-320, 460-525, 675-704
  aralıkları satır satır).
- `doğrulanamadı`: `SHOpenWithDialog` yolunun Windows 11 24H2'de gerçekten varsayılanı
  değiştirdiği — kaynak kodda niyet var, davranış testi yok, ben de çalıştırmadım.
- `doğrulanamadı`: "88 açık issue" düşük görünüyor ama depo issue'ları büyük ölçüde
  başka yerde (forum) yönetiliyor olabilir; bunu teyit etmedim.
