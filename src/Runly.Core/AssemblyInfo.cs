using System.Runtime.CompilerServices;

// Lets the test project exercise internal helpers (argument quoting, trust-path matching, the cache format)
// directly instead of only through the public interfaces (SPEC 11 coverage goal).
[assembly: InternalsVisibleTo("Runly.Core.Tests")]
