# Kabuk Standardı

Arayüzün ölçülebilir kuralları. Her satırın yanındaki test `tests/Runly.Core.Tests/KabukStandardiTests.cs`
içindedir; kural kaynakta bozulursa o test kırmızıya döner. Ölçülemeyen kural buraya yazılmaz.

## Renk

- Renk yalnız `src/Runly.Core/Theme/TeknesyumTokens.cs` belirteçlerinden gelir; ayarlar
  uygulamasında düz RGB (`Color.FromArgb(r, g, b)`, `ColorTranslator`, adlı sistem rengi)
  yazılmaz. Saydamlık katmak için `Color.FromArgb(alfa, Palette.X)` serbesttir. — `RenkYalnizPaletten`
- Başlatıcının Win32 renkleri (COLORREF) yalnız `NeonWindowChrome.cs` içinde tanımlanır. — `BaslaticiRenkleriTekYerde`
- Palette `Info` adında bir renk yoktur; nötr bildirim varsayılan kenar ve beyaz yazıdır. — `InfoRengiYoktur`

## Giriş

- Yer tutucu metin yok: `PlaceholderText` ve `EM_SETCUEBANNER` kullanılmaz, görünür etiket
  ve altında yardım satırı konur. — `YerTutucuMetinYok`

## Pencere

- Sistem başlık çubuğu kaldırılır, kendi çubuğumuz çizilir; kırılan her şey geri verilir:
  `FormBorderStyle.None` + `WS_THICKFRAME` kenardan boyutlandırmayı ve Aero Snap'i,
  `WM_NCHITTEST` üstünden `HTCAPTION`/`HTLEFT` sürüklemeyi ve boyutlandırmayı, çift tık
  büyütme/geri almayı verir. — `KendiBaslikCubugu`

## Bağımlılık

- Görsel tema kitaplığı yok: MaterialSkin, MetroFramework, Krypton, Guna, ReaLTaiizor,
  DarkUI, Bunifu, SunnyUI, AntdUI ve benzerleri hiçbir `.csproj`'de `PackageReference`
  değildir. — `GorselTemaKitapligiYok`

## Uygulanmayan Maddeler

- Parıltının yazıya konmaması: GDI'da yazı gölgesi ayrı bir özellik değil, kaynaktan
  ölçülemez; canlı ölçüm aracı (`tests/Runly.UiAudit`) yazı zeminini ölçer.
- Hata bildiriminin kendi kapanmaması: Runly'de kendiliğinden kapanan bildirim yok, bütün
  bildirimler kipli iletişim kutusu.
