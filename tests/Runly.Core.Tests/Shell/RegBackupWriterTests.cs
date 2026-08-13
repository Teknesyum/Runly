using System.Text;
using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

/// <summary>Covers the hand written .reg emitter and the backup snapshot rules (T4).</summary>
public sealed class RegBackupWriterTests
{
    private static RegistryBackup NewBackup(FakeRegistryAccessor registry) =>
        new(registry, Path.Combine(Path.GetTempPath(), "runly-tests-unused"));

    [Fact]
    public void Write_starts_with_the_version_5_header()
    {
        var text = RegFileWriter.Write([RegKeyBlock.DeleteKey(@"Software\Classes\.js")]);

        Assert.StartsWith("Windows Registry Editor Version 5.00\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_keys_are_written_as_delete_lines()
    {
        var registry = new FakeRegistryAccessor();
        var text = NewBackup(registry).BuildBackupText([@"Software\Classes\Runly.Script.js"]);

        Assert.Contains(@"[-HKEY_CURRENT_USER\Software\Classes\Runly.Script.js]", text, StringComparison.Ordinal);
        // Nothing exists yet, so the delete line must be the only block.
        Assert.DoesNotContain(@"[HKEY_CURRENT_USER\", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_shared_keys_are_exported_without_a_delete_line()
    {
        // Deleting and recreating a shared key would momentarily destroy every other application's entries;
        // a crash in between would leave the user with none of them.
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js", "", "JSFile");
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "VSCode.js", "");

        var text = NewBackup(registry).BuildBackupText([@"Software\Classes\.js"]);

        Assert.DoesNotContain(@"[-HKEY_CURRENT_USER\Software\Classes\.js]", text, StringComparison.Ordinal);
        Assert.Contains(@"[HKEY_CURRENT_USER\Software\Classes\.js]", text, StringComparison.Ordinal);
        Assert.Contains(@"[HKEY_CURRENT_USER\Software\Classes\.js\OpenWithProgids]", text, StringComparison.Ordinal);
        Assert.Contains(@"@=""JSFile""", text, StringComparison.Ordinal);
        Assert.Contains(@"""VSCode.js""=""""", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisteredApplications_is_never_deleted_wholesale()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Notepad++", @"Software\Notepad++\Capabilities");

        var text = NewBackup(registry).BuildBackupText([@"Software\RegisteredApplications"]);

        Assert.DoesNotContain("[-HKEY_CURRENT_USER", text, StringComparison.Ordinal);
        Assert.Contains(@"""Notepad++""=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_Runly_owned_key_is_deleted_before_it_is_restored()
    {
        // Runly's own keys carry nobody else's data, so wiping them before a restore is safe and correct.
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.js", "", "eski");
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Runly\Capabilities", "ApplicationName", "Runly");

        var text = NewBackup(registry).BuildBackupText([@"Software\Classes\Runly.Script.js", @"Software\Runly"]);

        Assert.Contains(@"[-HKEY_CURRENT_USER\Software\Classes\Runly.Script.js]", text, StringComparison.Ordinal);
        Assert.Contains(@"[-HKEY_CURRENT_USER\Software\Runly]", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"Software\Classes\Runly.Script.js", true)]
    [InlineData(@"Software\Classes\Runly.Script.ps1\shell\open\command", true)]
    [InlineData(@"Software\Classes\Applications\Runly.exe", true)]
    [InlineData(@"Software\Runly", true)]
    [InlineData(@"Software\Runly\Capabilities\FileAssociations", true)]
    [InlineData(@"Software\Classes\.js", false)]
    [InlineData(@"Software\Classes\.js\OpenWithProgids", false)]
    [InlineData(@"Software\RegisteredApplications", false)]
    [InlineData(@"Software\Classes\Applications\notepad++.exe", false)]
    public void Runly_owned_keys_are_told_apart_from_shared_ones(string key, bool owned)
    {
        Assert.Equal(owned, RegistryBackup.IsRunlyOwned(key));
    }

    [Fact]
    public void Nested_keys_are_dropped_so_a_child_delete_cannot_wipe_the_parent_restore()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Runly\Capabilities", "ApplicationName", "Runly");

        var text = NewBackup(registry).BuildBackupText(
            [@"Software\Runly", @"Software\Runly\Capabilities"]);

        var deleteLines = text.Split("\r\n").Count(l => l.StartsWith("[-", StringComparison.Ordinal));
        Assert.Equal(1, deleteLines);
        Assert.Contains(@"[-HKEY_CURRENT_USER\Software\Runly]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_keys_are_collapsed()
    {
        var registry = new FakeRegistryAccessor();
        var text = NewBackup(registry).BuildBackupText(
            [@"Software\Runly", @"software\runly", @"\Software\Runly\"]);

        Assert.Equal(1, text.Split("\r\n").Count(l => l.StartsWith("[-", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(@"C:\Program Files\nodejs\node.exe", @"C:\\Program Files\\nodejs\\node.exe")]
    [InlineData(@"""%1"" %*", @"\""%1\"" %*")]
    [InlineData(@"C:\Test klasörü\çalış.js", @"C:\\Test klasörü\\çalış.js")]
    public void Strings_are_escaped_the_way_regedit_expects(string raw, string expected)
    {
        Assert.Equal(expected, RegFileWriter.EscapeString(raw));
    }

    [Fact]
    public void Command_values_survive_a_write_parse_round_trip()
    {
        const string command = @"""C:\Program Files\Runly\Runly.exe"" ""%1"" %*";

        var block = new RegKeyBlock
        {
            SubKey = @"Software\Classes\Runly.Script.js\shell\open\command",
            Values = [RegValueOperation.Set(RegistryValueEntry.FromString("", command))],
        };

        var parsed = RegFileParser.Parse(RegFileWriter.Write([block]));

        Assert.Single(parsed);
        Assert.Equal(block.SubKey, parsed[0].SubKey);
        Assert.Equal(command, parsed[0].Values[0].Value!.AsString());
    }

    [Fact]
    public void Dword_binary_expand_and_multi_string_values_round_trip()
    {
        var values = new List<RegValueOperation>
        {
            RegValueOperation.Set(RegistryValueEntry.FromDWord("flag", 0x0000002A)),
            RegValueOperation.Set(RegistryValueEntry.FromBinary("blob", [0x00, 0x01, 0xFE, 0xFF])),
            RegValueOperation.Set(RegistryValueEntry.FromExpandString("dir", @"%APPDATA%\Runly")),
            RegValueOperation.Set(RegistryValueEntry.FromMultiString("list", ["bir", "iki"])),
        };

        var text = RegFileWriter.Write([new RegKeyBlock { SubKey = @"Software\Runly", Values = values }]);
        var parsed = RegFileParser.Parse(text);
        var back = parsed[0].Values;

        Assert.Contains("dword:0000002a", text, StringComparison.Ordinal);
        Assert.Contains("hex:00,01,fe,ff", text, StringComparison.Ordinal);
        Assert.Contains("hex(2):", text, StringComparison.Ordinal);
        Assert.Contains("hex(7):", text, StringComparison.Ordinal);

        Assert.Equal(4, back.Count);
        Assert.Equal(0x2Au, back[0].Value!.AsDWord());
        Assert.Equal(new byte[] { 0x00, 0x01, 0xFE, 0xFF }, back[1].Value!.Data);
        Assert.Equal(@"%APPDATA%\Runly", back[2].Value!.AsString());
        Assert.Equal(RegistryValueKind.MultiString, back[3].Value!.Kind);
        Assert.Equal(
            Encoding.Unicode.GetBytes("bir\0iki\0\0"),
            back[3].Value!.Data);
    }

    [Fact]
    public void Long_binary_values_are_wrapped_and_still_parse()
    {
        var payload = new byte[200];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)i;
        }

        var text = RegFileWriter.Write(
        [
            new RegKeyBlock
            {
                SubKey = @"Software\Runly",
                Values = [RegValueOperation.Set(RegistryValueEntry.FromBinary("blob", payload))],
            },
        ]);

        Assert.Contains("\\\r\n  ", text, StringComparison.Ordinal);
        Assert.All(text.Split("\r\n"), line => Assert.True(line.Length <= 80, $"çok uzun satır: {line.Length}"));

        var parsed = RegFileParser.Parse(text);
        Assert.Equal(payload, parsed[0].Values[0].Value!.Data);
    }

    [Fact]
    public void A_full_backup_round_trips_into_the_same_structure()
    {
        var source = new FakeRegistryAccessor();
        source.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js", "", "JSFile");
        source.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "VSCode.js", "");
        source.Seed(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Notepad++", @"Software\Notepad++\Capabilities");

        var text = NewBackup(source).BuildBackupText(
            [@"Software\Classes\.js", @"Software\RegisteredApplications", @"Software\Classes\Runly.Script.js"]);

        // Replay the backup into a second registry that Runly has already been installed into.
        var target = new FakeRegistryAccessor();
        target.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js", "", "Runly.Script.js");
        target.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "Runly.Script.js", "");
        target.Seed(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.js\shell\open\command", "", "runly");
        target.Seed(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Runly", @"Software\Runly\Capabilities");

        var restorePath = Path.Combine(Path.GetTempPath(), $"runly-restore-{Guid.NewGuid():N}.reg");
        File.WriteAllText(restorePath, text, new UnicodeEncoding(false, true));

        try
        {
            new RegistryBackup(target, Path.GetTempPath()).RestoreBackup(restorePath);
        }
        finally
        {
            File.Delete(restorePath);
        }

        // Values that existed before are restored exactly, and other applications survive untouched.
        Assert.Equal("JSFile", target.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js", "")!.AsString());
        Assert.NotNull(target.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "VSCode.js"));
        Assert.NotNull(target.GetValue(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "Notepad++"));

        // Runly's own ProgID tree is wiped, because that key belongs to nobody else.
        Assert.False(target.KeyExists(RegistryRoot.CurrentUser, @"Software\Classes\Runly.Script.js"));

        // Runly's entries in shared keys are Uninstall's responsibility, not the backup's.
        Assert.NotNull(target.GetValue(RegistryRoot.CurrentUser, @"Software\Classes\.js\OpenWithProgids", "Runly.Script.js"));
    }

    [Fact]
    public void CreateBackup_writes_a_timestamped_file_and_lists_it()
    {
        var dir = Path.Combine(Path.GetTempPath(), "runly-backups-" + Guid.NewGuid().ToString("N"));
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\.js", "", "JSFile");

        var clock = new DateTime(2026, 8, 9, 14, 12, 30, DateTimeKind.Local);
        var backup = new RegistryBackup(registry, dir, () => clock);

        try
        {
            var first = backup.CreateBackup([@"Software\Classes\.js"]);

            // Same second, same name: the writer must not overwrite the earlier backup.
            var collision = backup.CreateBackup([@"Software\Classes\.js"]);

            Assert.Equal("assoc-20260809-141230.reg", Path.GetFileName(first));
            Assert.Equal("assoc-20260809-141230-1.reg", Path.GetFileName(collision));

            clock = clock.AddMinutes(5);
            var newest = backup.CreateBackup([@"Software\Classes\.js"]);

            var listed = backup.ListBackups();
            Assert.Equal(3, listed.Count);
            Assert.Equal(newest, listed[0].Path);
            Assert.Equal(new DateTime(2026, 8, 9, 14, 17, 30), listed[0].CreatedUtc);
            Assert.Equal(newest, backup.GetLatestBackup()!.Path);
            Assert.All(listed, b => Assert.True(b.SizeBytes > 0));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Empty_key_paths_are_rejected()
    {
        var registry = new FakeRegistryAccessor();
        Assert.Throws<ArgumentException>(() => NewBackup(registry).BuildBackupText(["   \\  "]));
    }
}
