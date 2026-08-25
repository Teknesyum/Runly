# Sözleşme — Kalan native sızıntılar (U4) ve risk notlarının tamamlanması (U5)

**Proje:** C:\Users\Administrator\Desktop\Projeler\Runly (.NET 8 / WinForms)
**Dal:** main üzerinde çalış, worktree açma.
**Kaynak:** `docs/UI-PLAN.md` U4 ve U5. Planı oku, buradaki metin onun özeti.

Bu iki madde 0.2.0'da **yarım** kaldı; bugünkü durumu kaynaktan doğrula, tamamlananı
tekrar yapma.

## U4 · Kalan native sızıntılar

Neon tema WinForms üzerinde; birkaç yüzey hâlâ Windows'un kendi açık renklerini gösteriyor.

- `ComboBox` açılır listesi ayrı bir pencere. Ana denetime tema uygulanması yetmiyor,
  açılır listeye de `DarkMode_CFD` gitmeli.
- Tooltip framework'te bile koyu değil (`dotnet/winforms#12420`). Özel tooltip gerekiyor —
  `Palette` renkleriyle, `Metrics` ölçüleriyle, mevcut neon çizim deseninde.
- `OpenFileDialog` ve `DataGridView`'in boş alanı **gözle taranacak**; beyaz kalan yüzey
  varsa düzeltilecek.
- `NeonControls.cs:40` `SetPreferredAppMode` ordinal imzası Windows derlemesine göre
  değişiyor; bugün koşulsuz çağrılıyor. Sürüm kontrolü ekle, desteklenmeyen derlemede
  sessizce atla.

**Kabul:** hover, focus, seçili, devre dışı ve **açık dropdown** durumlarının hepsi ekran
görüntüsüyle gezilir, görüntüler `docs/reports/` altına konur. Tek beyaz yüzey kalmaz.
Kalan varsa neden kalamadığı yazılır.

## U5 · Risk notları

Katalogda altı uzantının `riskNote`'u var (`.hta`, `.vbs`, `.wsf`, `.js`, `.ps1`, `.jar`).
LOLBAS denetimi beş uzantının katalogda **hiç olmadığını** buldu: `.vbe`, `.jse`, `.wsc`,
`.sct`, `.chm`; `.wsh` var ama notsuz.

- Ayrıntılar panelinde risk notu görünür; ızgarada satır işaretlenir. (Bir kısmı bugün
  çalışıyor olabilir — `MainForm.cs`'te `RiskNote` geçen yerleri oku, eksik olanı tamamla.)
- Not metni barındırıcıyı **adıyla** söyler (`mshta.exe`, `wscript.exe`) — kullanıcının
  kendi doğrulayabileceği tek somut ipucu.
- Eksik beş uzantı **elle sınandıktan sonra** kataloğa eklenir. Sınama: her biri için
  `%TEMP%` altında zararsız bir örnek üret, Windows'un o uzantıyı gerçekten hangi
  barındırıcıya verdiğini `assoc`/`ftype` ya da `AssocQueryString` ile ölç, raporda yaz.
  Ölçemediğini kataloğa **ekleme**.
- `.wsh` için not yazılır.
- Notlar Türkçe ve İngilizce; `locale` altyapısı neyse ona uy, koda gömme.

**Kabul:** `.hta` satırı seçilince not okunur (ekran görüntüsü). `catalog.json` testi altı
yerine **on bir** uzantıda not arar — testi güncelle.

## Kabul kriterleri (ortak)

1. `dotnet build Runly.sln -c Debug` → **0 hata, 0 uyarı**.
2. `dotnet test Runly.sln -c Debug --no-build` → hepsi geçer, katalog testi güncellenmiş hâliyle.
3. `dotnet format --verify-no-changes` → temiz.
4. Uygulama gerçekten açılır, yukarıdaki gezinti yapılır, süreç kapatılır ve
   `%APPDATA%\Runly` yapılandırması eski hâline getirilir.
5. `docs/UI-PLAN.md`'de U4 ve U5 altına tek satır "yapıldı" notu.

## Kurallar

- **Kod yorumu yazma** — mevcut yorumlar bir kısıtı anlatıyor, sen de ancak öyle bir kısıt
  varsa yaz.
- Renk ve ölçü uydurma; `Palette` ve `Metrics` dışına çıkma, sabit piksel yazma.
- Risk notu metnini abartma, korkutma dili kullanma: ne olduğunu ve hangi barındırıcının
  çalıştıracağını söyle, o kadar.
- Commit atma, push etme.

## Rapor

Türkçe, en fazla 15 satır: hangi dosyada ne değişti, beş uzantının barındırıcı ölçümü,
hangi yüzeylerin beyaz kaldığı, kabul çıktıları.
