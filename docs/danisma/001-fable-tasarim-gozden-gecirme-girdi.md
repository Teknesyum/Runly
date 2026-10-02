# Danışma 001 girdi: Runly Ayarlar Penceresi — Tasarım Gözden Geçirme

Ajana giden metin:

---

[[danisma:001]]

# Runly Ayarlar Penceresi — Tasarım Gözden Geçirme

## Kullanıcının cümlesi (aynen)

"neden ui konusunda ciddi sıkıntılarımız var masaüstündeki en güncel sürüm mü
ayrıca js ekle derken runly var runlyconsole var bir tane runly olacak aç dersem runly ile çalıştır demek ancak sağ tıklama menüsünde runly ile düzenle de olmalıydı
yukardan metin arattırmak istedim hiç simetrik değil ergonomik bir tasarım yapmamışız kullanıcı dostluğu adına fable a danış ve tüm tasarımımızı yeniden gözden geçir"

## Bağlam

Runly: Windows'ta dosya uzantılarını (.js, .ps1, .py, .md…) bir yorumlayıcı ya da düzenleyiciyle ilişkilendiren araç. Bu pencere ayarlar ekranı. WinForms, çerçevesiz koyu tema, özel boyama. Renk ve ölçüler yalnız token'dan (teknesyum-ui): space1..space6 basamakları, H3/Body/Mono yazı tipleri, renk-1 (mavi vurgu), renk-2 (mor), surface siyah. Yeni renk ya da ölçü uydurulamaz. Pencere büyütülmüş açılır (2560x1400). Yazı ölçeği %125.

Ekran görüntüsü (güncel kurulu sürüm, %125): `tmp/simdi-125.png`. Eski denetim görüntüleri: `docs/ui-denetim/2026-09-30/canli-yazi-125.png`, `ana-100-sonra.png`. Önceki dış bakış ham notları: `docs/ui-denetim/2026-09-30/dis-bakis.md`.

## Ana pencerenin şu anki düzeni (yukarıdan aşağı)

1. Başlık çubuğu: solda logo + "Runly Ayarları" + sürüm; sağda Yardım, Aa %125, Runly kurulu, TR|en, Destek Ol, Teknesyum, pencere düğmeleri.
2. Arama şeridi (tek satır, `MainForm.cs` ~338):
   - solda: "Uzantı ara" etiketi (H3, renk-1) → 280 px metin kutusu → "Temizle" düğmesi → "Örnek: .md, markdown ya da notepad" ipucu/sonuç metni;
   - sağda: etiketsiz boş açılır kutu (yüklü uygulama listesi) → birincil "Kategoriyi bu uygulamayla aç" düğmesi.
   - Üst kenar boşlukları elle 11 / 5 / 8 px verilmiş; öğelerin dikey ortası tutmuyor. Arama kutusu ne kategori listesinin ne tablonun sol kenarıyla hizalı; etiket kategori sütununun üstünde, kutu tablonun ortasına doğru başlıyor.
3. Orta alan üç sütun: sol kategori listesi (Betikler 6/34, Kod/Geliştirme 0/38 …), orta tablo (Etkin, Uzantı, Tür [Çalıştır/Aç çipi], İşleyici, Bulundu, Argümanlar, Durum [Bağlı / Bağlı değil / ⚠ Varsayılan yap]), sağ "Ayrıntılar" paneli (ilerleme halkası, açıklama, altta "Uygulama seç…").
4. Tablo altı düğme sırası: Tümünü seç, Uzantı ekle, Seçili uzantıyı sil, Profili dışa aktar, Profili içe aktar, Uygulama seç… (hepsi aynı ağırlıkta; "Uygulama seç…" sağ panelde de var).
5. İki panel: Güvenlik (3 radyo, Güvenilen klasörler listesi + Ekle/Çıkar, Güvenilen dosyalar sayacı + Tümünü temizle) ve Davranış (Pencereyi açık tut 3 radyo, Düzenleyici komutu + Seç…/Test et, Günlük tut, Betikleri yönetici olarak çalıştır, Günlük klasörünü aç).
6. Alt sağ düğme sırası: Kur / Güncelle (birincil), Kaldır, Yedekten geri yükle, Sağ menü…, Kaydet, Yenile, Kapat — hepsi aynı ağırlıkta, yıkıcı "Kaldır" ayrılmıyor.

## Soru

Kullanıcı dostu, simetrik ve ergonomik bir düzen için önceliklendirilmiş, somut bir yeniden tasarım öner. Özellikle:

1. Arama şeridi: hangi öğe nerede, neyle hizalı (kategori listesi / tablo / ayrıntı paneli sütunlarıyla), etiket gerekli mi (yer tutucu yeter mi), Temizle düğmesi mi kutu içi ✕ mi, ipucu ve sonuç sayısı nerede, toplu atama (açılır kutu + düğme) aynı satırda mı kalmalı yoksa başka yere mi gitmeli.
2. Düğme yığınları (tablo altı 6, en alt 7): gruplama, birincil/ikincil/yıkıcı ayrımı, tekrar eden "Uygulama seç…".
3. Güvenlik/Davranış panellerinin iç hizası (etiket-alan sütunları, boş alanlar).
4. Genel dikey ritim: bölümler arası boşluk hangi token basamağı.

Biçim: en çok 10 madde, önem sırasıyla; her madde "ne değişir → neden" ve mümkünse WinForms karşılığı (TableLayoutPanel sütunu, Anchor, Dock). Uygulanabilirliği düşük ya da büyük iş olanı ayrıca işaretle. Yeni renk/ölçü önerme; token basamağı adıyla konuş.
