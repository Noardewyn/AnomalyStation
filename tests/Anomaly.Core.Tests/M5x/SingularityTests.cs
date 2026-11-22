using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5x;

/// <summary>
/// M5x slice 5 (D149, D150): tg's gravitational singularity (power/singularity/singularity.dm, datums/components/singularity.dm,
/// SSsinguloprocess) in a toy station room: the stage by the starting energy (600: stage 3, 160 x 160 at -64, -64, pulling 8 tiles
/// and eating 2), the turfs it eats (ScrapeAway, 2 energy each) and the items it pulls, its dissipation, a collapse at 0 energy,
/// an explosion's energy loss, two singularities merging, a person eaten (dead, the body staying until M6, D150) and the spiral
/// order of spiral_range_turfs.
/// </summary>
public class SingularityTests
{
    private const int Second = 20;
    private const int W = 40, H = 30;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= W - 1; x++)
                for (int y = 2; y <= H - 1; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
        });
        r.Step(Second);
        return r;
    }

    private static turf At(int x, int y) => locate(x, y, 1);

    private static obj_singularity Spawn(AudioRoom r, int x, int y, float energy)
        => r.In(() => Dm.@new(typeof(obj_singularity), At(x, y), energy) as obj_singularity);

    [Fact]
    public void TheSpiralGoesRingByRingFromTheCentre()
    {
        var r = Room(6601);
        r.In(() =>
        {
            DmList spiral = (DmList)spiral_range_turfs(2, At(10, 10));
            Assert.Equal(25f, spiral.len);
            Assert.Same(At(10, 10), spiral[1]);
            // ring 1 starts at its north row, west of the corner: (10, 11), (11, 11), then down the east side
            Assert.Same(At(10, 11), spiral[2]);
            Assert.Same(At(11, 11), spiral[3]);
            Assert.Same(At(11, 9), spiral[4]);
            float ring = 0;
            foreach (turf t in Dm.each_cast<turf>(spiral))
            {
                float d = Math.Max(Math.Abs(t.x - 10), Math.Abs(t.y - 10));
                Assert.True(d >= ring);
                ring = d;
            }
        });
    }

    [Fact]
    public void SixHundredEnergyIsAStageThreeThatEatsTheFloorAndPullsItems()
    {
        var r = Room(6602);
        obj_singularity singulo = Spawn(r, 20, 15, 600);
        obj_item wrench = r.In(() => Dm.@new<obj_item_wrench>(At(26, 15))); // 6 tiles east: pulled, not eaten
        r.In(() =>
        {
            Assert.Equal((float)STAGE_THREE, singulo.current_size);
            Assert.Equal("singularity_s5", singulo.icon_state);
            Assert.Equal(-64f, singulo.pixel_x);
            var c = (datum_component_singularity)singulo.GetComponent(typeof(datum_component_singularity));
            Assert.Equal(8f, Dm.num(c.grav_pull));
            Assert.Equal(2f, Dm.num(c.consume_range));
            Assert.True(Dm.truthy(c.roaming));
            Assert.Contains(c, Dm.each_cast<object>(GLOB.singularities));
        });
        float energyBefore = r.In(() => singulo.energy);
        int wrenchStart = r.In(() => (int)wrench.x);
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.False(QDELETED(singulo));
            // the floors in its consume range were scraped (iron -> plating -> space; 2 energy a turf, minus the dissipation)
            int eaten = Enumerable.Range(2, W - 2).SelectMany(x => Enumerable.Range(2, H - 2).Select(y => At(x, y))).Count(t => t is turf_open_space);
            Assert.True(eaten > 0, "it ate the floor");
            Assert.True(QDELETED(wrench) || Math.Abs(wrench.x - singulo.x) < Math.Abs(wrenchStart - 20), "the wrench was pulled toward it (or eaten)");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCeilingOverAnEatenFloorIsEatenWithIt()
    {
        var r = Room(6603);
        var ceilings = r.In(() => { var s = CeilingSystem.Of(r.World); s.Initialize(); return s; });
        r.In(() => ceilings.Set(21, 15, 1, CeilingState.Of(CeilingLattice.Lattice)));
        Assert.True(r.In(() => ceilings.At(20, 15, 1).Exists), "the floor starts under a ceiling");
        Spawn(r, 20, 15, 600);
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.False(ceilings.At(20, 15, 1).Exists, "the plating ceiling over its own tile is gone");
            Assert.False(ceilings.At(21, 15, 1).Exists, "the lattice in its consume range is gone");
            Assert.True(ceilings.At(35, 15, 1).Exists, "a ceiling out of its range stays");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStageOneSitsStillDissipatesAndCollapses()
    {
        var r = Room(6603);
        r.In(() => Waiting.NoSleep(At(30, 10).ChangeTurf(typeof(turf_open_space))));
        obj_singularity singulo = Spawn(r, 30, 10, 1); // on space: eating its own tile gives nothing
        r.In(() =>
        {
            Assert.Equal((float)STAGE_ONE, singulo.current_size);
            var c = (datum_component_singularity)singulo.GetComponent(typeof(datum_component_singularity));
            Assert.False(Dm.truthy(c.roaming), "stage one does not roam");
        });
        // SSsinguloprocess fires every tick (wait 0.5 ds): process gives dissipate the tick's 0.05 s, and only every 2 s
        // (singularity.dm:162-170, as tg does), so a stage one's 10 s dissipate delay takes 400 s
        r.Step(10 * Second);
        r.In(() =>
        {
            Assert.False(QDELETED(singulo));
            Assert.Equal(1f, singulo.energy);
            Assert.InRange(singulo.time_since_last_dissipiation, 0.2f, 0.3f); // five gates of 0.05 s
            singulo.energy = 0;
        });
        r.Step(3 * Second);
        Assert.True(r.In(() => QDELETED(singulo)), "collapsed at 0 energy (check_energy at the next 2 s act)");
        Assert.Equal(30f, r.In(() => (float)At(30, 10).x)); // it never moved
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnExplosionTakesEnergyAndTwoSingularitiesMerge()
    {
        var r = Room(6604);
        obj_singularity a = Spawn(r, 12, 15, 600);
        obj_singularity b = Spawn(r, 28, 15, 300);
        r.In(() =>
        {
            float before = a.energy;
            a.ex_act(EXPLODE_HEAVY, null);
            Assert.Equal(before - Dm.round((before + 1) / 3, 1), a.energy);
            // b eaten by a: half b's energy to a, an explosion where b was
            float aBefore = a.energy, bHalf = b.energy / 2;
            Waiting.NoSleep(a.consume(b));
            Assert.True(QDELETED(b));
            Assert.Equal(aBefore + bHalf, a.energy);
            Assert.True(SSexplosions.highturf.len + SSexplosions.medturf.len + SSexplosions.lowturf.len > 0);
        });
    }

    [Fact]
    public void APersonEatenIsGibbedAndLeavesGibs()
    {
        // M6 slice 5: tg's gib() blows the body apart (D150's stub, the body that stayed and was eaten again, is gone)
        var r = Room(6605);
        var victim = r.Dummy(20, 15);
        obj_singularity singulo = r.In(() => Dm.@new(typeof(obj_singularity), At(25, 15), 600f) as obj_singularity);
        r.In(() =>
        {
            float before = singulo.energy;
            Waiting.NoSleep(singulo.consume(victim));
            Assert.Equal(before + 20, singulo.energy); // a human with no engineer's mind: 20
            Assert.True(QDELETED(victim), "gibbed");
            Assert.Contains(Dm.each(range(3, At(20, 15))), a => a is obj_effect_decal_cleanable_blood_gibs);
        });
    }

    [Fact]
    public void APersonInItsPathIsGibbedAndAnInertHumanIsLeftAlone()
    {
        // the M7 visual check: an inert human has no DNA, organs or limbs (D112; Meta's monkeys until C5, a cerulean now), and a singularity rolling over one
        // threw in gib_animation (dna.species) every tick it was eaten again, and its glare in Stun (spec_stun reads dna.species)
        var r = Room(6611);
        var victim = r.Dummy(23, 15);
        var monkey = r.In(() => Dm.@new<mob_living_carbon_human_species_cerulean>(At(17, 15)));
        r.Step(Second);
        Assert.True(r.In(() => InertSeam.IsInert(monkey)));
        obj_singularity singulo = Spawn(r, 20, 15, 600);
        r.In(() => Waiting.NoSleep(singulo.mezzer()));
        r.Step(10 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(victim), "gibbed");
            Assert.False(QDELETED(monkey), "DV83: the inert monkey is left uneaten");
            Waiting.NoSleep(singulo.consume(monkey));
            Waiting.NoSleep(singulo.mezzer());
            Waiting.NoSleep(singulo.combust_mobs()); // a stage six's fire burns an inert monkey as tg's (singularity.dm:434)
            Assert.False(QDELETED(monkey));
        });
        r.Step(3 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDevGodmodeKeepsAPersonFromTheSingularityAndFromADevastatingBlast()
    {
        // the owner (2026-10-16): tg's singularity and a devastating blast gib a mob whatever its godmode; the dev godmode adds
        // TRAIT_BOMBIMMUNE (EX_ACT's check) and the singularity leaves a godmode mob uneaten
        var r = Room(6608);
        var victim = r.Dummy(20, 15);
        obj_singularity singulo = r.In(() => Dm.@new(typeof(obj_singularity), At(25, 15), 600f) as obj_singularity);
        r.In(() =>
        {
            DevCommands.GodMode(victim);
            float before = singulo.energy;
            Waiting.NoSleep(singulo.consume(victim));
            Assert.Equal(before, singulo.energy);
            Assert.NotEqual(DEAD, victim.stat);
            Waiting.Detach(EX_ACT(victim, EXPLODE_DEVASTATE));
            Assert.NotEqual(DEAD, victim.stat);
            DevCommands.GodMode(victim);
            Waiting.NoSleep(singulo.consume(victim));
            Assert.Equal(DEAD, victim.stat);
        });
    }

    [Theory]
    [InlineData(1200f)] // stage four: a wall in its pull comes apart 30 % of the time (walls.dm:274), anchored things stay
    [InlineData(2200f)] // stage five: walls 50 % (walls.dm:270), anchored objects are pulled too (objs.dm:165)
    public void AStageFourOrFivePullsTheStructuresApartAndInto(float energy)
    {
        // the owner's play test (2026-10-16): walls, floors and airlocks should go into the singularity too
        var r = Room(energy > 2000 ? 6610UL : 6609UL);
        r.In(() =>
        {
            for (int y = 4; y <= 26; y++)
                Waiting.NoSleep(At(30, y).ChangeTurf(typeof(turf_closed_wall)));
        });
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(At(28, 15)));
        r.Step(Second);
        obj_singularity singulo = Spawn(r, 20, 15, energy);
        r.In(() => singulo.GetComponent(typeof(datum_component_singularity)) is datum_component_singularity c ? c.roaming = FALSE : null);
        int walls() => r.In(() => Enumerable.Range(4, 23).Count(y => At(30, y) is turf_closed_wall));
        float doorX = r.In(() => door.x);
        r.Step(10 * Second);
        r.In(() =>
        {
            Assert.True(walls() < 23, "walls in its pull came apart");
            if (energy > 2000)
                Assert.True(QDELETED(door) || door.x < doorX, "the anchored airlock was pulled toward it (or eaten)");
            else
                Assert.True(!QDELETED(door) && door.x == doorX, "a stage four leaves anchored things where they are");
        });
    }

    [Fact]
    public void AForcedQdelRemovesTheSingularityAndItsComponent()
    {
        var r = Room(6606);
        obj_singularity singulo = Spawn(r, 20, 15, 50);
        r.In(() =>
        {
            var c = singulo.GetComponent(typeof(datum_component_singularity)) as datum_component_singularity;
            qdel(singulo, force: TRUE);
            Assert.True(QDELETED(singulo));
            Assert.Null(singulo.loc);
            Assert.True(QDELETED(c));
            Assert.Empty(Dm.each_cast<object>(GLOB.singularities));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ItemsThrownIntoTheSingularityAreEatenCleanly()
    {
        var r = Room(6607);
        obj_singularity singulo = Spawn(r, 20, 15, 1500); // stage 4: items in its pull are thrown at it
        var items = r.In(() => Enumerable.Range(0, 12).Select(i => (obj_item)Dm.@new<obj_item_wrench>(At(12 + (i % 4), 10 + i / 4))).ToList());
        r.Step(8 * Second);
        r.In(() => Assert.All(items, i => Assert.True(QDELETED(i) || i.x != 0)));
        // tg's drift waits its delay (movement_types.dm:71): no item is eaten inside its throw's finalize (throwing.dm:229) and no drift
        // steps again inside Moved before atmos_sensitive moves its turf_expose (atoms_movable.dm:899-902)
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInertBasicMobDeletedLeavesNoRuntime()
    {
        // a basic mob of no port yet (a blob minion, antagonists) is inert: it never ran /atom/Initialize, whose
        // `ai_controller = new ai_controller(src)` (atoms_initializing_EXPENSIVE.dm:155) Destroy's QDEL_NULL expects
        var r = Room(6608);
        var spider = r.In(() => Dm.@new<mob_living_basic_blob_minion>(At(10, 10)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(InertSeam.IsInert(spider));
            qdel(spider);
            Assert.True(QDELETED(spider));
            Assert.Null(spider.ai_controller);
        });
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APlumbingMachineDeletedLeavesNoRuntime()
    {
        // E15: plumbing is live, so the reaction chamber's reagents type path (reaction_chamber.dm:11) is the holder its Initialize's
        // create_reagents made (_plumb_machinery.dm:31), and Destroy's QDEL_NULL deletes it (no machine keeps a type path any more)
        var r = Room(6609);
        var chamber = r.In(() => Dm.@new<obj_machinery_plumbing_reaction_chamber>(At(10, 10)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(chamber));
            var holder = Assert.IsType<datum_reagents_plumbing_reaction_chamber>(chamber.reagents);
            qdel(chamber);
            Assert.True(QDELETED(chamber));
            Assert.Null(chamber.reagents);
            Assert.True(QDELETED(holder));
        });
        r.Step(Second);
        r.AssertNoRuntimes();
    }
}
