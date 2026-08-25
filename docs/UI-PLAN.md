# Runly arayüz elden geçirme planı

22.08.2026. Kaynak: `docs/taramalar/` (50 depo) + canlı makinede ölçüm + `docs/KNOWN-ISSUES.md`.

## Karar: sıra 2 → 1, ve bu sıra zorunlu

İki ayrı yığın var. Sezgi "önce görsel kimliği oturt" diyor; **yanlış**.

Tipografi tabanını 13px'ten 16px'e çıkarmak satır yüksekliğini, panel genişliğini ve taşma
davranışını yeniden dengeler. DPI doğrulaması bundan **önce** yapılırsa aynı iş iki kez yapılır.
Bu yüzden önce işlevsel borç (Yığın 2), sonra standart hizalaması (Yığın 1, 0.3'e).

Bir kural bu sırayı güvenli kılıyor: **Yığın 2'de hiçbir ölçü sabit piksel yazılmayacak,
`Font.Height` metriğinden türetilecek.** O zaman 0.3'teki font değişimi bu kodu kırmaz.

---

## Yığın 2 — 0.2.0 içinde (kullanıcıya görünen kusurlar)

### U1 · Simge katmanı

Bugün `ChooseApplicationDialog.ResolveIcon` `Icon.ExtractAssociatedIcon` kullanıyor: sabit 32px,
%150 ölçekte bulanık, `"yol,indeks"` biçimini çözemiyor, `@dosya,-id` dolaylı dizeleri okuyamıyor.

- `IShellItemImageFactory` ile DPI'ya göre doğru kareyi iste.
- `"yol,indeks"` için `ExtractIconEx`, `@dosya,-id` için `SHLoadIndirectString`.
- Önbellek ölçek başına ayrı tutulsun; pencere monitör değiştirince tazelensin.
- Başarısızlıkta bugünkü davranışa düş, çökme yok.

**Kabul:** %100 ve %150'de aynı liste ekran görüntüsüyle karşılaştırılır, simgeler net.

**Yapıldı** — `Discovery/ShellIconLoader.cs`; kanıt `docs/reports/U1-simge-olcek-karsilastirma.png`.
Canlı %150 ekranı yerine 32/48 px karşılaştırma sayfası basıldı (ekran kapısı kapalı).

### U2 · Windows'un kendi "birlikte aç" listesi

`SHAssocEnumHandlers` / `IAssocHandler` hiç kullanılmıyor. `GetUIName`, `GetIconLocation`,
`IsRecommended` hazır geliyor.

- Mevcut `ApplicationFinder`'ın **yerine değil yanına**; iki kaynak birleştirilip tekilleştirilir.
- `NoOpenWith` işaretli uygulamalar listeden elenir.
- `IsRecommended` bugünkü katalog `suggestedApps` sıralamasıyla harmanlanır.

**Kabul:** `.md` için liste Windows'un kendi "Birlikte aç" listesini kapsar; kapsamayan varsa
gerekçesi yazılır.

**Yapıldı** — `Discovery/AssocHandlerFinder.cs` + `ChooseApplicationDialog.Merge`; kanıt
`docs/reports/U2-birlikte-ac-karsilastirma.txt`. `SHOpenWithDialog` yerine ham `IAssocHandler`
dökümü ile karşılaştırıldı (ekran kapısı kapalı).

### U3 · DPI

- Owner-draw `ListBox.ItemHeight` ölçeklenmiyor (`dotnet/winforms#6382`). Bugün sabit: `MainForm.cs:194`
  (34), `NeonControls.cs:337` (22), `ChooseApplicationDialog.cs:128` (48).
- `RowTemplate.Height = 26` ve `ColumnHeadersHeight = 30` de sabit (`MainForm.cs:438`, `:456`).
- Hepsi `Font.Height` katsayısına çevrilir. Ölçek katsayısı açılışta bir kez sabitlenir.

**Kabul:** %100, %125, %150'de üç ekran görüntüsü; hiçbir metin kırpılmıyor, hiçbir satır üst üste
binmiyor. `docs/KNOWN-ISSUES.md`'deki DPI maddesi kapanır.

### U4 · Kalan native sızıntılar

- `ComboBox` açılır listesi ayrı bir pencere; `DarkMode_CFD` teması uygulanmalı.
- Tooltip framework'te bile eksik (`dotnet/winforms#12420`) — özel tooltip gerekiyor.
- `OpenFileDialog` ve `DataGridView` boş alanı gözle taranır.
- `SetPreferredAppMode` ordinal imzası build'e göre değişiyor (`NeonControls.cs:40`); koşulsuz
  çağrı yerine sürüm kontrolü.

**Kabul:** hover, focus, seçili, devre dışı ve **açık dropdown** durumlarının hepsi ekran
görüntüsüyle gezilir. Tek bir beyaz yüzey kalmaz.

**Yapıldı** — `NeonToolTip` (owner-draw koyu kart), `ThemeClassFor` `ComboBox` için
`DarkMode_CFD`, `EnableDarkMode` artık 18362 altındaki derlemelerde `SetPreferredAppMode`'u
hiç çağırmıyor. **Eksik:** hover / focus / açık dropdown turu ekran kapısı kapalı olduğu için
canlı gezilemedi.

### U5 · Risk notları arayüzde

Katalogda altı uzantının `riskNote`'u var (`.hta`, `.vbs`, `.wsf`, `.js`, `.ps1`, `.jar`) ama
arayüz hiç göstermiyor. LOLBAS denetimi ayrıca beş uzantının katalogda **hiç olmadığını** buldu:
`.vbe`, `.jse`, `.wsc`, `.sct`, `.chm`; `.wsh` var ama notsuz.

- Ayrıntılar panelinde risk notu görünür; ızgarada satır işaretlenir.
- Not metni barındırıcıyı **adıyla** söyler (`mshta.exe`, `wscript.exe`) — kullanıcının kendi
  doğrulayabileceği tek somut ipucu.
- Eksik beş uzantı **elle sınandıktan sonra** kataloğa eklenir; sınanmadan eklenmez.

**Kabul:** `.hta` satırı seçilince not okunur; `catalog.json` testi altı yerine on bir uzantıda not
arar.

**Yapıldı** — notlar barındırıcıyı adıyla söylüyor, ızgarada notlu uzantı işaretleniyor,
ayrıntılar panelinde `catalog.riskNote` başlığıyla görünüyor. `.wsh` notu yazıldı; `.vbe`,
`.jse`, `.wsc`, `.chm` ölçülüp eklendi. **`.sct` eklenmedi:** bu makinede uzantı makine
düzeyinde Photoshop'a devredilmiş, Windows'un kendi varsayılanı gözlemlenemiyor —
ölçüm `docs/reports/U5-uzanti-barindirici-olcumu.txt`. Test on bir uzantı arıyor.

### U6 · İngilizce arayüzde taşma

Bugüne kadar yalnız Türkçe arayüzde gözle bakıldı. Standart "yerleşimi en uzun dil belirler" diyor;
Türkçe genelde daha uzun, ama doğrulanmadı.

**Kabul:** dil değiştirilip her panel ve diyalog gezilir, kırpılan tek etiket kalmaz.

### U7 · Büyük liste

Debounce eklendi (180 ms) ama filtreleme hâlâ tam yeniden projeksiyon. PowerToys deseni: bir kez
kurulan indeks + `CancellationToken`'lı iptal.

**Kabul:** 400+ satırda yazarken ölçülen yenileme süresi kaydedilir; bugünkü 15–47 ms taban.

---

## Yığın 1 — 0.3'e ertelendi (standart hizalaması)

`teknesyum-ui` standardının bugünkü hali projeninkiyle üç yerde çelişiyor:

| Standart | Projede | Etki |
|---|---|---|
| UPPERCASE yasak | `G Ü V E N İ L E N   K L A S Ö R L E R`, `ETKİN`, `DURUM` | Bütün etiketler ve sütun başlıkları |
| Taban 16px, ikincil ≥14px | gövde 10pt (~13px), etiket 7.5pt (~10px) | Bütün ölçüler yeniden dengelenir |
| Ara gri yok, metin tam beyaz | `TextDim`, `TextHint` yoğun kullanımda | Kontrast ve hiyerarşi baştan kurulur |

**`Palette.cs`'teki "değiştirme" mührü bir engel değil, bir karar kaydı** — R5'in kararı. Sessizce
aşılmaz; değiştirilirken başlığa yeni karar satırı yazılır: `R6: teknesyum-ui 2026-08 ile hizalandı`.

Bu yığın kullanıcıya görünen bir **kusur** değil, stilistik uyum. Testi de yok; geri dönüşü göz
kontrolü. 0.2.0 yayınlandıktan sonra tek parça hâlinde yapılır.

---

## Yapılmayacaklar

- **.NET 9/10 yerleşik dark mode'a geçmek.** `Application.SetColorMode` hâlâ
  `[Experimental("WFO5001")]`; `DataGridView` koyu paleti PR açık, `MessageBox` koyu değil. Runly'nin
  en çok emek verdiği iki katman framework'ün bitmemiş kısmı.
- **`EarTrumpet`'ten kod almak.** Lisansı OSI değil.
- **Windows 11 ön bağlam menüsü.** Ayrı C++ DLL + imzalı sparse package; kazancına değmez.

---

## Sıra

```
U3 (DPI, font metriğinden türet)  ─┐
U1 (simge)                         ├─ paralel, birbirine değmiyor
U4 (native sızıntılar)            ─┘
        ↓
U2 (IAssocHandler)  ·  U5 (risk notları)   ─ paralel
        ↓
U6 (EN taşma)  ·  U7 (liste başarımı)      ─ paralel, ikisi de doğrulama ağırlıklı
        ↓
0.2.0 yayın
        ↓
Yığın 1 (0.3)
```

U3 önce gidiyor çünkü diğerlerinin ölçü tabanını o kuruyor.
