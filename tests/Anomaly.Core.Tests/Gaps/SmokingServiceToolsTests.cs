using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package J3 (D298): smoking (cigarettes.dm, lighter.dm, fancy.dm's packs, ignites_matches.dm) and the service tools (crayons.dm, laserpointer.dm, sharpener.dm, charter.dm and the
/// rest) in a booted toy room: a zippo lights a cigarette in the mouth, it burns down, feeds its reagents to the smoker and leaves its butt; a pack gives a cigarette; a match lights on a
/// boot; a crayon draws on the floor; a laser pointer blinds; a whetstone sharpens; the charter renames the station; every type spawns live and is destroyed with no runtimes.
/// </summary>
public class SmokingServiceToolsTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_match), typeof(obj_item_match_firebrand), typeof(obj_item_match_battery), typeof(obj_item_storage_box_matches),
        typeof(obj_item_cigarette), typeof(obj_item_cigarette_space_cigarette), typeof(obj_item_cigarette_dromedary), typeof(obj_item_cigarette_uplift), typeof(obj_item_cigarette_robust),
        typeof(obj_item_cigarette_greytide), typeof(obj_item_cigarette_robustgold), typeof(obj_item_cigarette_carp), typeof(obj_item_cigarette_syndicate), typeof(obj_item_cigarette_shadyjims),
        typeof(obj_item_cigarette_xeno), typeof(obj_item_cigarette_flash_powder), typeof(obj_item_cigarette_rollie), typeof(obj_item_cigarette_rollie_nicotine), typeof(obj_item_cigarette_rollie_trippy),
        typeof(obj_item_cigarette_rollie_cannabis), typeof(obj_item_cigarette_rollie_mindbreaker), typeof(obj_item_cigarette_candy), typeof(obj_item_cigarette_candy_nicotine), typeof(obj_item_cigarette_dart),
        typeof(obj_item_cigarette_cigar), typeof(obj_item_cigarette_cigar_premium), typeof(obj_item_cigarette_cigar_cohiba), typeof(obj_item_cigarette_cigar_havana),
        typeof(obj_item_cigarette_pipe), typeof(obj_item_cigarette_pipe_cobpipe), typeof(obj_item_cigarette_pipe_crackpipe),
        typeof(obj_item_cigbutt), typeof(obj_item_cigbutt_roach), typeof(obj_item_cigbutt_greycigbutt), typeof(obj_item_cigbutt_cigarbutt), typeof(obj_item_cigbutt_cigarbutt_cohiba), typeof(obj_item_cigbutt_cigarbutt_havana),
        typeof(obj_item_rollingpaper), typeof(obj_item_storage_fancy_rollingpapers), typeof(obj_item_storage_fancy_rollingpapers_empty),
        typeof(obj_item_storage_fancy_cigarettes), typeof(obj_item_storage_fancy_cigarettes_empty), typeof(obj_item_storage_fancy_cigarettes_dromedaryco), typeof(obj_item_storage_fancy_cigarettes_cigpack_uplift),
        typeof(obj_item_storage_fancy_cigarettes_cigpack_robust), typeof(obj_item_storage_fancy_cigarettes_cigpack_robustgold), typeof(obj_item_storage_fancy_cigarettes_cigpack_greytide),
        typeof(obj_item_storage_fancy_cigarettes_cigpack_carp), typeof(obj_item_storage_fancy_cigarettes_cigpack_syndicate), typeof(obj_item_storage_fancy_cigarettes_cigpack_midori),
        typeof(obj_item_storage_fancy_cigarettes_cigpack_candy), typeof(obj_item_storage_fancy_cigarettes_cigpack_shadyjims), typeof(obj_item_storage_fancy_cigarettes_cigpack_xeno),
        typeof(obj_item_storage_fancy_cigarettes_cigpack_cannabis), typeof(obj_item_storage_fancy_cigarettes_cigpack_mindbreaker), typeof(obj_item_storage_fancy_cigarettes_flash_powder),
        typeof(obj_item_storage_fancy_cigarettes_cigars), typeof(obj_item_storage_fancy_cigarettes_cigars_cohiba), typeof(obj_item_storage_fancy_cigarettes_cigars_havana),
        typeof(obj_item_lighter), typeof(obj_item_lighter_greyscale), typeof(obj_item_lighter_greyscale_empty), typeof(obj_item_lighter_slime), typeof(obj_item_lighter_skull), typeof(obj_item_lighter_mime), typeof(obj_item_lighter_bright), typeof(obj_item_lighter_empty),
        typeof(obj_item_access_key), typeof(obj_item_sharpener), typeof(obj_item_sharpener_super),
        typeof(obj_item_toy_crayon), typeof(obj_item_toy_crayon_red), typeof(obj_item_toy_crayon_orange), typeof(obj_item_toy_crayon_yellow), typeof(obj_item_toy_crayon_green), typeof(obj_item_toy_crayon_blue),
        typeof(obj_item_toy_crayon_purple), typeof(obj_item_toy_crayon_black), typeof(obj_item_toy_crayon_white), typeof(obj_item_toy_crayon_mime), typeof(obj_item_toy_crayon_rainbow),
        typeof(obj_item_toy_crayon_spraycan), typeof(obj_item_toy_crayon_spraycan_hellcan), typeof(obj_item_toy_crayon_spraycan_lubecan), typeof(obj_item_toy_crayon_spraycan_mimecan), typeof(obj_item_toy_crayon_spraycan_infinite), typeof(obj_item_toy_crayon_spraycan_roboticist),
        typeof(obj_item_storage_crayons), typeof(obj_item_toy_balloon), typeof(obj_item_toy_balloon_long), typeof(obj_item_storage_box_balloons),
        typeof(obj_item_laser_pointer), typeof(obj_item_laser_pointer_red), typeof(obj_item_laser_pointer_green), typeof(obj_item_laser_pointer_blue), typeof(obj_item_laser_pointer_purple), typeof(obj_item_laser_pointer_upgraded), typeof(obj_item_laser_pointer_infinite_range),
        typeof(obj_item_key_displaycase), typeof(obj_item_station_charter), typeof(obj_item_station_charter_admin), typeof(obj_item_station_charter_banner), typeof(obj_item_choice_beacon_hero),
        typeof(obj_item_storage_briefcase), typeof(obj_item_storage_briefcase_lawyer), typeof(obj_item_reagent_containers_cup_glass_bottle_bottleofnothing), typeof(obj_item_broken_bottle),
        typeof(obj_item_camera), typeof(obj_item_camera_spooky), typeof(obj_item_camera_detective), typeof(obj_item_photo), typeof(obj_item_photo_old), typeof(obj_item_barcodescanner), typeof(obj_item_scythe), typeof(obj_item_plant_analyzer),
    };

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Make<T>(AudioRoom r, atom where) where T : obj_item
    {
        T thing = r.In(() => Dm.@new<T>(where));
        r.Step(2);
        return thing;
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T thing = Make<T>(r, who.loc as atom);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(thing)))));
        return thing;
    }

    private static T Wear<T>(AudioRoom r, mob_living_carbon_human who, object slot) where T : obj_item
    {
        T thing = Make<T>(r, who.loc as atom);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(thing, slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))));
        r.Step(2);
        return thing;
    }

    // the toy room's turfs are no floors (isfloorturf)
    private static turf PlainFloor(AudioRoom r, int x, int y)
    {
        r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron))));
        r.Step(2);
        return r.In(() => locate(x, y, 1));
    }

    private static List<string> OverlayStates(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void EveryTypeSpawnsLiveAndIsDestroyedWithNoRuntimes()
    {
        var (r, _, bob) = Room(9901);
        var failures = new List<string>();
        foreach (Type type in AllTypes)
        {
            obj_item item = r.In(() => (obj_item)Dm.@new(type, bob.loc));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(item)))
                failures.Add(type.Name + " is inert");
            r.In(() => Waiting.NoSleep(item.examine(bob)));
            r.In(() => qdel(item));
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AZippoLightsACigaretteInTheMouthThatBurnsDownFeedsItsReagentsAndLeavesItsButt()
    {
        var (r, feed, bob) = Room(9902);
        obj_item_lighter zippo = Hold<obj_item_lighter>(r, bob);
        obj_item_cigarette_robust cig = Wear<obj_item_cigarette_robust>(r, bob, ITEM_SLOT_MASK);
        Assert.Equal("cigoff", r.In(() => cig.icon_state));

        // an unlit zippo lights nothing
        Assert.Equal(NONE, r.In(() => Waiting.NoSleep(cig.attempt_light(bob, zippo))));
        Assert.False(r.In(() => Dm.truthy(cig.lit)));

        // flip it open
        r.In(() => Waiting.NoSleep(zippo.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(zippo.lit)));
        Assert.Equal("zippo-on", r.In(() => zippo.icon_state));
        Assert.True(r.In(() => Dm.truthy(zippo.light_on)));
        Assert.Equal(heat_of_lit_zippo, r.In(() => Dm.num(zippo.get_temperature())));
        Assert.Contains("lighter_overlay_" + r.In(() => Dm.str(zippo.overlay_state)) + "-on", OverlayStates(r, zippo));

        // its flame lights the cigarette in the mouth (lighter.attack, the help_light_cig path)
        r.In(() => Waiting.NoSleep(zippo.attack__proc(bob, bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(cig.lit)));
        Assert.Equal("cigon", r.In(() => cig.icon_state));
        Assert.True(r.In(() => Dm.truthy(cig.light_on)));
        Assert.Equal(BURN, r.In(() => Dm.str(cig.damtype)));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("cig_light") == true);

        // it burns down and the nicotine reaches the smoker, in drags
        datum_reagents holder = r.In(() => (datum_reagents)cig.reagents);
        float before_time = r.In(() => cig.smoketime);
        float before_volume = r.In(() => holder.total_volume);
        r.Step(25 * Second);
        Assert.True(r.In(() => cig.smoketime) < before_time);
        Assert.True(r.In(() => holder.total_volume) < before_volume);
        Assert.True(r.In(() => Dm.num(((datum_reagents)bob.reagents).get_reagent_amount(typeof(datum_reagent_drug_nicotine)))) > 0, "the smoker has no nicotine");

        // the last of it: the butt on the floor, and a chat line
        r.In(() => cig.smoketime = 1);
        r.Step(4 * Second);
        Assert.True(r.In(() => QDELETED(cig)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_cigbutt>(bob.loc)));
        Assert.True(feed.Any("goes out"));

        // shut the zippo again
        r.In(() => Waiting.NoSleep(zippo.attack_self(bob)));
        Assert.False(r.In(() => Dm.truthy(zippo.lit)));
        Assert.Equal("zippo", r.In(() => zippo.icon_state));
        r.AssertNoRuntimes();
    }

    private const float heat_of_lit_zippo = 1400; // HIGH_TEMPERATURE_REQUIRED - 100

    [Fact]
    public void ACheapLighterBurnsAThumbAndRunsOutOfFuel()
    {
        var (r, feed, bob) = Room(9903);
        obj_item_lighter_greyscale lighter = Hold<obj_item_lighter_greyscale>(r, bob);
        bool burnt = false;
        for (int i = 0; i < 40 && !burnt; i++)
        {
            r.In(() => Waiting.NoSleep(lighter.attack_self(bob)));
            burnt = feed.Any("burn yourself");
            r.In(() => Waiting.NoSleep(lighter.attack_self(bob)));
        }
        Assert.True(burnt, "a cheap lighter never burnt a bare hand in 40 tries");

        // the fuel runs out and the flame dies
        r.In(() => Waiting.NoSleep(lighter.attack_self(bob)));
        Assert.True(r.In(() => Dm.truthy(lighter.lit)));
        r.In(() => Waiting.NoSleep(((datum_reagents)lighter.reagents).remove_reagent(typeof(datum_reagent_fuel), 3)));
        r.In(() => Waiting.NoSleep(lighter.use(0)));
        Assert.False(r.In(() => Dm.truthy(lighter.lit)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APackGivesACigaretteToTheHandAndTheMouthAndTearsItsCouponOffWhenEmpty()
    {
        var (r, feed, bob) = Room(9904);
        obj_item_storage_fancy_cigarettes pack = Hold<obj_item_storage_fancy_cigarettes>(r, bob);
        Assert.Equal(6, r.In(() => Dm.length(pack.contents)));
        Assert.Equal("cig", r.In(() => pack.icon_state));
        Assert.DoesNotContain("cig_open", OverlayStates(r, pack));

        // the open pack shows its cigarettes
        r.In(() => Waiting.NoSleep(pack.attack_self(bob)));
        r.Step(2);
        Assert.Contains("cig_open", OverlayStates(r, pack));
        Assert.Contains("cigarette_6", OverlayStates(r, pack));

        // a click on yourself: one in the mouth
        r.In(() => Waiting.NoSleep(pack.interact_with_atom(bob, bob)));
        r.Step(2);
        Assert.IsType<obj_item_cigarette_space_cigarette>(r.In(() => bob.wear_mask));
        Assert.Equal(5, r.In(() => Dm.length(pack.contents)));

        // the right click: one in the free hand
        r.In(() => Waiting.NoSleep(pack.attack_hand_secondary(bob)));
        r.Step(2);
        Assert.Equal(4, r.In(() => Dm.length(pack.contents)));
        Assert.NotNull(r.In(() => bob.get_inactive_held_item() as obj_item_cigarette));
        Assert.DoesNotContain("cigarette_5", OverlayStates(r, pack));

        // emptied: the coupon comes off and the pack holds no more
        r.In(() =>
        {
            foreach (obj_item_cigarette cig in Dm.each<obj_item_cigarette>(pack.contents))
                qdel(cig);
        });
        r.Step(2);
        Assert.Equal("cig_empty", r.In(() => pack.icon_state));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(bob.get_inactive_held_item() as obj_item)));
        r.In(() => Waiting.NoSleep(pack.attack_self(bob)));
        r.Step(2);
        Assert.Equal("discarded cigarette packet", r.In(() => pack.name));
        Assert.NotNull(r.In(() => bob.get_inactive_held_item() as obj_item_coupon));
        Assert.Equal(0f, r.In(() => pack.atom_storage.max_slots));
        Assert.True(feed.Any("free coupon"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMatchLightsOnABootBurnsOutAndLightsACigar()
    {
        var (r, feed, bob) = Room(9905);
        obj_item_clothing_shoes_jackboots boots = Wear<obj_item_clothing_shoes_jackboots>(r, bob, ITEM_SLOT_FEET);
        obj_item_match match = null;
        bool lit = false;
        for (int i = 0; i < 12 && !lit; i++)
        {
            match = Hold<obj_item_match>(r, bob);
            Click(r, bob, boots);
            r.Step(Second);
            lit = r.In(() => Dm.truthy(match.lit));
            if (!lit)
                r.In(() => Waiting.NoSleep(bob.dropItemToGround(match)));
        }
        Assert.True(lit, "no match lit on the boot");
        Assert.True(feed.Any("igniting it"));
        Assert.Equal("match_lit", r.In(() => match.icon_state));
        Assert.Equal("lit match", r.In(() => match.name));

        // a lit match lights a cigar in the mouth
        obj_item_cigarette_cigar cigar = Wear<obj_item_cigarette_cigar>(r, bob, ITEM_SLOT_MASK);
        r.In(() => Waiting.NoSleep(match.attack__proc(bob, bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(cigar.lit)));
        Assert.Equal("cigaron", r.In(() => cigar.icon_state));

        // ten seconds later it has burnt out
        r.Step(11 * Second);
        Assert.True(r.In(() => Dm.truthy(match.burnt)));
        Assert.Equal("match_burnt", r.In(() => match.icon_state));

        // and the matchbox lights one too: the box's picture follows its count
        obj_item_storage_box_matches box = Make<obj_item_storage_box_matches>(r, bob.loc as atom);
        Assert.Equal(10, r.In(() => Dm.length(box.contents)));
        Assert.Equal("matchbox", r.In(() => box.icon_state));
        r.In(() => { Waiting.NoSleep(Dm.locate<obj_item_match>(box.contents).forceMove(bob.loc as atom)); });
        r.Step(2);
        Assert.Equal("matchbox_almostfull", r.In(() => box.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACrayonDrawsItsStencilOnTheFloorAndItsWindowChoosesTheStencil()
    {
        var (r, feed, bob) = Room(9906);
        obj_item_toy_crayon_blue crayon = Hold<obj_item_toy_crayon_blue>(r, bob);

        // the crayon's window: tg's stencils, sent to the client; a click picks one
        r.In(() => Waiting.NoSleep(crayon.ui_interact(bob)));
        System.Text.Json.JsonElement data = Data(r, bob, out string window);
        Assert.Equal("Graffiti", data.GetProperty("drawables")[0].GetProperty("name").GetString());
        Act(r, bob, window, "select_stencil", ("item", "rune3"));
        Assert.Equal("rune3", r.In(() => Dm.str(crayon.drawtype)));
        r.In(() => SStgui.close_uis(crayon));

        float charges_before = r.In(() => Dm.num(crayon.charges_left));
        turf floor = PlainFloor(r, 5, 4);
        Click(r, bob, floor);
        Assert.True(feed.Any("You start drawing a rune"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Null(r.In(() => Dm.locate<obj_effect_decal_cleanable_crayon>(floor)));
        r.Step(6 * Second);
        obj_effect_decal_cleanable_crayon rune = r.In(() => Dm.locate<obj_effect_decal_cleanable_crayon>(floor));
        Assert.NotNull(rune);
        Assert.Equal("rune3", r.In(() => rune.icon_state));
        Assert.Equal(r.In(() => crayon.paint_color), r.In(() => rune.paint_colour));
        Assert.True(r.In(() => Dm.num(crayon.charges_left)) < charges_before);
        Assert.True(feed.Any("You finish drawing"));

        // a spray can is instant and needs its cap off
        obj_item_toy_crayon_spraycan can = Make<obj_item_toy_crayon_spraycan>(r, bob.loc as atom);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(crayon)));
        r.In(() => Waiting.NoSleep(bob.put_in_hands(can)));
        r.Step(Second);
        turf other = PlainFloor(r, 5, 5);
        r.In(() => Waiting.NoSleep(can.interact_with_atom(other, bob)));
        Assert.Null(r.In(() => Dm.locate<obj_effect_decal_cleanable_crayon>(other)));
        r.In(() => Waiting.NoSleep(can.click_alt(bob)));
        Assert.Equal("spraycan", r.In(() => can.icon_state));
        r.In(() => { can.drawtype = "star"; Waiting.NoSleep(can.interact_with_atom(other, bob)); });
        r.Step(2);
        Assert.Equal("star", r.In(() => Dm.locate<obj_effect_decal_cleanable_crayon>(other)?.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALaserPointerBlindsThroughTheEyesAndDrainsItsCharge()
    {
        var (r, feed, bob) = Room(9907);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);
        obj_item_laser_pointer_red pointer = Hold<obj_item_laser_pointer_red>(r, bob);
        r.In(() => { pointer.effectchance = 100; bob.zone_selected = BODY_ZONE_PRECISE_EYES; });

        bool blinded = false;
        for (int i = 0; i < 8 && !blinded; i++)
        {
            r.In(() => Waiting.NoSleep(pointer.laser_act(alice, bob)));
            r.Step(2);
            blinded = feed.Any("You blind");
        }
        Assert.True(blinded, "the pointer never blinded");
        Assert.True(r.In(() => pointer.energy) < 10);
        Assert.Equal("pointer", r.In(() => pointer.icon_state));

        // the diode comes out and it does nothing
        obj_item_screwdriver driver = Make<obj_item_screwdriver>(r, bob.loc as atom);
        r.In(() => Waiting.NoSleep(pointer.screwdriver_act(bob, driver)));
        Assert.Null(r.In(() => pointer.diode));
        r.In(() => Waiting.NoSleep(pointer.laser_act(alice, bob)));
        Assert.True(feed.Any("but nothing happens"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWhetstoneSharpensOnceAndWearsOut()
    {
        var (r, feed, bob) = Room(9908);
        obj_item_sharpener stone = Make<obj_item_sharpener>(r, bob.loc as atom);
        obj_item_knife_kitchen knife = Hold<obj_item_knife_kitchen>(r, bob);
        float force = r.In(() => knife.force);
        r.In(() => Waiting.NoSleep(stone.item_interaction(bob, knife)));
        Assert.Equal(force + 4, r.In(() => knife.force));
        Assert.StartsWith("sharpened", r.In(() => knife.name));
        Assert.Equal(0f, r.In(() => stone.uses));
        Assert.StartsWith("worn out", r.In(() => stone.name));
        r.In(() => Waiting.NoSleep(stone.item_interaction(bob, knife)));
        Assert.True(feed.Any("too worn"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCharterRenamesTheStationOnce()
    {
        var (r, feed, bob) = Room(9909);
        obj_item_station_charter charter = Hold<obj_item_station_charter>(r, bob);
        r.Step(2);

        // the standard format is approved at once
        r.In(() => Waiting.Detach(charter.attack_self(bob)));
        r.Step(2);
        LastUpdate(Take(bob), out string window);
        Act(r, bob, window, "submit", ("entry", "Aegis Station 7"));
        r.Step(Second);
        Assert.True(feed.Any("automatically approved"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal("Aegis Station 7", r.In(() => Dm.str(station_name())));
        Assert.Equal("station charter for Aegis Station 7", r.In(() => charter.name));
        Assert.True(r.In(() => Dm.truthy(charter.used)));

        // and only once
        r.In(() => Waiting.NoSleep(charter.attack_self(bob)));
        Assert.True(feed.Any("has already been named"));

        // another name waits for the employers, and is approved by the timer
        obj_item_station_charter_admin admin = Make<obj_item_station_charter_admin>(r, bob.loc as atom);
        r.In(() => { admin.approval_time = 5; Waiting.NoSleep(bob.dropItemToGround(charter)); Waiting.NoSleep(bob.put_in_hands(admin)); });
        r.In(() => Waiting.Detach(admin.attack_self(bob)));
        r.Step(2);
        LastUpdate(Take(bob), out string second);
        Act(r, bob, second, "submit", ("entry", "Bobs Own"));
        r.Step(2);
        Assert.True(feed.Any("sent to your employers"));
        r.Step(Second);
        Assert.Equal("Bobs Own", r.In(() => Dm.str(station_name())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAccessKeyTakesItsDepartmentFromTheSignal()
    {
        var (r, feed, bob) = Room(9910);
        obj_item_access_key key = Hold<obj_item_access_key>(r, bob);
        r.In(() =>
        {
            Assert.True(Dm.@in(key, GLOB.janitor_devices));
            SSdcs._SendSignal(COMSIG_ON_DEPARTMENT_ACCESS, Dm.list(SSdcs, Dm.list(REGION_ENGINEERING)));
        });
        Assert.Equal(REGION_ENGINEERING, r.In(() => Dm.str(key.department_access_value)));
        Assert.True(r.In(() => Dm.truthy(Dm.@in("It currently holds access to the " + REGION_ENGINEERING + " region.", Waiting.NoSleep(key.examine(bob)) as DmList))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACameraTakesAPhotoOfWhatItSees()
    {
        var (r, feed, bob) = Room(9911);
        mob_living_carbon_human alice = r.Join(6, 4, "Alice");
        r.Step(Second);
        obj_item_camera camera = Hold<obj_item_camera>(r, bob);
        r.In(() => camera.can_customise = FALSE);
        float film = r.In(() => camera.pictures_left);
        Assert.True(r.In(() => Dm.truthy(camera.attempt_picture(alice, bob))));
        r.Step(Second);
        Assert.Equal(film - 1, r.In(() => camera.pictures_left));
        obj_item_photo photo = r.In(() => bob.get_inactive_held_item() as obj_item_photo);
        Assert.NotNull(photo);
        Assert.Contains("meters by", r.In(() => photo.desc));
        Assert.Equal("camera_off", r.In(() => camera.icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("polaroid") == true);
        // the flash charges back
        r.Step(8 * Second);
        Assert.Equal("camera", r.In(() => camera.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABottleOfNothingHoldsItsReagentAndSmashes()
    {
        var (r, feed, bob) = Room(9912);
        obj_item_reagent_containers_cup_glass_bottle_bottleofnothing bottle = Hold<obj_item_reagent_containers_cup_glass_bottle_bottleofnothing>(r, bob);
        Assert.Equal(100f, r.In(() => Dm.num(((datum_reagents)bottle.reagents).get_reagent_amount(typeof(datum_reagent_consumable_nothing)))));
        turf t = r.In(() => (turf)bob.loc);
        r.In(() => Waiting.NoSleep(bottle.smash(t, bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(bottle)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_broken_bottle>(bob.contents)));
        r.AssertNoRuntimes();
    }
}
