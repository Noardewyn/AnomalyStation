using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>
/// The inert rule (D112, docs/ai/specs/types-converter.md 4): <c>[DmInert]</c> is on exactly the types the rule makes inert,
/// computed here from the Tg assembly by reflection and the checked-in index, so it runs without the clones.
/// </summary>
public class InertMarkTests
{
    private static readonly TypesIndex Index = TypesIndex.Index;

    private static readonly HashSet<string> LiveBase = new(
        File.ReadLines(Path.Combine(TgClone.RepositoryRoot, "docs", "ai", "specs", "generated", "live-base.txt")).Where(l => l.StartsWith('/')),
        StringComparer.Ordinal);

    /// <summary>The DM names of the methods <paramref name="cls"/> declares (rules 2.2, 2.4), without the generated stubs.</summary>
    private static HashSet<string> Declared(Type cls, out HashSet<string> stubs)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        stubs = new HashSet<string>(StringComparer.Ordinal);
        foreach (MethodInfo m in cls.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (m.IsSpecialName || m.IsDefined(typeof(CompilerGeneratedAttribute))) continue;
            string name = m.Name.EndsWith("__proc", StringComparison.Ordinal) ? m.Name[..^"__proc".Length] : m.Name;
            (m.IsDefined(typeof(DmGeneratedStubAttribute)) ? stubs : names).Add(name);
        }
        return names;
    }

    [Fact]
    public void TheIndexTranslatedColumnIsWhatTheTgAssemblyDeclares()
    {
        // Rerun the tool after adding or removing a translated proc (types-converter.md 2): this test catches a stale index.
        var wrong = new List<string>();
        foreach (var group in Index.Procs.Where(p => p.Owner != "/").GroupBy(p => p.Owner))
        {
            Type cls = DmTypes.FromPath(group.Key);
            HashSet<string> stubs = new(StringComparer.Ordinal);
            HashSet<string> declared = cls == null ? new() : Declared(cls, out stubs);
            foreach (TypesIndex.ProcRow p in group)
            {
                string expected = declared.Contains(p.Name) ? "yes" : stubs.Contains(p.Name) ? "stub" : "no";
                if (p.Translated != expected) wrong.Add($"{p.Owner}/{p.Name}: index {p.Translated}, assembly {expected}");
            }
        }
        Assert.True(wrong.Count == 0, "stale types-index.tsv (rerun tools/Anomaly.Tools.Types): " + string.Join("; ", wrong.Take(20)));
    }

    [Fact]
    public void DmInertIsExactlyOnTheTypesTheRuleMakesInert()
    {
        // Types-converter.md 4.2: live when in the live base, or when the parent is live and every tg proc is declared.
        var inert = new Dictionary<string, bool>(StringComparer.Ordinal);
        var wrong = new List<string>();
        foreach (TypesIndex.TypeRow t in Index.Types)
        {
            bool isInert;
            if (t.Class == "-" || t.State == "none" || LiveBase.Contains(t.Path)) isInert = false;
            else
            {
                bool undeclared = Index.ProcsAt(t.Path).Any(p => p.Translated == "no");
                bool parentInert = inert.TryGetValue(t.Parent, out bool pi) && pi;
                isInert = undeclared || parentInert;
            }
            inert[t.Path] = isInert;
            string state = LiveBase.Contains(t.Path) ? "base" : t.Class == "-" || t.State == "none" ? "none" : isInert ? "inert" : "live";
            if (t.State != state) wrong.Add($"{t.Path}: index {t.State}, rule {state}");
            Type cls = DmTypes.FromPath(t.Path);
            if (cls != null && cls.IsDefined(typeof(DmInertAttribute), inherit: false) != isInert)
                wrong.Add($"{t.Path}: [DmInert] {(isInert ? "missing" : "on a live type")}");
        }
        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(20)));
    }

    [Fact]
    public void HandPickedTypesFollowTheRule()
    {
        // The raw bluespace core that defines no procs: inert while /obj/item/raw_anomaly_core's procs are not declared (the anomaly
        // cores wait; the mass drivers of this example went live with the ordnance lab, the departmental protolathe in gaps RD1, the
        // syndicate bomb in the syndicate campaign G2, the air sensor in engineering package E5, the thermomachine in M7 slice 4, the
        // firelock of M3's example in M4b slice 2).
        Assert.Empty(Index.ProcsAt("/obj/item/raw_anomaly_core/bluespace"));
        Assert.Contains(Index.ProcsAt("/obj/item/raw_anomaly_core"), p => p.Translated == "no");
        AssertInert(typeof(obj_item_raw_anomaly_core), true);
        AssertInert(typeof(obj_item_raw_anomaly_core_bluespace), true);
        // M3 P3 declared every proc of /obj/machinery/door/airlock (translated or live-partial stubs): it and its procless
        // subtypes are live.
        Assert.Empty(Index.ProcsAt("/obj/machinery/door/airlock/public/glass/incinerator"));
        Assert.DoesNotContain(Index.ProcsAt("/obj/machinery/door/airlock"), p => p.Translated == "no");
        AssertInert(typeof(obj_machinery_door_airlock), false);
        AssertInert(typeof(obj_machinery_door_airlock_public_glass_incinerator), false);
        // The M2 human and its organs: the live base.
        foreach (Type t in new[] { typeof(mob_living_carbon_human), typeof(obj_item_organ_lungs), typeof(obj_item_organ_heart), typeof(obj_item_bodypart_chest), typeof(obj_machinery_door) })
        {
            Assert.Contains(DmTypes.PathOf(t), LiveBase);
            AssertInert(t, false);
        }
        // A var-only subtype of a live base type: live.
        Assert.Empty(Index.ProcsAt("/obj/item/clothing/under/rank/security"));
        Assert.Equal("live", Index.ByPath["/obj/item/clothing/under/rank/security"].State);
        AssertInert(typeof(obj_item_clothing_under_rank_security), false);
        // A type with one undeclared proc under a live base type: inert.
        // (the chicken cutlet's, the example until gaps H2 translated it; the okonomiyaki's until the restaurant; now the cult's unholy water)
        Assert.Equal(new[] { "Initialize" }, Index.ProcsAt("/obj/item/reagent_containers/cup/beaker/unholywater").Where(p => p.Translated == "no").Select(p => p.Name).Distinct());
        AssertInert(typeof(obj_item_reagent_containers_cup_beaker_unholywater), true);
        // A data-only datum type under /datum: live, and its class is generated only.
        AssertInert(typeof(datum_bitfield_status_flags), false);
        Assert.True(typeof(datum_bitfield_status_flags).IsDefined(typeof(DmGeneratedOnlyAttribute), false));
    }

    private static void AssertInert(Type t, bool inert)
        => Assert.True(t.IsDefined(typeof(DmInertAttribute), inherit: false) == inert, $"{DmTypes.PathOf(t)} should {(inert ? "" : "not ")}be inert");
}
