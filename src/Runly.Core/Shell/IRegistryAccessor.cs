namespace Runly.Core.Shell;

/// <summary>
/// The narrow registry surface Runly needs, kept behind an interface so the shell logic can be unit tested
/// without ever touching the real registry (SPEC 11).
/// </summary>
public interface IRegistryAccessor
{
    /// <summary>Whether the given sub key exists under the root.</summary>
    bool KeyExists(RegistryRoot root, string subKey);

    /// <summary>Immediate child key names of the given sub key; empty when the key does not exist.</summary>
    IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey);

    /// <summary>All values of the given sub key, including the unnamed default value; empty when the key does not exist.</summary>
    IReadOnlyList<RegistryValueEntry> GetValues(RegistryRoot root, string subKey);

    /// <summary>A single value, or <see langword="null"/> when the key or the value is missing.</summary>
    RegistryValueEntry? GetValue(RegistryRoot root, string subKey, string valueName);

    /// <summary>Creates the key and every missing parent; only <see cref="RegistryRoot.CurrentUser"/> is writable.</summary>
    void CreateKey(RegistryRoot root, string subKey);

    /// <summary>Writes a value, creating the key when needed; only <see cref="RegistryRoot.CurrentUser"/> is writable.</summary>
    void SetValue(RegistryRoot root, string subKey, RegistryValueEntry value);

    /// <summary>Deletes a single value; missing keys and values are ignored.</summary>
    void DeleteValue(RegistryRoot root, string subKey, string valueName);

    /// <summary>Deletes a key together with everything below it; a missing key is ignored.</summary>
    void DeleteKeyTree(RegistryRoot root, string subKey);
}
