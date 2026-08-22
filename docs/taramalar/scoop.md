# ScoopInstaller/Scoop

## 1. Künye

- Depo: `ScoopInstaller/Scoop`
- Lisans: **Unlicense veya MIT** (kullanıcı seçer; LICENSE dosyasında `SPDX-License-Identifier: UNLICENSE or MIT`). GitHub API `NOASSERTION` döner, dosyadan okundu. İkisi de OSI/kamuya açık; marka için depoda ayrı bir kayıt yok.
- Yıldız: 24.578 · Açık issue: 538
- Son commit: 2026-08-21 · Son etiketli sürüm: **v0.5.3, 2025-08-12** (bir yıldan uzun süredir etiket yok, geliştirme sürüyor)

## 2. Ne yapıyor

Yönetici hakkı istemeden, kullanıcı profiline uygulama kuran komut satırı paket yöneticisi. Uygulamayı sürümlü klasöre açıp dış dünyaya (shim, PATH, kısayol, dosya ilişkilendirmesi) manifest'ten türetilen bağlar kuruyor.

## 3. Runly ile kesişimi

- **Kurulum yeri:** varsayılan `~\scoop`; uygulama `apps\<ad>\<sürüm>`, dışarıya verilen yol `apps\<ad>\current` junction'ı. Runly'nin `%LOCALAPPDATA%\Programs\Runly` seçimiyle aynı sınıf: HKCU, UAC yok.
- **Yol değişince:** sürüm değişse de `current` sabit kaldığı için kayıtlı yollar bozulmuyor — *ama* bu dolaylılık Explorer tarafında kırılıyor, §5'e bak.
- **Kayıt yedeği:** yok. Scoop yedek almıyor, yeniden üretiyor (05'te var).
- **Sürümleme:** yan yana sürüm klasörü + `scoop reset` ile eski sürüme dönme.
- **Paket doğrulama:** manifest'te `hash` alanı (varsayılan SHA256; `md5:`/`sha1:`/`sha512:` önekleriyle diğerleri). İndirme sonrası eşleşmezse kurulum durur. Ek olarak isteğe bağlı `scoop virustotal` komutu, dosya hash'ini VirusTotal'a sorup kurulum öncesi rapor veriyor.
- **Kaldırma dürüstlüğü:** 05'te var.

## 4. Alınacak fikir

1. **`scoop export` / `scoop import`** — kurulu durumun tamamı JSON'a yazılıyor, başka makinede geri kuruluyor. Runly'nin karşılığı: kullanıcının ilişkilendirme profilini (hangi uzantı hangi ProgID, hangi özel uzantı eklendi) tek dosyaya dışa aktarmak ve yeni makinede geri yüklemek. Runly zaten config'i seyrek tutuyor; dışa aktarma bunun üstüne az iş.
2. **`current` yerine gerçek yolu yaz.** Scoop'un kendi hatası (§5) tam olarak bu: kabuk tarafına dolaylı yol vermek. Runly `DefaultIcon` ve `shell\open\command` değerlerine sembolik/junction yol değil, **çözülmüş gerçek yolu** yazmalı; sürüm klasörü kullanılacaksa güncellemede değeri yeniden yazmalı.
3. **Kurulum öncesi hash + isteğe bağlı ikinci görüş.** `install.ps1` indirdiği zip'in SHA256'sını release'te yayımlanan değerle karşılaştırmalı; eşleşmezse kurulumu başlatmadan durmalı. Scoop'un `virustotal` komutu gibi bunu ayrı ve isteğe bağlı bir adım tutmak, zorunlu bağımlılık yaratmadan güven veriyor.

## 5. Kaçınılacak hata

**Issue #6721 (açık, 2026-08-21): "vscode: File association icons are broken because `current` symlink doesn't point to the actual install root."** Scoop, VS Code'un HKCU altındaki `DefaultIcon` kayıtlarını `apps\vscode\current\...` üzerinden yazıyor; Explorer bu yolu ikon çözerken beklendiği gibi izlemiyor ve `.py`, `.js`, `.cpp`, `.html`, `.css` gibi türlerde ikon boş veya yanlış çıkıyor. Runly için doğrudan uyarı: **kabuğa verilen ikon ve komut yolları reparse point içermemeli.**

İkinci tuzak, **issue #6714 (açık): "Scoop silently overwrites existing manifest.json and install.json without warning or error."** Kurulum defterinin sessizce üzerine yazılması, sonradan "bunu kim yazdı" sorusunu cevapsız bırakıyor. Runly'nin yedek/kurulum defteri aynı hataya açık: var olan yedeği uyarmadan ezmemeli.

Üçüncüsü, shim'lerin bağlam dışı ortamlarda kopması: **#6680 (Task Scheduler)**, **#6612 (SSH oturumu)**, **#6682 (kullanıcı adında Unicode karakter varken CMD)**. Ortak kök: kabuk/oturum bağlamının farklı olduğu yerde dolaylı başlatıcı çalışmıyor. Runly'nin başlatıcısı da Explorer dışı bağlamlardan (zamanlanmış görev, uzak oturum) çağrılabilir.

## 6. Doğrulama

- Kaynaktan okundu: `repos/ScoopInstaller/Scoop` (yıldız, açık issue, push tarihi), `releases/latest`, `commits[0]`, `contents/LICENSE`, kök + `lib/` + `libexec/` dizin listesi, README, issue #6721 / #6714 gövdeleri, `gh search issues` sonuçları.
- Okunmadı / `doğrulanamadı`: manifest hash algoritma önekleri ve `scoop export` çıktı biçimi wiki'de belgeleniyor, bu taramada `lib/download.ps1` ve `libexec/scoop-export.ps1` içerikleri okunmadı — davranış dosya adlarından ve README'den çıkarıldı. VirusTotal entegrasyonunun kapsamı (`libexec/scoop-virustotal.ps1`) yalnız dosya adından biliniyor.
- 538 açık issue'nun ne kadarının ilişkilendirme/shim kaynaklı olduğu `doğrulanamadı`.
