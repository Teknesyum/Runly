# Danışma 001: Runly Ayarlar Penceresi — Tasarım Gözden Geçirme

claude-fable-5-1, 7.288 çıktı token, 130 sn. Girdi: [001-fable-tasarim-gozden-gecirme-girdi.md](001-fable-tasarim-gozden-gecirme-girdi.md). Cevap olduğu gibi:

---

# Danışma 001 — Cevap

Ekran görüntüsünü (`tmp/simdi-125.png`), `MainForm.cs` düzen kodunu (satır 230-410, 658-760) ve eski dış bakış notlarını okudum. Kök sorun tek cümleyle: **arama şeridi ve iki düğme sırası ızgaranın sütunlarına bağlı değil, serbest akan `FlowLayoutPanel`'lerde elle piksel verilerek hizalanmaya çalışılmış.** Ne zaman yazı ölçeği ya da pencere genişliği değişse hiza bozuluyor. Aşağıdaki 10 madde önem sırasıyla.

## 1. Arama şeridini ızgaranın üç sütununa oturt → simetri buradan başlar

**Ne değişir:** `searchStrip` ayrı bir 2 sütunlu tablo olmaktan çıkar; `gridArea`'nın 0. satırına üç hücre olarak doğrudan yerleşir (bugün `SetColumnSpan(searchStrip, 3)` ile tek hücreye yayılıyor, satır 398-399).

- Sütun 0 (kategori rayı, 210 px): boş bırakılır ya da küçük "Kategoriler" başlığı (H3, renk-1) — tablonun "Etkin" sütunuyla aynı üst çizgide.
- Sütun 1 (tablo): arama kutusu, **tablonun sol kenarıyla hizalı başlar**, `Dock = Fill` değil `Anchor = Left|Right` ile tablonun genişliğinin yarısına kadar uzar (`MaximumSize.Width = Metrics.Px(480)`).
- Sütun 2 (ayrıntı paneli, 300 px): toplu atama açılır kutusu, **Ayrıntılar panelinin sol kenarıyla hizalı**.

**Neden:** Bugün etiket kategori sütununun, kutu tablonun ortasında başlıyor; göz üç ayrı sol kenar görüyor. Üç hücre üç sütunun kenarını paylaşınca "neden burada" sorusu kalkar.

**WinForms:** `gridArea.Controls.Add(searchCell, 1, 0); gridArea.Controls.Add(bulkCell, 2, 0);` — span yok. Her hücre tek `TableLayoutPanel` (2 sütun: `Percent 100` + `AutoSize`), içindeki her denetim `Anchor = Left | Right` ve `Margin` yalnız yatay. Dikey ortalama için `RowStyle Absolute` + `Anchor = None` yerine `Dock = Fill` + `TextAlign = MiddleLeft`; elle 11/5/8 px üst boşlukları silin.

## 2. "Uzantı ara" etiketi kalkar, yer tutucu + kutu içi ✕ gelir

**Ne değişir:** H3 etiket silinir. Kutuya yer tutucu: "Uzantı, tür ya da uygulama ara  (.md, markdown, notepad)". Kutunun sağ içinde, metin varken görünen ✕ simgesi. "Temizle" düğmesi kalkar. Sağdaki "Örnek: …" ipucu metni kalkar; **aynı etiket yalnızca sonuç sayısını gösterir** ("12 eşleşme" / "Eşleşme yok", Mono, renk-2) ve kutunun hemen altına değil, kutunun sağına, Body hizasında.

**Neden:** Etiket + yer tutucu + ipucu aynı bilgiyi üç kez veriyor; H3 etiket arama kutusunu bir "bölüm başlığı"na çeviriyor ve dikey hizayı kıran da o (H3'ün satır yüksekliği kutunun yüksekliğinden farklı). Kutu içi ✕ sektör standardı; ayrıca Esc zaten temizliyorsa (`OnSearchBoxKeyDown`) düğme gereksiz.

**WinForms:** `NeonTextBox`'a `PlaceholderText` (zaten .NET'te var) ve `OnPaint` içinde sağ `Space3` genişliğinde ✕ bölgesi; `MouseClick` ile `ClearSearch()`. Bu kadar özel boyama zaten `NeonTextBox`'ta var, küçük iş.

## 3. Toplu atama arama satırından çıkar, tablo altındaki düğme sırasına "kategori işlemleri" olarak iner

**Ne değişir:** Açılır kutu + "Kategoriyi bu uygulamayla aç" düğmesi 2. maddedeki sütun 2 hücresine **geçici** olarak gidebilir; kalıcı çözüm tablo altı düğme sırasının sağ ucu. Düğme metni kısalır: "Kategoriyi aç:" etiketi + açılır kutu + "Uygula" (birincil). Açılır kutunun yer tutucusu: "Uygulama seçin".

**Neden:** Arama bir *filtre*, toplu atama bir *eylem*. Aynı satırda olunca kullanıcı "arama sonucuna mı uygulanıyor, kategoriye mi" diye tereddüt ediyor (dış bakış notu da "etiketsiz boş açılır kutu, ne işe yaradığı anlaşılmıyor" diyor). Eylem düğmeleri tablonun altında, filtre tablonun üstünde: okuma yönüyle uyumlu.

**WinForms:** `extButtons` `FlowLayoutPanel`'i 2 sütunlu `TableLayoutPanel` olur: sol `AutoSize` (seçim düğmeleri), sağ `AutoSize` + `Anchor = Right` (toplu atama). Orta `Percent 100` boş sütun ikisini ayırır.

## 4. Tablo altı düğme sırası: 6 → 4 ve rol ayrımı

**Ne değişir:**

| Bugün | Önerilen | Rol |
|---|---|---|
| Tümünü seç | Tümünü seç (onay kutusu olarak tablo başlığındaki "Etkin" hücresine) | – |
| Uzantı ekle | Uzantı ekle | ikincil |
| Seçili uzantıyı sil | Sil | ikincil, kırmızı değil ama **sağda ayrık** (bkz. 6) |
| Profili dışa aktar / içe aktar | tek "Profil ▾" açılır düğme (Dışa aktar / İçe aktar) | ikincil |
| Uygulama seç… | **kalkar** — ayrıntı panelindeki kalır | – |

**Neden:** "Uygulama seç…" aynı ekranda iki yerde; seçili satırın eylemi olduğu için bağlamı ayrıntı paneli. "Tümünü seç" bir düğme değil durum; başlık onay kutusu hem yer kazandırır hem standarttır. İki profil düğmesi haftada bir kullanılan şey, bir açılırda saklanabilir.

**WinForms:** `DataGridView` başlık hücresine onay kutusu: `CellPainting` ile çizim + `ColumnHeaderMouseClick`. Açılır düğme: `NeonButton` + `ContextMenuStrip.Show(button, 0, button.Height)`. Orta iş; başlık onay kutusu yarım gün, gerisi küçük.

## 5. En alt düğme sırası: üç gruba böl, yıkıcıyı ayır

**Ne değişir:** Sol → sağ, `Space5` boşlukla ayrılmış üç grup:

- **Sol (kurulum):** Kur / Güncelle (birincil) · Kaldır (ikincil, **renk-2 metin, kenarlık yok**, soldaki birincilden `Space4` değil `Space5` uzak) · Yedekten geri yükle
- **Orta (pencere):** Sağ menü… · Yenile  — ya da ikisi de başlık çubuğuna/araç menüsüne çıkar
- **Sağ (form):** Kaydet (birincil) · Kapat

Birincil düğme bir sırada en çok **iki** olur ve ikisi aynı grupta olamaz: "Kur / Güncelle" kurulum grubunun, "Kaydet" form grubunun ucunda.

**Neden:** Bugün 7 düğme tek ağırlıkta, "Kaldır" ile "Kaydet" yan yana komşu. Gruplama hangisinin geri alınabilir olduğunu söylüyor. Yıkıcı için yeni renk uydurmuyoruz: kenarlıksız + renk-2 metin + mesafe yeterli ayrım.

**WinForms:** `buttonsFlow` (RightToLeft akış, satır 435) yerine 5 sütunlu `TableLayoutPanel`: `AutoSize | Percent 50 | AutoSize | Percent 50 | AutoSize`. `_progressLabel` orta sütuna taşınır (ilerleme metni zaten ortada okunur).

## 6. Güvenlik / Davranış panellerini iki sütunlu tek ızgaraya bağla

**Ne değişir:** İki `NeonGroupPanel`'in içi bugün ayrı `TableLayoutPanel`'ler (satır 659, 716) ve kendi girintileri var; sonuç: Güvenlik'te "Güvenilen klasörler" başlığı radyolardan 10 px daha solda, Davranış'ta "Düzenleyici komutu" etiketi dikeyde kutuyla hizasız, altında büyük boş alan. Her iki panel aynı iç şablonu kullanır:

- Sütun A `Absolute Metrics.Px(160)`: etiketler, `TextAlign = MiddleLeft`, Body **kalın değil** (bugün küçük kalın mavi "Güvenilen klasörler", dış bakış "zayıf okunuyor" demiş).
- Sütun B `Percent 100`: alan.
- Sütun C `AutoSize`: alanın düğmeleri (Ekle/Çıkar, Seç…/Test et).

Satırlar: Güvenlik → İzin (radyolar yatay değil **dikey**, bugünkü gibi) / Güvenilen klasörler / Güvenilen dosyalar. Davranış → Pencere / Düzenleyici / Günlük / Yönetici. "Günlük tut" ve "Betikleri yönetici olarak çalıştır" iki ayrı satıra iner; "Günlük klasörünü aç" "Günlük tut" satırının C sütununa.

**Neden:** Etiket sütunu iki panelde aynı genişlikte olunca göz tek bir ızgara görür; Davranış panelindeki boşluk dolar çünkü satır sayısı eşitlenir (3+3 yerine 4+4).

**WinForms:** Ortak bir `NeonFormGrid : TableLayoutPanel` yardımcı sınıfı (`AddRow(label, field, trailing)`); radyoların `FlowLayoutPanel`'i B sütununa `ColumnSpan 2`. Orta iş, iki panel birden yeniden yazılır.

## 7. Dikey ritim: üç basamak, başka yok

**Ne değişir:**

- **Bölümler arası** (başlık çubuğu ↔ arama, arama ↔ tablo, tablo ↔ paneller, paneller ↔ alt düğmeler): `Space5`.
- **Bölüm içi satırlar arası** (radyo ↔ radyo, etiket satırı ↔ sonraki): `Space3`.
- **Denetim içi** (etiket ↔ kutu yatay boşluğu, düğmeler arası): `Space2`.

Bugün karışık: `gridArea` üstte `Space4`, `searchStrip` altında `Space3`, `panelsRow` üstte `Space5`, `bottomBar` üst/alt `Space4`, arada elle 11/5/8/2 px. `Metrics.Px(11)`, `Px(5)`, `Px(2)` çağrılarının hepsi silinir; sayısal piksel yalnız sütun genişliklerinde kalır.

**Neden:** Üç basamak göz tarafından "aynı seviyedeki şeyler" olarak öğrenilir; dördüncü değer ritmi bozar. Elle verilen üst boşluklar yazı ölçeği değişince (%100 ↔ %125) kayıyor — kullanıcının "simetrik değil" şikâyetinin somut kaynağı bu.

**WinForms:** `root.RowStyles` sabitleri (`PanelsRowHeight`, `BottomBarHeight`) zaten toplam; o toplamın içine `Space5` üst/alt paddingi girsin, denetimlerde `Margin` dikey bileşeni 0.

## 8. Tablo sütunlarını sabitle, "Bulundu" ve "Argümanlar"ı daralt

**Ne değişir:** Etkin (`Absolute 48`), Uzantı (`Absolute 110`), Tür (`Absolute 100`), İşleyici (`Percent 30`), Bulundu (`Percent 30`), Argümanlar (`Percent 40`), Durum (`Absolute 170`). "Bulundu" sütunundaki yalın "X" → "—" ve araç ipucu; "✓ C:\Program Files\n…" → yalnız ✓ + dosya adı (`node.exe`), tam yol araç ipucunda.

**Neden:** Yollar üç sütunda da kesiliyor, oysa kullanıcıya gereken "bulundu mu, hangi exe". Sütun başlıkları ortalı, hücre metinleri ortalı: metin sütunlarını (İşleyici, Bulundu, Argümanlar) **sola** yaslayın; yalnız Etkin/Tür/Durum ortalı kalsın. Sol yaslı metin ekrandaki en belirgin "simetrisizlik" hissini azaltır çünkü göz tek bir okuma kenarı bulur.

**WinForms:** `DataGridViewColumn.AutoSizeMode = Fill` + `FillWeight`; `DefaultCellStyle.Alignment = MiddleLeft`; `ToolTipText` hücre bazında. Küçük iş.

## 9. Ayrıntılar paneli: boş alanı kazan, "Uygulama seç…" burada tek kalsın

**Ne değişir:** Halka + "6/38 uzantı bağlı" üstte kalır; altındaki açıklama metninin altına, seçili uzantının **eylemleri** gelir: "Bu uzantı için uygulama seç…" (ikincil), varsayılan değilse "Varsayılan yap" (uyarı çipi aynı), "Bağla / Bağlantıyı kes" (ikincil). Panel bir "özellik sayfası" gibi davranır.

**Neden:** Panelin alt üçte ikisi boş (dış bakış da işaretlemiş); tablo satırındaki "⚠ Varsayılan yap" çipine tıklamak küçük hedef, aynı eylem panelde büyük düğme olunca erişilebilir. Madde 4'teki tekrarı da buradan çözüyoruz.

**WinForms:** `detailPanel` içinde `TableLayoutPanel` 3 satır: `AutoSize` (halka), `Percent 100` (açıklama), `AutoSize` (düğme yığını, `FlowDirection.TopDown`, her düğme `Dock = Top`). Küçük iş.

## 10. Başlık çubuğu: sağ grubu seyrelt *(düşük öncelik, isteğe bağlı)*

**Ne değişir:** "Yardım · Aa %125 · ● Runly kurulu · TR|en · Destek Ol · Teknesyum" altı öğe; "Destek Ol" ve "Teknesyum" bir "···" menüsüne, "● Runly kurulu" durumu alt düğme sırasının orta sütununa (`_progressLabel` yanı) inebilir. Kalan: Yardım · Aa · TR|en.

**Neden:** Başlık çubuğu bir satır; altı tıklanabilir öğe ile pencere düğmeleri birbirine karışıyor. Ama bu çubuk markanın yüzü, kullanıcı imza öğelerini isteyebilir — bu yüzden son sırada ve **isteğe bağlı**.

---

## Uygulanabilirlik Özeti

| Madde | İş | Not |
|---|---|---|
| 1, 2, 7 | küçük–orta | **ilk tur**: hiza şikâyetinin %80'i burada biter |
| 3, 5, 8, 9 | küçük | ikinci tur, her biri yarım gün |
| 4 | orta | başlık onay kutusu `CellPainting` ister |
| 6 | **orta–büyük** | iki panel yeniden yazılır, `PanelsRowHeight` sabiti yeniden hesaplanır |
| 10 | küçük | isteğe bağlı |

**Sıra önerisi:** 7 → 1 → 2 → 5 → 3 → 8 → 9 → 4 → 6 → 10. Önce ritim sabitleri (7), çünkü sonraki her madde o üç basamağı kullanır.

**Kapsam dışı ama kullanıcının cümlesinde olan:** "Birlikte aç" menüsünde Runly ve RunlyConsole iki ayrı görünüyor, "Runly ile düzenle" sağ tıkta yok — bunlar `ShellRegistrar` / `ContextMenuCleaner` işi, tasarım gözden geçirmesi değil; ayrı bir iş olarak listelenmeli. Ekran görüntüsü v0.3.1.0, masaüstündeki en güncel sürüm.
