using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The slime management console in a booted toy room (xenobio_camera.dm): clicked, it gives its user the xenobiology camera eye,
/// its seven actions and its HUD; the eye picks up the slimes on its tile into the console's storage and spits them out elsewhere,
/// stored monkey cubes become monkeys fed to the slimes, a linked monkey recycler takes dead monkeys back as cubes, a loaded
/// potion goes onto a slime, and the hotkeys (shift, ctrl and alt clicks) do the same from the eye. Ending the view takes it all back.
/// </summary>
public class XenobiologyConsoleTests
{
    private sealed class Lab
    {
        public AudioRoom R;
        public Feed Feed;
        public mob_living_carbon_human Bob;
        public obj_machinery_computer_camera_advanced_xenobio Console;
        public obj_machinery_monkey_recycler Recycler;
    }

    private static Lab Room(ulong seed)
    {
        var lab = new Lab();
        lab.R = new AudioRoom(15, 11, seed: seed, mapExtras: w =>
        {
            lab.Console = Dm.@new<obj_machinery_computer_camera_advanced_xenobio>(locate(3, 5, 1));
            lab.Recycler = Dm.@new<obj_machinery_monkey_recycler>(locate(2, 8, 1));
            Dm.@new<obj_machinery_camera>(locate(9, 9, 1));
        });
        lab.Feed = Listen(lab.R);
        lab.Bob = lab.R.Join(4, 5, "Bob");
        lab.R.Step(Second);
        return lab;
    }

    private static mob_eye_camera_remote_xenobio Use(Lab lab)
    {
        Click(lab.R, lab.Bob, lab.Console);
        lab.R.Step(Second / 2);
        var eye = lab.R.In(() => lab.Bob.remote_control as mob_eye_camera_remote_xenobio);
        Assert.NotNull(eye);
        return eye;
    }

    private static void EyeTo(Lab lab, mob_eye_camera_remote_xenobio eye, int x, int y)
    {
        lab.R.In(() => eye.setLoc(locate(x, y, 1)));
        lab.R.Step(2);
    }

    private static void Press<T>(Lab lab) where T : datum_action_innate
    {
        lab.R.In(() => Waiting.Detach(Dm.locate<T>(lab.Console.actions).Activate()));
        lab.R.Step(4);
    }

    private static mob_living_basic_slime Slime(Lab lab, int x, int y)
    {
        var slime = lab.R.In(() => Dm.@new<mob_living_basic_slime>(locate(x, y, 1), typeof(datum_slime_type_grey)));
        // held still for the test: the console's own stasis trait is the one it takes off
        lab.R.In(() => ADD_TRAIT(slime, TRAIT_IMMOBILIZED, "test"));
        lab.R.Step(2);
        return slime;
    }

    [Fact]
    public void TheConsoleGivesItsUserTheEyeTheActionsAndTheHud()
    {
        var lab = Room(13820);
        var eye = Use(lab);
        lab.R.In(() =>
        {
            Assert.Same(lab.Bob, lab.Console.current_user);
            Assert.Same(eye, lab.Bob.client.eye);
            // the console's area is the eye's (xenobio_camera.dm:12)
            Assert.Equal(Dm.str((get_area(lab.Console) as area).name), eye.allowed_area);
            foreach (Type action in new[]
            {
                typeof(datum_action_innate_slime_place), typeof(datum_action_innate_slime_pick_up), typeof(datum_action_innate_feed_slime),
                typeof(datum_action_innate_monkey_recycle), typeof(datum_action_innate_slime_scan), typeof(datum_action_innate_feed_potion),
                typeof(datum_action_innate_hotkey_help), typeof(datum_action_innate_camera_off),
            })
                Assert.True(Dm.each<datum_action>(lab.Bob.actions).Any(a => action.IsInstanceOfType(a)), action.Name);
            Assert.IsType<atom_movable_screen_xenobio_console>(lab.Bob.hud_used.screen_objects[HUD_XENOBIO_CONSOLE]);
            // the recycler in the same area was linked at map load (xenobio_camera.dm:64)
            Assert.Same(lab.Recycler, lab.Console.connected_recycler_ref.resolve());
        });
        Press<datum_action_innate_hotkey_help>(lab);
        Assert.True(lab.Feed.Any("Shift-click a slime to pick it up"), "the hotkeys' help");
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheEyePicksUpSlimesAndSpitsThemOutElsewhere()
    {
        var lab = Room(13821);
        var eye = Use(lab);
        var slime = Slime(lab, 8, 6);
        EyeTo(lab, eye, 8, 6);
        Press<datum_action_innate_slime_pick_up>(lab);
        lab.R.In(() =>
        {
            Assert.Same(lab.Console, slime.loc);
            Assert.Contains(slime, Dm.each<object>(lab.Console.stored_slimes));
            Assert.True(HAS_TRAIT_FROM(slime, TRAIT_STASIS, XENOBIO_CONSOLE_TRAIT));
            var hud = (atom_movable_screen_xenobio_console)lab.Bob.hud_used.screen_objects[HUD_XENOBIO_CONSOLE];
            Assert.Contains("1", Dm.str(hud.maptext));
        });
        Assert.True(lab.Feed.Any("The slime gets sucked up!"));

        EyeTo(lab, eye, 10, 4);
        Press<datum_action_innate_slime_place>(lab);
        lab.R.Step(Second / 2);
        lab.R.In(() =>
        {
            Assert.Same(locate(10, 4, 1), slime.loc);
            Assert.Equal(0, Dm.length(lab.Console.stored_slimes));
            Assert.False(HAS_TRAIT_FROM(slime, TRAIT_STASIS, XENOBIO_CONSOLE_TRAIT));
        });
        Assert.True(lab.Feed.Any("The slime is spat out!"));
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void StoredMonkeyCubesBecomeMonkeysFedToTheSlimes()
    {
        var lab = Room(13822);
        var eye = Use(lab);
        lab.R.In(() =>
        {
            var cube = Dm.@new<obj_item_food_monkeycube>(lab.Bob.loc);
            Waiting.NoSleep(lab.Bob.put_in_active_hand(cube));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(lab.Console.item_interaction(lab.Bob, cube, Dm.list())));
            Assert.Equal(1f, lab.Console.stored_monkeys);
            Assert.True(QDELETED(cube));
        });
        EyeTo(lab, eye, 9, 5);
        Press<datum_action_innate_feed_slime>(lab);
        lab.R.Step(Second / 2);
        var monkey = lab.R.In(() => Dm.each<mob_living_carbon_human>(range(1, locate(9, 5, 1))).SingleOrDefault(m => ismonkey(m)));
        Assert.NotNull(monkey);
        lab.R.In(() =>
        {
            Assert.NotNull(monkey.has_status_effect(typeof(datum_status_effect_slime_food)));
            Assert.True(HAS_TRAIT(monkey, TRAIT_SPAWNED_MOB));
            Assert.Equal(0f, lab.Console.stored_monkeys);
        });
        Press<datum_action_innate_feed_slime>(lab);
        Assert.True(lab.Feed.Any("needs to have at least 1 monkey stored. Currently has 0 monkeys stored."));
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheLinkedRecyclerTakesADeadMonkeyBackAsACube()
    {
        var lab = Room(13823);
        var eye = Use(lab);
        var monkey = lab.R.In(() => Dm.@new<mob_living_carbon_human_species_monkey>(locate(9, 5, 1)));
        lab.R.Step(Second);
        lab.R.In(() => Waiting.NoSleep(monkey.death()));
        lab.R.Step(2);
        float production = lab.R.In(() => lab.Recycler.cube_production);
        EyeTo(lab, eye, 9, 5);
        Press<datum_action_innate_monkey_recycle>(lab);
        lab.R.In(() =>
        {
            Assert.True(QDELETED(monkey), $"stat {monkey.stat}, monkey {ismonkey(monkey)}, at {(monkey.loc as turf)?.x},{(monkey.loc as turf)?.y}, eye at {(eye.loc as turf)?.x},{(eye.loc as turf)?.y}; "
                + string.Join(" | ", lab.Feed.Chat.Select(c => c.Html)));
            Assert.Equal(production, lab.Console.stored_monkeys, 3);
        });
        // (tg's "reclaimed for recycling!" comes after the suction made the monkey invisible, xenobio_camera.dm:343: no one reads it)
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void ALoadedPotionGoesOntoTheSlimeUnderTheEye()
    {
        var lab = Room(13824);
        var eye = Use(lab);
        var slime = Slime(lab, 8, 6);
        float cores = lab.R.In(() => slime.cores);
        lab.R.In(() =>
        {
            var steroid = Dm.@new<obj_item_slimepotion_slime_steroid>(lab.Bob.loc);
            Waiting.NoSleep(lab.Bob.put_in_active_hand(steroid));
            Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(lab.Console.item_interaction(lab.Bob, steroid, Dm.list())));
            Assert.Same(steroid, lab.Console.current_potion);
            Assert.Same(lab.Console, steroid.loc);
        });
        EyeTo(lab, eye, 8, 6);
        Press<datum_action_innate_feed_potion>(lab);
        lab.R.Step(Second / 2);
        lab.R.In(() =>
        {
            Assert.Equal(cores + 1, slime.cores);
            Assert.True(lab.Console.current_potion == null || QDELETED(lab.Console.current_potion));
        });
        Press<datum_action_innate_feed_potion>(lab);
        Assert.True(lab.Feed.Any("No potion loaded."));
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheHotkeysPickUpPlaceScanAndFeedFromTheEye()
    {
        var lab = Room(13825);
        var eye = Use(lab);
        var slime = Slime(lab, 8, 6);
        EyeTo(lab, eye, 8, 6);
        // shift-click a slime: picked up; shift-click a floor: placed
        lab.R.In(() => Waiting.NoSleep(lab.Bob.ShiftClickOn(slime)));
        lab.R.Step(2);
        lab.R.In(() => Assert.Same(lab.Console, slime.loc));
        lab.R.In(() => Waiting.NoSleep(lab.Bob.ShiftClickOn(locate(9, 6, 1))));
        lab.R.Step(Second / 2);
        lab.R.In(() => Assert.Same(locate(9, 6, 1), slime.loc));
        // ctrl-click a slime: scanned
        lab.R.In(() => Waiting.NoSleep(lab.Bob.CtrlClickOn(slime)));
        lab.R.Step(2);
        Assert.True(lab.Feed.Any("Slime scan results:"), "the scan");
        // ctrl-click a floor with a cube stored: a monkey
        lab.R.In(() => lab.Console.stored_monkeys = 1);
        lab.R.In(() => Waiting.NoSleep(lab.Bob.CtrlClickOn(locate(10, 6, 1))));
        lab.R.Step(Second / 2);
        Assert.True(lab.R.In(() => Dm.each<mob_living_carbon_human>(range(1, locate(10, 6, 1))).Any(m => ismonkey(m))), "a monkey");
        // alt-click a slime with a loaded stabilizer: tg calls the potion's melee attack (xenobio_camera.dm:508), which only taps the
        // slime with it; the potion is not fed and stays loaded (tg's behaviour, kept)
        float chance = lab.R.In(() => slime.mutation_chance);
        lab.R.In(() =>
        {
            var stabilizer = Dm.@new<obj_item_slimepotion_slime_stabilizer>(lab.Console);
            lab.Console.current_potion = stabilizer;
            Waiting.NoSleep(lab.Bob.AltClickOn(slime));
        });
        lab.R.Step(Second / 2);
        Assert.Equal(chance, lab.R.In(() => slime.mutation_chance));
        Assert.True(lab.R.In(() => lab.Console.current_potion is obj_item_slimepotion_slime_stabilizer), "the potion stays loaded");
        Assert.True(lab.R.Sounds.Heard.Any(h => h.File == "sound/items/weapons/tap.ogg"), "the tap");
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void OutsideTheCamerasTheEyeCannotPickUp()
    {
        var lab = Room(13826);
        var eye = Use(lab);
        lab.R.In(() =>
        {
            // a tile the camera network does not see answers "outside of view!"
            var floor = locate(13, 2, 1) as turf_open;
            bool seen = Dm.truthy(SScameras.is_visible_by_cameras(floor));
            if (!seen)
                Assert.Equal(FALSE, Dm.num(lab.Console.validate_turf(lab.Bob, floor)));
            Assert.Equal(TRUE, Dm.num(lab.Console.validate_turf(lab.Bob, locate(9, 8, 1) as turf_open)));
        });
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void EndingTheViewTakesTheEyeTheActionsAndTheHudBack()
    {
        var lab = Room(13827);
        Use(lab);
        Press<datum_action_innate_camera_off>(lab);
        lab.R.In(() =>
        {
            Assert.Null(lab.Bob.remote_control);
            Assert.Same(lab.Bob, lab.Bob.client.eye);
            Assert.DoesNotContain(Dm.each<datum_action>(lab.Bob.actions), a => a is datum_action_innate_slime_pick_up);
            Assert.Null(lab.Bob.hud_used.screen_objects[HUD_XENOBIO_CONSOLE]);
        });
        lab.R.AssertNoRuntimes();
    }

    [Fact]
    public void TheConsoleTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_machinery_computer_camera_advanced_xenobio), typeof(mob_eye_camera_remote_xenobio), typeof(datum_action_innate_slime_place),
            typeof(datum_action_innate_slime_pick_up), typeof(datum_action_innate_feed_slime), typeof(datum_action_innate_monkey_recycle),
            typeof(datum_action_innate_slime_scan), typeof(datum_action_innate_feed_potion), typeof(datum_action_innate_hotkey_help),
            typeof(obj_effect_abstract_sucked_atom), typeof(obj_effect_abstract_xenosuction), typeof(atom_movable_screen_xenobio_console),
            typeof(atom_movable_screen_xenobio_potion), typeof(datum_status_effect_slime_food),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
    }
}
