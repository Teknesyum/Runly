namespace Runly.Core.Shell;

/// <summary>How Runly hides a context-menu entry it does not own.</summary>
public enum MenuHideMethod
{
    /// <summary>A static verb, narrowed with an HKCU <c>AppliesTo</c> overlay; only Runly's own file types lose it.</summary>
    AppliesTo,

    /// <summary>A shell extension handler, listed under <c>Shell Extensions\Blocked</c>; every item type loses it and Explorer must restart.</summary>
    Blocked,
}

/// <summary>A context-menu entry found on Runly's file types that Runly can hide.</summary>
public sealed record ContextMenuItem
{
    /// <summary>Stable identifier stored in <c>hiddenMenuItems</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The text the user sees in the menu, or the closest name Runly could find.</summary>
    public required string Label { get; init; }

    /// <summary>How the entry is hidden.</summary>
    public required MenuHideMethod Method { get; init; }

    /// <summary>Where the entry comes from, for the settings list.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>File types that lose the entry when it is hidden; <see langword="null"/> means every file.</summary>
    public IReadOnlyList<string>? Extensions { get; init; }

    /// <summary>HKCR-relative verb keys an <see cref="MenuHideMethod.AppliesTo"/> entry is made of.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>Handler CLSIDs a <see cref="MenuHideMethod.Blocked"/> entry is made of.</summary>
    public IReadOnlyList<string> Clsids { get; init; } = [];

    /// <summary>Whether the entry opens the file in an editor, duplicating "Runly: Düzenle".</summary>
    public bool IsEditor { get; init; }

    /// <summary>Whether Runly hides it when the user has not chosen: an editor duplicate that stays off other files.</summary>
    public bool Recommended { get; init; }

    /// <summary>Whether the entry is hidden right now.</summary>
    public bool Hidden { get; init; }

    /// <summary>Whether Runly can hide the entry itself; when false the preview offers a way to the owning program.</summary>
    public bool Manageable { get; init; } = true;

    /// <summary>What to start when Runly cannot hide the entry: a settings URI or an executable path.</summary>
    public string? ConfigureTarget { get; init; }

    /// <summary>A Turkish line explaining why the entry is offered or why it is useless right now.</summary>
    public string? Note { get; init; }
}

/// <summary>Outcome of applying or reverting the context-menu cleanup.</summary>
public sealed record MenuCleanupResult
{
    /// <summary>Turkish, user-visible log of what was done.</summary>
    public IReadOnlyList<string> Actions { get; init; } = [];

    /// <summary>Whether the blocked-handler list changed; Explorer reads it once per process.</summary>
    public bool ExplorerRestartNeeded { get; init; }
}
