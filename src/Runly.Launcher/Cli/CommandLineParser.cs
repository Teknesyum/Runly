using Runly.Core.Models;

namespace Runly.Launcher.Cli;

/// <summary>Parses the <c>Runly.exe</c> command line described in SPEC 7.</summary>
internal static class CommandLineParser
{
    /// <summary>The short Turkish usage text shown on a malformed command line (SPEC 7).</summary>
    internal const string UsageText = """
        Runly — script dosyalarını çift tıkla çalıştırır.

        Kullanım:
          Runly.exe [--verb <run|runas|edit|prompt-args>] [--no-wait] <script-yolu> [script argümanları...]

        Seçenekler:
          --verb run           Script'i normal çalıştırır (varsayılan).
          --verb runas         Yükseltilmiş (yönetici) çalıştırır.
          --verb edit          Editörde açar, çalıştırmaz.
          --verb prompt-args   Önce argüman sorar, sonra çalıştırır.
          --no-wait            Bu çalıştırma için pencereyi açık tutma.

        Çıkış kodları:
          0 başarılı · 1 script hata verdi · 2 kullanım hatası
          3 yorumlayıcı bulunamadı · 4 güvenlik kapısında iptal edildi
        """;

    /// <summary>
    /// Parses the raw arguments. Options are only recognised before the script path; everything after it
    /// belongs to the script, so a script may legitimately receive its own <c>--verb</c> flag.
    /// </summary>
    internal static bool TryParse(string[] args, out LaunchRequest request, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        request = new LaunchRequest();
        error = null;

        var verb = LaunchVerb.Run;
        var noWait = false;
        string? scriptPath = null;
        var scriptArgs = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];

            if (scriptPath is not null)
            {
                scriptArgs.Add(current);
                continue;
            }

            switch (current)
            {
                case "--verb":
                    if (i + 1 >= args.Length)
                    {
                        error = "--verb seçeneği bir değer bekliyor.";
                        return false;
                    }

                    if (!TryParseVerb(args[++i], out verb))
                    {
                        error = $"Bilinmeyen fiil: {args[i]}";
                        return false;
                    }

                    break;

                case "--no-wait":
                    noWait = true;
                    break;

                case "--help" or "-h" or "-?" or "/?":
                    error = null;
                    return false;

                default:
                    if (current.StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"Bilinmeyen seçenek: {current}";
                        return false;
                    }

                    scriptPath = current;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            error = null;
            return false;
        }

        request = new LaunchRequest
        {
            ScriptPath = ToFullPath(scriptPath),
            Verb = verb,
            ScriptArgs = [.. scriptArgs],
            NoWait = noWait,
        };

        return true;
    }

    private static bool TryParseVerb(string value, out LaunchVerb verb)
    {
        // These four spellings are what T4's registry layout writes into shell\...\command (T3.md).
        switch (value)
        {
            case "run":
                verb = LaunchVerb.Run;
                return true;
            case "runas":
                verb = LaunchVerb.RunAs;
                return true;
            case "edit":
                verb = LaunchVerb.Edit;
                return true;
            case "prompt-args":
                verb = LaunchVerb.PromptArgs;
                return true;
            default:
                verb = LaunchVerb.Run;
                return false;
        }
    }

    private static string ToFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
