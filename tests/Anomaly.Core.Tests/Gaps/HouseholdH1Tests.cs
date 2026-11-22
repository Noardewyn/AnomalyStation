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
/// Gaps package H1 (D333): the station's washing and household things in a booted toy room with Bob as a player: the sink and the
/// water source (washing hands and items, filling and draining containers, the reclaimer), the toilet (the lid, the flush, the bowl,
/// the cistern, the plunger), the urinal's cake, the shower (its spray, the washing effect, the thermostat, the mist and the wet
/// floor), the washing machine (the dye of a crayon, a pet's bloody mess), the mirror (its menu, breaking it and the omen, welding it),
/// the dresser and the recycler.
/// </summary>
public class HouseholdH1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second); // tg's click cooldown (changeNext_move) of the click before
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    /// <summary>An atom as the map loader makes it: Initialize with mapload (as BrigTimerTests' Mapped).</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y, params object[] args) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        T made = (T)Dm.@new(typeof(T), new object[] { locate(x, y, 1) }.Concat(args).ToArray());
        SSatoms.initialized = previous;
        return made;
    });

    // an act in the newest window of that interface the player has open (tg's input windows)
    private static void ActIn(AudioRoom r, mob who, string iface, string action, string key, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(Dm.kv(key, value))));
        r.Step(4);
    }

    private static float Volume(AudioRoom r, atom a) => r.In(() => ((datum_reagents)a.reagents).total_volume);

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void TheSinkWashesBloodyHandsFillsABeakerDrainsItAndReclaimsItsWater()
    {
        var (r, feed, bob) = Room(7101);
        var sink = Place<obj_structure_sink>(r, 5, 4);
        Assert.Equal(100f, Volume(r, sink));
        r.In(() => bob.blood_in_hands = 3);

        // washing the hands: four seconds, the faucet's sound, five units gone
        Use(r, bob, sink);
        Assert.True(feed.Any("You start washing your hands..."));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("sink-faucet") == true);
        r.Step(4 * Second + 5);
        Assert.True(feed.Any("You wash your hands using"));
        r.In(() => Assert.Equal(0f, bob.blood_in_hands));
        Assert.InRange(Volume(r, sink), 94.9f, 96f); // five units gone, the reclaimer refilling since

        // a beaker fills from it, a right click empties it down the drain
        var beaker = Hold<obj_item_reagent_containers_cup_beaker>(r, bob);
        Use(r, bob, sink);
        Assert.True(r.In(() => ((datum_reagents)beaker.reagents).total_volume) > 0);
        Assert.True(r.In(() => ((datum_reagents)beaker.reagents).has_reagent(typeof(datum_reagent_water))) != null);
        RightClick(r, bob, sink);
        Assert.Equal(0f, r.In(() => ((datum_reagents)beaker.reagents).total_volume));
        Assert.True(feed.Any("washing its contents down the drain"));

        // the reclaimer refills it at 0.5 units a second
        float low = Volume(r, sink);
        r.Step(10 * Second);
        Assert.True(Volume(r, sink) > low + 3, "the sink did not reclaim its water");

        // a crowbar pries the reclaimer out, after which it stays as it is
        r.In(() => qdel(beaker));
        Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, sink);
        r.In(() => Assert.False(Dm.truthy(sink.has_water_reclaimer)));
        Assert.Single(r.In(() => Dm.each<obj_item_stock_parts_water_recycler>(locate(5, 4, 1).contents).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePuddleWashesItemsAndTheInfiniteSourceNeverRunsDry()
    {
        var (r, feed, bob) = Room(7102);
        var puddle = Place<obj_structure_water_source_puddle>(r, 5, 4);
        Hold<obj_item_knife_kitchen>(r, bob);
        Use(r, bob, puddle);
        Assert.True(feed.Any("You start washing"), "nothing washed: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        r.Step(4 * Second + 5);
        Assert.True(feed.Any("You wash"), "the wash did not finish: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.True(Volume(r, puddle) > 1e6, "the water source ran dry");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheToiletsLidFlushBowlCisternAndPlungerWork()
    {
        var (r, feed, bob) = Room(7103);
        var toilet = Place<obj_structure_toilet>(r, 5, 4);
        r.In(() => { toilet.cover_open = FALSE; toilet.update_appearance(); });

        // the alt click lifts the lid: toilet10, with the water in the bowl
        Use(r, bob, toilet, "alt=1");
        r.In(() => Assert.Equal("toilet10", toilet.icon_state));
        Assert.Contains("toilet-water", Overlays(r, toilet));

        // a pen dropped in the bowl blocks the drain: the flush overflows with foam
        var pen = Hold<obj_item_pen>(r, bob);
        Use(r, bob, toilet);
        r.In(() => Assert.Same(pen, toilet.stuck_item));
        RightClick(r, bob, toilet);
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("toilet_flush") == true);
        r.In(() => Assert.True(Dm.truthy(toilet.flushing)));
        r.Step(4 * Second + 2);
        r.In(() => Assert.False(Dm.truthy(toilet.flushing)));
        Assert.True(r.In(() => Dm.each<obj_effect_particle_effect_fluid_foam>(range(2, toilet)).Any()), "no overflow foam");

        // the plunger frees the pen
        var plunger = Hold<obj_item_plunger>(r, bob);
        Use(r, bob, toilet);
        r.Step(3 * Second + 5);
        r.In(() => Assert.Null(toilet.stuck_item));
        r.In(() => Assert.Same(locate(5, 4, 1), pen.loc));

        // the crowbar lifts the cistern's lid; an item hidden in it comes back by hand with the bowl's lid down
        r.In(() => qdel(plunger));
        var crowbar = Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, toilet);
        r.Step(3 * Second + 5);
        r.In(() => Assert.True(Dm.truthy(toilet.cistern_open)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(crowbar)));
        var lighter = Hold<obj_item_lighter>(r, bob);
        Use(r, bob, toilet);
        r.In(() => Assert.Contains(lighter, Dm.each<obj_item>(toilet.cistern_items).ToList()));
        Use(r, bob, toilet, "alt=1"); // the lid down
        Use(r, bob, toilet);
        r.In(() => Assert.Same(bob, lighter.loc));
        Assert.True(feed.Any("in the cistern."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMapsUrinalHidesACakeBehindItsScrewedCap()
    {
        var (r, feed, bob) = Room(7104);
        var urinal = Mapped<obj_structure_urinal>(r, 5, 4);
        r.Step(Second);
        var cake = r.In(() => urinal.hidden_item as obj_item_food_urinalcake);
        Assert.NotNull(cake);
        Hold<obj_item_screwdriver>(r, bob);
        Use(r, bob, urinal);
        r.Step(2 * Second + 5);
        r.In(() => Assert.True(Dm.truthy(urinal.exposed)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.get_active_held_item() as obj_item)));
        Use(r, bob, urinal);
        r.In(() => Assert.Same(bob, cake.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShowerSpraysWashesAndWarmsItsCrewThenWetsTheFloor()
    {
        var (r, feed, bob) = Room(7105);
        var shower = r.In(() => Dm.@new<obj_machinery_shower>(locate(4, 4, 1), SOUTH));
        r.Step(Second);
        r.In(() => bob.blood_in_hands = 3);

        r.In(() => Waiting.NoSleep(shower.interact(bob)));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(shower.actually_on)));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("shower") == true);
        Assert.Contains("water", Overlays(r, shower));
        r.Step(3 * Second);
        // the spray: the washing effect, the water on the turf's things
        r.In(() => Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_washing_regen))));
        r.Step(4 * Second);
        Assert.True(r.In(() => Dm.locate<obj_effect_mist>(locate(4, 4, 1))) != null, "no mist after five seconds of a warm shower");

        // the thermostat to freezing: a cold shower
        float warm = r.In(() => bob.bodytemperature);
        r.In(() => shower.current_temperature = obj_machinery_shower.SHOWER_FREEZING);
        r.Step(3 * Second);
        Assert.True(r.In(() => bob.bodytemperature) < warm, "the freezing shower did not cool Bob");
        Assert.True(feed.Any("is freezing!"));

        // turned off: the floor is wet and the effect is gone
        r.In(() => Waiting.NoSleep(shower.interact(bob)));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(shower.actually_on)));
        r.In(() => Assert.Null(bob.has_status_effect(typeof(datum_status_effect_washing_regen))));
        r.In(() => Assert.NotNull((locate(4, 4, 1) as turf_open).GetComponent(typeof(datum_component_wet_floor))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShowersMultitoolCyclesItsShutOffAndTheTimedModeStopsAfterFifteenSeconds()
    {
        var (r, feed, bob) = Room(7106);
        var shower = r.In(() => Dm.@new<obj_machinery_shower>(locate(5, 4, 1), SOUTH));
        r.Step(Second);
        Hold<obj_item_multitool>(r, bob);
        Use(r, bob, shower);
        r.In(() => Assert.Equal(obj_machinery_shower.SHOWER_MODE_TIMED, Dm.num(shower.mode)));
        Assert.True(feed.Any("run for 15 seconds or until empty"));
        r.In(() => Waiting.NoSleep(shower.interact(bob)));
        r.Step(5 * Second);
        r.In(() => Assert.True(Dm.truthy(shower.actually_on)));
        r.Step(12 * Second);
        r.In(() => Assert.False(Dm.truthy(shower.actually_on)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWashingMachineDyesTheLoadWithACrayonsColour()
    {
        var (r, feed, bob) = Room(7107);
        var washer = Place<obj_machinery_washing_machine>(r, 5, 4);
        r.In(() => Assert.True(Dm.num(washer.max_wash_capacity) >= 2));
        var suit = Hold<obj_item_clothing_under_color_white>(r, bob);
        Use(r, bob, washer);
        var crayon = Hold<obj_item_toy_crayon_red>(r, bob);
        Use(r, bob, washer);
        r.In(() => Assert.Same(crayon, washer.color_source));
        r.In(() => Assert.Equal("wm_1_1", washer.icon_state));

        Use(r, bob, washer); // the door shut
        r.In(() => Assert.Equal("wm_0_1", washer.icon_state));
        RightClick(r, bob, washer);
        r.In(() => Assert.True(Dm.truthy(washer.busy)));
        r.In(() => Assert.Equal("wm_running_0", washer.icon_state));
        r.Step((int)(r.In(() => Dm.num(washer.time_to_wash)) / 10 * Second) + Second);
        r.In(() => Assert.False(Dm.truthy(washer.busy)));
        r.In(() => Assert.Equal("red jumpsuit", suit.name));
        r.In(() => Assert.Equal("#eb0c07", Dm.str(suit.greyscale_colors)));
        r.In(() => Assert.True(QDELETED(crayon)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APetInTheWashingMachineMakesABloodyMessThatSoapCleans()
    {
        var (r, feed, bob) = Room(7108);
        var washer = Place<obj_machinery_washing_machine>(r, 5, 4);
        var corgi = r.In(() => Dm.@new<mob_living_basic_pet_dog_corgi>(washer));
        r.Step(2);
        r.In(() => washer.state_open = FALSE);
        RightClick(r, bob, washer);
        r.Step((int)(r.In(() => Dm.num(washer.time_to_wash)) / 10 * Second) + Second);
        r.In(() => Assert.True(Dm.truthy(washer.bloody_mess)));
        r.In(() => Assert.True(QDELETED(corgi)));
        r.In(() => Assert.Equal("wm_0_blood", washer.icon_state));
        r.In(() => Waiting.NoSleep(washer.wash(CLEAN_TYPE_BLOOD)));
        r.In(() => Assert.False(Dm.truthy(washer.bloody_mess)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMirrorsMenuCutsTheHairAndBreakingItCursesTheBreakerUntilAWeldMendsIt()
    {
        var (r, feed, bob) = Room(7109);
        var mirror = Place<obj_structure_mirror>(r, 4, 5);
        r.In(() => Assert.NotNull(mirror.GetComponent(typeof(datum_component_reflection))));

        // the radial menu, then the hairstyle list
        r.In(() => Waiting.Detach(mirror.change_hair(bob)));
        ActIn(r, bob, "ListInputWindow", "submit", "entry", "Bald");
        r.In(() => Assert.Equal("Bald", Dm.str(bob.hairstyle)));

        // smashed by Bob: mirror_broke, the shatter, seven incidents of bad luck
        r.In(() => bob.set_combat_mode(TRUE));
        var toolbox = Hold<obj_item_storage_toolbox_mechanical>(r, bob);
        for (int i = 0; i < 20 && !r.In(() => Dm.truthy(mirror.broken)); i++)
        {
            Use(r, bob, mirror);
            r.Step(Second);
        }
        r.In(() => Assert.True(Dm.truthy(mirror.broken)));
        r.In(() => Assert.Equal("mirror_broke", mirror.icon_state));
        Assert.True(feed.Any("A chill runs down your spine as"));
        var omen = r.In(() => bob.GetComponent(typeof(datum_component_omen)) as datum_component_omen);
        Assert.NotNull(omen);
        r.In(() => Assert.Equal(7f, omen.incidents_left));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_CURSED)));

        // a welder mends it
        r.In(() => bob.set_combat_mode(FALSE));
        r.In(() => qdel(toolbox));
        var welder = Hold<obj_item_weldingtool>(r, bob);
        r.In(() => Waiting.NoSleep(welder.attack_self(bob)));
        r.Step(2);
        Use(r, bob, mirror);
        r.Step(2 * Second);
        r.In(() => Assert.False(Dm.truthy(mirror.broken)));
        r.In(() => Assert.Equal("mirror", mirror.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOmensLastIncidentLiftsIt()
    {
        var (r, feed, bob) = Room(7110);
        r.In(() => Waiting.NoSleep(bob.AddComponent(typeof(datum_component_omen), Dm.named("incidents_left", 1))));
        var omen = r.In(() => bob.GetComponent(typeof(datum_component_omen)) as datum_component_omen);
        r.In(() => omen.consume_omen());
        r.In(() => Assert.True(QDELETED(omen)));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_CURSED)));
        Assert.True(feed.Any("You feel a horrible omen lifted off your shoulders!"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDresserChangesTheUnderwear()
    {
        var (r, feed, bob) = Room(7111);
        var dresser = Place<obj_structure_dresser>(r, 5, 4);
        string socks = r.In(() => Dm.each<object>(SSaccessories.socks_list).Select(Dm.str).First(s => s != "Nude"));
        Use(r, bob, dresser);
        ActIn(r, bob, "ListInputWindow", "submit", "entry", "Socks");
        ActIn(r, bob, "ListInputWindow", "submit", "entry", socks);
        r.In(() => Assert.Equal(socks, bob.socks));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheRecyclerSalvagesAnItemsMaterialsAndStopsForTheLiving()
    {
        var (r, feed, bob) = Room(7112);
        var recycler = r.In(() => Dm.@new<obj_machinery_recycler>(locate(8, 4, 1)));
        r.Step(Second);
        r.In(() => Assert.Equal("grinder-o1", recycler.icon_state));

        // an iron rod stack goes in: its iron comes out as a sheet
        var rods = r.In(() => Dm.@new<obj_item_stack_rods>(locate(7, 4, 1), 10));
        r.Step(2);
        r.In(() => Waiting.NoSleep(rods.forceMove(locate(8, 4, 1))));
        r.Step(Second);
        r.In(() => Assert.True(QDELETED(rods)));
        Assert.True(r.In(() => Dm.each<obj_item_stack_sheet_iron>(locate(8, 4, 1).contents).Any()), "no iron came out");

        // a living mob triggers the safety stop, which reboots ten seconds later
        var mouse = r.In(() => Dm.@new<mob_living_basic_mouse>(locate(7, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(mouse.forceMove(locate(8, 4, 1))));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(recycler.safety_mode)));
        r.In(() => Assert.Equal("grinder-o0", recycler.icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("buzz-sigh") == true);
        r.In(() => Assert.False(QDELETED(mouse)));
        r.Step(10 * Second + 5);
        r.In(() => Assert.False(Dm.truthy(recycler.safety_mode)));
        r.AssertNoRuntimes();
    }
}
