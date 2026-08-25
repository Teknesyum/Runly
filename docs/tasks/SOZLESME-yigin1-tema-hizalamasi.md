# Sözleşme — Yığın 1: Ayarlar penceresinin teknesyum-ui hizalaması

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma.
**Kapsam:** yalnız `src/Runly.Settings/` ve yeni ortak token dosyası.
`src/Runly.Launcher/Ui/` **bu sözleşmede yok** — sıradaki turda ayrı ajan alacak.

## Neden

`docs/UI-PLAN.md` "Yığın 1" maddesi 0.3'e ertelenmişti. Planın saydığı üç sapmanın
**hepsi kapanmış** (denetim yapıldı): harf aralıklı başlık yok, UPPERCASE sütun başlıkları
çalışma anında title-case'e dönüyor, `TextDim` beyaz, 7.5pt etiket kalmadı, sabit piksel yok.

Ama standart o tespitten sonra ilerledi. Bugün açık olan sapmalar aşağıda; hepsi denetimle
dosya:satır düzeyinde doğrulandı, yeniden araştırma yapma, veri olarak al.

## Yapılacaklar

### 1. Tek doğruluk kaynağı — `src/Runly.Core/Theme/TeknesyumTokens.cs` (yeni)

Bugün renk değerleri iki yerde ayrı ayrı yazılı: `Runly.Settings/Palette.cs` ve
`Runly.Launcher/Ui/NeonWindowChrome.cs`. İkisi **kaymış** — yüzey rengi birinde `#0A0A0C`,
diğerinde `#08090A`.

Token değerleri Core'da tek yerde dursun; string hex sabitleri olarak. `Palette` bunları
okusun. Launcher sonraki turda aynı dosyadan COLORREF'e çevirecek — **o dönüşümü sen
yazma**, yalnız değerleri yerleştir. Core AOT-uyumlu kalmalı: yalnız `const string`,
`System.Drawing` bağımlılığı **yok**.

### 2. Tipografi ölçeği — `Palette.cs:51-58`

Standart §3 beş basamak istiyor: **14 · 16 · 20 · 24 · 30** px. Bugünkü:

| Rol | Bugün | Olması gereken |
|---|---|---|
| `H2` | 15pt = 20px | 24px |
| `H3` | 12pt = 16px | 20px — bugün gövdeyle **aynı boyut**, standardın adını koyduğu hata |
| `Hero` | 21pt = 28px | 30px |
| `MonoBody` | 11pt ≈ 14.7px | ölçek dışı, 14px'e çek |

96 dpi'da punto = piksel × 3/4. `Palette.cs:8-11`'deki "14/16/20/28" yorumu eski ölçeği
anlatıyor, güncelle.

**Dikkat:** punto değişimi satır yüksekliklerini taşır. `Metrics` zaten `Font.Height`
üzerinden türetiyor, yani yerleşim kendini toplamalı — ama toplamadığı yeri gözle bul ve
düzelt, sabit piksel ekleyerek değil ölçüden türeterek.

### 3. Renk rolleri

- **`pink-text` yok.** Standart §2 metin rolü için `#FF54EB` (7.72:1) şart; dolgu hex'i
  `#FF00EA` 6.44:1 ile metinde geçmiyor. Dolgu olarak `#FF00EA` kalır, **metin olarak**
  kullanılan yerler yeni token'a geçer: `MainForm.cs:41`, `:359`, `:870`, `:1692`,
  `Dialogs/ChooseApplicationDialog.cs:389`, `Dialogs/ResultDialog.cs:49-50`.
- **`purple-text`** için de aynısını yap; mor dolgu hex'i metinde kontrastı tutmuyorsa
  standardın metin karşılığını kullan.
- **`warning` tanımlı değil.** `#FBBF24` eklensin; uyarı yüzeyi (`⚠ Varsayılan yap`,
  `MainForm.cs:40-41`) bugün pembeye biniyor — uyarı ile "dikkat çeken pembe" aynı renk
  olmamalı.
- **Sapan hex:** `Palette.cs:19` Surface `#0A0A0C` → `#08090A`. `Palette.cs:21`
  FieldBg `#101214` ve `NeonControls.cs:559` `#123238` token listesinde yok — standarttaki
  karşılıklarına çek, karşılığı yoksa raporda gerekçesini yaz.

### 4. Gri temizliği

Standart §2: tek gri **yalnız devre dışı kontrol** içindir. Bugün `TextHint = Disabled`
(`Palette.cs:37`) durum metinlerinde kullanılıyor: `MainForm.cs:43` (bağlı değil satırı),
`:270`, `:447`, `:745`, `:2370`, `:2388`, `BindingProgressRing.cs:47`.

Bunlar devre dışı **değil** — okunması gereken metin. Beyaza çek; hiyerarşiyi boyut,
ağırlık ve neon vurguyla kur. `TextHint` adı kalabilir ama beyaz olur, tıpkı `TextDim` gibi.

### 5. Yarıçap

`teknesyum-ui.json` ekNotu net: **tek yarıçap 6 DIP**, yalnız pencere köşesi 12.
Bugün: `NeonControls.cs:353` buton `Px(12)`, `:401` panel `Px(16)`, `NeonForm.cs:437`
`Px(12)`. Pencere köşesi (`Metrics.cs:321`) zaten 12, ona dokunma.

### 6. Odak halkası — standart §5.3

Çift katman: iç opak `#000000`, dış 2 DIP `neon-blue`. Bugün `NeonGridCells.cs:87-98`
tek katman pembe; `NeonButton.OnPaint` (`NeonControls.cs:345-375`) odak durumunu **hiç
çizmiyor**, yalnız hover var. Klavyeyle gezen kullanıcı nerede olduğunu göremiyor.

### 7. Zemin gradienti — standart §2

Uygulamayı kaplayan **tek** gradient, **≥11 durak** (koyu temada bantlaşmayı ölçülmüş
biçimde kırıyor), uçlar `#000000` ile `#08090A` arasında. Düz `#000000` standartta
"eksik teslim" sayılıyor. Projede tek bir `LinearGradientBrush` yok.

- Tek kesintisiz yüzey: üst şeride bir, içeriğe başka gradient verip dikiş bırakma.
- **Hareket kısmını bu sözleşmede yapma.** Standart yavaş dönüşe izin veriyor
  (`--tk-bg-donus`, ≥40 s) ama bu bir ayarlar penceresi; sürekli dönen bir zemin boşta
  CPU yakar. **Statik gradientle teslim et**, hareketi ayrı bir karara bırakıyorum.
  Raporda bunun bilinçli bir eksik olduğunu yaz.

### 8. Gömülü metin ve UPPERCASE artığı

`NeonMessageBox.cs:131-134` hâlâ koda gömülü UPPERCASE taşıyor: `"HATA"`, `"DİKKAT"`,
`"ONAY"`, `"BİLGİ"` — karşılıkları `locale/tr.json:34-37`'de zaten duruyor. `:139-143`
düğme metinleri de gömülü Türkçe. İkisi de locale'e geçsin.

`MainForm.cs:559-565`'teki UPPERCASE sütun başlığı dizgileri **ölü varsayılan** — çalışma
anında `ApplyLanguage` onları title-case'e çeviriyor. Ölü dizgiyi de düzelt, yanıltıyor.

### 9. Yazı tipi zinciri — **kısmi**

Standart §3: sans `Atkinson Hyperlegible Next` → `Segoe UI`, ve font **projeye gömülür**
(WinForms'ta `PrivateFontCollection`); gömülmediyse zincir Segoe UI'ye düşer ve bu
"eksik teslim" sayılır. Bugün `Palette.cs:45` `Inter → Segoe UI` diyor; Inter standardın
hiçbir yerinde yok.

**Font dosyası henüz depoda yok — indirme izni bekliyorum.** Sen şunu yap:

- Zinciri `Atkinson Hyperlegible Next → Segoe UI` olarak düzelt (Inter'i çıkar).
- `PrivateFontCollection` ile **gömülü kaynaktan** yükleyen yolu yaz; kaynak yoksa
  sessizce `Segoe UI`'ye düşsün, çökmesin.
- Mono zinciri `Cascadia Mono → Consolas` olsun; `JetBrains Mono` çıksın.
- Font dosyası eklendiğinde tek yapılacak iş `.ttf`'i gömülü kaynak olarak eklemek olsun.

## Kabul kriterleri

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı** (`TreatWarningsAsErrors` açık).
2. `dotnet test Runly.sln -c Debug --no-build` → mevcut testlerin hepsi geçer.
3. `dotnet format --verify-no-changes` → temiz.
4. **Canlı ölçüm.** Derlenmiş `RunlySettings.exe`'yi `Start-Process` ile aç, 8 sn bekle,
   `PrintWindow` ile pencerenin görüntüsünü al ve `docs/reports/` altına koy. İki dilde
   (`%APPDATA%\Runly\config.json` içindeki `language` alanı) tekrarla. Ölçüm bitince
   yapılandırmayı **eski hâline getir**.
   Görüntülerde şunlar görünmeli: kırpılan etiket yok, satırlar üst üste binmiyor,
   gradient bantlaşmıyor, gri metin kalmadı.
5. Gradient bant testi: aldığın PNG'de zemin boyunca görünür şerit olmamalı. 8-bit
   görüntüde şerit varsa durak sayısını artır.
6. `docs/UI-PLAN.md` "Yığın 1" bölümünün altına ne yapıldığını yazan bir **Yapıldı** notu
   düş; yapılmayanı (hareket, font dosyası) açıkça ayır.

## Kurallar

- **Kod yorumu yazma** — bu depodaki mevcut yorumlar bir kısıtı anlatıyor; sen de ancak
  öyle bir kısıt varsa yaz, "ne yaptığını" anlatan satır ekleme.
- Renk ve ölçü **uydurma**: değerler standarttan gelir. Standartta karşılığı olmayan bir
  değer bırakman gerekiyorsa raporda gerekçesini yaz.
- `Palette.cs` başlığındaki karar kaydını güncelle — yeni satır: bu turun kararı ve tarihi.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, hangi token nereden geldi, 4. ve 5.
maddenin ölçüm çıktıları, bilerek eksik bıraktıkların.

## Devam notu (ana oturum, tur ortasında eklendi)

Ajan kod yazımını bitirdi ama ölçüme geçemeden durdu. `dotnet build Runly.sln -c Debug`
**0 hata** veriyor, derleme temiz — doğrulandı. Kalan iş:

1. `dotnet test --no-build` ve `dotnet format --verify-no-changes` çalıştır. Test sayısı
   **252** olmalı; başka bir ajan 11 test ekledi, bu normal.
2. Kabul 4 ve 5'i uygula: iki dilde `PrintWindow` görüntüsü, gradient bant kontrolü.
3. Kabul 6'yı uygula: `docs/UI-PLAN.md`'ye Yapıldı notu.
4. `Dialogs/ChooseApplicationDialog.cs` ve `Discovery/` **artık serbest** — o ajan bitti.
   Oradaki pembe metin token değişikliğini yapabilirsin, ama yalnız renk satırına dokun,
   sıralama mantığına karışma.

---

## Rapor (ui-builder, 2026-08-26)

Standart yürürlükteydi (`~/.claude/teknesyum-ui.json`), ekNot'taki tek yarıçap kuralı uygulandı.

**Yeni:** `src/Runly.Core/Theme/TeknesyumTokens.cs` (yalnız `const string`, System.Drawing yok),
`src/Runly.Settings/NeonBackground.cs` (gradient + `NeonLayoutPanel`).
**Değişen:** `Palette.cs`, `NeonControls.cs`, `NeonForm.cs`, `NeonFields.cs`, `NeonGridCells.cs`,
`NeonMessageBox.cs`, `BindingProgressRing.cs`, `MainForm.cs`, `Dialogs/ResultDialog.cs`,
`Dialogs/ChooseApplicationDialog.cs` (yalnız `:478` renk satırı), `docs/UI-PLAN.md`.

Tokenlar standardın §2 tablosundan geldi: `pink-text #FF54EB`, `purple-text #C67EFF`,
`warning #FBBF24`, `surface #08090A`. Karşılığı olmayan üç hex: `FieldBg #101214` → `surface`
(standartta ayrı field tokenı yok, alan çerçevesiyle ayrılır), `#123238` → `neon-blue/30`
opak karışımı `#00494D` (aktif basamağı), `#152229` → `neon-blue/10` `#00181A` (dekoratif).
Panel/alan çerçeveleri §2'nin ölçülmüş `/50` basamağına (128) çekildi — yüzey artık zeminle
1.06:1 olduğu için paneli görünür kılan tek şey çerçevesi.

**Kabul 1-3:** build 0 hata 0 uyarı · `dotnet test` 252/252 · `dotnet format --verify-no-changes` temiz.
**Kabul 4:** `docs/reports/yigin1-turkce-ana-pencere.png` ve `yigin1-ingilizce-ana-pencere.png`
(1480x1000, ekrandan kopya — `PrintWindow` non-client çizilen alan çerçevelerini almıyor).
İki dilde de kırpılan etiket, üst üste binen satır ve gri metin yok; yapılandırma eski hâline döndü.
**Kabul 5:** tarama — sol kenar sütunu, başlık bandı ve paneller arası yatay hat; en büyük adım
RGB toplamında 3, yani kanal başına 1 seviye. Ayraç çizgisinin iki yanındaki pikseller birebir
aynı (`1,1,2`), yani başlık şeridiyle içerik tek yüzey. Şerit yok.
**Kabul 6:** `docs/UI-PLAN.md` "Yığın 1" altına Yapıldı notu düşüldü.

**Bilerek eksik:** gradient hareketi (sözleşme statik istedi) · font `.ttf` deposunda yok, zincir
Segoe UI'ye düşüyor (standart bunu "eksik teslim" sayar) · `DrawCheckGlyph` onay kutusu yarıçapı
`Px(3)` bırakıldı — 14 DIP'lik kutuda 6 yarıçap glifi daireye çevirip radyo düğmesiyle karıştırıyor.
**Yol üstünde bulunanlar** (`_sorun.log`'a da yazıldı): "Düzenleyici komutu" alanının çerçevesi
hiç çizilmiyordu (WM_NCPAINT gelmiyor), `NeonFields.Handle`'a WM_PAINT eklenerek düzeltildi ·
`locale/tr.json` `caption.sponsor` Türkçe'de de "Buy me a coffee" (§4 "Destek" istiyor) ·
`NeonTheme.DisabledOutline` griyi değil neon maviyi gösteriyor — son ikisi kapsam dışı, dokunulmadı.
