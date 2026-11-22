using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 4: tg's floor construction (game/turfs/open/floor.dm, floor/plating.dm, _open.dm:625-675, space/space.dm:130,
/// game/objects/structures/lattice.dm) in a booted toy room: a crowbar pries a tile up (pry_tile, remove_tile, spawn_tile,
/// make_plating's ScrapeAway with CHANGETURF_INHERIT_AIR: the plating keeps the floor's air), a floor tile on plating makes
/// the floor again (place_tile), rods on space make a lattice and a second rod a catwalk, a floor tile on a lattice makes
/// plating (build_with_floor_tiles: the lattice goes, the plating keeps lattice_underneath), wirecutters cut a lattice back
/// to its rod, and a tile with no support is refused.
/// </summary>
public class FloorTests
{
    private const int Second = 20;
    private const int X = 5, Y = 4;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static turf Here(AudioRoom r) => r.In(() => locate(X, Y, 1));

    private static List<T> On<T>(AudioRoom r) => r.In(() => Dm.each_cast<atom>(locate(X, Y, 1).contents).OfType<T>().ToList());

    [Fact]
    public void ACrowbarPriesATileUpAndATilePutsItBack()
    {
        var (r, bob) = Room(5411);
        var floor = (turf_open)WallTests.Wall(r, typeof(turf_open_floor_iron), X, Y);
        Assert.True(r.In(() => Dm.truthy(floor.overfloor_placed)));
        r.Step(Second);
        WallTests.Hold<obj_item_crowbar>(r, bob);

        // floor.dm:144 crowbar_act -> pry_tile (the crowbar's sound at 80) -> remove_tile -> spawn_tile, make_plating
        r.Command(() =>
        {
            floor.air.temperature = 350;
            ClientSeam.Click(bob.client, floor, "left=1;button=left");
        });
        r.Step(1);
        var plating = Assert.IsType<turf_open_floor_plating>(Here(r));
        var tile = Assert.Single(On<obj_item_stack_tile_iron>(r));
        Assert.Equal(1f, r.In(() => Dm.num(tile.amount)));
        Assert.True(r.In(() => plating.air.temperature) > 330f, "CHANGETURF_INHERIT_AIR: the plating keeps the floor's air");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/tools/crowbar.ogg");

        // plating.dm:73 a floor tile on plating: place_tile (tile_types.dm:76) puts the floor on top
        r.Step(Second);
        var tiles = WallTests.Hold<obj_item_stack_tile_iron>(r, bob, 4);
        WallTests.Click(r, bob, plating, 1);
        Assert.IsType<turf_open_floor_iron>(Here(r));
        Assert.Equal(3f, r.In(() => Dm.num(tiles.amount)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/genhit.ogg");

        // a second pry gives the plating back, and the welder fixes nothing on undamaged plating (plating.dm:141)
        WallTests.Hold<obj_item_crowbar>(r, bob);
        WallTests.Click(r, bob, Here(r), 1);
        Assert.IsType<turf_open_floor_plating>(Here(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void RodsOnSpaceMakeALatticeAndATileMakesPlating()
    {
        var (r, bob) = Room(5412);
        // the space turf and the build happen in one tick, before the room's air reaches it
        var rods = WallTests.Hold<obj_item_stack_rods>(r, bob, 10);
        var tiles = r.In(() => (obj_item_stack_tile_iron)Dm.@new(typeof(obj_item_stack_tile_iron), get_turf(bob), 4f, FALSE));
        r.In(() =>
        {
            var space = (turf)Waiting.NoSleep(locate(X, Y, 1).ChangeTurf(typeof(turf_open_space)));
            Assert.IsType<turf_open_space>(space);

            // a tile on space with no lattice and no floor next to it: "needs support" (_open.dm:655; the toy room's floors are
            // /turf/open/indestructible, not /turf/open/floor)
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(space.item_interaction(bob, tiles))));
            Assert.Equal(4f, Dm.num(tiles.amount));
            Assert.IsType<turf_open_space>(locate(X, Y, 1));

            // space.dm:130 rods -> build_with_rods (_open.dm:625): one rod, a lattice
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(space.item_interaction(bob, rods))));
            Assert.Equal(9f, Dm.num(rods.amount));
            var lattice = Assert.IsType<obj_structure_lattice>(Dm.locate<obj_structure_lattice>(space));
            // give_turf_traits (lattice.dm:24): the turf now stops chasms and ignores slowdown
            Assert.True(HAS_TRAIT(space, TRAIT_TURF_IGNORE_SLOWDOWN));

            // a floor tile on the lattice (lattice.dm:69 hands it to the turf): plating on top, the lattice goes
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(lattice.item_interaction(bob, tiles))));
            var plating = Assert.IsType<turf_open_floor_plating>(locate(X, Y, 1));
            Assert.Equal(3f, Dm.num(tiles.amount));
            Assert.True(QDELETED(lattice));
            Assert.Equal(TRUE, plating.lattice_underneath);
            Assert.False(HAS_TRAIT(plating, TRAIT_TURF_IGNORE_SLOWDOWN), "the lattice's traits went with it");
            var below = (DmList)plating.baseturfs; // place_on_top: the space's baseturfs, then the space itself
            Assert.Equal(typeof(turf_open_space), below[below.len]);
        });
        r.Step(Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASecondRodMakesACatwalkAndWirecuttersCutTheLatticeBackToItsRod()
    {
        var (r, bob) = Room(5413);
        var rods = WallTests.Hold<obj_item_stack_rods>(r, bob, 10);
        r.In(() =>
        {
            var space = (turf)Waiting.NoSleep(locate(X, Y, 1).ChangeTurf(typeof(turf_open_space)));
            Waiting.NoSleep(space.item_interaction(bob, rods));
            var lattice = Dm.locate<obj_structure_lattice>(space);
            // _open.dm:637: rods on a lattice: replace_with_catwalk (lattice.dm:107)
            Waiting.NoSleep(lattice.item_interaction(bob, rods));
            Assert.True(QDELETED(lattice));
            var catwalk = Assert.IsType<obj_structure_lattice_catwalk>(Dm.locate<obj_structure_lattice>(space));
            Assert.Equal(8f, Dm.num(rods.amount));
            // a third: "There is already a catwalk here!"
            Waiting.NoSleep(catwalk.item_interaction(bob, rods));
            Assert.Equal(8f, Dm.num(rods.amount));

            // lattice.dm:73 wirecutters: deconstruct -> atom_deconstruct: number_of_mats rods (a catwalk's two)
            var cutters = Dm.@new<obj_item_wirecutters>(get_turf(bob));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Dm.num(Waiting.NoSleep(catwalk.wirecutter_act(bob, cutters))));
            Assert.True(QDELETED(catwalk));
            var dropped = Dm.each_cast<atom>(space.contents).OfType<obj_item_stack_rods>().ToList();
            Assert.Equal(2f, dropped.Sum(s => Dm.num(s.amount)));
            Assert.False(HAS_TRAIT(space, TRAIT_TURF_IGNORE_SLOWDOWN));
        });
        r.Step(Second);
        r.AssertNoRuntimes();
    }
}
