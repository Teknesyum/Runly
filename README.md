# Runly

Windows üzerinde `.js`, `.ps1`, `.py` ve `.sh` scriptlerini çift tıkla veya sağ tık menüsünden doğrudan çalıştıran başlatıcı.

## Neden gerekli?

Windows varsayılan olarak bu dosya türlerini açar, çalıştırmaz:

- **`.js`**: Node.js yorumlayıcısı yerine metin editörü açılır
- **`.ps1`**: PowerShell scriptleri kısıtlı güvenlik modunda çalıştırılır (yerel diskten çalıştırma izni yoktur)
- **`.py`**: Python varsa bile varsayılan işlem çoğu zaman hatalıdır
- **`.sh`**: Git Bash veya WSL'e yönlendirilse de beklenmeyen davranışlar olabilir

Runly bu sorunları çözer: scriptleri algılar, doğru yorumlayıcıyı seçer ve güvenli bir şekilde çalıştırır.

## Kurulum

### Otomatik Kurulum

1. `install.ps1` dosyasını indirin.
2. PowerShell'i açın (Başlat → PowerShell).
3. Şu komutu çalıştırın:
   ```powershell
   Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope CurrentUser
   ```
4. Sonra:
   ```powershell
   & "C:\İndirilenler\install.ps1"
   ```

Runly ayarlar penceresi açılacaktır. Orada gerekli ayarları yapın.

### Sessiz kurulum yoktur

`install.ps1` yalnızca dosyaları yerine kopyalar ve ayarlar penceresini açar. Uzantı bağlama
işini penceredeki **Kur / Güncelle** düğmesi yapar, çünkü Windows 11 her uzantı için sizin
onayınızı istiyor (bkz. [Bilinen sınırlar](docs/KNOWN-ISSUES.md)). Bu yüzden komut satırından
tek seferde biten bir kurulum mümkün değil.

## Kullanım

### Çift Tık ile Çalıştırma

1. Bir `.js`, `.ps1`, `.py` veya `.sh` dosyasına çift tıklayın.
2. Runly güvenlik durumunu kontrol eder:
   - **Güven**: Dosya güvenilir bulunursa doğrudan çalıştırılır
   - **İlk Çalıştırma**: Dosya ilk kez çalıştırılıyorsa onay istenir
   - **Reddedildi**: Güven dışı dosya çalıştırılmaz

3. PowerShell scriptleri (`.ps1`) için Windows UAC onay penceresi açılabilir (bkz: "_PowerShell Scripti Güvenlik Onayı_" bölümü).

### Sağ Tık Menüsü ile Çalıştırma

1. Bir script dosyasına sağ tıklayın.
2. **"Runly ile çalıştır"** seçeneğini tıklayın.
3. Bir dialog açılır:
   - Dosya yolunun doğru olduğunu kontrol edin
   - İsteğe bağlı olarak argümanlar girin (örn: `dosya.txt --verbose`)
   - **Çalıştır** butonuna tıklayın

### Komut Satırından Çalıştırma

```bash
# Direct Runly usage
Runly.exe hello.js
Runly.exe hello.ps1
Runly.exe myscript.py arg1 arg2
```

## Güvenlik Modeli

Runly üç güvenlik seviyesi kullanır:

### 1. Güvenilir Dosyalar (Yeşil)

Aşağıdakilere sahip dosyalar otomatik çalıştırılır:
- Runly'nin güven deposunda kayıtlı bir sertifika
- Son kaydedilişinden bu yana değiştirilmemiş
- NTFS akışında Windows MOTW işareti yok

### 2. İlk Çalıştırma (Sarı)

Dosya ilk kez çalıştırılıyorsa:
1. Runly "Bu dosyayı çalıştırmak istiyor musunuz?" sorar
2. "Evet", "Hayır" veya "Daima Güven Et" seçenekleri sunar
3. "Daima Güven Et" seçilirse dosya güven deposuna eklenir

### 3. Reddedilmiş Dosyalar (Kırmızı)

Runly'nin güven dışı listesindeki dosyalar çalıştırılmaz. Bunlar genellikle:
- Ağdan indirilen dosyalar (MOTW işareti var)
- Güvenlik uyarısı gösteren dosyalar
- Kullanıcının "Asla Çalıştırma" seçtiği dosyalar

## PowerShell Scripti Güvenlik Onayı

Windows, PowerShell scriptlerini çalıştırmadan önce ek onay gerektirir:

### İlk Çalıştırmada (Otomatik)

Runly bir PowerShell scripti çalıştırırken:

1. **Runly başlıyor** → Dosya güvenlik denetimi yapılır
2. **PowerShell başlıyor** → Windows, yönetici izni talep edebilir
3. **UAC İzni** (opsiyonel) → "Evet" seçiniz
4. **Script çalışıyor** → Dosya çalıştırılır

### Kullanıcı ExecutionPolicy'si Ayarlamak

PowerShell'in kısıtlı olması işten düştüyse, durumu iyileştirmek için:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Bu, yerel dosyaları çalıştırır ama ağdan indirilen dosyaları ister. (Runly bunu zaten yapıyor, bu sadece manuel çalıştırma içindir.)

## Ayarlar

Runly ayarları buraya kaydedilir:

```
%APPDATA%\Runly\config.json
```

Ayarları değiştirmek için:

1. **Runly Settings GUI**: Kurulum sırasında veya daha sonra `RunlySettings.exe` dosyasını çalıştırın
2. **Elle düzenleme**: `config.json` dosyasını metin editörü ile açın

Başlıca ayarlar:
- **SecurityMode**: `Strict` (en güvenli), `Standard` (varsayılan), `Permissive` (en esnek)
- **TrustExpiry**: Bir dosyanın güven süresi (günler cinsinden)
- **PreferredInterpreters**: Her uzantı için kullanılacak yorumlayıcı

## Kaldırma

### GUI Kaldırması

1. `Denetim Masası` → `Programlar` → `Programları Kaldır` gidin
2. `Runly` seçip `Kaldır` tıklayın
3. Veya: `RunlySettings.exe` çalıştırın → `Kaldır` butonuna tıklayın

### Komut Satırından Kaldırma

```powershell
& "C:\Program Files\Runly\uninstall.ps1"
```

Runly kaldırıldığında:
- Program dosyaları silinir
- Registry kaydı temizlenir
- Türleri için sağ tık menüsü kaldırılır
- Ayarlar ve güven deposu saklanır (silmek istiyorsanız siz silebilirsiniz: `%APPDATA%\Runly`)

## Sorun Giderme

### "Yorumlayıcı Bulunamadı" Hatası

Örneğin: `node.exe bulunamadı` veya `python.exe bulunamadı`

**Çözüm:**
1. Gerekli yazılımın kurulu olduğundan emin olun (Node.js, Python, vb.)
2. Kurulum sırasında "PATH'e ekle" seçeneğini işaretlediğinizden emin olun
3. Bilgisayarı yeniden başlatın (PATH değişikliklerinin etkinleşmesi için)
4. Runly Settings'te yorumlayıcı yolunu elle ayarlayın

### Çift Tık Hâlâ Eski Uygulamayı Açıyor

Windows dosya türü ilişkilendirmesini önbelleğe alabilir.

**Çözüm:**
1. Kurulum sırasında "Dosya Türlerini Kaydet" seçeneğinin seçili olduğundan emin olun
2. Runly Settings'te `Dosya Türlerini Yenile` butonunu tıklayın
3. Komut satırından:
   ```powershell
   & "C:\Users\<UserName>\AppData\Local\Programs\Runly\Runly.exe" --register-types
   ```
4. Windows Explorer'ı kapatıp açın (F5 ile yenileyin)

### ExecutionPolicy Hatası (PowerShell)

Örneğin: `Bu sisteme Script dosyaları yüklenemez...`

**Çözüm:**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Sonra PowerShell'i kapatıp açın.

### Script Çıktısı Görmek

Normalde script çalışır ve kapanır. Çıktısını görmek için:

1. PowerShell veya CMD ile scriptini açın
2. Veya `output.log` dosyasını kontrol edin:
   ```
   %APPDATA%\Runly\logs\
   ```

## Yasal Uyarı

Runly programı çalıştırılabilir yapıyor. Bu, sisteminizi daha işlevsel hale getirmesine rağmen, güvenlik sorumlulukları artırır:

- **Kötü amaçlı script**: Eğer güvenilmeyen bir script'i "Daima Güven Et" ile çalıştırırsanız, bu script sisteminiz üzerinde tam kontrol elde edebilir.
- **Ağ kaynakları**: İnternetten indirilen scriptleri doğrudan çalıştırmayın (Runly bu tür dosyaları MOTW işareti yüzünden varsayılan olarak engeller).
- **Güncellemeler**: Runly'yi düzenli olarak güncelleyin (güvenlik yamaları için).

Runly güvenlik modeli makul önlemleri alır, ancak hiçbir sistem %100 güvenli değildir. Sorumlu kalmak size bağlıdır.

---

**Runly sürümü:** 0.1.0  
**Son güncelleme:** 2026-08-09  
**Destek:** [github.com/...](https://github.com/)
