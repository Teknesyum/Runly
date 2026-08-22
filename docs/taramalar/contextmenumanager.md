# BluePointLilac/ContextMenuManager

## 1. Künye

- Depo: `BluePointLilac/ContextMenuManager`
- Lisans: **GPL-3.0** — güçlü copyleft, tek satır almak Runly'yi GPL'e sokar. **Kod alınamaz.**
- Yıldız: 19.860 · Açık issue: 175 · Son commit: 2024-05-05 (push 2024-08-17)
- Son etiketli sürüm: `3.3.3.1` (2021-08-28) — **sürümsüz beş yıl**. Bağımlılık kurulmaz.

## 2. Ne yapıyor

Windows sağ tık menüsünü kayıt defteri üzerinden yöneten .NET Framework WinForms uygulaması;
menü girdilerini tarar, açar/kapatır, siler, simge ve komut düzenletir.

## 3. Runly ile kesişimi

Runly'ye en yakın depo: aynı sorun sınıfı (kayıt defteri tabanlı shell yapılandırması),
aynı çerçeve (koddan kurulan WinForms), aynı çözüm (elle çizim).

- **Elle çizilmiş kabuk:** `Controls/` altında `MyMainForm`, `MySideBar`, `MyToolBar`,
  `MyStatusBar`, `MyListBox`, `MyCheckBox`, `SelectDialog`, `MessageBoxEx` — Runly'nin
  kategori rayı + tablo + özel `MessageBox` üçlüsünün karşılığı.
- **Simge çekme:** `Methods/ResourceIcon.cs` içinde `SHGetFileInfo`, `ExtractIconEx`,
  `LoadImage` ve `"shell32.dll,-123"` biçimli dizeyi ayrıştıran
  `GetIcon(string, out string, out int)` — Runly'nin simge sütunuyla aynı iş.
- **Yükseltilmiş erişim:** `RegTrustedInstaller.cs`, `ElevatedFileDroper.cs`. DPI: `HighDpi.cs`.
- Koyu tema **yok** — sabit açık tema; bu eksende ders vermiyor.

## 4. Alınacak fikir

1. **`ResourceIcon.GetIcon(iconLocation, out path, out index)` deseni.** `DefaultIcon` hep
   `"yol,indeks"`; ayrıştırma + `ExtractIconEx` + `DestroyIcon` tek yardımcıda. ~80 satır
   P/Invoke, sıfır bağımlılık.
2. **`MySideBar` + `MyToolBar` ayrımı.** Gezinme ile eylem ayrı kontrol, ayrı dosya;
   Runly'de ikisi de `MainForm` içinde — ayırmak `NeonControls.cs`'i hafifletir.
3. **`ToolTipBox.cs`.** Hücre başına tooltip yerine tek paylaşılan örneği konumlandırmak.

## 5. Kaçınılacak hata

- **`MyListBox` aslında `ListBox` değil.** `Panel` + `AutoScroll`, içinde `FlowLayoutPanel`
  ve satır başına bir `UserControl`. 400+ satırda satır başına bir pencere tanıtıcısı —
  Runly'nin owner-draw `DataGridView` tercihi doğru. Yazar kaydırmayı yavaşlatmış:
  `OnMouseWheel` deltayı `Math.Sign(e.Delta) * 50` ile sabitliyor, gerekçe yorumda
  "çok hızlı kaydırınca yeniden çizim yetişmiyor".
- **`HighDpi.DpiScale` tek seferlik ve tek monitör:** `PrimaryScreen.Bounds.Width /
  SystemParameters.PrimaryScreenWidth`, `static readonly`. Monitör başına ve çalışma anı DPI
  değişimi kapsanmıyor; üstelik yalnız bunun için `PresentationFramework` (WPF) çekiyor.
- **Ölü bakım:** son sürüm 2021, 175 açık issue; `#118`/`#243` Windows 11 24H2 desteğini soruyor.

## 6. Doğrulama

- Künye, sürüm ve issue sayıları `gh api` ile doğrulandı.
- `HighDpi.cs`, `MyListBox.cs` okundu; `ResourceIcon.cs` imza düzeyinde okundu.
- `Controls/` ve `Methods/` listeleri API'den; içerikleri okunmadı, issue gövdeleri (Çince)
  okunmadı — **iç davranış ve issue ayrıntısı doğrulanamadı**.

## Kaynaklar

- https://github.com/BluePointLilac/ContextMenuManager · `gh api repos/.../releases`
- `BluePointLilac.Methods/HighDpi.cs`, `ResourceIcon.cs` · `BluePointLilac.Controls/MyListBox.cs`
