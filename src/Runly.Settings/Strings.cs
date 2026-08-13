namespace Runly.Settings;

/// <summary>Embedded Turkish/English UI dictionary; avoids satellite assemblies for the AOT launcher.</summary>
internal static class Strings
{
    private static readonly Dictionary<string, (string Tr, string En)> Values = new(StringComparer.Ordinal)
    {
        ["app.title"] = ("Runly Ayarları", "Runly Settings"),
        ["refresh"] = ("Yenile", "Refresh"),
        ["addExtension"] = ("Uzantı ekle", "Add extension"),
        ["removeExtension"] = ("Seçili uzantıyı sil", "Remove selected extension"),
        ["close"] = ("Kapat", "Close"),
        ["save"] = ("Kaydet", "Save"),
        ["restore"] = ("Yedekten geri yükle", "Restore backup"),
        ["uninstall"] = ("Kaldır", "Uninstall"),
        ["install"] = ("Kur / Güncelle", "Install / Update"),
        ["security"] = ("GÜVENLİK", "SECURITY"),
        ["securityInvariant"] = ("GÜVENLIK", "SECURITY"),
        ["behavior"] = ("DAVRANIŞ", "BEHAVIOR"),
        ["details"] = ("AYRINTILAR", "DETAILS"),
        ["enabled"] = ("ETKİN", "ENABLED"),
        ["extension"] = ("UZANTI", "EXTENSION"),
        ["interpreter"] = ("YORUMLAYICI", "INTERPRETER"),
        ["found"] = ("BULUNDU", "FOUND"),
        ["arguments"] = ("ARGÜMANLAR", "ARGUMENTS"),
        ["status"] = ("DURUM", "STATUS"),
        ["bound"] = ("Bağlı", "Bound"),
        ["notBound"] = ("Bağlı değil", "Not bound"),
        ["needsApproval"] = ("Windows onayı gerekiyor", "Windows approval required"),
        ["askWindows"] = ("⚠ Windows'a sor", "⚠ Ask Windows"),
        ["installed"] = ("Runly kurulu", "Runly installed"),
        ["notInstalled"] = ("Runly kurulu değil", "Runly not installed"),
        ["cancel"] = ("Vazgeç", "Cancel"),
        ["add"] = ("Ekle", "Add"),
        ["yes"] = ("Evet", "Yes"),
        ["no"] = ("Hayır", "No"),
        ["ok"] = ("Tamam", "OK"),
        ["retry"] = ("Yeniden dene", "Retry"),
        ["error"] = ("HATA", "ERROR"),
        ["warning"] = ("DİKKAT", "WARNING"),
        ["confirmation"] = ("ONAY", "CONFIRMATION"),
        ["information"] = ("BİLGİ", "INFORMATION"),
        ["runlyInstalledShort"] = ("Runly kurulu", "Runly installed"),
        ["runlyNotInstalledShort"] = ("Runly kurulu değil", "Runly not installed"),
        ["version"] = ("sürüm", "version"),
        ["saved"] = ("Kaydedildi ✓", "Saved ✓"),
        ["securityWarningTitle"] = ("Runly Ayarları — Güvenlik uyarısı", "Runly Settings — Security warning"),
        ["neverAskWarning"] = ("Bu ayarla, çift tıkladığınız her script hiçbir soru sorulmadan çalışır. İnternetten\nindirilmiş dosyalar yine de uyarı gösterir. Devam edilsin mi?", "With this setting, every script you double-click runs without any prompt. Files downloaded\nfrom the internet still show a warning. Continue?"),
        ["selectRow"] = ("Bir satır seçtiğinizde ayrıntılar burada görünür.", "Select a row to see its details here."),
        ["alwaysAsk"] = ("Her seferinde sor", "Ask every time"),
        ["trustFirst"] = ("İlk seferde sor, sonra güven", "Ask once, then trust"),
        ["neverAsk"] = ("Hiç sorma (önerilmez)", "Never ask (not recommended)"),
        ["neverAskShort"] = ("Hiç sorma", "Never ask"),
        ["keepAlwaysShort"] = ("Her zaman", "Always"),
        ["keepErrorShort"] = ("Sadece hata olursa", "Only on error"),
        ["keepNeverShort"] = ("Hiçbir zaman", "Never"),
        ["windowOpen"] = ("P E N C E R E Y İ   A Ç I K   T U T", "K E E P   W I N D O W   O P E N"),
        ["editorCommand"] = ("D Ü Z E N L E Y İ C İ   K O M U T U", "E D I T O R   C O M M A N D"),
        ["trustedFolders"] = ("G Ü V E N İ L E N   K L A S Ö R L E R", "T R U S T E D   F O L D E R S"),
        ["remove"] = ("Çıkar", "Remove"),
        ["clearAll"] = ("Tümünü temizle", "Clear all"),
        ["test"] = ("Test et", "Test"),
        ["logShort"] = ("Günlük tut", "Enable logging"),
        ["openLog"] = ("Günlük klasörünü aç", "Open log folder"),
        ["keepAlways"] = ("Her zaman açık tut", "Always keep open"),
        ["keepError"] = ("Yalnızca hatada açık tut", "Keep open only on error"),
        ["keepNever"] = ("Hiç açık tutma", "Never keep open"),
        ["log"] = ("Günlük kaydı açık", "Enable logging"),
    };

    public static string Language { get; set; } = "tr";
    public static string Get(string key) => Values.TryGetValue(key, out var value) ? (Language == "en" ? value.En : value.Tr) : key;

    public static string Translate(string text)
    {
        foreach (var value in Values.Values)
        {
            if (string.Equals(text, value.Tr, StringComparison.Ordinal)) return Language == "en" ? value.En : value.Tr;
            if (string.Equals(text, value.En, StringComparison.Ordinal)) return Language == "tr" ? value.Tr : value.En;
        }
        return text;
    }

    public static void Apply(Control root)
    {
        // A RichTextBox holds rendered runs (bold, mono, neon-pink code spans), not a translatable
        // caption. Assigning .Text here would flatten every run back to plain body text.
        if (root is RichTextBox)
        {
            return;
        }

        root.Text = Translate(root.Text);
        if (root is NeonGroupPanel group) group.Title = Translate(group.Title);
        if (root is DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns) column.HeaderText = Translate(column.HeaderText);
        }
        foreach (Control child in root.Controls) Apply(child);
        root.Invalidate();
    }
}
