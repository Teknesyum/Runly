# Dış Bakış — Ham Yanıt

Alt ajan: sonnet, genel amaçlı. Verilen: yalnız klasör yolu ve `*-sonra.png` ad biçimi; kural verilmedi.
Görüntüler: `sonra` koşusu (sağ menü taşma düzeltmesinden önceki kareler).

---

## ana

- %100: Tablo dolu, okunaklı. Sağ üstteki "Kur/Güncelle... Kaydet Yenile Kapat" satırındaki düğmeler biraz sıkışık ama okunuyor. Genel olarak sorun görmedim.
- %125: Aynı düzen korunuyor, tablo yatay kaydırma çubuğu görünüyor, "Durum" sütunundaki "⚠ Varsayılan yap" uyarısı sağ kenara taşıp kırpılıyor (aynı sorun %100'de de var, biraz daha belirgin).
- %150: Sol kategori listesi ("3B ve Tas...", "Ofis/Doku...") isimleri kısaltılmış kalıyor — kısaltma her ölçekte var, yeni bir bozulma yok. Genel yerleşim sağlam kalmış.

## kaldir-onay

- %100: Net, okunaklı, sorun görmedim.
- %125: Aynı, sorun yok.
- %150: Aynı, sorun yok. Üç ölçekte de tutarlı ve temiz.

## mesaj

- %100: Basit "Evet/Hayır" onay kutusu, düzgün.
- %125: İçerik aynı kalıyor ama pencere çok büyürken metin ve düğmeler sol üstte kalıp altta/sağda geniş boş bir alan oluşuyor — ölçek büyüdükçe orantısız boşluk artıyor.
- %150: Aynı boşluk sorunu daha da belirginleşiyor; pencere neredeyse yarısı boş, ilk kullanıcı için "eksik bir şey mi var" izlenimi verebilir.

## sag-menu

- %100: Liste kutusunun altındaki son satır ("DriveFS ContextMenu Handler") kırpılmış görünüyor, checkbox'ı görünmüyor; sağ alttaki "Vazgeç" düğmesi pencere kenarında kesiliyor (metin tam okunmuyor).
- %125: Aynı kırpılma sorunu son satırda devam ediyor; "Vazgeç" düğmesi bu sefer tam görünüyor, %100'e göre daha iyi.
- %150: Yine son liste satırı kırpılıyor (checkbox yok), ama düğmeler tam görünüyor. Bu davranış scroll alanının sonu olduğu için normal olabilir, emin değilim — ama ilk kullanıcı bu son satırı işaretlenebilir sanıp checkbox arayabilir.

## sag-menu-bos

- %100: Boş durumda aynı uyarı metni ("Runly türlerinde gizlenebilecek başka öğe bulunamadı.") hem küçük kutuda hem de altındaki büyük siyah alanda iki kez tekrar ediyor, altında da çok büyük boş siyah alan kalıyor — tuhaf ve amaçsız görünüyor.
- %125: Aynı çift mesaj tekrarı ve büyük boş alan sorunu.
- %150: Aynı sorun, boşluk pencere büyüdükçe daha da göze batıyor. Bu ekranın en belirgin sorunu bu tekrar/boşluk.

## sonuc-basari

- %100, %125, %150: Üç ölçekte de temiz, okunaklı, sorun görmedim.

## sonuc-hata

- %100, %125, %150: Üç ölçekte de temiz, hata mesajı net okunuyor, sorun görmedim.

## sonuc-uyari

- %100, %125, %150: Üç ölçekte de temiz, sorun görmedim.

## uygulama-sec

- %100: Dosya yolları ("C:\Program Files\Microsoft VS ...\Code.exe" gibi) ortadan "..." ile kırpılmış — tam yol görülemiyor, ilk kullanıcı hangi programı seçtiğinden emin olamayabilir.
- %125: Aynı kırpma sorunu devam ediyor.
- %150: Aynı kırpma sorunu, pencere büyüse de yol genişlemiyor gibi — muhtemelen sabit oranlı bir sütun genişliği kullanılıyor.

## uygulama-sec-bos

- %100: Bu sefer üstte "Windows bu uygulamayı öneriyor" yazıyor ama listede vurgulanan satırda (Antigravity IDE) "Önerilen" rozeti yok — diğer ekranda (uygulama-sec) aynı vurgulu satırda rozet vardı. Metinle görsel işaret tutarsız, kafa karıştırıcı.
- %125: Aynı tutarsızlık sürüyor.
- %150: Aynı tutarsızlık, ayrıca yol kırpma sorunu burada da var.

## uzanti-ekle

- %100: Diyalogda sağ üstteki X dışında hiçbir onay/iptal düğmesi (Ekle/Vazgeç gibi) görünmüyor — ilk kullanıcı formu doldurduktan sonra nasıl kaydedeceğini bulamayabilir. Alttaki örnek metni ("Örnek: uzantı .rb, yorumlayıcı r...") sağdan kırpılıyor.
- %125: Aynı iki sorun devam ediyor (düğme yok, metin kırpık).
- %150: Aynı, örnek metni yine "...ru..." şeklinde kesik. Düğme eksikliği üç ölçekte de sabit.

## yedek-geri

- %100: Tablo başlık satırı (Tarih/Dosya/Boyut) beyaz arka planla geliyor, geri kalan her şey koyu neon temada — bu satır temaya uymayan bir native kontrol gibi duruyor, göze batıyor.
- %125: Aynı beyaz başlık şeridi sorunu sürüyor.
- %150: Aynı, pencere büyüdükçe beyaz şerit de büyüyüp daha dikkat çekici hale geliyor.

## Genel

1. **uzanti-ekle** diyaloğunda hiçbir Ekle/Kaydet/Vazgeç düğmesi yok — sadece X var; kullanıcı formu nasıl onaylayacağını bilemez (üç ölçekte de).
2. **sag-menu-bos** ekranında aynı "öğe bulunamadı" mesajı iki kez tekrarlanıyor ve altında amaçsız büyük bir boşluk kalıyor (üç ölçekte de).
3. **yedek-geri** tablosundaki beyaz başlık şeridi tema ile uyumsuz duruyor; ayrıca **uygulama-sec** ekranlarında dosya yolları "..." ile kırpılıyor ve **uygulama-sec-bos**'ta "Önerilen" rozeti ile metin tutarsız.
