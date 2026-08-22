# Schniz/fnm

## 1. Künye

| Alan | Değer (`gh api`, 2026-08-22) |
|---|---|
| Depo | `Schniz/fnm` |
| **Lisans** | **GPL-3.0** (API + kökteki `LICENSE` başlığı ile teyit). **Runly MIT'tir: bu depodan kod alınamaz, yalnız desen okunur.** |
| Yıldız / açık issue · son commit | 26.652 / 239 · 2026-07-24 |
| Son etiketli sürüm | `v1.39.0` · 2026-03-06 |

## 2. Ne yapıyor

Rust ile yazılmış Node.js sürüm yöneticisi, nvm-windows'un çapraz platform karşılığı; kabuk
oturumuna ortam değişkeni enjekte edip `PATH`'e sürüme özgü dizin ekliyor.

## 3. Runly ile kesişimi

**Konsol/GUI ayrımı yok; konsol yanıp sönmesi de doğmuyor, çünkü başlatma yolunda süreç yok.**
fnm tek konsol ikilisi. Node çalışırken fnm **çalışmıyor**: `fnm env` kabuk oturumunda bir kez
değerlendiriliyor (`eval` / `Invoke-Expression`), `PATH`'e sürüm dizini ekleniyor, `node.exe`
doğrudan bulunuyor. Runly'nin problemi (çift tıklanan dosya → araya giren süreç) fnm'de yok;
`PATHEXT`, argüman kaçışı, çıkış kodu/Ctrl+C, shim boyutu ve gecikme bu depoda **yok**.

**Yine de bir "yanıp sönme" hâli var, `cmd` tarafında.** `src/shell/windows_cmd/cd.cmd`,
`cd /d %*` sonrası `.nvmrc`/`.node-version` varsa `fnm use --silent-if-unchanged` çağırıyor — her
`cd`'de süreç başlıyor, bayrağın tek işi o sürecin **çıktı üretmemesi**. Runly'nin "konsol
çakmasın" hedefinin kabuk-hook karşılığı: pencereyi gizlemek değil, sesi kesmek.

**Yol ve bağ tarafı:** `src/fs.rs` — Windows'ta `symlink_dir` **değil** `junction::create`;
junction yönetici hakkı veya Developer Mode istemiyor. `fnm.manifest` yalnız `longPathAware =
true` içeriyor, `build.rs` bunu `embed_resource` ile gömüyor.
`src/commands/env.rs:40-50` `make_symlink` rastgele adlı geçici bağ üretip çakışmada yeniden
deniyor, yolu `FNM_MULTISHELL_PATH` ile aktarıyor.

## 4. Alınacak fikir

1. **Windows'ta symlink yerine junction kullan** (`src/fs.rs`, `#[cfg(windows)]`). Symlink
   yönetici veya Developer Mode ister, junction istemez. Maliyet: bir bağımlılık.
2. **`longPathAware` manifestini gömerek dağıt** (`fnm.manifest` + `build.rs` `embed_resource`).
   nvm-windows'un #289'u tam olarak bunun yokluğu. Maliyet: bir manifest + gömme adımı.
3. **Sessizlik bayrağını arayüzün parçası yap** (`cd.cmd`, `fnm use --silent-if-unchanged`): `RunlyConsole.exe` otomasyonda "değişiklik yoksa yazma" kipine ihtiyaç duyar. Maliyet: az.

## 5. Kaçınılacak hata

`src/shell/windows_compat.rs`: yol dönüşümü için **`cygpath` alt süreci** çağrılıyor, başarısızsa
orijinal yol dönüyor — her dönüşümde süreç maliyeti, artı makinede `cygpath` varsa sessizce
değişen davranış. Runly başlatma yolunda "varsa kullan" alt süreci taşımamalı.
Açık **#1379** ("Does not put node on path in Windows PowerShell?", 25 reaksiyon) ve **#1366**
("fnm multishell makes it unable to run npx MCP server in Claude Desktop", 17): kabuk enjeksiyonuna
dayanan modelin sınırı — kabuğu **atlayan** her çağıran (Explorer, GUI uygulaması) doğru sürümü
görmüyor. Runly'nin shim yaklaşımının üstünlüğü tam burada.

## 6. Doğrulama

Okundu: `LICENSE` ilk satırları, `src/fs.rs`, `fnm.manifest`, `build.rs`, `cd.cmd` (tam),
`windows_compat.rs` (ilk 21 satır), `src/commands/env.rs` (grep), dosya listeleri, issue
başlıkları, künye.
`doğrulanamadı`: `src/shell/powershell.rs` okunmadı — PowerShell hook'unun `cd.cmd` ile aynı
sessizliği gösterdiği **doğrulanamadı**. `benchmarks/` çalıştırılmadı; #1379/#1366 tekrarlanmadı.
