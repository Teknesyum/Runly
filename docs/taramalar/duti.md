# duti

## 1. Künye

| Alan | Değer |
|---|---|
| Depo | `moretension/duti` (GitHub) |
| **Lisans** | **Kamu malı (public domain) beyanı** — `COPYRIGHT`: *"duti is free software released into the public domain by Andrew Mortensen, 2008."* Atıf rica ediliyor, şart koşulmuyor. **OSI onaylı bir lisans değil**, SPDX karşılığı yok; GitHub API `NOASSERTION` döndürüyor. |
| Yıldız | 2.056 |
| Son commit | **2023-07-09** — 3 yıldan uzun süredir dokunulmamış |
| Son etiketli sürüm | **Yok** — `releases/latest` 404; depoda `VERSION` dosyası var, GitHub sürümü yayımlanmamış |
| Açık issue | 27 |
| Kayıt | Veri kümesi yok. 4 C kaynağı (`duti.c`, `handler.c`, `plist.c`, `util.c`) + man sayfası |

## 2. Ne yapıyor

macOS'ta belge türleri ve URL şemaları için varsayılan uygulamayı komut satırından ayarlar;
tür anahtarı olarak Apple'ın Uniform Type Identifier'ını (`public.html`, `com.microsoft.word.doc`)
kullanır. Ayrıca `-x <uzantı>` ile bir uzantının mevcut varsayılan uygulamasını sorgular.

## 3. Runly ile kesişimi

Runly'nin macOS'taki birebir işlevsel eşi: kullanıcı seçimini yazma ve okuma. Ama **veri
kesişimi sıfır** — uzantı/MIME tablosu, görünen ad, kategori, çeviri, içerik tespiti,
tehlikeli tür koruması hiçbiri yok. Tür kimliğini de kendi tutmaz, LaunchServices'e sorar.
Alınacak şey yalnız arayüz tasarımı ve ayar kaynağı modeli. Dikkat çeken fark: duti'de
"tehlikeli tür" kavramı yok — `.command` veya `.app` için de aynı tek satır çalışıyor;
Runly'nin 16 `blocked` uzantısı burada karşılıksız. (08'de tema ekseninde geçti.)

## 4. Alınacak fikir

1. **Dört ayrı ayar kaynağı, tek biçim.** Aynı `bundle-id / UTI / rol` üçlüsü stdin'den,
   ayar dosyasından, plist'ten veya komut satırından okunabiliyor; dizin verilirse içindeki
   tüm geçerli dosyalar uygulanıyor, `.` ile başlayanlar atlanıyor. Runly'nin toplu profil
   uygulaması (bir dosya = bir ilişkilendirme kümesi) için hazır tasarım. *Lisans: kamu malı —
   hem desen hem kod serbest, ama Runly'ye kod alınmayacak; C, .NET'e gelmez.*
2. **Rol kavramı (`all` / `viewer` / `editor`).** Bir uygulama bir türü "açabilir" ama
   "düzenleyemez" olabilir. Runly bugün tek bir `defaultKind` alanı taşıyor; rol ayrımı,
   Windows'un "Aç" / "Düzenle" fiil ayrımıyla eşleşiyor. *Lisans: kamu malı — serbest.*
3. **Sorgulama komutunun çıktısı.** `duti -x jpg` üç satır döner: görünen ad, tam yol, bundle
   kimliği. Runly'nin tanı/denetim çıktısı için doğru granülerlik — kullanıcıya ad, makineye
   kimlik, hata ayıklamaya yol. *Lisans: kamu malı — serbest.*

## 5. Kaçınılacak hata

- **Lisans belirsizliği gerçek bir risk.** "Public domain" bazı hukuk düzenlerinde (özellikle
  Avrupa) geçerli bir feragat sayılmıyor; MIT/Unlicense/CC0 gibi bir yedek metin yok. Koddan
  bir şey alınacaksa hukuki durum net değil — desen almak güvenli, satır almak değil.
- **Terk edilmiş.** 3 yıl commit yok, hiç yayımlanmış sürüm yok, 27 açık issue. macOS'un
  LaunchServices davranışı bu sürede değişti; araç bugün kısmen çalışıyor olabilir.
  `doğrulanamadı` — test edilmedi.
- **`-x` özelliği üçüncü taraf kaynaktan.** README, uzantı sorgulama kodunun bir blog
  yazısındaki "kamu malı" kaynaktan geldiğini yazıyor — zincirin ikinci halkası daha belirsiz.

## 6. Doğrulama

Okunan: GitHub API künyesi (2.056 yıldız, 27 issue, son push 2023-07-09), `releases/latest`
(404 — sürüm yok), `commits` (son commit tarihi), depo kök listesi, `COPYRIGHT` tam metni,
`README.md`. `doğrulanamadı`: aracın güncel macOS'ta çalışıp çalışmadığı denenmedi; Homebrew
üzerinden kullanım hacmi ölçülmedi; `pkg-resources` içeriği incelenmedi.
