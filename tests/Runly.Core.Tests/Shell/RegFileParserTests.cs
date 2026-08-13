using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

/// <summary>Covers the .reg parser Runly uses instead of <c>regedit /s</c>, including its hive guard (T4).</summary>
public sealed class RegFileParserTests
{
    private const string Header = "Windows Registry Editor Version 5.00\r\n\r\n";

    [Theory]
    [InlineData(@"[HKEY_LOCAL_MACHINE\Software\Runly]")]
    [InlineData(@"[HKEY_CLASSES_ROOT\.js]")]
    [InlineData(@"[HKEY_USERS\S-1-5-21\Software]")]
    [InlineData(@"[HKEY_CURRENT_CONFIG\Software]")]
    [InlineData(@"[-HKEY_LOCAL_MACHINE\Software\Runly]")]
    public void Roots_other_than_HKCU_are_rejected(string keyLine)
    {
        var ex = Assert.Throws<RegFileFormatException>(() => RegFileParser.Parse(Header + keyLine + "\r\n"));

        Assert.Contains("HKEY_CURRENT_USER", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"HKEY_CURRENT_USER\Software\Runly")]
    [InlineData(@"HKCU\Software\Runly")]
    [InlineData(@"hkey_current_user\Software\Runly")]
    public void HKCU_is_accepted_in_both_spellings(string path)
    {
        var parsed = RegFileParser.Parse(Header + "[" + path + "]\r\n");

        Assert.Single(parsed);
        Assert.Equal(@"Software\Runly", parsed[0].SubKey);
    }

    [Fact]
    public void Missing_header_is_rejected()
    {
        Assert.Throws<RegFileFormatException>(
            () => RegFileParser.Parse("[HKEY_CURRENT_USER\\Software\\Runly]\r\n"));
    }

    [Fact]
    public void Empty_file_is_rejected()
    {
        Assert.Throws<RegFileFormatException>(() => RegFileParser.Parse("   \r\n\r\n"));
    }

    [Fact]
    public void A_byte_order_mark_does_not_confuse_the_header_check()
    {
        var parsed = RegFileParser.Parse("\uFEFF" + Header + @"[HKEY_CURRENT_USER\Software\Runly]" + "\r\n");

        Assert.Single(parsed);
    }

    [Fact]
    public void Delete_blocks_are_recognised()
    {
        var parsed = RegFileParser.Parse(Header + @"[-HKEY_CURRENT_USER\Software\Classes\Runly.Script.js]" + "\r\n");

        Assert.True(parsed[0].Delete);
        Assert.Equal(@"Software\Classes\Runly.Script.js", parsed[0].SubKey);
        Assert.Empty(parsed[0].Values);
    }

    [Fact]
    public void Default_and_named_values_are_parsed()
    {
        var parsed = RegFileParser.Parse(
            Header +
            "[HKEY_CURRENT_USER\\Software\\Runly]\r\n" +
            "@=\"varsayılan\"\r\n" +
            "\"ad\"=\"değer\"\r\n" +
            "\"sayı\"=dword:0000002a\r\n");

        var values = parsed[0].Values;
        Assert.Equal(3, values.Count);
        Assert.True(values[0].Name.Length == 0);
        Assert.Equal("varsayılan", values[0].Value!.AsString());
        Assert.Equal("değer", values[1].Value!.AsString());
        Assert.Equal(42u, values[2].Value!.AsDWord());
    }

    [Fact]
    public void Value_removal_lines_are_parsed()
    {
        var parsed = RegFileParser.Parse(
            Header + "[HKEY_CURRENT_USER\\Software\\Runly]\r\n\"eski\"=-\r\n");

        Assert.True(parsed[0].Values[0].Delete);
        Assert.Equal("eski", parsed[0].Values[0].Name);
        Assert.Null(parsed[0].Values[0].Value);
    }

    [Fact]
    public void Escaped_quotes_and_backslashes_are_unescaped()
    {
        var parsed = RegFileParser.Parse(
            Header + "[HKEY_CURRENT_USER\\Software\\Runly]\r\n" +
            "@=\"\\\"C:\\\\Runly\\\\Runly.exe\\\" \\\"%1\\\" %*\"\r\n");

        Assert.Equal(@"""C:\Runly\Runly.exe"" ""%1"" %*", parsed[0].Values[0].Value!.AsString());
    }

    [Fact]
    public void Comments_and_blank_lines_are_ignored()
    {
        var parsed = RegFileParser.Parse(
            Header + "; bu bir yorum\r\n\r\n[HKEY_CURRENT_USER\\Software\\Runly]\r\n\r\n; başka yorum\r\n\"a\"=\"b\"\r\n");

        Assert.Single(parsed);
        Assert.Single(parsed[0].Values);
    }

    [Fact]
    public void Continuation_lines_are_joined()
    {
        var parsed = RegFileParser.Parse(
            Header + "[HKEY_CURRENT_USER\\Software\\Runly]\r\n" +
            "\"blob\"=hex:00,01,\\\r\n  02,03\r\n");

        Assert.Equal(new byte[] { 0x00, 0x01, 0x02, 0x03 }, parsed[0].Values[0].Value!.Data);
    }

    [Theory]
    [InlineData("\"a\"")]                       // no '=' at all
    [InlineData("\"a\"=")]                      // empty right hand side
    [InlineData("\"a\"=nonsense")]              // unknown value form
    [InlineData("\"a\"=dword:zzzz")]            // bad hex digits
    [InlineData("\"a\"=hex:zz")]                // bad hex byte
    [InlineData("\"unterminated=\"x\"")]        // unterminated name
    [InlineData("bare=\"x\"")]                  // unquoted name
    public void Malformed_value_lines_are_rejected_with_a_line_number(string valueLine)
    {
        var ex = Assert.Throws<RegFileFormatException>(() => RegFileParser.Parse(
            Header + "[HKEY_CURRENT_USER\\Software\\Runly]\r\n" + valueLine + "\r\n"));

        Assert.True(ex.LineNumber > 0, "hata satır numarası taşımalı");
    }

    [Fact]
    public void A_value_line_outside_a_key_block_is_rejected()
    {
        Assert.Throws<RegFileFormatException>(() => RegFileParser.Parse(Header + "\"a\"=\"b\"\r\n"));
    }

    [Fact]
    public void A_value_line_inside_a_delete_block_is_rejected()
    {
        Assert.Throws<RegFileFormatException>(() => RegFileParser.Parse(
            Header + "[-HKEY_CURRENT_USER\\Software\\Runly]\r\n\"a\"=\"b\"\r\n"));
    }

    [Fact]
    public void An_unclosed_key_line_is_rejected()
    {
        Assert.Throws<RegFileFormatException>(() => RegFileParser.Parse(
            Header + "[HKEY_CURRENT_USER\\Software\\Runly\r\n"));
    }

    [Fact]
    public void RestoreBackup_refuses_a_file_that_leaves_HKCU()
    {
        var registry = new FakeRegistryAccessor();
        var path = Path.Combine(Path.GetTempPath(), $"runly-evil-{Guid.NewGuid():N}.reg");
        File.WriteAllText(path, Header + "[HKEY_LOCAL_MACHINE\\Software\\Runly]\r\n\"a\"=\"b\"\r\n");

        try
        {
            Assert.Throws<RegFileFormatException>(
                () => new RegistryBackup(registry, Path.GetTempPath()).RestoreBackup(path));

            // The file is fully validated before the first write, so nothing may have been applied.
            Assert.Empty(registry.AllKeys(RegistryRoot.CurrentUser));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RestoreBackup_reports_a_missing_file()
    {
        var registry = new FakeRegistryAccessor();
        var backup = new RegistryBackup(registry, Path.GetTempPath());

        Assert.Throws<FileNotFoundException>(
            () => backup.RestoreBackup(Path.Combine(Path.GetTempPath(), "runly-yok.reg")));
    }
}
