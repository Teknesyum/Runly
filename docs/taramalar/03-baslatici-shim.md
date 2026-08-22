# 03 — Başlatıcı/shim ikilileri ve yorumlayıcı çözümleme

Tarama tarihi: 2026-08-22. Tüm rakamlar `gh api repos/<owner>/<repo>` ve
`gh api repos/<owner>/<repo>/releases/latest` çıktısından. Kod kopyalanmadı; alınan şey
desen, sınır ve hata.

## Depo sağlığı (birincil kaynak: GitHub API, 2026-08-22)

| Depo | Lisans | Son push | Son etiketli sürüm | Yıldız | Açık issue |
|---|---|---|---|---|---|
| ScoopInstaller/Shim | MIT | 2026-08-04 | `cpp/v0.1.1` · 2026-07-20 | 116 | 4 |
| 71/scoop-better-shimexe | MIT OR Unlicense | 2023-06-19 | `1.2` · 2019-09-07 | 86 | 6 |
| chocolatey/shimgen | yok (repo `null`); `shim/` altı **Ms-RSL** | 2022-02-24 | sürüm yok | 104 | 0 |
| gerardog/gsudo | MIT | 2026-08-12 | `v2.6.1` · 2025-10-06 | 6.029 | 50 |
| astral-sh/uv | Apache-2.0 | 2026-08-22 | `0.12.5` · 2026-08-14 | 88.967 | 2.849 |
| python/cpython (`PC/launcher2.c`) | NOASSERTION (PSF) | 2026-08-22 | — | 74.662 | 9.563 |
| python/pymanager | NOASSERTION (PSF) | 2026-08-18 | `26.3` · 2026-06-30 | 331 | 23 |
| pyenv-win/pyenv-win | MIT | 2026-08-21 | `v3.1.1` · **2022-07-20** | 7.374 | 169 |
| Schniz/fnm | **GPL-3.0** | 2026-07-24 | `v1.39.0` · 2026-03-06 | 26.649 | 239 |
| coreybutler/nvm-windows | MIT | 2026-04-17 | `1.2.2` · 2025-01-01 | 47.437 | 83 |
| PowerShell/PowerShell | MIT | 2026-08-20 | `v7.6.5` · 2026-08-14 | 55.056 | 1.603 |

---

## 1. python/cpython — `PC/launcher2.c` (`py.exe`)

Windows'ta `.py` dosya ilişkilendirmesinin arkasındaki resmî başlatıcı. Runly'ye en yakın
analog: çift tıklanan betiği alıp doğru yorumlayıcıyı seçip `CreateProcessW` ile başlatıyor.
Dosya `main` dalından kaldırıldı (işlev `python/pymanager`'a taşındı); son bulunduğu
etiket `v3.13.0`, 2.826 satır — okuma için hâlâ birincil kaynak.

**Runly'ye alınacak fikirler (dosyada doğrulandı):**

- **argv0'ın tırnak kuralı komut satırının geri kalanından farklıdır.** `findArgv0Length`
  başındaki yorum bunu açıkça yazıyor: argv0'da ters bölü ile kaçış **yoktur**, iç tırnaklar
  etkisizdir; tırnaklı argv0 `"` ile başlayıp `"` ile biter, tırnaksızsa ilk boşluk/tab'da
  biter. Yani `"{interpreter}"` için `CommandLineToArgvW` kaçış kuralını uygulamak yanlış.
- **Yorumlayıcı yolu yalnızca boşluk içeriyorsa tırnaklanıyor** (`calculateCommandLine`:
  `wcschr(path, L' ') && path[0] != L'"'`), kullanıcı argümanları ise
  **ham `restOfCmdLine` olarak aynen ekleniyor** — yeniden tırnaklama yok. Round-trip kaybı
  bu şekilde sıfırlanıyor.
- **Özyineleme koruması:** shebang'ten çözülen hedef `GetModuleFileNameW(NULL, …)` ile
  karşılaştırılıyor; kendisiyse `RC_RECURSIVE_SHEBANG` dönüp shebang yokmuş gibi devam
  ediliyor. Runly'de `.py` → Runly.exe ilişkilendirmesi varken çözümlenen "yorumlayıcı"nın
  Runly.exe çıkması aynı sonsuz döngüyü üretir.
- **Job object + Ctrl+C:** `CreateJobObject` + `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE |
  JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK`, ardından `AssignProcessToJobObject`. `ctrl_c_handler`
  **koşulsuz `TRUE` dönüyor** — "tüm kontrol olaylarını yok sayıyoruz". Olay zaten aynı
  konsol grubundaki çocuğa da gidiyor; kararı çocuk veriyor.
- **std handle'lar:** `_safeDuplicateHandle` ile üç handle çoğaltılıp `STARTF_USESTDHANDLES`
  ve `bInheritHandles=TRUE` ile geçiliyor. Çıkış kodu `GetExitCodeProcess` ile alınıp aynen
  dönülüyor.
- **Store takozu:** `AppExecLinkFile` yapısı (`IO_REPARSE_TAG_APPEXECLINK`) parse edilip
  hedefin `AppInstallerPythonRedirector.exe` olup olmadığına bakılıyor; şebeng yolunda da
  `ensure_no_redirector_stub` çağrılıyor.
- **Tampon:** `MAXLEN = PATHCCH_MAX_CCH` (32.767) — `CreateProcess` sınırıyla aynı, kesilme yok.

**Kaçınılacak hata:** GUI/başlatma imleci için `PostMessage(0,0,0,0); GetMessage(...)`
hilesi kullanılıyor (bugs.python.org/issue17290 referanslı) — bu bir Windows kabuk
davranışı yaması; kopyalanacaksa gerekçesiyle kopyalanmalı, yoksa "app starting" imleci
saniyelerce döner.

## 2. astral-sh/uv — `crates/uv-trampoline` + `crates/uv-python`

Python konsol/GUI giriş noktaları için üretilen küçük Rust "trampoline" ikilileri, artı
Windows'ta yorumlayıcı bulma katmanı. `njsmith/posy` trampoline'ünün forku.

**Runly'ye alınacak fikirler:**

- **Store takozunu 0 bayt ile değil, reparse point ile ayır.** `discovery.rs::is_windows_store_shim`:
  önce yol bileşenlerinde `WindowsApps` var mı diye hızlı eleme, sonra
  `FILE_ATTRIBUTE_REPARSE_POINT` kontrolü, sonra `CreateFileW(... FILE_FLAG_OPEN_REPARSE_POINT
  | FILE_FLAG_BACKUP_SEMANTICS)` + `FSCTL_GET_REPARSE_POINT`, tamponda
  `\AppInstallerPythonRedirector.exe` aranıyor. Kaynak kod yorumu (Rye'den alıntı) dürüst:
  *"pretty dumb way… Microsoft does not want us to do this as the format is unstable. So this
  is a best effort way."* — yani en iyi bilinen yöntem bile heuristik, ama 0 bayttan kat kat
  ayırt edici.
- **İkinci savunma hattı: adayı sorgula.** uv belgesi (`docs/concepts/python-versions.md`)
  net: *"Each discovered executable is queried for metadata… If the query fails, the executable
  will be skipped."* Yani dosya sistemi ipuçları yetmezse aday çalıştırılıp doğrulanıyor.
- **Çocuğun komut satırını `GetCommandLineA()` ham çıktısından kur.** `bounce.rs::push_arguments`
  → `skip_one_argument`, MSVCRT ayrıştırma kurallarını (`parsing-c-command-line-arguments`)
  uygulayarak **tam bir argüman** atlıyor, kalanı **bayt bayt aynen** ekliyor. Yorumlayıcı
  yolu ise `push_quoted_path` ile tırnaklanıyor ve içteki `"` **üç tırnağa** (`"""`)
  çevriliyor — kaynak yorumu: biri açık aralığı kapatır, biri literal tırnak olur, biri yeni
  aralık açar.
- **Job object başarısızlığı ölümcül değil.** Kod yorumu bunu gerekçelendiriyor:
  `AssignProcessToJobObject` başarısız olursa çocuk zorla öldürmede hayatta kalabilir ama
  normal akış etkilenmez; `distlib/PC/launcher.c` de aynı şekilde davranıyor. Yarış koşulu
  (çocuk atamadan önce çıkarsa) gerçek bir senaryo.
- **Ctrl+C: handler kur ve yok say.** Yorum: *"we want to ignore control-C/control-Break/logout/etc.;
  the same event will be delivered to the child, so we let them decide whether to exit or not."*
- **Başlatıcı orijinal cwd'yi açık tutmasın.** Spawn'dan sonra cwd geçici dizine alınıyor
  (`distlib::switch_working_directory` referanslı) ve devralınan handle'lar kapatılıyor.
  Runly çift tıklanan betiğin klasörünü kilitli tutarsa kullanıcı o klasörü silemez.
- **İki ayrı ikili: `uv-trampoline-console.exe` ve `uv-trampoline-gui.exe`.** Tek ikiliyle
  hem konsol hem GUI davranışı verilmiyor; PE subsystem derleme zamanı kararı.

**Boyut ve derleme profili (doğrulanmış, `crates/uv-trampoline-builder/trampolines`):**
x86_64 console 45.056 B, x86_64 gui 46.080 B, i686 console 37.888 B, aarch64 console 45.056 B.
Profil: `lto = true`, `codegen-units = 1`, `opt-level = "z"`, `panic = "immediate-abort"`,
`strip = true`. `exit_with_status` üstündeki yorum `std::process::exit`'in `core::fmt`
çekerek ~5–10 KB eklediğini not ediyor — boyut hassasiyetinin ölçeği bu.

**Kaçınılacak hata / risk:** trampoline etrafında açık issue yığını var — #19390 "trampoline
failed to spawn Python child process. entity not found (os error 2)", #20955 "AV, yeni
yazılmış trampoline exe'yi exclusive access ile açmaya çalışırken işaretliyor", #20100
"gui-scripts entry point `uv run` ile başlatıldığında korunmuyor". Yeni yazılan başlatıcı
ikilisini hemen çalıştırmak antivirüs ile yarışır.

## 3. ScoopInstaller/Shim

Yanındaki `<name>.shim` metin dosyasını okuyup hedefi başlatan Scoop yardımcısı. Aynı format
için dört bağımsız uygulama: C# (.NET Framework 4.5), C++, Rust (`windows-sys`), Zig.

**Runly'ye alınacak fikir:** **Ölçülmüş boyut/gecikme tablosunu depoda tut.** README'deki
boyut tablosu (x64: C# 14,5 KB, Zig 71,5 KB, Rust 121,5 KB, C++ 155,0 KB) ve
`benchmark/README.md`'deki hyperfine sonucu (20 ısınma + 50 ölçüm, hedef `whoami.exe`):
direct 16,5 ± 3,7 ms · C# 26,1 ± 3,5 ms · Zig 77,4 ± 3,3 ms · Rust 78,2 ± 6,8 ms ·
C++ 80,5 ± 12,1 ms. Runly'nin NativeAOT başlatıcısı için aynı disiplin (`hyperfine`,
depoya işlenmiş sonuç) tek satırlık iş.

**Şüpheli nokta — işaretleyin:** bu tablo sezgiye ters. `71/scoop-better-shimexe`'in tüm
varlık gerekçesi ".NET shim yavaş"tı; ScoopInstaller/Shim'in kendi ölçümü C#'ı native
uygulamaların **üç katı hızlı** gösteriyor. Depoda bu farkın açıklaması yok. Rakam
yayımlanmış ölçümdür ama **nedeni doğrulanamadı** — Runly kendi ölçümünü yapmadan bu
tablodan "AOT gereksiz" sonucu çıkarmamalı.

**Kaçınılacak hata:** açık issue #10 — göreli ikili yolları shim'lenemiyor. Kapalı issue #6
— `scoop shim add blender "C:\Program Files (x86)\...\blender.exe"` çağrısı
`ERROR: Command path does not exist: C:\Program` veriyor; yani boşluklu yol hatası shim
ikilisinde değil, **shim'i üreten katmanda** çıkmış. Runly'de aynı sınır: kayıt defterine
yazılan `"{script}" {args}` şablonunun kendisi yanlış tırnaklanırsa başlatıcı ne kadar doğru
olursa olsun kurtaramaz.

## 4. 71/scoop-better-shimexe

Scoop'un C# shim'inin yerine yazılmış tek dosyalık C shim'i (`shim.c`). Son etiketli sürüm
**2019-09-07**, son push 2023-06-19 — pratikte terk edilmiş; bağımlılık kurulmaz, tasarımı
okunur.

**Runly'ye alınacak fikir:** README, C# shim'i **iki somut hatayla** reddediyor ve ikisi de
Runly'yi doğrudan ilgilendiriyor: (1) her çağrıda bir .NET komut satırı uygulaması ayağa
kalkıyor; (2) **Ctrl+C ve diğer sinyaller yanlış işleniyor** — REPL'ler ve uzun süren
uygulamalar ölüyor (scoop#2339, scoop#1896, FluentTerminal#221 referanslı). Çözümü:
Ctrl+C'den doğan sinyaller **yok sayılıyor**, doğrudan çocuk işliyor; ayrıca shim öldürülünce
çocuk da öldürülüyor, yetim süreç kalmıyor. Aynı sonuca uv ve `py.exe` bağımsız olarak
varmış — üç bağımsız kaynak aynı kuralı söylüyor.

**Kaçınılacak hata:** GUI uygulama tespiti bu tasarımda çözülmemiş. Açık issue #16
("Doesn't detect GUI apps"), #17, #6 (`gitk` ve `dot` 1.2 ile çalışmıyor). Ayrıca açık #14
"Forward arguments" — kullanıcı argümanlarının hedefe iletilmesi **hiç yok**; sadece `.shim`
içindeki sabit `args` geçiyor. #9 ise `emacsclient -a="" -nw` çağrısında erken bellek
serbestleşmesinden access violation. Ders: shim'in "küçük ve C" olması argüman aktarımını
otomatik olarak doğru yapmıyor.

## 5. chocolatey/shimgen (kaynak kapalı — belgelenmiş davranış + referans kaynak)

Chocolatey'nin shim üreticisi. Üretici kapalı kaynak; ürettiği shim'in **referans kaynağı**
(`shim/ShimProgram.cs`, `shim/CommandExecutor.cs`) **Ms-RSL** altında yayımlanmış —
"yalnızca referans", kullanılamaz, kopyalanamaz.

**Belgelenmiş davranış (README):** konsol uygulamaları için bloklayıp bekliyor, GUI için hemen
çıkıyor; hedefin ikonunu shim'e kopyalıyor; `--shimgen-waitforexit`, `--shimgen-exit`,
`--shimgen-gui`, `--shimgen-usetargetworkingdirectory`, `--shimgen-noop`, `--shimgen-log`,
`--shimgen-help` bayrakları var. Sembolik bağa üstünlüğünü de gerekçelendiriyor: symlink,
hedefin yanındaki DLL/veri bağımlılıklarında çöküyor.

**Runly'ye alınacak fikir:** **GUI/konsol kararı üretim zamanında ikiliye gömülüyor.**
Referans kaynakta `{{IsGui}}` bir şablon token'ı; `wait_for_exit = !is_gui` ondan türüyor.
Yani karar çalışma zamanında tahmin edilmiyor. Ayrıca ayrılmış bir bayrak alan adı
(`--shimgen-*`) kullanıcı argümanlarıyla çakışmayı azaltıyor — Runly'nin kendi bayrakları
için aynı ad alanı disiplini gerekli.

**Kaçınılacak hata — üç tanesi referans kaynakta açıkça görünüyor:**

1. **Komut satırını boşlukla bölüp yeniden birleştirme.** `Environment.CommandLine` içinden
   kendi yolu `.Replace(...)` ile siliniyor, kalan `Split(" ")` ile parçalanıp `string.Join(" ")`
   ile birleştiriliyor. Ardışık boşluklar ve tırnak yapısı bu turda kaybolur. Yanındaki
   yorumlanmış satır (`quote_arg_value_if_required`) daha önce denenip terk edilmiş yolu
   gösteriyor. uv ve `py.exe` bunun yerine **ham komut satırından tek argüman atlayıp gerisine
   dokunmuyor**.
2. **Bayrak eşleşmesi `Contains()` ile yapılıyor.** `args.Any(a => a.Contains("shimgen-help"))`
   — içinde bu metni geçiren herhangi bir dosya yolu yardım ekranını tetikler.
3. **Ctrl+C'de çocuk `process.Kill()` ile öldürülüyor.** Bu, `71/scoop-better-shimexe`,
   `uv` ve `py.exe`'nin üçünün de kaçındığı davranışın ta kendisi: REPL'ler ve
   graceful-shutdown yapan süreçler temizlik yapamadan ölür.

**Risk:** kaynak kapalı, lisans OSI onaylı değil, marka Chocolatey Software'de; sürüm etiketi
yok, son push 2022-02-24, issue'lar `chocolatey/home`'a taşınmış. Ne bağımlılık ne örnek kod
kaynağı olabilir — yalnızca davranış sözleşmesi kaynağı.

## 6. gerardog/gsudo

Windows için `sudo`; komutu yükseltilmiş bir servise devredip I/O'yu geri köprülüyor.
Runly'nin ilgilendiği kısım yükseltme değil, **komutu bir kabuk üzerinden yeniden kurma**
katmanı (`CommandToRunAdapter`).

**Runly'ye alınacak fikirler:**

- **Kaçış kuralı hedefin ayrıştırıcısına göre değişir, sürümüne göre bile.** Issue #422:
  PowerShell 7.3, `-Command "..."` içinde `\"` kaçışını bıraktı; iç tırnaklar artık
  ikilenmeli (`""`). gsudo sürüm kontrolünü koymuş ama **değiştirme dalını yazmayı unutmuş** —
  sonuç: iç tırnaklar tamamen kaybolmuş, boşluklu argümanlar kelimelere bölünmüş
  (`ls "C:\Program Files"` → `ls`, `C:\Program`, `Files"`). Düzeltme, kaçış fonksiyonunu
  ayrı ve test edilebilir hale getirmiş, 10 birim + 1 entegrasyon regresyon testi eklemiş.
- **"Pencereyi açık tut" çıkış kodunu yutar.** Issue #421 (PR #410 ile birlikte): kullanıcı
  komutundan sonra eklenen her `postCommand` — UNC `--chdir` için `popd`, `--keepWindowOpen`
  için `pause` — `%ERRORLEVEL%`'i eziyor. Üstelik eski koruma da bozukmuş: `set errl = !ErrorLevel!`
  (eşittirin çevresindeki boşluk) sonda boşluklu bir değişken adı yaratıyor, `!errl!` hep boş
  genişliyor. Çözüm: `postCommands` boş değilse **listeyi baştan `set errl=!ErrorLevel!` ve
  sondan `exit /b !errl!` ile tek seferde sarmak**. Regresyon testi:
  `gsudo --chdir \\localhost\C$ cmd /c exit 42`.
- **Bayrak isimlendirmesi ve "kabuk tespitini atla" kaçış kapısı:** `-d | --direct`, "kabuk
  tespitini atla, CMD varsay". Otomatik tespitin yanıldığı durumda kullanıcıya elle çıkış
  yolu bırakmak.

**Kaçınılacak hata:** gsudo'nun kendi belgesi (`troubleshooting.md`, `usage/powershell.md`)
PowerShell'i .NET global tool olarak kurmamayı öneriyor, gerekçe olarak
PowerShell/PowerShell#11747'yi gösteriyor — yani **başka birinin shim'i argüman ayrıştırmayı
bozduğu için** kendi ürününü çalıştıramıyor. Runly de üçüncü taraf shim'lere zincirlendiğinde
(Scoop shim'i → node, pyenv shim'i → python) aynı hattın ortasında kalır.

**Risk:** 50 açık issue; kod tabanı named pipe, PseudoConsole, token manipülasyonu içeriyor —
Runly'nin ihtiyacından kat kat büyük bir yüzey. Alınacak şey mimari değil, iki hata dersi.

## 7. PowerShell/PowerShell

Runly `.ps1` için `powershell`/`pwsh` çağırdığından, PowerShell'in native argüman aktarımı
hakkında yıllardır tuttuğu kayıt doğrudan uygulanabilir.

**Runly'ye alınacak fikir — tek cümlelik özet issue #15143'te:** `ProcessStartInfo.ArgumentList`
Unix'te sorunu **tamamen** çözüyor; Windows'ta **yalnızca Microsoft C/C++ runtime'ının tırnak
kurallarına uyan programlar için** çözüyor. Uymayanlar aynı issue'da tek tek sayılmış:

- **`cmd.exe` ve batch dosyaları** yalnızca `""` kaçışını kabul ediyor, `\"` değil. Üstelik
  batch dosyaları argümanlarını *cmd.exe içinden geçirilmiş gibi* ayrıştırıyor; bu yüzden
  `.\foo.cmd http://example.org?foo&bar` `&`'in ifade ayırıcı sanılmasıyla bozuluyor —
  ve PowerShell tarafında tırnaklamak da işe yaramıyor, çünkü boşluk/iç tırnak içermeyen
  değerin tırnakları komut satırı yeniden kurulurken haklı olarak düşürülüyor.
- **`msiexec` tarzı programlar kısmi tırnaklamaya duyarlı:** `PROP="VALUE WITH SPACES"` ile
  `"PROP=VALUE WITH SPACES"` C/C++ runtime'a göre eşdeğer, pratikte değil.
- **WSH (`cscript`/`wscript`, `.vbs`/`.js` ilişkilendirmeleri)** `\"` ile kötü davranıyor;
  `""` daha iyi sonuç veriyor ama tam çözüm yok.
- Kalan uç durumlar için kaçış kapıları: `--%` (konsol uygulamaları) ve tek string alan
  `Start-Process -ArgumentList` (GUI subsystem).

Ayrıca issue #26432: `Legacy` modda bile **kapanış tırnağından önceki `\` beklenmedik şekilde
kaçırılıyor** (`choice /d Y /t 0 /m 'a b\'`). Sondaki ters bölü + tırnak, Windows argüman
kaçışının klasik tuzağı ve Runly'nin `"{script}"` şablonunda betik yolu `\` ile bittiğinde
aynen tetiklenir. Açık issue #26437 ise `explorer.exe`'nin de "legacy" listesine alınması
gerektiğini söylüyor.

**Kaçınılacak hata:** "tek doğru kaçış kuralı" varsaymak. Yok. Kural hedef ayrıştırıcıya
bağlı ve PowerShell bunu deneysel özellik + `$PSNativeCommandArgumentPassing` = `Legacy` /
`Standard` / `Windows` üçlü moduyla, yıllara yayılan regresyonlarla (#15239, #15250, #15261,
#15289, #17305, #18694) öğrendi.

## 8. pyenv-win/pyenv-win

Windows'ta Python sürüm yöneticisi; shim'leri `.bat` dosyaları olarak üretiyor.
Son etiketli sürüm `v3.1.1` · **2022-07-20** — dört yıldır etiket yok, ama son push
2026-08-21, 169 açık issue.

**Kaçınılacak hata — bu depo `.bat` shim'in neden çalışmadığının kanıt dosyası:**

- **Açık issue #170: batch shim argümanlardaki `%` karakterlerini yiyor.** Bildirilen
  davranış: `%0`, `%*`, `%~p0`, `%PATH%` gibi her şey betiğe ulaşmadan genişletiliyor; tek
  `%` siliniyor (`%foo` → `foo`); `a%foo%b` → `ab`; `%foo%` argüman olarak tamamen kayboluyor;
  `%~foo` pyenv'in kendisini hataya düşürüyor. Kullanıcının kaçınma yolu **dört kat kaçış**:
  `%%%%0`. Bildirimdeki tekrar örneği: `python args.py %0 %1 %2` çağrısı betiğe
  `args.py`, `pyenv`, `exec`, `python` argümanlarını veriyor.
- **Kapalı issue #506: `WindowsApps` PATH'te önde.** Yeni bir Windows 11'de pyenv kurulup
  `pyenv global 3.10.5` çalıştırıldıktan sonra bile `python` yazınca Microsoft Store açılıyor,
  çünkü pyenv kendi `shims`/`bin` klasörünü `%USERPROFILE%\AppData\Local\Microsoft\WindowsApps`'in
  **altına** ekliyor. Yani PATH sıralaması, doğru shim'i üretmek kadar önemli.
- **Topluluk `.exe` shim istiyor:** açık #352 ve #614 ("shims as .exe files"), #599 (shim'lenen
  `.exe` yolunun sonunda satır sonu karakteri), #458 (`python3.9` gibi ara sürüm shim'leri
  çalışmıyor), #363 (pip shim'i requirement specifier'ları bozuyor). Runly'nin başlatıcıyı
  gerçek bir ikili yapma kararı bu listeyle doğrulanıyor.

**Runly'ye alınacak fikir:** yorumlayıcı arayışında Runly'nin PATH sırası `WindowsApps`
konumuna göre nerede duruyor — bu, PathSearcher'ın kendi mantığından bağımsız bir arıza
kaynağı ve teşhis çıktısında görünmeli.

## 9. coreybutler/nvm-windows

Node sürüm yöneticisi (Go ile yazılmış). Shim de PATH yeniden yazımı da kullanmıyor:
sistem PATH'ine **kurulumda bir kez** konan tek bir sembolik bağın hedefi değiştiriliyor.

**Runly'ye alınacak fikir:** README (satır 193–197) üç yaklaşımı da gerekçesiyle karşılaştırıyor —
(a) her geçişte sistem PATH'ini değiştirmek, (b) node'u taklit eden `.bat` dosyasıyla
yönlendirmek ("bu bana hep biraz uydurma geldi, ve bunun sonucu bazı tuhaflıklar var"),
(c) tek symlink. Symlink seçilmiş çünkü açık konsollarda anında etkili, yeniden başlatmaya
dayanıklı, her konsolda `nvm use` gerektirmiyor. **Bedeli açıkça yazılmış:** symlink oluşturma
yönetici hakkı ve UAC ister; symlink var olan bir fiziksel dizinin üzerine yazamaz
(`C:\Program Files\nodejs` ile çakışır); önceki Node kurulumu silinmezse `nvm use` hiçbir şey
yapmıyormuş gibi görünür (PATH çakışması, teşhis için `nvm debug`).

**Kaçınılacak hata:** README teşekkürler bölümü, "yollardaki boşluk kaçışı sorununu çözen"
PR #355'i (2018-08-11) anıyor. PR açıklaması ders veriyor: *"exec.Command() boşlukları
tırnaklansa bile işleyemiyor. Komut `SysProcAttr` ile değiştirilmek zorunda"* — golang/go#15566
referanslı. Yani **yüksek seviye süreç API'sinin argüman listesi soyutlaması Windows'ta
komut satırını sizin adınıza yanlış kurabilir**; kaçış kontrolünü ele almak gerekir.
.NET tarafında karşılığı: `ProcessStartInfo.Arguments` (ham string, kaçış sizin) ile
`ArgumentList` (kaçış runtime'ın, ama yalnızca C/C++ runtime kuralına uyan hedefler için
doğru — bkz. §7) arasındaki seçim bilinçli yapılmalı.

## 10. Schniz/fnm

Rust ile yazılmış Node sürüm yöneticisi. **GPL-3.0** — kod kopyalamak zaten yasak, burada
ayrıca lisans engeli var.

**Runly'ye alınacak fikir — negatif örnek olarak değerli.** fnm shim ikilisi üretmiyor;
her kabukta bir başlangıç kancası (`eval "$(fnm env --use-on-cd --shell …)"`) ile PATH
ayarlıyor. README, bash/zsh/fish/PowerShell 5/PowerShell 6+/cmd/Cmder için ayrı ayrı kurulum
adımı veriyor; cmd tarafında `for /F`'in yeni bir cmd örneği başlatması yüzünden
`FNM_AUTORUN_GUARD` adında sonsuz döngü koruması gerekiyor. **Runly bu yolu seçemez:**
Explorer'dan çift tıklama hiçbir kabuk profilini çalıştırmaz. Bu, "shim ikilisi yaz"
kararının bir başka bağımsız doğrulaması.

**Kaçınılacak hata — Runly'nin de yiyeceği ısırıklar:** açık issue #1481 kapatılmış olsa da
kayıtta: "kullanıcı adı CJK içerdiğinde fnm'in Windows PATH'i bozuk karakterli". Kod sayfası /
UTF-8 sorunu ASCII olmayan kullanıcı adlarında çıkıyor. Açık #1585 ve #1583: çıkarma sonrası
`rename` işlemi antivirüs handle'ı yüzünden `os error 5` veriyor ve kullanıcıya **"indirme
hatası" olarak** raporlanıyor — yanlış hata mesajı, doğru hatadan daha pahalı. Açık #1413:
VS Code içindeki Git'te `'node': No such file or directory` — kancasız ortamlar sessizce
başarısız oluyor.

---

# Runly için sonuç

1. **Store takozu tespitini 0 bayt heuristiğinden reparse point okumasına taşı.**
   `PathSearcher.SearchPathAndPathExt` içindeki yorum sorunu zaten itiraf ediyor: *"a working
   alias (py.exe on this machine) is byte-identical, so size alone cannot tell them apart"* —
   ve K9 kararıyla sıfır baytlı adayı son çare olarak kabul ediyor. uv ve `py.exe` aynı sorunu
   `FILE_FLAG_OPEN_REPARSE_POINT` + `FSCTL_GET_REPARSE_POINT` ile çözmüş; APPEXECLINK verisinin
   üçüncü dizesi zaten **gerçek hedef exe yolu**. O yolu okuyup doğrudan kullanmak hem takozu
   eler hem çalışan alias'ı kurtarır. Maliyet: bir P/Invoke bloğu, ~60 satır, NativeAOT ile
   uyumlu.

2. **Çocuk komut satırını yeniden kurma — ham komut satırından dilimle.** Bugün
   `InterpreterResolver` her token'ı `QuoteArgumentIfNeeded` ile yeniden tırnaklıyor. Bu
   `CommandLineToArgvW`'ye uyan hedefler için doğru, batch/WSH/msiexec için değildir
   (PowerShell#15143). uv ve `py.exe`'nin ortak deseni: **tam bir argüman atla, gerisini
   aynen aktar.** Runly'de `{args}` bu şekilde geçmeli; yalnızca `{script}` tırnaklanmalı.

3. **argv0 (yorumlayıcı yolu) için ayrı kural uygula.** `py.exe`'nin `findArgv0Length`
   yorumu net: argv0'da ters bölü kaçışı yok, tırnak yalnızca sınırlayıcıdır. Yorumlayıcı
   yolunu **yalnızca boşluk içeriyorsa** tırnakla, içine `\"` kaçışı sokma.

4. **Batch hedefi için `^` kaçışını gözden geçir.** `ProcessLauncher.BuildBatchArgumentLine`
   `& | < > ^ ( )` karakterlerini caret'liyor, ama argümanlar zaten
   `QuoteArgumentIfNeeded`'dan geçmişse çift kaçış olur. PowerShell'in batch için vardığı
   sonuç farklı: batch `\"` değil `""` ister ve `&` sorunu tırnaklamayla çözülmez
   (PowerShell#15143, #15250). Bu yolun regresyon testi yoksa yazılmalı.

5. **`SetConsoleCtrlHandler` kur ve `TRUE` dön — çocuğu öldürme.** Şu an Runly'de kontrol
   olayı işleme yok. Üç bağımsız uygulama (`py.exe` `ctrl_c_handler`, uv `install_ctrl_handler`,
   `shim.c`) aynı kuralı koyuyor; shimgen'in `process.Kill()` yaklaşımı ise REPL'i öldüren
   davranışın kaynağı. Maliyet: tek P/Invoke + bir delegate'i GC'den koruma.

6. **Job object ile çocuğu bağla, ama başarısızlığı ölümcül sayma.**
   `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK` — Runly zorla
   öldürülünce yetim yorumlayıcı kalmaz. uv'nin kod yorumu atama başarısızlığının
   (yarış koşulu, erken çıkan çocuk) normal akışı etkilemediğini ve `distlib`'in de bunu
   ölümcül saymadığını söylüyor. Aynı yerde cwd'yi geçici dizine al: Runly şu an çift
   tıklanan betiğin klasörünü açık tutuyor.

7. **"Pencereyi açık tut" davranışını çıkış kodundan izole et.** gsudo#421 tam olarak bunu
   kaybetti: `pause`/`popd` gibi son komutlar `%ERRORLEVEL%`'i ezdi ve koruma kodu
   `set errl = ` yazım hatasıyla sessizce çalışmadı. Runly'nin bekletme davranışı çocuğun
   çıkış kodunu **önce yakalayıp**, bekletmeden **sonra** aynen döndürmeli; regresyon testi
   bilinen bir kodla (örn. 42) doğrulamalı.

8. **Konsol/GUI kararını çalışma zamanında tahmin etme, iki ikili derle — ve gecikmeyi ölç.**
   `Runly.Launcher.csproj` bugün tek `OutputType=Exe` (konsol subsystem); bu, `.pyw`/GUI
   betiklerinde konsol penceresi yanıp söner. uv iki ayrı trampoline (console + gui,
   x64'te 45/46 KB), shimgen kararı üretim zamanında `{{IsGui}}` ile gömüyor. Aynı anda
   ScoopInstaller/Shim'in `benchmark/` klasörü deseni alınmalı: `hyperfine`, 20 ısınma +
   50 ölçüm, sonuç depoya işlenir. Runly'nin NativeAOT başlatıcısının soğuk başlatma
   maliyeti şu an **ölçülmemiş** — 8. maddenin ilk yarısı ancak bu ölçümle gerekçelendirilir.

---

## Kaynaklar

Depo metadata'sı ve sürüm tarihleri: `gh api repos/<owner>/<repo>` ve
`gh api repos/<owner>/<repo>/releases/latest`, 2026-08-22.

- ScoopInstaller/Shim — <https://github.com/ScoopInstaller/Shim> ·
  `README.md` (boyut tablosu, `.shim` formatı, çıkış kodu sözleşmesi) ·
  `benchmark/README.md` (hyperfine sonuçları) · issue #2, #4, #6, #10
- 71/scoop-better-shimexe — <https://github.com/71/scoop-better-shimexe> ·
  `README.md` (C# shim reddi, Ctrl+C ve yetim süreç gerekçesi) · issue #6, #9, #14, #16, #17
- chocolatey/shimgen — <https://github.com/chocolatey/shimgen> ·
  `README.md` (bayraklar, GUI/konsol bekleme sözleşmesi, lisans SSS'i) ·
  `shim/README.md` (Ms-RSL) · `shim/ShimProgram.cs`, `shim/CommandExecutor.cs` (yalnız okundu)
- gerardog/gsudo — <https://github.com/gerardog/gsudo> · issue #421, #422 ·
  `docs/docs/how-it-works.md`, `docs/docs/troubleshooting.md`,
  `docs/docs/usage/usage.md`, `docs/docs/usage/powershell.md`
- astral-sh/uv — <https://github.com/astral-sh/uv> ·
  `crates/uv-trampoline/src/bounce.rs`, `crates/uv-trampoline/Cargo.toml`,
  `crates/uv-trampoline/README.md`, `crates/uv-trampoline-builder/trampolines/` (dosya boyutları) ·
  `crates/uv-python/src/discovery.rs`, `crates/uv-python/src/microsoft_store.rs` ·
  `docs/concepts/python-versions.md` · issue #19390, #20100, #20955
- python/cpython — `PC/launcher2.c` @ `v3.13.0` ·
  <https://github.com/python/cpython/blob/v3.13.0/PC/launcher2.c>
- python/pymanager — <https://github.com/python/pymanager> · `README.md` · PEP 773
- pyenv-win/pyenv-win — <https://github.com/pyenv-win/pyenv-win> · issue #170, #352, #363,
  #458, #506, #599, #614
- Schniz/fnm — <https://github.com/Schniz/fnm> · `README.md` (kabuk kurulumu, cmd autorun
  guard) · issue #1413, #1481, #1583, #1585
- coreybutler/nvm-windows — <https://github.com/coreybutler/nvm-windows> · `README.md`
  (symlink gerekçesi, PATH çakışmaları) · PR #355 · golang/go#15566
- PowerShell/PowerShell — issue #11747, #15143, #15239, #15250, #15261, #15289, #17305,
  #18694, #26432, #26437

**Doğrulanamayan:** ScoopInstaller/Shim benchmark tablosundaki C#'ın native uygulamalardan
hızlı çıkması ölçüm olarak yayımlanmış ama **nedeni depoda açıklanmamış**; başka bir kaynakla
teyit edilemedi. Runly kendi ölçümünü yapmadan bu tablodan sonuç çıkarmamalı.
