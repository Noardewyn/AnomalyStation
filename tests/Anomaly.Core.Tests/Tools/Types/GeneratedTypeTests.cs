using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>
/// The generated classes (D111, docs/ai/specs/types-converter.md 3): every type has its class, the order is OpenDream's,
/// the type tests and <c>initial()</c> work over them, and member names do not clash (rules 2.3, 2.4).
/// </summary>
public class GeneratedTypeTests
{
    private static readonly TypesIndex Index = TypesIndex.Index;

    [TgFact]
    public void EveryMetaStationTypeHasAClass()
    {
        string dmm = Path.Combine(TgClone.Root, "_maps", "map_files", "MetaStation", "MetaStation.dmm");
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        var line = new Regex(@"^\s*\(?(/[a-z][A-Za-z0-9_/]*)");
        foreach (string l in File.ReadLines(dmm))
        {
            if (l.StartsWith("(1,1,", StringComparison.Ordinal)) break;
            Match m = line.Match(l);
            if (m.Success) paths.Add(m.Groups[1].Value.TrimEnd('/'));
        }
        Assert.True(paths.Count > 2600, $"read only {paths.Count} types from {dmm}");
        var missing = paths.Where(p => DmTypes.FromPath(p) == null).ToList();
        Assert.True(missing.Count == 0, "MetaStation types without a class: " + string.Join(", ", missing.Take(20)));
        Assert.All(paths, p => Assert.True(Index.ByPath[p].Meta, p + " is not marked in the Meta closure"));
    }

    [Fact]
    public void EveryIndexedTypeWithAClassIsRegistered()
    {
        var missing = Index.Types.Where(t => t.Class != "-" && DmTypes.FromPath(t.Path) == null).Select(t => t.Path).ToList();
        Assert.True(missing.Count == 0, "indexed types without a registered class: " + string.Join(", ", missing.Take(20)));
        Assert.True(Index.Types.Count(t => t.Class != "-") > 46000, "fewer classes than tg has types");
    }

    [Fact]
    public void TheTypeOrderIsOpenDreamsPreorderAsTheIndexLists()
    {
        Assert.Equal(Index.Types.Select(t => t.Path), TypeOrder.Paths);
        Assert.Equal(TypeOrder.Count, TypeOrder.Paths.Length);
        // The 8 types tools/coloring's regex scan missed (speech_debuffs.dm), and none of OpenDream's /x/var artefacts.
        foreach (string p in new[]
        {
            "/datum/status_effect/speech/stutter/anxiety", "/datum/status_effect/speech/stutter/obsession",
            "/datum/status_effect/speech/stutter/derpspeech", "/datum/status_effect/speech/slurring",
            "/datum/status_effect/speech/slurring/generic", "/datum/status_effect/speech/slurring/drunk",
            "/datum/status_effect/speech/slurring/cult", "/datum/status_effect/speech/slurring/heretic",
        })
            Assert.Contains(p, TypeOrder.Paths);
        Assert.DoesNotContain(TypeOrder.Paths, p => p.EndsWith("/var", StringComparison.Ordinal));
        // The registry orders by it: a parent before its children, siblings in compile order.
        Type airlock = DmTypes.FromPath("/obj/machinery/door/airlock");
        Assert.True(DmTypes.TreeOrdinal(airlock) < DmTypes.TreeOrdinal(DmTypes.FromPath("/obj/machinery/door/airlock/public")));
    }

    [Fact]
    public void IstypeTypesofAndInitialWorkOverGeneratedTypes()
    {
        Type glass = DmTypes.FromPath("/obj/machinery/door/airlock/public/glass");
        Assert.NotNull(glass);
        Assert.True(glass.IsSubclassOf(typeof(obj_machinery_door_airlock)));
        Assert.Equal("/obj/machinery/door/airlock/public", DmTypes.PathOf(DmTypes.ParentOf(glass)));
        Assert.True(ispath(glass, typeof(obj_machinery_door)));
        var instance = (datum)Activator.CreateInstance(glass);
        Assert.True(istype(instance, typeof(obj_machinery_door_airlock_public)));
        Assert.True(instance is obj_machinery_door_airlock);
        DmList subtypes = Dm.typesof(typeof(obj_machinery_door_airlock_public));
        Assert.Equal(typeof(obj_machinery_door_airlock_public), subtypes[1f]);
        Assert.True(Dm.@in(glass, subtypes));
        Assert.Equal(Dm.typesof(typeof(obj_machinery_door_airlock_public)).Count, DmTypes.Subtypes(typeof(obj_machinery_door_airlock_public), true).Count);
        // initial(): the constructor's overrides in DM order, the declared defaults of the generated parents.
        Assert.Equal("public glass airlock", Dm.initial(glass, (atom p) => p.name));
        Assert.Equal(0f, Dm.initial(glass, (atom p) => p.opacity));
        Assert.Equal(typeof(obj_structure_door_assembly_door_assembly_public), Dm.initial(glass, (obj_machinery_door_airlock p) => p.assemblytype));
        Assert.Equal(Dm.resource("icons/obj/doors/airlocks/public/glass.dmi"), Dm.initial(glass, (atom p) => p.icon));
    }

    private static List<object> Items(DmList l) => Enumerable.Range(1, l.Count).Select(i => l[(float)i]).ToList();

    private static bool ispath(Type t, Type of) => DmTypes.IsSubtypeOrSame(t, of);

    private static bool istype(object x, Type t) => x != null && DmTypes.IsSubtypeOrSame(x.GetType(), t);

    [Fact]
    public void ThePathCollisionsOfRules23GetTheSuffix()
    {
        Assert.Equal("/obj/projectile/energy/tesla/cannon", DmTypes.PathOf(typeof(obj_projectile_energy_tesla_cannon)));
        Assert.Equal("/obj/projectile/energy/tesla_cannon", DmTypes.PathOf(typeof(obj_projectile_energy_tesla_cannon__2)));
        Assert.Equal("/datum/action/innate/mimic/lock", DmTypes.PathOf(typeof(datum_action_innate_mimic_lock)));
        Assert.Equal("/datum/action/innate/mimic_lock", DmTypes.PathOf(typeof(datum_action_innate_mimic_lock__2)));
    }

    [Fact]
    public void NoGeneratedFieldClashesWithAMethodOfItsClassOrItsAncestors()
    {
        // Rules 2.4: a var never shares its name with a proc along one ancestor line, the property accessors included
        // (a var named set_color would hide atom.color's setter; types-converter.md 3.4 lists the vars left out).
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var clashes = new List<string>();
        foreach (TypesIndex.TypeRow row in Index.Types.Where(t => !t.Hand && t.Class != "-"))
        {
            Type cls = DmTypes.FromPath(row.Path);
            if (cls == null || cls.Assembly != typeof(datum).Assembly) continue;
            foreach (FieldInfo f in cls.GetFields(declared))
            {
                for (Type t = cls; t != null && t != typeof(object); t = t.BaseType)
                {
                    if (t.GetMethods(declared).Any(m => m.Name == f.Name && !m.IsPrivate))
                        clashes.Add($"{row.Path}.{f.Name} vs {t.Name}.{f.Name}()");
                }
            }
        }
        Assert.True(clashes.Count == 0, "generated fields that clash with methods: " + string.Join(", ", clashes.Take(20)));
        Assert.Null(typeof(obj_item_circuit_component_holo_signboard).GetField("set_color", declared));
    }

    [Fact]
    public void GeneratedOnlyMarksExactlyTheClassesWithoutAHandWrittenPart()
    {
        var wrong = new List<string>();
        foreach (TypesIndex.TypeRow row in Index.Types.Where(t => t.Class != "-"))
        {
            Type cls = DmTypes.FromPath(row.Path);
            if (cls == null || cls.Assembly != typeof(datum).Assembly) continue;
            bool marked = cls.IsDefined(typeof(DmGeneratedOnlyAttribute), inherit: false);
            if (marked == row.Hand) wrong.Add(row.Path + (marked ? " (hand-written, marked)" : " (generated only, not marked)"));
        }
        Assert.True(wrong.Count == 0, string.Join(", ", wrong.Take(20)));
    }

    [Fact]
    public void TypesofWrittenLeavesOutTheGeneratedOnlyTypes()
    {
        DmList all = Dm.typesof(typeof(datum_mutation));
        DmList written = GlobalProcs.typesof_written(all);
        Assert.Contains(typeof(datum_mutation), Items(written));
        Assert.True(all.Count > written.Count, "tg's mutations are generated, the written list is shorter");
        Assert.All(Items(written), t => Assert.False(((Type)t).IsDefined(typeof(DmGeneratedOnlyAttribute), false), ((Type)t).Name));
        Assert.Equal(Items(written).Skip(1), Items(GlobalProcs.subtypesof_written(typeof(datum_mutation))));
    }

    [Fact]
    public void TheWrappedBootLoopsCreateNoGeneratedOnlyObject()
    {
        // Types-converter.md 8: the wrapped boot loops (subsystems, mutations, blood types, species) skip generated-only types; the other
        // boot loops (config entries, languages, and the language holders since gaps E2c: DV25) create every type as tg does.
        var world = new Anomaly.Core.Kernel.World(1);
        DmWorldState state = DmWorldState.Of(world);
        state.Files.WriteText("config/config.txt", "RUN_DELAY 1.5\nWALK_DELAY 4\n");
        state.WorldLog = _ => { };
        GeneticsStrings.Mount(world); // gaps P3: setupGenetics' speech mutations read them
        TgWorld.Boot(world, _ => TestRoom.Build(16, 16, TestRoomLayout.Classic));
        Type[] roots = { typeof(datum_controller_subsystem), typeof(datum_mutation), typeof(datum_blood_type), typeof(datum_species) };
        var generated = new List<string>();
        world.Objects.ForEachLiveInIdOrder((id, o) =>
        {
            Type t = o.GetType();
            if (t.IsDefined(typeof(DmGeneratedOnlyAttribute), false) && roots.Any(r => r.IsAssignableFrom(t))) generated.Add(DmTypes.PathOf(t));
        });
        Assert.True(generated.Count == 0, "generated-only objects of the wrapped loops in the booted world: " + string.Join(", ", generated.Distinct().Take(20)));
    }
}
