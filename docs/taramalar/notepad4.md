# zufuliu/notepad4

## 1. Künye
- Depo: `zufuliu/notepad4`
- Lisans: **BSD 3-Clause** — `License.txt` ilk satırı Notepad4/matepath/Notepad2'nin
  BSD 3-Clause altında olduğunu yazıyor. GitHub API `NOASSERTION` döndürüyor (dosya birden
  çok bileşenin lisansını birleştirdiği için tanınmıyor).
- Yıldız: 4.922 · Açık issue: 218 (PR'ler dahil)
- Son commit: 2026-08-21 · Son etiketli sürüm: `v26.08r6282`, 2026-08-16
- Ölçüm tarihi: 2026-08-22, `gh api repos/zufuliu/notepad4`

## 2. Ne yapıyor
Scintilla tabanlı, hafif Win32 metin düzenleyici (Notepad2 soyundan). Windows Not
Defteri'nin yerine geçmeyi ve kabukla bütünleşmeyi açık bir özellik olarak sunuyor.

## 3. Runly ile kesişimi
Doğrudan kesişiyor: `src/Dialogs.cpp` içindeki "System Integration" diyaloğu üç şeyi tek
ekranda yönetiyor — bağlam menüsü kaydı (`HKCR\*\shell\Notepad4`), jump list için
`HKCR\Applications\Notepad4.exe`, ve IFEO ile `notepad.exe`'nin ele geçirilmesi. Sonuncusu
Runly'nin başlatıcı-shim problemiyle birebir aynı: başka bir uygulama adına çağrılan bir
exe'nin devraldığı komut satırını doğru ayrıştırması.

## 4. Alınacak fikir
1. **Kur ve kaldır tek fonksiyonda, bit maskesiyle.** `Dialogs.cpp:2524` okuma
   (`GetSystemIntegrationStatus` → mask), `2573` yazma (`UpdateSystemIntegrationStatus`):
   her özellik için `if (mask & X) { yaz } else { RegDeleteTree }`. Ayrı "kaldır" kod yolu
   olmadığı için kur/kaldır asimetrisinden doğan artık kayıt da yok. Runly'de yazma ve
   temizleme yolları ayrı olduğu sürece bu risk sürer.
2. **Devralınan komut satırı için açık bir "ilk argümanı at" bayrağı.** IFEO `Debugger`
   değeri `"Notepad4.exe" /z` olarak yazılıyor (satır 2621-2628); süreç `notepad.exe
   C:\x.txt` komut satırını aldığı için `/z` dalında `ExtractFirstArgument` ile ilk argüman
   düşürülüyor (`Notepad4.cpp:5770-5776`). Runly'nin başlatıcısı bir gün başka bir exe
   adına çağrılırsa, argüman ayrıştırma varsayımı bayrakla açıkça belirtilmeli.
3. **Yükseltmeyi yeni bir yardımcı exe ile değil, kendini `runas` ile yeniden başlatarak
   yap.** `Notepad4.cpp:7631-7684` (`RelaunchElevated`): manuel yükseltmede önce açık dosya
   kaydediliyor, sonra parametreler yeniden üretilip aynı exe `runas` ile başlatılıyor;
   `fIsElevated` zaten doğruysa hiç girilmiyor (döngü koruması).

## 5. Kaçınılacak hata
İki tuzak. (a) `RelaunchElevated` çağrısında `SEE_MASK_NOZONECHECKS` var
(`Notepad4.cpp` ~7666) — bölge/ek (Attachment) denetimini kapatıyor. Runly'nin güvenlik
kapısı MOTW'ye bakıyor; "UAC uyarısı iki kez çıkmasın" kolaylığı için bu bayrağı
kopyalamak kapıyı sessizce devre dışı bırakır. (b) IFEO ile `notepad.exe` ele geçirmek
HKLM gerektiriyor ve Store'un Not Defteri alias'ı yüzünden `AppExecutionAliasRedirect` /
`AppExecutionAliasRedirectPackages` değerlerinin silinmesini şart koşuyor
(`Dialogs.cpp:2630-2634`). Windows güncellemesi bunları geri koyabilir — kırılgan zemin.

## 6. Doğrulama
- Okundu: depo metadata, `License.txt` başlığı, `Dialogs.cpp` satır 2480-2660 ve 2689-2777,
  `Notepad4.cpp` satır 5765-5785 ve 7628-7690.
- `doğrulanamadı`: `SEE_MASK_NOZONECHECKS`'in oradaki gerekçesi — kodda yorum yok, etki
  yorumu bana ait. IFEO'nun Windows güncellemesiyle geri alındığına dair bu depoda issue
  aramadım.
