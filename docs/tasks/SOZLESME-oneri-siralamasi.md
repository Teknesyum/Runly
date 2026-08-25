# Sözleşme — Öneri sıralaması: kullanıcının fiilî alışkanlığı en üste

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma.

## Şikâyet

> "Notepad önerdi ama Notepad++ önermedi, oysa benim kullandığım main text editör
> Notepad++ — ilk sırada önermeliydi."

Doğru şikâyet. Bugün sıralama şöyle (`Dialogs/ChooseApplicationDialog.cs:224`):

```
katalog suggestedApps  →  IAssocHandler.IsRecommended  →  alfabetik
```

Katalog 408 uzantı için **jenerik** öneri taşıyor (`notepad.exe`, `Code.exe`). Kullanıcının
o makinede gerçekten kullandığı uygulama sıralamaya **hiç girmiyor** — üçüncü bir sinyal
olarak var bile değil.

Windows bu bilgiyi zaten tutuyor. Bu makinede ölçüldü:

```
HKCU\...\Explorer\FileExts\.json\OpenWithList   MRUList=ba   a=notepad++.exe  b=Runly.exe
HKCU\...\Explorer\FileExts\.txt\OpenWithList    MRUList=cab  c=notepad++.exe
```

## Karar (ikinci görüş alındı)

Sıra **kullanıcı alışkanlığı → Windows önerisi → katalog** olacak. Katalog "hiç sinyal
yokken makul varsayılan"dır; sinyal varken susar.

**Sınır:** öneriyi doldurmak serbest, kayıt defterine yazmak değil. Hiçbir adımda
kullanıcı onayı olmadan bağlama yapılmaz.

## Yapılacaklar

### 1. Alışkanlık sağlayıcısı — `src/Runly.Settings/Discovery/UsageHistory.cs` (yeni)

`AssocHandlerFinder` deseninde, uzantı başına sıralı bir aday listesi döndürür. Okunacak
kaynaklar, **bu sırayla ağırlıklı**:

1. `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\<uzantı>\OpenWithList`
   — `MRUList` değeri harf sırasını verir (`"cab"` = önce `c`, sonra `a`, sonra `b`).
   **Bu sıra korunacak**, alfabetiğe çevrilmeyecek.
2. Aynı anahtarın altındaki `OpenWithProgids` — ProgID'den `shell\open\command` ile
   çalıştırılabilir yola çözülür.
3. **Runly'nin kendi geçmişi:** `_config.Extensions` içinde kullanıcının daha önce seçtiği
   `Interpreter` / `OpenWith` yolları. Aynı uzantı için birebir, ayrıca aynı `category`
   içindeki komşu uzantılar için ikinci ağırlıkta (kullanıcı `.md` için Notepad++ seçtiyse
   `.txt` önerisinde de öne çıksın).

Kurallar:

- `Runly.exe` ve `RunlySettings.exe` listeden **elenir** — kendini önermek saçma.
- Var olmayan yol elenir; paketli uygulamalar (`...!App` biçimi) yola çözülemiyorsa atlanır.
- Kayıt defteri okunamazsa sağlayıcı **boş liste** döner, uygulama eskisi gibi çalışır.
- `UserAssist`, `MuiCache`, atlama listeleri **bu sözleşmede yok** — getirisi marjinal,
  ayrıştırması kırılgan. Sonraki tura bırakıldı.

### 2. Sıralama — `Dialogs/ChooseApplicationDialog.cs`

`Merge` üç değil dört sinyalle sıralasın:

```
kullanıcı alışkanlığı (MRU sırası korunarak)
  →  IAssocHandler.IsRecommended
  →  katalog suggestedApps
  →  alfabetik
```

`AppChoice` kaydına alışkanlık için bir alan ekle (örn. `UsageRank`, düşük olan önde,
sinyal yoksa `int.MaxValue`). Mevcut `Suggested` / `Recommended` alanları kalsın.

### 3. Önden seçili açılış

- Diyalog açıldığında **en iyi tahmin seçili** gelsin ve listede görünür olsun. Bugün
  `currentPath` varsa o seçiliyor; yoksa hiçbir şey seçili değil — artık sıralamanın ilk
  satırı seçili gelir.
- `Enter` seçili adayı onaylasın (bugünkü OK davranışı).
- **Kayıt defterine hiçbir şey yazılmaz.** Seçim yine kullanıcının onayıyla `_config`'e
  girer, bağlama yine "Kur / Güncelle" ile olur. Bu maddede o akış değişmiyor.

### 4. Kullanıcı neyin okunduğunu görsün

Ayrıntılar panelinde ya da diyalogun üstünde tek satır: önerinin **nereden** geldiği —
"bu uzantıyı en son bununla açtınız", "Windows öneriyor", "Runly kataloğu". Metin
`locale/tr.json` + `en.json`'a yeni anahtarla girer, koda gömülmez.

Gerekçe: makinenin kullanım geçmişini okuyan bir araç, neyi okuduğunu söylemek zorunda.
SPEC'teki güvenlik duruşu bunu gerektiriyor.

## Kabul kriterleri

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı** (`TreatWarningsAsErrors` açık).
2. `dotnet test Runly.sln -c Debug --no-build` → mevcut testlerin hepsi geçer.
3. `dotnet format --verify-no-changes` → temiz.
4. `UsageHistory`'nin **saf** kısmı için birim testi: `MRUList` çözümlemesi (`"cab"` sırası),
   eksik/bozuk `MRUList`, kendini eleme, var olmayan yol, boş uzantı. Kayıt defterine
   dokunmayan bir arayüzün arkasına al ki test edilebilsin.
5. **Canlı ölçüm — asıl kabul.** `RunlySettings.exe --select .json` ile aç, uygulama seçme
   diyaloğunu getir ve ekran görüntüsü al: **`notepad++.exe` ilk sırada ve seçili** olmalı.
   Bu makinede `.json` için `OpenWithList` MRU'su `notepad++.exe` ile başlıyor, yani
   sinyal var. Aynısını `.txt` için tekrarla. Ekran görüntülerini `docs/reports/` altına koy.
   Ölçüm bitince `%APPDATA%\Runly\config.json` eski hâline getirilsin.
6. Sinyalsiz bir uzantı (örn. `.zzq`) için liste bugünkü davranışına düşsün — katalog önde,
   çökme yok. Bunu da göster.

## Kurallar

- **Kod yorumu yazma** — bu depodaki mevcut yorumlar bir kısıtı anlatıyor; sen de ancak öyle
  bir kısıt varsa yaz.
- Renk ve ölçü uydurma; `Palette` ve `Metrics` dışına çıkma.
- Kayıt defterine **yazma**. Bu sözleşme yalnız okur.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, 5. maddedeki iki ekran görüntüsünün
sonucu, hangi sinyalin hangi uzantıda devreye girdiği, takıldığın nokta.

---

## Rapor — durum: submitted

- `Discovery/UsageHistory.cs` (yeni): `IUsageHistorySource` arayüzü kayıt defterini soyutluyor,
  `Rank` ve `OrderByMru` saf; gerçek okuma iç sınıf `RegistryUsageHistorySource`'ta
  (OpenWithList + MRUList, OpenWithProgids, App Paths / `Applications\...\shell\open\command`).
- `Dialogs/ChooseApplicationDialog.cs`: `AppChoice`'a `UsageRank` eklendi, sıralama
  alışkanlık → `IsRecommended` → katalog → alfabetik oldu; kaynak satırı (`_sourceLabel`)
  eklendi; ilk satır seçili ve görünür geliyor.
- `MainForm.cs`: yalnız çağrı satırı — `UsageHistory.Rank(extension, _config.Extensions)`.
- `locale/tr.json` + `en.json`: `chooseApp.sourceUsage/sourceWindows/sourceCatalog/sourceNone`.
- `tests/Runly.Core.Tests/UsageHistoryTests.cs`: 11 test — MRU `"cab"` sırası, eksik/bozuk
  MRUList, çözülemeyen yol, paketli `!App` atlama, kendini eleme, boş uzantı, ProgID ağırlığı,
  Runly'nin kendi geçmişi (aynı uzantı > kategori komşusu), yinelenen yol.
- Kabul 1-3: build 0 hata/0 uyarı, `dotnet test` 252/252 geçti, `dotnet format` temiz.
- Kabul 5 (canlı): `docs/reports/oneri-siralamasi-json.png` ve `-txt.png` — her ikisinde de
  `notepad++.exe` **ilk sırada ve seçili**, üst satır "Bu uzantıyı en son bununla açtınız
  (Windows kullanım geçmişi)." yazıyor. `.json` sinyali MRU `"ba"` (b=Runly.exe elendi),
  `.txt` sinyali MRU `"cab"` (a=paketli Notepad atlandı, ikinci sıraya firefox geldi).
- Kabul 6: `docs/reports/oneri-siralamasi-zzq.png` — sinyalsiz uzantıda liste alfabetik,
  çökme yok, satır "Bu uygulama için öneri sinyali yok." diyor.
- `%APPDATA%\Runly\config.json` ölçüm öncesi yedeklendi ve sonrasında geri yüklendi.
- Takıldığım nokta: ızgarada çift tıklayamadım; diyaloğu "Uygulama seç…" düğmesine
  `PostMessage(BM_CLICK)` ile açtım, görüntüyü `PrintWindow` ile aldım.
- Kapsam dışı not: `ChooseApplicationDialog` düzenine bir satır eklendi (RowCount 4→5);
  ölçüler mevcut `Metrics.Row(Palette.Help, 8)` ile türetildi, yeni sabit uydurulmadı.
