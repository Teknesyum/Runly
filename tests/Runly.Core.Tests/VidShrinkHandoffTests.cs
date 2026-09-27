using Runly.Core.Abstractions;
using Runly.Core.Models;
using Runly.Core.Shell;
using Runly.Core.Tests.Shell;
using Runly.Launcher;

namespace Runly.Core.Tests;

public sealed class VidShrinkHandoffTests
{
    private const string VidShrinkExe = @"C:\Users\u\AppData\Local\Programs\VidShrink\app\VidShrink.App.exe";
    private const string RunlyExe = @"C:\Tools\Runly\RunlyConsole.exe";
    private const string Video = @"C:\Videos\klip.mp4";

    private sealed class ListLogger : ILogger
    {
        internal List<string> Lines { get; } = [];

        public void Info(string message) => Lines.Add(message);

        public void Warn(string message) => Lines.Add(message);

        public void Error(string message, Exception? exception = null) => Lines.Add(message);
    }

    private static FakeRegistryAccessor Installed(params string[] extensions)
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "VidShrink", @"Software\Teknesyum\VidShrink\Capabilities");
        foreach (var extension in extensions)
        {
            registry.Seed(RegistryRoot.CurrentUser, @"Software\Teknesyum\VidShrink\Capabilities\FileAssociations", extension, "Teknesyum.VidShrink.Video");
        }

        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\Teknesyum.VidShrink.Video\shell\open\command", RegistryValueEntry.DefaultValueName, $"\"{VidShrinkExe}\" \"%1\"");
        return registry;
    }

    private static VidShrinkHandoff Handoff(IRegistryAccessor registry, params string[] existing) =>
        new(registry, path => existing.Contains(path, StringComparer.OrdinalIgnoreCase), RunlyExe);

    private static RunlyConfig ConfigWith(string extension, ExtensionMapping mapping)
    {
        var config = new RunlyConfig();
        config.Extensions[extension] = mapping;
        return config;
    }

    [Fact]
    public void Installed_VidShrink_claims_extension_and_the_video_is_handed_off()
    {
        var started = new List<(string Exe, string File)>();
        var logger = new ListLogger();
        var handoff = Handoff(Installed(".mp4", ".mkv"), VidShrinkExe, Video);
        var config = ConfigWith(".mp4", new ExtensionMapping { Kind = HandlerKind.Run, Category = "video", Interpreter = @"C:\GOM\GOM64.EXE", Enabled = true });

        var handed = handoff.TryHandOff(new LaunchRequest { ScriptPath = Video }, config, (exe, file) => { started.Add((exe, file)); return true; }, logger);

        Assert.True(handed);
        Assert.Equal([(VidShrinkExe, Video)], started);
    }

    [Fact]
    public void Extension_without_a_Runly_mapping_is_still_handed_off_when_VidShrink_claims_it()
    {
        var handoff = Handoff(Installed(".webm"), VidShrinkExe);

        Assert.Equal(VidShrinkExe, handoff.FindExecutable(".webm", null));
    }

    [Fact]
    public void Video_category_mapping_is_handed_off_even_when_VidShrink_does_not_list_the_extension()
    {
        var handoff = Handoff(Installed(".mp4"), VidShrinkExe);

        Assert.Equal(VidShrinkExe, handoff.FindExecutable(".m2v", new ExtensionMapping { Category = "video" }));
    }

    [Fact]
    public void Non_video_extension_is_not_handed_off()
    {
        var handoff = Handoff(Installed(".mp4"), VidShrinkExe);

        Assert.Null(handoff.FindExecutable(".js", new ExtensionMapping { Category = "scripts" }));
    }

    [Fact]
    public void Missing_VidShrink_keeps_todays_behaviour()
    {
        var started = 0;
        var handoff = Handoff(new FakeRegistryAccessor(), Video);
        var config = ConfigWith(".mp4", new ExtensionMapping { Category = "video", Enabled = true });

        var handed = handoff.TryHandOff(new LaunchRequest { ScriptPath = Video }, config, (_, _) => { started++; return true; }, new ListLogger());

        Assert.False(handed);
        Assert.Equal(0, started);
    }

    [Fact]
    public void Registered_but_deleted_executable_is_not_used()
    {
        var handoff = Handoff(Installed(".mp4"));

        Assert.Null(handoff.FindExecutable(".mp4", new ExtensionMapping { Category = "video" }));
    }

    [Fact]
    public void Executable_path_comes_from_the_registry_not_a_fixed_location()
    {
        const string elsewhere = @"D:\Apps\VS\VidShrink.App.exe";
        var registry = Installed(".mp4");
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\Teknesyum.VidShrink.Video\shell\open\command", RegistryValueEntry.DefaultValueName, $"\"{elsewhere}\" \"%1\"");

        Assert.Equal(elsewhere, Handoff(registry, elsewhere).FindExecutable(".mp4", null));
    }

    [Fact]
    public void Command_pointing_back_at_Runly_is_refused()
    {
        var registry = Installed(".mp4");
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Classes\Teknesyum.VidShrink.Video\shell\open\command", RegistryValueEntry.DefaultValueName, $"\"{RunlyExe}\" \"%1\"");

        Assert.Null(Handoff(registry, RunlyExe).FindExecutable(".mp4", null));
    }

    [Fact]
    public void Runly_progid_in_capabilities_is_not_treated_as_VidShrink()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, @"Software\RegisteredApplications", "VidShrink", @"Software\Teknesyum\VidShrink\Capabilities");
        registry.Seed(RegistryRoot.CurrentUser, @"Software\Teknesyum\VidShrink\Capabilities\FileAssociations", ".mp4", "Runly.Script.mp4");

        Assert.Null(Handoff(registry, VidShrinkExe).FindExecutable(".mp4", null));
    }

    [Theory]
    [InlineData(LaunchVerb.Edit)]
    [InlineData(LaunchVerb.RunAs)]
    [InlineData(LaunchVerb.PromptArgs)]
    public void Only_the_plain_open_verb_is_handed_off(LaunchVerb verb)
    {
        var started = 0;
        var handoff = Handoff(Installed(".mp4"), VidShrinkExe, Video);

        var handed = handoff.TryHandOff(new LaunchRequest { ScriptPath = Video, Verb = verb }, new RunlyConfig(), (_, _) => { started++; return true; }, new ListLogger());

        Assert.False(handed);
        Assert.Equal(0, started);
    }

    [Fact]
    public void Failed_start_falls_back_to_Runly()
    {
        var logger = new ListLogger();
        var handoff = Handoff(Installed(".mp4"), VidShrinkExe, Video);

        var handed = handoff.TryHandOff(new LaunchRequest { ScriptPath = Video }, new RunlyConfig(), (_, _) => false, logger);

        Assert.False(handed);
        Assert.Contains(logger.Lines, line => line.Contains("başlatılamadı", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"C:\\A B\\VidShrink.App.exe\" \"%1\"", @"C:\A B\VidShrink.App.exe")]
    [InlineData(@"C:\VS\VidShrink.App.exe ""%1""", @"C:\VS\VidShrink.App.exe")]
    [InlineData("VidShrink.App.exe \"%1\"", null)]
    [InlineData("\"C:\\VS\\open.cmd\" \"%1\"", null)]
    [InlineData("", null)]
    public void Command_line_yields_the_absolute_executable(string command, string? expected)
    {
        Assert.Equal(expected, VidShrinkHandoff.CommandExecutable(command));
    }
}
