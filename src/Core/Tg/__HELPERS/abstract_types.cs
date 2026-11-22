// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/abstract_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/abstract_types.dm:3  var/static/list/abstracts (rules 10.6: a TgState field)
    private static ref DmList get_abstract_types__abstracts => ref TgState.Current.GlobalProcs__get_abstract_types__abstracts;

    /// Returns a list of all abstract typepaths for all datums
    // tg: code/__HELPERS/abstract_types.dm:2
    public static object get_abstract_types()
    {
        if (Dm.truthy(get_abstract_types__abstracts))
            return get_abstract_types__abstracts;
        get_abstract_types__abstracts = Dm.list();
        // GAP(M3 P1 -> lead): lead-authorised (M3 P1 perf, types-converter.md 9): the loop runs over the candidates of tg's
        // compile-time abstract_type table (the types that may hold their own path), in tree order, instead of all of
        // subtypesof(/datum), so a world boot does not load the 46k generated classes; the test is tg's own.
        foreach (Type sometype in Dm.each_cast<Type>(abstract_type_candidates()))
        {
            if (sometype == abstract_type_of(sometype))
                get_abstract_types__abstracts[abstract_type_of(sometype)] = TRUE;
        }

        return get_abstract_types__abstracts;
    }

    // sometype::abstract_type (rules 10.3: Dm.initial). SHIM-GAP: BYOND's builtin value types (/matrix, /sound, /exception, ...)
    // are /datum subtypes and inherit tg's /datum vars, so for them the var holds /datum's default; the shim's classes of those
    // types do not derive from tg's datum class, so initial() cannot read the var there and this gives that default.
    private static Type abstract_type_of(Type sometype)
        => typeof(datum).IsAssignableFrom(sometype) ? Dm.initial(sometype, (datum p) => p.abstract_type) : typeof(datum);

    // The time complexity of these two procs is really bad, sitting at O(n * m).
    // And yet, the overhead is ridiculously small for some reason. We never figured out why.
    // So doing list subtraction instead of filtering is faster in all cases, at least for now.
    // If we ever breach like 3000-5000 abstract types, that will likely change.
    // As of right now we're at 272 or so, with subtraction being ~10x faster.
    // Curse this language, for you shall live in hell alongside me.

    /// Like subtypesof, but automatically excludes abstract typepaths
    // tg: code/__HELPERS/abstract_types.dm:21
    public static object valid_subtypesof(Type sometype = null)
    {
        return Dm.sub(subtypesof(sometype), get_abstract_types());
    }

    /// Like typesof, but automatically excludes abstract typepaths
    // tg: code/__HELPERS/abstract_types.dm:25
    public static object valid_typesof(Type sometype = null)
    {
        return Dm.sub(typesof(sometype), get_abstract_types());
    }

    /// Returns a list of concrete types under abstract sub-branches of `root`
    // tg: code/__HELPERS/abstract_types.dm:29
    public static object get_abstract_branch_descendants(Type root = null)
    {
        DmList abstracts = get_abstract_types() as DmList;
        DmList to_remove = Dm.list();
        DmList seen_abstract_parents = Dm.list();

        foreach (Type sometype in Dm.each_cast<Type>(subtypesof(root)))
        {
            if (Dm.truthy(abstracts[sometype]))
                continue;

            Type parenttype = Dm.parent_type(sometype);
            while (Dm.truthy(parenttype) && parenttype != root)
            {
                if (Dm.truthy(seen_abstract_parents[parenttype]) || Dm.truthy(abstracts[parenttype]))
                {
                    seen_abstract_parents[parenttype] = TRUE;
                    Dm.append(ref to_remove, sometype);
                    break;
                }
                parenttype = Dm.parent_type(parenttype);
            }
        }

        return to_remove;
    }

    /// Like valid_subtypesof(), but excludes concrete descendants of abstract sub-branches
    // tg: code/__HELPERS/abstract_types.dm:50
    public static object valid_direct_subtypesof(Type root = null)
    {
        DmList result = subtypesof(root);

        // Remove all abstract types
        Dm.remove(ref result, get_abstract_types());

        // Remove concrete types under abstract sub-branches
        Dm.remove(ref result, get_abstract_branch_descendants(root));

        return result;
    }

    /// Like valid_typesof(), but excludes concrete descendants of abstract sub-branches
    // tg: code/__HELPERS/abstract_types.dm:61
    public static object valid_direct_typesof(Type root = null)
    {
        DmList result = typesof(root);

        // Remove all abstract types
        Dm.remove(ref result, get_abstract_types());

        // Remove concrete types under abstract sub-branches
        Dm.remove(ref result, get_abstract_branch_descendants(root));

        return result;
    }
}

public sealed partial class TgState
{
    // tg: code/__HELPERS/abstract_types.dm:3  /proc/get_abstract_types var/static/list/abstracts
    internal DmList GlobalProcs__get_abstract_types__abstracts;
}
