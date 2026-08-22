# git-for-windows/git — safe.directory

## 1. Künye
`git-for-windows/git` · **Lisans: GPL-2.0** (`COPYING` = "GNU GENERAL PUBLIC LICENSE
Version 2", bazı parçalar LGPL-2.1; GitHub API `license.spdx_id` = `NOASSERTION`, yani
API tek lisansa eşleyemedi — dosya birincil kaynaktır) · 9.364 yıldız · 231 açık issue ·
son push 2026-08-21 · son sürüm `v2.55.0.windows.5` (2026-08-20).

## 2. Ne yapıyor
Git, CVE-2022-24765'ten sonra sahibi mevcut kullanıcı olmayan bir deponun config'ini
**okumayı bile** reddeder. `safe.directory`, bu sahiplik denetimine kullanıcının elle
istisna eklediği çok değerli bir ayardır.

## 3. Runly ile kesişimi
**Güven kapsamı — en yakın akraba.** `trust.json` ile aynı şey: yol tabanlı, çok değerli,
kullanıcıya ait allow-list. İki karar doğrudan alınabilir.
*Birinci:* `safe.directory` **yalnızca korumalı yapılandırmada** (system + global) okunur,
depo-yerel `.git/config`'te yazılamaz. Gerekçe belgede açık: aksi hâlde saldırgan, içinde
`safe.directory` bulunan bir depo göndererek kendini güvenilir ilan ederdi. Runly
karşılığı: `trust.json` **asla** açılan dosyanın yanındaki bir dosyadan beslenmemeli.
Bugün öyle — bu kural yazıya geçmeli.
*İkinci:* `safe.bareRepository` varsayılanı `all`'dan `explicit`'e Git 3.0'da geçecek ve
bu **önceden ilan edilmiş**. Runly'nin "hiç sorma" modu için aynı yol: varsayılanı
daraltmak, sessizce değil sürüm sınırında ilan ederek.

**Yol eşleştirme.** `~/<yol>` ev dizinine, `%(prefix)/<yol>` çalışma önekine genişler.
Sonuna `/*` eklenen dizin altındaki tüm depoları kapsar — yani **öneki miras alma açıkça
işaretlenir**, örtük değil. Runly'de klasör güveni bugün örtük olarak alt ağacı kapsıyor.
**Kaçış deliği.** `safe.directory = *` denetimi tümüyle kapatır; belge, sistem config'inde
`*` varsa korumayı geri açmak için listeyi **boş değerle sıfırlamayı** öneriyor.
**Symlink/junction.** Belge symlink çözümünden söz etmiyor (*doğrulanamadı*).
**MOTW / denetim izi / diyalog.** Karşılığı yok; `safe.directory` soru sormaz, hata verip
kopyalanacak komutu ekrana yazar. Kesişmiyor.

## 4. Alınacak fikir
1. **Güven kaydının kaynağı korumalı olsun ve belgelensin** — "Runly güven listesini
   yalnızca `%APPDATA%\Runly\trust.json`'dan okur; açılan dosyanın yanındaki hiçbir dosya
   güven üretemez." Maliyet: düşük, SPEC'e bir cümle + bir test vakası.
2. **Miras açıkça işaretlensin** — `C:\Projeler` yalnız o klasör, `C:\Projeler\*` alt ağaç.
   Neden: kullanıcı kapsamı seçebilsin. Maliyet: orta, şemada bir alan + bir dal.
3. **"Hepsini güven"den geri dönüş yolu arayüzde olsun** — Git'te bu ancak belgeyi
   okuyarak bulunuyor. Maliyet: düşük.

## 5. Kaçınılacak hata
- **#3798 (kapalı, 2022-04-14): "Windows User Groups not counted for ownership rules."**
  Administrators grubunun sahip olduğu klasör, yönetici için bile "başkasının" sayılıyordu.
  Windows'ta "sahip" POSIX uid değildir — yol sahipliğine dayanan kuralın ilk tuzağı budur.
- **#3786 (kapalı), #4817 ve #6181 (açık):** UNC/ağ konumları `safe.directory`'ye
  eklenemiyor, `%(prefix)/` UNC'de belirsiz. Yol tabanlı allow-list'in ikinci kırılma
  noktası hep ağ yolları. **#6359 (açık):** v2.55'te sıkılaştırılan doğrulamalar UNC'de
  "ya çok sıkı ya hiç denetlemiyor" hâline geldi — sürüm içi sıkılaştırma uyarısız kırar.
- **#3809 (kapalı): `safe.directory = *` çalışmıyor.** Kaçış deliği bile bozulabiliyor;
  kullanıcı orada tıkanır, gidecek başka yeri yoktur.

## 6. Doğrulama
Okudum: `gh api repos/git-for-windows/git` + `releases/latest`; depodaki
`Documentation/config/safe.adoc` tam metni (kapsam kısıtı, `*` semantiği, `~`/`%(prefix)`
genişletmesi, `/*` soneki, `safe.bareRepository` planı); `COPYING` başlığı; issue #3798,
#3786, #3809, #4817, #6181, #6359. Okuyamadım: sahiplik denetiminin Windows uygulaması —
symlink/junction çözümü **doğrulanamadı**. CVE-2022-24765 ayrıntısı ikincildir, yalnız
gerekçe olarak anıldı, içeriği depodan **doğrulanmadı**.
