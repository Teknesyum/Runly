using System.Runtime.Versioning;
using Runly.Launcher.Cli;

namespace Runly.Core.Tests;

[SupportedOSPlatform("windows")]
public sealed class ArgumentSplitterTests
{
    [Fact]
    public void Split_ExplicitEmptyArgument_IsPreserved()
    {
        var result = ArgumentSplitter.Split("alpha \"\" omega");

        Assert.Equal(["alpha", string.Empty, "omega"], result);
    }

    [Fact]
    public void Split_QuotedWhitespace_RemainsOneArgument()
    {
        var result = ArgumentSplitter.Split("\"hello world\" tail");

        Assert.Equal(["hello world", "tail"], result);
    }
}
