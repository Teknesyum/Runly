using Runly.Core.Shell;

namespace Runly.Core.Tests.Shell;

/// <summary>Covers UserChoice detection, including the real <c>.ps1</c> situation on the target machine (SPEC 2).</summary>
public sealed class UserChoiceInspectorTests
{
    /// <summary>The ProgID measured on the target machine for <c>.ps1</c>: the Microsoft Store Notepad.</summary>
    private const string StoreNotepadProgId = "AppXxf01pj590w7z9mxmyv3nx0a9ewj3e51g";

    [Fact]
    public void No_UserChoice_key_means_the_extension_is_free()
    {
        var inspector = new UserChoiceInspector(new FakeRegistryAccessor());

        var state = inspector.Check(".py");

        Assert.Equal(UserChoiceOwner.None, state.Owner);
        Assert.Null(state.ProgId);
    }

    [Fact]
    public void An_empty_ProgId_value_is_treated_as_free()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".py"), "ProgId", "");

        Assert.Equal(UserChoiceOwner.None, new UserChoiceInspector(registry).Check(".py").Owner);
    }

    [Fact]
    public void The_Store_Notepad_holding_ps1_is_reported_generically()
    {
        // SPEC 2: this is the measured state of the target machine and must be reported correctly.
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);

        var state = new UserChoiceInspector(registry).Check(".ps1");

        Assert.Equal(UserChoiceOwner.OwnedByOther, state.Owner);
        Assert.Equal(StoreNotepadProgId, state.ProgId);
        Assert.Equal("bir Microsoft Store uygulaması", state.FriendlyName);
    }

    [Fact]
    public void A_classic_ProgId_is_resolved_through_HKCR()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "JSFile");
        registry.Seed(RegistryRoot.ClassesRoot, "JSFile", "", "JScript Script File");

        var state = new UserChoiceInspector(registry).Check(".js");

        Assert.Equal(UserChoiceOwner.OwnedByOther, state.Owner);
        Assert.Equal("JScript Script File", state.FriendlyName);
    }

    [Fact]
    public void An_Applications_ProgId_prefers_FriendlyAppName()
    {
        // Measured on the target machine: .txt is held by ProgID "Applications\notepad++.exe".
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".txt"), "ProgId", @"Applications\notepad++.exe");
        registry.Seed(RegistryRoot.ClassesRoot, @"Applications\notepad++.exe", "FriendlyAppName", "Notepad++");

        Assert.Equal("Notepad++", new UserChoiceInspector(registry).Check(".txt").FriendlyName);
    }

    [Fact]
    public void An_Applications_ProgId_without_a_name_falls_back_to_the_executable_name()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".txt"), "ProgId", @"Applications\notepad++.exe");

        Assert.Equal("notepad++.exe", new UserChoiceInspector(registry).Check(".txt").FriendlyName);
    }

    [Fact]
    public void An_unresolvable_ProgId_falls_back_to_the_ProgId_itself()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "Bilinmeyen.Uygulama");

        Assert.Equal("Bilinmeyen.Uygulama", new UserChoiceInspector(registry).Check(".js").FriendlyName);
    }

    [Fact]
    public void FriendlyTypeName_is_used_when_the_default_value_is_missing()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "Foo.Bar");
        registry.Seed(RegistryRoot.ClassesRoot, "Foo.Bar", "FriendlyTypeName", "Foo Düzenleyici");

        Assert.Equal("Foo Düzenleyici", new UserChoiceInspector(registry).Check(".js").FriendlyName);
    }

    [Fact]
    public void Runlys_own_ProgId_is_recognised()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "Runly.Script.js");

        var state = new UserChoiceInspector(registry).Check(".js");

        Assert.Equal(UserChoiceOwner.OwnedByRunly, state.Owner);
        Assert.Equal("Runly", state.FriendlyName);
    }

    [Theory]
    [InlineData("js")]
    [InlineData(".JS")]
    [InlineData("  .js  ")]
    public void Extension_spelling_is_normalised(string extension)
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".js"), "ProgId", "JSFile");

        Assert.Equal(UserChoiceOwner.OwnedByOther, new UserChoiceInspector(registry).Check(extension).Owner);
    }

    [Fact]
    public void The_inspector_never_writes_to_the_registry()
    {
        var registry = new FakeRegistryAccessor();
        registry.Seed(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "ProgId", StoreNotepadProgId);
        var before = registry.AllKeys(RegistryRoot.CurrentUser).Count;

        new UserChoiceInspector(registry).Check(".ps1");

        // SPEC 2 forbids writing, deleting or forging the UserChoice key.
        Assert.Equal(before, registry.AllKeys(RegistryRoot.CurrentUser).Count);
        Assert.Null(registry.GetValue(RegistryRoot.CurrentUser, UserChoiceInspector.UserChoiceKey(".ps1"), "Hash"));
    }
}
