# Runly — 0.3 yol haritası

**Yazıldı:** 2026-08-26. **Öncesi:** `docs/UI-PLAN.md` (Yığın 2 kapandı, Yığın 1 kapandı),
`docs/PLAN.md` (T/R paket dizisi kapandı).

## Boşluk

Runly'nin amacı kullanıcının hayatını kolaylaştırmak. Bugün ise **kullanıcı Runly'yi
çalıştırıyor**: makine hakkında bilinen her şey (`UsageHistory`, `AssocHandlerFinder`,
katalog) yalnız Ayarlar penceresinin seçim diyaloğunda, kullanıcı bir satıra çift
tıklarsa ortaya çıkıyor. Bilgi zaten toplanıyor — yanlış anda sunuluyor.

Olması gereken: **uygulama bildiğini önerir, kullanıcı onaylar.** Form doldurtmaz.

İkinci görüş (fable, 2026-08-26) aynı teşhisi koydu ve iki şeyi işaret etti:

1. Sıralama mantığı `Runly.Settings` içinde — WinForms, `Microsoft.Win32.Registry`, COM.
   Launcher AOT ve WinForms'suz. Mantık AOT-güvenli bir katmana taşınmazsa duruş
   düzeltmesi Ayarlar penceresine hapsolur.
2. Runly bir uzantıyı kendine bağladığı an Windows'un `FileExts` MRU sinyali o uzantı
   için **donar**. Kurulumdan sonra tek taze sinyal Runly'nin kendi geçmişidir; bu
   yüzden kullanıcı seçimini config'e yazmak önerinin bir parçasıdır, ayrı bir konfor
   özelliği değil.

## Sıra

### Y1 · Boş eşlemeleri önceden doldur *(ucuz, mekanizma hazır)*

Ayarlar açılışında ve kurulumda, `OpenWith`/`Interpreter` boş olan her satır
`UsageHistory.Rank`'in ilk adayıyla **tohumlanır**. Satır "Notepad++ önerildi —
değiştir?" der; bağlama yine kullanıcı onayıyla olur, kayıt defterine yazılmaz.

Kabul: sinyali olan uzantı Ayarlar ilk açılışta dolu gelir; sinyalsiz uzantı bugünkü
boş hâlinde kalır; hiçbir öneri onaysız bağlanmaz.

**Yapıldı (2026-08-26, `59682c6`):** boş işleyici hücresi artık makinenin kendi
geçmişinin işaret ettiği uygulamayı adıyla gösteriyor; Etkin kutusu onay anıdır.
Hücre 13 karakter genişliğinde ve büyütülemiyor (sütun en küçük genişlikleri toplamı
ızgarayı zaten aşıyor), o yüzden açıklama Ayrıntılar paneline taşındı. Kayıt defteri
okuması 412 uzantı için bir kez +32 ms, sonraki yenilemelerde +1 ms.

### Y2 · Sıralamayı AOT katmanına taşı *(Y3'ün ön koşulu)*

`UsageHistory`'nin saf kısmı ve aday birleştirme `Runly.Core`'a iner; kayıt defteri
okuması launcher'dan da çağrılabilen AOT-güvenli bir sağlayıcının arkasına geçer
(`Microsoft.Win32.Registry` AOT'ta sorunsuz; COM tarafı `AssocHandlerFinder`'da kalır
ve launcher'da opsiyonel olur).

Kabul: `Runly.Settings` referansı olmadan launcher projesi sıralamayı çağırabilir;
AOT publish 0 uyarı; mevcut 252 test yeşil kalır.

### Y3 · Çift tık anında öner *(asıl duruş düzeltmesi)*

Launcher'ın "ilişkilendirilmemiş" yolu bugün yalnız "Ayarlar'ı aç" diyor. Bunun yerine
sıralı ilk üç aday **komut bağlantısı** olarak gösterilir; seçilen config'e yazılır ve
dosya hemen açılır. İhtiyaç anı çift tık anıdır, Ayarlar penceresi değil.

Kabul: bilinmeyen uzantıya çift tık → aday listesi → tek tıkla açılır ve bir daha
sorulmaz. Ayarlar penceresine hiç gidilmez.

### Y4 · Launcher'ı tema jetonlarına hizala

`src/Runly.Launcher/Ui/` hâlâ kendi renklerini taşıyor: gri `#9CA3AF`, 12/16 köşe
yarıçapı, 9pt yazı tipi, gömülü Türkçe metin (yerelleştirme yok). `TeknesyumTokens`
tüketilecek, yarıçap 6/12'ye inecek, metinler locale dosyalarına taşınacak.

Bu Y3'ten sonra gelmeli — Y3 launcher penceresine yeni içerik ekliyor.

### Y5 · Kalan kusurlar *(tek tur, birlikte)*

- %125 / %150 DPI kontrolü (kullanıcı erteledi, sistem ayarı gerektiriyor)
- `NeonTheme.DisabledOutline` gri yerine neon mavi gösteriyor
- Onay kutusu işaret yarıçapı `Px(3)`

**Yapıldı (2026-08-26):** `caption.sponsor` Türkçede artık `Destek` (§4). Uygulama
seçicideki `Administrative Tools` ve `Lua documentation` satırlarının kaynağı bulundu —
argüman taşıyan Başlat menüsü kısayolları uygulama sanılıyordu, artık atlanıyor; kalan
iki `notepad` gerçek ve farklı (paketli / System32), diyalog ikisinin de yolunu yazıyor.

**Yapıldı (2026-08-26):** gömülü `Atkinson Hyperlegible Next` (Regular + Bold, upstream
`7925f50`, OFL). GDI ve GDI+ font-linking ölçüldü — fontun taşımadığı üç işaret (✓ ⚠ →)
sistem yedeğinden geliyor, `.notdef` kutusu yok; kanıt `docs/reports/font-fallback-testi.png`.
Aynı turda `NeonActionCell` eklendi: "Varsayılan yap" hücresi görsel stil düğmesi olarak
beyaz çiziliyordu, artık amber neon hap.

## Sonraya

`UserAssist`, `MuiCache`, atlama listeleri: getirisi marjinal, ayrıştırması kırılgan.
Y1-Y3 yerleşip öneri kalitesi ölçülmeden açılmayacak.

Arka plan gradyanının hareketi: boşta duran bir ayar penceresinde sürekli CPU yakar.
Karar 0.4'e bırakıldı.
