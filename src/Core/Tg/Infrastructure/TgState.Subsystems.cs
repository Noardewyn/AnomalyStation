using System.Linq;
using System.Reflection;
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// Owner: contracts (W3.0, complete). Two pieces of infrastructure for /datum/controller/master/New
// (controllers/master.dm:110-125), which the mc package translates:
// - the per-world subsystem-type filter (lead decision W3 G2): Master/New creates one subsystem per type of
//   subtypesof(/datum/controller/subsystem); the type registry also holds the test assembly's fixture subsystems, so the
//   list passes through this filter, which by default keeps only the types of the Tg assembly (production has no
//   other). A test that wants its fixtures (or only them) sets the filter on its world before Master is created.
// - the global.vars scan (lead decision W3 G3, risk R9): master.dm:115-117 looks for subsystems surviving in global
//   vars; global.vars is not translated (rules 10.7), and the GLOBAL_REAL subsystems are exactly TgState's fields of a
//   subsystem type (SUBSYSTEM_DEF, rules 10.6), so global_vars__subsystems lists their values.
public sealed partial class TgState
{
    /// <summary>
    /// PORT(test seam): which subsystem types <c>Master/New</c> creates in this world (lead decision W3 G2). Null, the
    /// default, keeps the types declared in the Tg assembly; a test sets its own predicate (for example only its fixture
    /// types) before <c>Master</c> is created. Not world state: not hashed.
    /// </summary>
    [NotHashed]
    internal Func<Type, bool> SubsystemTypeFilter;

    /// <summary>
    /// The subsystem types of <paramref name="subsystem_types"/> (a <c>subtypesof(/datum/controller/subsystem)</c>
    /// list) that <see cref="SubsystemTypeFilter"/> keeps, in their order, as a new list. Written in <c>Master/New</c> as
    /// <c>DmList subsystem_types = TgState.Current.FilterSubsystemTypes(subtypesof(typeof(datum_controller_subsystem)));
    /// // PORT(test seam)</c>.
    /// </summary>
    internal DmList FilterSubsystemTypes(DmList subsystem_types)
    {
        var kept = new DmList();
        foreach (object item in Dm.each(subsystem_types))
        {
            // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25; types-converter.md 8): by default a
            // generated-only subsystem type is left out too (a generated timer subtype would run SStimer's New and Recover).
            if (item is Type type && (SubsystemTypeFilter?.Invoke(type) ?? (type.Assembly == typeof(TgState).Assembly
                && !Attribute.IsDefined(type, typeof(DmGeneratedOnlyAttribute), inherit: false))))
                kept.Add(type);
        }
        return kept;
    }
}

public static partial class GlobalProcs
{
    /// <summary>
    /// PORT: the subsystems held by global vars, for the <c>global.vars</c> scan of <c>Master/New</c>
    /// (<c>controllers/master.dm:115-117</c>; lead decision W3 G3): the non-null values of this world's
    /// <see cref="TgState"/> fields of a subsystem type (the <c>SUBSYSTEM_DEF</c> globals, rules 10.6), in ordinal order
    /// of the field names, as a new list. Written <c>foreach (object global_var in Dm.each(global_vars__subsystems()))</c>
    /// with tg's <c>istype</c> test kept in the loop body.
    /// </summary>
    internal static DmList global_vars__subsystems()
    {
        TgState state = TgState.Current;
        var found = new DmList();
        IEnumerable<FieldInfo> fields = typeof(TgState)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => typeof(datum_controller_subsystem).IsAssignableFrom(f.FieldType))
            .OrderBy(f => f.Name, StringComparer.Ordinal);
        foreach (FieldInfo field in fields)
        {
            if (field.GetValue(state) is datum_controller_subsystem subsystem) found.Add(subsystem);
        }
        return found;
    }
}
