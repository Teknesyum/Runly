using System.Text;

namespace Runly.Core.Shell;

/// <summary>
/// Builds the PowerShell command that makes Runly the default for many extensions in one go (K34).
/// The command downloads PS-SFTA at a pinned commit, checks its SHA256, and calls <c>Set-FTA</c>
/// once per extension. Runly never runs it: the user pastes it into their own shell.
/// </summary>
public static class BulkAssociationCommand
{
    /// <summary>PS-SFTA at a pinned commit, so the hash below cannot drift.</summary>
    public const string ScriptUrl =
        "https://raw.githubusercontent.com/DanysysTeam/PS-SFTA/22a32292e576afc976a1167d92b50741ef523066/SFTA.ps1";

    /// <summary>SHA256 of <see cref="ScriptUrl"/>; the command stops when the download differs.</summary>
    public const string ScriptSha256 =
        "3eb6f6dee3fd8c91604042060b9d658f08ec85d3fd0a14769119dfd78bc30851";

    /// <summary>Project page of the tool, shown to the user.</summary>
    public const string ToolPage = "https://github.com/DanysysTeam/PS-SFTA";

    /// <summary>
    /// Types the command never touches beyond <see cref="RunlyRegistryLayout.IsBlockedExtension"/>: UCPD refuses
    /// PowerShell's write to these (see docs/taramalar/toplu-atama-engelleri.md), and .reg/.hta are too risky.
    /// </summary>
    public static readonly IReadOnlySet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".htm", ".html", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".reg", ".hta",
    };

    /// <summary>Normalised, de-duplicated extensions with the blocked and excluded types removed.</summary>
    public static IReadOnlyList<string> Assignable(IEnumerable<string> extensions) =>
        extensions
            .Select(RunlyRegistryLayout.NormalizeExtension)
            .Where(extension => !RunlyRegistryLayout.IsBlockedExtension(extension) && !Excluded.Contains(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>One-line PowerShell 5.1 command that binds every assignable extension to its Runly ProgID.</summary>
    public static string Build(IEnumerable<string> extensions)
    {
        var assignable = Assignable(extensions);
        var list = new StringBuilder();
        for (var i = 0; i < assignable.Count; i++)
        {
            if (i > 0) list.Append(',');
            list.Append("@('").Append(assignable[i]).Append("','")
                .Append(RunlyRegistryLayout.ProgIdFor(assignable[i])).Append("')");
        }

        return string.Join(
            "; ",
            "Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force",
            "[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072",
            $"$u='{ScriptUrl}'",
            $"$h='{ScriptSha256}'",
            "$p=Join-Path $env:TEMP 'Runly-SFTA.ps1'",
            "Invoke-WebRequest -UseBasicParsing -Uri $u -OutFile $p",
            "Unblock-File $p",
            "if((Get-FileHash -Algorithm SHA256 $p).Hash -ne $h){Write-Error 'Runly: dosya butunlugu dogrulanamadi, islem durduruldu'; return}",
            ". $p",
            $"$e=@({list},$null)",
            "$e=@($e | Where-Object { $_ })",
            "$ok=0",
            @"$fx='HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\'",
            @"for($i=0;$i -lt $e.Count;$i++){ Write-Progress -Activity 'Runly varsayilan atama' -Status $e[$i][0] -PercentComplete (($i+1)*100/$e.Count); if(Test-Path ($fx+$e[$i][0]+'\UserChoiceLatest')){Write-Warning ('Runly: {0} yeni Windows korumasinda, Ayarlar uzerinden baglayin' -f $e[$i][0]); continue}; Set-FTA $e[$i][1] $e[$i][0]; if((Get-FTA $e[$i][0]) -eq $e[$i][1]){$ok++}else{Write-Warning ('Runly: {0} atanamadi' -f $e[$i][0])} }",
            "Write-Progress -Activity 'Runly varsayilan atama' -Completed",
            "Write-Host ('Runly: {0}/{1} uzanti atandi.' -f $ok,$e.Count)");
    }
}
