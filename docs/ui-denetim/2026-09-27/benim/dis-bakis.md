# Dış Bakış — Benim Düzeni

Alt ajan (sonnet), işi yapmamış; yalnız görüntü klasörü verildi, kural verilmedi. Ham yanıt aşağıda, değiştirilmedi.

## Gönderilen

> Bir masaüstü uygulamasının ekran görüntülerine kullanıcı gözüyle bak. Kural listesi yok; yalnız gördüğüne dayan. Klasör: docs\ui-denetim\2026-09-27\benim. Bak: `*-100-olcum.png` (18 dosya), `kurulum\*.png` (4 dosya), `yan-yana.png` (solda tasarım önizlemesi, sağda uygulama). Her görüntü için: okunmayan yazı, kırpılan/taşan öğe, çakışma, işlevi belirsiz düğme, tutarsız renk ya da boyut, çevrilmemiş/ham anahtar gibi görünen metin (ör. `app.title`), boş/hata durumunda ne yapılacağının belirsizliği. yan-yana.png için: uygulama önizlemedeki görsel dile uyuyor mu. Çıktı: dosya adı başlıklı kısa maddeler; sonda en önemli 5 sorun.

## Gelen

**ana-100-olcum.png**
- Tablonun son satırı (waf/vbs vb.) alt kenarda yarım kesiliyor, kaydırma çubuğu üstüne biniyor.
- "Bulundu" ve "Argümanlar" sütunlarındaki yollar "C:\Program..." şeklinde kırpılıyor; tam yolu görecek bir tooltip/genişletme yok.

**ana-guncelleme-100-olcum.png**
- ana-100 ile aynı; üstte turuncu "Güncelleme" noktası eklenmiş, başka sorun yok.

**guncelleme-hata-100-olcum.png**
- Sorun yok; hata metni okunur, üç seçenek (İndir ve yükle / İndir / Kapat) net.

**guncelleme-hazir-100-olcum.png**
- Sorun yok.

**guncelleme-iniyor-100-olcum.png**
- İndirme sürerken hem "İptal" hem "Kapat" birlikte duruyor; "Kapat" indirmeyi arka plana mı alıyor yoksa iptal mi ediyor belirsiz.

**guncelleme-var-100-olcum.png**
- Sorun yok.

**guncelleme-yenileniyor-100-olcum.png**
- Sorun yok; buton olmaması bu aşamada mantıklı.

**kaldir-onay-100-olcum.png**
- Sorun yok, metin net.

**mesaj-100-olcum.png**
- Sorun yok.

**sag-menu-100-olcum.png**
- Liste kaydırılabilir, son öğe ("DriveFS ContextMenu Handler") kaydırma alanına yakın kesiliyor gibi görünüyor ama işlevsel; kritik değil.

**sag-menu-bos-100-olcum.png**
- Sorun yok, boş durum mesajı net.

**sonuc-basari-100-olcum.png / sonuc-hata-100-olcum.png / sonuc-uyari-100-olcum.png**
- Sorun yok.

**uygulama-sec-100-olcum.png / uygulama-sec-bos-100-olcum.png**
- İki ekranda önerilen uygulama sıralaması farklı (birinde Visual Studio Code ilk sırada, diğerinde Antigravity IDE ilk sırada) — sıralama tutarsız görünüyor, "bos" adının neyi ifade ettiği (arama kutusu boş mu, seçim mi yok) görsellerden anlaşılmıyor.

**uzanti-ekle-100-olcum.png**
- Sorun yok.

**yedek-geri-100-olcum.png**
- Sorun yok.

**kurulum\bos-3000.png / calisiyor-2500.png / calisiyor-9000.png**
- Sorun yok; adımlar ve yüzde göstergesi net. (calisiyor-2500 dosya adı "çalışıyor"yu çağrıştırsa da içerik %92 "tamamlandı" gösteriyor — sadece dosya adlandırma/zamanlama uyumsuzluğu, arayüz hatası değil.)

**kurulum\hata-6000.png**
- Hata metni hem durum satırında hem log satırında kesiliyor: "Cannot find drive. A drive with the nam..." — kullanıcı tam hata mesajını göremiyor, kopyalama/genişletme imkânı yok.

**yan-yana.png**
- Renk paleti (siyah zemin, camgöbeği başlıklar, macenta/pembe vurgular), tipografi ve genel koyu tema uygulamada tasarım önizlemesiyle tutarlı.
- Tasarım tarafındaki kart/buton bileşenleri (renk kademeleri, "Geri Al" vb.) ile sağdaki tablo/liste yoğunluğu farklı bir bileşen dili kullanıyor (kart vs. yoğun tablo) ama bu beklenen bir fark; renk ve çizgi kalınlığı tutarlı görünüyor.
- Uygulama tarafında bazı tablo hücreleri ("..." ile kesilen yollar) tasarım tarafındaki net/okunur etiket yaklaşımına göre daha az bilgilendirici.

**En önemli 5 sorun**
1. `kurulum\hata-6000.png`: Hata mesajı kesiliyor, kullanıcı tam metni göremiyor/kopyalayamıyor.
2. `ana-100-olcum.png`: Tablonun son satırı kaydırma çubuğuyla çakışıp yarım görünüyor.
3. `ana-100-olcum.png` / `ana-guncelleme-100-olcum.png`: "Bulundu"/"Argümanlar" sütunlarında yol kırpılıyor, tam bilgiye erişim yok.
4. `guncelleme-iniyor-100-olcum.png`: İndirme sırasında "İptal" ve "Kapat" birlikte var, işlev farkı belirsiz.
5. `uygulama-sec-100-olcum.png` vs `uygulama-sec-bos-100-olcum.png`: Önerilen uygulama sıralaması iki ekran arasında tutarsız.
