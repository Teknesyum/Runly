# gerardog/gsudo

## 1. Künye

| Alan | Değer (`gh api`, 2026-08-22) |
|---|---|
| Depo | `gerardog/gsudo` |
| **Lisans** | **MIT** (API + `gsudo.csproj` `PackageLicenseExpression`). Ayrı marka koruma ifadesi görülmedi. |
| Yıldız / açık issue · son push | 6.029 / 50 · 2026-08-12 |
| Son etiketli sürüm | `v2.6.1` · **2025-10-06** — kod hareketli ama **10 aydır etiketli sürüm yok**. |

## 2. Ne yapıyor

Windows için `sudo`: komutu aynı konsolda yükseltilmiş yetkiyle çalıştırıp çıktıyı geri akıtıyor.
Runly'yi ilgilendiren kısım yükseltme değil; "komutu çözümle, kaçır, başlat, pencereyi doğru anda
kapat" katmanı.

## 3. Runly ile kesişimi

**Konsol/GUI ayrımı — dördüncü yol: tek ikili, hedefin subsystem'ini çalışma zamanında sor.**
`ProcessFactory.cs:150-158` `IsWindowsApp(exe)`: yol `FindExecutableInPath` ile çözülüp
`SHGetFileInfo(path, …, SHGFI_EXETYPE)` çağırıyor, dönen değerin **yüksek word'ü sıfırdan
büyükse** GUI. Scoop Shim aynı bilgiyi PE başlığından elle okuyor; gsudo kabuk API'sinden.
Bu bilgi doğrudan **pencereyi açık tutma** kararına bağlanıyor: `CommandToRunAdapter.cs:409`
— `keepWindowOpen && !IsWindowsApp` ise `pause` ekleniyor. "İş bitince pencere kapanmasın"
davranışı GUI hedeflerde **kapatılıyor**. Runly'nin aynı ayarında birebir aynı sınır.

**`PATHEXT` ve yorumlayıcı bulma:** `ProcessFactory.cs:160-213` — önce
`ExpandEnvironmentVariables`, dosya varsa doğrudan; yoksa `PATHEXT` **process scope**'undan
okunup `;` ile bölünüyor. Girdinin uzantısı listedeyse ham ad da aday oluyor, sonra her uzantı
ekleniyor. Sıra: girdide klasör varsa yalnız o klasör, yoksa **önce cwd**, sonra `PATH`.
Kabuk yerleşikleri için ayrı dal var (`CommandToRunAdapter.cs:246` → `cmd /s /c`). Çıkış kodu ve
Ctrl+C aktarımı `ProcessRenderers/` altında dört stratejiye bölünmüş (Attached/Piped/VT/
TokenSwitch) — Runly'nin tek modeline göre aşırı.

**NativeAOT:** `gsudo.csproj` `net9.0` + `net46` çift hedef; `net9.0`'da `PublishAot`,
`PublishTrimmed`, `InvariantGlobalization`, `IlcOptimizationPreference = Size` — Runly başlatıcısıyla aynı profil.

## 4. Alınacak fikir

1. **"Pencereyi açık tut"u hedefin subsystem'ine bağla** (`CommandToRunAdapter.cs:409`): GUI hedefte `pause` yok. Maliyet: tek `if` + `IsWindowsApp` çağrısı.
2. **`SHGetFileInfo` + `SHGFI_EXETYPE` ile subsystem tespiti** (`ProcessFactory.cs:150-158`). PE başlığını elle ayrıştırmadan aynı bilgi. Maliyet: bir P/Invoke.
3. **`PATHEXT`'i process scope'undan oku; girdinin uzantısı listedeyse ham adı da aday yap** (`ProcessFactory.cs:174-185`). Machine/User scope oturumda değişeni kaçırır. Maliyet: yok.

## 5. Kaçınılacak hata

`ArgumentsHelper.cs`: `Quote()` düz olarak baştan sona tırnak ekliyor — içteki tırnak ve ters bölü
kaçırılmıyor. `SplitArgs` de yalnız tırnak sayarak bölüyor, `\"` kaçışını tanımıyor.
Açık issue **#297** ("Incorrect quote handling on PowerShell>=v7.3 …") bunun görünen yüzü.
`"{script}" {args}` şablonunda naif tırnaklama aynı sınıf hatayı verir; uv/CPython'un ham komut
satırını aynen taşıma yaklaşımı daha güvenli.
İkincisi: `CommandToRunAdapter.cs:373-399` ek adımlar için geçici `.bat` üretip **`Everyone: FullControl`** ACL veriyor. Runly böyle bir dosya üretirse aynı ACL yerel yükseltme yüzeyi olur.

## 6. Doğrulama

Okundu: `gsudo.csproj` (ilk 50 satır), `ArgumentsHelper.cs` (tam), `ProcessFactory.cs`
(`IsWindowsApp` + `FindExecutableInPath`), `CommandToRunAdapter.cs` (459 satır, `Build()` ve
geçici `.bat` bölümleri), `ProcessHosts`/`ProcessRenderers` listeleri, issue başlıkları, künye.
`doğrulanamadı`: `ProcessRenderers/*.cs` okunmadı — Ctrl+C ve çıkış kodu aktarımının detayı
**doğrulanamadı**. #297'nin güncel sürümde hâlâ tekrarlandığı test edilmedi.
