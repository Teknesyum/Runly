using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>Covers absolute-path pass-through, PATH/PATHEXT scanning, the unresolvable zero-byte fallback and cache staleness.</summary>
public sealed class PathSearcherTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cachePath;

    public PathSearcherTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("runly-pathsearcher-tests-").FullName;
        _cachePath = Path.Combine(_tempDir, "cache", "ipcache.json");
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string CreateFakeExe(string relativeDir, string fileName, int sizeBytes = 10)
    {
        var dir = Path.Combine(_tempDir, relativeDir);
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, fileName);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        return fullPath;
    }

    [Fact]
    public void Find_AbsolutePath_ReturnsItWhenFileExists()
    {
        var exePath = CreateFakeExe("bin", "tool.exe");
        var searcher = new PathSearcher(_cachePath, pathEnvOverride: "", pathExtEnvOverride: "");

        Assert.Equal(exePath, searcher.Find(exePath));
    }

    [Fact]
    public void Find_AbsolutePath_ReturnsNullWhenMissing()
    {
        var searcher = new PathSearcher(_cachePath, pathEnvOverride: "", pathExtEnvOverride: "");

        Assert.Null(searcher.Find(Path.Combine(_tempDir, "nope", "missing.exe")));
    }

    [Fact]
    public void Find_RelativePathWithDirectorySeparator_ResolvesWithoutSearchingPath()
    {
        var exePath = CreateFakeExe("relative", "tool.exe");
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, exePath);
        var searcher = new PathSearcher(_cachePath, pathEnvOverride: string.Empty, pathExtEnvOverride: ".exe");

        Assert.Equal(exePath, searcher.Find(relative));
    }

    [Fact]
    public void Find_CacheTimestampFromFuture_IsIgnored()
    {
        var oldDir = Path.Combine(_tempDir, "old");
        var newDir = Path.Combine(_tempDir, "new");
        Directory.CreateDirectory(oldDir);
        Directory.CreateDirectory(newDir);
        var oldExe = CreateFakeExe("old", "node.exe");
        var newExe = CreateFakeExe("new", "node.exe");
        var future = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        new PathSearcher(_cachePath, pathEnvOverride: oldDir, pathExtEnvOverride: ".exe", utcNowOverride: () => future).Find("node");

        var now = future.AddDays(-10);
        var found = new PathSearcher(_cachePath, pathEnvOverride: newDir, pathExtEnvOverride: ".exe", utcNowOverride: () => now).Find("node");

        Assert.NotEqual(oldExe, found);
        Assert.Equal(newExe, found);
    }

    [Fact]
    public void Find_ScansPathWithPathExt_AndFindsMatch()
    {
        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        var exePath = CreateFakeExe("dir2", "node.exe");

        var searcher = new PathSearcher(
            _cachePath,
            pathEnvOverride: $"{dir1}{Path.PathSeparator}{dir2}",
            pathExtEnvOverride: ".exe");

        Assert.Equal(exePath, searcher.Find("node"));
    }

    [Fact]
    public void Find_NotOnPath_ReturnsNull()
    {
        var searcher = new PathSearcher(_cachePath, pathEnvOverride: _tempDir, pathExtEnvOverride: ".exe");

        Assert.Null(searcher.Find("doesnotexist"));
    }

    [Fact]
    public void Find_ZeroByteStoreStub_IsSkipped_RealCandidateFoundNext()
    {
        var stubDir = Path.Combine(_tempDir, "stub");
        var realDir = Path.Combine(_tempDir, "real");
        Directory.CreateDirectory(stubDir);
        Directory.CreateDirectory(realDir);
        CreateFakeExe("stub", "python.exe", sizeBytes: 0);
        var realExe = CreateFakeExe("real", "python.exe", sizeBytes: 128);

        var searcher = new PathSearcher(
            _cachePath,
            pathEnvOverride: $"{stubDir}{Path.PathSeparator}{realDir}",
            pathExtEnvOverride: ".exe");

        Assert.Equal(realExe, searcher.Find("python"));
    }

    [Fact]
    public void Find_ZeroByteStubOnly_IsAcceptedAsLastResort()
    {
        // Decision K9: py.exe on this machine is a working 0-byte app-execution alias and is the only
        // candidate on PATH. Rejecting it outright would make .py impossible to bind.
        var stubDir = Path.Combine(_tempDir, "stub");
        Directory.CreateDirectory(stubDir);
        var stubExe = CreateFakeExe("stub", "python.exe", sizeBytes: 0);

        var searcher = new PathSearcher(_cachePath, pathEnvOverride: stubDir, pathExtEnvOverride: ".exe");

        Assert.Equal(stubExe, searcher.Find("python"));
    }

    [Fact]
    public void Find_ZeroByteStubInEarlierDirectory_LosesToRealExeInLaterDirectory()
    {
        // The last-resort rule must not weaken the preference for a real executable.
        var stubDir = Path.Combine(_tempDir, "stubfirst");
        var realDir = Path.Combine(_tempDir, "reallater");
        Directory.CreateDirectory(stubDir);
        Directory.CreateDirectory(realDir);
        CreateFakeExe("stubfirst", "py.exe", sizeBytes: 0);
        var realExe = CreateFakeExe("reallater", "py.exe", sizeBytes: 4096);

        var searcher = new PathSearcher(
            _cachePath,
            pathEnvOverride: $"{stubDir}{Path.PathSeparator}{realDir}",
            pathExtEnvOverride: ".exe");

        Assert.Equal(realExe, searcher.Find("py"));
    }

    [Fact]
    public void Find_SecondCall_UsesCacheInsteadOfRescanning()
    {
        var dir = Path.Combine(_tempDir, "dir1");
        Directory.CreateDirectory(dir);
        var exePath = CreateFakeExe("dir1", "node.exe");
        var firstSearcher = new PathSearcher(_cachePath, pathEnvOverride: dir, pathExtEnvOverride: ".exe");
        var first = firstSearcher.Find("node");

        // A fresh searcher with an empty PATH: if it rescanned instead of trusting the cache, it would find nothing.
        var secondSearcher = new PathSearcher(_cachePath, pathEnvOverride: "", pathExtEnvOverride: ".exe");
        var second = secondSearcher.Find("node");

        Assert.Equal(exePath, first);
        Assert.Equal(exePath, second);
        Assert.True(File.Exists(_cachePath));
    }

    [Fact]
    public void Find_StaleCacheEntry_IsRescanned()
    {
        var dir = Path.Combine(_tempDir, "dir1");
        Directory.CreateDirectory(dir);
        var exePath = CreateFakeExe("dir1", "node.exe");
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var firstSearcher = new PathSearcher(_cachePath, pathEnvOverride: dir, pathExtEnvOverride: ".exe", utcNowOverride: () => now);
        firstSearcher.Find("node");

        // Move the interpreter to a new location and advance the clock past the 24 hour TTL.
        var newDir = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(newDir);
        var newExePath = Path.Combine(newDir, "node.exe");
        File.Move(exePath, newExePath);
        var later = now.AddHours(25);
        var secondSearcher = new PathSearcher(
            _cachePath,
            pathEnvOverride: $"{dir}{Path.PathSeparator}{newDir}",
            pathExtEnvOverride: ".exe",
            utcNowOverride: () => later);

        Assert.Equal(newExePath, secondSearcher.Find("node"));
    }

    [Fact]
    public void Find_CachedPathNoLongerExists_IsRescanned()
    {
        var dir = Path.Combine(_tempDir, "dir1");
        Directory.CreateDirectory(dir);
        var exePath = CreateFakeExe("dir1", "node.exe");
        var searcher1 = new PathSearcher(_cachePath, pathEnvOverride: dir, pathExtEnvOverride: ".exe");
        searcher1.Find("node");

        File.Delete(exePath);
        var newDir = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(newDir);
        var newExePath = Path.Combine(newDir, "node.exe");
        File.WriteAllBytes(newExePath, [1, 2, 3]);

        var searcher2 = new PathSearcher(_cachePath, pathEnvOverride: $"{dir}{Path.PathSeparator}{newDir}", pathExtEnvOverride: ".exe");

        Assert.Equal(newExePath, searcher2.Find("node"));
    }
}
