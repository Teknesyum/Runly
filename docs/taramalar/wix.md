# wixtoolset/wix

## 1. Künye

- Depo: `wixtoolset/wix`
- Lisans: **MS-RL — Microsoft Reciprocal License** (LICENSE.TXT'ten okundu; GitHub API `NOASSERTION` döndürüyor). OSI onaylı, **dosya bazında karşılıklı**: MS-RL'li bir kaynak dosyayı alıp değiştirirsen o dosyayı aynı lisansla yayımlamak zorundasın. Runly açısından pratik sonuç: **koddan alıntı yapılmaz**, yalnız desen alınır.
- **Ek yük:** ayrıca `OSMFEULA.txt` var — Open Source Maintenance Fee. Kaynak kod MS-RL ile serbest, ama **ikili sürümleri indirmek, issue açmak/yorumlamak ve tartışmalara katılmak** ücrete tabi. Yıllık brüt geliri 10.000 USD'nin altındaki kullanıcılar muaf.
- Yıldız: 1.124 · Açık issue: **3** — bu sayı yanıltıcı, sorun takibi ayrı depoda: `wixtoolset/issues`, **666 açık issue**, son push 2025-06-09.
- Son commit: 2026-08-18 · Son etiketli sürüm: **v7.0.0, 2026-04-06**

## 2. Ne yapıyor

MSI paketleri ve bunları zincirleyen bootstrapper'lar üreten araç zinciri. Kurulum niyetini XML ile bildiriyorsun, WiX bunu Windows Installer'ın veritabanı biçimine derliyor.

## 3. Runly ile kesişimi

- **Kurulum yeri, sürümleme, KeyPath ve self-heal (Darwin descriptor), ICE kural denetimi:** 05'te var.
- **Kayıt yedeği:** MSI'ın kendi rollback günlüğü var — kurulum yarıda kalırsa Installer değiştirdiği kayıtları geri alır. Runly'nin elle aldığı yedeğin işletim sistemi düzeyindeki karşılığı bu.
- **Paket doğrulama:** Burn zincirdeki her paketi indirdikten sonra hash ve/veya Authenticode imzasıyla doğruluyor; doğrulanmayan paket çalıştırılmıyor.
- **Kesişmeyen taraf:** Runly per-user, HKCU ve MSI kullanmıyor. WiX'ten alınacak olan araç değil, **muhasebe disiplini**.

## 4. Alınacak fikir

1. **Burn'ün motor / arayüz ayrımı: `src/burn/engine` ve `src/burn/stub`, arayüz tarafında ayrı `ext/Bal` (Bootstrapper Application Layer).** Kurulum motoru kararları veriyor, arayüz yalnız olayları dinleyip komut gönderiyor; arayüz değiştirilebilir bir bileşen. Runly'nin karşılığı: registry motoru WinForms'tan tamamen ayrık olmalı, öyle ki sessiz/CLI kurulum ekstra iş olmadan çıksın. Maliyet: sınır zaten `Runly.Core`/`Runly.Settings` olarak var — korunması gereken bir kural, yeni iş değil.
2. **Uzantılar ayrı paket, çekirdek dar: `ext/` altında Firewall, Http, Iis, NetFx, PowerShell, Util, UI ayrı ayrı.** Çekirdek yalnız kurulum muhasebesini biliyor; alan bilgisi uzantıda. Runly'de karşılığı: bağlam menüsü, MOTW, ikon çekme gibi işler çekirdeğe değil ayrı modüllere gitmeli.
3. **Sorun takibini koddan ayrı depoda tutmak (`wixtoolset/issues`).** Kod deposunda 3, sorun deposunda 666 açık issue. Bu ayrımın faydası tartışmalı (§6) ama tek faydası net: kod deposunun issue listesi PR ve kod incelemesi için temiz kalıyor. Runly tek geliştiricili; **alınacak olan ayrı depo değil, etiketle ayırma disiplini**. Maliyet: sıfır.

## 5. Kaçınılacak hata

Custom action ile yazılan kayıtların bileşen muhasebesi dışında kalıp kaldırmada silinmemesi — orphan kaydın klasik kaynağı (05'te var).

Bu taramada yeni görülen tuzak **süreç tarafında**: sorun takibinin ayrı depoya taşınması + issue açmanın ücrete bağlanması, dışarıdan bakan birinin projenin gerçek hata yükünü ölçmesini imkânsız hale getiriyor. `wixtoolset/wix` sayfasında 3 açık issue görünüyor, gerçek sayı 666 ve o depo **2025-06-09'dan beri push almamış**. Bağımlılık değerlendirirken tek depo sayfasına bakmak yanlış sonuç veriyor. Runly açısından ders çift yönlü: (a) üçüncü taraf bağımlılığı değerlendirirken issue sayısının nerede tutulduğunu sor; (b) Runly kendi deposunu böyle bölmemeli — kullanıcı sorunu nereye yazacağını aramamalı.

İkinci gizli maliyet: WiX'i **kaynaktan derlemek** README'ye göre Visual Studio 2026 (18.6+), .NET 10 Runtime, .NET Framework 4.8 SDK ve C++ masaüstü iş yükü istiyor. Ücreti ödemeden ikili almak istersen bu kurulum yükü seninde.

## 6. Doğrulama

- Kaynaktan okundu: `repos/wixtoolset/wix` metadata, `releases/latest` (v7.0.0), `commits[0]`, `contents/LICENSE.TXT` (MS-RL metni), `contents/OSMFEULA.txt` (ücret şartları, 10.000 USD eşiği), README (OSMF bölümü + derleme önkoşulları), kök ve `src/`, `src/burn/`, `src/ext/` dizin listeleri, açık issue listesi, `repos/wixtoolset/issues` metadata (666 açık, son push 2025-06-09).
- Okunmadı / `doğrulanamadı`: WiX kaynak kodu okunmadı (MS-RL nedeniyle zaten alıntılanmayacak). Burn'ün paket doğrulama davranışı (hash + Authenticode) resmî belgeye dayanıyor, bu taramada koddan doğrulanmadı — `doğrulanamadı`.
- MSI rollback günlüğü davranışı Windows Installer belgesinden bilinen genel davranış, bu depodan doğrulanmadı — `doğrulanamadı`.
- Ücretin fiilen nasıl uygulandığı (ödemeyen kullanıcının issue'sunun kapatılıp kapatılmadığı) `doğrulanamadı`.
