using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>
/// The compile-time table (docs/ai/specs/types-converter.md 9.2) answers <c>T::var</c> exactly as a prototype does: every
/// value it gives equals what a freshly constructed object of the class holds, and <c>Dm.initial</c> returns it.
/// </summary>
public class TypeConstantsTests
{
    private static readonly MethodInfo Constant = typeof(TypeOrder).GetMethod("Constant", BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly string[] Vars = { "abstract_type", "name", "icon_state", "layer", "id", "slot", "dupe_mode", "priority", "init_stage", "cost" };

    [Fact]
    public void TheTableEqualsAFreshObjectOnASampleOfTypes()
    {
        Assert.NotNull(Constant);
        int checkedValues = 0;
        var wrong = new List<string>();
        for (int i = 0; i < TypeOrder.Paths.Length; i += 37)
        {
            string path = TypeOrder.Paths[i];
            Type type = DmTypes.FromPath(path);
            if (type == null || type.IsAbstract || type.Assembly != typeof(datum).Assembly || !typeof(datum).IsAssignableFrom(type)) continue;
            object fresh = null;
            foreach (string var in Vars)
            {
                object[] args = { path, var, null };
                int found = (int)Constant.Invoke(null, args);
                if (found != 1) continue;
                fresh ??= Activator.CreateInstance(type);
                object actual = Read(fresh, var, out bool has);
                if (!has) { wrong.Add($"{path}.{var}: the class has no member"); continue; }
                checkedValues++;
                object expected = args[2];
                bool same = expected == null ? actual == null : Dm.eq(expected, actual) || Equals(expected, actual);
                if (!same) wrong.Add($"{path}.{var}: table {expected ?? "null"}, object {actual ?? "null"}");
            }
        }
        Assert.True(checkedValues > 500, $"only {checkedValues} values checked");
        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(20)));
    }

    [Fact]
    public void InitialOnATypeReadsTheTableAndAStopsPrototype()
    {
        // A generated type: the table; a hand-written var block's class: its prototype; both as tg compiles them.
        Assert.Equal(typeof(obj_machinery_door_airlock_public_glass), Dm.initial(typeof(obj_machinery_door_airlock_public_glass), (atom p) => p.type));
        Assert.Equal("public glass airlock", Dm.initial(typeof(obj_machinery_door_airlock_public_glass), (atom p) => p.name));
        Assert.Equal(typeof(obj_item), Dm.initial(typeof(obj_item), (datum p) => p.abstract_type));
        object[] args = { "/obj/item/organ/heart", "abstract_type", null };
        Assert.Equal(0, (int)Constant.Invoke(null, args)); // a hand-written var block: the stop
        Assert.Equal("/obj/item/organ/heart", args[2]);
    }

    [Fact]
    public void GetAbstractTypesMatchesTgsTestOverEverySubtypeOfDatum()
    {
        // types-converter.md 9.3: the candidates give tg's result.
        var room = new Anomaly.Core.Tests.Tg.Wave4.Mob.MobRoom();
        DmList fast = null;
        room.In(() => fast = (DmList)GlobalProcs.get_abstract_types());
        var expected = new List<Type>();
        foreach (Type t in DmTypes.Subtypes(typeof(datum), includeRoot: false))
        {
            if (t.Assembly != typeof(datum).Assembly) continue; // the fixtures are not candidates (types-converter.md 9.3)
            Type abstractType = typeof(datum).IsAssignableFrom(t) ? (Type)Read(Activator.CreateInstance(t), "abstract_type", out _) : typeof(datum);
            if (abstractType == t) expected.Add(t);
        }
        var actual = new List<Type>();
        for (int i = 1; i <= fast.Count; i++) actual.Add((Type)fast[(float)i]);
        Assert.Equal(expected, actual);
    }

    private static object Read(object o, string name, out bool has)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo f = t.GetField(name, flags);
            if (f != null) { has = true; return f.GetValue(o); }
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null && p.GetIndexParameters().Length == 0) { has = true; return p.GetValue(o); }
        }
        has = false;
        return null;
    }
}
