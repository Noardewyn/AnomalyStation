using System.Text.Json;
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
/// Gaps package ST4 (D333): the buildable structures, in a booted toy room with Bob as a player: the bonfire (lit by a lighter, burning
/// who stands in it, its grill from a rod, taken apart into its logs), the fireplace (fed planks, lit, its fire stage and light, put out
/// with its fuel kept), the chest press (a rep's flick, creak and stamina) and the punching bag, the mineral doors (opened and shut,
/// the see-through plasma door), the guillotine (a sharp blade takes a head), the platform (its turf's traits, its sheets), the
/// spirit board, the loom (cotton spun into cloth), the training machine (its window, its roaming, its attached target) and the
/// training toolbox's hit counter, and the steps a runner climbs onto (their trip is overridden in tg, as here).
/// </summary>
public class StructuresST4Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who, Action<T> setup = null) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        if (setup != null)
            r.In(() => setup(item));
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    // an act in the newest window of that interface the player has open (tg's input windows: AlertModal, ListInputWindow)
    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(args.Select(a => (object)Dm.kv(a.Key, a.Value)).ToArray())));
        r.Step(4);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static string Examine(AudioRoom r, atom a, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(a.examine(who))).Select(o => Dm.str(o))));

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private sealed class FlickRecorder : IAppearanceSink
    {
        private readonly List<(object Target, object State)> _flicks;

        public FlickRecorder(List<(object Target, object State)> flicks) => _flicks = flicks;

        public void Changed(object atom, AppearanceAspect aspect) { }

        public void Animate(object target, DmList arguments) { }

        public void AnimateStep(object target, object subject, DmList arguments, DmList before) { }

        public void Flick(object target, object state) => _flicks.Add((target, state));

        public void LightChanged(object atom) { }

        public void VisibilityChanged(object atom) { }
    }

    [Fact]
    public void TheBonfireIsLitByALighterBurnsWhoStandsInItAndGoesOut()
    {
        var (r, feed, bob) = Room(8401);
        var bonfire = Place<obj_structure_bonfire>(r, 5, 4);
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        Hold<obj_item_lighter>(r, bob, l => l.set_lit(TRUE));

        Use(r, bob, bonfire);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bonfire.burning));
            Assert.Equal("bonfire_on_fire", bonfire.icon_state);
            Assert.Equal(6f, Dm.num(bonfire.light_range));
            Assert.IsType<particles_bonfire>(bonfire.particles);
        });
        Assert.True(Heard(r, "match_strike")); // tg's burning loop starts
        r.Step(4 * Second);
        Assert.True(Heard(r, "comfyfire")); // and crackles on
        r.In(() => Assert.True(Dm.truthy(dummy.on_fire), "the one standing in the bonfire did not catch fire"));

        r.In(() => Waiting.NoSleep(bonfire.extinguish()));
        r.In(() =>
        {
            Assert.False(Dm.truthy(bonfire.burning));
            Assert.Equal("bonfire", bonfire.icon_state);
            Assert.Null(bonfire.particles);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBonfireTakesARodAsAGrillAndComesApartIntoItsLogs()
    {
        var (r, feed, bob) = Room(8402);
        var bonfire = Place<obj_structure_bonfire>(r, 5, 4);
        r.In(() =>
        {
            for (int i = 0; i < 2; i++)
                Waiting.NoSleep(Dm.@new<obj_item_grown_log>(bonfire).forceMove(bonfire));
        });
        var rods = Hold<obj_item_stack_rods>(r, bob, s => Waiting.NoSleep(s.add(4)));
        float before = r.In(() => Dm.num(rods.amount));

        Use(r, bob, bonfire);
        ActIn(r, bob, "AlertModal", "choose", ("choice", "Grill"));
        r.In(() =>
        {
            Assert.True(Dm.truthy(bonfire.grill));
            Assert.Equal(before - 1, Dm.num(rods.amount));
        });
        Assert.Contains("bonfire_grill", Overlays(r, bonfire));
        Assert.True(feed.Any("You add a grill to"));

        // taken apart by an empty hand: its logs and the grill's rod on the floor
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(rods)));
        Use(r, bob, bonfire);
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(bonfire));
            turf floor = locate(5, 4, 1);
            Assert.Equal(2, Dm.each<obj_item_grown_log>(floor.contents).Count());
            Assert.Contains(Dm.each<obj_item_stack_rods>(floor.contents), s => s != rods);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFireplaceBurnsItsPlanksInTgsFireStagesAndKeepsItsFuelWhenPutOut()
    {
        var (r, feed, bob) = Room(8403);
        var fireplace = Place<obj_structure_fireplace>(r, 5, 4);
        var wood = Hold<obj_item_stack_sheet_mineral_wood>(r, bob, s => Waiting.NoSleep(s.add(9)));

        Use(r, bob, fireplace);
        Assert.True(feed.Any("You add some fuel to"));
        r.In(() =>
        {
            Assert.Equal(1500f, fireplace.fuel_added); // ten planks, 15 seconds each
            Assert.True(QDELETED(wood) || Dm.num(wood.amount) == 0);
        });

        Hold<obj_item_lighter>(r, bob, l => l.set_lit(TRUE));
        Use(r, bob, fireplace);
        r.In(() =>
        {
            Assert.True(Dm.truthy(fireplace.lit));
            Assert.Equal("A large stone brick fireplace, warm and cozy.", fireplace.desc);
            Assert.Equal(3f, Dm.num(fireplace.light_range)); // 1000 to 1500 deciseconds of fire left
        });
        Assert.Contains("fireplace_fire2", Overlays(r, fireplace));
        Assert.Contains("fireplace_glow", Overlays(r, fireplace));

        r.Step(2 * Second);
        r.In(() => Waiting.NoSleep(fireplace.extinguish()));
        r.In(() =>
        {
            Assert.False(Dm.truthy(fireplace.lit));
            Assert.InRange(fireplace.fuel_added, 1400f, 1500f); // what was left of the fire
            Assert.Equal(0f, Dm.num(fireplace.light_range));
            Assert.Equal("A large stone brick fireplace.", fireplace.desc);
        });
        Assert.DoesNotContain("fireplace_glow", Overlays(r, fireplace));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChestPressRunsARepThatFlicksCreaksAndTires()
    {
        var (r, feed, bob) = Room(8404);
        var press = Place<obj_structure_weightmachine>(r, 5, 4);
        var flicks = new List<(object Target, object State)>();
        PresentationSinks.Of(r.World).Appearance = new FlickRecorder(flicks);
        r.In(() => bob.nutrition = NUTRITION_LEVEL_FED);

        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(press.buckle_mob(bob, force: TRUE)))));
        r.In(() => Assert.Contains(press.weight_action, Dm.each<datum_action>(bob.actions)));
        float stamina = r.In(() => Dm.num(bob.get_stamina_loss()));
        float nutrition = r.In(() => bob.nutrition);

        r.In(() => Waiting.Detach(press.weight_action.Trigger(bob)));
        r.Step(9 * Second);
        Assert.Contains(flicks, f => ReferenceEquals(f.Target, press) && Dm.eq(f.State, "stacklifter-u"));
        Assert.True(Heard(r, "creak"));
        Assert.True(Heard(r, "click"));
        r.In(() =>
        {
            Assert.True(Dm.num(bob.get_stamina_loss()) > stamina, "the rep drained no stamina");
            Assert.True(bob.nutrition <= nutrition - 5 + 0.5f, "the rep burnt no nutrition");
            Assert.Equal("stacklifter", press.icon_state);
        });

        r.In(() => Waiting.NoSleep(press.unbuckle_mob(bob)));
        r.In(() => Assert.DoesNotContain(press.weight_action, Dm.each<datum_action>(bob.actions)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePunchingBagSwingsWithAHitSoundAndTiresThePuncher()
    {
        var (r, feed, bob) = Room(8405);
        var bag = Place<obj_structure_punching_bag>(r, 5, 4);
        var flicks = new List<(object Target, object State)>();
        PresentationSinks.Of(r.World).Appearance = new FlickRecorder(flicks);
        float stamina = r.In(() => Dm.num(bob.get_stamina_loss()));

        Use(r, bob, bag);
        Assert.Contains(flicks, f => ReferenceEquals(f.Target, bag) && Dm.eq(f.State, "punchingbag-punch"));
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("genhit", StringComparison.Ordinal) || (h.File ?? "").Contains("punch", StringComparison.Ordinal));
        r.In(() => Assert.True(Dm.num(bob.get_stamina_loss()) >= stamina + 3 - 0.01f, "the punch cost no stamina"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMineralDoorSwingsOpenAndShutAndThePlasmaOneStaysSeeThrough()
    {
        var (r, feed, bob) = Room(8406);
        var door = Place<obj_structure_mineral_door_iron>(r, 5, 4);
        r.In(() =>
        {
            Assert.True(Dm.truthy(door.density));
            Assert.True(Dm.truthy(door.opacity));
            Assert.Equal(20f, door.sheetAmount);
        });

        Use(r, bob, door);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(door.door_opened));
            Assert.False(Dm.truthy(door.density));
            Assert.False(Dm.truthy(door.opacity));
            Assert.Equal("metalopen", door.icon_state);
        });
        Assert.True(Heard(r, "stonedoor_openclose"));

        Use(r, bob, door);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(door.door_opened));
            Assert.True(Dm.truthy(door.density));
            Assert.True(Dm.truthy(door.opacity));
            Assert.Equal("metal", door.icon_state);
        });

        var plasma = Place<obj_structure_mineral_door_transparent_plasma>(r, 4, 5);
        Use(r, bob, plasma);
        r.Step(2 * Second);
        Use(r, bob, plasma);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(plasma.density));
            Assert.False(Dm.truthy(plasma.opacity));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheGuillotinesSharpBladeTakesTheHeadOfWhoIsStrappedIn()
    {
        var (r, feed, bob) = Room(8407);
        var guillotine = Place<obj_structure_guillotine>(r, 5, 4);
        mob_living_carbon_human victim = r.Dummy(5, 4);
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(guillotine.buckle_mob(victim, force: TRUE)))));
        r.In(() => Assert.Equal(-16f, victim.pixel_y));
        Assert.Contains("strapped in", Examine(r, guillotine, bob));

        r.In(() => bob.set_combat_mode(TRUE));
        Use(r, bob, guillotine);
        Assert.True(feed.Any("You begin to the pull the lever."));
        r.Step(5 * Second);
        r.In(() =>
        {
            Assert.Null(victim.get_bodypart(BODY_ZONE_HEAD));
            Assert.Equal(1f, guillotine.kill_count);
            Assert.Equal(9f, guillotine.blade_sharpness);
            Assert.Equal("guillotine", guillotine.icon_state);
            Assert.False(Dm.truthy(guillotine.has_buckled_mobs()));
        });
        Assert.True(Heard(r, "guillotine"));
        Assert.Contains("guillotine_bloody_overlay", Overlays(r, guillotine));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePlatformsTurfIgnoresSlowdownAndItComesApartIntoItsSheets()
    {
        var (r, feed, bob) = Room(8408);
        var platform = Place<obj_structure_platform_iron>(r, 5, 4);
        r.In(() =>
        {
            turf floor = locate(5, 4, 1);
            Assert.True(HAS_TRAIT(floor, TRAIT_TURF_IGNORE_SLOWDOWN));
            Assert.True(HAS_TRAIT(floor, TRAIT_TURF_IGNORE_SLIPPERY));
            Assert.True(HAS_TRAIT(platform, TRAIT_ELEVATING_OBJECT));
        });

        Hold<obj_item_screwdriver>(r, bob);
        RightClick(r, bob, platform);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(platform));
            obj_item_stack_sheet_iron sheets = Dm.each<obj_item_stack_sheet_iron>(locate(5, 4, 1).contents).FirstOrDefault();
            Assert.NotNull(sheets);
            Assert.Equal(2f, Dm.num(sheets.amount));
            Assert.False(HAS_TRAIT(locate(5, 4, 1), TRAIT_TURF_IGNORE_SLOWDOWN));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLoomSpinsRawCottonIntoClothFourAtATime()
    {
        var (r, feed, bob) = Room(8409);
        var loom = Place<obj_structure_loom>(r, 5, 4);
        var cotton = Hold<obj_item_stack_sheet_cotton>(r, bob, s => Waiting.NoSleep(s.add(7)));
        Assert.Contains("You could probably process", Examine(r, cotton, bob));

        Use(r, bob, loom);
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(QDELETED(cotton) || Dm.num(cotton.amount) == 0);
            obj_item_stack_sheet_cloth cloth = Dm.each<obj_item_stack_sheet_cloth>(locate(5, 4, 1).contents).FirstOrDefault();
            Assert.NotNull(cloth);
            Assert.Equal(2f, Dm.num(cloth.amount));
        });
        Assert.True(feed.Any("spun"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTrainingMachinesWindowSetsItsRangeAndItRoamsWithItsTarget()
    {
        var (r, feed, bob) = Room(8410);
        var machine = Place<obj_structure_training_machine>(r, 5, 4);
        var target = Hold<obj_item_target>(r, bob);

        Use(r, bob, machine);
        r.In(() =>
        {
            Assert.Same(target, machine.attached_item);
            Assert.Contains(target, Dm.each<atom>(machine.vis_contents));
            Assert.True(Dm.truthy(machine.density));
        });
        Assert.True(feed.Any("to the training device."));

        Take(bob);
        Use(r, bob, machine);
        var data = Data(r, bob, out string window);
        Assert.Equal(1, data.GetProperty("range").GetDouble());
        Act(r, bob, window, "range", ("range", 3));
        Act(r, bob, window, "toggle");
        r.In(() =>
        {
            Assert.Equal(3f, machine.range);
            Assert.True(Dm.truthy(machine.moving));
        });
        Assert.True(feed.Any("Beginning training simulation."));

        r.Step(10 * Second);
        r.In(() =>
        {
            turf at = get_turf(machine);
            Assert.True(get_dist(at, machine.starting_turf) <= 3, "the machine left its range");
        });
        // the machine may have roamed out of the window's reach (tg's physical state): stopped as its button would
        r.In(() => Waiting.Detach(machine.toggle()));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(machine.moving)));
        Assert.True(feed.Any("Ending training simulation."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheTrainingToolboxCountsItsHitsAndItsLapResetsOnAltClick()
    {
        var (r, feed, bob) = Room(8411);
        mob_living_carbon_human dummy = r.Dummy(5, 4);
        var toolbox = Hold<obj_item_training_toolbox>(r, bob);
        r.In(() => bob.set_combat_mode(TRUE));

        Use(r, bob, dummy);
        Use(r, bob, dummy);
        r.In(() =>
        {
            Assert.Equal(2f, toolbox.total_hits);
            Assert.Equal(2f, toolbox.lap_hits);
        });
        Assert.True(Heard(r, "smash"));
        Assert.Contains("Total Hits: <b>2</b>", Examine(r, toolbox, bob));

        r.In(() => Waiting.NoSleep(toolbox.click_alt(bob)));
        r.In(() => Assert.Equal(0f, toolbox.lap_hits));
        Assert.Contains("Current Lap: <b>0</b>", Examine(r, toolbox, bob));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStepsLetARunnerClimbOnWithoutTrippingAsTgsOverriddenHandlerDoes()
    {
        // tg at the snapshot: the steps register their trip (on_enter) on their turf's COMSIG_ATOM_ENTERED through connect_loc, then
        // climb_walkable's own connect_loc registers on_climb_enter for the same turf and signal with override = TRUE
        // (connect_loc.dm:32), which replaces it: a runner coming from the side the steps face climbs on and does not trip.
        var (r, feed, bob) = Room(8412);
        var steps = Place<obj_structure_steps>(r, 4, 5);
        r.In(() =>
        {
            Waiting.NoSleep(steps.setDir(SOUTH));
            bob.move_intent = MOVE_INTENT_RUN;
            Waiting.NoSleep(bob.Move(locate(4, 5, 1), NORTH));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(locate(4, 5, 1), bob.loc);
            Assert.True(HAS_TRAIT(bob, TRAIT_ON_CLIMBABLE));
            Assert.Null(bob.has_status_effect(typeof(datum_status_effect_incapacitating_knockdown)));
        });
        Assert.False(feed.Any("You tripped over"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSpiritBoardNeedsTwoAttentivePlayersAroundIt()
    {
        var (r, feed, bob) = Room(8413);
        var board = Place<obj_structure_spirit_board>(r, 5, 4);
        Assert.Contains("The planchette is currently at the letter \"A\".", Examine(r, board, bob));

        // alone: not enough people
        Use(r, bob, board);
        Assert.True(feed.Any("There aren't enough people around to use") || feed.Any("It's too bright here to use"));
        r.AssertNoRuntimes();
    }
}
