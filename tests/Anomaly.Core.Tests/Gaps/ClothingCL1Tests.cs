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
/// Gaps package CL1 (D333): the clothing leftovers in a booted toy room with Bob as a player: the welding goggles' and the welding
/// mask's visors flipped with their tint and sounds, the cargo gauntlets' strong pull, the laser tag vests and pins, the animal
/// masks' speech and curse, the jumpsuit rolled down and its sensors set, installed and repaired, the shoes that quack, dry the floor
/// and go under the uniform, the costumes that disguise and befriend, the hats, badges and shirts with procs of their own, and the
/// clothes' suicides.
/// </summary>
public class ClothingCL1Tests
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

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, float slot) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(item, slot, bypass_equip_delay_self: TRUE))), "could not wear " + item.type.Name));
        r.Step(2);
        return item;
    }

    private static void Use(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        Click(r, who, target, modifiers);
    }

    private static void RightClick(AudioRoom r, mob who, atom target, string modifiers = "")
    {
        r.Step(Second);
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right" + (modifiers.Length > 0 ? ";" + modifiers : "")));
        r.Step(2);
    }

    // the use-in-hand key (Z, tg's activate_inhand: the held item's attack_self), pressed and released
    private static void UseInHand(AudioRoom r, mob who)
    {
        r.Step(Second / 2);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Step(2);
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    private static void Walk(AudioRoom r, mob who, int x, int y, float dir)
    {
        r.Step(Second / 2);
        r.In(() => Waiting.NoSleep(who.Move(locate(x, y, 1), dir)));
        r.Step(4);
    }

    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(args.Select(a => (object)Dm.kv(a.Key, a.Value)).ToArray())));
        r.Step(4);
    }

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    private static string Examine(AudioRoom r, atom thing, mob who) => r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(thing.examine(who))).Select(Dm.str)));

    private static float Stamina(AudioRoom r, mob_living who) => r.In(() => Dm.num(who.get_stamina_loss()));

    [Fact]
    public void TheWeldingGogglesAndMaskFlipTheirVisorsWithTheirTintAndSounds()
    {
        var (r, feed, bob) = Room(9701);

        // the goggles in hand: Z pushes them up (no tint, no flash protection, tg's "up" state) and back down
        var goggles = Hold<obj_item_clothing_glasses_welding>(r, bob);
        r.In(() =>
        {
            Assert.Equal("welding-g", goggles.icon_state);
            Assert.Equal(2f, goggles.tint);
        });
        UseInHand(r, bob);
        r.In(() =>
        {
            Assert.True(Dm.truthy(goggles.up));
            Assert.Equal("welding-gup", goggles.icon_state);
            Assert.Equal(0f, goggles.tint);
            Assert.Equal(0f, goggles.flash_protect);
        });
        Assert.True(feed.Any("out of the way"));
        UseInHand(r, bob);
        r.In(() =>
        {
            Assert.False(Dm.truthy(goggles.up));
            Assert.Equal("welding-g", goggles.icon_state);
            Assert.Equal(2f, goggles.tint);
        });
        Assert.True(feed.Any("back into place"));

        // worn down, they darken the view (the "tint" fullscreen); the visor's toggle (its action button) clears it
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(goggles)));
        r.Step(2);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(goggles, ITEM_SLOT_EYES, bypass_equip_delay_self: TRUE)))));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.@in("tint", bob.screens), "the tint fullscreen");
            Assert.Equal(2f, ((atom_movable_screen_fullscreen)bob.screens["tint"]).severity);
        });
        r.In(() => Waiting.NoSleep(goggles.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal("welding-gup", goggles.icon_state);
            Assert.False(Dm.@in("tint", bob.screens), "the tint fullscreen is cleared");
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(goggles)));

        // the welding mask: its visor goes up with tg's visor sound and its own message, and down again
        var mask = Hold<obj_item_clothing_mask_gas_welding>(r, bob);
        r.In(() => Assert.Equal("weldingmask", mask.icon_state));
        UseInHand(r, bob);
        r.In(() =>
        {
            Assert.True(Dm.truthy(mask.up));
            Assert.Equal("weldingmaskup", mask.icon_state);
            Assert.Equal(0f, mask.tint);
        });
        Assert.True(feed.Any("You push the visor up."));
        Assert.True(Heard(r, "helmet/visor_up"), r.Sounds.Dump());
        UseInHand(r, bob);
        r.In(() =>
        {
            Assert.False(Dm.truthy(mask.up));
            Assert.Equal("weldingmask", mask.icon_state);
            Assert.Equal(2f, mask.tint);
        });
        Assert.True(feed.Any("You pull the visor down."));
        Assert.True(Heard(r, "helmet/visor_down"), r.Sounds.Dump());
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCargoGauntletsHoldWhatTheirWearerPulls()
    {
        var (r, feed, bob) = Room(9702);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        var crate = Place<obj_structure_closet_crate>(r, 5, 4);

        var gauntlets = Wear<obj_item_clothing_gloves_cargo_gauntlet>(r, bob, ITEM_SLOT_GLOVES);
        Assert.True(feed.Any("You feel the gauntlets activate"));
        var pull = r.In(() => bob.GetComponent(typeof(datum_component_strong_pull)) as datum_component_strong_pull);
        Assert.NotNull(pull);

        // Bob pulls the crate: it is held too tightly for Alice to take it away
        Use(r, bob, crate, "ctrl=1");
        r.In(() =>
        {
            Assert.Same(crate, bob.pulling);
            Assert.Same(crate, pull.strongpulling);
            Assert.True(HAS_TRAIT(crate, TRAIT_STRONGPULL));
        });
        Use(r, alice, crate, "ctrl=1");
        Assert.Contains(feed.Chat, c => c.Target == alice && c.Html == "balloon: gripped too tightly!");
        r.In(() =>
        {
            Assert.Same(crate, bob.pulling);
            Assert.Null(alice.pulling);
        });

        // taken off, the gauntlets let go of their grip
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gauntlets)));
        r.Step(2);
        Assert.True(feed.Any("You have lost the grip power of"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLaserTagVestsJoinATeamThatOnlyTheOtherTeamsBeamsTire()
    {
        var (r, _, bob) = Room(9703);
        mob_living_carbon_human alice = r.Join(9, 4, "Alice");
        Wear<obj_item_clothing_suit_redtag>(r, bob, ITEM_SLOT_OCLOTHING);
        var blueVest = Wear<obj_item_clothing_suit_bluetag>(r, alice, ITEM_SLOT_OCLOTHING);
        r.In(() =>
        {
            Assert.Equal(LASERTAG_TEAM_RED, ((datum_component_lasertag)bob.GetComponent(typeof(datum_component_lasertag))).team_color);
            Assert.Equal(LASERTAG_TEAM_BLUE, ((datum_component_lasertag)alice.GetComponent(typeof(datum_component_lasertag))).team_color);
        });

        // the red gun's pin fires for the red team only; an unsuited player is refused too
        var gun = r.In(() => Dm.@new<obj_item_gun_energy_laser_redtag>(bob.loc));
        r.Step(2);
        r.In(() =>
        {
            var pin = Assert.IsType<obj_item_firing_pin_tag_red>(gun.pin);
            Assert.True(Dm.truthy(Waiting.NoSleep(pin.pin_auth(bob))));
            Assert.False(Dm.truthy(Waiting.NoSleep(pin.pin_auth(alice))));
        });

        // a red beam tires the blue player, a blue one does not
        float before = Stamina(r, alice);
        r.In(() =>
        {
            var beam = Dm.@new<obj_projectile_beam_lasertag_redtag>(locate(7, 4, 1));
            beam.original = alice;
            Waiting.NoSleep(beam.fire(90));
        });
        r.Step(Second);
        float hit = Stamina(r, alice);
        Assert.True(hit >= before + 30, $"the red beam tired her by {hit - before}");
        r.In(() =>
        {
            var beam = Dm.@new<obj_projectile_beam_lasertag_bluetag>(locate(7, 4, 1));
            beam.original = alice;
            Waiting.NoSleep(beam.fire(90));
        });
        r.Step(Second);
        Assert.True(Stamina(r, alice) <= hit, "her own team's beam tired her");

        // the vest off, the team is gone
        r.In(() => Waiting.NoSleep(alice.dropItemToGround(blueVest)));
        r.Step(2);
        r.In(() => Assert.Null(alice.GetComponent(typeof(datum_component_lasertag))));

        // the security box of tag pins holds three of each team
        var box = Place<obj_item_storage_box_lasertagpins>(r, 4, 5);
        r.In(() =>
        {
            Assert.Equal(3, Dm.each<obj_item_firing_pin_tag_red>(box.contents).Count());
            Assert.Equal(3, Dm.each<obj_item_firing_pin_tag_blue>(box.contents).Count());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePigMaskOinksItsVoiceboxTurnsOffAndItsCursedKindMeltsOntoTheFace()
    {
        var (r, feed, bob) = Room(9704);
        var mask = Wear<obj_item_clothing_mask_animal_pig>(r, bob, ITEM_SLOT_MASK);
        r.In(() => Waiting.NoSleep(bob.say("Hello there")));
        r.Step(2);
        Assert.True(feed.Any("Oink") || feed.Any("Squee"), "the mask did not change the speech: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));

        // the alt-click turns its voicebox off: the speech is left as said
        Use(r, bob, mask, "alt=1");
        Assert.True(feed.Any("voicebox"));
        r.In(() => Assert.True(Dm.truthy(Dm.bitand(mask.clothing_flags, VOICEBOX_DISABLED))));
        Assert.Contains("disabled", Examine(r, mask, bob), StringComparison.Ordinal);
        r.In(() => Waiting.NoSleep(bob.say("General Kenobi")));
        r.Step(2);
        Assert.True(feed.Any("General Kenobi"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mask)));

        // the cursed pig mask: its curse sound, its name; worn, it cannot be taken off
        var cursed = Place<obj_item_clothing_mask_animal_pig_cursed>(r, 5, 4);
        Assert.True(Heard(r, "magic/pighead_curse"), r.Sounds.Dump());
        r.In(() =>
        {
            Assert.Equal("pig face", cursed.name);
            Assert.True(HAS_TRAIT(cursed, TRAIT_NODROP));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(cursed, ITEM_SLOT_MASK, bypass_equip_delay_self: TRUE))));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(Waiting.NoSleep(bob.dropItemToGround(cursed))));
            Assert.Same(cursed, bob.wear_mask);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheJumpsuitRollsDownSetsItsSensorsAndTakesANewSensor()
    {
        var (r, feed, bob) = Room(9705);
        var suit = Wear<obj_item_clothing_under_color_grey>(r, bob, ITEM_SLOT_ICLOTHING);

        // the alt-click rolls it down (the chest and arms bare) and back up
        Use(r, bob, suit, "alt=1");
        Assert.True(feed.Any("You adjust the suit to wear it more casually."));
        r.In(() =>
        {
            Assert.Equal((float)ALT_STYLE, suit.adjusted);
            Assert.False(Dm.truthy(Dm.bitand(suit.body_parts_covered, CHEST)));
        });
        Use(r, bob, suit, "alt=1");
        Assert.True(feed.Any("You adjust the suit back to normal."));
        r.In(() => Assert.Equal((float)NORMAL_STYLE, suit.adjusted));

        // the right click opens the sensor modes
        RightClick(r, bob, suit);
        ActIn(r, bob, "ListInputWindow", "submit", ("entry", "Tracking beacon"));
        r.In(() => Assert.Equal((float)SENSOR_COORDS, suit.sensor_mode));
        Assert.True(feed.Any("as well as your coordinate position"));

        // a jumpsuit without sensors takes a suit sensor (five seconds), with its mode
        var bare = Place<obj_item_clothing_under_color_grey>(r, 5, 4);
        r.In(() => bare.set_has_sensor(NO_SENSORS));
        var sensor = Hold<obj_item_suit_sensor>(r, bob);
        r.In(() => sensor.set_mode(SENSOR_VITALS));
        Use(r, bob, bare);
        Assert.True(feed.Any("balloon: installing sensors..."));
        r.Step(5 * Second + 5);
        Assert.True(feed.Any("balloon: sensors installed"));
        Assert.True(Heard(r, "sparks/sparks4"), r.Sounds.Dump());
        r.In(() =>
        {
            Assert.Equal((float)HAS_SENSORS, bare.has_sensor);
            Assert.Equal((float)SENSOR_VITALS, bare.sensor_mode);
            Assert.True(QDELETED(sensor));
        });

        // broken sensors are mended with a length of cable
        r.In(() => bare.set_has_sensor(BROKEN_SENSORS));
        var cable = Hold<obj_item_stack_cable_coil>(r, bob);
        float cableBefore = r.In(() => Dm.num(cable.amount));
        Use(r, bob, bare);
        Assert.True(feed.Any("repairs the suit sensors on"));
        r.In(() =>
        {
            Assert.Equal((float)HAS_SENSORS, bare.has_sensor);
            Assert.Equal(cableBefore - 1, Dm.num(cable.amount));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheShoesQuackDryTheFloorAndGoUnderTheUniform()
    {
        var (r, feed, bob) = Room(9706);
        r.In(() => Waiting.NoSleep(bob.Move(locate(2, 4, 1), WEST)));
        r.Step(Second);

        // the ducky shoes: a waddle, and quacks on a walk (the squeak's steps counted)
        var ducky = Wear<obj_item_clothing_shoes_ducky_shoes>(r, bob, ITEM_SLOT_FEET);
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_WADDLING)));
        for (int x = 3; x <= 9; x++)
            Walk(r, bob, x, 4, EAST);
        Assert.True(Heard(r, "effects/quack"), r.Sounds.Dump());

        // the alt right click wears them under the uniform, and over it again
        RightClick(r, bob, ducky, "alt=1");
        Assert.True(feed.Any("balloon: wearing under uniforms"));
        r.In(() => Assert.Equal((float)UNDER_UNIFORM_LAYER, ducky.alternate_worn_layer));
        RightClick(r, bob, ducky, "alt=1");
        Assert.True(feed.Any("balloon: wearing over uniforms"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(ducky)));
        r.Step(2);
        r.In(() => Assert.False(HAS_TRAIT(bob, TRAIT_WADDLING)));

        // the absorbent galoshes dry the water they step in
        Wear<obj_item_clothing_shoes_galoshes_dry>(r, bob, ITEM_SLOT_FEET);
        var wet = r.In(() => (turf_open)locate(10, 4, 1));
        r.In(() => Waiting.NoSleep(wet.MakeSlippery(TURF_WET_WATER, min_wet_time: 60 * Second)));
        r.Step(2);
        r.In(() => Assert.NotNull(wet.GetComponent(typeof(datum_component_wet_floor))));
        Walk(r, bob, 10, 4, EAST);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(wet, bob.loc);
            var floor = wet.GetComponent(typeof(datum_component_wet_floor)) as datum_component_wet_floor;
            Assert.True(floor == null || QDELETED(floor), "the floor is still wet");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCostumesDisguiseTheirWearerToRobotsAndBefriendTheBears()
    {
        var (r, _, bob) = Room(9707);

        // the cardboard borg: helmet and suit together, the wearer looks like a robot to silicons; the helmet off ends it
        var borgHead = Wear<obj_item_clothing_head_costume_cardborg>(r, bob, ITEM_SLOT_HEAD);
        var borgSuit = Wear<obj_item_clothing_suit_costume_cardborg>(r, bob, ITEM_SLOT_OCLOTHING);
        r.In(() =>
        {
            Assert.True(Dm.truthy(borgSuit.in_use));
            Assert.IsType<datum_atom_hud_alternate_appearance_basic_silicons>(bob.alternate_appearances?["standard_borg_disguise"]);
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(borgHead)));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(borgSuit.in_use)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(borgSuit)));
        r.Step(2);

        // the bear suit with its pelt: the bears' faction, lost with the pelt
        var pelt = Wear<obj_item_clothing_head_costume_bearpelt>(r, bob, ITEM_SLOT_HEAD);
        var bear = Wear<obj_item_clothing_suit_costume_bear_suit>(r, bob, ITEM_SLOT_OCLOTHING);
        r.In(() =>
        {
            Assert.True(Dm.truthy(bear.full_suit));
            Assert.True(Dm.@in(FACTION_BEAR, bob.faction));
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(pelt)));
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(bear.full_suit));
            Assert.False(Dm.@in(FACTION_BEAR, bob.faction));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHatsBadgesVestsAndShirtsDoWhatTheirOwnProcsSay()
    {
        var (r, feed, bob) = Room(9708);

        // a collectable hat names its series
        var hat = Place<obj_item_clothing_head_collectable_hos>(r, 5, 4);
        string examined = Examine(r, hat, bob);
        Assert.Contains("Super duper collectable hats", examined, StringComparison.Ordinal);
        Assert.Contains("Collect them all:", examined, StringComparison.Ordinal);

        // the tinfoil hat fills the head with conspiracies, gone with the hat
        var foil = Wear<obj_item_clothing_head_costume_foilhat>(r, bob, ITEM_SLOT_HEAD);
        Assert.True(feed.Any("As you don the foiled hat"));
        r.In(() => Assert.True(Dm.truthy(bob.has_trauma_type(typeof(datum_brain_trauma_mild_phobia_conspiracies)))));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(foil)));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(bob.has_trauma_type(typeof(datum_brain_trauma_mild_phobia_conspiracies)))));

        // an allergy dogtag pinned on a jumpsuit is read with it up close
        var suit = Place<obj_item_clothing_under_color_grey>(r, 5, 4);
        var tag = r.In(() => (obj_item_clothing_accessory_dogtag_allergy)Dm.@new(typeof(obj_item_clothing_accessory_dogtag_allergy), bob.loc, "peanuts"));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(tag)));
        Use(r, bob, suit);
        r.In(() => Assert.Same(suit, tag.loc));
        string suitExamined = Examine(r, suit, bob);
        Assert.Contains("A dogtag is attached to", suitExamined, StringComparison.Ordinal);
        Assert.Contains("listing of allergies: peanuts", suitExamined, StringComparison.Ordinal);

        // a balloon vest pops on a sharp hit
        var vest = Wear<obj_item_clothing_suit_armor_balloon_vest>(r, bob, ITEM_SLOT_OCLOTHING);
        var knife = Place<obj_item_knife_kitchen>(r, 3, 4);
        r.In(() => Waiting.NoSleep(vest.hit_reaction(bob, knife)));
        r.Step(2);
        r.In(() => Assert.True(QDELETED(vest)));
        Assert.True(Heard(r, "cartoon_pop"), r.Sounds.Dump());

        // a well-worn shirt shrinks in the wash, six times, and implodes on the seventh
        var shirt = Place<obj_item_clothing_suit_costume_wellworn_shirt>(r, 6, 6);
        var washer = Place<obj_machinery_washing_machine>(r, 7, 6);
        r.In(() => Waiting.NoSleep(shirt.machine_wash(washer)));
        r.In(() =>
        {
            Assert.Equal(1f, shirt.wash_count);
            Assert.InRange(((matrix)shirt.transform).a, 0.89f, 0.92f);
        });
        Assert.True(feed.Any("appears to have shrunken after being washed."));
        for (int i = 0; i < 6; i++)
            r.In(() => Waiting.NoSleep(shirt.machine_wash(washer)));
        r.In(() => Assert.True(QDELETED(shirt)));
        Assert.True(feed.Any("implodes due to repeated washing."));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClothesHaveTheirOwnSuicides()
    {
        var (r, feed, bob) = Room(9709);

        // the suicide verb asks the held item (client/verbs/suicide.dm): the gloves strangle, the breath mask's tube too, the mesons stab
        var gloves = Hold<obj_item_clothing_gloves_color_black>(r, bob);
        r.In(() => Assert.Equal((object)OXYLOSS, Waiting.NoSleep(gloves.suicide_act(bob))));
        Assert.True(feed.Any("are forcing") && feed.Any("hands around"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gloves)));
        var mask = Hold<obj_item_clothing_mask_breath>(r, bob);
        r.In(() => Assert.Equal((object)OXYLOSS, Waiting.NoSleep(mask.suicide_act(bob))));
        Assert.True(feed.Any("tube around"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mask)));
        var mesons = Hold<obj_item_clothing_glasses_meson>(r, bob);
        r.In(() => Assert.Equal((object)BRUTELOSS, Waiting.NoSleep(mesons.suicide_act(bob))));
        Assert.True(feed.Any("overloading the brightness"));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mesons)));
        var sci = Hold<obj_item_clothing_glasses_science>(r, bob);
        r.In(() => Assert.Equal((object)OXYLOSS, Waiting.NoSleep(sci.suicide_act(bob))));
        Assert.True(feed.Any("straps around"));
        r.AssertNoRuntimes();
    }
}
