using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

public class EditorNameTests
{
    [Theory]
    [InlineData("code", "Runly ile düzenle (Code)")]
    [InlineData("notepad", "Runly ile düzenle (Notepad)")]
    [InlineData("\"C:\\Program Files\\Notepad++\\notepad++.exe\"", "Runly ile düzenle (Notepad++)")]
    [InlineData("C:\\Yok\\vi.exe", "Runly ile düzenle (VI)")]
    public void The_label_names_the_editor(string command, string expected) =>
        Assert.Equal(expected, EditorName.EditVerbLabel(command));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_an_editor_the_label_stays_plain(string? command) =>
        Assert.Equal("Runly ile düzenle", EditorName.EditVerbLabel(command));

    [Fact]
    public void A_real_file_is_named_by_its_own_description()
    {
        var path = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        if (!File.Exists(path))
        {
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(EditorName.Describe(path)));
    }
}
