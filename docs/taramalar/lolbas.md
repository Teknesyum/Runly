# LOLBAS-Project/LOLBAS

## 1. Künye
`LOLBAS-Project/LOLBAS` · **Lisans: GPL-3.0** (API `license.spdx_id`; ayrı marka koruma
metni görülmedi — *doğrulanamadı*) · 8.766 yıldız · 27 açık issue · son push 2026-08-21 ·
**etiketli sürüm yok** (`releases/latest` → HTTP 404). Girdi sayısını dizin listesinden
kendim saydım: OSBinaries 135, OtherMSBinaries 79, OSLibraries 17, OSScripts 10,
HonorableMentions 3 → **244**. README rozeti üçüncü taraf SVG'dir, kullanılmadı.

## 2. Ne yapıyor
Microsoft imzalı, işletim sistemiyle gelen ikili/script/kitaplıkların "beklenmedik"
yeteneklerini YAML olarak belgeler: kod çalıştırma, indirme, ADS'te saklama, beyaz liste
atlatma. Kabul ölçütü net — dosya Microsoft imzalı olmalı ve **amaçlanmamış** bir
yeteneği bulunmalı; amaçlanan kullanımlar belgelenmez.

## 3. Runly ile kesişimi
Bu depo, Runly'nin risk notlarının **denetleyicisi**. Notlar yanlış değil, ama tehdidin
yanlış yerini gösteriyor.
**Uzantı, tehlikenin taşıyıcısı değil.** `Wscript.exe` girdisindeki iki komutun ikisi de
ADS kategorisinde; `Cscript.exe` girdisindeki tek komut da öyle. `wscript //e:vbscript
<yol>:script.vbs` biçiminde `//e:` anahtarı motoru **zorla** seçer; dosyanın adı ve
uzantısı tamamen ilgisizdir. `Rundll32.exe` girdisi açıkça yazıyor: "ilk parça bir DLL
dosyası olmalı (**her uzantı kabul edilir**)". Tehlike uzantıda değil, çağrılan
barındırıcıdadır. Runly uzantı ekseninde çalıştığı için bu sınır kabul edilebilir, ama
**notun dili** "bu uzantı tehlikeli" değil, "bu uzantı şu barındırıcıya gider ve o
barındırıcı tam yetkiyle kod çalıştırır" demeli — mevcut notlar büyük ölçüde böyle.
**MOTW'nin güvenilmez tarafı.** `Zone.Identifier`'ın kendisi bir ADS'tir; `wscript`,
`cscript`, `rundll32` ve (eski Windows'ta) `mshta` girdilerinin tamamı **başka bir ADS'ten
kod çalıştırmayı** belgeliyor. Yük `belge.txt:script.vbs` içindeyse ne uzantı denetimi ne
MOTW ne de desen taraması onu görür — Runly `belge.txt`'i tarar, akışı değil. Güvenilen
klasördeki zararsız görünümlü dosyanın ADS'i kapıdan hiç geçmeden çalışır: Runly'nin
hatası değil, kapsamının dışı — ama SPEC'te yazılı olmalı. **Denetim izi:** kesişmiyor.

## 4. Alınacak fikir
1. **Katalogdaki uzantı boşluklarını kapat.** `catalog.json`'da (408 girdi) `.vbe`,
   `.jse`, `.wsc`, `.sct`, `.chm` **hiç yok**; `.wsh` var ama `riskNote`'suz. `.vbe`/`.jse`
   aynı `wscript` tarafından aynı yetkiyle çalışır ve kodlanmış oldukları için Runly'nin
   desen taraması onlarda **hiç iş görmez**. Maliyet: düşük, sadece katalog + metin.
2. **Risk notunda barındırıcıyı adıyla söyle** — `mshta.exe`, `wscript.exe`,
   `regsvr32 + scrobj.dll`. Neden: kullanıcının kendi doğrulayabileceği tek somut ip ucu.
   Maliyet: düşük, `locale/*.json` revizyonu.
3. **"Runly'nin görmediği şeyler" başlığı.** ADS'te saklanan yük, `//e:` ile zorlanan
   motor, `mshta vbscript:...` komut satırı çağrısı — SPEC'te kapsam dışı ilan et.
   Neden: kapının abartılı güvence gibi sunulmasını engeller. Maliyet: düşük.

## 5. Kaçınılacak hata
- **`.hta` notu bir noktada eksik.** `Mshta.exe` girdisi `.hta` açmanın yanında
  `mshta.exe vbscript:Close(Execute(...))`, `mshta.exe javascript:...` ve
  `mshta.exe {REMOTEURL}` komutlarını da belgeliyor: gerçek yol çoğu zaman diskteki `.hta`
  değil, `mshta`'yı komut satırıyla çağıran kısayoldur. Runly `.lnk`/`.url`'ü `blocked`
  işaretlemiş ama gerekçesi "Windows güvenliği ve sistem bütünlüğü nedeniyle yönetilemez"
  — bu bir *yetenek* açıklaması, *risk* açıklaması değil.
- **Ölü teknik.** ADS varyantının işletim sistemi alanı "Windows 10 (**1903 ve sonrasında
  çalışmaz**)" diyor; Runly Windows 11 hedefliyor, geçersiz teknik uyarıya girmemeli.
- **Sürümsüz bağımlılık.** Etiketli sürüm yok, içerik sürekli değişiyor; alınacak şey bir
  kerelik okuma sonucudur. GPL-3.0 olduğu için YAML'i gömmek lisans yükü getirir.

## 6. Doğrulama
Okudum: `gh api repos/LOLBAS-Project/LOLBAS`, `releases/latest` (404), README, dizin
sayımları ve `yml/OSBinaries/` altındaki `Mshta.yml`, `Wscript.yml`, `Cscript.yml`,
`Rundll32.yml`, `Regsvr32.yml` ham içerikleri. Runly tarafını `catalog.json` üzerinden
saydım: 408 uzantı girdisi, 22 `riskNote`, 16 `blocked`. **Doğrulanamadı:** `.vbe`/`.jse`
bu makinede sınanmadı; dayanak `//e:` belgesi ve WSH davranışı — kataloğa eklemeden önce
elle sınanmalı. LOLBAS'ta bu ikisi için **ayrı girdi yok**, sonuç çıkarımdır.
