using Runly.Core.Models;

namespace Runly.Core.Defaults;

/// <summary>The built-in configuration used when <c>config.json</c> is missing or corrupt (SPEC 5.1).</summary>
public static class DefaultConfig
{
    /// <summary>The argument template used by most interpreters: the script path followed by the user arguments.</summary>
    public const string ScriptThenArgs = "\"{script}\" {args}";

    /// <summary>Default editor command for the "edit" verb.</summary>
    public const string DefaultEditorCommand = "code";

    /// <summary>Builds the default configuration exactly as documented in SPEC 5.1.</summary>
    public static RunlyConfig Create() => new()
    {
        Version = RunlyConfig.CurrentVersion,
        Language = "tr",
        SecurityMode = SecurityMode.TrustOnFirstUse,
        KeepWindowOpen = KeepWindowMode.OnError,
        EditorCommand = DefaultEditorCommand,
        LogEnabled = true,
        Extensions = CreateExtensions(),
    };

    /// <summary>
    /// Builds the default extension table. Only the interpreters measured as present on the target machine
    /// (node, powershell, py) are enabled; everything else ships disabled (SPEC 2, SPEC 5.1).
    /// </summary>
    public static Dictionary<string, ExtensionMapping> CreateExtensions()
    {
        var map = RunlyConfig.CreateExtensionDictionary();

        map[".js"] = new ExtensionMapping { Interpreter = "node", Args = ScriptThenArgs, Enabled = true, Icon = "js.ico" };
        map[".mjs"] = new ExtensionMapping { Interpreter = "node", Args = ScriptThenArgs, Enabled = true };
        map[".cjs"] = new ExtensionMapping { Interpreter = "node", Args = ScriptThenArgs, Enabled = true };
        map[".ts"] = new ExtensionMapping { Interpreter = "node", Args = "--experimental-strip-types \"{script}\" {args}", Enabled = false };
        map[".ps1"] = new ExtensionMapping { Interpreter = "powershell", Args = "-NoLogo -ExecutionPolicy Bypass -File \"{script}\" {args}", Enabled = true };
        map[".py"] = new ExtensionMapping { Interpreter = "py", Args = ScriptThenArgs, Enabled = true };
        map[".pyw"] = new ExtensionMapping { Interpreter = "pyw", Args = ScriptThenArgs, Enabled = false };
        map[".rb"] = new ExtensionMapping { Interpreter = "ruby", Args = ScriptThenArgs, Enabled = false };
        map[".pl"] = new ExtensionMapping { Interpreter = "perl", Args = ScriptThenArgs, Enabled = false };
        map[".lua"] = new ExtensionMapping { Interpreter = "lua", Args = ScriptThenArgs, Enabled = false };
        map[".php"] = new ExtensionMapping { Interpreter = "php", Args = ScriptThenArgs, Enabled = false };
        map[".sh"] = new ExtensionMapping { Interpreter = "bash", Args = ScriptThenArgs, Enabled = false };
        map[".r"] = new ExtensionMapping { Interpreter = "Rscript", Args = ScriptThenArgs, Enabled = false };
        map[".jar"] = new ExtensionMapping { Interpreter = "java", Args = "-jar \"{script}\" {args}", Enabled = false };

        return map;
    }

    /// <summary>Builds an empty trust store (SPEC 5.2).</summary>
    public static TrustStore CreateTrustStore() => new()
    {
        Version = TrustStore.CurrentVersion,
    };
}
