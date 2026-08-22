# notepad-plus-plus/notepad-plus-plus

## 1. Künye
- Depo: `notepad-plus-plus/notepad-plus-plus`
- Lisans: **GPL-3.0**, ancak `LICENSE` dosyası "Version 3 with the clarifications and
  exceptions described below" diyor — düz GPLv3 değil. GitHub API bu yüzden `NOASSERTION`
  döndürüyor.
- Yıldız: 29.089 · Açık issue: 2.912 (PR'ler dahil)
- Son commit: 2026-08-20 (`Fix lower IL process sends WM_COMMAND to higher IL Notepad++`)
- Son etiketli sürüm: `v8.9.7`, 2026-07-14
- Ölçüm tarihi: 2026-08-22, `gh api repos/notepad-plus-plus/notepad-plus-plus`

## 2. Ne yapıyor
Win32 tabanlı metin ve kaynak kod düzenleyici. Tercihler penceresi içinde, Runly'nin ana
işine benzeyen bir "dosya uzantısı ilişkilendirme" ekranı taşıyor.

## 3. Runly ile kesişimi
Tam kesişim, ama eski yöntemle: `PowerEditor/src/MISC/RegExt/regExtDlg.cpp` bir uzantı
listesi gösterip seçilen uzantıları kendi ProgID'sine (`Notepad++_file`) bağlıyor,
kaldırırken geri alıyor. Runly'nin uzantı kataloğu + atama + geri alma üçlüsünün yaklaşık
yirmi yıllık bir uygulaması; hem alınacak hem kaçınılacak yanı burada.

## 4. Alınacak fikir
1. **Önceki değeri kaydın yanında yedekle.** `regExtDlg.cpp:348-370` (`addExt`): anahtar
   zaten varsa mevcut `(Default)` değeri aynı anahtarın `Notepad++_backup` adlı değerine
   yazılıyor; `deleteExts` (satır 374-405) yedeği geri yazıp siliyor, yedek yoksa değeri
   siliyor, anahtar boşsa anahtarı komple siliyor. Runly'nin dışarıdaki geri alma dosyasına
   ek olarak bu "yedek verinin kaydın yanında durması" deseni, dosya kaybolsa da geri
   dönüşü mümkün kılıyor.
2. **"Benim atadıklarım" listesini ayrı defterden değil registry'den üret.**
   `getRegisteredExts` (satır 314-338) HKCR altındaki `.` ile başlayan tüm anahtarları
   gezip `(Default)` değeri kendi ProgID'sine eşit olanları listeliyor. Config ile registry
   ayrışırsa doğru olan registry'dir.
3. **Yetki yoksa işlemi denemeden arayüzü kapat.** Satır 85-96: admin değilken listeler ve
   düğmeler `EnableWindow(false)`, gerekçe statik metni görünür. Hata diyaloğu göstermek
   yerine, yapılamayacak işi baştan sunmamak.

## 5. Kaçınılacak hata
`addExt` ProgID'yi `HKEY_CLASSES_ROOT\.ext` `(Default)` değerine yazıyor. Windows 8 ve
sonrasında bu değer varsayılanı belirlemez; `Explorer\FileExts\.ext\UserChoice` kazanır.
Kullanıcı yüzündeki sonucu issue'larda görünüyor: #12091 (2022-09-03, açık) ".log çift
tıklanınca Not Defteri açılıyor, .txt çalışıyor", #4595 (2018-06-19, açık) "varsayılan
uygulama olarak kaydedilemiyor". Runly bu yola girmemeli; ayrıca "atadım" mesajını yazma
işleminin başarısına değil, sonradan yapılan okumaya dayandırmalı. İkinci uyarı: dialog
HKCR'ye yazdığı için makine geneli etki ve admin şartı doğuruyor — HKCU tercih edilmeli.

## 6. Doğrulama
- Okundu: depo metadata, `LICENSE` ilk paragrafı, `regExtDlg.cpp` (461 satır; 71-260 ve
  300-461 aralıkları), issue #4595 ve #12091 başlık + gövde (gh api).
- `doğrulanamadı`: "UserChoice hash doğrulaması nedeniyle çalışmıyor" nedenselliği benim
  çıkarımım; issue gövdelerinde bu açıklama yok, yalnız belirti var.
- `doğrulanamadı`: `LICENSE`'ın tamamı okunmadı — marka/logo şartlarının ayrı korunup
  korunmadığını teyit etmedim.
