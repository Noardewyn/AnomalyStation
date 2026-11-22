using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>
/// M3 T2: SSatoms' initialization of a loaded map against tg's DM (controllers/subsystem/atoms.dm InitializeAtoms and
/// CreateAtoms; game/atom/atoms_initializing_EXPENSIVE.dm InitAtom and /atom/New; __DEFINES/subsystems.dm
/// INITIALIZE_IMMEDIATE). While a map loads, tg's loader holds SSatoms.initialized at INITIALIZATION_INSSATOMS, so /atom/New
/// does not initialise; SSatoms.Initialize then walks `world` (mobs, then the other movables, then areas, then turfs, each in id
/// order: object-model.md 6) and the LATELOAD atoms follow in the order they asked. Fixture: init (2 x 2) with the probe types
/// of T2Probes.cs.
/// </summary>
public class InitOrderTests
{
    private static readonly string[] ExpectedTrace =
    {
        // the load (SSmapping): every atom is created, the immediate one initialises inside its New
        "L1 New", "N1 New", "Q1 New", "N2 New", "I1 New", "I1 Initialize mapload=1",
        // SSatoms.Initialize: the mob, the other movables in creation order (the qdel hint deletes Q1 at once), the area, the turfs
        "M Initialize mapload=1",
        "L1 Initialize mapload=1", "N1 Initialize mapload=1", "Q1 Initialize mapload=1", "Q1 Destroy", "N2 Initialize mapload=1",
        "area Initialize mapload=1",
        "turf(1,2) Initialize mapload=1", "turf(1,1) Initialize mapload=1", "turf(2,2) Initialize mapload=1", "turf(2,1) Initialize mapload=1",
        // atoms.dm:50-58: the late loaders, in the order they were added
        "L1 LateInitialize", "area LateInitialize",
    };

    [Fact]
    public void A_loaded_map_initialises_in_the_worlds_order_with_the_hints_honoured()
    {
        World world = T2Maps.Boot("init", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            Assert.Equal(ExpectedTrace, T2Trace.Of(world).Lines.ToArray());
        }
    }

    [Fact]
    public void The_qdel_hint_deletes_the_atom_and_the_others_end_up_initialised()
    {
        World world = T2Maps.Boot("init");
        using (world.Enter())
        {
            var contents = locate(1, 2, 1).contents.Items().Cast<obj_t2_init>().ToList();
            Assert.Equal(new[] { "L1", "N1" }, contents.Select(o => o.name).ToArray()); // Q1 left with its qdel
            Assert.All(contents, o => Assert.True(Dm.truthy(Dm.bitand(o.flags_1, INITIALIZED_1))));
            Assert.True(Dm.truthy(Dm.bitand(locate(1, 2, 1).flags_1, INITIALIZED_1)));
            Assert.True(Dm.truthy(Dm.bitand(locate(1, 2, 1).loc is area a ? a.flags_1 : 0f, INITIALIZED_1)));
            // everything the map made is initialised, so SSatoms has nothing left to flag (atoms.dm:24 BadInitializeCalls)
            Assert.Equal(0f, SSatoms.BadInitializeCalls.len);
            Assert.Equal(0f, SSatoms.late_loaders.len);
        }
    }

    [Fact]
    public void An_inert_type_is_initialised_without_its_own_Initialize_code()
    {
        World world = T2Maps.Boot("init", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            var inert = locate(2, 1, 1).contents.Items().OfType<obj_t2_inert>().Single();
            Assert.True(InertSeam.IsInert(inert));
            // types-converter.md 7: SSatoms.InitAtom runs dm_inert_initialize where tg calls Initialize
            Assert.DoesNotContain(T2Trace.Of(world).Lines, l => l.Contains("BAD", StringComparison.Ordinal));
            Assert.True(Dm.truthy(Dm.bitand(inert.flags_1, INITIALIZED_1)));
            Assert.Equal("X1", inert.name); // the map's edit is there all the same
            // and the immediate probe is not inert
            Assert.False(InertSeam.IsInert(locate(2, 1, 1).contents.Items().OfType<obj_t2_init_immediate>().Single()));
        }
    }

    [Fact]
    public void Every_atom_of_a_loaded_map_is_initialised_whether_its_type_is_live_or_inert()
    {
        // the preload map holds live types (the probes), a generated inert one (/obj/item/crowbar) and turfs and an area
        World world = T2Maps.Boot("preload", out TgTestWorld.Log log);
        using (world.Enter())
        {
            Assert.Empty(log.Runtimes);
            for (int y = 1; y <= Dm.world.maxy; y++)
            {
                for (int x = 1; x <= Dm.world.maxx; x++)
                {
                    turf t = locate(x, y, 1);
                    Assert.True(Dm.truthy(Dm.bitand(t.flags_1, INITIALIZED_1)), $"turf ({x},{y})");
                    foreach (atom_movable m in t.contents.Items())
                        Assert.True(Dm.truthy(Dm.bitand(m.flags_1, INITIALIZED_1)), $"{m.GetType().Name} at ({x},{y})");
                }
            }
            foreach (area a in GLOB.areas.Items().Cast<area>())
                Assert.True(Dm.truthy(Dm.bitand(a.flags_1, INITIALIZED_1)), a.GetType().Name);
        }
    }
}
