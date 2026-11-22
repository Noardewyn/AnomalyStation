namespace Anomaly.Core.Tg;

/// <summary>
/// BYOND builtin procs that need atoms, turfs or the map (<c>get_step</c>, <c>get_dist</c>, <c>range</c>, <c>view</c>,
/// <c>locate(x, y, z)</c>, <c>ismob</c>, <c>isturf</c>, ...), written bare through
/// <c>using static Anomaly.Core.Tg.Builtins;</c> (docs/ai/translation-rules.md 1.5, Appendix C.1). Members come with the
/// atom wave, one part per file in <c>src/Core/Tg/Builtin/*.builtin.cs</c>; none may share a name with a tg
/// <c>#define</c> or global proc (rules 1.6).
/// </summary>
public static partial class Builtins
{
}
