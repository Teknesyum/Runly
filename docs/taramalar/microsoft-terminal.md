# microsoft/terminal

## 1. Künye
- Depo: `microsoft/terminal`
- Lisans: **MIT** (GitHub API `license.spdx_id`)
- Yıldız: 104.669 · Açık issue: 1.739 (PR'ler dahil)
- Son commit: 2026-08-18 (`Fix two KKP ReportEventTypes issues #20551`)
- Son etiketli sürüm: `v1.24.11911.0`, 2026-07-16
- Ölçüm tarihi: 2026-08-22, `gh api repos/microsoft/terminal`

## 2. Ne yapıyor
Windows Terminal ve klasik konsol ana bilgisayarı (conhost) aynı depoda. Sekmeli terminal
uygulamasının yanında, Windows'un "varsayılan terminal uygulaması" devir mekanizmasını da
barındırıyor.

## 3. Runly ile kesişimi
İki noktada: (a) kabuk kaydı — `src/cascadia/ShellExtension` altında `IExplorerCommand`
uygulaması ("Open in Terminal"), Runly'nin bağlam menüsü/"birlikte aç" tarafıyla aynı
arayüz; (b) devir kaydı — `src/propslib/DelegationConfig.cpp`, "bu iş hangi uygulamaya
gidiyor" bilgisinin HKCU'da tek yerde tutulması, Runly'nin ProgID/handler yaklaşımının
işletim sistemi tarafındaki karşılığı. Uzantı kataloğu ve MOTW tarafında kesişim yok.

## 4. Alınacak fikir
1. **Fiili öğeye göre gizle, gri gösterme.** `OpenTerminalHere.cpp:105-127` (`GetState`):
   `SFGAO_FILESYSTEM` yoksa veya `SFGAO_FOLDER | SFGAO_STREAM` (zip içi sanal klasör) ise
   `ECS_HIDDEN` dönülüyor. Runly'nin kabuk girdisi "This PC", "Quick Access" ve arşiv içi
   öğelerde hiç görünmemeli — devredeceği gerçek bir dosya yolu yok.
2. **Yavaş iş için `fOkToBeSlow` sözleşmesi.** Aynı fonksiyonun yorumu (satır 108-113):
   IO gerekiyorsa `E_PENDING` dönüp arka plan iş parçacığında yeniden çağrılmak. Runly'nin
   uzantısı katalog/registry okuyacaksa Explorer'ın menü açılışını bloklamamalı.
3. **Devir kaydını tek yaz/oku çiftinde topla.** `DelegationConfig.cpp:288-305` tek `s_Set`
   ile `HKCU\Console\%%Startup` altına `DelegationConsole`/`DelegationTerminal` CLSID'lerini
   yazıyor; okuma tarafında (satır 238-283) "varsayılan" ve "conhost" CLSID'leri
   "kullanıcı henüz seçmedi" halini ayırt ediyor. Runly'de de "atanmamış" ile "bize
   atanmış" ayrımı ayrı bir bayrakla değil, kaydın kendi değeriyle temsil edilmeli.

## 5. Kaçınılacak hata
Kabuk uzantısı ayrı bir DLL (`WindowsTerminalShellExt.vcxproj` + `.def`) ve Terminal'in
paket kimliği içinde taşınıyor. Runly imzasız/paketsiz bir DLL'i `HKCU` üzerinden
kaydederse Windows 11'in birincil bağlam menüsüne giremez; klasik kayıt "Diğer seçenekleri
göster" altına düşer. Bu davranışı bu depodan satır düzeyinde teyit etmedim — aşağıda
işaretli.

## 6. Doğrulama
- Okundu: depo metadata (gh api), `OpenTerminalHere.cpp` satır 105-130 ve fonksiyon
  listesi, `DelegationConfig.cpp` içindeki anahtar/CLSID akışı, `ShellExtension` klasör
  listesi.
- `doğrulanamadı`: "paketsiz uzantı Win11 birincil menüsüne giremez" — bu depoda bunu
  söyleyen bir belge okumadım, klasör yapısından çıkarım. `doc/specs` altındaki
  `#492 - Default Terminal/spec.md` okunmadı.
- `doğrulanamadı`: yıldız/issue sayıları GitHub sayaçlarıdır; issue sayacı PR içerir.
