# Tarama 01 — Windows dosya ilişkilendirme yöneticileri

Tarama tarihi: 2026-08-22. Tüm depo rakamları `gh api repos/<owner>/<repo>` ve
`.../releases/latest` çağrılarından alındı; doğrulanamayan her şey ayrıca işaretlendi.

Aranan dört düğüm: (1) UserChoice hash duvarı, (2) kullanıcıyı Ayarlar'a gönderme,
(3) geri alma/yedek, (4) uzantı kataloğunun kaynağı.

---

## 1. sumatrapdfreader/sumatrapdf

GPL-3.0 · 17.371 yıldız · 89 açık issue · son push 2026-08-22 · son sürüm `3.6.1rel` (2026-04-06)

PDF/e-kitap okuyucu; `src/RegistryInstaller.cpp` dosyası tek başına Runly'nin çözdüğü
problemin referans uygulaması: HKCU/HKLM altında ProgID üretimi, `Capabilities\FileAssociations`,
`RegisteredApplications` ve varsayılan olma denetimi.

**Runly'ye alınacak fikir — üç katmanlı "varsayılan mıyım" sorgusu.**
Kod önce `IApplicationAssociationRegistration::QueryAppIsDefault(ext, AT_FILEEXTENSION, AL_EFFECTIVE, …)`
soruyor; başarısızsa aynı arayüzün `QueryCurrentDefault` çağrısıyla ProgID alıp kendi
şemasıyla karşılaştırıyor; o da olmazsa `AssocQueryStringW(ASSOCSTR_EXECUTABLE)` ile
çözülen exe yolunu kendi yoluyla kıyaslıyor. Registry'den `UserChoice` okumak yalnızca
"kim aldı" sorusunu cevaplamak için kullanılıyor, "varsayılan mıyım" sorusu için değil.

**İkinci fikir — `SHOpenWithDialog` önce, `ms-settings` sonra.**
`LaunchDefaultAppDialogForExtension` sahte bir dosya adı (`document.pdf`, diskte olması
gerekmiyor) ile `SHOpenWithDialog`'u `OAIF_FORCE_REGISTRATION | OAIF_REGISTER_EXT |
OAIF_ALLOW_REGISTRATION` bayraklarıyla açıyor. Yalnızca HRESULT başarısızsa
`ms-settings:defaultapps?registeredAppUser=<ad>` (HKCU kaydı varsa) /
`?registeredAppMachine=<ad>` (HKLM kaydı varsa) / çıplak `ms-settings:defaultapps`
sırasıyla düşüyor. Deep link biçimi hangi kovanda kayıtlı olduğuna göre seçiliyor —
Runly'nin `MainForm.cs:1791` satırındaki tek biçimli kullanım bu ayrımı yapmıyor.

**Kaçınılacak hata / kaydedilmiş itiraf.** Dosya içindeki uzun yorum bloğu
`FileExts\.pdf\UserChoice\Progid` için açıkça "bu anahtara yazılamaz — o yüzden siliyoruz"
diyor. Aynı blokta `FileExts\.pdf\Progid` için "başka hiçbir uygulama bunu yazmıyor gibi,
yalnızca UserChoice fark yaratıyor — bu hâlâ Windows XP için mi gerekli, bilmiyoruz"
notu var. Yani proje kendi eski yazımlarının hangisinin hâlâ işe yaradığını bilmiyor ve
temizlik fonksiyonu (`UnregisterFromBeingDefaultViewer`) bu belirsiz anahtarları da
silmek zorunda kalıyor. Ders: yazdığın her anahtarı **neden** yazdığını kayıt altına al,
yoksa kaldırma kodu tahmin yürütür.

---

## 2. d2phap/ImageGlass

GPLv3 (LICENSE dosyası ayrıca Pro/ticari sürüm ve **marka** için ayrı şartlar tanımlıyor;
GitHub API lisansı "Other" olarak raporluyor) · 14.098 yıldız · 224 açık issue ·
son sürüm `10.0.4.819` (2026-08-21)

Görüntü görüntüleyici; `source/ImageGlass.Win32/Common/WinAPI/Win32DefaultAppApi.cs`
dosyası çok uzantılı bir uygulamanın HKCU/HKLM ikilisini nasıl yönettiğinin güncel örneği.

**Runly'ye alınacak fikir — UCPD'yi silme yoluyla aşma.**
`ClearUserChoice` fonksiyonundaki not, doğrudan Runly'yi ilgilendiriyor: `reg.exe` ve
PowerShell `Remove-Item` `UserChoice` üzerinde reddediliyor, ama **üst anahtarı yazılabilir
açıp `DeleteSubKey("UserChoice")` çağırmak geçiyor.** Hash yazmadan, sadece rakip
UserChoice'ı düşürüp klasik `Software\Classes\.<ext>` varsayılanını devreye sokuyorlar.
Silme işlemi `onlyIfProgId` parametresiyle koşullandırılabiliyor — kaldırma sırasında
yalnızca kendi ProgID'sini gösteren UserChoice siliniyor, başkasınınki bırakılıyor.

**İkinci fikir — kovan seçimi kurulum yolundan türetiliyor.**
`GetScope()`: exe `Program Files`/`Program Files (x86)` altındaysa HKLM, değilse HKCU;
MSIX paketliyse her zaman HKCU (paketli exe HKLM için yükseltilmiş olarak yeniden
başlatılamıyor). Kullanıcıya "yönetici olarak mı çalıştırayım" sorusu sorulmuyor, karar
kuruluma bakılarak veriliyor; HKLM gerektiğinde `RelaunchElevatedAsync` kendi exe'sini
`SET_DEFAULT_PHOTO_VIEWER;<ext;ext;ext>` argümanıyla yükseltip bekliyor.

**Kaçınılacak hata.** ProgID şeması `ImageGlass.AssocFile.<EXT>`; `Capabilities\FileAssociations`
altındaki değer bu ProgID'yi işaret ediyor. SumatraPDF'in aynı dosyadaki yorumu bu noktada
uyarıyor: `FileAssociations` altındaki ProgID HKCR'de çözülemezse **Ayarlar > Varsayılan
uygulamalar arayüzü uygulamayı hiç göstermiyor.** Yani ProgID kaydı ile Capabilities kaydı
tek işlemde ve aynı isimle yazılmazsa hata sessiz oluyor: registry doğru görünüyor, UI boş.

---

## 3. DanysysTeam/PS-SFTA

Depoda LICENSE dosyası **yok** (GitHub API `license: null`); README "MIT" diyor —
çelişki, kullanmadan önce çözülmeli · 379 yıldız · 11 açık issue ·
son kod push 2022-10-10 · **hiç etiketli sürüm/tag yok**, README rozeti v1.2.0 diyor

SetUserFTA'nın hash algoritmasını saf PowerShell'e taşıyan betik: `Set-FTA`, `Set-PTA`,
`Register-FTA`. UserChoice hash'ini hesaplayıp anahtara yazan tarafın açık kaynak temsilcisi.

**Kaçınılacak hata — bu yol iki kez çöktü, bakımcı devam etmiyor.**
Açık issue #37 ("New, unsupported method of preventing user choices: UserChoiceLatest",
2025-06-14) `Register-FTA`'nın artık çalışmadığını bildiriyor. Aynı issue altında
Sophia Script bakımcısı farag2 kendi bypass'ını gösteriyor ama sonra "bu gerçekten yeni
bir sorun" diyor; PS-SFTA'nın yazarı Danyfirex ise onarmayı reddediyor ("tamir etmekle
ilgilenmiyorum, yapabileceğimden de emin değilim"). Açık issue listesindeki #33
("Write Reg Protocol UserChoice FAILED"), #34 ("masaüstündeki tüm ikonlar yanıp sönüyor"),
#28 ("SystemSettings.exe ayarladığım protokolü bazen siliyor") aynı yolun kırılganlık
imzaları. **Runly hash yazmaya kalkarsa varacağı yer burası.**

**Yine de alınacak tek şey — ApplicationAssociationToasts.**
CHANGELOG 1.2.0 (2022-04-17): "hiçbir varsayılan uygulama seçilmediğinde OpenWith.exe'nin
gösterilmemesi için `ApplicationAssociationToasts` tazeleniyor." Sophia da aynı anahtara
`<ProgID>_<ext>` adıyla DWORD 0 yazıyor. Bu, "Bu dosyayı nasıl açmak istersiniz?" balonunun
Runly'nin ilk bağlamasından hemen sonra çıkmasını engelleyen ucuz bir dokunuş.

---

## 4. SetUserFTA / DanysysTeam/SFTA

`DanysysTeam/SFTA`: MIT · PureBasic · 72 yıldız · 1 açık issue · son push 2021-03-11.
Asıl ürün `SetUserFTA` **kapalı kaynak ve ticari** (kolbi.cz duyurusu: "SetUserFTA artık
ticari bir ürün olduğu için indirme mevcut değil").

UserChoice hash'ini kırıp yazan orijinal araç (2017). Kurumsal dünyada fiilî standart.

**Kaçınılacak hata — çalışma bağlamı ve zamanlama tuzağı.**
Yazarın kendi belgelediği sınırlar: araç **SYSTEM olarak değil, kullanıcı bağlamında**
çalışmalı; kullanıcı profili yüklendikten *sonra* çalışmalı; bazı uygulamalar için
`HKCU\SOFTWARE\Classes` altına önceden kayıt (pre-staging) gerekiyor. Sürüm 1.8.1'in
"UCPD.sys etkinken çalıştığı" iddiası yalnızca satıcının kendi sayfasında — **bağımsız
doğrulanamadı**. Runly açısından ders: hash'i doğru hesaplasan bile "ne zaman, hangi
bağlamda, hangi ön kayıtlarla" sorusu ayrı bir kırılganlık yüzeyi.

**UserChoiceLatest (kaynak: kolbi.cz, 2025-04-20 — üçüncü taraf blog, MS belgesi yok,
şüpheli).** Windows 11'de A/B testiyle çıkan yeni koruma: mevcut UserChoice hash'leri
doğrulanıp yeni algoritmayla `UserChoiceLatest` altına yazılıyor; geçiş tamamlandıktan
sonra Windows eski `UserChoice` anahtarını **yok sayıyor**. Yeni hash makine kimliğini de
içeriyor (profil roaming'i bozuluyor). Blog `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\
SystemProtectedUserData\<SID>\AnyoneRead\AppDefaults` altındaki `HashVersion` değeriyle
geri göç ve ViveTool feature ID'leri (43229420, 27623730) ile kapatma öneriyor —
**hiçbiri desteklenen yol değil, Runly'nin kullanacağı bir şey değil.** Ama Runly'nin
*teşhis* için `UserChoiceLatest` anahtarının varlığına bakması anlamlı: varsa,
`UserChoice` okumasına dayanan tüm mantık yanlış cevap veriyor.

---

## 5. default-username-was-already-taken/set-fileassoc

Unlicense (OSI onaylı kamu malı muadili) · 40 yıldız · 0 açık issue ·
son push ve tek sürüm `v1.0.0` → **2020-09-26**, o gün bugün dokunulmamış

SetUserFTA'nın kapalı kaynak ve tek kullanıcılık olmasına tepki olarak yazılmış PowerShell
betiği; `-CurrentUser`, `-AllUsers`, `-Users user1,user2` ile başka kullanıcıların
kovanlarını yükleyip yazabiliyor.

**Runly'ye alınacak fikir — GPO/XML yolunun neden yetmediğinin net dökümü.**
README, Microsoft'un desteklediği `DISM /Export-DefaultAppAssociations` + GPO XML yolunun
üç somut sınırını sayıyor: her Feature Update'te listeyi elle güncellemek gerekiyor;
makine domain'de değilse ilişkilendirmeler ancak referans imajında ayarlanabiliyor;
kullanıcı bir kez elle değiştirdikten sonra XML yöntemi geri alamıyor. Runly'nin
"neden bir araç gerekiyor" gerekçesi tam olarak bu üç madde — belgeye böyle yazılabilir.

**Kaçınılacak hata — hukuki uyarıyı kendi README'sine koymuş.**
"Is that... legal?" başlığı altında: betik Windows ikililerinin tersine mühendisliğinin
ürünü, Microsoft'un kurcalamayı önleme tedbirlerini atlatıyor, EULA'ya sıkı bağlı
kurumlarda sorun olabilir, hukuk biriminize danışın. Runly ticari/kurumsal ortamda
kullanılacaksa hash yazma yolunu seçmemesinin ikinci gerekçesi bu — teknik değil, hukuki.

---

## 6. farag2/Sophia-Script-for-Windows

MIT · 9.666 yıldız · 0 açık issue · son sürüm `7.2.0` (2026-07-31) · son push 2026-08-22

Windows 10/11 ince ayar modülü. `Sophia.psm1` içindeki `Set-Association`, `Export-Associations`,
`Import-Associations` fonksiyonları bu taramanın en kapsamlı üçlüsü.

**Kaçınılacak hata — UCPD'yi exe kopyalayarak aşmak.**
`Set-Association` fonksiyonu, kendi yorumuyla: "Microsoft KB5034765 ile `.pdf` uzantısı ve
http/https protokolleri için UserChoice anahtarına yazma erişimini kapattı, bu yüzden
değerleri UCPD sürücü kısıtlarını aşmak için `powershell.exe`'nin bir kopyasıyla yazıyoruz."
Fonksiyon `powershell.exe`'yi **`System32` içine `powershell_temp.exe` adıyla kopyalıyor**,
tüm registry yazımlarını o kopya üzerinden yapıp sonunda siliyor. UCPD.sys çalıştırılabilirin
adını izlediği için ad değiştirmek yetiyor. Bu Runly için **kesin kırmızı çizgi olmalı**:
System32'ye imzasız kopya bırakmak EDR/AV için birebir kötü amaçlı yazılım imzası, ayrıca
kopyalama başarısız olduğunda fonksiyon sessizce yanlış davranıyor. Ders alınacak kısım
mekanizma değil, **teşhis**: KB5034765 (Şubat 2024) sonrası UCPD.sys var ve yalnızca belirli
uzantı/protokolleri koruyor.

**Runly'ye alınacak fikir — semantik yedek (Export/Import-Associations).**
`Export-Associations` iki kaynağı birleştiriyor: `Dism.exe /Online /Export-DefaultAppAssociations`
ile üretilen XML'den uzantı listesini alıyor, sonra her ProgID için HKCU/HKLM
`Classes\<ProgID>\shell\open\command` ve `DefaultIcon` değerlerini çözüp **`Application_Associations.json`**
yazıyor. UWP ProgID'lerini `HKCR\Local Settings\…\AppModel\PackageRepository\Extensions\ProgIDs`
listesine bakarak ayırıyor ve `DelegateExecute` taşıyanların yolunu boş bırakıyor.
`Import-Associations` JSON'ı okuyup her satır için `Set-Association` çağırıyor.
Dokümantasyonundaki dürüst not: "geri yüklemek için JSON'daki tüm uygulamaları kurmuş
olman gerekir." Yani yedek registry görüntüsü değil, **yeniden uygulanabilir niyet listesi**.

**Uzantı kataloğunun kaynağı:** gömülü liste değil — `DISM /Online /Export-DefaultAppAssociations`
çıktısı. Windows'un kendi varsayılan ilişkilendirme tablosu, sürüme göre kendiliğinden güncel.

---

## 7. Sophia-Community/SophiApp

MIT · 5.149 yıldız · 7 açık issue · son push 2026-08-14 · **son etiketli sürüm `1.0.97`
(2023-07-27)** — kod üç yıldır aktif ama yayınlanmış sürüm yok

Sophia Script'in WPF arayüzlü kardeşi. Runly'ye asıl katkısı depo yapısı değil, bu tarih
uyuşmazlığının kendisi.

**Kaçınılacak hata — "aktif depo" ile "yayınlanmış sürüm" aynı şey değil.**
`pushed_at` 2026-08-14, `releases/latest` 2023-07-27. Kullanıcının indirdiği ikili üç yıllık;
`develop` dalındaki düzeltmeler kimseye ulaşmıyor. Aynı organizasyonun script tarafı
(§6) düzenli etiketleniyor, GUI tarafı etiketlenmiyor. Runly 0.2.0'da yayınlanmamışken
bu bir uyarı: GUI ürününde "yayınlamadan geliştirmeye devam etme" varsayılan çürüme yolu.

---

## 8. microsoft/PowerToys

MIT · 137.958 yıldız · **7.514 açık issue** · son sürüm `v0.100.2` (2026-06-26) ·
son push 2026-08-22

Doğrudan dosya ilişkilendirme modülü yok; alınacak şey ayarlar uygulaması deseni.

**Runly'ye alınacak fikir — ayar yedeğini birinci sınıf özellik yapmak.**
`src/settings-ui/Settings.UI.Library/SettingsBackupAndRestoreUtils.cs` singleton bir
yardımcı: son yedeğin var olup olmadığını, ne zaman çalıştığını ve sonucun
(`Success, Severity, LastBackupExists, LastRan`) durumunu tutuyor; yedekleme ve eski
yedekleri temizleme ayrı kilitlerle korunuyor; geri yükleme JSON **merge** ile yapılıyor
(dizi öğelerinde tekrar üretmeyecek biçimde), tam üzerine yazma ile değil. Arayüz tarafı
`GeneralPage`/`GeneralViewModel` üzerinden bu durumu kullanıcıya gösteriyor.
Runly'nin `RegistryBackup` sınıfı `.reg` dosyaları üretiyor ama "son yedek ne zaman alındı,
başarılı mıydı" durumu GUI'de aynı ağırlıkta değil.

**Kaçınılacak hata / ölçek uyarısı.** 7.514 açık issue, MIT lisansın bakım yükünü
çözmediğini gösteriyor. PowerToys'un modül başına ayrı süreç + ayrı `settings.json`
mimarisi Runly'nin tek pencereli ölçeği için fazla; kopyalanacak olan yedek/geri yükleme
durum modeli, mimari değil.

---

## 9. chocolatey/choco

Apache-2.0 (LICENSE dosyası doğrulandı; GitHub API "NOASSERTION" diyor çünkü depoda ayrıca
marka/isim kullanımını kısıtlayan ek metin var) · 11.489 yıldız · 514 açık issue ·
son push 2026-08-19

`Install-ChocolateyFileAssociation.ps1` — paket kurulumu sırasında uzantı bağlayan yardımcı.

**Kaçınılacak hata — `assoc`/`ftype` yolu UserChoice'ı hiç görmüyor.**
Fonksiyon yönetici hakkı isteyip `cmd /c assoc .ext=FileType` ve `cmd /c ftype FileType="exe" "%1" "%*"`
çalıştırıyor, sonra `HKCR:\FileType` varsayılan değerini yazıyor. Yani **yalnızca makine
genelindeki klasik varsayılanı** kuruyor. Kullanıcıda o uzantı için bir `UserChoice` varsa —
Windows 8'den beri neredeyse her yaygın uzantıda var — kurulum "başarılı" raporlayıp
hiçbir davranış değişmiyor. Sessiz başarısızlık: kullanıcı çift tıklar, eski uygulama açılır,
hiçbir hata yok. Runly'nin durum modelinde bu vaka zaten `NeedsUserChoice` olarak var;
choco'nun hatası bu ayrımı hiç yapmamış olması.

**İkinci kaçınılacak hata — geri alma yok.**
Depoda `Uninstall-ChocolateyFileAssociation` diye bir fonksiyon **yok** (helpers/functions
dizininde `assoc` eşleşen tek dosya `Install-ChocolateyFileAssociation.ps1`). 2015-03-10
tarihli issue #161 çoklu uzantı desteğini, 2020-04-06 tarihli issue #2028 ise
"Install fonksiyonlarının oluşturduğu öğeleri takip et" başlığıyla kurulum yan etkilerinin
hiç izlenmediğini bildiriyor — ikisi de **hâlâ açık**. Yani paket kaldırıldığında
ilişkilendirme registry'de kalıyor.

---

## 10. Belphemur/SoundSwitch

GPL-2.0 · 3.355 yıldız · 102 açık issue · son sürüm `v7.2.0` (2026-08-20)

Dosya ilişkilendirme değil ses cihazı aracı, ama aynı sınıf problem: Windows'un
belgelenmemiş/korunan bir varsayılanını (varsayılan ses çıkışı) programatik değiştirmek.

**Runly'ye alınacak fikir — sürüme göre daralan arayüz zinciri, tek bir çağrı noktasında.**
`SoundSwitch.Audio.Manager/Interop/Client/PolicyClient.cs` tek bir COM nesnesini
(`_PolicyConfigClient`) üç ayrı belgelenmemiş arayüze cast etmeye çalışıyor:
`IPolicyConfigX`, `IPolicyConfig`, `IPolicyConfigVista`. Hangisi null değilse o kullanılıyor;
hepsi tek `SetDefaultEndpoint` metodunun arkasında. Ayrıca `ExtendedPolicyClient` daha yeni
`IAudioPolicyConfig` yolunu bir factory üzerinden **tembel** kuruyor ve Serilog ile hangi
yolun seçildiğini kaydediyor. COM hata kodları maskeleniyor
(`COM_ERROR_NOT_FOUND` → alan-anlamlı `DeviceNotFoundException`).

Runly karşılığı: `UserChoice` / `UserChoiceLatest` / `IApplicationAssociationRegistration` /
`SHOpenWithDialog` seçenekleri tek bir arayüzün ardında sıralanmalı, hangi yolun seçildiği
loglanmalı, ham HRESULT kullanıcıya değil log'a gitmeli.

**Kaçınılacak hata.** Belgelenmemiş COM arayüzüne bağlanmak sürüm başına yeni bir cast
dalı demek — depo üç dal biriktirmiş ve 102 açık issue'nun bir kısmı bu yüzeyden geliyor
(issue dağılımı tek tek doğrulanmadı). Belgelenmemiş yol seçilecekse **en baştan** sürüm
tespiti + fallback + log üçlüsüyle seçilmeli, sonradan eklenmiyor.

---

## 11. NirSoft FileTypesMan

Kapalı kaynak **freeware** ("ücret almadığın sürece serbestçe dağıtabilirsin" — OSI onaylı
lisans değil, türev çalışma hakkı yok) · sürüm 2.01 · telif 2008-2026
(sürüm tarihleri yalnızca satıcı sayfasından, bağımsız **doğrulanamadı**)

Windows'un en yaygın kullanılan GUI dosya türü yöneticisi. `HKEY_CLASSES_ROOT` (uzantılar,
dosya türleri, uygulamalar) ve `HKCU\…\Explorer\FileExts` (UserChoice girdileri) alanlarını
düzenliyor.

**Runly'ye alınacak fikir — ağır işi kapatılabilir yapmak.**
Belgelenmiş bilinen sorunlar: ikon yüklerken çökebiliyor/donabiliyor, çare komut satırı
anahtarı `/DontLoadIcons`; ikon seçme diyaloğu bazı Windows 10 sistemlerinde kilitleniyor.
Yani listedeki en pahalı işlem (her uzantı için shell ikonunu çözmek) kullanıcı tarafından
kapatılabilir bir anahtarın arkasında. Runly'nin uzantı ızgarası da ikon çözüyor;
aynı kaçış kapısı ucuz sigorta.

**Kaçınılacak hata.** Belge UserChoice'ın "çift tıklamada varsayılan eylemi ezdiğini"
kabul ediyor ama Windows 8/10/11'in hash korumasını hiç ele almıyor. Sonuç: araç
UserChoice'ı düzenleyebiliyor görünüyor, kullanıcı düzenliyor, Windows hash uyuşmazlığı
yüzünden girdiyi yok sayıyor — ve kullanıcı bunu ancak çift tıklayınca anlıyor.
**Bir ayarı yazabiliyor olmak, onun etkili olduğu anlamına gelmiyor;** yazımdan sonra
etkinlik ayrıca doğrulanmalı.

---

# Runly için sonuç

1. **Hash yazma yolunu kalıcı olarak reddet, gerekçesini belgeye yaz.** PS-SFTA issue #37'de
   yazarın bakımı bıraktığı, UserChoiceLatest'in eski anahtarı yok saydığı ve set-fileassoc
   README'sindeki EULA uyarısı üç ayrı gerekçe. Runly'nin `SHOpenWithDialog` + `Applications\Runly.exe`
   dolaylı bağlama stratejisi doğru olan; bu kararın *neden* alındığı `docs/` içinde
   kaynaklarıyla dursun ki altı ay sonra tekrar tartışılmasın.

2. **"Varsayılan mıyım" sorusunu `IApplicationAssociationRegistration` ile sor.**
   Bugün `UserChoiceInspector` registry okuyor. SumatraPDF'in üç katmanlı sırası
   (`QueryAppIsDefault` → `QueryCurrentDefault` → `AssocQueryStringW`) UserChoiceLatest
   geçişinde de doğru cevap verir, çünkü kabuğun kendi çözümlemesini kullanır.
   Registry okuması "kim aldı" sorusuna (`UserChoiceOwnerName`) indirgensin.

3. **`UserChoiceLatest` anahtarının varlığını teşhis olarak ekle.** Varsa, `UserChoice`
   temelli her okuma şüpheli; kullanıcıya "bu makinede Windows yeni bir koruma kullanıyor,
   bağlama yalnızca Birlikte aç diyaloğuyla yapılabilir" denilebilir. Yazma denemesi yok,
   yalnızca okuma ve durum mesajı.

4. **Rakip UserChoice'ı silme yolunu değerlendir — ama yalnızca kendi ProgID'miz için.**
   ImageGlass'ın bulgusu: üst anahtarı yazılabilir açıp `DeleteSubKey("UserChoice")`
   `reg.exe`'nin reddedildiği yerde geçiyor. Runly için asıl değeri **kaldırmada**:
   `OrphanedUserChoice.Removed` bugün false kalabiliyor, bu yöntemle true yapılabilir.
   Koşul mutlaka `onlyIfProgId == Runly ProgID` olmalı — başkasının seçimini silmek
   kullanıcı verisi silmektir.

5. **`ms-settings:defaultapps` deep link'ini kovan durumuna göre seç.** Bugün
   `MainForm.cs:1791` sabit `registeredAppUser=Runly` kullanıyor. HKCU
   `RegisteredApplications` altında Runly yoksa bu link boş sayfa açar; SumatraPDF'in
   sırası (HKCU → `registeredAppUser`, HKLM → `registeredAppMachine`, yoksa çıplak URI)
   üç satırlık bir düzeltme. `ftfilter=<ext>` kullanımı zaten doğru; `KNOWN-ISSUES.md`
   satır 68'deki tespitle çelişmiyor.

6. **Yedeği `.reg`'in yanına semantik JSON olarak da al.** `.reg` dosyası UserChoice'ı geri
   yükleyemez (hash tutmaz) — Sophia'nın `Application_Associations.json` deseni
   (uzantı → ProgID → exe yolu → ikon) "yeniden uygulanabilir niyet listesi" olduğu için
   yükleyebilir. Sophia'nın dürüst uyarısı da alınmalı: geri yükleme, hedef uygulamaların
   kurulu olmasını gerektirir; kurulu değilse satır atlanıp kullanıcıya bildirilmeli.

7. **Uzantı kataloğunun tek kaynağı gömülü `DefaultConfig.cs` kalmasın.**
   `DISM /Online /Export-DefaultAppAssociations` çıktısı, Windows'un kendi sürümüne uygun
   uzantı listesini veriyor ve Feature Update'lerle kendiliğinden güncelleniyor; set-fileassoc
   README'sinin saydığı "her güncellemede listeyi elle takip etme" derdini ortadan kaldırır.
   Gömülü liste öneri/varsayılan olarak kalsın, katalog ondan beslensin.

8. **Yedek durumunu GUI'de birinci sınıf göster ve ikon çözmeyi kapatılabilir yap.**
   PowerToys'un `(Success, Severity, LastBackupExists, LastRan)` dörtlüsü kullanıcının
   "geri dönebilir miyim" sorusunu tek bakışta cevaplıyor. Ayrıca FileTypesMan'in
   `/DontLoadIcons` dersi: uzantı ızgarasındaki ikon çözümlemesi ayarla kapatılabilsin,
   çünkü bozuk bir shell ikon sağlayıcısı tüm pencereyi kilitleyebiliyor.

---

## Kaynaklar

- `gh api repos/{sumatrapdfreader/sumatrapdf, d2phap/ImageGlass, DanysysTeam/PS-SFTA,
  DanysysTeam/SFTA, default-username-was-already-taken/set-fileassoc,
  farag2/Sophia-Script-for-Windows, Sophia-Community/SophiApp, microsoft/PowerToys,
  chocolatey/choco, Belphemur/SoundSwitch}` ve `/releases/latest` — 2026-08-22
- `sumatrapdfreader/sumatrapdf` → `src/RegistryInstaller.cpp`
- `d2phap/ImageGlass` (develop) → `source/ImageGlass.Win32/Common/WinAPI/Win32DefaultAppApi.cs`, `LICENSE`
- `farag2/Sophia-Script-for-Windows` (master) → `src/Sophia_Script_for_Windows_11/Module/Sophia.psm1`
- `DanysysTeam/PS-SFTA` → `README.md`, `CHANGELOG.md`, issue #37 ve açık issue listesi
- `default-username-was-already-taken/set-fileassoc` → `README.md`
- `chocolatey/choco` (develop) → `src/chocolatey.resources/helpers/functions/Install-ChocolateyFileAssociation.ps1`,
  `LICENSE`, issue #161 ve #2028
- `microsoft/PowerToys` (main) → `src/settings-ui/Settings.UI.Library/SettingsBackupAndRestoreUtils.cs`
- `Belphemur/SoundSwitch` (dev) → `SoundSwitch.Audio.Manager/Interop/Client/PolicyClient.cs`,
  `ExtendedPolicyClient.cs`
- kolbi.cz, "SetUserFTA – UserChoice hash defeated" (2017-10-25) — satıcı blogu, şüpheli
- kolbi.cz, "UserChoiceLatest: Microsoft's new protection for file type associations"
  (2025-04-20) — üçüncü taraf blog, Microsoft belgesi yok, şüpheli
- nirsoft.net/utils/file_types_manager.html — satıcı sayfası, sürüm/tarih doğrulanamadı
- Runly kendi kaynağı: `src/Runly.Core/Shell/`, `src/Runly.Settings/MainForm.cs`,
  `docs/KNOWN-ISSUES.md`, `docs/CLAUDE-HANDOFF.md`
