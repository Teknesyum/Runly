using System.Text.Json.Serialization;
using Runly.Core.Models;

namespace Runly.Core.Json;

/// <summary>
/// Source-generated JSON contracts for Runly's on-disk files. Reflection-based serialization breaks
/// under NativeAOT, so every serializable root type must be listed here (SPEC 3).
/// </summary>
[JsonSerializable(typeof(RunlyConfig))]
[JsonSerializable(typeof(TrustStore))]
internal sealed partial class RunlyJsonContext : JsonSerializerContext
{
}
