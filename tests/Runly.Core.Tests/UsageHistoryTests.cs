using Runly.Core.Models;
using Runly.Settings.Discovery;

namespace Runly.Core.Tests;

public sealed class UsageHistoryTests
{
    private sealed class FakeSource : IUsageHistorySource
    {
        public string? Order { get; set; }

        public Dictionary<string, string> List { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ProgIds { get; } = [];

        public Dictionary<string, string> Resolvable { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ProgIdTargets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ResolveCalls { get; } = [];

        public string? OpenWithListOrder(string extension) => Order;

        public IReadOnlyDictionary<string, string> OpenWithList(string extension) => List;

        public IReadOnlyList<string> OpenWithProgIds(string extension) => ProgIds;

        public string? ResolveExecutable(string candidate)
        {
            ResolveCalls.Add(candidate);
            return Resolvable.TryGetValue(candidate, out var path) ? path : null;
        }

        public string? ResolveProgId(string progId) =>
            ProgIdTargets.TryGetValue(progId, out var path) ? path : null;
    }

    private static FakeSource ThreeEntries()
    {
        var source = new FakeSource { Order = "cab" };
        source.List["a"] = "code.exe";
        source.List["b"] = "wordpad.exe";
        source.List["c"] = "notepad++.exe";
        source.Resolvable["code.exe"] = @"C:\Apps\code.exe";
        source.Resolvable["wordpad.exe"] = @"C:\Apps\wordpad.exe";
        source.Resolvable["notepad++.exe"] = @"C:\Apps\notepad++.exe";
        return source;
    }

    [Fact]
    public void MruListOrderIsPreserved()
    {
        var ranked = UsageHistory.Rank(".json", ThreeEntries(), null);

        Assert.Equal(
            [@"C:\Apps\notepad++.exe", @"C:\Apps\code.exe", @"C:\Apps\wordpad.exe"],
            ranked);
    }

    [Fact]
    public void MissingMruListFallsBackToValueNameOrder()
    {
        var source = ThreeEntries();
        source.Order = null;

        var ranked = UsageHistory.Rank(".json", source, null);

        Assert.Equal(
            [@"C:\Apps\code.exe", @"C:\Apps\wordpad.exe", @"C:\Apps\notepad++.exe"],
            ranked);
    }

    [Fact]
    public void CorruptMruListKeepsTheLettersItDoesNameAndAppendsTheRest()
    {
        var source = ThreeEntries();
        source.Order = "cz c";

        var ranked = UsageHistory.Rank(".json", source, null);

        Assert.Equal(
            [@"C:\Apps\notepad++.exe", @"C:\Apps\code.exe", @"C:\Apps\wordpad.exe"],
            ranked);
    }

    [Fact]
    public void UnresolvablePathIsDropped()
    {
        var source = ThreeEntries();
        source.Resolvable.Remove("code.exe");

        var ranked = UsageHistory.Rank(".json", source, null);

        Assert.Equal([@"C:\Apps\notepad++.exe", @"C:\Apps\wordpad.exe"], ranked);
    }

    [Fact]
    public void PackagedApplicationMonikerIsSkippedWithoutAskingTheSource()
    {
        var source = new FakeSource { Order = "ab" };
        source.List["a"] = "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App";
        source.List["b"] = "notepad++.exe";
        source.Resolvable["notepad++.exe"] = @"C:\Apps\notepad++.exe";

        var ranked = UsageHistory.Rank(".txt", source, null);

        Assert.Equal([@"C:\Apps\notepad++.exe"], ranked);
        Assert.DoesNotContain(source.ResolveCalls, call => call.Contains('!', StringComparison.Ordinal));
    }

    [Fact]
    public void RunlyDoesNotSuggestItself()
    {
        var source = new FakeSource { Order = "abc" };
        source.List["a"] = "Runly.exe";
        source.List["b"] = "RunlySettings.exe";
        source.List["c"] = "notepad++.exe";
        source.Resolvable["Runly.exe"] = @"C:\Program Files\Runly\Runly.exe";
        source.Resolvable["RunlySettings.exe"] = @"C:\Program Files\Runly\RunlySettings.exe";
        source.Resolvable["notepad++.exe"] = @"C:\Apps\notepad++.exe";

        var ranked = UsageHistory.Rank(".json", source, null);

        Assert.Equal([@"C:\Apps\notepad++.exe"], ranked);
    }

    [Fact]
    public void EmptyOrDottedExtensionYieldsNothing()
    {
        Assert.Empty(UsageHistory.Rank(string.Empty, ThreeEntries(), null));
        Assert.Empty(UsageHistory.Rank("   ", ThreeEntries(), null));
        Assert.Empty(UsageHistory.Rank(".", ThreeEntries(), null));
        Assert.Empty(UsageHistory.Rank("json", ThreeEntries(), null));
    }

    [Fact]
    public void ProgIdsRankBelowTheOpenWithList()
    {
        var source = new FakeSource { Order = "a" };
        source.List["a"] = "notepad++.exe";
        source.Resolvable["notepad++.exe"] = @"C:\Apps\notepad++.exe";
        source.ProgIds.Add("VSCode.json");
        source.ProgIdTargets["VSCode.json"] = @"C:\Apps\code.exe";

        var ranked = UsageHistory.Rank(".json", source, null);

        Assert.Equal([@"C:\Apps\notepad++.exe", @"C:\Apps\code.exe"], ranked);
    }

    [Fact]
    public void RunlysOwnChoicesRankBelowWindowsAndTheSameExtensionBeatsItsNeighbours()
    {
        var source = new FakeSource();
        source.Resolvable[@"C:\Apps\sublime.exe"] = @"C:\Apps\sublime.exe";
        source.Resolvable[@"C:\Apps\code.exe"] = @"C:\Apps\code.exe";

        var mappings = new Dictionary<string, ExtensionMapping>(StringComparer.OrdinalIgnoreCase)
        {
            [".txt"] = new() { Category = "text", Kind = HandlerKind.Open, OpenWith = @"C:\Apps\sublime.exe" },
            [".md"] = new() { Category = "text", Kind = HandlerKind.Open, OpenWith = @"C:\Apps\code.exe" },
        };

        var ranked = UsageHistory.Rank(".txt", source, mappings);

        Assert.Equal([@"C:\Apps\sublime.exe", @"C:\Apps\code.exe"], ranked);
    }

    [Fact]
    public void DuplicateExecutableKeepsItsBestPosition()
    {
        var source = new FakeSource { Order = "ab" };
        source.List["a"] = "np.exe";
        source.List["b"] = "notepad++.exe";
        source.Resolvable["np.exe"] = @"C:\Apps\notepad++.exe";
        source.Resolvable["notepad++.exe"] = @"C:\APPS\NOTEPAD++.EXE";

        var ranked = UsageHistory.Rank(".json", source, null);

        Assert.Equal([@"C:\Apps\notepad++.exe"], ranked);
    }

    [Fact]
    public void OrderByMruIgnoresBlankValues()
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "  ",
            ["b"] = " notepad++.exe ",
        };

        Assert.Equal(["notepad++.exe"], UsageHistory.OrderByMru("ab", entries));
    }
}
