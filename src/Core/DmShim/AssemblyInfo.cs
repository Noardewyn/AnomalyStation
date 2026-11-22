using System.Runtime.CompilerServices;

// The shim's tests check internal invariants (the list snapshot, the numeric normalization) directly.
[assembly: InternalsVisibleTo("Anomaly.Core.Tests")]
