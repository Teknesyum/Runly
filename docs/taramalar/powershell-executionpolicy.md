# PowerShell/PowerShell — ExecutionPolicy ve Unblock-File

## 1. Künye
`PowerShell/PowerShell` · **Lisans: MIT** · 55.059 yıldız · 1.604 açık issue (tüm depo) ·
son push 2026-08-20 · son etiketli sürüm `v7.6.5` (2026-08-14).

## 2. Ne yapıyor
ExecutionPolicy, `.ps1`/`.psm1`/`.ps1xml` dosyalarının hangi koşulda yükleneceğini
belirler; varsayılan `RemoteSigned` internetten inen imzasız script'i reddeder.
`Unblock-File` dosyanın `Zone.Identifier` ADS'ini silerek bu reddi kaldırır.

## 3. Runly ile kesişimi
Runly'nin güvenlik kapısıyla **birebir aynı problem**: MOTW oku, karara bağla, kaldırma
yolu sun. `MotwService.Strip()` = `Unblock-File`.

**MOTW'nin güvenilmez tarafı — birincil kaynaktan.** `about_Execution_Policies` açıkça
yazıyor: "Dosya indirmenin başka yöntemleri dosyayı Internet Zone'dan gelmiş olarak
işaretlemeyebilir. Örnekler: `curl.exe`, `Invoke-RestMethod`, `Invoke-WebRequest`."
MOTW'nin **yokluğu hiçbir şey kanıtlamaz**. Runly'nin diyaloğu "bu dosya internetten
indirilmemiş" dememeli, "indirildiğine dair işaret yok" demeli. Birincisi yanlış güvence.

**Zone okuma hata hâli.** Belge, Server Core / Nano Server'da ve masaüstü kabuğu hazır
değilken zone denetiminin `AuthorizationManager check failed` ile **çöktüğünü** söylüyor;
sebep, PowerShell'in zone doğrulaması için `explorer.exe` kabuk API'lerini kullanması.
Runly ADS'i doğrudan okuduğu için bu tuzağa düşmüyor, ama kendi hata hâli aynı yerde:
FAT32 veya ADS desteklemeyen paylaşımda **zone okunamadığında ne olacağı ayrı bir karar
dalı olmalı**, "MOTW yok" ile aynı dal değil.
**"Güvenlik sınırı değil" itirafı.** Belgenin kendi cümlesi: "Yürütme ilkesi, kullanıcı
eylemlerini kısıtlayan bir güvenlik sistemi değildir… kullanıcılar script içeriğini komut
satırına yazarak ilkeyi kolayca atlatabilir." Runly'nin `.ps1` risk notu bunu zaten doğru
söylüyor ("kolayca atlatılabilir").

**Kapsam.** Beş kapsam öncelik sırasıyla (MachinePolicy > UserPolicy > Process >
LocalMachine > CurrentUser); `Get-ExecutionPolicy -List` hepsini **aynı anda** gösterir.
**Güven listesi / symlink.** Yol tabanlı allow-list yok. Kesişmiyor.

## 4. Alınacak fikir
1. **`-List` benzeri "etkin karar" tablosu** — bir dosya için MOTW durumu, uzantı kuralı,
   güven kaydı, aktif mod ve hangisinin kazandığı tek ekranda. Neden: güven listesini
   denetlenebilir kılar. Maliyet: düşük, karar zaten `SecurityDecision.Reason`'da.
2. **"İşaret yok" ≠ "güvenli" dili** — MOTW yokluğunu güvence gibi sunan her cümleyi
   diyaloglardan ve `locale/*.json`'dan çıkar. Maliyet: düşük, metin revizyonu.
3. **"Zone okunamadı" ayrı bir sonuç** — `MotwService.GetZoneId` `int?` dönüyor; `null`'ın
   "ADS yok" mu "okunamadı" mı olduğu çağıranda ayrışmıyor. Neden: FAT32/ağ paylaşımında
   Runly'yi dürüst kılar. Maliyet: düşük, dönüş tipinde tek ayrım.

## 5. Kaçınılacak hata
- **#9707 (kapalı): "Unblock-File fails silently."** 5.1'de çalışan unblock, 6.2.1'de
  sessizce yapılmıyordu. Runly `Strip()` sonrası `GetZoneId` ile teyit etmeli.
- **#26121 (açık, 2025-09-30):** kullanıcı profili ağ yolundaysa ExecutionPolicy her şeyi
  Internet Zone sayıp iş göremez hâle geliyor. UNC, zone tabanlı her tasarımın kırılma yeri.
- **#27233 (açık):** meşru bir modül içe aktarımı çok sayıda güvenlik uyarısı üretiyor —
  uyarı sayısı arttıkça değeri sıfıra gider; toplu klasör açmada Runly'de de aynı risk.

## 6. Doğrulama
Okudum: `gh api repos/PowerShell/PowerShell` + `releases/latest`; resmî belge
`about_Execution_Policies` (7.6, ms.date 2025-03-13) tam metni — alıntılar oradan; issue
#9707, #26121, #27233, #12336. Okuyamadım: `Unblock-File` ve `AuthorizationManager`
kaynak kodu — bugünkü uygulama **doğrulanamadı**, dayanak belge ve issue. Belge ayrı
depoda (`MicrosoftDocs/PowerShell-Docs`); alıntılar kod deposundan değil oradan gelir.
