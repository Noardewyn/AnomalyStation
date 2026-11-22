using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.M5;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G2: tg's floors with their own procs, the mineral rocks and the plastitanium walls in a booted toy room, through the
/// player's clicks: the wood floor unscrews to its tile or pries off broken (fancy_floor.dm), the carpet gives its tile and turns to
/// plating when broken, the grass digs up to sand (the diggable element), the light floor toggles and takes a bulb (light_floor.dm),
/// the circuit floor glows (misc_floor.dm), the reinforced plating's three stages (plating.dm), the foam plating's welder block and
/// tiling, the catwalk's cover (catwalk_plating.dm), the plasma floor's tile (mineral_floor.dm), a pickaxe on a volcanic rock
/// (minerals.dm) and a welder on a plastitanium wall.
/// </summary>
public class FloorFamiliesTests
{
    private const int Second = 20;
    private const int X = 5, Y = 4;

    private static (AudioRoom r, mob_living_carbon_human bob, GapsHarness.Feed feed) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var feed = GapsHarness.Listen(r);
        r.Step(Second);
        return (r, bob, feed);
    }

    // A station tile: the floor over plating over space (the floor's baseturfs, as on the station).
    private static turf Floor(AudioRoom r, Type type, int x = X, int y = Y)
        => r.In(() => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(type, Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating)))));

    private static turf At(AudioRoom r, int x = X, int y = Y) => r.In(() => locate(x, y, 1));

    private static List<T> On<T>(AudioRoom r, int x = X, int y = Y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static void Click(AudioRoom r, mob who, atom target, float seconds = 1) => WallTests.Click(r, who, target, seconds);

    [Fact]
    public void AScrewdriverUnscrewsTheWoodToItsTileAndACrowbarPriesItOffBroken()
    {
        var (r, bob, feed) = Room(8201);
        var wood = Floor(r, typeof(turf_open_floor_wood));
        var large = Floor(r, typeof(turf_open_floor_wood_large), X, Y + 1);
        r.Step(Second);
        Assert.False(r.In(() => InertSeam.IsInert(wood)), "the wood floor is live");

        // fancy_floor.dm:29 screwdriver_act -> pry_tile -> remove_tile(make_tile = TRUE): "You unscrew the planks.", the tile, the plating
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, wood);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Single(On<obj_item_stack_tile_wood>(r));
        Assert.True(feed.Any("You unscrew the planks."));
        Assert.Contains(r.Sounds.Heard, h => h.File?.StartsWith("sound/items/tools/screwdriver", StringComparison.Ordinal) == true);

        // :47 a crowbar's pry is not a screwdriver: the planks are destroyed (no tile); the large wood's own tile type is the same family
        WallTests.Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, large);
        Assert.IsType<turf_open_floor_plating>(At(r, X, Y + 1));
        Assert.Empty(On<obj_item_stack_tile_wood>(r, X, Y + 1));
        Assert.True(feed.Any("You forcefully pry off the planks, destroying them in the process."));

        // a broken plank gives nothing either way: "You remove the broken planks."
        var parquet = Floor(r, typeof(turf_open_floor_wood_parquet), X, Y - 1);
        r.In(() => parquet.break_tile());
        Assert.Contains("wood_parquet-broken", r.In(() => Dm.str(((turf_open)parquet).broken_states() is DmList l ? l[1] : null)));
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, parquet);
        Assert.IsType<turf_open_floor_plating>(At(r, X, Y - 1));
        Assert.Empty(On<obj_item_stack_tile>(r, X, Y - 1));
        Assert.True(feed.Any("You remove the broken planks."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWoodAndCarpetShowTheirExamineLines()
    {
        var (r, bob, _) = Room(8202);
        var wood = Floor(r, typeof(turf_open_floor_wood));
        var carpet = Floor(r, typeof(turf_open_floor_carpet), X, Y + 1);
        r.Step(Second);
        string woodText = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(wood.examine(bob))).Select(o => Dm.str(o))));
        string carpetText = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(carpet.examine(bob))).Select(o => Dm.str(o))));
        Assert.Contains("There's a few <b>screws</b> and a <b>small crack</b> visible.", woodText);
        Assert.Contains("There's a <b>small crack</b> on the edge of it.", carpetText);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrowbarPriesTheCarpetToItsTileAndABrokenOrBurntCarpetTurnsToPlating()
    {
        var (r, bob, feed) = Room(8203);
        var carpet = Floor(r, typeof(turf_open_floor_carpet_green));
        r.Step(Second);
        Assert.StartsWith("carpet_green-", r.In(() => carpet.icon_state)); // smoothed by its bitmask (fancy_floor.dm:302 update_appearance)

        // floor.dm:144 crowbar_act -> pry_tile: the green carpet tile and the plating
        WallTests.Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, carpet);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Single(On<obj_item_stack_tile_carpet_green>(r));
        Assert.True(feed.Any("You remove the floor tile."));

        // fancy_floor.dm:527 break_tile, :531 burn_tile: update_icon makes the plating
        var broken = (turf_open_floor_carpet)Floor(r, typeof(turf_open_floor_carpet), X, Y + 1);
        var burnt = (turf_open_floor_carpet)Floor(r, typeof(turf_open_floor_carpet), X, Y - 1);
        r.In(() => { broken.break_tile(); burnt.burn_tile(); });
        Assert.IsType<turf_open_floor_plating>(At(r, X, Y + 1));
        Assert.IsType<turf_open_floor_plating>(At(r, X, Y - 1));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGrassPicksAPatchAndAShovelDigsItUpToSand()
    {
        var (r, bob, feed) = Room(8204);
        var grass = Floor(r, typeof(turf_open_floor_grass));
        r.Step(Second);
        // fancy_floor.dm:173 spawniconchange: grass0 to grass3
        Assert.Matches("^grass[0-3]$", r.In(() => grass.icon_state));

        // the diggable element (diggable.dm:38): two piles of sand (or one merged stack of two), "You uproot the grass patch.", the plating
        WallTests.Hold<obj_item_shovel>(r, bob);
        Click(r, bob, grass);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Equal(2f, r.In(() => On<obj_item_stack_ore_glass>(r).Sum(s => Dm.num(s.amount))));
        Assert.True(feed.Any("You uproot"));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/shovel_dig.ogg");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AScrewdriverTurnsTheLightFloorOffAndOnAndABulbFixesABrokenOne()
    {
        var (r, bob, feed) = Room(8205);
        var light = (turf_open_floor_light)Floor(r, typeof(turf_open_floor_light));
        r.Step(Second);
        Assert.Equal("light_on-1", r.In(() => light.icon_state)); // the cyan, the first of coloredlights
        Assert.Equal(5f, r.In(() => Dm.num(light.light_range)) + 2f); // :80 set_light(5), then light_range = 3

        // :114 screwdriver_act: on = !on, the light goes out and the state reads "light_off"
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, light);
        Assert.Equal("light_off", r.In(() => light.icon_state));
        Assert.Equal(0f, r.In(() => Dm.num(light.light_range)));
        Click(r, bob, light);
        Assert.Equal("light_on-1", r.In(() => light.icon_state));

        // a colour from the multitool's menu (:121): the green is the fourth picture
        r.In(() => { light.currentcolor = LIGHT_COLOR_GREEN; light.update_appearance(); });
        Assert.Equal("light_on-4", r.In(() => light.icon_state));
        Assert.Equal(LIGHT_COLOR_GREEN, r.In(() => Dm.str(light.light_color)));

        // :60 break_tile picks a broken state; :133 a working bulb fixes it: "You replace the light bulb."
        r.In(() => light.break_tile());
        Assert.InRange(r.In(() => Dm.num(light.state)), 1f, 3f);
        WallTests.Hold<obj_item_light_bulb>(r, bob);
        Click(r, bob, light);
        Assert.Equal(0f, r.In(() => Dm.num(light.state)));
        Assert.True(feed.Any("You replace the light bulb."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACircuitFloorGlowsWithItsAreaAndTheOffOneStaysDark()
    {
        var (r, _, _) = Room(8206);
        var circuit = (turf_open_floor_circuit)Floor(r, typeof(turf_open_floor_circuit_green));
        var off = (turf_open_floor_circuit)Floor(r, typeof(turf_open_floor_circuit_green_off), X, Y + 1);
        r.Step(Second);
        bool powered = r.In(() => Dm.truthy(((area)get_area(circuit)).powered(AREA_USAGE_LIGHT)));
        Assert.True(powered, "the toy room's area has light power");
        // misc_floor.dm:56 handle_powerchange: on; :34 update_appearance: set_light(2, 1.5) in its colour; :43 its own icon state
        Assert.Equal(TRUE, r.In(() => Dm.num(circuit.on)));
        Assert.Equal("gcircuit", r.In(() => circuit.icon_state));
        Assert.Equal(2f, r.In(() => Dm.num(circuit.light_range)));
        Assert.Equal(1.5f, r.In(() => Dm.num(circuit.light_power)));
        Assert.Equal(LIGHT_COLOR_VIVID_GREEN, r.In(() => Dm.str(circuit.light_color)));
        Assert.Contains(circuit, r.In(() => Dm.each(SSmapping.nuke_tiles).ToList()));

        // the always-off variant: "gcircuitoff", no light
        Assert.Equal(FALSE, r.In(() => Dm.num(off.on)));
        Assert.Equal("gcircuitoff", r.In(() => off.icon_state));
        Assert.Equal(0f, r.In(() => Dm.num(off.light_range)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReinforcedPlatingComesOffInThreeStagesToTwoPlasteelSheets()
    {
        var (r, bob, feed) = Room(8207);
        var plate = (turf_open_floor_plating_reinforced)Floor(r, typeof(turf_open_floor_plating_reinforced));
        r.Step(Second);
        Assert.Equal("r_plate-0", r.In(() => plate.icon_state));
        Assert.Contains("securely <b>bolted</b>", r.In(() => Dm.str(plate.deconstruction_hints(bob))));

        // plating.dm:271 PLATE_INTACT: a wrench, ten seconds: the bolts (r_plate-1)
        WallTests.Hold<obj_item_wrench>(r, bob);
        Click(r, bob, plate, 11);
        Assert.Equal(1f, r.In(() => Dm.num(plate.deconstruction_state)));
        Assert.Equal("r_plate-1", r.In(() => plate.icon_state));
        Assert.True(feed.Any("balloon: removed bolts"));

        // a screwdriver secures it back in fifteen seconds; the wrench again
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, plate, 16);
        Assert.Equal(0f, r.In(() => Dm.num(plate.deconstruction_state)));
        WallTests.Hold<obj_item_wrench>(r, bob);
        Click(r, bob, plate, 11);
        Assert.Equal(1f, r.In(() => Dm.num(plate.deconstruction_state)));

        // a lit welder slices it in fifteen seconds (r_plate-2)
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, plate, 16);
        Assert.Equal(2f, r.In(() => Dm.num(plate.deconstruction_state)));
        Assert.Equal("r_plate-2", r.In(() => plate.icon_state));

        // a crowbar, twenty seconds: two plasteel sheets and the plating under it
        WallTests.Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, plate, 21);
        var plating = Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.IsNotType<turf_open_floor_plating_reinforced>(plating);
        Assert.Equal(2f, r.In(() => On<obj_item_stack_sheet_plasteel>(r).Sum(s => Dm.num(s.amount))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFoamPlatingBlocksTheWelderAndAnIronTileMakesItPlating()
    {
        var (r, bob, feed) = Room(8208);
        var foam = r.In(() => (turf)Waiting.NoSleep(locate(X, Y, 1).ChangeTurf(typeof(turf_open_floor_plating_foam))));
        var tiled = r.In(() => (turf)Waiting.NoSleep(locate(X, Y + 1, 1).ChangeTurf(typeof(turf_open_floor_plating_foam))));
        r.Step(Second);

        // plating.dm:165 the tool blocker: the welder skips to the attack (:190), which taps the foam (and a lit welder's force
        // smashes through it by tg's prob(force * 20 - 25))
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, foam);
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/tap.ogg");

        // :175 an iron tile reinforces it: "You reinforce the foamed plating with tiling.", the plating
        WallTests.Hold<obj_item_stack_tile_iron>(r, bob, 4);
        Click(r, bob, tiled);
        Assert.IsType<turf_open_floor_plating>(At(r, X, Y + 1));
        Assert.IsNotType<turf_open_floor_plating_foam>(At(r, X, Y + 1));
        Assert.True(feed.Any("You reinforce the foamed plating with tiling."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCatwalkMustBeUncoveredBeforeItsTileComesUp()
    {
        var (r, bob, feed) = Room(8209);
        var catwalk = (turf_open_floor_catwalk_floor)Floor(r, typeof(turf_open_floor_catwalk_floor_iron));
        r.Step(Second);

        // catwalk_plating.dm:61 covered: "remove cover first!"
        WallTests.Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, catwalk);
        Assert.Same(catwalk, At(r));
        Assert.True(feed.Any("balloon: remove cover first!"));

        // :37 the screwdriver takes the cover off: the state below, the low layer, the uncovered trait
        WallTests.Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, catwalk);
        Assert.Equal(FALSE, r.In(() => Dm.num(catwalk.covered)));
        Assert.Equal("iron_below", r.In(() => catwalk.icon_state));
        Assert.True(r.In(() => HAS_TRAIT(catwalk, TRAIT_UNCOVERED_TURF)));
        Assert.True(feed.Any("balloon: cover removed"));

        // then the crowbar pries the catwalk tile off
        WallTests.Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, catwalk);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Single(On<obj_item_stack_tile_catwalk_tile>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlasmaAndTitaniumFloorsKeepTheirPicturesAndGiveTheirTiles()
    {
        var (r, bob, _) = Room(8210);
        var plasma = (turf_open_floor_mineral)Floor(r, typeof(turf_open_floor_mineral_plasma));
        var titanium = (turf_open_floor_mineral)Floor(r, typeof(turf_open_floor_mineral_titanium), X, Y + 1);
        r.Step(Second);
        Assert.Equal("plasma", r.In(() => plasma.icon_state));
        Assert.Contains("plasma_dam", r.In(() => Dm.each(plasma.icons).Select(o => Dm.str(o)).ToList())); // typelist("icons", icons)
        Assert.Equal("damaged1", r.In(() => Dm.str(((DmList)titanium.broken_states())[1])));

        WallTests.Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, plasma);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Single(On<obj_item_stack_tile_mineral_plasma>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APickaxeDigsTheVolcanicRockToBasaltInFourSeconds()
    {
        var (r, bob, _) = Room(8211);
        // the rock over its own floor, as mapped (its baseturfs: the basalt)
        var rock = r.In(() => (turf)Waiting.NoSleep(locate(X, Y, 1).ChangeTurf(typeof(turf_closed_mineral_volcanic), Dm.list(typeof(turf_open_misc_asteroid_basalt)))));
        r.Step(Second);
        Assert.False(r.In(() => InertSeam.IsInert(rock)), "the volcanic rock is live");
        // minerals.dm:53 the wall-plane copy of tg's rock picture
        Assert.True(r.In(() => rock.overlays.Count) > 0);

        // :232 manual_mine: use_tool(tool_mine_speed = 4 SECONDS, volume 50), then gets_drilled: the basalt floor, tg's stone break
        WallTests.Hold<obj_item_pickaxe>(r, bob);
        r.In(() => ClientSeam.MouseDown(bob.client, rock, "left=1;button=left")); // DV348: the dig runs while the button is held
        Click(r, bob, rock, 2);
        Assert.Same(rock, At(r));
        r.Step(3 * Second);
        var basalt = Assert.IsType<turf_open_misc_asteroid_basalt>(At(r));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/break_stone.ogg");
        Assert.Matches("^basalt([0-9]|1[0-2])?$", r.In(() => basalt.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWelderSlicesAPlastitaniumWallIntoAGirderAndPlastitaniumSheets()
    {
        var (r, bob, _) = Room(8212);
        var wall = WallTests.Wall(r, typeof(turf_closed_wall_mineral_plastitanium), X, Y);
        r.Step(Second);
        Assert.False(r.In(() => InertSeam.IsInert(wall)), "the plastitanium wall is live");
        Assert.StartsWith("plastitanium_wall-", r.In(() => wall.icon_state));

        // walls.dm's deconstruction: a lit welder, the wall's slicing time, then the girder and its sheet_type
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, wall, 12);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Single(On<obj_structure_girder>(r));
        Assert.Equal(2f, r.In(() => On<obj_item_stack_sheet_mineral_plastitanium>(r).Sum(s => Dm.num(s.amount))));
        r.AssertNoRuntimes();
    }
}
