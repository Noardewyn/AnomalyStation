using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Owner: M3 P1. The boot-enumeration rule for generated-only types (docs/ai/specs/types-converter.md section 8, DV25).

public static partial class GlobalProcs
{
    /// <summary>
    /// PORT (types-converter.md 8, DV25): the types of <paramref name="types"/> (a <c>typesof</c>, <c>subtypesof</c> or
    /// <c>valid_subtypesof</c> list), in their order, as a new list, without the generated-only classes
    /// (<see cref="DmGeneratedOnlyAttribute"/>). Used only at a boot site that creates one object per listed type and
    /// breaks on the types nobody has translated yet, with a <c>GAP(M3 P1 -&gt; owner)</c> comment.
    /// </summary>
    public static DmList typesof_written(object types)
    {
        var kept = Dm.list();
        foreach (object item in Dm.each(types))
        {
            if (item is Type type && Attribute.IsDefined(type, typeof(DmGeneratedOnlyAttribute), inherit: false)) continue;
            kept.Add(item);
        }
        return kept;
    }

    /// <summary><c>typesof_written(subtypesof(T))</c> (types-converter.md 8).</summary>
    public static DmList subtypesof_written(object typepath) => typesof_written(subtypesof(typepath));

    /// <summary>
    /// PORT (types-converter.md 9; lead-authorised, M3 P1 perf): the /datum subtypes that may hold their own path in
    /// <c>abstract_type</c>, in tree order: from tg's compile-time table (<c>TypeOrder.SelfReferenceCandidates</c>) the types
    /// whose generated part sets it to their own path or to a subtype's, and every class with a hand-written var block.
    /// <c>get_abstract_types</c> makes tg's own test on each instead of loading every class of <c>subtypesof(/datum)</c>.
    /// The test assemblies' fixture types are not candidates.
    /// </summary>
    public static DmList abstract_type_candidates()
    {
        DmList candidates = Dm.list();
        foreach (string path in TypeOrder.SelfReferenceCandidates("abstract_type"))
        {
            if (path == "/datum") continue;
            Type type = DmTypes.FromPath(path);
            if (type != null && DmTypes.IsSubtypeOrSame(type, typeof(datum))) candidates.Add(type);
        }
        return candidates;
    }
}
