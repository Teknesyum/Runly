# Runly — Bilinen Sınırlar ve Açık Sorunlar

Sürüm 0.1.0 · Son güncelleme: 2026-08-11 (R3)

## 1. Windows kısıtları (çözümü yok)

- **Hiçbir uzantı tek tıkla bağlanamaz (K19 + K23).** Kurulum ProgID'leri ve `.ext` varsayılanını
  yazar, ama Windows 11 çift tıkta `FileExts\<ext>\UserChoice`'a bakar. Bağlamanın tek meşru yolu:
  **sağ tık → Birlikte aç → Başka bir uygulama seç → Runly → "Her zaman"**. Hash programatik
  olarak üretilemez; üretmeye çalışmak yasaktır (SPEC §2).
- **`SHOpenWithDialog` varsayılanı bağlayamıyor (K23).** R1 ekranda ölçtü: pencerede yalnız
  "Yalnızca bir kez" düğmesi çıkıyor; `OAIF_FORCE_REGISTRATION` eklenince Windows doğrudan
  reddediyor. GUI bu yüzden kullanıcıyı Ayarlar → Varsayılan uygulamalar'a ya da Explorer'ın
  "Başka bir uygulama seç" akışına yönlendiriyor.
- **`UserChoice` anahtarı bazı makinelerde silinemez.** Windows bu anahtara DELETE'i reddeden bir
  ACE koyabilir. R1'in turunda **engellenmedi** (kendi yazdığımız kayıt sorunsuz silindi), T7'de
  engellenmişti. Kod iki durumu da ele alıyor; silinemezse kaldırma diyaloğu uzantıyı öksüz
  olarak listeliyor ve `ms-settings:defaultapps` kısayolu sunuyor (K20).
- **SmartScreen:** `Runly.exe` imzasız olduğu için ilk çalıştırmada "Bilinmeyen yayımcı" uyarısı
  çıkabilir. Kod imzalama sertifikası olmadan çözümü yok.
- **Antivirüs:** Script çalıştıran imzasız bir launcher heuristik olarak işaretlenebilir.
- **pwsh 7 kurulu değil;** `.ps1` Windows PowerShell 5.1 ile çalışır.

## 2. Açık sorunlar

| # | Sorun | Etki | Sahibi |
|---|---|---|---|
| B6 | **Ayarlar penceresi diskteki config değişikliğini fark etmiyor.** Pencere açıkken config dışarıdan değişirse "Kaydet" eski bellek durumunu geri yazıyor. | Küçük; sadece eşzamanlı düzenlemede. | T5 |
| B7 | **Tablo, uygulama dışında yapılan bağlama değişikliğini canlı yakalamıyor.** Pencere yeniden açılınca doğru gösteriyor. | Küçük. | T5 |
| B8 | **Kök `README.md` satır 38-41 hâlâ `install.ps1 -Silent` örneği veriyor**, oysa bu parametre R2'de kaldırıldı. | Yanlış belge. | T0/T6 kararı |

### Kapatılanlar

- **B1** (kurulum "bağlandı" diye yanlış iddiada bulunuyordu) — R1'de kapatıldı; `Install` artık
  yalnız `UserChoice` bizi gösterirken `Bound` diyor, `AssocQueryString` ile ikinci görüş alıyor.
- **B2** (kaldırmada öksüz `UserChoice`) — R1'de kapatıldı; kaldırma önce tespit ediyor, silmeyi
  deniyor, sonucu registry'ye tekrar bakarak ölçüyor, silemezse açıkça listeliyor.
- **B3** (junction ile güven atlatma) — R2'de kapatıldı; `TrustMatching` artık
  `GetFinalPathNameByHandleW` ile reparse point çözüyor, 4 junction testi yeşil.
- **B4** (`install.ps1 -Silent` ölü kod) — R2'de kaldırıldı.
- **B5** (`samples/hello.ps1` BOM'suz) — R2'de BOM eklendi.

## 3. Doğrulanamayanlar — kullanıcı tarafından doğrulanmalı

- **125% / 150% DPI ölçeklemede Ayarlar penceresi ve TaskDialog görünümü.** Sistem ölçeklemesini
  değiştirmek oturum açma/kapama gerektirdiği için ne T5, ne T7, ne R3 bunu deneyebildi.
  **Kullanıcı doğrulamalı:** Ayarlar → Sistem → Ekran → Ölçek %125 ve %150 yapıp
  `RunlySettings.exe`'yi ve bir güvenlik diyaloğunu açın; kırpılan metin/buton var mı bakın.
- **`--verb runas` (UAC yükseltme).** UAC istemi otomasyonla onaylanamadığı için (Windows UIPI)
  uçtan uca denenemedi; sağ tık menüsünde fiilin **var olduğu** T7'de doğrulandı.
- **Farklı bir kullanıcı hesabında kurulum.** Tek hesapta test edildi.
- **R3'ün 6 maddesi (S1, S7, S12, B3 regresyonu, B5, dürüstlük denetimi)** — bu oturumda gerçek
  makinede çalıştırılamadı, bkz. `docs/reports/R3-COMPLETE.md`.
