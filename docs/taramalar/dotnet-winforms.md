# dotnet/winforms

## 1. Künye

- Depo: `dotnet/winforms`
- Lisans: **MIT**. .NET Foundation marka politikası ayrı; ad korumalı, kod MIT.
- Yıldız: 4.847 · Açık issue: 861 · Son commit: 2026-08-21
- Son kararlı sürüm: `v10.0.8` (2026-05-12); servis dalları `v9.0.16`, `v8.0.27`

## 2. Ne yapıyor

WinForms'un kaynak kodu — Runly'nin üstünde durduğu çerçeve: `System.Windows.Forms`,
`.Primitives`, `System.Drawing`, tasarımcı katmanları.

## 3. Runly ile kesişimi

- **Koyu mod API'si:** `Application.SetColorMode(SystemColorMode)` + `IsDarkModeEnabled`
  (`src/System.Windows.Forms/.../Application.cs`). Deneysel: `list-of-diagnostics.md`
  **`WFO5001`** kaydında "NET9.0, değerlendirme amaçlı, kaldırılabilir" yazıyor. Runly'nin
  .NET 8'de kalıp temayı elle çizme kararı bu satırla doğrulanıyor.
- **Varsayılan `Classic`:** `ColorMode => s_colorMode ?? SystemColorMode.Classic` —
  çağırmadıkça hiçbir şey değişmiyor; sızıntı riski yok, kazanç da yok.
- `DataGridView` çizimi, `ListBox.DrawItem` ve DPI hataları burada izleniyor: "bizde mi
  çerçevede mi" sorusunun tek cevap yeri.

## 4. Alınacak fikir

1. **Deneysel yüzeyi tanılama kimliğiyle işaretlemek.** `WFO5001`/`WFO5002` deseni: API'yi
   silmek yerine derleyici uyarısıyla kilitlemek. Runly'nin kararsız yüzeyleri (tema
   tokenleri, katalog şeması) için aynı ucuz kalıp.
2. **`SystemColorMode` üçlüsü — `Classic`/`System`/`Dark`.** "Sistemi izle" seçeneği için
   hazır, doğrulanmış üçlü; ikili aç/koyu yetmiyor.
3. **`docs/list-of-diagnostics.md` biçimi.** Tek tabloda kimlik/sürüm/mesaj — Runly hata
   kodları için 20 satırlık karşılığı yazılabilir.

## 5. Kaçınılacak hata

Açık issue listesi koyu modun **hâlâ sızdırdığını** gösteriyor; hepsi Runly'nin elle
çizmekle atladığı tuzaklar:

- `#12280` `ListView.GridLines` fazla parlak · `#14578` ilk `TabPage`'teki `ComboBox` teması
  yanlış · `#14785` koyudan açığa geçişte `Button` tıklaması bozuluyor · `#14909` geçici alt
  formlar görünmüyor · `#14866` `Appearance=Button` kontroller renk özelliklerini yok sayıyor.

Ders: `SetColorMode` anahtar değil, kısmi boyama. Bir kontrol grubu için açıp gerisini
elle çizmek en kötüsü — iki sistem çakışır.

## 6. Doğrulama

- Künye ve sürüm etiketleri `gh api` ile alındı, doğrulandı.
- `Application.cs`'teki `ColorMode` / `SetColorMode` / `IsDarkModeEnabled` ve
  `list-of-diagnostics.md`'deki `WFO5001` girdisi doğrudan okundu.
- Issue gövdeleri okunmadı, bir kısmı açık PR olabilir — **kapanma durumu doğrulanamadı**.
- Koyu modun .NET 11'de deneysel kalıp kalmayacağı **doğrulanamadı**.

## Kaynaklar

- https://github.com/dotnet/winforms · `gh api repos/dotnet/winforms/releases`
- `src/System.Windows.Forms/System/Windows/Forms/Application.cs` · `docs/list-of-diagnostics.md`
