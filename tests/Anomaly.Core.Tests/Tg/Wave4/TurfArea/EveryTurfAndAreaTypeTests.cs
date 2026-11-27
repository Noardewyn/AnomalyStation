using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// One of every translated turf and area type is created and initialized (the turf half of tg's create_and_destroy.dm,
/// which creates turf types through ChangeTurf, M3, and skips areas): `new T(old)` replaces a cell, and SSatoms is up, so
/// the type's New and Initialize run at once. Types whose Initialize reaches a leaf of a later milestone are listed with
/// the leaf; when that milestone translates the leaf, its row here is to be removed.
/// </summary>
public class EveryTurfAndAreaTypeTests
{
    // Initialize paths that are Dm.NotTranslated until a later milestone (docs/ai/specs/not-translated.md): type name -> the
    // part of the NotTranslated message that names the leaf.
    private static readonly Dictionary<string, string> GapsOfLaterMilestones = new()
    {
        // (turf_open_indestructible_light's update_light is translated since M3 P3, lighting_atom.dm:44)
        // (the permalube and honk floors' permanent wet floor is translated since the bitrunning clown planet)
        // (the planetary gas mixtures of the boss and hierophant arenas are real since M2 P2: SSair.planetary, LINDA_turf_tile.dm:59)
    };

    // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25; types-converter.md 8).
    private static IEnumerable<Type> TranslatedTypes(Type root) =>
        Dm.each_cast<Type>(typesof(root)).Where(t => t.Assembly == typeof(atom).Assembly && !t.IsDefined(typeof(DmGeneratedOnlyAttribute), inherit: false));

    public static IEnumerable<object[]> TurfTypeNames() => TranslatedTypes(typeof(turf)).Select(t => new object[] { t.Name });

    public static IEnumerable<object[]> AreaTypeNames() => TranslatedTypes(typeof(area)).Select(t => new object[] { t.Name });

    [Theory]
    [MemberData(nameof(TurfTypeNames))]
    public void EveryTurfTypeIsCreatedAndInitializedUnlessItNeedsALaterMilestone(string name)
    {
        Type type = TranslatedTypes(typeof(turf)).Single(t => t.Name == name);
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            TurfAreaWorld.InitializeSSatoms();
            turf made = (turf)Dm.@new(type, locate(1, 1, 1));
            if (GapsOfLaterMilestones.TryGetValue(name, out string leaf))
            {
                Assert.Single(t.Runtimes);
                Assert.Contains("not translated yet", t.Runtimes[0].Message);
                Assert.Contains(leaf, t.Runtimes[0].Message);
            }
            else
            {
                Assert.Empty(t.Runtimes);
                // TURF-IDENTITY: a turf whose Initialize changes it (the ruins' biome-replacing rock and basalt) names the cell's final turf
                var cell = (turf)turf.dm_identity(made);
                Assert.True(Dm.truthy(Dm.bitand(cell.flags_1, INITIALIZED_1)) || type == typeof(turf_open_space_basic), name);
                Assert.Same(cell, locate(1, 1, 1));
            }
        }
    }

    [Theory]
    [MemberData(nameof(AreaTypeNames))]
    public void EveryAreaTypeIsCreatedRegisteredAndInitialized(string name)
    {
        Type type = TranslatedTypes(typeof(area)).Single(a => a.Name == name);
        var t = new TurfAreaWorld(2, 1, 1);
        using (t.World.Enter())
        {
            TurfAreaWorld.InitializeSSatoms();
            area made = (area)Dm.@new(type);
            Assert.Empty(t.Runtimes);
            Assert.True(Dm.@in(made, GLOB.areas));
            Assert.True(Dm.truthy(Dm.bitand(made.flags_1, INITIALIZED_1)));
            qdel(made);
            Assert.False(Dm.@in(made, GLOB.areas));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheGapTableNamesOnlyTurfTypesThatExist()
    {
        var names = TranslatedTypes(typeof(turf)).Select(t => t.Name).ToHashSet();
        foreach (string gap in GapsOfLaterMilestones.Keys)
            Assert.Contains(gap, names);
    }
}
