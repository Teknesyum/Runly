# Toplu Atama Engelleri — Hangi Uzantılar Neden Atanamıyor

Tarih: 2026-09-29. Araştırma ajanın (opus) yanıtı, kelimesi kelimesine. Uygulanan karar: SPEC K34.

## Uygulananlar

- `BulkAssociationCommand.Excluded`: `.pdf .htm .html .doc .docx .xls .xlsx .ppt .pptx .reg .hta` (UCPD + risk).
- `RunlyRegistryLayout.BlockedExtensions` (`.bat .cmd .exe .lnk …`) zaten komuta girmiyor.
- Komut her uzantıda `FileExts\<ext>\UserChoiceLatest` varsa yazmadan atlar, uyarı verir.
- Her atamadan sonra `Get-FTA` ile doğrular, `n/toplam` raporlar.
- Deneme (atama yapmadan, bu makinede): `Butunluk tamam, Set-FTA yuklu: True` / `Mevcut .py: Runly.Script.py`.

## Ajanın Yanıtı

**"Bazı uzantılarda yapamıyorduk" diye bir kayıt yok.** Runly bazı uzantıları bilerek kendisi reddediyor. `.exe .dll .sys .com .bat .cmd .msi .msc .cpl .scr .lnk .pif .url .job .ocx .drv` listesi `src/Runly.Core/Shell/RunlyRegistryLayout.cs:9-13`'te duruyor, commit `c841c47` ile geldi. `catalog.json` bunları `"blocked": true, category: "locked"` olarak işaretliyor (5865-5967 arası satırlar). Tek kayıtlı "bağlanamadı" durumu K19/K23: hiçbir uzantı tek tıkla bağlanamıyor, `SHOpenWithDialog` yalnızca "Yalnızca bir kez" sunuyor (`docs/KNOWN-ISSUES.md:7-14`).

**Bu makinede ölçülenler (salt okunur, kayda yazılmadı):**
- Windows 11 23H2, derleme 22631.6199.
- UCPD çalışıyor, otomatik başlıyor, `FeatureV2=7086`. `\Microsoft\Windows\AppxDeploymentClient\UCPD velocity` görevi hazır durumda.
- Katalogdaki 414 uzantının hiçbirinde `UserChoiceLatest` yok; http ve https'te de yok. `AppDefaults\HashVersion` değeri de yok, yani yeni hash'e geçilmemiş.
- Runly'ye bağlı olanlar: `.mjs .cjs .ps1 .py .svg`. `.md` ve `.json` `Applications\Runly.exe`'ye bağlı.
- `.js` için UserChoice yok, yalnızca HKCU'daki varsayılan Runly'yi gösteriyor. Yani bağlı değil.
- `.pdf` NitroPDF'e, http/https ChromeHTML'e bağlı. `.bat .cmd .exe .reg .hta .vbs .lnk` için UserChoice anahtarı yok.
- `HKLM\...\Explorer\FileAssociation` altındaki `UseLocalMachineSoftwareClassesWhenImpersonating` değeri: `.bas .hta .js .msi .ps1 .reg .vb .vbs .wsf`. Bu uzantılarda yükseltilmiş ya da kimliğe bürünmüş bağlamda HKCU kaydı devre dışı kalıyor. Bu değerin anlamını adından çıkarıyorum; dokümante edilmiş kaynak bulamadım.

| Uzantı | Engel | Neden | PS-SFTA aşar mı | Kaynak |
|---|---|---|---|---|
| .exe .com .scr .cpl .msi .dll .sys | Var (Runly'nin kendi engeli) | Çalıştırılabilir ya da sistem türü; Runly bilerek reddediyor | Aşmamalı. Bozulursa sistem açılmaz hale gelebilir | `RunlyRegistryLayout.cs:11` |
| .bat .cmd | Var (Runly + Windows arayüzü) | Explorer'da "Birlikte aç" menüsü hiç yok. Windows kendi kararıyla gizliyor | Teknik olarak yazılabilir. Windows'un bu kaydı gerçekten kullanıp kullanmadığı ölçülmedi. Runly ayrıca .bat/.cmd'yi zaten `cmd.exe` ile çalıştırıyor (K16) | [NinjaOne](https://www.ninjaone.com/blog/open-bat-file-context-menu-option-windows-11/), [MS Q&A](https://learn.microsoft.com/en-us/answers/questions/4160269/how-to-find-the-open-with-option-for-bat-files), `SPEC.md:339` |
| .lnk .pif .url | Var | Kısayol türleri (`IsShortcut`, ölçüldü) | Hayır, dokunulmamalı | `RunlyRegistryLayout.cs:12` |
| .pdf, http, https | Var (UCPD) | UCPD, powershell.exe, pwsh.exe, reg.exe, regedit.exe, cmd.exe, rundll32.exe, dllhost.exe, wscript.exe, cscript.exe ve WmiPrvSE.exe'nin yazmasını engelliyor | Hayır. PS-SFTA issue #36 ve #33 aynı hatayı veriyor: "Write Reg … UserChoice FAILED" | [kolbi 2024](https://kolbi.cz/blog/2024/04/03/userchoice-protection-driver-ucpd-sys/), [#36](https://github.com/DanysysTeam/PS-SFTA/issues/36), [#33](https://github.com/DanysysTeam/PS-SFTA/issues/33) |
| .htm .html .doc(x) .xls(x) .ppt(x) | Var (UCPD 4.x) | Koruma listesi genişledi. Yeniden adlandırılmış kopyaları da yakalıyor (exe'nin içindeki `OriginalFilename` kaynağına bakıyor) | Hayır | [kolbi 2025 part 2](https://kolbi.cz/blog/2025/07/15/ucpd-sys-userchoice-protection-driver-part-2/) |
| .hta .reg .js .vbs .wsf .ps1 .msi | Kısmi | Yükseltilmiş bağlamda HKCU devre dışı (makinede ölçüldü) | Normal bağlamda evet, yükseltilmiş bağlamda hayır | makine: `Explorer\FileAssociation` |
| Diğer betikler (.py .mjs .rb .lua .sh .jar .chm …) | Yok (23H2'de) | UCPD bu uzantıları korumuyor | Evet. Bu makinede eski hash hâlâ geçerli | ölçüm |
| Hepsi, 24H2/25H2'de | Var olabilir | `UserChoiceLatest` + makineye bağlı yeni hash, A/B testiyle açılıyor (özellik kimlikleri 43229420 ve 27623730). Geçiş tamamlanınca eski `UserChoice` yok sayılıyor | Hayır. Issue #37 açık, bakımcı onarmayacağını söyledi | [kolbi 2025](https://kolbi.cz/blog/2025/04/20/userchoicelatest-microsofts-new-protection-for-file-type-associations/), [#37](https://github.com/DanysysTeam/PS-SFTA/issues/37) |

PS-SFTA'nın diğer açık sorunları: #31 (bağladıktan sonra da "Bu dosyayı nasıl açmak istersiniz?" soruyor), #34 (masaüstü ikonları yanıp sönüyor), #38 (bazı hesaplarda SID çözülemiyor, hata veriyor). Depoda LICENSE dosyası da yok (`01-dosya-iliskilendirme.md:80`).

**Öneriler:**
1. Toplu komut dışında kalmalı: `BlockedExtensions` listesinin tamamı; UCPD'nin koruduğu türler (`.pdf .htm .html .doc(x) .xls(x) .ppt(x)`, http/https); `.bat .cmd`.
2. `.reg .hta .msi` de dışarıda kalmalı, çünkü yüksek riskli.
3. Komut çalışmadan önce iki kontrol yapmalı: herhangi bir uzantıda `UserChoiceLatest` varsa ya da `AppDefaults\HashVersion` 1 ise, hiç yazmadan durup "Ayarlar'dan bağla" yoluna yönlendirmeli.
4. Yazdıktan sonra sonuç her uzantı için yeniden ölçülmeli (UserChoice ve `AssocQueryString` ile, B1 kuralı). Başarısız olanlar `NeedsUserChoice` olarak raporlanmalı.
5. PS-SFTA'nın yaptığı gibi `ApplicationAssociationToasts` altına `<ProgID>_<ext>=0` yazılırsa #31'deki soru balonu çıkmaz.
6. Yazmaya başlamadan önce SPEC'teki yasak (§2, K19) kararla güncellenmeli. Aksi halde kod belgelerle çelişir.
