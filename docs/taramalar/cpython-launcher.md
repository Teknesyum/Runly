# python/cpython — `PC/launcher2.c` ve `PC/launcher.c`

## 1. Künye

| Alan | Değer (`gh api`, 2026-08-22) |
|---|---|
| Depo | `python/cpython` |
| **Lisans** | API'de **NOASSERTION**; kökteki `LICENSE` satır 73: **"PYTHON SOFTWARE FOUNDATION LICENSE VERSION 2"** (OSI onaylı, GPL uyumlu). Marka PSF'te ayrı korunuyor. |
| Yıldız / açık issue | 74.667 / 9.565 |
| Son push (depo) | 2026-08-22 |
| Bu iki dosyanın son commit'i | **2026-07-20** — `bd315837`, `gh-153511: Removes … legacy py.exe launcher sources`. Yani **`main`'de artık yoklar.** Okunabilir son etiket `v3.13.0`: `launcher2.c` 88.908 B / 2.826 satır, `launcher.c` 67.181 B. |

## 2. Ne yapıyor

`py.exe` / `pyw.exe`: `.py` ve `.pyw` ilişkilendirmesinin arkasındaki resmî başlatıcı — shebang,
`py.ini`, kayıt defteri ve PEP 514 ortamlarından yorumlayıcıyı seçip `CreateProcessW` ile
başlatıyor. `launcher.c` eski uygulama, `launcher2.c` yeniden yazımı.

## 3. Runly ile kesişimi

**Konsol/GUI ayrımı — tek kaynak, iki proje dosyası.** `PCbuild/pylauncher.vcxproj` ve
`pywlauncher.vcxproj` **aynı `..\PC\launcher2.c`**'yi derliyor; fark üç satır:
`<TargetName>` `py`↔`pyw`, `<SubSystem>` `Console`↔`Windows`, `<PreprocessorDefinitions>`
`_CONSOLE`↔`_WINDOWS`. `venvlauncher` / `venvwlauncher` aynı deseni tekrarlıyor — CPython bu
ayrımı dört ikilide tek şablonla kuruyor.

`_WINDOWS` iki şeyi değiştiriyor: giriş noktası `wmain` yerine `wWinMain` (`:2811+`); ve hata
yüzeyi — `winerror`/`error` (`:103`, `:122`) konsolda `fwprintf(stderr, …)`, GUI'de `MessageBoxW`.

**Ayrım koda da sızıyor:** `SearchInfo.windowed` (`:435`). `pyw` çağrıldığında `true` oluyor
(`:632`); kayıt defterinden `WindowedExecutablePath` / `WindowedExecutableArguments` okunuyor
(`:1631-1645`), Store aramasında `pythonw.exe` seçiliyor (`:1750`). Yani GUI başlatıcı yalnız
kendi subsystem'ini değil **seçtiği yorumlayıcıyı da** değiştiriyor.

argv0 tırnak kuralı, özyineleme koruması, job object, `MAXLEN = PATHCCH_MAX_CCH`: 03'te var.

## 4. Alınacak fikir

1. **İkiliyi ayırırken yorumlayıcı seçimini de ayır.** `SearchInfo.windowed` deseni: GUI
   başlatıcı konsolsuz varyantı (`pythonw.exe`) tercih etmeli, yoksa normale düşmeli
   (`:1642`). Maliyet: katalogda yorumlayıcı başına ikinci bir yol alanı.
2. **Aynı kaynak, iki proje dosyası, tek define.** Runly'de `Runly.csproj` ↔ `Runlyw.csproj`
   aynı `.cs` kümesini `Exe` ↔ `WinExe` ile derleyebilir. Maliyet: bir proje dosyası.
3. **Hata metni tek fonksiyondan geçsin.** `error()`/`winerror()` tek yerde dallanıyor;
   çağıranlar konsol mu GUI mi bilmiyor. Maliyet: bir kerelik çağrı yeri taraması.

## 5. Kaçınılacak hata

`SetConsoleCtrlHandler(ctrl_c_handler, TRUE)` (`:2617`) **koşulsuz** çağrılıyor — GUI
derlemesinde konsol yokken de; zararsız no-op ama ayrım kod içinde eksik. İkinci tuzak: GUI derlemesindeki `PostMessage(0,0,0,0); GetMessage(...)` hilesi (`:2577-2591`,
bugs.python.org/issue17290). Gerekçesiz kopyalanırsa anlamsız görünür, hiç kopyalanmazsa
Explorer'ın kum saati imleci saniyelerce döner.

## 6. Doğrulama

Okundu: `v3.13.0` etiketinden `PC/launcher2.c` (tam indirildi, ilgili bölümler okundu),
`PCbuild/pylauncher.vcxproj` ve `pywlauncher.vcxproj` (grep), `PCbuild` launcher proje listesi,
kaldırma commit'i `bd315837`, `LICENSE` başlığı, API künyesi.
`doğrulanamadı`: `PC/launcher.c` içeriği okunmadı — yalnız boyut ve son commit tarihi alındı.
`venvlauncher.vcxproj` içeriği okunmadı, yalnız dosya adlarından çıkarıldı.
