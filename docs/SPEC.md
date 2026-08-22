# Runly — Teknik Şartname (SPEC)

> Bu dosya projenin tek doğruluk kaynağıdır. Her görev paketi (docs/tasks/T*.md) bu dosyayı
> okumuş olduğunu varsayar. Bir çelişki görürsen SPEC kazanır; SPEC eksikse görev paketinde
> belirtilen sahibine sor, kendi kafana göre genişletme.

## 1. Problem

Windows'ta `.js`, `.ps1`, `.py` gibi script dosyaları çift tıkla çalışmaz:
- `.js` varsayılan olarak `WScript.exe`'ye bağlıdır (Node değil, eski ve tehlikeli WSH motoru).
- `.ps1` çift tıkta metin editöründe açılır (Microsoft'un bilinçli güvenlik kararı).
- `.py`, `.rb`, `.lua` vb. için hiçbir ilişki yoktur.

Runly, bu uzantıları kendi üzerine alan küçük bir launcher'dır: doğru yorumlayıcıyı bulur,
çalıştırır, hata durumunda pencereyi açık tutar ve çalıştırmadan önce bir güvenlik kapısından
geçirir.

## 2. Hedef makine (ölçüldü, 2026-08-09)

| Bileşen | Durum |
|---|---|
| OS | Windows 11 Pro 10.0.22631, x64 |
| node | `C:\Program Files\nodejs\node.exe` |
| python / py | 3.14.2 (`%LOCALAPPDATA%\Microsoft\WindowsApps`) |
| powershell | 5.1 (`System32\WindowsPowerShell\v1.0\powershell.exe`) — pwsh 7 YOK |
| ruby/perl/lua/deno/bun | YOK |
| .NET | ✅ SDK **8.0.423** kurulu (T0'da `dotnet-install.ps1` ile — winget MSI 0x80070643 verdi) |
| MSVC | ✅ VS 2022 BuildTools + VCTools kurulu, AOT publish doğrulandı |
| NuGet | ✅ `nuget.org` kaynağı eklendi (başta hiç kaynak yoktu) |
| vswhere | ILCompiler bulamadı → T0'da `C:\Program Files\dotnet\vswhere.exe`'ye kopyalandı; ayrıca `...\Visual Studio\Installer` kullanıcı PATH'ine eklendi. Kopya bayatlarsa PATH'teki asıl dosya devreye girer. |
| ExecutionPolicy (CurrentUser) | `RemoteSigned` |
| `.js` | `JSFile` → `WScript.exe "%1" %*` |
| `.ps1` | `UserChoice` = `AppXxf01pj590w7z9mxmyv3nx0a9ewj3e51g` (Store Not Defteri) |
| `.py`, `.vbs` | UserChoice yok |

**Kritik çıkarım:** `.ps1` için `UserChoice` anahtarı zaten var. Bu anahtar hash korumalıdır ve
programatik olarak ezilemez. `.ps1`'i Runly'ye bağlamanın tek meşru yolu, kullanıcının
"Birlikte aç → Runly → Her zaman bu uygulamayı kullan" akışını tamamlamasıdır. Uygulama bunu
tespit edip kullanıcıyı bu diyaloğa yönlendirmek zorundadır. UserChoice hash'ini kırmaya
çalışmak **yasaktır**.

## 3. Teknoloji kararları (tartışmaya kapalı)

- **Dil/platform:** C# / .NET 8.
- **`Runly.exe` (launcher):** Console subsystem, `PublishAot=true`, `InvariantGlobalization=true`,
  tek dosya, ~2-4 MB, runtime bağımlılığı yok. Açılış gecikmesi hedefi **< 50 ms**.
  AOT olduğu için: WinForms/WPF kullanılamaz, `System.Text.Json` **source-generated context**
  ile kullanılır (reflection tabanlı serileştirme AOT'de kırılır), `Assembly.Load`/reflection yok.
- **`RunlySettings.exe` (GUI):** WinForms, `win-x64`, self-contained, **trim kapalı**
  (WinForms trim/AOT desteklemez). Boyut önemsiz.
- **`Runly.Core` (classlib):** İkisinin ortak mantığı. AOT-uyumlu yazılmak zorunda.
- **Testler:** xUnit, `Runly.Core` üzerinde. GUI ve registry testleri manuel.
- **Hiçbir NuGet paketi eklenmeyecek** (xUnit test bağımlılıkları hariç). Win32 çağrıları
  `[DllImport]`/`LibraryImport` ile elle yazılır.

## 4. Çözüm yapısı

```
C:\Users\Administrator\Desktop\Projeler\Runly\
├─ Runly.sln
├─ src\
│  ├─ Runly.Core\            # sözleşmeler + mantık (AOT-safe)
│  ├─ Runly.Launcher\        # Runly.exe  (AOT console)
│  └─ Runly.Settings\        # RunlySettings.exe (WinForms)
├─ tests\
│  └─ Runly.Core.Tests\      # xUnit
├─ assets\                   # ikonlar
├─ scripts\                  # build.ps1, install.ps1, uninstall.ps1
├─ samples\                  # test için örnek scriptler
└─ docs\
   ├─ SPEC.md                # bu dosya
   └─ tasks\T0..T7.md
```

## 5. Çalışma zamanı verisi

Konum: `%APPDATA%\Runly\`

```
%APPDATA%\Runly\
├─ config.json          # kullanıcı ayarları + uzantı eşlemeleri
├─ trust.json           # güvenilen klasörler ve dosya parmak izleri
├─ runly.log            # son 1 MB, döngüsel
└─ backups\
   └─ assoc-20260809-141230.reg   # kurulumdan önceki registry yedeği
```

### 5.1 config.json şeması

```jsonc
{
  "version": 1,
  "securityMode": "TrustOnFirstUse",   // AlwaysAsk | TrustOnFirstUse | NeverAsk
  "keepWindowOpen": "OnError",         // Always | OnError | Never
  "editorCommand": "code",             // "Düzenle" fiili için; boş ise notepad
  "logEnabled": true,
  "extensions": {
    ".js":  { "interpreter": "node",       "args": "\"{script}\" {args}", "enabled": true,  "icon": "js.ico" },
    ".mjs": { "interpreter": "node",       "args": "\"{script}\" {args}", "enabled": true },
    ".cjs": { "interpreter": "node",       "args": "\"{script}\" {args}", "enabled": true },
    ".ts":  { "interpreter": "node",       "args": "--experimental-strip-types \"{script}\" {args}", "enabled": false },
    ".ps1": { "interpreter": "powershell", "args": "-NoLogo -ExecutionPolicy Bypass -File \"{script}\" {args}", "enabled": true },
    ".py":  { "interpreter": "py",         "args": "\"{script}\" {args}", "enabled": true },
    ".pyw": { "interpreter": "pyw",        "args": "\"{script}\" {args}", "enabled": false },
    ".rb":  { "interpreter": "ruby",       "args": "\"{script}\" {args}", "enabled": false },
    ".pl":  { "interpreter": "perl",       "args": "\"{script}\" {args}", "enabled": false },
    ".lua": { "interpreter": "lua",        "args": "\"{script}\" {args}", "enabled": false },
    ".php": { "interpreter": "php",        "args": "\"{script}\" {args}", "enabled": false },
    ".sh":  { "interpreter": "bash",       "args": "\"{script}\" {args}", "enabled": false },
    ".r":   { "interpreter": "Rscript",    "args": "\"{script}\" {args}", "enabled": false },
    ".jar": { "interpreter": "java",       "args": "-jar \"{script}\" {args}", "enabled": false }
  }
}
```

- `interpreter`: PATH'te aranacak isim **veya** tam yol. PATH'te bulunamazsa uzantı GUI'de
  "yorumlayıcı bulunamadı" diye gri gösterilir ve kurulumda atlanır.
- Şablon değişkenleri: `{script}` (tam yol), `{args}` (kullanıcı argümanları), `{dir}` (script klasörü).
- Config yoksa veya bozuksa: gömülü varsayılanlar kullanılır, bozuk dosya `.bak` olarak yeniden
  adlandırılır ve yenisi yazılır. **Asla çökme.**

### 5.2 trust.json şeması

```jsonc
{
  "version": 1,
  "trustedFolders": ["C:\\Users\\Administrator\\Desktop\\Projeler"],
  "trustedFiles": {
    "C:\\path\\to\\script.js": { "sha256": "ab12...", "addedUtc": "2026-08-09T12:00:00Z" }
  }
}
```

## 6. Güvenlik kapısı (projenin kalbi)

Bu özelliği zayıflatan hiçbir "kolaylık" kabul edilmez. Sıra:

1. **MOTW kontrolü:** Dosyanın `:Zone.Identifier` alternate data stream'i var mı ve `ZoneId>=3` mü?
   → **Her zaman** kırmızı uyarı diyaloğu. `securityMode` ne olursa olsun atlanamaz.
   Seçenekler: `Yine de çalıştır` / `Önce kodu göster` / `İptal`.
   Ek onay kutusu: "İnternet işaretini kaldır" (işaretlenirse ADS silinir).
2. **Yol kontrolü:** Dosya `trustedFolders` altındaysa → sessiz çalıştır.
3. **Parmak izi kontrolü:** `trustedFiles`'ta ve SHA-256 eşleşiyorsa → sessiz çalıştır.
   Hash eşleşmiyorsa (dosya değişmiş) → "Bu dosya son onaydan sonra değişti" diyaloğu.
4. **securityMode:**
   - `AlwaysAsk` → her seferinde sor.
   - `TrustOnFirstUse` (varsayılan) → sor; onay diyaloğunda "Bu dosyaya her zaman güven"
     ve "Bu klasöre her zaman güven" kutuları.
   - `NeverAsk` → sadece adım 1 uygulanır. GUI'de bu seçenek kırmızı uyarıyla ve ayrı bir
     onay diyaloğuyla seçtirilir.
5. **`-ExecutionPolicy Bypass` sadece kapıdan geçmiş `.ps1`'lere uygulanır.** Sistem genelinde
   ExecutionPolicy **asla** değiştirilmez.

**Diyalog teknolojisi:** `comctl32.dll` → `TaskDialogIndirect` P/Invoke. AOT-uyumlu, native görünür,
WinForms gerektirmez. Uygulama manifestinde `comctl32 v6` bağımlılığı olmalı, yoksa API başarısız olur.
Diyalog gövdesi şunu göstermeli: dosya adı, tam yol, uzantı, çalışacak **tam komut satırı**,
dosya boyutu ve değiştirilme tarihi. "Önce kodu göster" ilk 100 satırı salt-okunur gösterir.

## 7. Launcher CLI sözleşmesi

```
Runly.exe [--verb <run|runas|edit|prompt-args>] [--no-wait] <script-path> [script args...]
```

- `--verb run` (varsayılan): normal çalıştır.
- `--verb runas`: yükseltilmiş çalıştır (`ShellExecute` + `runas`). Kullanıcıya UAC çıkar.
- `--verb edit`: `editorCommand` ile aç, güvenlik kapısını atla (çalıştırma yok).
- `--verb prompt-args`: önce argüman soran bir TaskDialog input'u göster, sonra çalıştır.
- `--no-wait`: `keepWindowOpen` ayarını bu çalıştırma için `Never` yap.
- Argüman yoksa veya dosya yoksa: kısa kullanım metni göster, exit 2.

**Çıkış kodları:** 0 başarılı · 1 script hata verdi (child'ın exit code'u aynen yansıtılır,
0 ve 1 dışındaysa olduğu gibi) · 2 Runly kullanım hatası · 3 yorumlayıcı bulunamadı ·
4 kullanıcı güvenlik kapısında iptal etti.

**Çalıştırma davranışı:**
- `WorkingDirectory` = script'in bulunduğu klasör.
- stdout/stderr **yeniden yönlendirilmez** — child aynı konsolu kullanır (renkler, ilerleme
  çubukları, interaktif input çalışsın diye). Bu yüzden `UseShellExecute=false` + redirect YOK.
- Bitince `keepWindowOpen` kuralına göre: `Always` → hep bekle, `OnError` → exit code != 0 ise bekle,
  `Never` → hiç bekleme. Beklerken şunu yaz:
  `--- Çıkış kodu: {code} ({süre} sn) — kapatmak için bir tuşa basın ---`

## 8. Yorumlayıcı çözümleme sırası

1. Dosyanın ilk satırı `#!` ile başlıyorsa shebang'i ayrıştır
   (`#!/usr/bin/env node` → `node`, `#!/usr/bin/python3` → `python`).
   Elde edilen isim PATH'te bulunabiliyorsa onu kullan.
2. `config.json`'daki uzantı eşlemesi.
3. Hiçbiri yoksa → exit 3, "`.xyz` için yorumlayıcı ayarlı değil, Ayarlar'ı açmak ister misiniz?" diyaloğu.

PATH araması `where.exe` çağırmadan yapılmalı: `PATH` + `PATHEXT` üzerinden manuel tarama
(hız için). Sonuç `%LOCALAPPDATA%\Runly\ipcache.json`'da 24 saat cache'lenir.

**Python tuzağı (K9 ile açıldı, K28 ile kapandı):** `%LOCALAPPDATA%\Microsoft\WindowsApps\*.exe`
app-execution alias'ları 0 bayt görünür. Bir kısmı Store'u açan ölü yönlendirici, bir kısmı çalışan
gerçek alias. K9 bunların **boyutla ayırt edilemediğini** söylüyordu ve bu doğruydu — ama boyut tek
bilgi değil. Bu dosyalar birer **reparse point**: `FSCTL_GET_REPARSE_POINT` ile etiket okunur,
`IO_REPARSE_TAG_APPEXECLINK` ise yükün içindeki hedef yol çözülür. Hedef bir `*Redirector.exe` ise
ölü takozdur ve atlanır; değilse çalışan alias'tır ve doğrudan kabul edilir. Reparse hiç okunamazsa
K9'un eski davranışına düşülür: 0 baytlık aday son çare olarak saklanır.

Kural yine de şu: gerçek bir exe varsa her zaman o tercih edilir. Bu makinede `py.exe` 0 bayttır ve
çalışır; `py` launcher'ı `python`'a tercih edilir.

## 9. Shell entegrasyonu

Hepsi **HKCU** altında — yönetici hakkı gerekmez.

**ProgID (uzantı başına):**
```
HKCU\Software\Classes\Runly.Script.js\
   (default)                  = "JavaScript Script (Runly)"
   DefaultIcon                = "<kurulum>\assets\js.ico,0"
   shell\open\command         = "<kurulum>\Runly.exe" "%1" %*
   shell\runas\command        = "<kurulum>\Runly.exe" --verb runas "%1" %*
   shell\edit\command         = "<kurulum>\Runly.exe" --verb edit "%1"
   shell\runlyargs\           = MUIVerb "Runly ile argümanlarla çalıştır…"
   shell\runlyargs\command    = "<kurulum>\Runly.exe" --verb prompt-args "%1"
```

**Uzantı bağlama:**
```
HKCU\Software\Classes\.js\(default)              = "Runly.Script.js"
HKCU\Software\Classes\.js\OpenWithProgids\Runly.Script.js = ""   (REG_SZ, boş)
```

**Uygulama kaydı (Birlikte aç listesinde görünmek için — .ps1 akışı buna bağlı):**
```
HKCU\Software\Classes\Applications\Runly.exe\
   FriendlyAppName            = "Runly"
   shell\open\command         = "<kurulum>\Runly.exe" "%1" %*
   SupportedTypes\.js         = ""
   SupportedTypes\.ps1        = ""   ... (etkin tüm uzantılar)
HKCU\Software\RegisteredApplications\Runly = "Software\Runly\Capabilities"
HKCU\Software\Runly\Capabilities\
   ApplicationName            = "Runly"
   ApplicationDescription     = "Script dosyalarını çift tıkla çalıştırır"
   FileAssociations\.js       = "Runly.Script.js"  ... (etkin tüm uzantılar)
```

**Kurulum akışı:**
1. Yedek al: değiştirilecek her anahtarın mevcut hali `backups\assoc-<ts>.reg` dosyasına yazılır
   (`reg export` ile değil, elle üretilen geçerli `.reg` metni ile — silinmiş anahtarlar için de
   `[-HKEY...]` satırları içermeli).
2. ProgID'leri ve Applications kaydını yaz.
3. Her uzantı için `UserChoice` var mı bak — **karar K19 ile değişti, aşağısı nihai hâlidir:**
   - `UserChoice` **bizim ProgID'mizi** gösteriyorsa → `Bound` ✅. **Tek `Bound` koşulu budur.**
   - `UserChoice` **yoksa** → `.ext` default'unu ProgID'ye yaz (zararsız), **ama sonucu
     `NeedsUserChoice` raporla.** "Anında çalışır ✅" iddiası YANLIŞTI: Windows 11'de
     `OpenWithProgids` altında başka aday varsa (ör. `.js` → `AntigravityIDE.js`) çift tıkta
     seçici çıkar. T7 bunu ölçtü.
   - `UserChoice` **başkasını** gösteriyorsa → `.ext` default'una dokunma, `NeedsUserChoice`.
   - `NeedsUserChoice` olan her uzantı için kullanıcıya "Windows onayı gerekiyor" satırı göster
     ve tek tıkla `SHOpenWithDialog`'u aç. Kullanıcı Runly'yi seçip "Her zaman"ı işaretler.
     **Bu akış istisna değil, normal kurulum adımıdır** — GUI onu böyle sunmalıdır.
4. `SHChangeNotify(SHCNE_ASSOCCHANGED, ...)` çağır ki Explorer ikonları tazelesin.

**Kaldırma:** yedek `.reg`'i uygula + Runly'nin yazdığı tüm anahtarları sil + `SHChangeNotify`.
Kaldırma sonrası `.js`'in tekrar `WScript.exe`'ye dönmemesi için kullanıcıya "eski hâline
döndür / boş bırak" seçeneği sunulur (varsayılan: boş bırak — WScript zaten kötü bir varsayılandı).

## 10. Ayarlar GUI (RunlySettings.exe)

Tek pencere, ~900x600, sekmesiz, üstten aşağı:

1. **Durum şeridi:** "Runly kurulu / kurulu değil", kurulum yolu, sürüm.
2. **Uzantı tablosu:** kolonlar → `Etkin (checkbox)` · `Uzantı` · `Yorumlayıcı` · `Bulundu mu (✓/✗ + yol)`
   · `Argümanlar` · `Durum`. Durum kolonu: `Bağlı ✅` / `Windows onayı gerekiyor ⚠` / `Bağlı değil`.
   `⚠` satırındaki butona basınca `SHOpenWithDialog` açılır.
3. **Güvenlik paneli:** securityMode radio grubu (NeverAsk seçilince kırmızı onay diyaloğu),
   güvenilen klasörler listesi (ekle/çıkar), güvenilen dosyalar listesi (temizle).
4. **Davranış:** keepWindowOpen radio, editorCommand text, log aç/kapa, "Log klasörünü aç".
5. **Alt bar:** `Kur / Güncelle` · `Kaldır` · `Yedeği geri yükle` · `Kaydet`.

Kurulum/kaldırma işlemleri sonunda ne yapıldığını satır satır listeleyen bir sonuç diyaloğu göster.

## 11. Kalite kuralları

- Tüm kullanıcıya görünen metinler **Türkçe**. Kod, sınıf/değişken isimleri, commit mesajları
  ve yorumlar **İngilizce**.
- Nullable reference types açık (`<Nullable>enable</Nullable>`), warnings-as-errors.
- `Runly.Core`'da hiçbir `Console.WriteLine` ve hiçbir UI çağrısı olmayacak — saf mantık,
  test edilebilir. UI/IO sınır sınıfları arayüz arkasında (`IFileSystem`, `IDialogService`).
- Her public tip için tek satırlık XML doc.
- Exception yutulmaz; `Runly.Core` throw eder, launcher/GUI yakalayıp kullanıcıya gösterir.
- Log formatı: `2026-08-09T14:12:30.123Z [INFO] mesaj`.

## 11.1 Karar günlüğü (yönetici kararları — bağlayıcı)

Paketler arası çelişkiler burada çözülür. Bir paket dosyası bu bölümle çelişirse **bu bölüm kazanır.**

| # | Konu | Karar | Tarih |
|---|---|---|---|
| K1 | Uzantı sayısı | **14** uzantı doğrudur (§5.1 tablosu). T1.md'deki "15" yazım hatasıydı, düzeltildi. | 2026-08-09 |
| K2 | `ipcache.json` konumu | **`%LOCALAPPDATA%\Runly\ipcache.json`** (§8). T2.md'deki `%APPDATA%` yanlıştı, düzeltildi. Gerekçe: makineye özgü, gezici profille taşınmamalı. | 2026-08-09 |
| K3 | `IShellRegistrar.Uninstall` imzası | `Uninstall(UninstallOptions? options = null)` **onaylandı**. T1'in çözümü doğru. | 2026-08-09 |
| K4 | Shebang `python3` → `python` fallback | **Resolver'ın işi, inspector'ın değil.** `IScriptInspector` PATH'e bakmaz, sadece ham ismi ayrıştırıp `ShebangInterpreter`'a yazar. `IInterpreterResolver` fallback zincirini yürütür. Gerekçe: inspector saf ve testte IO'suz kalsın. | 2026-08-09 |
| K5 | "Kodu göster" mekanizması | **TaskDialog'un genişletilebilir alanı** kullanılacak, ayrı diyalog turu yok. `SecurityDecisionReason.CodeRequested` sözleşmede kalır ama T3 onu döndürmez. | 2026-08-09 |
| K6 | `ExtensionStatus.UserChoiceOwnerName` | **Eklenmesi onaylandı** — `string? UserChoiceOwnerName { get; init; }`. Bu, T1 modeline yapılan tek yetkili eklemedir; T4 ekleyecek. Gerekçe: §10.2'deki turuncu satır "Windows bu uzantıyı *Not Defteri*'ne bağlamış" diyebilmeli; isim olmadan mesaj soyut kalıyor. | 2026-08-09 |
| K7 | `editorCommand` boşsa | `notepad.exe`'ye düş. Sabit T3'te (`Ui/EditorLauncher.cs`), Core'a konmayacak. | 2026-08-09 |
| K9 | 0-byte aday (Store alias) | **Kural değişti.** §8'in "0 bayt adayı atla" kuralı mutlak değil: gerçek bir exe her zaman tercih edilir, **ama hiç bulunamazsa 0 baytlık aday son çare olarak kabul edilir.** Gerekçe: bu makinede `py.exe` **tek** aday ve 0 bayt; kural harfiyen uygulanınca `.py` hiç bağlanamıyordu. Store install stub'ı ile çalışan app-execution alias'ı bayt boyutuyla ayırt edilemiyor. Yönetici tarafından `PathSearcher.cs`'te düzeltildi + 2 test. | 2026-08-09 |
| K28 | Store takozunu boyutla ayırt etme | **K9 kapandı.** 0 bayt sezgisi yerine reparse point okunuyor: `FSCTL_GET_REPARSE_POINT` + `IO_REPARSE_TAG_APPEXECLINK`, hedef `*Redirector.exe` ise ölü takoz sayılıp atlanıyor, değilse alias doğrudan kabul ediliyor. Reparse okunamazsa K9'un son-çare davranışına düşülüyor. `uv` ve CPython `launcher2.c` aynı yolu kullanıyor. Yönlendirici kararı yalnız hedef adına bağlı — paket ailesine bağlamak `winget.exe`'yi yanlışlıkla eliyordu. | 2026-08-22 |
| K10 | Shebang + config eşlemesi çakışması | **Shebang config'i tamamen bypass eder**, argüman şablonu `"{script}" {args}` olur. T2'nin seçimi onaylandı. Gerekçe: shebang dosyanın kendi beyanıdır ve `.ts` gibi bir uzantının bayrakları başka bir yorumlayıcıya taşınırsa anlamsız/tehlikeli olur. | 2026-08-09 |
| K11 | `TrustFile`/`TrustFolder` diske yazmaz | Onaylandı — bellekte mutasyon, çağıran `Save()` eder. T3 kullanıcı onayından **sonra** açıkça `trustStore.Save()` çağırmalı; unutulursa güven kalıcı olmaz. | 2026-08-09 |
| K12 | Yedekteki silme satırı kapsamı | T4'ün daraltması **onaylandı**: paylaşılan anahtarlara (`.ext`, `RegisteredApplications`) silme satırı yazılmaz, sadece dışa aktarılır. Gerekçe: 71 uygulamanın kaydını tutan paylaşılan bir anahtarı "sil + yeniden yaz" yapmak kabul edilemez risk. Kozmetik bedeli (yedek tek başına geri yüklenirse Runly "Birlikte aç" listesinde kalır) kabul edildi. | 2026-08-09 |
| K13 | ProgID adları + fiil etiketleri Türkçe | Onaylandı. §9'daki İngilizce dizeler şablon örneğiydi; §11 bağlayıcı. `open`/`runas`/`edit` fiillerine de `MUIVerb` eklenmesi doğru. | 2026-08-09 |
| K14 | `ListBackups` / `OpenWithDialog` arayüz dışında | Onaylandı, `IShellRegistrar` genişletilmeyecek. T5 bu iki sınıfı doğrudan kullanır. | 2026-08-09 |
| K15 | `prompt-args` sırası | **Argüman kutusu güvenlik kapısından ÖNCE gelir.** T3'ün sapması onaylandı, T3.md'nin sırası hatalıydı. Gerekçe: aksi hâlde güvenlik diyaloğu argümansız komut satırını gösterir, kullanıcı gerçekte çalışacak olandan farklı bir şeyi onaylar — §6'nın "çalışacak tam komut satırı" şartının ihlali. Argüman kutusu iptali → exit 4. Editör açılamazsa → exit 1 (yeni kod tanımlanmadı). | 2026-08-09 |
| K16 | `.cmd` / `.bat` yorumlayıcılar | **Eksik, T7 düzeltecek.** `ProcessLauncher` `UseShellExecute=false` kullandığı için batch dosyası başlatamaz (Win32 kısıtı) ve kullanıcıya yanıltıcı "yorumlayıcı bulunamadı" (exit 3) döner. Kullanıcı `interpreter` olarak `tsx`, `deno`, npm/pnpm shim'i gibi bir `.cmd` yazarsa bu tetiklenir. Çözüm: hedef `.cmd`/`.bat` ise `cmd.exe /c "hedef" ...` üzerinden başlat, argüman kaçışına dikkat et. Test şart. | 2026-08-09 |
| K17 | **`UserChoice`'ın kanonik yolu** | T5, `HKCU\Software\Classes\<ext>\UserChoice`'a bakıp bulamayınca "yedekleme UserChoice'ı kaybediyor" raporladı. **Bu bir hata değildi — UserChoice o yolda hiç bulunmaz.** Tek geçerli yol: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\<ext>\UserChoice`. Yönetici doğruladı: `.ps1` → `ProgId=AppXxf01pj590w7z9mxmyv3nx0a9ewj3e51g`, `Hash=W9Q+SZXWATg=` — proje başındaki değerle birebir aynı, kayıp yok. `UserChoiceInspector.cs` zaten doğru yolu okuyor. **`RegistryBackup`'ta düzeltilecek bir şey yok.** Runly bu anahtara hiç yazmadığı için yedeklemesine de gerek yok. Doğrulama komutu: `Get-ItemProperty "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ps1\UserChoice"`. | 2026-08-09 |
| K18 | İkonlar gerçek ICO olmalı | T6 `make-icons.ps1`'de PNG'yi `.ico` adıyla **kopyalıyordu** (imza `89504E47`). Bu ICO değildir; Explorer `DefaultIcon`'u render etmez, yani T4'ün ikon kaydı sessizce boşa çıkardı. Rapor bunu sapma saymamıştı. Yönetici düzeltti: script artık `ICONDIR` başlıklı, 16/32/48/256 kareli gerçek ICO üretiyor (imza `00000100`, 4 kare, `System.Drawing.Icon` ile yüklenebiliyor) ve ara PNG'leri siliyor. Ayrıca `ApplicationIcon` her iki csproj'a eklendi — T6'ya "src/'ye dokunma" dediğim için o satırı yazamamıştı, kapsam hatası bendeydi. | 2026-08-09 |
| K19 | **§9 adım 3 varsayımı yanlıştı** | "UserChoice yoksa `.ext` varsayılanını yaz, anında çalışır" **Windows 11'de doğru değil.** T7 ölçtü: kurulum "bağlandı ✅" dedi, çift tık seçici gösterdi. Kök neden: `.js`'in `OpenWithProgids`'inde zaten `AntigravityIDE.js` vardı; ikinci aday eklenince `UserChoice` yokluğunda Windows seçim yapmıyor, kullanıcıya soruyor. **Yeni kural:** `Install` hiçbir uzantıyı, etkin ilişkiyi **doğrulamadan** `Bound` sayamaz. Doğrulama: `FileExts\<ext>\UserChoice\ProgId` bizim ProgID'imiz mi. Değilse durum `NeedsUserChoice`'tır. Pratikte neredeyse her uzantı bu duruma düşer — "Birlikte aç → Her zaman" akışı **istisna değil, normal kurulum adımıdır** ve GUI bunu böyle sunmalıdır. SPEC §9 adım 3 bu doğrultuda geçersizdir. | 2026-08-09 |
| K20 | Kaldırmada öksüz `UserChoice` | `UserChoice` bizim ProgID'mizi gösterirken kaldırma yapılırsa uzantı **silinmiş bir handler'a** bağlı kalıyor (B2) ve Windows anahtarın silinmesini ACL ile engelliyor. **Kural:** kaldırma bunu tespit etmek, hangi uzantıların etkilendiğini **açıkça listelemek** ve kullanıcıyı düzeltmeye yönlendirmek zorunda (`SHOpenWithDialog` veya `ms-settings:defaultapps`). Öksüz kayıt varken "temiz kaldırıldı" denmesi yasak. | 2026-08-09 |
| K21 | Junction ile güven atlatma (B3) | `TrustMatching` reparse point çözmüyor; güvenilen klasör içindeki bir junction dışarıyı işaret ederse hedef dosya güvenilir sayılıyor. Önem derecesi **düşük** (saldırganın zaten güvenilen klasöre yazma hakkı olması gerekir) ama düzeltmesi ucuz. `File.ResolveLinkTarget` / `GetFinalPathNameByHandle` ile normalize edilecek + test. | 2026-08-09 |
| K22 | **Konteyner tuzağı — ölçüm kuralı** | Ajan oturumlarının `PowerShell`/`Bash` araçları MSIX konteynerinde çalışabilir; `HKCU` ve `%APPDATA%` **yazmaları sanallaştırılır**, okumalar gerçekle karışır. Bu yüzden konteyner içinden yapılan kurulum "başarılı" görünüp gerçek registry'ye hiç dokunmayabilir, silinen anahtarlar hâlâ "var" görünebilir. **Kural:** kurulum/kaldırma ve registry doğrulaması **Explorer üzerinden** yapılacak (GUI'yi Explorer'dan başlat, dökümü Explorer'dan çift tıklanan bir script'e aldır). Yönetici de dahil kimse konteyner içi registry okumasına dayanarak "temiz/kirli" hükmü vermeyecek. | 2026-08-09 |
| K23 | **`SHOpenWithDialog` varsayılan bağlayamıyor** | R1 ekranda ölçtü: Windows 11'de `SHOpenWithDialog` penceresinde yalnızca **"Yalnızca bir kez"** düğmesi çıkıyor, "Her zaman" yok. `OAIF_FORCE_REGISTRATION` eklenince Windows doğrudan reddediyor ("Ayarlar > Varsayılan uygulamalar'a gidin"). **Çalışan tek yol:** Explorer'da sağ tık → Birlikte aç → **Başka bir uygulama seç** → Runly → Her zaman. SPEC §9'un "tek tıkla `SHOpenWithDialog` aç, kullanıcı Her zaman'ı işaretler" cümlesi **geçersizdir.** GUI bunu dürüstçe anlatacak ve kullanıcıyı Ayarlar/Explorer yoluna yönlendirecek — R1 bunu yaptı. Ürün sonucu: "tek tıkla bağla" vaadi Windows 11'de teknik olarak yok; kurulum kaçınılmaz olarak uzantı başına elle bir adım içeriyor. | 2026-08-11 |
| K24 | **Geri yükleme doğrulaması oturum SONUNDA olacak** | R1 makineyi geri yükleyip döküm aldı (11:56), sonra teste devam edip tekrar kurdu ve bir daha döküm almadı; oturum sonunda registry'de 5 ProgID, `Applications\Runly.exe`, `Software\Runly`, `RegisteredApplications\Runly` ve `.ext` varsayılanları **kalmıştı** — hepsi silinmiş bir exe'yi gösteriyordu. Rapor "tek fark Hash" diyordu, döküm anı itibarıyla doğruydu, teslim anı itibarıyla değil. **Kural:** geri yükleme dökümü paketin **en son işlemi** olacak; dökümden sonra kurulum/kaldırma yapılmayacak. Rapor dökümün saatini ve "bu dökümden sonra hiçbir işlem yapılmadı" beyanını içerecek. Yönetici temizliği Registry MCP ile yaptı; iki bağımsız okuma yolu doğruladı. | 2026-08-11 |
| K25 | Registry MCP gerçek registry'yi görüyor | `mcp__Windows-MCP__Registry` konteyner dışını okuyor/yazıyor (kanıt: R1'in en son yazdığı `.js` `UserChoice` `Hash = ACLO94VoMj0=` değerini görüyor). K22 hâlâ geçerli — ama artık doğrulama için Explorer script'ine ek olarak bu araç da kullanılabilir. Sınırı: `(default)` değerini **silemiyor**, yalnızca boş dizeye ayarlayabiliyor. | 2026-08-11 |
| K26 | Kaldırmada `UserChoice` **geri yüklenmez, silinir** | R4 sordu: kaldırırken `.js`'in eski `ProgId`'si (`JSFile`) geri yazılabilir mi? **Hayır ve denenmeyecek.** Windows `UserChoice`'ı `Hash` ile doğrular; geçerli hash olmadan yazılan `ProgId` bozuk bir kayıt üretir ve Windows bunu kurcalama sayıp yok sayar — silmekten **daha kötü** bir sonuç. Hash üretmek §2 gereği yasak. Dolayısıyla mevcut davranış (kendi yazdığımız `UserChoice`'ı silmek) doğrudur ve kaldırma diyaloğunda zaten beyan ediliyor (".js eski WScript davranışına dönmez"). Bulgu kapatıldı, paket açılmadı. **Gelecek iyileştirme (engelleyici değil):** kurulum yedeği önceki handler'ı biliyor; kaldırma diyaloğu "eskiden X ile açılıyordu" diyebilir. | 2026-08-11 |
| K27 | Varsayılan uygulama deep link'i | Dış araştırma (GPT) `ms-settings:defaultapps?registeredAppUser=Runly` deep link'ini önerdi. **Kabul edildi — ekleme olarak.** Windows 11'de destekleniyor, uygulamanın `RegisteredApplications` + `Capabilities` kaydını gerektiriyor (Runly bunu zaten yazıyor) ve genel ayar ekranına göre kullanıcıyı doğrudan Runly sayfasına götürüyor. **Reddedilen kısım:** aynı araştırma "Explorer'ın Birlikte aç akışını kaldırın, varsayılan belirleyemiyor" diyor — bu **yanlış**; `SHOpenWithDialog` **API'si** ile Explorer'ın **kendi** "Başka bir uygulama seç → Her zaman" akışı karıştırılmış. İkincisi R1 ve R3'te iki kez ölçüldü ve `UserChoice`'ı gerçekten yazdı (K23). O akış korunacak; deep link hızlı yol, Explorer akışı kanıtlanmış geri düşüş. **Şart:** deep link'in bu makinede kaç tık gerektirdiği **ölçülmeden** arayüzde "tek tıkla" benzeri bir cümle yazılamaz (B1'in tekrarı olur). | 2026-08-13 |
| K8 | Publish dosya kilidi | Gerçek (T1'de görüldü, Defender kaynaklı). T6 `build.ps1` publish adımına **3 denemeye kadar retry** koyacak (500 ms bekleme). | 2026-08-09 |

## 12. Kabul senaryoları (T7'de bunlar tek tek denenecek)

| # | Senaryo | Beklenen |
|---|---|---|
| 1 | `samples\hello.js`'e çift tık | Güvenlik diyaloğu → onay → çıktı görünür, pencere açık kalmaz |
| 2 | `samples\fail.js`'e çift tık (exit 1) | Pencere açık kalır, çıkış kodu satırı görünür |
| 3 | Aynı dosyaya 2. çift tık | Diyalog çıkmaz (TrustOnFirstUse), doğrudan çalışır |
| 4 | Dosya içeriği değişince 3. çift tık | "Dosya değişti" diyaloğu çıkar |
| 5 | İnternetten indirilmiş `.js` (MOTW'lu) | Kırmızı diyalog, `NeverAsk` modunda bile |
| 6 | `samples\hello.ps1` — Birlikte aç akışıyla bağlandıktan sonra | Çalışır, ExecutionPolicy hatası vermez |
| 7 | `samples\hello.py` çift tık | Çalışır (`py` üzerinden) |
| 8 | Shebang'li `.txt` uzantısız dosya sağ tık → Runly ile çalıştır | Shebang'e göre doğru yorumlayıcı |
| 9 | Sağ tık → Düzenle | VS Code'da açılır, çalışmaz |
| 10 | Sağ tık → Argümanlarla çalıştır | Argüman kutusu, girilen argümanlar script'e ulaşır |
| 11 | Yorumlayıcısı olmayan `.rb` | Exit 3, açıklayıcı diyalog |
| 12 | Kaldır → sonra `.js` çift tık | Runly çalışmaz, sistem eski davranışına döner |
| 13 | `config.json` elle bozulur | Uygulama çöker mi? Hayır — `.bak` alır, varsayılana döner |
| 14 | Boşluklu/Türkçe karakterli yol (`C:\Test klasörü\çalış.js`) | Sorunsuz çalışır |
| 15 | Soğuk başlangıç süresi | `Runly.exe` → child process başlangıcı < 50 ms |
