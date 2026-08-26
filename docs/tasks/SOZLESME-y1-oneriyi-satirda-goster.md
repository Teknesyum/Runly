# Sözleşme — Y1: öneri satırda görünsün, kullanıcı yalnız onaylasın

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma.
**Bağlam:** `docs/YOL-HARITASI.md` → Y1.
**Durum:** submitted · round: 1

## Sorun

Runly makine hakkında bildiğini (`Discovery/UsageHistory.Rank`) yalnız **uygulama seçme
diyaloğunda** gösteriyor — kullanıcı satıra çift tıklarsa. Ana ızgarada işleyicisi boş
satır bugün sadece "Seçmek için tıklayın" diyor. Kullanıcı, uygulamanın zaten bildiği
şeyi elle aramak zorunda kalıyor.

Duruş: **uygulama bildiğini önerir, kullanıcı onaylar.** Form doldurtmaz.

## Yapılacaklar

### 1. Öneri ızgarada görünsün — `MainForm.cs`, satır ~883

Bugünkü kod, işleyici boşken hücrenin `NullValue`'suna `handler.choosePrompt` yazıyor.
Oradaki yorum kritik ve **korunacak**: ipucu hücrenin *değeri* olmaz, yoksa
`OnGridCellValueChanged` onu kaydeder.

Yeni davranış: işleyici boşsa `UsageHistory.Rank(extension, _config.Extensions)`
sorulur.

- Sonuç varsa `NullValue` = `Strings.Get("handler.suggested")` biçimlendirilmişi —
  içinde önerilen uygulamanın **dosya adı** geçsin (tam yol değil, sütun dar).
- Sonuç yoksa bugünkü `handler.choosePrompt` aynen kalır.
- Renk yine `Palette.TextDim`; öneri bir değer değil, ipucu.

**Başarım:** `Rank` kayıt defteri okuyor, 408 satır için satır başına çağrılamaz.
Izgara yenilemesi başına **tek sefer** hesapla ve sözlükte tut (`CatalogSearchIndex`
deseni). Ölçüm kabul kriterlerinde.

### 2. Onay anı: kullanıcı satırı etkinleştirince öneri değere dönsün

Kullanıcı `Etkin` kutusunu işaretlediğinde ve o satırın işleyicisi boşsa, öneri
**o anda** `_config` içine yazılır (`Interpreter` ya da `Kind`'a göre `OpenWith`).
Kullanıcı kutuyu işaretleyerek onayını vermiştir; bundan sonrası bugünkü akış.

- Kutu geri kaldırılırsa yazılan değer **geri alınmaz** — kullanıcı artık onu görüyor
  ve elle silebilir. Sessiz geri alma daha şaşırtıcı olur.
- İşleyicisi zaten dolu satırda hiçbir şey değişmez.
- **Kayıt defterine yazılmaz.** Bağlama yine "Kur / Güncelle" ile olur.

### 3. Metinler

`locale/tr.json` + `en.json`, `handler.choosePrompt`'un hemen yanına:

```
"handler.suggested": "{app} önerildi — değiştirmek için tıklayın"
"handler.suggested": "{app} suggested — click to change"
```

Koda gömme.

## Kabul kriterleri

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı** (`TreatWarningsAsErrors` açık).
2. `dotnet test Runly.sln -c Debug --no-build` → 252 testin hepsi + yenileri geçer.
3. `dotnet format --verify-no-changes` → temiz.
4. Birim testi: öneri seçimi saf bir işlevde olsun (uzantı + `Rank` sonucu → gösterilecek
   dosya adı ya da `null`). Boş liste, var olmayan yol, zaten dolu işleyici.
5. **Canlı ölçüm — asıl kabul.** `RunlySettings.exe` aç, `.json` ve `.txt` satırlarının
   İşleyici sütununda **`notepad++.exe` önerildi** yazdığını göster (bu makinede ikisinin
   de MRU sinyali var). Ekran görüntüsü `docs/reports/` altına.
6. **Onay ölçümü:** `.json` satırının Etkin kutusunu işaretle, hücrenin gerçek değere
   döndüğünü ve `Kaydet` sonrası `%APPDATA%\Runly\config.json` içinde `notepad++.exe`
   göründüğünü göster. Ölçüm bitince config eski hâline getirilsin.
7. **Başarım ölçümü:** ızgara yenilemesinin öneri eklenmeden önceki ve sonraki süresi
   (ms). Fark **50 ms'yi geçmesin**; geçiyorsa önbelleği düzelt. Sayıları
   `docs/reports/y1-basarim.txt` dosyasına yaz.
8. Sinyalsiz uzantı (`.zzq`) bugünkü "Seçmek için tıklayın" metnini göstersin.

## Kurallar

- **Kod yorumu yazma** — bu depodaki mevcut yorumlar ölçülmüş bir kısıtı anlatıyor;
  sen de ancak öyle bir kısıt varsa yaz.
- Renk ve ölçü uydurma; `Palette`, `Metrics` ve `Runly.Core/Theme/TeknesyumTokens`
  dışına çıkma.
- Kayıt defterine **yazma**.
- Ölçüm kodu üründe kalmasın.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, 5-7. maddelerin sonucu,
takıldığın nokta.

## Rapor — durum: submitted

Standart yürürlükte: `~/.claude/teknesyum-ui.json` var. Renk, ölçü, yarıçap uydurulmadı;
öneri ipucu bugünkü `Palette.TextDim`, kabul edilen değer hücrenin kendi rengine döner.

Değişen dosyalar:
- `src/Runly.Settings/Discovery/HandlerSuggestion.cs` (yeni) — saf seçim: uzantı + `Rank`
  sonucu → tam yol (`Pick`) ya da dosya adı (`DisplayName`), yoksa `null`.
- `src/Runly.Settings/MainForm.cs` — `_suggestedHandlers` sözlüğü + `SuggestedHandler`;
  boş işleyici hücresinin `NullValue`'su artık `handler.suggested`; `OnGridCellValueChanged`
  içinde Etkin kutusu işaretlenince öneri değere dönüyor (`AdoptSuggestedHandler`).
- `src/Runly.Settings/locale/tr.json`, `en.json` — `handler.suggested`.
- `tests/Runly.Core.Tests/HandlerSuggestionTests.cs` (yeni) — 7 test.
- `docs/reports/y1-basarim.txt`, `y1-oneri-json.png`, `y1-oneri-txt.png`,
  `y1-onay-json.png`, `y1-sinyalsiz-zzq.png`.

Kabul kriterleri:
1. Derleme: 0 hata, 0 uyarı. 2. Test: 259/259 (252 + 7 yeni). 3. `dotnet format`: temiz.
5. `.json` ve `.txt` satırları "notepad++.exe önerildi — değiştirmek i…" gösteriyor
   (`y1-oneri-json.png`, `y1-oneri-txt.png`). İki uzantı iki farklı kategoride olduğu için
   tek ekran görüntüsüne sığmadı, ikisi ayrı alındı.
6. `.json` Etkin kutusu işaretlendi → hücre notepad++.exe'nin tam yoluna döndü, `Kaydet`
   sonrası `%APPDATA%\Runly\config.json` içinde göründü (`y1-onay-json.png`). Ölçüm bitince
   config eski hâline (3294 bayt) geri yüklendi, runly.log de öyle.
7. 412 satırlık yenileme: önce 37,6 ms, sonra 69,6 ms → **+32,0 ms** (soğuk önbellek,
   tek sefer). Sıcak önbellekte +1,1 ms. Sayılar `docs/reports/y1-basarim.txt`.
8. `.zzq` bugünkü metni gösteriyor (`y1-sinyalsiz-zzq.png`).

Takıldığım noktalar:
- Sözleşme 8. maddede bugünkü metni "Seçmek için tıklayın" diye anıyor; `handler.choosePrompt`
  gerçekte "Çift tıklayın" / "Double-click". Metne dokunmadım, kriteri mevcut metinle doğruladım.
- İşleyici sütunu dar: **varsayılan pencere boyutunda** ipucu "notepad++.ex…" diye kırpılıyor,
  yalnız büyütülmüş pencerede "notepad++.exe önerildi — değiştirmek i…" okunuyor. Sütun
  genişliğine de metne de dokunmadım — hangisinin değişeceği T0'ın kararı.
- `ApplicationFinder.cs` ve `ShortcutTargetReader.cs` T0'ın işi, dokunmadım. `_sorun.log`'a yazıldı.

## T0 kapanış notu (2026-08-26)

Rapordaki iki açık soru karara bağlandı:

- **Kırpılma.** İşleyici sütunu genişletilemez: `FillWeight` etkisiz, çünkü sütunların
  en küçük genişlikleri toplamı ızgara genişliğini zaten aşıyor (yatay kaydırma çubuğu
  bunun kanıtı). U6'nın dersi de aynı yerdeydi. Bu yüzden metin kısaldı: hücre artık
  yalnız uygulama adını gösteriyor (`handler.suggested` = `{app}`, 13 karakter, tam
  sığıyor), önerinin nereden geldiğini ve nasıl kabul edileceğini anlatan cümle
  Ayrıntılar paneline taşındı (`handler.suggestedDetail`). Kanıt: `y1-oneri-txt.png`.
- **"Seçmek için tıklayın" / "Çift tıklayın".** Sözleşmenin hatasıydı, metin doğru.
  Değişiklik yok.

Ayrıca aynı turda `ApplicationFinder` argüman taşıyan Başlat menüsü kısayollarını
atlıyor — `Administrative Tools` (`control.exe /name …`) ve `Lua documentation`
(`notepad.exe <bir txt>`) listeden düştü, uygulama sayısı 167 → 165.
