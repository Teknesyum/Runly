using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Runly.Core.Services;

namespace Runly.Core.Tests;

/// <summary>
/// Covers the APPEXECLINK reparse-point reader that replaced the zero-byte guess in <see cref="PathSearcher"/>
/// (SPEC 8, decision K9). The layout tests run on synthetic bytes so they need no Store alias present.
/// </summary>
public sealed class AppExecutionAliasTests : IDisposable
{
    private const uint AppExecLinkTag = 0x8000001B;
    private const uint MountPointTag = 0xA0000003;

    private const string PythonFamily = "PythonSoftwareFoundation.PythonManager_3847v3x7pw1km";
    private const string PythonAumid = "PythonSoftwareFoundation.PythonManager_3847v3x7pw1km!Python.Exe";
    private const string PythonTarget =
        @"C:\Program Files\WindowsApps\PythonSoftwareFoundation.PythonManager_26.3.240.0_x64__3847v3x7pw1km\python.exe";

    private const string AppInstallerFamily = "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe";
    private const string RedirectorTarget =
        @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.29.280.0_x64__8wekyb3d8bbwe\AppInstallerPythonRedirector.exe";

    private readonly string _tempDir = Directory.CreateTempSubdirectory("runly-appexeclink-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static byte[] BuildReparseBuffer(uint tag, uint version, params string[] strings)
    {
        var payload = new List<byte>();
        payload.AddRange(BitConverter.GetBytes(version));
        foreach (var value in strings)
        {
            payload.AddRange(Encoding.Unicode.GetBytes(value));
            payload.AddRange(Encoding.Unicode.GetBytes("\0"));
        }

        var buffer = new byte[8 + payload.Count];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, tag);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), (ushort)payload.Count);
        payload.CopyTo(buffer, 8);
        return buffer;
    }

    private static byte[] BuildWorkingAlias() =>
        BuildReparseBuffer(AppExecLinkTag, 3, PythonFamily, PythonAumid, PythonTarget, "0");

    private static byte[] BuildRedirectorStub() =>
        BuildReparseBuffer(
            AppExecLinkTag,
            3,
            AppInstallerFamily,
            $"{AppInstallerFamily}!PythonRedirector",
            RedirectorTarget,
            "0");

    [Fact]
    public void TryParse_WorkingAlias_ReadsEveryField()
    {
        Assert.True(AppExecutionAliasReader.TryParse(BuildWorkingAlias(), out var alias));

        Assert.NotNull(alias);
        Assert.Equal(3u, alias.Version);
        Assert.Equal(PythonFamily, alias.PackageFamilyName);
        Assert.Equal(PythonAumid, alias.ApplicationUserModelId);
        Assert.Equal(PythonTarget, alias.TargetPath);
        Assert.Equal("0", alias.ApplicationType);
    }

    [Fact]
    public void TryParse_WorkingAlias_IsNotAStoreRedirector()
    {
        AppExecutionAliasReader.TryParse(BuildWorkingAlias(), out var alias);

        Assert.False(alias!.IsStoreRedirector);
    }

    [Fact]
    public void TryParse_RedirectorStub_IsRecognisedAsStoreRedirector()
    {
        Assert.True(AppExecutionAliasReader.TryParse(BuildRedirectorStub(), out var alias));

        Assert.Equal(RedirectorTarget, alias!.TargetPath);
        Assert.True(alias.IsStoreRedirector);
    }

    [Fact]
    public void TryParse_WingetAlias_IsNotARedirectorDespiteAppInstallerPackage()
    {
        // winget.exe is a working alias owned by the very package the dead python stubs come from, so the
        // owning package must never be the thing that condemns an alias.
        var buffer = BuildReparseBuffer(
            AppExecLinkTag,
            3,
            AppInstallerFamily,
            $"{AppInstallerFamily}!winget",
            @"C:\Program Files\WindowsApps\Microsoft.DesktopAppInstaller_1.29.280.0_x64__8wekyb3d8bbwe\winget.exe",
            "0");

        Assert.True(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.False(alias!.IsStoreRedirector);
    }

    [Fact]
    public void TryParse_MissingApplicationTypeField_StillResolvesTheTarget()
    {
        var buffer = BuildReparseBuffer(AppExecLinkTag, 3, PythonFamily, PythonAumid, PythonTarget);

        Assert.True(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.Equal(PythonTarget, alias!.TargetPath);
        Assert.Equal(string.Empty, alias.ApplicationType);
    }

    [Fact]
    public void TryParse_ForeignReparseTag_ReturnsFalse()
    {
        var buffer = BuildReparseBuffer(MountPointTag, 3, PythonFamily, PythonAumid, PythonTarget, "0");

        Assert.False(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryParse_EmptyBuffer_ReturnsFalse()
    {
        Assert.False(AppExecutionAliasReader.TryParse(ReadOnlySpan<byte>.Empty, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryParse_HeaderWithoutPayload_ReturnsFalse()
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, AppExecLinkTag);

        Assert.False(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryParse_TruncatedStringList_ReturnsFalse()
    {
        var buffer = BuildWorkingAlias();

        Assert.False(AppExecutionAliasReader.TryParse(buffer.AsSpan(0, 40), out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryParse_DeclaredLengthLongerThanBuffer_ReturnsFalse()
    {
        var buffer = BuildWorkingAlias();
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(4), (ushort)(buffer.Length + 16));

        Assert.False(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryParse_TooFewStrings_ReturnsFalse()
    {
        var buffer = BuildReparseBuffer(AppExecLinkTag, 3, PythonFamily, PythonAumid);

        Assert.False(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryParse_EmptyTarget_ReturnsFalse()
    {
        var buffer = BuildReparseBuffer(AppExecLinkTag, 3, PythonFamily, PythonAumid, string.Empty, "0");

        Assert.False(AppExecutionAliasReader.TryParse(buffer, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryReadReparseData_PlainFile_ReturnsFalse()
    {
        var plainFile = Path.Combine(_tempDir, "plain.exe");
        File.WriteAllBytes(plainFile, []);

        Assert.False(AppExecutionAliasReader.TryReadReparseData(plainFile, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void TryReadReparseData_MissingFile_ReturnsFalse()
    {
        Assert.False(AppExecutionAliasReader.TryReadReparseData(Path.Combine(_tempDir, "nope.exe"), out var data));
        Assert.Null(data);
    }

    [Fact]
    public void TryRead_PlainFile_ReturnsFalse()
    {
        var plainFile = Path.Combine(_tempDir, "plain2.exe");
        File.WriteAllBytes(plainFile, []);

        Assert.False(AppExecutionAliasReader.TryRead(plainFile, out var alias));
        Assert.Null(alias);
    }

    [Fact]
    public void TryReadReparseData_Junction_ReadsTheTagInsteadOfFollowingTheLink()
    {
        // A junction is the only reparse point that can be created without elevation, and it proves the part
        // of the native call that cannot be faked: without FILE_FLAG_OPEN_REPARSE_POINT the handle would land
        // on the target directory and the control code would fail instead of returning a tag.
        var target = Path.Combine(_tempDir, "target");
        var junction = Path.Combine(_tempDir, "link");
        Directory.CreateDirectory(target);
        if (!TryCreateJunction(junction, target))
        {
            return;
        }

        try
        {
            Assert.True(AppExecutionAliasReader.TryReadReparseData(junction, out var data));
            Assert.NotNull(data);
            Assert.Equal(MountPointTag, BinaryPrimitives.ReadUInt32LittleEndian(data));
            Assert.False(AppExecutionAliasReader.TryParse(data, out var alias));
            Assert.Null(alias);
        }
        finally
        {
            Directory.Delete(junction);
        }
    }

    [Fact]
    public void TryRead_InstalledAppExecutionAlias_ResolvesToARealTarget()
    {
        var aliasDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps");

        if (!Directory.Exists(aliasDir))
        {
            return;
        }

        var candidate = Directory.EnumerateFiles(aliasDir, "*.exe")
            .FirstOrDefault(file => AppExecutionAliasReader.TryRead(file, out _));

        if (candidate is null)
        {
            return;
        }

        Assert.True(AppExecutionAliasReader.TryRead(candidate, out var alias));
        Assert.NotEmpty(alias!.TargetPath);
        Assert.NotEmpty(alias.PackageFamilyName);
        Assert.Contains('!', alias.ApplicationUserModelId);
    }

    private static bool TryCreateJunction(string link, string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
