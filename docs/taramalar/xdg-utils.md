# xdg-utils

## 1. Künye

| Alan | Değer |
|---|---|
| Depo | `xdg/xdg-utils` — gitlab.freedesktop.org (proje id 1212). **GitHub'da `freedesktop/xdg-utils` diye bir depo yok** (API 404); birincil kaynak GitLab. |
| **Lisans** | **MIT** — `LICENSE` dosyası MIT metnini birebir taşıyor (yorum işaretleriyle, betiklere gömülü olduğu için). Marka koruması ayrı değil. |
| Yıldız | 25 (GitLab), 85 fork |
| Son commit | 2026-06-05 "Fix missing dependency documentation" |
| Son etiketli sürüm | **v1.2.1** / 2024-02-06 (öncesi v1.2.0 / 2024-01-30) — ≈2,5 yıldır yeni etiket yok |
| Açık issue | 115 açık, 308 toplam |
| Kayıt | Veri kümesi yok, 14 kabuk betiği: `xdg-open`, `xdg-mime`, `xdg-settings`, `xdg-desktop-menu`, `xdg-email`, `xdg-icon-resource`, `xdg-file-dialog` vb. |

## 2. Ne yapıyor

Masaüstünden bağımsız bir kabuk betiği kümesiyle "dosyayı aç", "varsayılan uygulamayı sor/ayarla",
"menü girdisi kur" gibi işleri soyutlar. Her betik altta KDE, GNOME, LXQt ve genel (freedesktop
`mimeapps.list`) için ayrı bir uygulama tutar, ortamı tespit edip doğru olanı çağırır.

## 3. Runly ile kesişimi

Runly'nin **işlevsel** karşılığı — Linux'ta varsayılan uygulama atamayı yapan araç bu. Kesişim
veri değil davranış: `xdg-mime query default`, `xdg-mime default <desktop> <mime>`, `xdg-open`.
Uzantı/MIME tablosu, görünen ad, kategori, çeviri, tehlikeli tür koruması **hiç yok** — tür
verisini shared-mime-info'dan alır, kendisi taşımaz. İçerikten tespit de yok, `xdg-mime query
filetype` işi `file`/`update-mime-database` tarafına devreder. (08'de tema ekseninde geçti.)

## 4. Alınacak fikir

1. **Arka uç başına ayrı yazıcı + genel yedek.** `xdg-mime` içinde `make_default_kde`,
   `make_default_gnome`, `make_default_lxqt`, `make_default_generic` ayrı fonksiyonlar; ortam
   tespit edilemezse genel olan `mimeapps.list`'e yazar. Runly'de Windows sürümü ve
   UserChoice imzası benzer dallanma gerektiriyor; "ortam tanınmadı" hâli hata değil, yedek yol
   olmalı. *Lisans: MIT — desen de betik de alınabilir; zaten desen alınıyor.*
2. **Yaz-ve-taşı (write-then-rename) dosya güncellemesi.** Betik `mimeapps.list`'i `.new`
   uzantılı geçici dosyaya yazıp sonra `mv` ile yerine koyuyor; yarıda kesilen yazma dosyayı
   bozmuyor. Runly'nin registry yedek/geri alma dosyaları için aynı disiplin. *Lisans: MIT.*
3. **Sembolik bağ çözme.** Hedef dosya symlink ise gerçek yolu bulup **oraya** yazıyor, bağı
   dosyayla ezmiyor. Runly'nin config ve yedek yollarında (junction/reparse point) aynı tuzak
   var. *Lisans: MIT — desen serbest.*

## 5. Kaçınılacak hata

- **Bakım yükü görünür.** 115 açık issue / 308 toplam ve 2,5 yıldır etiketsiz ana dal. Aktif
  commit var ama sürüm çıkmıyor; dağıtımlar yamalı sürümler taşıyor. Bağımlılık kurulacak
  proje değil, okunacak proje.
- **Kabuk betiği çözümünün doğal sınırı.** Mantık `sed`/`awk` içine gömülü; test edilebilirliği
  düşük, hata mesajları ortamdan ortama değişiyor. Runly'nin C# tarafında bu deseni taklit
  ederken mantığı betiğe değil sınıfa koymak gerekir.
- **Windows tarafında hiçbir karşılığı yok.** UserChoice hash'i, `SetUserFTA` engelleri,
  MoTW — bunların hiçbiri burada yok. Linux'un "dosyaya yaz, bitti" kolaylığı yanıltıcı.

## 6. Doğrulama

Okunan: GitLab API künyesi (yıldız, fork, son aktivite), etiket listesi (v1.2.1 / 2024-02-06),
son commit, issue istatistikleri (62 değil, 115 açık), `LICENSE` (MIT metni), `README.md`,
kök ve `scripts/` ağacı, `scripts/xdg-mime.in` fonksiyon adları. `doğrulanamadı`: `xdg-open`
başarım/kapsam iddiaları test edilmedi; GitHub'daki `freedesktop/xdg-utils` aynasının varlığı
sorgulandı ve **bulunamadı** (404) — rapor GitLab kaynağına göre yazıldı.
