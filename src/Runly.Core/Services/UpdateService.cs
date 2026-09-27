using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Runly.Core.Paths;

namespace Runly.Core.Services;

/// <summary>A published release that is newer than the running build.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string AssetName, Uri AssetUrl, Uri? ChecksumUrl);

/// <summary>Checks GitHub Releases for a newer Runly, downloads it at low priority, verifies its SHA-256
/// and swaps it in place with <c>.old</c> renames so running executables can be replaced.</summary>
public sealed partial class UpdateService
{
    /// <summary>The executables a package must carry before it may replace an installation.</summary>
    public static readonly string[] RequiredFiles = { "Runly.exe", "RunlyConsole.exe", "RunlySettings.exe" };

    private const string Repository = "Teknesyum/Runly";
    private const string OldSuffix = ".old";
    private static readonly Uri LatestUri = new($"https://api.github.com/repos/{Repository}/releases/latest");

    private readonly HttpClient _http;
    private readonly string _stagingRoot;

    /// <summary>Creates the service; <paramref name="stagingRoot"/> defaults to <c>%LOCALAPPDATA%\Runly\guncelleme</c>.</summary>
    public UpdateService(HttpClient http, string? stagingRoot = null)
    {
        _http = http;
        _stagingRoot = stagingRoot ?? Path.Combine(RunlyPaths.LocalAppDataDir, "guncelleme");
    }

    [GeneratedRegex(@"^Runly-v.+-win-x64\.zip$", RegexOptions.IgnoreCase)]
    private static partial Regex PackageName();

    /// <summary>Reads <c>v0.2.1</c> or <c>0.2.1</c>; anything after a <c>-</c> or <c>+</c> is dropped.</summary>
    public static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var core = text.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(core, out var version) ? Normalize(version) : null;
    }

    /// <summary>Reads the first token of a <c>.sha256</c> file; null unless it is 64 hex digits.</summary>
    public static string? ParseChecksum(string? content)
    {
        var token = (content ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is not null && token.Length == 64 && token.All(Uri.IsHexDigit) ? token.ToLowerInvariant() : null;
    }

    /// <summary>Picks the Windows package and its checksum out of a GitHub <c>releases/latest</c> answer.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var version = ParseVersion(tag);
        if (version is null || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var urls = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            if (name is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                urls[name] = uri;
            }
        }

        var package = urls.Keys.FirstOrDefault(name => PackageName().IsMatch(name));
        if (package is null)
        {
            return null;
        }

        urls.TryGetValue(package + ".sha256", out var checksum);
        return new ReleaseInfo(version, tag!, package, urls[package], checksum);
    }

    /// <summary>True when <paramref name="candidate"/> is strictly newer than <paramref name="current"/>.</summary>
    public static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

    /// <summary>Asks GitHub for the latest release; null when it is not newer or cannot be read.</summary>
    public async Task<ReleaseInfo?> CheckAsync(Version current, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestUri);
        request.Headers.UserAgent.ParseAdd("Runly-Updater");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var release = ParseRelease(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
        return release is not null && IsNewer(release.Version, current) ? release : null;
    }

    /// <summary>Downloads, verifies and extracts <paramref name="release"/> on a lowest-priority thread.
    /// Returns the extracted package folder. Reports 0–100 through <paramref name="progress"/>.</summary>
    public Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellation)
    {
        return Task.Factory.StartNew(
            () =>
            {
                Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                return Download(release, progress, cancellation);
            },
            cancellation,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private string Download(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellation)
    {
        var folder = Path.Combine(_stagingRoot, release.Tag);
        var archive = Path.Combine(folder, release.AssetName);
        var package = Path.Combine(folder, "paket");
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);

        using (var response = Get(release.AssetUrl, cancellation))
        {
            var total = response.Content.Headers.ContentLength ?? 0;
            using var source = response.Content.ReadAsStream(cancellation);
            using var target = File.Create(archive);
            var buffer = new byte[64 * 1024];
            long done = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                target.Write(buffer, 0, read);
                done += read;
                if (total > 0)
                {
                    progress?.Report(Math.Min(95, done * 95.0 / total));
                }
            }
        }

        if (release.ChecksumUrl is not null)
        {
            using var response = Get(release.ChecksumUrl, cancellation);
            using var reader = new StreamReader(response.Content.ReadAsStream(cancellation));
            var expected = ParseChecksum(reader.ReadToEnd())
                ?? throw new InvalidDataException("Yayımlanan sha256 okunamadı.");
            VerifyChecksum(archive, expected);
        }

        progress?.Report(97);
        ZipFile.ExtractToDirectory(archive, package);
        EnsurePackage(package);
        progress?.Report(100);
        return package;
    }

    private HttpResponseMessage Get(Uri uri, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Runly-Updater");
        var response = _http.Send(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        return response;
    }

    /// <summary>Throws when the file's SHA-256 differs from <paramref name="expected"/>.</summary>
    public static void VerifyChecksum(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture,
                "sha256 uyuşmadı: beklenen {0}, inen {1}.", expected.ToLowerInvariant(), actual));
        }
    }

    /// <summary>Throws when a required executable is missing from <paramref name="package"/>.</summary>
    public static void EnsurePackage(string package)
    {
        foreach (var required in RequiredFiles)
        {
            if (!File.Exists(Path.Combine(package, required)))
            {
                throw new InvalidDataException(required + " pakette yok.");
            }
        }
    }

    /// <summary>Copies <paramref name="package"/> over <paramref name="installDir"/>. Each existing file is
    /// first renamed to <c>.old</c>, which Windows allows for a running executable.</summary>
    public static void Apply(string package, string installDir)
    {
        EnsurePackage(package);
        foreach (var source in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(installDir, Path.GetRelativePath(package, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                var old = target + OldSuffix;
                if (File.Exists(old))
                {
                    File.Delete(old);
                }

                File.Move(target, old);
            }

            File.Copy(source, target);
        }
    }

    /// <summary>Removes <c>.old</c> files a previous update left behind; locked ones stay for next time.</summary>
    public static int CleanupOld(string installDir)
    {
        var removed = 0;
        if (!Directory.Exists(installDir))
        {
            return removed;
        }

        foreach (var old in Directory.EnumerateFiles(installDir, "*" + OldSuffix, SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(old);
                removed++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return removed;
    }

    private static Version Normalize(Version v) =>
        new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build), Math.Max(0, v.Revision));
}
