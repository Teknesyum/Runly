# Plan — Ana Pencere Yeniden Düzeni

Kaynak: [danışma 001](danisma/001-fable-tasarim-gozden-gecirme.md). Kullanıcı şikâyeti: arama şeridi simetrik değil, tasarım ergonomik değil.

## Bu Tur

1. **Arama şeridi ızgaraya oturur.** `searchStrip` kalkar; arama kutusu `gridArea` satır 0, sütun 1'e (tablonun sol kenarı), sütun 0'a "Kategoriler" başlığı. Elle 11/5/8 px üst boşluklar silinir, dikey hiza `Anchor` ile.
2. **Etiket, Temizle ve ipucu kalkar.** `NeonSearchBox`: kendi çizdiği yer tutucu (odakta da görünür) + kutu içi ✕. Sonuç etiketi yalnız arama varken görünür.
3. **Toplu atama tablo altına iner.** Sağ uçta "Kategoriyi aç:" + açılır kutu + "Uygula" (birincil).
4. **Tablo altı:** yinelenen "Uygulama seç…" kalkar (ayrıntı panelinde kalır).
5. **En alt sıra üç grup:** kurulum (Kur / Güncelle, Kaldır, Yedekten geri yükle) · araç (Sağ menü…, Yenile, ilerleme metni) · form (Kaydet birincil, Kapat). Kaldır: `NeonButton.Danger`, kenarlıksız renk-2 metin.
6. **Tablo:** İşleyici / Bulundu / Argümanlar sola yaslı; Bulundu yalnız dosya adı, tam yol ayrıntı panelinde; "✗" yerine "Bulunamadı".

## Sonraki Tura

- Güvenlik / Davranış panellerinin ortak etiket sütunu (danışma md. 6, orta–büyük).
- "Tümünü seç" yerine başlık onay kutusu, profil düğmelerinin tek açılır düğmede birleşmesi (temalı menü altyapısı yok).
- Başlık çubuğunu seyreltme (md. 10, isteğe bağlı; teknesyum-ui başlık kuralıyla çelişir).

## Dosyalar

`MainForm.cs`, `NeonFields.cs`, `NeonControls.cs`, `locale/tr.json`, `locale/en.json`, `CHANGELOG.md`.
