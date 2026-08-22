# mime-db

## 1. Künye

| Alan | Değer |
|---|---|
| Depo | `jshttp/mime-db` (GitHub) |
| **Lisans** | **MIT** — `LICENSE` = "(The MIT License)", Jonathan Ong 2014 / Douglas Christopher Wilson 2015-2022. Marka koruması ayrı değil, ek şart yok. |
| Yıldız | 1.248 |
| Son commit | 2026-06-21 |
| Son etiketli sürüm | **v1.54.0** / 2025-03-18 (≈17 ay etiketsiz) |
| Açık issue | 54 |
| Kayıt | `db.json`: **2601 MIME türü**, bunlardan **1025'inin uzantısı var**, **1246 benzersiz uzantı**. Kaynak dağılımı: iana 2215, apache 275, nginx 13, özel 98. `compressible` 846 kayıtta, `charset` 41 kayıtta. |

## 2. Ne yapıyor

Tek bir `db.json` içinde MIME türü → uzantı listesi, kaynak, sıkıştırılabilirlik ve karakter
kümesi tutar; hiç kod içermez, salt veridir. Veri IANA, Apache httpd ve nginx `mime.types`
dosyalarından betikle çekilip `src/custom-types.json` ile birleştirilir.

## 3. Runly ile kesişimi

Uzantı/MIME eşlemesinde **doğrudan kesişir ve lisansı uyumludur** — Runly'nin MIT ikilisine
atıfla gömülebilecek tek büyük veri kümesi bu. Kesişmediği yerler net: **görünen ad yok**
(ne İngilizce ne Türkçe), **kategori yok** (üst düzey MIME tipinden türetmek gerekir),
**tehlikeli tür işareti yok**, **içerikten tespit yok**. Runly'nin `displayName`, `category`,
`blocked`, `riskNote` alanlarının hiçbiri buradan gelemez; yalnız `extension` ekseni beslenir.
Kapsam: Runly 408 uzantı, mime-db 1246 — fark 3 kat, ama fazlalığın çoğu masaüstünde çift
tıklanmayan protokol/sunucu türü. (08'de tema ekseninde geçti.)

## 4. Alınacak fikir

1. **`source` alanı = köken izlenebilirliği.** Her kaydın hangi otoriteden geldiği (`iana`,
   `apache`, `nginx`, boş=özel) veride duruyor; Runly kataloğunda böyle bir alan yok, "bu satır
   nereden geldi" sorusu cevapsız. *Lisans: MIT — desen de veri de alınır, atıf yeterli.*
2. **Kapsam boşluğu denetimi.** mime-db'nin 1246 uzantısı Runly'nin 408'iyle karşılaştırılıp
   eksik ama yaygın uzantılar (özellikle `apache` kaynaklı olanlar) tespit edilebilir; el yapımı
   kataloğa hangi 30-50 satırın ekleneceğine veriyle karar verilir. *Lisans: MIT, uzantı listesi
   doğrudan alınabilir; görünen ad ve risk notu yine el yazımı kalır.*
3. **"Semver veriyi kapsamaz" itirafı.** README açıkça yazıyor: semver yalnız API'yi korur,
   MIME çözümlemesi minor sürümde değişebilir. Runly de kataloğu uygulama sürümünden ayrı
   sürümlemeli. *Lisans: desen — serbest.*

## 5. Kaçınılacak hata

- **Veri kalitesi tek yönlü.** Katkı yolu "önce upstream'e (IANA) kaydet" diyor; yanlış bir
  uzantı eşlemesi burada hızlı düzeltilemez. Runly kataloğuna alınan satır körlemesine
  güvenilmez, en azından yaygın uzantılarda gözle geçilmeli.
- **Sürüm kayması.** Son etiket 2025-03, son commit 2026-06 — ana dal ile yayımlanan sürüm
  arasında ≈17 ay fark var. README'nin kendisi jsDelivr'de `@master` yerine etiket kullanmayı
  öneriyor; ana daldan çekmek sessiz veri kaymasıdır.
- **Alt kaynakların lisansı.** MIT olan derlemenin kendisi; IANA kayıt verisi ve Apache
  `mime.types` ayrı şartlara tabi olabilir — "MIT" damgası yalnız bu depo için geçerli.

## 6. Doğrulama

Okunan: GitHub API künyesi (lisans, yıldız, issue, push), `releases/latest`, `LICENSE` ilk
satırları, `README.md`, `src/` listesi. `db.json` indirilip sayıldı — 2601/1025/1246 ve kaynak
dağılımı bu dosyadan hesaplandı. `doğrulanamadı`: npm indirme sayıları ve "en yaygın MIME
veritabanı" iddiası ölçülmedi; alt kaynakların (IANA, Apache) lisans şartları incelenmedi.
