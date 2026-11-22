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
/// Engineering package E13: the rapid construction device (RHD.dm, RCD.dm) and the rcd_vals and rcd_act of its targets in a booted toy
/// room. Its designs and window data, matter from cartridges and sheets, the upgrade disks; a left click builds the selected design after
/// tg's delay with the constructing effect (plating on space, a wall on plating, a grille and a window on it, an airlock with the
/// integrated electronics' access, a girder finished into a wall, a chair), a right click deconstructs (a wall back to plating, a window,
/// an airlock), the remembered wall makes the rebuild cheap and fast, a reinforced wall needs an RCD that can cut it.
/// </summary>
public class RcdTests
{
    private const int Second = 20;
    private const int X = 5, Y = 4;
    private const string Left = "icon-x=16;icon-y=16;left=1;button=left";
    private const string Right = "icon-x=16;icon-y=16;right=1;button=right";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Bob, obj_item_construction_rcd Rcd);

    private static Rig Build<T>(ulong seed) where T : obj_item_construction_rcd
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, Y, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var rcd = M5.WallTests.Hold<T>(r, bob);
        return new Rig(r, bob, rcd);
    }

    private static turf At(AudioRoom r, int x = X, int y = Y) => r.In(() => locate(x, y, 1));

    private static turf Make(AudioRoom r, Type type, int x = X, int y = Y) => r.In(() => (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(type)));

    private static void Click(Rig g, atom target, float seconds = 1, string how = Left)
    {
        g.R.Command(() => ClientSeam.Click(g.Bob.client, target, how));
        g.R.Step((int)(seconds * Second));
    }

    private static object Act(Rig g, string action, params (string Key, object Value)[] args) => g.R.In(() =>
    {
        DmList p = Dm.list();
        foreach ((string key, object value) in args)
            p[key] = value;
        datum_tgui ui = Dm.@new<datum_tgui>(g.Bob, g.Rcd, "RapidConstructionDevice", "RCD");
        return Waiting.NoSleep(g.Rcd.ui_act(action, p, ui, null));
    });

    private static void Pick(Rig g, string root, string category, int index)
    {
        Act(g, "root_category", ("root_category", root));
        Act(g, "design", ("category", category), ("index", (float)index));
    }

    private static float Matter(Rig g) => g.R.In(() => g.Rcd.matter);

    private static List<T> On<T>(AudioRoom r, int x = X, int y = Y) where T : atom => r.In(() => Dm.each<T>(locate(x, y, 1)).ToList());

    [Fact]
    public void TheRcdStartsOnTheFloorWallDesignAndItsWindowListsTgsCategories()
    {
        var g = Build<obj_item_construction_rcd>(13001);
        g.R.In(() =>
        {
            Assert.Equal(new[] { "Construction", "Airlocks", "Airlock Access" }, Dm.each(GLOB.rcd_designs).Select(Dm.str));
            Assert.Equal(("Construction", "Structures", "Floor/Wall"), (Dm.str(g.Rcd.root_category), Dm.str(g.Rcd.design_category), Dm.str(g.Rcd.design_title)));
            Assert.Equal(typeof(turf_open_floor_plating_rcd), g.Rcd.rcd_design_path);
            Assert.Equal((float)RCD_TURF, Dm.num(g.Rcd.mode));
            Assert.Same(g.Rcd, g.Rcd.airlock_electronics.holder);
            Assert.Contains(g.Rcd, Dm.each(GLOB.rcd_list));
            Assert.Equal(0f, g.Rcd.matter); // the plain RCD comes empty

            var data = (DmList)Waiting.NoSleep(g.Rcd.ui_static_data(g.Bob));
            Assert.Equal(0f, Dm.num(data["silo_upgraded"]));
            Assert.Equal("Construction", Dm.str(data["selected_root"]));
            // without the frames and furnishing disks the Machines and Furniture categories are left out
            var cats = Dm.each<DmList>(data["categories"]).ToList();
            Assert.Equal(new[] { "Structures" }, cats.Select(c => Dm.str(c["cat_name"])));
            var designs = Dm.each<DmList>(cats[0]["designs"]).ToList();
            Assert.Equal(8, designs.Count);
            Assert.Equal(("Floor/Wall", "FloorWall"), (Dm.str(designs[0]["title"]), Dm.str(designs[0]["icon"])));
            Assert.NotNull(data["regions"]); // the airlock electronics' static data is merged in

            var live = (DmList)Waiting.NoSleep(g.Rcd.ui_data(g.Bob));
            Assert.Equal((0f, "Structures", "Floor/Wall"), (Dm.num(live["matterLeft"]), Dm.str(live["selected_category"]), Dm.str(live["selected_design"])));
            Assert.NotNull(live["accesses"]);
        });

        // the Airlocks root and its Solid Airlocks' first design
        Pick(g, "Airlocks", "Solid Airlocks", 1);
        g.R.In(() =>
        {
            Assert.Equal(typeof(obj_machinery_door_airlock), g.Rcd.rcd_design_path);
            Assert.Equal(((float)RCD_AIRLOCK, "Solid Airlocks", "Airlock"), (Dm.num(g.Rcd.mode), Dm.str(g.Rcd.design_category), Dm.str(g.Rcd.design_title)));
            Assert.Equal(1f, Dm.num(g.Rcd.blueprint_changed));
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void CartridgesAndSheetsLoadMatterAndAFullRcdTakesNoMore()
    {
        var g = Build<obj_item_construction_rcd>(13002);
        g.R.In(() =>
        {
            var ammo = Dm.@new<obj_item_rcd_ammo>(get_turf(g.Bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Rcd.insert_matter(ammo, g.Bob))));
            Assert.Equal(40f, g.Rcd.matter);
            Assert.True(QDELETED(ammo)); // used up
            Assert.Contains(Dm.each(g.Rcd.overlays), o => o is IDmAppearance a && a.icon_state == "rcd_charge3"); // ceil(40 / 160 * 10)

            // iron sheets: tg's matter_amount each, as many as fit
            var iron = (obj_item_stack_sheet_iron)Dm.@new(typeof(obj_item_stack_sheet_iron), get_turf(g.Bob), 50f, FALSE);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Rcd.insert_matter(iron, g.Bob))));
            float per = iron.matter_amount;
            Assert.Equal(40f + per * Math.Min(50f, (float)Math.Round((160f - 40f) / per)), g.Rcd.matter);

            g.Rcd.matter = g.Rcd.max_matter;
            var large = Dm.@new<obj_item_rcd_ammo_large>(get_turf(g.Bob));
            Assert.False(Dm.truthy(Waiting.NoSleep(g.Rcd.insert_matter(large, g.Bob)))); // storage full
            Assert.Equal(160f, large.ammoamt);
        });
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnUpgradeDiskInstallsOnceAndUnlocksItsCategory()
    {
        var g = Build<obj_item_construction_rcd_loaded>(13003);
        g.R.In(() =>
        {
            var disk = Dm.@new<obj_item_rcd_upgrade_furnishing>(get_turf(g.Bob));
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Rcd.install_upgrade(disk, g.Bob))));
            Assert.Equal((float)RCD_UPGRADE_FURNISHING, g.Rcd.construction_upgrades);
            Assert.True(QDELETED(disk));
            var again = Dm.@new<obj_item_rcd_upgrade_furnishing>(get_turf(g.Bob));
            Assert.False(Dm.truthy(Waiting.NoSleep(g.Rcd.install_upgrade(again, g.Bob)))); // already installed
            var data = (DmList)Waiting.NoSleep(g.Rcd.ui_static_data(g.Bob));
            Assert.Equal(new[] { "Structures", "Furniture" }, Dm.each<DmList>(data["categories"]).Select(c => Dm.str(c["cat_name"])));
        });

        // a chair, facing the way Bob faces
        Pick(g, "Construction", "Furniture", 1);
        turf floor = Make(g.R, typeof(turf_open_floor_plating));
        g.R.In(() => Waiting.NoSleep(g.Bob.setDir(EAST)));
        Click(g, floor, 1.5f);
        var chair = Assert.Single(On<obj_structure_chair>(g.R));
        Assert.Equal(EAST, g.R.In(() => Dm.num(chair.dir)));
        Assert.Equal(160f - 4f, Matter(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void PlatingOnSpaceThenAWallAndBackWithTheConstructingEffect()
    {
        var g = Build<obj_item_construction_rcd_loaded>(13004);
        turf space = Make(g.R, typeof(turf_open_space));

        // plating on space: no delay, three units without a lattice
        Click(g, space, 0.25f);
        turf plating = At(g.R);
        Assert.IsType<turf_open_floor_plating>(plating);
        Assert.Equal(157f, Matter(g));

        // a wall on the plating: tg's 2 s and 16 units, the constructing effect while it builds
        g.R.Command(() => ClientSeam.Click(g.Bob.client, plating, Left));
        g.R.Step(Second);
        // the plating's effect still plays its end (1.5 s); the wall's is the one building
        var effect = Assert.Single(On<obj_effect_constructing_effect>(g.R), e => g.R.In(() => e.icon_state) != "rcd_end");
        g.R.In(() => Assert.Equal(("rcd_short", "construction effect"), (effect.icon_state, Dm.str(effect.name)))); // 20 ds: the short state
        Assert.IsType<turf_open_floor_plating>(At(g.R));
        g.R.Step((int)(1.25 * Second));
        Assert.IsType<turf_closed_wall>(At(g.R));
        Assert.Equal(157f - 16f, Matter(g));
        g.R.In(() => Assert.Equal("rcd_end", effect.icon_state)); // the end animation, gone 1.5 s later
        g.R.Step(2 * Second);
        Assert.True(g.R.In(() => QDELETED(effect)));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/items/deconstruct.ogg");
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/machines/click.ogg");

        // ChangeTurf carries the tile's memory (change_turf.dm:84, :137): a built wall remembers nothing, a mapped one (walls.dm:21) does
        g.R.In(() =>
        {
            Assert.Null(At(g.R).rcd_memory);
            At(g.R).rcd_memory = RCD_MEMORY_WALL;
        });

        // a right click deconstructs the wall: 4 s, 26 units, the plating again
        Click(g, At(g.R), 4.5f, Right);
        turf back = At(g.R);
        Assert.IsType<turf_open_floor_plating>(back);
        Assert.Equal(141f - 26f, Matter(g));
        Assert.Equal((float)RCD_MEMORY_WALL, g.R.In(() => Dm.num(back.rcd_memory))); // the wall is remembered

        // rebuilding the remembered wall costs an eighth and takes a fifth of the time (rcd_result_with_memory)
        g.R.In(() =>
        {
            g.Rcd.mode = g.Rcd.construction_mode; // what the next left click sets before rcd_vals
            var vals = (DmList)back.rcd_vals(g.Bob, g.Rcd);
            Assert.Equal((2f, 4f, 1f), (Dm.num(vals["cost"]), Dm.num(vals["delay"]), Dm.num(vals[RCD_RESULT_BYPASS_FREQUENT_USE_COOLDOWN])));
        });
        Click(g, back, 0.5f);
        Assert.IsType<turf_closed_wall>(At(g.R));
        Assert.Equal(115f - 2f, Matter(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnRcdWithoutMatterBuildsNothing()
    {
        var g = Build<obj_item_construction_rcd>(13005);
        turf plating = Make(g.R, typeof(turf_open_floor_plating));
        Click(g, plating, 2.5f);
        Assert.IsType<turf_open_floor_plating>(At(g.R));
        Assert.Empty(On<obj_effect_constructing_effect>(g.R));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AGrilleAndAFullTileWindowOnItThenTheWindowIsTakenDown()
    {
        var g = Build<obj_item_construction_rcd_loaded>(13006);
        Pick(g, "Construction", "Structures", 4); // full tile window
        Assert.Equal(typeof(obj_structure_window_fulltile), g.R.In(() => g.Rcd.rcd_design_path));
        turf plating = Make(g.R, typeof(turf_open_floor_plating));

        // the floor gets the grille first (4 units, 1 s)
        Click(g, plating, 1.5f);
        var grille = Assert.Single(On<obj_structure_grille>(g.R));
        Assert.Equal(1f, g.R.In(() => Dm.num(grille.anchored)));
        Assert.Equal(156f, Matter(g));

        // the grille gets the window (8 units, 3 s)
        Click(g, grille, 3.5f);
        var window = Assert.Single(On<obj_structure_window_fulltile>(g.R));
        Assert.Equal(1f, g.R.In(() => Dm.num(window.anchored)));
        Assert.Equal(148f, Matter(g));

        // a right click takes the window down (10 units, 2.5 s); the grille stays
        Click(g, window, 3f, Right);
        Assert.Empty(On<obj_structure_window>(g.R));
        Assert.Single(On<obj_structure_grille>(g.R));
        Assert.Equal(138f, Matter(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AnAirlockIsBuiltWithTheRcdsAccessSettingsAndDeconstructed()
    {
        var g = Build<obj_item_construction_rcd_loaded>(13007);
        Pick(g, "Airlocks", "Solid Airlocks", 2); // the public airlock
        Act(g, "set", ("access", "engineering")); // the integrated electronics' access list (the default branch of handle_ui_act)
        turf plating = Make(g.R, typeof(turf_open_floor_plating));

        Click(g, plating, 3f);
        Assert.Empty(On<obj_machinery_door_airlock>(g.R)); // 5 s
        g.R.Step(3 * Second);
        var door = Assert.Single(On<obj_machinery_door_airlock>(g.R));
        Assert.IsType<obj_machinery_door_airlock_public>(door);
        g.R.In(() =>
        {
            Assert.Contains("engineering", Dm.each(door.req_access).Select(Dm.str).Concat(Dm.each(door.req_one_access).Select(Dm.str)));
            Assert.Empty(Dm.each<obj_structure_door_assembly>(locate(X, Y, 1)));
        });
        Assert.Equal(160f - 16f, Matter(g));

        // a right click removes it: 5 s, 32 units
        Click(g, door, 5.5f, Right);
        Assert.Empty(On<obj_machinery_door_airlock>(g.R));
        Assert.Equal(144f - 32f, Matter(g));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void AGirderIsFinishedIntoAWallAndAReinforcedWallNeedsAnRcdThatCutsIt()
    {
        var g = Build<obj_item_construction_rcd_loaded>(13008);
        turf plating = Make(g.R, typeof(turf_open_floor_plating));
        g.R.In(() => Dm.@new<obj_structure_girder>(plating));
        Click(g, At(g.R), 2.5f);
        Assert.IsType<turf_closed_wall>(At(g.R));
        Assert.Empty(On<obj_structure_girder>(g.R));
        Assert.Equal(152f, Matter(g)); // the girder's 8 units

        // a reinforced wall: the plain RCD has no values for it
        turf rwall = M5.WallTests.Wall(g.R, typeof(turf_closed_wall_r_wall), X, Y);
        g.R.In(() => Assert.Null(rwall.rcd_vals(g.Bob, g.Rcd)));
        Click(g, rwall, 4.5f, Right);
        Assert.IsType<turf_closed_wall_r_wall>(At(g.R));
        Assert.Equal(152f, Matter(g));

        // the industrial RCD cuts it, in twice a wall's time
        var combat = M5.WallTests.Hold<obj_item_construction_rcd_combat>(g.R, g.Bob);
        var h = g with { Rcd = combat };
        g.R.In(() =>
        {
            combat.mode = RCD_DECONSTRUCT; // what the right click sets before rcd_vals
            Assert.Equal(8f * SECONDS, Dm.num(((DmList)rwall.rcd_vals(g.Bob, combat))["delay"]));
        });
        Click(h, rwall, 6f, Right);
        Assert.IsType<turf_closed_wall_r_wall>(At(g.R));
        g.R.Step(3 * Second);
        Assert.IsType<turf_open_floor_plating>(At(g.R));
        Assert.Equal(500f - 26f, g.R.In(() => combat.matter));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheDestructionScanShowsTheRememberedWallsAndWindowsAsFadingHolograms()
    {
        var g = Build<obj_item_construction_rcd_loaded>(13009);
        turf wallTile = Make(g.R, typeof(turf_open_floor_plating));
        turf windowTile = Make(g.R, typeof(turf_open_floor_plating), X + 1, Y);
        g.R.In(() =>
        {
            wallTile.rcd_memory = RCD_MEMORY_WALL;
            windowTile.rcd_memory = RCD_MEMORY_WINDOWGRILLE;
            Waiting.NoSleep(g.Rcd.ui_action_click(g.Bob, null));
        });
        var wallHolo = Assert.Single(On<obj_effect_rcd_hologram>(g.R));
        var windowHolo = Assert.Single(On<obj_effect_rcd_hologram>(g.R, X + 1, Y));
        g.R.In(() =>
        {
            Assert.Same(GLOB.icon_holographic_wall, wallHolo.icon);
            Assert.Same(GLOB.icon_holographic_window, windowHolo.icon);
            Assert.Equal(0f, wallHolo.alpha); // animate's target (the client shows the fade)
            Waiting.NoSleep(g.Rcd.ui_action_click(g.Bob, null)); // the cooldown: a low buzz, no second scan
        });
        Assert.Single(On<obj_effect_rcd_hologram>(g.R));
        Assert.Contains(g.R.Sounds.Heard, h => h.File == "sound/items/tools/rcdscan.ogg");
        g.R.Step(16 * Second); // RCD_HOLOGRAM_FADE_TIME
        Assert.Empty(On<obj_effect_rcd_hologram>(g.R));
        g.R.AssertNoRuntimes();
    }
}
