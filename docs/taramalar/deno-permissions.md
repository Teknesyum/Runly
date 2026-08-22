# denoland/deno — izin modeli

## 1. Künye
`denoland/deno` · **Lisans: MIT** · 108.277 yıldız · 1.529 açık issue (tüm depo) ·
son push 2026-08-22 · son etiketli sürüm `v2.9.5` (2026-08-06).

## 2. Ne yapıyor
Script'i varsayılan olarak izinsiz çalıştırır; dosya, ağ, ortam değişkeni, alt süreç, FFI
ve sistem bilgisi ayrı ayrı izin ister. İzin ya önden `--allow-*` / `--deny-*` ile verilir
ya da çalışma anında terminalde sorulur.

## 3. Runly ile kesişimi
**Güven kapsamı — asıl fark.** Runly güveni *dosyaya/klasöre* verir: geçtiysen her şeyi
yaparsın. Deno güveni *yeteneğe* verir ve kapsamlandırır (`--allow-read=./data`,
`--allow-env=API_KEY`). Runly kendi yorumlayıcısını yazmıyor — `wscript`, `python`, `node`
çağırıyor — bu yüzden yetenek kapsamlandırması Runly'de **teknik olarak mümkün değil**.
Alınacak olan mekanizma değil, dürüstlük: kullanıcıya "şunu yapma izni verme"nin mümkün
olmadığını açıkça söylemek.

**Symlink ile atlatma.** #33894 (2026-05-07, **hâlâ açık**): "deny kurallarını symlink
çözümlü yola karşı yeniden denetle" — yani deny listesi symlink ile atlatılabiliyor.
#35031 (açık) symlink kapsamlandırması istiyor. Runly bu sınıf hatayı `TrustMatching`'de
reparse-point çözümüyle kapatmış; bu bir doğrulama, kopyalanacak fikir değil.

**Yorgunluk.** Soru `[y/n/A]` — üçüncü seçenek "bu türün **tümüne** izin ver", yani
Runly'nin "ilk seferde sor sonra güven" modunun aynısı. #12763 (kapalı, 125 tepki)
izinlerin dosyaya yazılmasını istemişti; talebin büyüklüğü, çalışma anı sorusunun tek
başına yaşanamaz olduğunu gösteriyor.

**Diyalog dili.** Belge iki izni ayrıca "sandbox'tan kaçış" diye etiketliyor:
`--allow-run` (alt süreç ana sürecin tam yetkisini alır; `--allow-run=deno` özellikle
tehlikeli, çünkü yeni süreç `--allow-all` ile başlatılabilir) ve `--allow-ffi`.

**MOTW / denetim izi.** Deno'da karşılığı yok. Kesişmiyor.

## 4. Alınacak fikir
1. **"Bu onay diğerlerinden farklı" işareti** — `.hta`/`.vbs`/`.wsf` gibi ana
   yorumlayıcıyı doğrudan kullanıcı yetkisiyle çalıştıran türler diyalogda ayrı bir sınıf
   gibi görünsün, aynı gövde metniyle değil. Maliyet: düşük, diyalog varyantı + metin.
2. **Kapsamı klasör + uzantı çiftine bağla** — `C:\Projeler` altındaki `.py` güvenilir,
   `.hta` değil. Deno'nun kapsamlandırmasının Runly'deki tek gerçekçi karşılığı.
   Maliyet: orta, `trust.json` şeması + `TrustMatching` dalı.
3. **Deny listesi, allow'un üstünde** — Deno 2 `--deny-*`'ı `--allow-*`'ı ezecek şekilde
   kurmuş. Runly'de `Downloads` gibi kalıcı ret, güven önek kuralını ezen katman olmalı.
   Maliyet: orta.

## 5. Kaçınılacak hata
- **#33894 (açık): allow tarafı normalize ediliyor, deny tarafı edilmiyor.** Klasik hata.
  Runly deny listesi eklerse reparse-point çözümü **iki tarafta da** çalışmalı.
- İzinlerin hiçbir yerde kalıcı olmaması (#12763) kullanıcıyı `--allow-all`'a itiyor.
  "Hiç sorma" modu bir başarısızlık değil, tasarımın kaçınılmaz sonucu — Runly bu modun
  varsayılan hâline geleceğini kabul edip onu güvenli tasarlamalı.

## 6. Doğrulama
Okudum: `gh api repos/denoland/deno` + `releases/latest`; issue #33894, #35031, #12763;
resmî belge `docs.deno.com/runtime/fundamentals/security/` (bayraklar, prompt metni,
`--allow-run`/`--allow-ffi` uyarıları). `runtime/permissions/` dosya listesini gördüm
(`broker.rs`, `prompter.rs`, `runtime_descriptor_parser.rs`) ama README boş döndü —
iç mimari **doğrulanamadı**. İzin kontrolünün symlink davranışının bugünkü kesin hâli
**doğrulanamadı**; kanıt yalnızca açık issue'nun varlığı.
