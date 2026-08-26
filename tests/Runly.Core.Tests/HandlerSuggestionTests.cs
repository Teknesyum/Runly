using Runly.Settings.Discovery;

namespace Runly.Core.Tests;

public sealed class HandlerSuggestionTests
{
    private static readonly string[] Ranked = [@"C:\Tools\notepad++.exe", @"C:\Windows\notepad.exe"];

    [Fact]
    public void PicksTheTopOfTheRanking()
    {
        Assert.Equal(@"C:\Tools\notepad++.exe", HandlerSuggestion.Pick(".json", null, Ranked));
        Assert.Equal("notepad++.exe", HandlerSuggestion.DisplayName(".json", null, Ranked));
    }

    [Fact]
    public void EmptyRankingSuggestsNothing()
    {
        Assert.Null(HandlerSuggestion.Pick(".zzq", null, []));
        Assert.Null(HandlerSuggestion.DisplayName(".zzq", null, []));
        Assert.Null(HandlerSuggestion.DisplayName(".zzq", null, null));
    }

    [Fact]
    public void FilledHandlerSuggestsNothing()
    {
        Assert.Null(HandlerSuggestion.Pick(".json", @"C:\Windows\notepad.exe", Ranked));
        Assert.Null(HandlerSuggestion.DisplayName(".json", "  code.exe  ", Ranked));
    }

    [Fact]
    public void SkipsEntriesThatAreNotAFile()
    {
        string[] ranked = ["   ", @"C:\Tools\", @"C:\Tools\code.exe"];
        Assert.Equal(@"C:\Tools\code.exe", HandlerSuggestion.Pick(".json", null, ranked));
        Assert.Equal("code.exe", HandlerSuggestion.DisplayName(".json", null, ranked));
    }

    [Fact]
    public void OnlyEntriesThatAreNotAFileSuggestNothing()
    {
        Assert.Null(HandlerSuggestion.Pick(".json", null, [@"C:\Tools\", "", "   "]));
    }

    [Fact]
    public void RejectsWhatIsNotAnExtension()
    {
        Assert.Null(HandlerSuggestion.Pick(null, null, Ranked));
        Assert.Null(HandlerSuggestion.Pick("", null, Ranked));
        Assert.Null(HandlerSuggestion.Pick("   ", null, Ranked));
        Assert.Null(HandlerSuggestion.Pick(".", null, Ranked));
        Assert.Null(HandlerSuggestion.Pick("json", null, Ranked));
    }

    [Fact]
    public void TrimsThePathItReturns()
    {
        Assert.Equal(@"C:\Tools\code.exe", HandlerSuggestion.Pick(".json", null, [@"  C:\Tools\code.exe  "]));
    }
}
