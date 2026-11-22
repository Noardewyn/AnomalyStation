using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E4: tg's reinforced floor (game/turfs/open/floor/reinforced_floor.dm) in a booted toy room. The engine floor
/// and its variants are live: a crowbar does nothing, a wrench takes the rods off in three seconds and leaves the plating
/// (keeping the air), the tile is unbreakable, unburnable and unplateable, explosions per tg's ex_act (a devastating blast takes two
/// layers, a heavy one half the tiles one layer, a light one nothing), the contents under it are shielded from all but a devastating
/// blast, the singularity of stage five shears it, and the gas-filled variants keep their air.
/// </summary>
public class EngineFloorTests
{
    private const int Second = 20;
    private const int X = 5, Y = 4;
    private const int W = 44, H = 12;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed, int width = 12, int height = 9)
    {
        var r = new AudioRoom(width, height, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    // A station tile: the floor over plating over space (the floor's baseturfs, as on the station).
    private static turf Floor(Type type, int x, int y)
        => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(type, Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));

    private static turf At(int x, int y) => locate(x, y, 1);

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    [Fact]
    public void ACrowbarDoesNothingAndAWrenchTakesTheRodsOffAndKeepsTheAir()
    {
        var (r, bob) = Room(7401);
        var floor = (turf_open)r.In(() => Floor(typeof(turf_open_floor_engine), X, Y));
        r.Step(Second);

        // reinforced_floor.dm:41 crowbar_act returns nothing: the floor stays, nothing comes up
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, floor, 2);
        Assert.Same(floor, r.In(() => At(X, Y)));
        Assert.Empty(On<obj_item_stack_rods>(r, X, Y));
        Assert.DoesNotContain(r.Sounds.Heard, h => h.File == "sound/items/tools/crowbar.ogg");

        // :44 wrench_act: "You begin removing rods...", use_tool(30, volume 80): three seconds, the ratchet; then the rods and the plating
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, floor, 1);
        Assert.Same(floor, r.In(() => At(X, Y)));
        r.Step(30);
        r.In(() => floor.air.temperature = 350); // warm the cell just before the rods come off
        for (int tick = 0; tick < 3 * Second && r.In(() => At(X, Y)) is not turf_open_floor_plating; tick++)
            r.Step(1);
        var plating = Assert.IsType<turf_open_floor_plating>(r.In(() => At(X, Y)));
        var rods = Assert.Single(On<obj_item_stack_rods>(r, X, Y));
        Assert.Equal(2f, r.In(() => Dm.num(rods.amount)));
        Assert.True(r.In(() => plating.air.temperature) > 310f, "CHANGETURF_INHERIT_AIR: the plating keeps the floor's air");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/tools/ratchet.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARemovedFloorIsNotWrenchedAgainWhenItWasReplacedDuringTheWait()
    {
        var (r, bob) = Room(7402);
        var floor = r.In(() => Floor(typeof(turf_open_floor_engine), X, Y));
        r.Step(Second);
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, floor, 1);
        // the floor is scraped away by someone else during the wait: tg's istype(src, /turf/open/floor/engine) asks the cell
        r.In(() => Waiting.NoSleep(At(X, Y).ScrapeAway()));
        r.Step(4 * Second);
        Assert.IsType<turf_open_floor_plating>(r.In(() => At(X, Y)));
        Assert.Empty(On<obj_item_stack_rods>(r, X, Y));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheInsulatedFloorGivesOneSheetOfPlastitaniumAndTheCultFloorGivesNothing()
    {
        var (r, bob) = Room(7403);
        var insulation = r.In(() => Floor(typeof(turf_open_floor_engine_insulation), X, Y));
        var cult = r.In(() => Floor(typeof(turf_open_floor_engine_cult), X, Y + 1));
        r.Step(Second);
        Assert.Equal("plating", r.In(() => cult.icon_state)); // reinforced_floor.dm:200 the base icon state is redefined
        Assert.Equal(0f, r.In(() => ((turf_open_floor_engine)insulation).thermal_conductivity));

        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, insulation, 5);
        Assert.IsType<turf_open_floor_plating>(r.In(() => At(X, Y)));
        var sheet = Assert.Single(On<obj_item_stack_sheet_mineral_plastitanium>(r, X, Y));
        Assert.Equal(1f, r.In(() => Dm.num(sheet.amount)));
        Assert.Empty(On<obj_item_stack_rods>(r, X, Y));

        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, cult, 5);
        Assert.IsType<turf_open_floor_plating>(r.In(() => At(X, Y + 1)));
        Assert.Empty(On<obj_item_stack_rods>(r, X, Y + 1));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEngineFloorIsUnbreakableUnburnableAndUnplateableUnlessForced()
    {
        var (r, bob) = Room(7404);
        r.In(() =>
        {
            var floor = (turf_open_floor_engine)Floor(typeof(turf_open_floor_engine), X, Y);
            Assert.Null(floor.break_tile());
            Assert.Null(floor.burn_tile());
            floor.crush(); // floor.dm:114 crush calls break_tile
            Assert.False(Dm.truthy(floor.broken));
            Assert.False(Dm.truthy(floor.burnt));
            Assert.Null(Waiting.NoSleep(floor.make_plating()));
            Assert.Same(floor, At(X, Y));

            // :38 try_replace_tile does nothing, even with a tile and a crowbar in hand
            var tile = Dm.@new<obj_item_stack_tile_iron>(get_turf(bob));
            Assert.Null(Waiting.NoSleep(floor.try_replace_tile(tile, bob, Dm.list())));
            Assert.Same(floor, At(X, Y));

            // :33 make_plating(force) goes on to the parent: the plating
            Assert.IsType<turf_open_floor_plating>(Waiting.NoSleep(floor.make_plating(TRUE)));
            Assert.IsType<turf_open_floor_plating>(At(X, Y));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ExplosionsTakeLayersOffByTgsSeverityAndALightBlastDoesNothing()
    {
        var (r, bob) = Room(7405, W, H);
        int heavyPlating = 0, heavyKept = 0, devaSpace = 0, devaPlating = 0;
        r.In(() =>
        {
            for (int x = 3; x <= W - 2; x++)
            {
                Floor(typeof(turf_open_floor_engine), x, 3);
                Floor(typeof(turf_open_floor_engine), x, 5);
                Floor(typeof(turf_open_floor_engine), x, 7);
            }
        });
        r.Step(Second);
        r.In(() =>
        {
            // light: nothing (a floor of iron would break half of them)
            for (int x = 3; x <= W - 2; x++)
            {
                Waiting.NoSleep(EX_ACT(At(x, 3), EXPLODE_LIGHT));
                var t = Assert.IsType<turf_open_floor_engine>(At(x, 3));
                Assert.False(Dm.truthy(t.broken));
            }
            // heavy: prob(50) one layer, an engine floor never breaks
            for (int x = 3; x <= W - 2; x++)
            {
                Assert.Equal(TRUE, At(x, 5).ex_act(EXPLODE_HEAVY));
                turf t = At(x, 5);
                if (t is turf_open_floor_plating) heavyPlating++;
                else
                {
                    Assert.IsType<turf_open_floor_engine>(t);
                    heavyKept++;
                }
            }
            // devastate: 80% two layers to space (lattice replacement of a station tile without lattice scrapes twice), 10% two
            // layers, 10% one: space nearly always, plating now and then
            for (int x = 3; x <= W - 2; x++)
            {
                Waiting.NoSleep(EX_ACT(At(x, 7), EXPLODE_DEVASTATE));
                turf t = At(x, 7);
                if (t is turf_open_space) devaSpace++;
                else
                {
                    Assert.IsType<turf_open_floor_plating>(t);
                    devaPlating++;
                }
            }

            // the epicentre itself (a planted charge): one layer, always
            Floor(typeof(turf_open_floor_engine), 4, 9);
            Assert.Equal(TRUE, At(4, 9).ex_act(EXPLODE_NONE, At(4, 9)));
            Assert.IsType<turf_open_floor_plating>(At(4, 9));

            // a dense object shields the floor below devastation (floor.dm:89, inherited), and the shielded call returns FALSE
            Floor(typeof(turf_open_floor_engine), 6, 9);
            Dm.@new<obj_structure_grille>(At(6, 9));
            Assert.Equal(FALSE, At(6, 9).ex_act(EXPLODE_HEAVY));
            Assert.IsType<turf_open_floor_engine>(At(6, 9));
        });
        Assert.True(heavyPlating > 5 && heavyKept > 5, $"heavy: {heavyPlating} plating, {heavyKept} kept");
        Assert.True(devaSpace > devaPlating * 2, $"devastate: {devaSpace} space, {devaPlating} plating");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThingsUnderTheFloorAreShieldedFromAllButADevastatingBlast()
    {
        var (r, bob) = Room(7406);
        r.In(() =>
        {
            var floor = (turf_open_floor_engine)Floor(typeof(turf_open_floor_engine), X, Y);
            var pipe = Dm.@new<obj_item_crowbar>(floor);
            ADD_TRAIT(pipe, TRAIT_UNDERFLOOR, "test");
            var plain = Dm.@new<obj_item_crowbar>(floor);

            // reinforced_floor.dm:80
            Assert.False(Dm.truthy(floor.can_propagate_explosion(pipe, EXPLODE_HEAVY)));
            Assert.False(Dm.truthy(floor.can_propagate_explosion(pipe, EXPLODE_LIGHT)));
            Assert.True(Dm.truthy(floor.can_propagate_explosion(pipe, EXPLODE_DEVASTATE)));
            Assert.True(Dm.truthy(floor.can_propagate_explosion(plain, EXPLODE_LIGHT)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSingularityShearsTheFloorFromStageFiveOnly()
    {
        var (r, bob) = Room(7407, W, H);
        int sheared = 0, kept = 0;
        r.In(() =>
        {
            for (int x = 3; x <= W - 2; x++)
            {
                Floor(typeof(turf_open_floor_engine), x, 3);
                Floor(typeof(turf_open_floor_engine), x, 5);
            }
        });
        r.Step(Second);
        r.In(() =>
        {
            // stage two: neither the floor's own pull nor the engine floor's
            for (int x = 3; x <= W - 2; x++)
            {
                Waiting.NoSleep(At(x, 3).singularity_pull(bob, STAGE_TWO));
                Assert.IsType<turf_open_floor_engine>(At(x, 3));
            }
            // stage five: the floor's 70% shear, then the engine floor's 30% of what is left (rods again and the plating)
            for (int x = 3; x <= W - 2; x++)
            {
                Waiting.NoSleep(At(x, 5).singularity_pull(bob, STAGE_FIVE));
                if (At(x, 5) is turf_open_floor_engine) kept++; else sheared++;
            }
        });
        Assert.True(sheared > kept, $"stage five: {sheared} sheared, {kept} kept");
        Assert.True(kept > 0, "some tiles survive the pull (the shear is 70%, then the engine floor's own 30%)");
        Assert.True(r.In(() => Enumerable.Range(3, W - 4).Sum(x => Dm.each_cast<atom>(At(x, 5).contents).OfType<obj_item_stack_rods>().Count())) > 0, "the sheared tiles dropped their rods");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryGasVariantKeepsItsInitialGasMix()
    {
        var (r, bob) = Room(7408);
        List<Type> variants = typeof(turf_open_floor_engine).Assembly.GetTypes()
            .Where(t => typeof(turf_open_floor_engine).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        Assert.Contains(typeof(turf_open_floor_engine_n2o), variants);
        Assert.True(variants.Count >= 35, variants.Count.ToString());
        r.In(() =>
        {
            foreach (Type type in variants)
            {
                var floor = (turf_open_floor_engine)Floor(type, X, Y);
                Assert.False(InertSeam.IsInert(floor), type.Name + " is live");
                var expected = (datum_gas_mixture)SSair.parse_gas_string(floor.initial_gas_mix);
                Assert.Equal(Dm.num(expected.total_moles()), Dm.num(floor.air.total_moles()), 3);
                Assert.Equal(Dm.num(expected.temperature), Dm.num(floor.air.temperature), 3);
            }
            // the engine room's tank floors in numbers (atmos_tank defines): the N2O floor is 6000 mol of nitrous oxide
            var n2o = (turf_open_floor_engine_n2o)Floor(typeof(turf_open_floor_engine_n2o), X, Y);
            Assert.Equal(6000f, Dm.num(n2o.air.total_moles()), 1);
            var airless = (turf_open_floor_engine_airless)Floor(typeof(turf_open_floor_engine_airless), X, Y);
            Assert.True(Dm.num(airless.air.total_moles()) < 0.01f);
        });
        r.AssertNoRuntimes();
    }
}
