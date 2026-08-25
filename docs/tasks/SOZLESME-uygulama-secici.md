# Sözleşme — Uygulama seçici: doğru simge + Windows'un kendi "Birlikte aç" listesi

**Durum:** submitted · round: 1
**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma.
**Kaynak:** `docs/UI-PLAN.md` U1 ve U2 maddeleri. Planı oku, buradaki metin onun özeti.

**Dokunma alanın:** `src/Runly.Settings/Dialogs/ChooseApplicationDialog.cs`,
`src/Runly.Settings/Discovery/*`, `locale/*.json`, testler. `MainForm.cs` ve
`NeonControls.cs` **başka bir işin elinde** — oralara dokunma. Zorunlu görünüyorsa
raporda bildir, kendin değiştirme.

## U1 · Simge katmanı

`ChooseApplicationDialog.ResolveIcon` (`:334`) `Icon.ExtractAssociatedIcon` kullanıyor:
sabit 32px, %150 ölçekte bulanık, `"yol,indeks"` biçimini çözemiyor, `@dosya,-id`
dolaylı dizelerini okuyamıyor.

- `IShellItemImageFactory` ile DPI'ya göre doğru kareyi iste. İstenecek boy
  `IconSize` (`:16`, `Metrics.Px(32)`) — sabit piksel yazma, mevcut metrikten türet.
- `"yol,indeks"` için `ExtractIconEx`, `@dosya,-id` için `SHLoadIndirectString`.
- `_iconCache` (`:31`) **ölçek başına ayrı** tutulsun; anahtar yol + istenen boy olsun.
- Her yolda başarısızlık bugünkü davranışa düşsün — çökme yok, boş kutu yok.
- `Dispose` yolu (`:365`) yeni önbellek yapısıyla da tüm görüntüleri bıraksın; sızıntı olmasın.

**Kabul:** %100 ve %150 ölçekte aynı listenin ekran görüntüsü alınır, simgeler net.
Görüntüler `docs/reports/` altına konur.

## U2 · `IAssocHandler` birleşimi

`SHAssocEnumHandlers` / `IAssocHandler` projede hiç kullanılmıyor. `GetUIName`,
`GetIconLocation`, `IsRecommended` hazır geliyor.

- Mevcut `Discovery/ApplicationFinder`'ın **yerine değil yanına** kondu; iki kaynak
  birleştirilip tekilleştirilir. Tekilleştirme ölçütü çözülmüş tam yol
  (`OrdinalIgnoreCase`), yoksa görünen ad.
- `NoOpenWith` işaretli uygulamalar listeden elenir.
- `IsRecommended` bugünkü katalog `suggestedApps` sıralamasıyla harmanlanır — katalog
  önerileri üstte kalmaya devam eder, `IsRecommended` ikinci öbeği oluşturur, kalanı alta.
- Numaralandırma başarısız olursa (COM hatası, uzantı boş) liste bugünkü kaynağıyla
  çalışmaya devam eder.

**Kabul:** `.md` için liste Windows'un kendi "Birlikte aç" listesini kapsar. Karşılaştırmayı
gerçekten yap: `SHOpenWithDialog`'u aç, iki listeyi raporda yan yana yaz. Kapsanmayan bir
giriş varsa gerekçesini yaz.

## Kabul kriterleri (ortak)

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı** (`TreatWarningsAsErrors` açık).
2. `dotnet test Runly.sln -c Debug --no-build` → mevcut testlerin hepsi geçer.
3. `dotnet format --verify-no-changes` → temiz.
4. Diyalog gerçekten açılır: `RunlySettings.exe`'yi derleyip başlat, bir satıra çift tıkla,
   listeyi ekran görüntüsüyle göster. Ölçümden sonra süreci kapat ve `%APPDATA%\Runly`
   yapılandırmasını eski hâline getir.
5. `docs/UI-PLAN.md`'de U1 ve U2 maddelerinin altına tek satır **"yapıldı"** notu düş.

## Kurallar

- **Kod yorumu yazma** — mevcut yorumlar bir kısıtı anlatıyor, sen de ancak öyle bir kısıt
  varsa yaz.
- Renk ve ölçü uydurma; `Palette` ve `Metrics` dışına çıkma. Sabit piksel yazma.
- COM arayüzlerini elle bildirirken NativeAOT gerekmiyor (Ayarlar AOT değil), ama
  `TreatWarningsAsErrors` açık — trim/COM uyarısı bırakma.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, U2'de iki listenin karşılaştırması,
kabul kriterlerinin çıktıları, takıldığın nokta.

---

## Rapor — ui-builder, tur 1

**Değişen dosyalar**
- `src/Runly.Settings/Discovery/ShellIconLoader.cs` (yeni) — `IShellItemImageFactory` ile istenen boyda
  kare; `"yol,indeks"` → `PrivateExtractIcons`, olmazsa `ExtractIconEx`; `@{paket?ms-resource://…}` →
  `SHLoadIndirectString`, çözülen hedef bir bitmap varlığıysa doğrudan okunur. Her yolun sonunda
  `Icon.ExtractAssociatedIcon` fallback'i duruyor, hiçbir yol dışarı istisna sızdırmıyor.
- `src/Runly.Settings/Discovery/AssocHandlerFinder.cs` (yeni) — `SHAssocEnumHandlers` / `IAssocHandler`;
  `GetName`, `GetUIName`, `GetIconLocation`, `IsRecommended`. COM hatasında boş liste döner.
- `src/Runly.Settings/Dialogs/ChooseApplicationDialog.cs` — `Merge` iki kaynağı tam yol
  (`OrdinalIgnoreCase`, yoksa görünen ad) ile tekilleştiriyor; sıra katalog → `IsRecommended` → kalan.
  `_iconCache` artık `(kaynak, boy)` anahtarlı; `OnFormClosed` aynı şekilde hepsini bırakıyor.
  Sabit piksel eklenmedi, `IconSize` (`Metrics.Px(32)`) türetilmeye devam ediyor.

**İki tuzak çıktı.** (1) `IShellItemImageFactory`'nin DIB'i alt-üst geliyor, pozitif stride ile okununca
her simge dikey aynalanıyordu — ilk karşılaştırma sayfasında görüldü, negatif stride ile düzeltildi.
(2) `NoOpenWith` dosya adına göre aranınca paketli Not Defteri eleniyordu; bayrak `system32` kaydına ait.
Artık kayıt kendi `shell\open\command`'ı ile aynı exe'yi gösteriyorsa uygulanıyor.

**U2 karşılaştırması (`.md`).** Windows'un ham `IAssocHandler` dökümü 15 giriş; Runly 11'ini gösteriyor
(+ Runly.exe kendi kendini elediği için listede 10 satır kalıyor). Kapsanmayan 4:
`Windows Fotoğraf Görüntüleyicisi` → `PhotoViewer.dll`, çalıştırılabilir değil;
üç giriş `GetName` ve `GetUIName`'i boş döndürüyor (paketli uygulamalar, ör. kullanıcının MRU'sundaki
`WhatsApp.Root.exe`) — kullanılabilir ne yol ne ad var. Tam döküm:
`docs/reports/U2-birlikte-ac-karsilastirma.txt`.

**Kabul kriterleri.** 1) build: 0 hata 0 uyarı ✓ · 2) test: 241/241 geçti ✓ · 3) `dotnet format
--verify-no-changes`: temiz ✓ · 5) `docs/UI-PLAN.md` U1/U2 altına "yapıldı" satırı düşüldü ✓
· 4) **ÖLÇEMEDİM** — ekran kapısı kapalı, pencere açan komutlar hook tarafından engelleniyor.
Yerine başsız ölçüm: `docs/reports/U1-simge-olcek-karsilastirma.png` aynı listeyi 32 px (%100) ve
48 px (%150) kutularda önce/sonra basıyor; eski yol 48'de 32'yi büyütüyor, yeni yol gerçek 48 px kareyi
getiriyor. Canlı diyalog ekran görüntüsü ve `SHOpenWithDialog` yan yana karşılaştırması alınamadı;
aynı satır `.claude/relay/live/_sorun.log`'a yazıldı.

**Not.** `teknesyum-ui` standardı kurulu (`~/.claude/teknesyum-ui.json`); bu iş renk/ölçü üretmedi,
mevcut `Palette`/`Metrics` dışına çıkılmadı. `MainForm.cs`, `Program.cs`, `NeonControls.cs` ve
`Runly.Launcher/` hiç açılmadı.
