using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Owner: the contract is the M3 contracts step's and frozen (docs/ai/specs/m3-ownership.md); M3 P2 implements it. The engine
// part of tg's preloader: BYOND's `what.vars[attribute] = value` (preloader.dm:20-35) without `vars`, which the port does
// not translate (rules 10.7).

/// <summary>Sets a DM var of an object by its DM name (docs/ai/specs/map-format.md 5).</summary>
public static class MapVars
{
    /// <summary>
    /// Assigns <paramref name="value"/> to the var named <paramref name="var"/> (its DM spelling, rules 2.1) of
    /// <paramref name="target"/>: the field of that name on the instance's class or an ancestor (rules 2.2's <c>@</c> names
    /// match without it; a builtin var with an engine property, <c>dir</c>, <c>name</c>, <c>icon_state</c>, goes through its
    /// setter, as BYOND's), converted to the field's C# type by rules 3.5 (<c>float</c>, <c>string</c>, <c>Type</c>,
    /// <see cref="DmList"/>, <c>object</c>); a list value is deep-copied (tg's <c>deep_copy_list</c>). False with
    /// <paramref name="error"/> set when the var does not exist or the value cannot convert (tg: a runtime, which the loader
    /// reports). Inside a world.
    /// </summary>
    public static bool TrySet(datum target, string var, object value, out string error)
    {
        ArgumentNullException.ThrowIfNull(target);
        try
        {
            // preloader.dm:24-25: a list value is deep-copied, so no two atoms share one
            if (value is DmList list) value = GlobalProcs.deep_copy_list(list);
            // BYOND's `what.vars[attribute] = value`: the shim's by-name assignment (rules 4.8, DmVars: the field or the
            // engine property of that DM name, the most derived declaration, converted by rules 3.5)
            Dm.set_var(target, var, value);
            error = null;
            return true;
        }
        catch (Exception e) when (e is DmRuntimeException or InvalidCastException or FormatException or OverflowException)
        {
            error = e.Message;
            return false;
        }
    }
}
