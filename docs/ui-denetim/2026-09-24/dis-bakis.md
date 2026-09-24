# Dış Bakış Ham Yanıtları

## Tur 1 — Eski Yakalama (Görüntü İlk Boyamadan Önce Alınmıştı; Görüntüler Yenilendi)

Alt ajan: sonnet, işi yapmamış, yalnız 8 görüntü yolu verildi. Yanıt olduğu gibi:

> ## Okunabilirlik İncelemesi (2026-09-24, %100 ölçek)
>
> **1. Sonra görüntülerinde okunması zor yazı/simge**
>
> - `ana-100-sonra.png` — Davranış panelinde, "Günlük tut" / "Betikleri yönetici olarak çalıştır" satırının altında 3 adet tamamen siyah, etiketsiz kutu var (önceki sürümde burada "Günlük klasörünü aç" butonu vardı). İçerik yok, zemin de pencere zeminiyle aynı siyah — buton mu boşluk mu belli değil.
> - `kaldir-onay-100-sonra.png` — alt kısımda iki siyah placeholder kutu (muhtemelen "Kaldır" / "İptal" butonları) metinsiz duruyor; aynı sebep, zeminle aynı siyah.
> - `sonuc-hata-100-sonra.png` — "İşlem başarısız oldu." başlığı parlak pembe/magenta üstte iyi okunuyor, ama alt yazı kutusundaki "Hata: Kayıt defterine yazılamadı." metni düşük kontrastlı gri-yeşil tonda, ince fontla — küçük ve soluk, dikkat gerektiriyor.
> - `mesaj-100-sonra.png` — kendi içinde okunur (bkz. madde 3), sorun yok.
>
> **2. Aynı renk ailesi üstünde aynı aile (mavi/mor/pembe/yeşil çakışması)**
>
> - `ana-100-sonra.png` (ve önce de aynı) — ".cjs" satırındaki "Bağlı" durumu: koyu teal/yeşil dolgu üstünde açık yeşil-mint yazı. Aynı yeşil-camgöbeği ailesi içinde iki ton — emin değilim, tam kontrast ölçmeden kesin diyemem ama riskli.
> - Sol menüde seçili "Betikler" satırı: koyu camgöbeği dolgu üstünde beyaz yazı — bu sorunsuz, farklı aile.
> - Diğer görüntülerde belirgin bir "aynı aile üstünde aynı aile" çakışması görmedim.
>
> **3. Önce/sonra göze çarpan fark**
>
> - `ana`: Tek fark, Davranış panelindeki isimli buton yerine 3 boş siyah kutunun gelmesi (madde 1) — okunurluğu ve işlev algısını kötüleştirmiş, iyileştirmemiş.
> - `uygulama-sec`: İki görüntü arasında gözle fark bulamadım — emin değilim, piksel bazında incelemedim.
> - `mesaj`: Büyük fark — "önce" pencere tamamen boş/siyah (başlık dışında hiçbir içerik render olmamış), "sonra" pencerede "Dikkat" başlığı, açıklama metni ve Evet/Hayır butonları tam görünür durumda. Bu net bir okunurluk kazanımı.
>
> **4. Buton/çip/satır seçimi ayırt edilebilirliği**
>
> - `mesaj-100-sonra.png`: "Evet" camgöbeği çerçeveli+dolgulu, "Hayır" mor çerçeveli — ikisi de buton gibi duruyor, birbirinden ayrılıyor.
> - `kaldir-onay-100-sonra.png`: Üstteki checkbox ("Yedeği de geri yükle") normal görünüyor; ama alttaki iki siyah kutu buton olarak hiç ayırt edilemiyor — zeminle birleşmiş.
> - `sonuc-hata-100-sonra.png`: "Kapat" butonu camgöbeği çerçeveli, metin okunur, buton olarak net.
> - `ana-100-sonra.png`: Tablo satırları, checkbox'lar, "Çalıştır" etiketleri önceki haliyle aynı ve ayırt edilebilir durumda; yalnızca yukarıda bahsedilen 3 boş kutu istisna.
> - `uygulama-sec` penceresinde "Önerilen" etiketi (beyaz yazı, magenta dolgu) net bir çip gibi görünüyor, karışmıyor.
>
> **Genel not:** En belirgin sorun `ana-100-sonra` ve `kaldir-onay-100-sonra`'daki etiketsiz siyah kutular — bunlar önceki sürümde metinli/renkli butonken şimdi boş görünüyor, kullanıcı gözüyle bakılınca "bozuk" veya "yüklenmemiş" izlenimi veriyor.

Bu turdan çıkan iş: siyah kutular ve boş "mesaj önce" görüntüsü yakalama zamanlamasından geliyordu (görüntü ölçümden önce, ilk boyama bitmeden alınıyordu). Harness görüntüyü artık ölçümler bittikten sonra alıyor; görüntüler yeniden üretildi ve Tur 2'ye verildi. `kaldir-onay` alt düğme kırpılması ise gerçek bir yerleşim sorunu çıktı (açık bulgu).

## Tur 2 — Yenilenmiş Görüntüler

Alt ajan: sonnet, işi yapmamış, önceki turu görmemiş; yalnız 9 görüntü yolu verildi. Yanıt olduğu gibi:

> ## 1. Zor okunan yazı/simge (sonra görüntüleri)
>
> - **kaldir-onay-100-sonra**: Alt kısımdaki düğmeler (teal ve mor) tamamen kırpılmış, sadece renkli bir şerit görünüyor — üstlerindeki yazı hiç okunmuyor.
> - **uygulama-sec-100-sonra**: "Önerilen" rozeti — mor zemin üstünde pembe/magenta yazı, ikisi de aynı sıcak-mor aile içinde; okunuyor ama kontrastı diğer düğmelere göre daha zayıf.
> - **ana-100-sonra**: Sağ üstte "Varsayılan yap" turuncu şerit kesik görünüyor, yazının tamamı seçilemiyor (tablo dışına taşmış/kırpılmış olabilir).
> - Diğerlerinde (mesaj, yedek-geri, sonuc-hata) belirgin bir okunurluk sorunu görmedim.
>
> ## 2. Aynı renk ailesi üstünde aynı renk ailesi (mavi/mor/pembe/yeşil çakışması)
>
> - **uygulama-sec-100-sonra**: "Önerilen" rozeti — mor zemin + pembe/magenta yazı, aynı aile (mor-pembe) üst üste. Net bir çakışma örneği bu.
> - Mavi üstünde mavi ya da yeşil üstünde yeşil bir yer görmedim.
> - Emin değilim: ana ekrandaki turkuaz linkler (TR|en, Teknesyum) ile turkuaz çerçeveler arasında ailesel yakınlık var ama üst üste durmuyorlar, sorun değil.
>
> ## 3. Önce/sonra farkı ve okunurluğa etkisi
>
> - Elimdeki 4 çift ("önce" etiketli olanlar: ana, uygulama-sec, mesaj) piksel piksel aynı görünüyor — gözle fark edemedim, emin değilim gerçekten bir değişiklik var mı.
> - "sonra"-yalnız üç görüntü (kaldir-onay, yedek-geri, sonuc-hata) için karşılaştıracak "önce" yok, tek başına değerlendirdim.
>
> ## 4. Etkileşimli öğeler ayırt edilebiliyor mu
>
> - **ana-100-sonra**: Düğmeler (Ekle/Çıkar, Kur/Kaldır vb.) mor çerçeveyle net ayrışıyor, checkbox'lar okunaklı; tablo satır seçimi (koyu turkuaz "Bağlı" satırı) belirgin.
> - **uygulama-sec-100-sonra**: Seçili satır (Visual Studio Code) teal arka planla net; "Gözat/Vazgeç/Seç" düğmeleri birbirinden ayrışıyor.
> - **mesaj-100-sonra**: "Evet/Hayır" düğmeleri net, dolgu-kontur farkı belirgin.
> - **yedek-geri-100-sonra**: Tablo satırları düz, hangi satırın seçili/tıklanabilir olduğuna dair bir vurgu (hover/seçim rengi) görünmüyor — sadece liste gibi duruyor, etkileşimli olduğu belli değil.
> - **kaldir-onay-100-sonra**: Düğmeler kırpıldığı için ayırt edilemiyor (bkz. madde 1 ve 5).
> - **sonuc-hata-100-sonra**: "Kapat" düğmesi net, ayırt edilebiliyor.
>
> ## 5. Kırpılmış/taşmış/yarım öğeler
>
> - **kaldir-onay-100-sonra**: En belirgin sorun — alt düğme satırı pencere dışına taşmış/kırpılmış, "Kaldır"/"Vazgeç" gibi metinler hiç görünmüyor, yalnızca renk şeridi kalmış.
> - **kaldir-onay-100-sonra**: Metin de yarım gibi duruyor — "...emin misiniz Bu, Runly'nin..." arasında soru işareti/nokta eksik, cümle kesik hissi veriyor (emin değilim, bilinçli tasarım mı yoksa metin hatası mı).
> - **ana-100-sonra**: "Varsayılan yap" turuncu şerit sağda kesik, tablo/dış çerçeve sınırına takılmış gibi.
> - Diğer görüntülerde (uygulama-sec, mesaj, yedek-geri, sonuc-hata) taşma/kırpılma görmedim.
