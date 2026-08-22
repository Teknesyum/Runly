# astral-sh/uv

## 1. Künye

| Alan | Değer (`gh api`, 2026-08-22) |
|---|---|
| Depo | `astral-sh/uv` |
| **Lisans** | **Apache-2.0** (depo kökü). `crates/uv-trampoline/Cargo.toml` ayrıca **MIT OR Apache-2.0**, author `Nathaniel J. Smith` — trampoline `posy`'den forklanmış, çift lisanslı. |
| Yıldız / açık issue | 88.972 / 2.849 |
| Son push | 2026-08-22 |
| Son etiketli sürüm | `0.12.5` · 2026-08-14 |

## 2. Ne yapıyor

Rust ile yazılmış Python paket ve proje yöneticisi. Runly'yi ilgilendiren tek alt bileşen
`crates/uv-trampoline`: venv `Scripts/` altındaki her giriş noktası için üretilen ~45 KB başlatıcı.

## 3. Runly ile kesişimi

**Konsol/GUI ayrımı — en net cevap burada.** `src/bin/uv-trampoline-console.rs` ve
`uv-trampoline-gui.rs` yedişer satır: `#![windows_subsystem = "console"]` vs `"windows"`, ikisi de
`#[no_main]` + `mainCRTStartup` tanımlayıp `bounce::bounce(false)` / `bounce(true)` çağırıyor.
Tüm mantık `bounce.rs`'te; `is_gui` bayrağı **yalnız bir yerde** kullanılıyor (`bounce.rs:461`):
`clear_app_starting_state`, Explorer'ın kum saati imlecini temizleyen geçici pencere hilesi.
Spawn, job object, Ctrl+C, bekleme, çıkış kodu iki ikilide birebir aynı.

**Hata yüzeyi derleme zamanında değil, çalışma zamanında seçiliyor.** `diagnostics.rs:47-60`
(`write_diagnostic`): `stderr` handle'ı null değilse stderr'e yaz; null **ve** hata ise
`MessageBoxA`. GUI ikilisi konsoldan başlatıldığında hâlâ metin basıyor.

**Kayıt yeri:** derlenmiş ikililer `crates/uv-trampoline-builder/trampolines/` altında depoya
işlenmiş — x86_64/i686/aarch64 × console/gui = 6 dosya, 37.888–46.080 B. Çalışma anında
derlenmiyor; kopyalanıp resource bölümüne hedef yol yazılıyor.

Argüman kaçışı, job object, cwd bırakma, Store shim reparse tespiti: 03'te var.
## 4. Alınacak fikir

1. **Ortak mantığı kütüphaneye al; iki ikili yalnızca subsystem + tek bool olsun.**
   `src/bin/*.rs` toplam 14 satır. Runly'nin konsol/GUI ayrımı da tek `Bounce(bool isGui)`
   girişi + iki proje dosyasına inebilir. Maliyet: bir kerelik proje bölme.
2. **Hata gösterimini `stderr` handle'ının null olmasına bağla** (`diagnostics.rs:47-60`).
   Tek kural iki ikiliyi de doğru davrandırır; `#if GUI` dallanması gerekmez.
3. **Başlatıcı ikililerini depoya işle, yeniden üretilebilirliğini CI'da doğrula**
   (`trampolines/` + PR #20853). "Hangi sürüm hangi shim'i yazdı" sorusu ortadan kalkar.

## 5. Kaçınılacak hata

Açık issue **#20955**: uv yeni yazdığı trampoline exe'sini hemen exclusive access ile açmaya
çalışıyor, AV taraması dosyayı tutuyor, işlem hata veriyor. Runly kurulumda bir ikili yazıp
aynı saniyede çalıştıracaksa aynı yarışa girer.
`bounce.rs:437` yorumu `AssignProcessToJobObject`'in **hata verebileceğini** ve bunun ölümcül
sayılmadığını kabul ediyor — "çocuk her zaman öldürülür" garantisi yok, iyi niyet var.

## 6. Doğrulama

Okundu: `Cargo.toml`, `build.rs`, `src/bin/*.rs` (tam), `src/diagnostics.rs` (tam),
`src/bounce.rs` (405-480 + grep), `trampolines/` listesi ve boyutları, API künyesi, açık
trampoline issue başlıkları.
`doğrulanamadı`: `uv-trampoline-builder/src` resource yazma kodu; ikililerin gerçek başlatma
gecikmesi (depoda ölçüm yok); #20955'in bugün hâlâ tekrarlanabilir olduğu.
