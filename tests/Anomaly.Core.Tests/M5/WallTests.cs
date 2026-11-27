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
/// M5 slice 4: tg's walls and girders (game/turfs/closed/walls.dm, wall/reinf_walls.dm, game/objects/structures/girders.dm,
/// datums/elements/uses_girder_wall_recipes.dm, _globalvars/lists/girder_wall_recipes.dm, false_walls.dm) in a booted toy room,
/// through the player's clicks: a lit welder slices a wall (try_decon: 2 fuel to start, use_tool's 10 s) into plating, a
/// girder and two iron sheets; the reinforced wall's seven deconstruction stages in order (wirecutters, screwdriver, welder,
/// crowbar, wrench, welder, crowbar) and each one back, with tg's decon icon states; sheets on a girder make its wall (iron:
/// a wall in 4 s; plasteel: a reinforced girder, then a reinforced wall; iron on a displaced girder: a false wall).
/// </summary>
public class WallTests
{
    private const int Second = 20;
    private const string Left = "left=1;button=left";
    private const int WallX = 5, WallY = 4;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        // a human starts at a random nutrition; a fed one's good mood lifts his sanity past neutral, and that level speeds his tools
        // up by a tenth (mood.dm:607, :661, high_sanity): Bob is held between hungry and fed at neutral sanity, where tg's delays
        // are the plain ones
        r.In(() => Waiting.NoSleep(bob.set_nutrition((NUTRITION_LEVEL_HUNGRY + NUTRITION_LEVEL_FED) / 2)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep((bob.mob_mood as datum_mood).set_sanity(SANITY_NEUTRAL)));
        return (r, bob);
    }

    /// <summary>A wall of <paramref name="type"/> on plating at (x, y): the plating under it is its baseturf, as on the station.</summary>
    internal static turf Wall(AudioRoom r, Type type, int x = WallX, int y = WallY) => r.In(() =>
    {
        var plating = (turf)Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_plating)));
        return (turf)Waiting.NoSleep(plating.place_on_top(type));
    });

    internal static T Hold<T>(AudioRoom r, mob_living_carbon_human who, float? amount = null) where T : obj_item => r.In(() =>
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item old)
                qdel(old);
        }
        T item = amount == null ? Dm.@new<T>(get_turf(who)) : (T)Dm.@new(typeof(T), get_turf(who), amount.Value, FALSE);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(item, merge_stacks: FALSE))), "the item reaches a hand");
        if (item is obj_item_weldingtool welder)
            Waiting.NoSleep(welder.attack_self(who));
        return item;
    });

    internal static void Click(AudioRoom r, mob who, atom target, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, Left));
        r.Step((int)(seconds * Second));
    }

    private static turf Here(AudioRoom r, int x = WallX, int y = WallY) => r.In(() => locate(x, y, 1));

    private static List<T> On<T>(AudioRoom r, int x = WallX, int y = WallY) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    [Fact]
    public void AWelderSlicesAWallIntoPlatingAGirderAndTwoIronSheets()
    {
        var (r, bob) = Room(5401);
        Wall(r, typeof(turf_closed_wall));
        var welder = Hold<obj_item_weldingtool>(r, bob);
        Assert.True(r.In(() => Dm.truthy(welder.welding)), "the welder is lit");
        float fuel = r.In(() => Dm.num(welder.get_fuel()));
        r.Step(Second);

        // try_decon (walls.dm:233): tool_start_check(amount = round(100 / 50) = 2), then use_tool(src, user, 100): 10 s
        Click(r, bob, Here(r), 9);
        Assert.IsType<turf_closed_wall>(Here(r));
        Assert.Equal(1f, r.In(() => Dm.num(bob.do_after_count())));
        r.Step(2 * Second);
        Assert.IsType<turf_open_floor_plating>(Here(r));
        Assert.Equal(0f, r.In(() => Dm.num(bob.do_after_count())));

        // dismantle_wall -> break_wall (walls.dm:113): new sheet_type(src, sheet_amount) and the girder; ScrapeAway to the plating
        var girder = Assert.Single(On<obj_structure_girder>(r));
        Assert.Equal(typeof(obj_structure_girder), girder.GetType());
        var iron = Assert.Single(On<obj_item_stack_sheet_iron>(r));
        Assert.Equal(2f, r.In(() => Dm.num(iron.amount)));
        Assert.True(r.In(() => Dm.truthy(girder.density) && Dm.truthy(girder.anchored)));
        Assert.True(r.In(() => Dm.num(welder.get_fuel())) < fuel, "the lit welder burnt fuel during the cut");

        // the break sound (walls.dm:44 break_sound, played at 100) and the welder's own use sounds
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/tools/welder.ogg" && h.Volume > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnlitWelderOrAnotherToolLeavesTheWallStanding()
    {
        var (r, bob) = Room(5402);
        Wall(r, typeof(turf_closed_wall));
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob))); // out again
        Assert.False(r.In(() => Dm.truthy(welder.welding)));
        Click(r, bob, Here(r), 12);
        Assert.IsType<turf_closed_wall>(Here(r));
        Assert.Equal(0f, r.In(() => Dm.num(bob.do_after_count())));

        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, Here(r), 12);
        Assert.IsType<turf_closed_wall>(Here(r));
        r.AssertNoRuntimes();
    }

    // One r-wall step: the tool in hand, a click, the state unchanged just before tg's delay, the next state after it.
    // tg's wrench on a wall without diagonal corners: /turf/closed/wall/wrench_act (walls.dm:349) answers
    // ITEM_INTERACT_SKIP_TO_ATTACK out of combat mode, and tool_act runs before item_interaction (atom_tool_acts.dm:12), so
    // the r-wall's two wrench stages are reached in combat mode only, where base_item_interaction skips tool_act.
    private static void Step<T>(AudioRoom r, mob_living_carbon_human bob, float delay, float expected) where T : obj_item
    {
        r.In(() => bob.set_combat_mode(typeof(T) == typeof(obj_item_wrench) ? TRUE : FALSE, TRUE));
        Hold<T>(r, bob);
        var wall = (turf_closed_wall_r_wall)Here(r);
        float before = r.In(() => wall.d_state);
        if (delay > 0)
        {
            Click(r, bob, wall, delay - 0.5f);
            Assert.Equal(before, r.In(() => wall.d_state));
            r.Step(Second);
        }
        else
            Click(r, bob, wall, 0.5f);
        Assert.Equal(expected, r.In(() => ((turf_closed_wall_r_wall)Here(r)).d_state));
    }

    [Fact]
    public void TheReinforcedWallsSevenStagesGoInOrderAndBack()
    {
        var (r, bob) = Room(5403);
        var rwall = (turf_closed_wall_r_wall)Wall(r, typeof(turf_closed_wall_r_wall));
        Assert.Equal("reinforced_wall-0", r.In(() => rwall.icon_state));

        // out of combat mode a wrench never reaches try_decon (walls.dm:349, above)
        Step<obj_item_wirecutters>(r, bob, 0, SUPPORT_LINES);
        Step<obj_item_screwdriver>(r, bob, 4, COVER);
        Step<obj_item_weldingtool>(r, bob, 6, CUT_COVER);
        Step<obj_item_crowbar>(r, bob, 10, ANCHOR_BOLTS);
        Hold<obj_item_wrench>(r, bob);
        r.In(() => bob.set_combat_mode(FALSE, TRUE));
        Click(r, bob, Here(r), 6);
        Assert.Equal((float)ANCHOR_BOLTS, r.In(() => rwall.d_state));
        Step<obj_item_crowbar>(r, bob, 2, CUT_COVER);
        Step<obj_item_weldingtool>(r, bob, 6, COVER);
        Step<obj_item_screwdriver>(r, bob, 4, SUPPORT_LINES);
        Step<obj_item_wirecutters>(r, bob, 0, INTACT);

        // forward (reinf_walls.dm:50-190)
        Step<obj_item_wirecutters>(r, bob, 0, SUPPORT_LINES);
        Assert.Equal("icons/turf/walls/reinforced_states.dmi", r.In(() => Dm.text("[]", rwall.icon)));
        Assert.Equal("r_wall-1", r.In(() => rwall.icon_state));
        Step<obj_item_screwdriver>(r, bob, 4, COVER);
        Assert.Equal("r_wall-2", r.In(() => rwall.icon_state));
        Step<obj_item_weldingtool>(r, bob, 6, CUT_COVER);
        Step<obj_item_crowbar>(r, bob, 10, ANCHOR_BOLTS);
        Step<obj_item_wrench>(r, bob, 4, SUPPORT_RODS);
        Step<obj_item_weldingtool>(r, bob, 10, SHEATH);
        Assert.Equal("r_wall-6", r.In(() => rwall.icon_state));

        // and back, each stage undone by its own tool
        Step<obj_item_weldingtool>(r, bob, 10, SUPPORT_RODS);
        Step<obj_item_wrench>(r, bob, 4, ANCHOR_BOLTS);
        Step<obj_item_crowbar>(r, bob, 2, CUT_COVER);
        Step<obj_item_weldingtool>(r, bob, 6, COVER);
        Step<obj_item_screwdriver>(r, bob, 4, SUPPORT_LINES);
        Step<obj_item_wirecutters>(r, bob, 0, INTACT);
        Assert.Equal("reinforced_wall-0", r.In(() => rwall.icon_state));
        Assert.Equal("icons/turf/walls/reinforced_wall.dmi", r.In(() => Dm.text("[]", rwall.icon)));

        // forward again to the sheath, and the crowbar's 10 s pry dismantles it: one plasteel and a reinforced girder
        Step<obj_item_wirecutters>(r, bob, 0, SUPPORT_LINES);
        Step<obj_item_screwdriver>(r, bob, 4, COVER);
        Step<obj_item_weldingtool>(r, bob, 6, CUT_COVER);
        Step<obj_item_crowbar>(r, bob, 10, ANCHOR_BOLTS);
        Step<obj_item_wrench>(r, bob, 4, SUPPORT_RODS);
        Step<obj_item_weldingtool>(r, bob, 10, SHEATH);
        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, Here(r), 9.5f);
        Assert.IsType<turf_closed_wall_r_wall>(Here(r));
        r.Step(Second);
        Assert.IsType<turf_open_floor_plating>(Here(r));
        Assert.Equal(typeof(obj_structure_girder_reinforced), Assert.Single(On<obj_structure_girder>(r)).GetType());
        Assert.Equal(1f, r.In(() => Dm.num(Assert.Single(On<obj_item_stack_sheet_plasteel>(r)).amount)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SheetsOnAGirderBuildItsWall()
    {
        var (r, bob) = Room(5404);
        var girder = r.In(() =>
        {
            var plating = (turf)Waiting.NoSleep(locate(WallX, WallY, 1).ChangeTurf(typeof(turf_open_floor_plating)));
            return Dm.@new<obj_structure_girder>(plating);
        });
        r.Step(Second);

        // two iron on a girder (GLOB.main_girder_wall_recipes: /turf/closed/wall's sheet_type, sheet_amount, make_delay 4 s)
        var iron = Hold<obj_item_stack_sheet_iron>(r, bob, 5);
        Click(r, bob, girder, 3.5f);
        Assert.IsType<turf_open_floor_plating>(Here(r));
        r.Step(Second);
        var wall = Assert.IsType<turf_closed_wall>(Here(r));
        Assert.Equal(3f, r.In(() => Dm.num(iron.amount)));
        Assert.True(r.In(() => QDELETED(girder)));
        Assert.Equal(typeof(obj_structure_girder), r.In(() => wall.girder_type));

        // the welder takes it down again, and the plasteel recipe: a reinforced girder (girders.dm:56, 5 s), then its wall
        // (reinf_walls.dm: one plasteel sheet, make_delay 5 s)
        Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, Here(r), 11);
        girder = Assert.Single(On<obj_structure_girder>(r));
        r.In(() => { foreach (var s in Dm.each_cast<atom>(locate(WallX, WallY, 1).contents).OfType<obj_item_stack>().ToList()) qdel(s); });
        var plasteel = Hold<obj_item_stack_sheet_plasteel>(r, bob, 5);
        Click(r, bob, girder, 6);
        var reinforced = Assert.IsType<obj_structure_girder_reinforced>(Assert.Single(On<obj_structure_girder>(r)));
        Assert.Equal(4f, r.In(() => Dm.num(plasteel.amount)));
        Click(r, bob, reinforced, 6);
        Assert.IsType<turf_closed_wall_r_wall>(Here(r));
        Assert.Equal(3f, r.In(() => Dm.num(plasteel.amount)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWrenchedGirderTakesAFalseWallAndAScrewdriverTakesItApart()
    {
        var (r, bob) = Room(5405);
        var girder = r.In(() =>
        {
            var plating = (turf)Waiting.NoSleep(locate(WallX, WallY, 1).ChangeTurf(typeof(turf_open_floor_plating)));
            return Dm.@new<obj_structure_girder>(plating);
        });
        r.Step(Second);

        // the wrench displaces it (girders.dm:92, 4 s): a displaced girder, unanchored
        Hold<obj_item_wrench>(r, bob);
        Click(r, bob, girder, 5);
        var displaced = Assert.IsType<obj_structure_girder_displaced>(Assert.Single(On<obj_structure_girder>(r)));
        Assert.False(r.In(() => Dm.truthy(displaced.anchored)));

        // two iron on a displaced girder: a false wall (girder_wall_recipes.dm:108, 2 s)
        Hold<obj_item_stack_sheet_iron>(r, bob, 2);
        Click(r, bob, displaced, 3);
        var fake = Assert.Single(On<obj_structure_falsewall>(r));
        Assert.True(r.In(() => Dm.truthy(fake.density) && Dm.truthy(fake.opacity)));
        Assert.Equal(typeof(obj_structure_girder_displaced), r.In(() => fake.girder_type));

        // an empty hand opens it (false_walls.dm:46: opening, then toggle_open after 0.5 s)
        r.In(() => { foreach (object held in Dm.each(bob.held_items.Copy())) if (held is obj_item old) qdel(old); });
        Click(r, bob, fake, 1);
        Assert.False(r.In(() => Dm.truthy(fake.density)), "the false wall opened");
        Assert.Equal("wall-open", r.In(() => fake.icon_state));
        Click(r, bob, fake, 1);
        Assert.True(r.In(() => Dm.truthy(fake.density)), "and closed");

        // the welder dismantles it (false_walls.dm:122): the displaced girder and two iron sheets
        Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, fake, 1);
        Assert.True(r.In(() => QDELETED(fake)));
        Assert.IsType<obj_structure_girder_displaced>(Assert.Single(On<obj_structure_girder>(r)));
        Assert.Equal(2f, On<obj_item_stack_sheet_iron>(r).Sum(s => r.In(() => Dm.num(s.amount))));

        // the screwdriver takes a displaced girder apart (girders.dm:65, 4 s): two iron
        r.In(() => { foreach (var s in On<obj_item_stack>(r)) qdel(s); });
        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, Assert.Single(On<obj_structure_girder>(r)), 5);
        Assert.Empty(On<obj_structure_girder>(r));
        Assert.Equal(2f, On<obj_item_stack_sheet_iron>(r).Sum(s => r.In(() => Dm.num(s.amount))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWallSupportOfTwoRodsKeepsAFireAlarmUpWhileTheWallIsCutAndItsWirecuttersDropTheAlarm()
    {
        // wall_support.dm and walls.dm:248 (the M5 leftovers, 2026-10-16): two rods on a wall build the support (2 s); the wall
        // sliced to plating keeps its mounted alarm (atom_mounted.dm:52); the screwdriver unfastens and fastens the support; its
        // wirecutters (right button) give back the two rods, and with no wall left the alarm falls (atom_mounted.dm:76)
        var (r, bob) = Room(5410);
        r.In(() => Waiting.NoSleep(locate(4, 4, 1).ChangeTurf(typeof(turf_open_floor_iron))));
        turf wall = Wall(r, typeof(turf_closed_wall), 4, 5);
        r.In(() => Waiting.NoSleep(bob.setDir(NORTH)));
        Hold<obj_item_wallframe_firealarm>(r, bob);
        Click(r, bob, wall, 1);
        var alarm = Assert.Single(On<obj_machinery_firealarm>(r, 4, 4));

        var rods = Hold<obj_item_stack_rods>(r, bob, 5);
        Click(r, bob, wall, 3);
        var support = Assert.Single(On<obj_structure_wall_support>(r, 4, 5));
        Assert.Equal(3f, r.In(() => Dm.num(rods.amount)));

        Hold<obj_item_weldingtool>(r, bob);
        Click(r, bob, wall, 12);
        Assert.IsType<turf_open_floor_plating>(Here(r, 4, 5));
        Assert.False(r.In(() => QDELETED(alarm)), "the support holds the alarm up");
        Assert.False(r.In(() => QDELETED(support)));

        Hold<obj_item_screwdriver>(r, bob);
        Click(r, bob, support, 1);
        Assert.False(r.In(() => Dm.truthy(support.anchored)), "unfastened");
        Click(r, bob, support, 1);
        Assert.True(r.In(() => Dm.truthy(support.anchored)), "fastened again");

        Hold<obj_item_wirecutters>(r, bob);
        r.Command(() => ClientSeam.Click(bob.client, support, "right=1;button=right"));
        r.Step(Second);
        Assert.True(r.In(() => QDELETED(support)));
        Assert.Equal(2f, r.In(() => On<obj_item_stack_rods>(r, 4, 5).Sum(x => Dm.num(x.amount))));
        Assert.True(r.In(() => QDELETED(alarm)), "no wall and no support: the alarm falls");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoPlanksOnADoorOrAFullTileWindowNailACrudeBarricadeOnItsTileAndACrowbarTakesItOff()
    {
        // can_barricade.dm and deployable.dm (the M5 leftovers, 2026-10-16): planks on a door (door.dm:142) or a full-tile window
        // (window.dm:62), 5 s of hammering, a crude plank barricade on the turf and two planks used; one plank refused; the
        // crowbar (2 s) gives back its one plank (drop_amount)
        var (r, bob) = Room(5411);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1)));
        var window = r.In(() => Dm.@new<obj_structure_window_fulltile>(locate(4, 5, 1)));
        r.Step(Second);
        var planks = Hold<obj_item_stack_sheet_mineral_wood>(r, bob, 1);
        Click(r, bob, door, 6);
        Assert.Empty(On<obj_structure_barricade>(r, 5, 4)); // "need 2 wooden plank sheets!"

        planks = Hold<obj_item_stack_sheet_mineral_wood>(r, bob, 5);
        Assert.True(r.In(() => door._listen_lookup?[COMSIG_ATOM_ATTACKBY] != null), "the door's can_barricade element");
        Click(r, bob, door, 6);
        Assert.Single(On<obj_structure_barricade_wooden_crude>(r, 5, 4));
        Assert.Equal(3f, r.In(() => Dm.num(planks.amount)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/hammering_wood.ogg");
        Click(r, bob, window, 6);
        var onWindow = Assert.Single(On<obj_structure_barricade_wooden_crude>(r, 4, 5));
        Assert.Equal(1f, r.In(() => Dm.num(planks.amount)));
        Assert.Equal("plankbarricade", r.In(() => onWindow.icon_state));

        Hold<obj_item_crowbar>(r, bob);
        Click(r, bob, onWindow, 3);
        Assert.True(r.In(() => QDELETED(onWindow)));
        Assert.Equal(1f, r.In(() => On<obj_item_stack_sheet_mineral_wood>(r, 4, 4).Concat(On<obj_item_stack_sheet_mineral_wood>(r, 4, 5)).Sum(x => Dm.num(x.amount))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WoodPlanksCarryTgsWoodRecipesWithTheWoodenBarricade()
    {
        var (r, bob) = Room(5412);
        var planks = Hold<obj_item_stack_sheet_mineral_wood>(r, bob, 10);
        var recipes = r.In(() => Dm.each((DmList)planks.get_main_recipes()).OfType<datum_stack_recipe>().ToList());
        var barricade = Assert.Single(recipes, x => r.In(() => x.result_type) == typeof(obj_structure_barricade_wooden));
        Assert.Equal(5f, r.In(() => Dm.num(barricade.req_amount)));
        Assert.Contains(recipes, x => r.In(() => x.result_type) == typeof(obj_structure_table_frame_wood));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ADoubleAirlockAndItsAssemblyTakeTwoTilesOfBoundsAlongTheirWay()
    {
        // icons.dm:1266-1285 (the M5 leftovers, 2026-10-16): set_bounds from the icon's width (64: the file's DMI header) and
        // the dir; locs covers the two turfs (door.dm:667's air updates read it); the assembly re-bounds after a move
        var (r, bob) = Room(5413);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock_multi_tile_public_glass>(locate(6, 4, 1)));
        r.Step(2);
        Assert.Equal((64, 64), IconSizes.Of(r.In(() => door.icon)));
        float dir = r.In(() => Dm.num(door.dir));
        bool horizontal = dir == NORTH || dir == SOUTH;
        Assert.Equal(horizontal ? 64f : 32f, r.In(() => door.bound_width));
        Assert.Equal(horizontal ? 32f : 64f, r.In(() => door.bound_height));
        var covered = r.In(() => Dm.each_cast<turf>(door.locs).Select(t => ((int)t.x, (int)t.y)).ToList());
        Assert.Equal(horizontal ? new[] { (6, 4), (7, 4) } : new[] { (6, 4), (6, 5) }, covered);

        var assembly = r.In(() => Dm.@new<obj_structure_door_assembly_multi_tile>(locate(3, 6, 1)));
        r.Step(2);
        r.In(() => { assembly.dir = EAST; Waiting.NoSleep(assembly.Move(locate(4, 6, 1), EAST)); });
        Assert.Equal(EAST, (int)r.In(() => Dm.num(assembly.dir)));
        Assert.Equal(32f, r.In(() => assembly.bound_width)); // east-west: one tile wide, its icon's width tall
        Assert.Equal(r.In(() => IconSizes.Of(assembly.icon).Width), (int)r.In(() => assembly.bound_height));
        r.In(() => Waiting.NoSleep(assembly.Move(locate(4, 7, 1), NORTH)));
        Assert.Equal(r.In(() => IconSizes.Of(assembly.icon).Width), (int)r.In(() => assembly.bound_width)); // turned north by the move
        Assert.Equal(32f, r.In(() => assembly.bound_height));
        Assert.Equal(32f, r.In(() => bob.bound_width)); // a mob is one tile
        Assert.Single(r.In(() => Dm.each(bob.locs).ToList()));
        r.AssertNoRuntimes();
    }
}
