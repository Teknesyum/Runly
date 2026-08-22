# coreybutler/nvm-windows

## 1. Künye

| Alan | Değer (`gh api`, 2026-08-22) |
|---|---|
| Depo | `coreybutler/nvm-windows` |
| **Lisans** | **MIT** (API). Ayrı marka koruma ifadesi görülmedi. |
| Yıldız / açık issue · son commit (`master`) | 47.438 / 83 · 2026-03-08 (push 2026-04-17) |
| Son etiketli sürüm | `1.2.2` · **2025-01-01** — 19 aydır etiketli sürüm yok. |

## 2. Ne yapıyor

Windows'ta birden çok Node.js sürümünü kurup geçiş yaptıran Go aracı. `nvm use <sürüm>`, `PATH`'te
sabit duran bir dizin bağını hedef sürüm klasörüne yeniden bağlıyor.

## 3. Runly ile kesişimi

**Konsol/GUI ayrımı yok — çünkü başlatma yolunda hiç durmuyor.** Tek konsol ikilisi (`nvm.exe`,
`src/nvm.go` 2.051 satır). Yorumlayıcı bulma, `PATHEXT`, argüman kaçışı, çıkış kodu/Ctrl+C
aktarımı, shim boyutu ve başlatma gecikmesi: hiçbiri bu depoda **yok**. `node.exe` `PATH`'ten
doğrudan bulunuyor; nvm yalnız kurulum ve geçiş anında çalışıyor. `Runly.exe` (GUI) /
`RunlyConsole.exe` (konsol) ayrımının karşılığı burada yok.

**Konsol yanıp sönmesini çözmüyor, sorunu yaşıyor.** Açık issue **#1068**: `nvm-update.exe` çift
tıklandığında "NVM for Windows should be run from a terminal such as CMD or PowerShell" diyor.
Konsol subsystem'li tek ikili Explorer'dan çağrılınca ne yapacağını bilmiyor; cevap kullanıcıyı
terminale yönlendirmek. Runly ikiye ayırarak tersini yaptı — nvm negatif örnek.

**Alternatif çözüm.** `nvm.go:1197`: `mklink /D <symlink> <root>\v<sürüm>`. Sabit `NVM_SYMLINK`
yolu `PATH`'te kalıcı, hedefi değişiyor; `nvm.iss` makine `PATH`'i için `HKLM\…\Session Manager\
Environment`, kullanıcı için `HKCU\Environment` yazıyor (`:116-132`). Yükseltme ayrı ikilide
değil, `elevate.cmd` / `elevate.vbs` yardımcılarında (`nvm.go:1867`).

## 4. Alınacak fikir

1. **Önce yükseltmesiz dene, düşersen yükseltme yardımcısına geç** (`nvm.go:1877-1886`
   `elevatedRun`). HKCU'ya yazamayınca HKLM'e geçme akışı için aynı desen. Maliyet: bir dal.
2. **Yükseltilmişliği tahmin etme, token'dan oku** (`nvm.go:1954-1976`): `token.IsElevated()` ve
   Administrators SID için `token.IsMember(sid)` **ayrı ayrı** dönüyor — "yönetici hesabı" ile
   "yükseltilmiş süreç" farkı Runly hata metinlerinde de lazım. Maliyet: iki API çağrısı.
3. **Bağı silmeden önce gerçekten bağ olduğunu doğrula** (`nvm.go:1263-1280`
   `abortOnBadSymlink`/`validSymlink`) — aksi hâlde `rmdir` gerçek klasörü siler. Maliyet: az.

## 5. Kaçınılacak hata

Açık **#1068**: konsol ikilisini GUI'den çağrılabilir hâle getirmemek, sonra kullanıcıya
"terminalden çalıştır" demek. Runly'nin ikiye ayırma kararının gerekçesi tam da bu.
`runElevated` (`nvm.go:1903+`) `syscall.SysProcAttr{CmdLine: command}` ile **ham komut satırı**
geçiriyor; kod yorumu bunun resmî belgede olmadığını itiraf ediyor ("Based on the official docs …
doesn't exist. But it does and is vital") — belgelenmemiş alana dayanan başlatma yolu.
Açık **#289** ("filename or extension is too long"): uzun yol sınırı. fnm bunu `longPathAware`
manifestiyle karşılıyor; nvm-windows'ta böyle bir manifest görülmedi.

## 6. Doğrulama

Okundu: `src/nvm.go` (2.051 satır; symlink, `elevatedRun`, `runElevated`,
`getProcessPermissions`, `validSymlink`), `nvm.iss` (grep), kök listesi, issue başlıkları, künye.
`doğrulanamadı`: `elevate.cmd`/`elevate.vbs` depo ağacında bulunamadı. `nvm.exe` PE subsystem'i
ikiliden değil koddan çıkarıldı. #1068 ve #289 test edilmedi.
