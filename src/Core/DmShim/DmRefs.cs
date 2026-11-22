using System.Globalization;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: dynamic. The text form of references, ref() and locate(ref) (docs/ai/specs/object-model.md section 3; rules
// 10.4); conformance rows: docs/ai/specs/dm-conformance.md, "Dynamic calls and types".

/// <summary>
/// BYOND's reference text, <c>"[0x" + hex + "]"</c> (OpenDream <c>DreamRefManager.GetRefString</c>: <c>$"[0x{@ref:x}]"</c>),
/// for the values the port can give one (object-model.md section 3):
/// <list type="bullet">
/// <item>a datum with a world id: the id itself, no kind prefix (<c>"[0x1f]"</c>);</item>
/// <item>a list: the list kind in the top byte and a per-world number allocated on the list's first <c>ref()</c>, in
/// allocation order (deterministic: the order of the <c>ref()</c> calls), held weakly like the datum registry
/// (object-model.md section 6): <c>0x0F000000</c> for a plain list (BYOND's and OpenDream's
/// <c>RefType.DreamObjectList</c>; tg's <c>TYPEID_NORMAL_LIST</c> "f", <c>__DEFINES/typeids.dm</c>), <c>0x55000000</c>
/// for an alist (OpenDream <c>RefType.DreamObjectAssocList</c>);</item>
/// <item>a typepath: <c>0x09000000</c> (OpenDream <c>RefType.DreamType</c>, which uses one kind for every typepath
/// where BYOND splits 0x08 to 0x0b) and the type's ordinal in DM object-tree order (<see cref="DmTypes.TreeOrdinal"/>,
/// fixed by the generated type order);</item>
/// <item>a resource (<see cref="DmResource"/>, an icon or a sound file): <c>0x27000000</c> (OpenDream
/// <c>RefType.DreamResource</c>, "Equivalent to file") and a per-world number allocated on the first <c>ref()</c> of its
/// path, in <c>ref()</c> order, never reused (resources are values, equal by path, and live as long as the world);</item>
/// <item><c>world</c>: <c>"[0xe000000]"</c>, kind 0x0E with number 0 (BYOND's world kind: tg's Lua helper compares
/// <c>ref(key)</c> with <c>"[0xe000001]"</c> for <c>global</c>, the next number of that kind, <c>_lists.dm:1077</c>).</item>
/// </list>
/// So that a datum's ref never reads as one of these, <c>Dm.@new</c> never gives a datum an id whose top byte is one
/// of these kinds (<see cref="IsReservedId"/>). Text, numbers, prototypes and builtin values without an id (a matrix, a
/// sound) have no ref: <c>ref()</c> of them is a runtime error (an open item, object-model.md section 3).
/// </summary>
internal static class DmRefs
{
    /// <summary>The kind byte of a plain list's ref.</summary>
    internal const long ListKind = 0x0F000000;

    /// <summary>The kind byte of an alist's ref.</summary>
    internal const long AlistKind = 0x55000000;

    /// <summary>The kind byte of a typepath's ref.</summary>
    internal const long TypeKind = 0x09000000;

    /// <summary>The kind byte of a resource's ref.</summary>
    internal const long ResourceKind = 0x27000000;

    /// <summary>The ref of <c>world</c> (kind 0x0E, number 0).</summary>
    internal const long WorldRef = 0x0E000000;

    /// <summary>The number part of a kind-prefixed ref.</summary>
    internal const long NumberMask = 0x00FFFFFF;

    private const long KindMask = ~NumberMask;

    /// <summary>
    /// Whether a world id would print as a kind-prefixed ref (its top byte is the list, alist, typepath or world kind):
    /// <c>Dm.@new</c> skips such ids, so the 16.7 million ids of each of those ranges are never a datum's.
    /// </summary>
    internal static bool IsReservedId(long id)
    {
        long kind = id & KindMask;
        return kind == ListKind || kind == AlistKind || kind == TypeKind || kind == WorldRef || kind == ResourceKind;
    }

    /// <summary><c>ref(Object)</c>: the reference text, or a runtime error for a value that has none.</summary>
    internal static string RefText(object value)
    {
        long number = value switch
        {
            null => 0, // OpenDream: null is ref 0
            IDmObject datum when datum.dm_id > 0 => datum.dm_id,
            DmList list => DmWorldState.Current.ListRef(list),
            Type type => TypeRef(type),
            DmWorld => WorldRef,
            DmResource resource => DmWorldState.Current.ResourceRef(resource),
            _ => throw new DmRuntimeException("ref(): " + Dm.KindOf(value) + " has no reference (the port gives refs to datums created by new, lists, typepaths, resources and world)"),
        };
        return "[0x" + number.ToString("x", CultureInfo.InvariantCulture) + "]";
    }

    private static long TypeRef(Type type)
    {
        int ordinal = DmTypes.TreeOrdinal(type);
        if (ordinal < 0) throw new DmRuntimeException("ref(): " + (type.FullName ?? type.Name) + " is not a DM type");
        if (ordinal > NumberMask) throw new DmRuntimeException("ref(): the type order has more typepaths than a ref can number");
        return TypeKind | ordinal;
    }

    /// <summary>
    /// <c>locate(TextRef)</c> of a <c>"[0x...]"</c> reference: the datum, the list (while it lives), the typepath or
    /// <c>world</c> it names; null when it names nothing (a collected list, an unused number, a kind the port does not
    /// make, <c>"[0x0]"</c>).
    /// </summary>
    internal static object Locate(long number)
    {
        if (number <= 0) return null;
        switch (number & KindMask)
        {
            case ListKind:
            case AlistKind:
                return DmWorldState.Current.ListByRef(number);
            case TypeKind:
                return DmTypes.Registry.TypeAtOrdinal((int)(number & NumberMask));
            case WorldRef:
                return number == WorldRef ? Dm.world : null;
            case ResourceKind:
                return DmWorldState.Current.ResourceByRef(number);
        }
        return World.Current.Objects.TryGet(number, out object found) ? found : null;
    }

    /// <summary>The kind of a list's ref: alist or plain list.</summary>
    internal static long ListKindOf(DmList list) => list._isAlist ? AlistKind : ListKind;
}
