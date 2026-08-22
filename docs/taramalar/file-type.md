# file-type

## 1. Künye

| Alan | Değer |
|---|---|
| Depo | `sindresorhus/file-type` (GitHub), npm paketi `file-type` |
| **Lisans** | **MIT** (`license` dosyası; `package.json` → `"license": "MIT"`). Marka ayrı korunmuyor, ek şart yok. |
| Yıldız | 4.320 |
| Son commit | 2026-08-15 |
| Son etiketli sürüm | **v22.0.2** / 2026-08-15 (commit ile aynı gün — canlı bakım) |
| Açık issue | **0** |
| Kayıt | `source/supported.js`: **183 uzantı**, **178 MIME türü**. Bağımlılık: 4 paket (`strtok3`, `token-types`, `uint8array-extras`, `@tokenizer/inflate`). `engines.node >= 22`, ESM-only. |

## 2. Ne yapıyor

Bir dosyanın, akışın veya bayt tamponunun türünü **içeriğindeki imzadan** (magic number) çıkarır;
uzantıya hiç bakmaz. Akış üzerinden çalıştığı için dosyanın tamamını belleğe almadan, yalnız
gereken baş bloğu okuyarak karar verir.

## 3. Runly ile kesişimi

Tek kesişim ekseni **içerikten tür tespiti** — Runly'nin bugün yapmadığı, ama "uzantısı `.txt`
ama içi PE çalıştırılabilir" senaryosunu yakalayacak tek yol bu. Uzantı/MIME eşleme tablosu
olarak kullanılamaz: 183 uzantı, çoğu ikili medya/arşiv formatı; `.bat`, `.ps1`, `.cmd`, `.lnk`
gibi Runly'nin risk tarafındaki metin/kabuk türlerinin imzası yok, çünkü onların imzası yok.
Görünen ad, kategori, çeviri, tehlikeli tür işareti **yok**. Doğrudan da kullanılamaz: Node.js
kütüphanesi, .NET WinForms'a gömülmez — alınan şey davranış modeli. (08'de tema ekseninde geçti.)

## 4. Alınacak fikir

1. **README'nin uyarısı, tavır olarak.** Depo en üste `[!IMPORTANT]` koyuyor: tespit
   "best-effort bir ipucu"dur, dosyanın gerçekten o tür veya sağlam olduğunu garanti etmez.
   Runly içerik tespiti eklerse aynı dili kullanmalı — "bu dosya güvenli" değil "uzantı ile
   içerik uyuşmuyor" demeli. *Lisans: MIT; metin değil tavır alınıyor.*
2. **Akış/erken çıkış tasarımı.** Yalnız baş bloğu okur, tüm dosyayı yüklemez. Runly'de
   Explorer'daki bir klasörün yüzlerce dosyası taranacaksa tek doğru model bu. *Lisans: MIT —
   desen serbest, gerekirse imza tablosu da atıfla alınabilir.*
3. **Güvenlik sınırını yazılı çizmek.** README, kötü niyetli girdiye dayanıklılığın
   "best-effort" olduğunu ve boyut sınırı + zaman aşımının *çağıranın* sorumluluğu olduğunu
   yazıyor; bunlar açık sayılmıyor. Runly'nin KNOWN-ISSUES'ında böyle bir sınır cümlesi işe
   yarar. *Lisans: desen — serbest.*

## 5. Kaçınılacak hata

- **Ekosistem uyumsuzluğu gizli maliyettir.** ESM-only, Node ≥ 22, 4 çalışma zamanı bağımlılığı.
  .NET tarafında karşılığı yok; "buradan alalım" kararı Node çalışma zamanı taşımak demek —
  Runly için kabul edilemez. Yalnız desen alınır.
- **Kapsam yanılgısı.** 183 uzantı bir "tür veritabanı" değil, "imzası olan ikili formatlar"
  listesi. Runly kataloğunun kapsam denetimi için kullanılmaz, o iş mime-db'nin.
- **Sürüm hızı.** Ana sürüm 22 — bu depo kırıcı değişiklik yapmakta çekingen değil. Veri veya
  API'sine bağlanan taraf sık güncelleme yükünü kabul etmiş olur.

## 6. Doğrulama

Okunan: GitHub API künyesi (MIT, 4.320 yıldız, 0 açık issue, son push), `releases/latest`
(v22.0.2), depo kök listesi, `source/` listesi, `package.json` (sürüm, lisans, bağımlılıklar,
`engines`), `readme.md`'nin güvenlik/uyarı bölümü. `source/supported.js` indirilip sayıldı —
183 uzantı / 178 MIME bu dosyadan. `doğrulanamadı`: npm indirme hacmi ve "en yaygın kullanılan
tespit kütüphanesi" tipi iddialar ölçülmedi; imzaların doğruluğu test edilmedi.
