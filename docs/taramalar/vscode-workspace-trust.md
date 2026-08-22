# microsoft/vscode — Workspace Trust

## 1. Künye
`microsoft/vscode` · **Lisans: MIT** · 189.263 yıldız · 20.121 açık issue (tüm depo) ·
son push 2026-08-22 · son etiketli sürüm `1.134.0` (2026-08-19).

## 2. Ne yapıyor
Klasör açılırken "bu dosyaların yazarlarına güveniyor musun" diye sorar; güvenilmezse
Restricted Mode'a düşer. O modda terminal, task, debug, workspace ayarları ve trust
bildirmeyen eklentiler kapalı, düz metin düzenleme açıktır.

## 3. Runly ile kesişimi
**Güven kapsamı.** Güven klasör düzeyinde verilir, alt klasörlere miras kalır; arayüz alt
klasör için "başka bir klasör nedeniyle güvenilir" der ve onu tek başına geri almayı
sunmaz. Runly'nin `TrustMatching` klasör-önek kuralı (SPEC 5.2) aynı model. Fazlası:
VS Code **hangi üst kaydın** güveni ürettiğini söylüyor; Runly diyaloğunda bu yok.

**Symlink/junction.** VS Code belgesinde reparse-point kuralı yok, ilgili #331799 kapalı
(*doğrulanamadı* — kaynak koda inmedim). Runly burada ileride: klasör eşleşmesi
reparse-point çözümlü gerçek yola karşı ikinci kez doğrulanıyor (SPEC 11.1 K21).

**Yorgunluk / üç mod.** `security.workspace.trust.startupPrompt` varsayılanı `"never"`,
`emptyWindow` `true`. Yani VS Code varsayılan olarak **sormamayı** seçmiş; Runly'nin
"her seferinde sor" modunun karşılığı hiç yok. Bilinçli bir yorgunluk kararı.

**Denetim izi.** Trusted Folders & Workspaces editörü tüm güven kayıtlarını tek ekranda
gösterir, tek tek geri aldırır. Runly'de `trust.json`'ın böyle bir yüzü olmalı.

**MOTW.** VS Code MOTW okumaz, karar tamamen yola dayanır. Kesişmiyor.

## 4. Alınacak fikir
1. **"Başka bir klasör nedeniyle güvenilir" mesajı** — diyalog ve liste, bir yolun neden
   serbest geçtiğini üreten üst kaydı adıyla göstersin. Aksi hâlde güven listesi
   denetlenemez. Maliyet: düşük, metin + tek alan.
2. **Kısıtlı mod, ret değil** — Restricted Mode "açma" demiyor, "aç ama çalıştırma"
   diyor. Runly karşılığı: script'i çalıştırmak yerine editörde açmayı öneren üçüncü
   düğme. Maliyet: orta, diyalog + tek yeni sonuç dalı.
3. **Güven listesi ekranı** — tüm kayıtlar tek ekranda, tek tek silinebilir.
   `trust.json`'ı elle düzenletmek kabul edilebilir bir yüz değil. Maliyet: orta.

## 5. Kaçınılacak hata
- Belge açıkça uyarıyor: "Workspace Trust kötü niyetli bir eklentinin kod çalıştırıp
  Restricted Mode'u yok saymasını engelleyemez." Runly de aynı sınırı yazmalı — kapı,
  geçtikten sonra çalışan yorumlayıcıyı sınırlamaz.
- **#126311** (açık, 2021-06-14, 52 tepki): kullanıcılar **açıkça güvenilmez ilan edilen
  klasör** istiyor, beş yıldır yok. Allow-list tek başına yetmiyor; `Downloads` için
  kalıcı deny kaydı Runly'de baştan düşünülmeli.
- **#126636** (açık): Restricted Mode test koşumlarını sessizce bozuyor. Kapı bir işi
  engellediğinde bunu genel hata olarak değil, adı konmuş bir durum olarak bildir.

## 6. Doğrulama
Okudum: `gh api repos/microsoft/vscode` + `releases/latest`; issue #126311, #126636,
#331799; resmî belge `code.visualstudio.com/docs/editing/workspaces/workspace-trust`
(ayar adları, Restricted Mode kapsamı, varsayılanlar, eklenti uyarısı).
Okuyamadım: `workspaceTrust.ts` — symlink/UNC davranışı **doğrulanamadı**.
20.121 açık issue tüm depoya ait; Workspace Trust'a özel sayı **doğrulanamadı**.
