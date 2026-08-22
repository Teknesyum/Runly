# Squirrel.Windows

## 1. Künye

- **Depo:** `Squirrel/Squirrel.Windows`
- **Lisans:** MIT (GitHub API `spdx_id: MIT`; kökte `COPYING`)
- **Yıldız:** 7.977 · **Açık issue:** 423
- **Son commit:** 2024-01-11 (`develop`) · **Son etiketli sürüm:** `2.0.1` — 2020-09-27
- **Durum:** arşivlenmemiş ama README'nin ilk başlığı "Contributors Needed" (#1470). Fiilen duran
  proje; halefleri için bkz. `velopack.md` — bu depoda yazılı değil, **doğrulanamadı**.

## 2. Ne yapıyor

Windows masaüstü uygulamaları için kurulum ve arka planda güncelleme çerçevesi. Uygulamayı
`%LocalAppData%\MyApp` altına, her sürümü ayrı `app-<sürüm>` klasörüne kuruyor.

## 3. Runly ile kesişimi

Dosya ilişkilendirme değil, **kurulum yolu değişimi ve kaldırma dürüstlüğü** ekseninde kesişiyor.
`docs/using/install-process.md`: kısayolun hedefi uygulama exe'si değil, kök klasördeki sabit
`Update.exe`; argüman `--processStart MyApp.exe`. Dışarıya verilen yol sürümden bağımsız shim.

Runly'nin modeli bunun tersi: `ShellRegistrar.WriteVerb` ve `WriteApplicationRegistration` her
ProgID verb'ine ve Applications anahtarının open komutuna **mutlak `Runly.exe` / `RunlyConsole.exe`
yolu** yazıyor. Exe taşınırsa kayıttaki komutlar ölü yolu gösterir, çift tık sessizce çuvallar.

## 4. Alınacak fikir

1. **Sürümden bağımsız sabit shim yolu.** Registry'ye oynak yol yazmamak; sabit bir giriş noktasına
   yazıp gerçek ikiliye oradan devretmek (`%LocalAppData%\Runly\Runly.exe` gibi). Maliyet: orta —
   kurulum akışına sabit konum kararı ekliyor.
2. **Güncelleme de bir olaydır.** `SquirrelAwareApp.HandleEvents(onInitialInstall, onAppUpdate,
   onAppUninstall, onFirstRun)`; `onAppUpdate` kısayolu yeniden yazıyor. Issue **#788**'de
   anaisbetts aynısını söylüyor: "Register the file association on install/update and remove it on
   uninstall." Runly'de güncelleme ayrı bir yol değil; olmalı. Maliyet: düşük.
3. **`[Create/Remove]UninstallerRegistryEntry` simetrisi.** Yazan her metodun sil karşılığı aynı
   adla yanında. Runly'de `UninstallOptions` / `UninstallResult` testle sabitlenebilir.

## 5. Kaçınılacak hata

**Issue #1805 — "Shortcut 'Start in' property points to incorrect path after update"** (açık,
2022-05-18, Squirrel 2.0.1). Güncellemeden sonra masaüstü kısayolunun **Start in** alanı ilk
kurulumun `app-1.0.0` klasörünü göstermeye devam ediyor; klasör güncellemede silindiği için
Özellikler → Uyumluluk sekmesi hata veriyor, "Yönetici olarak çalıştır" ayarlanamıyor. Kısayolun
**hedefi** sabit `Update.exe` shim'i olduğundan uygulama yine açılıyor — hata yıllarca görünmüyor.

Ders Runly için birebir: yol tutan **her** alan sayılmalı. Dört ProgID verb'i (open, runas, edit,
runlyargs), Applications altındaki open komutu, DefaultIcon ve varsa çalışma dizini ayrı ayrı
güncellenmeli. Biri unutulursa sonuç #1805'in aynısı: yarı çalışan, teşhisi zor bir kurulum.

İkinci ders #788'den: Squirrel dosya ilişkilendirmeyi hiç üstlenmedi, "kendi olay handler'ında yap"
dedi; sonuç, yorumlarda dolaşan üçüncü taraf `.reg` çözümleri ve kaldırmada neyin silineceğini
kimsenin bilmemesi. Runly'nin bunu `RegistryBackup` + `UninstallOptions` ile merkezde tutması doğru.

## 6. Doğrulama

- Kaynaktan okundu: künye (API), README başı, `docs/using/install-process.md`,
  `docs/using/custom-squirrel-events.md`, issue #1805 gövdesi, #788 gövdesi ve yorumları.
- **Doğrulanamadı:** #1805'in sonraki sürümlerde düzelip düzelmediği (son etiket 2020'den, issue
  2022'de açılmış ve kapanmamış); issue'daki ekran görüntüsü açılmadı; halef proje ilişkisi.
