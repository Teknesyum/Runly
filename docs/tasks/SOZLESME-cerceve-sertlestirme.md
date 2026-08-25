# Sözleşme — Çerçeve sertleştirme ve ölü DPI zincirinin onarımı

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma — dokunulan dosyalar başka bir işle çakışmıyor.

## Arka plan

Kullanıcı Ayarlar penceresini bazen **klasik açık renkli Windows başlık çubuğuyla**
görüyor. Neon başlık bandı duruyor ama üstüne sistem çubuğu biniyor.

Bu oturumda ölçülenler — bunları tekrar araştırma, veri olarak al:

- Güncel `dist\RunlySettings.exe` (v0.2.0) canlı ölçüldü: `WS_CAPTION=False`,
  pencere ve istemci dikdörtgeni birebir aynı. Maximize / restore / minimize
  turlarında ve Explorer üzerinden başlatmada da bozulmuyor. **Yeniden üretilemedi.**
- `dist-e2e\RunlySettings.exe` (v0.1.0, 13.08 14:14) klasik çubuk veriyor —
  içinde `NeonForm` sınıfı hiç yok. Ama içerik düzeni 0.1.0, kullanıcının
  gördüğü ekran 0.2.0. Yani suçlu o değil.
- Kullanıcının ekran görüntüsünde içerik **aşağı kaymamış**; konumlar güncel
  yapıyla birebir. Yani çubuk istemci alanını daraltmıyor, **üzerine çiziliyor**.
- Makinede `StartAllBack 3.9.23` kurulu (pencere çerçevesi yeniden çizen araç) ve
  sürece `RTSSHooks64.dll` enjekte oluyor. Dışarıdan stil eklenmesi mümkün.

Sonuç: kaynağı kesin saptanamadı, ama pencereye **dışarıdan** `WS_CAPTION`
eklenmesi en tutarlı açıklama. Savunma kodun kendi tarafında olmalı.

## Yapılacaklar

### 1. Çerçeve nöbeti — `src/Runly.Settings/NeonForm.cs`

`OnHandleCreated` içinde, `RemoveSystemBorder` çağrısının yanına:

- Pencere stilini `GetWindowLong(GWL_STYLE)` ile oku. `WS_CAPTION` (0x00C00000),
  `WS_DLGFRAME` (0x00400000) veya `WS_BORDER` (0x00800000) varsa **temizle** ve
  `SetWindowLong` ile geri yaz, ardından `SetWindowPos` ile
  `SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE`
  göndererek çerçeveyi yeniden hesaplat.
- `WS_THICKFRAME | WS_MAXIMIZEBOX | WS_MINIMIZEBOX` **korunacak** — bunlar kenar
  boyutlandırma, Aero Snap ve çift tık maximize için gerekli, `CreateParams`
  bilerek ekliyor.
- Aynı temizliği `WM_STYLECHANGED` (0x007D) geldiğinde de uygula ki süreç
  çalışırken dışarıdan eklenen stil de düşsün. Sonsuz döngüye girme: yalnız
  gerçekten kirli stil varsa yaz.

### 2. Ölü `ApplyDarkTitleBar` — `src/Runly.Settings/NeonControls.cs:25`

Bu metot tanımlı ama **hiçbir yerden çağrılmıyor**. İki seçenek var, ikincisini uygula:

`NeonForm.OnHandleCreated` içinden çağır. Nöbet bir şekilde delinirse ortaya
çıkan çubuk hiç değilse koyu olur, beyaz şerit görünmez. Maliyeti iki DWM çağrısı.

### 3. Ölü DPI ayarı — `src/Runly.Settings/Program.cs`

`Runly.Settings.csproj:13` `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`
diyor ama bu ayar yalnız üretilen `ApplicationConfiguration.Initialize()` üzerinden
uygulanır ve o metot **projede hiç çağrılmıyor** — süreç şu an SystemAware koşuyor.

**Dikkat, bu riskli kısım.** PerMonitorV2'yi açmak yerleşimi bozabilir çünkü
`Metrics.Initialize` yalnız `MainForm.cs:167`'de bir kez çalışıyor (`s_initialized`
bayrağı) ve hiçbir formda `OnDpiChanged` override'ı yok. Açacaksan ikisini birlikte
yap:

- `Program.cs`'te `Application.EnableVisualStyles()` öncesine
  `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);` ekle.
- `Metrics`'i DPI'ya göre yeniden hesaplanabilir hale getir; `NeonForm`'a
  `OnDpiChangedAfterParent` / `OnDpiChanged` override'ı ekleyip ölçüleri
  tazeledikten sonra `LayoutCaptionItems` + `ApplyCornerRegion` çağır ve
  `Invalidate` et.

**Yerleşim bozulursa bu maddeyi tamamen geri al** ve onun yerine csproj'daki ölü
`ApplicationHighDpiMode` satırını sil — yalan söyleyen yapılandırma bırakma.
Hangisini yaptığını raporda yaz.

## Kabul kriterleri

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı**
   (`TreatWarningsAsErrors` açık, uyarı gerçek sinyal).
2. `dotnet test Runly.sln -c Debug --no-build` → **228/228 geçer**, düşen test yok.
3. `dotnet format --verify-no-changes` → temiz.
4. Uygulama gerçekten açılıp ölçülür. `dotnet run` yerine derlenmiş exe'yi
   `Start-Process` ile aç, 6 saniye bekle, `GetWindowLong(hwnd, -16)` oku ve
   raporda **style değerini** yaz. `WS_CAPTION` çıkmamalı, `WS_THICKFRAME`
   durmalı, `GetWindowRect` ile `GetClientRect` yükseklik farkı **0** olmalı.
   Ölçümden sonra süreci kapat.
5. Nöbetin çalıştığı kanıtlanır: pencere açıkken dışarıdan
   `SetWindowLong(hwnd, GWL_STYLE, style | WS_CAPTION)` ile stil kirletilir,
   sonra pencere yeniden boyutlandırılıp stil tekrar okunur — `WS_CAPTION`
   düşmüş olmalı. Bu ölçümü raporda göster. Düşmüyorsa 1. maddeyi düzelt.

## Kurallar

- **Kod yorumu yazma** — bu depoda mevcut yorumlar bir kısıtı veya nedeni
  anlatıyor; sen de ancak öyle bir kısıt varsa yaz, "ne yaptığını" anlatan
  satır ekleme.
- Commit atma, push etme. Ben atacağım.
- Yerleşim ölçülerini ve renkleri kendi kafandan değiştirme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, 3. maddede hangi yolu
seçtin ve neden, kabul kriterlerinin ölçüm çıktıları.

---

- `NeonForm.cs`: `EnforceBorderlessStyle()` eklendi — `GetWindowLong(GWL_STYLE)` okur,
  `WS_CAPTION|WS_DLGFRAME|WS_BORDER` varsa temizler, `SetWindowLong` + `SetWindowPos`
  (`SWP_FRAMECHANGED|NOMOVE|NOSIZE|NOZORDER|NOACTIVATE`) gönderir. Yalnız stil kirliyse
  yazar, döngü yok. Çağrıldığı yerler: `OnHandleCreated`, `WM_STYLECHANGED` (0x007D),
  `Resize`. `WS_THICKFRAME|WS_MAXIMIZEBOX|WS_MINIMIZEBOX` korunuyor.
- `NeonForm.OnHandleCreated`: `NeonTheme.ApplyDarkTitleBar(this)` çağrısı eklendi (2. madde).
- 3. madde: **PerMonitorV2 açılmadı**, `Runly.Settings.csproj`'daki ölü
  `ApplicationHighDpiMode` satırı silindi. Gerekçe: makine 96 dpi tek ölçek, açmanın
  yerleşimi bozup bozmadığı canlı doğrulanamıyor; `Metrics` tek seferlik ve formlar
  `AutoScaleMode`'u etkisizleştiriyor, DPI değişiminde kontroller yeniden ölçülmüyor.
  Doğrulanamayan riskli değişiklik yerine yalan yapılandırma kaldırıldı.
- KK1 `dotnet build -c Debug`: 0 uyarı, 0 hata. KK2 `dotnet test --no-build`: 228/228 başarılı,
  0 başarısız. KK3 `dotnet format --verify-no-changes`: çıkış kodu 0.
- KK4 canlı ölçüm (yeni exe, 6 sn sonra): `style=0x16070000` — `WS_CAPTION=False`,
  `WS_THICKFRAME=True`, `WS_MAXIMIZEBOX=True`, `WS_MINIMIZEBOX=True`,
  `windowH=1000 clientH=1000 diff=0`. Süreç kapatıldı.
- KK5 nöbet kanıtı — dışarıdan `SetWindowLong(hwnd,-16, style|WS_CAPTION)`, sonra 40px küçültme:
  - yeni exe: kirletme sonrası `0x16070000` (WS_CAPTION=False), yeniden boyut sonrası
    `0x16070000` (WS_CAPTION=False, diff=0) — stil anında düştü.
  - kontrol, nöbetsiz `dist\RunlySettings.exe`: kirletme sonrası `0x16C70000`
    (WS_CAPTION=True), yeniden boyut sonrası hâlâ `0x16C70000` — kirletme tekniği çalışıyor,
    farkı yaratan nöbet.
- Kapsam dışı not: projede `.claude/relay` kökü yok, bu sözleşmede `status`/`owns` alanı da yok.
