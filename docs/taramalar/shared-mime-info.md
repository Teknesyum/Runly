# shared-mime-info

## 1. Künye

| Alan | Değer |
|---|---|
| Depo | `xdg/shared-mime-info` — gitlab.freedesktop.org (proje id 1205) |
| **Lisans** | **GPL-2.0** (`COPYING` = GNU GPL v2) |
| Yıldız | 13 (GitLab; GitHub aynaları sayılmıyor) |
| Son commit | 2026-07-27 "audio/vorbis: use IANA type as primary" |
| Son etiketli sürüm | **2.5.1** / 2026-06-29 (öncesi 2.4 / 2023-11-12) |
| Açık issue | 62 açık, 271 toplam |
| Kayıt | `data/freedesktop.org.xml.in`: **1040 mime-type**, 1438 glob, **1253 benzersiz `*.uzantı`**, 658 `<magic>`, 351 alias, 575 `sub-class-of`, 294 acronym, 501 generic-icon |
| Çeviri | `po/` = **78 dil**; `tr.po` 1019 msgid, 11 boş, 0 fuzzy → **%98,9 çevrili** |

## 2. Ne yapıyor

Linux/BSD masaüstlerinin ortak MIME veritabanı: her tür için görünen ad, uzantı deseni, içerik
imzası, tür hiyerarşisi ve ikon adı tek XML'de toplanır. `update-mime-database` bu XML'i ikili
indekse derler, tüm masaüstü uygulamaları o indeksten okur.

## 3. Runly ile kesişimi

Runly kataloğunun tam karşılığı, kesişimi en yüksek kaynak. Uzantı→tür eşlemesi (`<glob>`),
görünen ad (`<comment>`), çok dilli ad (`po/tr.po`), içerikten tespit (`<magic>`) ve kaba
kategori (üst düzey tür + `<generic-icon>`) hepsi var. Eksik olan tek şey Runly'nin ayırt edici
alanı: **tehlikeli tür işareti yok** — `application/x-ms-dos-executable` ile `image/png` veride
eşit vatandaş. Ölçek: 1253 uzantı / 13 üst düzey tür, Runly'de 408 / 14. (08'de tema ekseninde geçti.)

## 4. Alınacak fikir

1. **`sub-class-of` tür hiyerarşisi** — `application/x-shellscript` → `text/plain` gibi üst-tür
   bağı. Runly'de `.bat/.cmd/.ps1` risk notunu ayrı ayrı taşıyor; ortak "yürütülebilir betik"
   üst türü hem notu hem varsayılan uygulamayı tek yerden türetir. *Lisans: desen, veri değil —
   GPL kısıtı uygulanmaz.*
2. **Glob `weight` çakışma çözümü** — 1438 globun 69'unda açık ağırlık var; aynı uzantıya iki
   tür talip olduğunda kazananı ağırlık belirler, gizli "son yazan kazanır" yok. Katalog
   büyüdükçe Runly'ye deterministik kural gerekir. *Lisans: desen, kopyalanan satır yok.*
3. **Çeviriyi veriden ayırma (gettext)** — adlar XML'de tek dilde durur, çeviriler `po/*.po`'da
   ayrı yaşar, derlemede enjekte edilir. Runly `displayName:{tr,en}` ile çeviriyi veriye gömüyor;
   üçüncü dilde katalog satırı şişer. *Lisans: yapı kararı — serbest.*

**Veri alınamaz.** `freedesktop.org.xml.in` ve `po/tr.po` GPL-2.0; MIT bir ikiliye gömmek
Runly'yi GPL'e sürükler — Türkçe tür adları için o dosyaya bakılmaz.

## 5. Kaçınılacak hata

- **Lisans tuzağı.** "Sadece Türkçe adları alalım" en doğal refleks ve en pahalısı; kısmi kopya
  da türev eser, risk metin uzunluğuyla azalmıyor.
- **Sürüm kayması.** 2.4 (2023-11) ile 2.5 (2026-06) arası 2,5 yıl etiketsiz — sürüme sabitleyip
  unutan yıllarca eski veriyle kalır, ana daldan çekmek de belirsiz.
- **Ölçek yükü.** 1040 tür / 658 magic bloğu Runly'nin el yapımı 408 satırını boğar; fazlalık
  kayıt = bakım maliyeti + yanlış eşleme.

## 6. Doğrulama

Okunan: GitLab API künye/etiket/commit/issue istatistikleri, `COPYING` başlığı (GPL v2),
`data/freedesktop.org.xml.in` ve `po/tr.po` indirilip sayıldı — tüm kayıt sayıları bu iki
dosyadan hesaplandı. `doğrulanamadı`: GitLab yıldızı (13) gerçek kullanımı temsil etmiyor;
`data/` için GPL dışında ayrı izin metni arandı, bulunamadı (yokluğu kanıtlanmış değil).
