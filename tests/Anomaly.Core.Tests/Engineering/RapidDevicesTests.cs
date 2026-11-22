using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Engineering;

/// <summary>
/// Engineering package E13: the rapid lighting, tiling and decoration devices (RLD.dm, RTD.dm, RDD.dm) in a booted toy room. The RLD mounts
/// a tube on the side of a wall nearest the user and a floor light on a floor, removes a light and throws a lit glowstick; the RTD lays its
/// design on a plating, picks the design of a tiled floor and tears a supported floor down to plating; the RDD places plastic decorations,
/// three to a tile at most, and removes one, each with tg's costs, delays and sounds, and its window's data and acts.
/// </summary>
public class RapidDevicesTests
{
    private const int Second = 20;
    private const int X = 5, Y = 4;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const string Right = "icon-x=16;icon-y=16;right=1;button=right";

    private static (AudioRoom R, mob_living_carbon_human Bob, T Device) Build<T>(ulong seed) where T : obj_item_construction
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob, M5.WallTests.Hold<T>(r, bob));
    }

    private static turf Make(AudioRoom r, Type type, int x = X, int y = Y) => r.In(() => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(type)));

    private static turf At(AudioRoom r, int x = X, int y = Y) => r.In(() => locate(x, y, 1));

    private static void Click(AudioRoom r, mob who, atom target, float seconds, string how = Left)
    {
        r.Command(() => ClientSeam.Click(who.client, target, how));
        r.Step((int)(seconds * Second));
    }

    private static object Act(AudioRoom r, mob who, obj_item_construction device, string iface, string action, params (string Key, object Value)[] args) => r.In(() =>
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        return Waiting.NoSleep(device.ui_act(action, p, Dm.@new<datum_tgui>(who, device, iface, "device"), null));
    });

    private static List<T> On<T>(AudioRoom r, int x = X, int y = Y) where T : atom => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    [Fact]
    public void TheRldMountsATubeOnTheWallSideNearestTheUserAndAFloorLightThenRemovesOne()
    {
        var (r, bob, rld) = Build<obj_item_construction_rld>(13101);
        Assert.Equal((200f, 2f), r.In(() => (rld.matter, Dm.num(rld.mode)))); // tg's charged RLD, Permanent Light Construction
        r.In(() => Assert.Equal(new[] { "Color Pick", "Glow Stick", "Deconstruct", "Light Fixture" }, Dm.each(rld.display_options).Select(Dm.str)));
        turf wall = M5.WallTests.Wall(r, typeof(turf_closed_wall), X, Y);

        // a wall: the tube goes on Bob's own tile (the free side nearest him), facing the wall, 10 units after 1 s
        Click(r, bob, wall, 1.5f);
        var tube = Assert.Single(On<obj_machinery_light>(r, 4, Y));
        r.In(() =>
        {
            Assert.Equal(EAST, Dm.num(tube.dir));
            Assert.Equal("#ffffff", Dm.str(tube.color));
            Assert.Equal(190f, rld.matter);
        });
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/light_flicker.ogg");

        // a floor: a floor light, 15 units
        turf floor = Make(r, typeof(turf_open_floor_plating), X, Y + 1);
        Click(r, bob, floor, 1.5f);
        Assert.Single(On<obj_machinery_light_floor>(r, X, Y + 1));
        Assert.Equal(175f, r.In(() => rld.matter));

        // the Deconstruct mode removes the tube: 10 units after 1.5 s
        r.In(() => rld.mode = 3);
        Click(r, bob, tube, 2f);
        Assert.True(r.In(() => QDELETED(tube)));
        Assert.Equal(165f, r.In(() => rld.matter));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRldsGlowstickModeThrowsALitGlowstickForFiveUnits()
    {
        var (r, bob, rld) = Build<obj_item_construction_rld>(13102);
        r.In(() => rld.mode = 1);
        turf far = At(r, 8, Y);
        Click(r, bob, far, 2f);
        var stick = r.In(() => Dm.each<obj_item_flashlight_glowstick>(Dm.world).Single());
        r.In(() =>
        {
            Assert.True(Dm.truthy(stick.light_on));
            Assert.Equal("#ffffff", Dm.str(stick.color));
            Assert.True(Dm.num(stick.x) > 4); // thrown towards the target
            Assert.Equal(195f, rld.matter);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRtdLaysItsDesignPicksAFloorsDesignAndTearsAFloorDown()
    {
        var (r, bob, rtd) = Build<obj_item_construction_rtd_loaded>(13103);
        r.In(() =>
        {
            Assert.Equal(("Conventional", "Standard", "Base", 4f), (rtd.root_category, rtd.design_category, Dm.str(rtd.selected_design.name), Dm.num(rtd.selected_design.cost)));
            var data = (DmList)Waiting.NoSleep(rtd.ui_static_data(bob));
            Assert.Equal(new[] { "Conventional", "Decorated", "Interior" }, Dm.each(data["root_categories"]).Select(Dm.str));
            var cats = Dm.each<DmList>(data["categories"]).ToList();
            Assert.Equal(new[] { "Standard", "Translucent", "Circuit" }, cats.Select(c => Dm.str(c["category_name"])));
            var live = (DmList)Waiting.NoSleep(rtd.ui_data(bob));
            Assert.Equal(("Base", "iconsturffloorsdmifloor", "south"), (Dm.str(live["selected_recipe"]), Dm.str(live["selected_icon"]), Dm.str(live["selected_direction"])));
        });

        // a plating gets the iron tile: 4 units after 0.6 s
        turf plating = Make(r, typeof(turf_open_floor_plating));
        Click(r, bob, plating, 1f);
        Assert.IsType<turf_open_floor_iron>(At(r));
        Assert.Equal(346f, r.In(() => rtd.matter));

        // a dark floor: its design is picked (Decorated, Dark Colored, Base)
        turf dark = Make(r, typeof(turf_open_floor_iron_dark), X, Y + 1);
        Click(r, bob, dark, 0.5f);
        r.In(() => Assert.Equal(("Decorated", "Dark Colored", "Base"), (rtd.root_category, rtd.design_category, Dm.str(rtd.selected_design.name))));

        // a right click tears the iron floor down: 4 * 0.7 units after 1 s
        Click(r, bob, At(r), 1.5f, Right);
        Assert.IsType<turf_open_floor_plating>(At(r));
        Assert.Equal(346f - 2.8f, r.In(() => rtd.matter), 0.001);

        // the window's recipe act: the Standard category's third design, facing south
        Act(r, bob, rtd, "RapidTilingDevice", "root_category", ("root_category", "Conventional"));
        Act(r, bob, rtd, "RapidTilingDevice", "recipe", ("category_name", "Standard"), ("id", "3"));
        r.In(() => Assert.Equal(("Large", 7f, (float)SOUTH), (Dm.str(rtd.selected_design.name), Dm.num(rtd.selected_design.cost), rtd.selected_direction)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRddPlacesUpToThreeDecorationsOnATileAndRemovesOne()
    {
        var (r, bob, rdd) = Build<obj_item_construction_rdd_loaded>(13104);
        r.In(() =>
        {
            Assert.Equal((60f, "Grasses", "Plastic Grass Patch"), (rdd.matter, Dm.str(rdd.selected_category), Dm.str(rdd.selected_design_name)));
            Assert.Equal(typeof(obj_structure_decoration_grass_first), rdd.selected_decoration);
            var data = (DmList)Waiting.NoSleep(rdd.ui_static_data(bob));
            var cats = Dm.each<DmList>(data["categories"]).ToList();
            Assert.Equal(new[] { "Grasses", "Bushes", "Flowers", "Snow", "Jungle", "Rocks", "Misc" }, cats.Select(c => Dm.str(c["cat_name"])));
            Assert.Equal("PlasticGrassPatch", Dm.str(Dm.each<DmList>(cats[0]["designs"]).First()["icon"]));
            var live = (DmList)Waiting.NoSleep(rdd.ui_data(bob));
            Assert.Equal((60f, 60f), (Dm.num(live["matter"]), Dm.num(live["max_matter"])));
        });

        // the Rocks' random rock
        Act(r, bob, rdd, "RapidDecorationDevice", "design", ("category", "Rocks"), ("name", "Plastic Rock (Random)"));
        Assert.Equal(typeof(obj_structure_decoration_rock_style_random), r.In(() => rdd.selected_decoration));

        turf floor = Make(r, typeof(turf_open_floor_plating));
        for (int i = 0; i < 4; i++)
            Click(r, bob, floor, 1f);
        var placed = On<obj_structure_decoration>(r);
        Assert.Equal(3, placed.Count); // a fourth is refused
        r.In(() => Assert.All(placed, d => Assert.Matches("^basalt[1-4]$", d.icon_state)));
        Assert.Equal(57f, r.In(() => rdd.matter));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/deconstruct.ogg");

        Click(r, bob, floor, 1f, Right);
        Assert.Equal(2, On<obj_structure_decoration>(r).Count);
        r.AssertNoRuntimes();
    }
}
