# filetype.py — içerik tabanlı tür tespiti

## 1. Künye

| Alan | Değer |
|---|---|
| Depo | `h2non/filetype.py` (GitHub), PyPI paketi `filetype`. Go'daki `h2non/filetype` paketinin Python portu. |
| **Lisans** | **MIT** (GitHub API `spdx_id: MIT`). Marka koruması ayrı değil, ek şart yok. |
| Yıldız | 770 |
| Son commit | **2025-05-02** (≈16 aydır dokunulmamış) |
| Son etiketli sürüm | **v1.2.0** / 2022-11-08 — **3 yıl 9 aydır yeni sürüm yok** |
| Açık issue | **65** |
| Kayıt | `filetype/types/__init__.py`: **82 tür**. Dosya bazında: archive 17 KB, image 10,7 KB, document 7,9 KB, video 5,6 KB, audio 5,3 KB, font 2,9 KB. **Sıfır bağımlılık**, saf Python. |

## 2. Ne yapıyor

Bir dosyanın ya da bayt tamponunun türünü başındaki imza baytlarından çıkarır, uzantı + MIME
döndürür. README'ye göre **yalnız ilk 261 bayt** yeterlidir; libmagic bağlaması veya C uzantısı
yok, tamamı Python.

## 3. Runly ile kesişimi

Tek kesişim **içerikten tür tespiti**. Runly bugün yalnız uzantıya bakıyor, "uzantı `.jpg` ama
içerik PE" durumunu göremiyor. Ötesinde kesişim yok: görünen ad, çeviri, tehlikeli tür işareti
yok; kategori yalnız kaba `kind` (image/video/audio/archive/document/font); uzantı→tür tablosu
olarak kullanılamaz (82 tür, hepsi ikili format).

Ayrıca .NET'e doğrudan gelmez. `file-type` (Node) ile aynı problemi çözüyor, fark ölçekte:
82'ye karşı 183 tür. (08'de tema ekseninde geçti.)

## 4. Alınacak fikir

1. **Sabit 261 baytlık başlık penceresi.** Okunacak bayt sayısı baştan sabit; bir klasördeki
   yüzlerce dosya taranırken bu üst sınır performansı öngörülebilir kılar. *Lisans: MIT —
   desen de imza tablosu da atıfla alınabilir.*
2. **`kind` ekseni: tür değil, tür ailesi.** Hem tam türü hem ailesini döndürüyor; çağıran
   "tam olarak ne" değil "hangi aile" diye sorabiliyor. Runly'nin 14 kategorisi zaten bu eksen,
   içerik tespiti eklenirse dönüş doğrudan kategoriye bağlanır. *Lisans: MIT — serbest.*
3. **Sıfır bağımlılık kararı.** README bunu ilk özellik sayıyor: C uzantısı yok, libmagic yok.
   Runly de içerik tespitini küçük bir iç imza tablosuyla çözmeli, native kütüphane bağlamamalı.
   *Lisans: karar, veri değil — serbest.*

## 5. Kaçınılacak hata

- **Yarı terk edilmiş.** Son sürüm 2022-11, son commit 2025-05, 65 açık issue — bağımlılık
  kurulacak proje değil. `file-type`'ta 0 açık issue ve commit günü yayımlanan sürüm var;
  aynı problem, farklı bakım disiplini.
- **Kapsam dar ve sessizce dar.** 82 tür, `file-type`'ın yarısından az; metin tabanlı türler
  (betikler, kaynak kod, yapılandırma) hiç yok. Runly'nin risk tarafındaki `.bat/.ps1/.js`
  bu yaklaşımla **hiç** tespit edilemez — içerik tespiti tehlikeli tür korumasının yerini almaz.
- **İmza tespiti güvenlik kontrolü değil.** Bir dosyanın başına PNG imzası koymak bedava;
  "tür doğrulandı" yanlış, en fazla "uzantı ile içerik uyuşuyor" denir.

## 6. Doğrulama

Okunan: GitHub API künyesi (MIT, 770 yıldız, 65 açık issue, son push 2025-05-02),
`releases/latest` (v1.2.0 / 2022-11-08), `filetype/types/` listesi ve dosya boyutları,
`filetype/types/__init__.py` indirilip sayıldı (82 tür), `README.rst` özellik listesi.
`doğrulanamadı`: README'nin "büyük dosyalarda bile hızlı" iddiası ölçülmedi, PyPI indirme
hacmi ölçülmedi, imzaların doğruluğu ve `file-type` ile örtüşme oranı test edilmedi.
