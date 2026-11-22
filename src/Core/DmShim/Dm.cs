namespace Anomaly.Core.DmShim;

/// <summary>
/// DM language semantics for translated tg code: the BYOND builtin procs and constants that need no tg type,
/// and the port's helpers (docs/ai/translation-rules.md, Appendix C). Translated files import it with
/// <c>using static Anomaly.Core.DmShim.Dm;</c>: builtins that keep their DM call shape are written bare
/// (<c>prob(50)</c>, <c>round(x, 0.5f)</c>, <c>world.time</c>), port helpers are written qualified
/// (<c>Dm.truthy(x)</c>, <c>Dm.list(...)</c>, <c>Dm.@new&lt;T&gt;(...)</c>), rules section 1.2.
/// <para>
/// The class is split into one file per owning package (docs/ai/specs/shim-ownership.md). Rules for every part:
/// no member may share a name with a tg <c>#define</c> or global proc (rules 1.6, pinned by
/// <c>DefineClashTests</c>); no mutable static state (object-model.md section 10); every number-to-text
/// conversion is culture-invariant (rules 3.9).
/// </para>
/// <para>
/// <b>A null <c>params</c> array means one null argument.</b> C# binds a single <c>null</c> argument to a
/// <c>params object[]</c> parameter as the array itself, so <c>Dm.list(null)</c>, <c>pick(null)</c> or
/// <c>Dm.Call(x, "f", null)</c> arrive with a null array. Every variadic member treats a null array as
/// <c>new object[] { null }</c>; no caller ever passes "no arguments" as null (that is an empty array).
/// </para>
/// </summary>
public static partial class Dm
{
    /// <summary>The array a null <c>params</c> array stands for (a fresh array each time: callees may keep it).</summary>
    internal static object[] OneNull() => new object[] { null };
}
