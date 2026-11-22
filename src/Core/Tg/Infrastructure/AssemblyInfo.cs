using System.Runtime.CompilerServices;

// The test seams of the Tg infrastructure are internal (translation-rules.md 10.6 keeps every TgState field of a
// translated global or static internal; the per-world subsystem-type filter of TgState.Subsystems.cs is a test seam):
// the test project sets them. Mirrors src/Core/DmShim/AssemblyInfo.cs.
[assembly: InternalsVisibleTo("Anomaly.Core.Tests")]
