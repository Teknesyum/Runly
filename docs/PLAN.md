# Runly — Proje Yönetim Planı

**Yönetici:** Opus (bu oturum). **Uygulayıcılar:** aşağıdaki tabloya göre.
**Devir yöntemi:** Kullanıcı, `docs/tasks/T<n>.md` dosyasının içeriğini ilgili modele verir.
Model işi bitirince "Teslim Raporu"nu yöneticiye getirir, yönetici onaylar, sonraki paket açılır.

## Görev sırası ve model dağılımı

| # | Paket | Model | Bağımlılık | Neden bu model |
|---|---|---|---|---|
| T0 | Ortam kurulumu (.NET SDK 8) | **Haiku** | — | Mekanik, komut çalıştırıp doğrulama |
| T1 | Solution iskeleti + Core sözleşmeleri | **Opus** | T0 | Tüm projenin şekli buradan çıkar, geri dönüşü pahalı |
| T2 | Core mantık implementasyonu + testler | **Sonnet** | T1 | Sözleşme netse iyi tanımlı, hacimli iş |
| T3 | Launcher + güvenlik kapısı (TaskDialog, AOT) | **Opus** | T2 | Projenin en riskli ve en ince kısmı |
| T4 | Shell/registry entegrasyonu + yedek/geri al | **Opus** | T1 | Sistemi bozma riski var, UserChoice tuzağı |
| T5 | Ayarlar GUI (WinForms) | **Sonnet** | T2, T4 | Geniş ama düz iş |
| T6 | Build/install scriptleri, ikonlar, README, samples | **Haiku** | T3, T4 | Şablonluk iş |
| T7 | Entegrasyon + güvenlik denetimi + paketleme | **Opus** | Hepsi | 15 kabul senaryosunun hakemliği |

**Paketler SIRAYLA yürütülür, eş zamanlı değil.** (Karar, 2026-08-09.) Paralel çalışmak toplam
token tüketimini artırmaz — aynı iş, aynı okumalar — ama 5 saatlik kullanım penceresini iki kat
hızlı tüketir ve kesinti riskini artırır. Teknik olarak T3/T4 paralelleştirilebilir; yine de
sıralı gidilecek.

## İlerleme günlüğü (ZORUNLU — kesinti sigortası)

Oturumlar 5 saatlik kullanım limitine takılıp **iş ortasında kesilebilir.** Bu yüzden her paket,
işe başlar başlamaz bir ilerleme dosyası açar ve **her anlamlı adımdan sonra günceller:**

```
C:\Users\Administrator\Desktop\Projeler\Runly\docs\reports\T<n>-PROGRESS.md
```

Format (dosyanın tamamı her seferinde üzerine yazılır, uzatma yok — 40 satırı geçmesin):

```markdown
STATUS: DEVAM EDIYOR
SON GUNCELLEME: 2026-08-09 15:42

## Yapılacaklar
- [x] ConfigStore yazıldı
- [x] TrustStoreService yazıldı
- [ ] PathSearcher  ← ŞU AN BURADAYIM
- [ ] ScriptInspector
- [ ] MotwService
- [ ] SecurityGate
- [ ] InterpreterResolver
- [ ] ProcessLauncher
- [ ] FileLogger
- [ ] Testler
- [ ] Kabul kriterleri (build / test / AOT publish)

## Sıradaki somut adım
PathSearcher'da PATHEXT taraması yazılacak; 0-byte stub filtresi henüz yok.

## Yazdığım/değiştirdiğim dosyalar
src/Runly.Core/Services/ConfigStore.cs (yeni, 96 satır, tamam)
src/Runly.Core/Services/TrustStoreService.cs (yeni, 120 satır, tamam)
src/Runly.Core/Services/PathSearcher.cs (yeni, YARIM)

## Bilinmesi gerekenler
Kesinti olursa yarım kalan tek dosya PathSearcher.cs. Diğerleri derleniyor.
```

Kurallar:
- İlk adımın bu dosyayı **iskelet hâliyle** oluşturmak olsun (yapılacaklar listesi dolu,
  hepsi işaretsiz). Sonra her madde bitince güncelle.
- Bir dosyayı yarım bırakıp başka dosyaya geçme. Kesinti gelirse tek bir yarım dosya olsun.
- Ara ara `dotnet build` çalıştır ki "en son ne zaman yeşildi" bilgin olsun; PROGRESS'e yaz.
- Paket tamamlanınca `STATUS: TAMAM` yaz ve `T<n>-COMPLETE.md`'yi oluştur.
  PROGRESS dosyası silinmez, kayıt olarak kalır.

## Kesintiden sonra devam etme

Kullanıcı yeni bir oturuma şu direktifi verir:

```
C:\Users\Administrator\Desktop\Projeler\Runly\docs\reports\T<n>-PROGRESS.md dosyasını oku.
Bir önceki oturum kesildi. Oradaki "Sıradaki somut adım"dan devam et.
Önce docs/SPEC.md ve docs/tasks/T<n>.md dosyalarını oku, sonra tamamlanmış maddeleri
BAŞTAN YAPMA — sadece kalanları bitir. PROGRESS dosyasını güncellemeye devam et.
```

Devam eden oturumun ilk işi: PROGRESS'te "tamam" denen dosyaların gerçekten var ve derlenir
olduğunu `dotnet build` ile doğrulamak. Uyuşmazlık varsa PROGRESS'i düzelt, sonra devam et.

## Teslim raporu nasıl verilir (ZORUNLU)

İşin bitince raporunu sohbete yazma — **dosyaya yaz**:

```
C:\Users\Administrator\Desktop\Projeler\Runly\docs\reports\T<n>-COMPLETE.md
```

Klasör yoksa oluştur. Dosyanın ilk satırı şu olmalı:

```
STATUS: TAMAM        (veya KISMİ / BLOKE)
```

Dosyayı yazmak paketin **son** adımıdır — build/test yeşil olmadan yazma. Yazdıktan sonra
sohbete sadece tek satır yaz: `T<n> bitti, rapor docs/reports/T<n>-COMPLETE.md dosyasında.`
Proje yöneticisi (Opus) raporu oradan okuyup doğrulayacak ve sonraki paketi açacak.

`KISMİ` veya `BLOKE` ise dosyada **nerede kaldığını ve neyin eksik olduğunu** açıkça yaz —
bir sonraki oturum o dosyadan devam edebilmeli.

## Rapor dosyasının içeriği

```
STATUS: TAMAM

## T<n> Teslim Raporu
- Durum: TAMAM / KISMİ / BLOKE
- Oluşturulan/değişen dosyalar: (yol listesi, satır sayısıyla)
- Build sonucu: (dotnet build çıktısının son satırları)
- Test sonucu: (varsa dotnet test özeti)
- SPEC'ten sapmalar: (yoksa "yok" — varsa neden)
- Sonraki pakete not: (bir sonraki modelin bilmesi gereken şeyler)
- Takıldığım/karar veremediğim noktalar:
```

## Görev durumu

| # | Paket | Durum |
|---|---|---|
| T0 | Ortam kurulumu | ✅ ONAYLANDI (SDK 8.0.423, AOT doğrulandı) |
| T1 | İskelet + sözleşmeler | ✅ ONAYLANDI (0 uyarı, 6/6 test, AOT exe 1.11 MB) |
| T2 | Core mantık | ✅ ONAYLANDI (K9 düzeltmesiyle) |
| T4 | Shell/registry | ✅ ONAYLANDI |
| T3 | Launcher + güvenlik kapısı | ✅ ONAYLANDI (`Runly.exe` 2,93 MB, 7/7 elle test) |
| T5 | Ayarlar GUI | ✅ ONAYLANDI (~1500 satır, 12/13 elle test; DPI kontrolü kullanıcıya kaldı) |
| T6 | Build/ikon/samples/README | ✅ ONAYLANDI (ikonlar K18 ile düzeltildi) |
| T7 | Entegrasyon + denetim | ✅ ONAYLANDI — kararı: **yayına hazır değil** |
| R1 | Dürüst bağlama + kaldırma (B1/B2) | ✅ ONAYLANDI (176 test; makine temizliğini yönetici tamamladı, K24) |
| R2 | Junction + script düzeltmeleri (B3/B4/B5) | ✅ ONAYLANDI (180 test; README `-Silent` bölümünü yönetici düzeltti) |
| R3 | Kısa regresyon + paketleme | ✅ ONAYLANDI — 6/6 geçti, zip SHA doğrulandı, makine temiz |
| R4 | Bayat durum çubuğu (B7) | ✅ ONAYLANDI — gerçek makinede doğrulandı, B7 kapandı |
| R5 | Teknesyum Neon arayüz geçişi | ✅ ONAYLANDI |
| R6 | Çerçevesiz neon pencere + üst/alt şerit | ✅ ONAYLANDI |

**Yayın durumu:** 0.2.0 yayında (AGPL-3.0-or-later, 228/228 test, 0 uyarı). Bundan sonraki iş
`docs/UI-PLAN.md` üzerinden yürüyor; T/R paket dizisi kapandı.

Toplam durum: 166/166 test yeşil, 0 uyarı, registry ve `%APPDATA%\Runly` temiz, `.ps1` UserChoice
sağlam (K17 — bildirilen kayıp yanlış alarmdı).
Açık teknik borç: **K16** (`.cmd`/`.bat` yorumlayıcı desteği) — T7'de kapatılacak.
Açık doğrulama: 125%/150% DPI kontrolü (sistem ayarı gerektirdiği için ajanlar yapmadı).

## Değişmez kurallar (her pakete geçerli)

1. `docs/SPEC.md` tek doğruluk kaynağıdır. SPEC'e aykırı bir şey yapma; SPEC eksikse
   uydurma, raporda "karar veremediğim nokta" olarak bildir.
2. Kendi paketinin dışındaki dosyalara **dokunma**. Başka bir paketin dosyasında hata
   görürsen düzeltme, raporda bildir.
3. NuGet paketi ekleme (xUnit hariç).
4. Kullanıcıya görünen metin Türkçe, kod İngilizce.
5. İş bitmeden "tamamlandı" deme. `dotnet build` yeşil değilse durum KISMİ'dir.
6. Registry'ye yazan kod yazıyorsan, önce yedek alan kodu yaz.
7. `docs/reports/T<n>-PROGRESS.md` dosyasını **ilk adımda** oluştur ve her maddede güncelle.
   Oturumun kesilebileceğini varsay. Bir dosyayı yarım bırakıp diğerine geçme.
