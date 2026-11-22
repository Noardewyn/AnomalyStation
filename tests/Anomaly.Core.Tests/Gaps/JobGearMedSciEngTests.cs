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
/// Gaps package J5 (D298): the job gear of medical, science and engineering in a booted toy room: a body bag folds out, takes a body, zips and folds back; the power tools work as
/// the two tools they are; the experimental welder refills itself; the crew pinpointer and monitor read suit sensors; the surgical tools' behaviours and sounds; every type spawns live.
/// </summary>
public class JobGearMedSciEngTests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_retractor), typeof(obj_item_retractor_advanced), typeof(obj_item_hemostat), typeof(obj_item_cautery), typeof(obj_item_cautery_advanced),
        typeof(obj_item_surgicaldrill), typeof(obj_item_scalpel), typeof(obj_item_scalpel_advanced), typeof(obj_item_circular_saw), typeof(obj_item_surgical_drapes),
        typeof(obj_item_bonesetter), typeof(obj_item_blood_filter), typeof(obj_item_shears),
        typeof(obj_item_bodybag), typeof(obj_item_bodybag_bluespace), typeof(obj_item_bodybag_environmental), typeof(obj_item_bodybag_environmental_prisoner), typeof(obj_item_bodybag_stasis),
        typeof(obj_item_autopsy_scanner), typeof(obj_item_sensor_device), typeof(obj_item_pinpointer), typeof(obj_item_pinpointer_crew), typeof(obj_item_pinpointer_pair), typeof(obj_item_suit_sensor),
        typeof(obj_item_sequence_scanner), typeof(obj_item_screwdriver_power), typeof(obj_item_crowbar_power), typeof(obj_item_weldingtool_experimental),
        typeof(obj_item_door_remote_chief_engineer), typeof(obj_item_door_remote_chief_medical_officer), typeof(obj_item_door_remote_research_director),
        typeof(obj_item_megaphone_command), typeof(obj_item_electropack), typeof(obj_item_reagent_containers_medigel), typeof(obj_item_reagent_containers_medigel_sterilizine),
        typeof(obj_item_ph_booklet), typeof(obj_item_ph_paper), typeof(obj_item_watertank), typeof(obj_item_watertank_atmos), typeof(obj_item_watertank_janitor),
        typeof(obj_item_autosurgeon), typeof(obj_item_pet_carrier), typeof(obj_item_storage_lockbox_medal_med), typeof(obj_item_storage_lockbox_medal_engineering), typeof(obj_item_storage_lockbox_medal_sci),
        typeof(obj_item_storage_bag_garment_chief_medical), typeof(obj_item_storage_bag_garment_engineering_chief), typeof(obj_item_storage_bag_garment_research_director),
        typeof(obj_item_storage_box_medigels), typeof(obj_item_storage_box_emptysandbags), typeof(obj_item_storage_box_skillchips_engineering), typeof(obj_item_storage_box_skillchips_science),
        typeof(obj_item_storage_box_stickers_chief_engineer), typeof(obj_item_sticker_smile), typeof(obj_item_emptysandbag), typeof(obj_item_stack_sheet_mineral_sandbags),
        typeof(obj_item_disk_computer_command_ce), typeof(obj_item_disk_computer_command_cmo), typeof(obj_item_disk_computer_command_rd), typeof(obj_item_stamp_head_ce), typeof(obj_item_stamp_head_cmo), typeof(obj_item_stamp_head_rd),
        typeof(obj_item_clothing_ears_earmuffs), typeof(obj_item_clothing_glasses_blindfold), typeof(obj_item_clothing_glasses_regular), typeof(obj_item_clothing_gloves_atmos), typeof(obj_item_clothing_gloves_chief_engineer),
        typeof(obj_item_clothing_head_bio_hood_cmo), typeof(obj_item_clothing_suit_bio_suit_cmo), typeof(obj_item_clothing_suit_bio_suit_scientist), typeof(obj_item_clothing_suit_jacket_straight_jacket),
        typeof(obj_item_clothing_suit_utility_fire_atmos), typeof(obj_item_clothing_suit_hazardvest), typeof(obj_item_clothing_head_utility_welding), typeof(obj_item_clothing_head_utility_hardhat_welding_atmos),
        typeof(obj_item_clothing_neck_cloak_ce), typeof(obj_item_clothing_neck_cloak_cmo), typeof(obj_item_clothing_neck_cloak_rd), typeof(obj_item_clothing_shoes_magboots_advance),
        typeof(obj_item_clothing_accessory_medal_plasma_nobel_science), typeof(obj_item_clothing_accessory_medal_silver_emergency_services_engineering), typeof(obj_item_clothing_mask_bandana_skull_black),
    };

    private static readonly Type[] Structures =
    {
        typeof(obj_structure_closet_body_bag), typeof(obj_structure_closet_body_bag_bluespace), typeof(obj_structure_closet_body_bag_environmental), typeof(obj_structure_closet_body_bag_environmental_nanotrasen),
        typeof(obj_structure_closet_body_bag_environmental_prisoner), typeof(obj_structure_closet_body_bag_environmental_stasis), typeof(obj_structure_closet_body_bag_environmental_hardlight),
        typeof(obj_structure_closet_secure_closet_medical3), typeof(obj_structure_closet_secure_closet_animal),
        typeof(obj_structure_closet_wardrobe_engineering_yellow), typeof(obj_structure_closet_wardrobe_atmospherics_yellow), typeof(obj_structure_closet_wardrobe_white_medical),
        typeof(obj_structure_closet_wardrobe_chemistry_white), typeof(obj_structure_closet_wardrobe_genetics_white), typeof(obj_structure_closet_wardrobe_virology_white),
        typeof(obj_structure_closet_wardrobe_science_white), typeof(obj_structure_closet_wardrobe_robotics_black),
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

    private static T Make<T>(AudioRoom r, atom where) where T : atom
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

    private static void Wear(AudioRoom r, mob_living_carbon_human who, obj_item thing, object slot)
    {
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_if_possible(thing, slot, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))));
        r.Step(2);
    }

    private static List<string> OverlayStates(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static void UseInHand(AudioRoom r, mob who, obj_item item)
    {
        r.Command(() => Waiting.Detach(item.attack_self(who)));
        r.Step(2);
    }

    [Fact]
    public void EveryTypeSpawnsLiveAndIsDestroyedWithNoRuntimes()
    {
        var (r, _, bob) = Room(9501);
        var failures = new List<string>();
        foreach (Type type in AllTypes.Concat(Structures))
        {
            atom thing = r.In(() => (atom)Dm.@new(type, bob.loc));
            r.Step(2);
            if (r.In(() => InertSeam.IsInert(thing)))
                failures.Add(type.Name + " is inert");
            r.In(() => Waiting.NoSleep(thing.examine(bob)));
            r.In(() => qdel(thing));
            r.Step(2);
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABodyBagFoldsOutTakesABodyZipsAndFoldsBack()
    {
        var (r, feed, bob) = Room(9502);
        mob_living_carbon_human ann = r.Join(5, 4, "Ann");
        r.Step(Second);
        obj_item_bodybag folded = Hold<obj_item_bodybag>(r, bob);

        // use in hand: it opens on the user's tile and the folded bag waits in nullspace
        UseInHand(r, bob, folded);
        obj_structure_closet_body_bag bag = r.In(() => Dm.each<obj_structure_closet_body_bag>(get_turf(bob)).Single());
        Assert.True(r.In(() => Dm.truthy(bag.opened)));
        Assert.Same(folded, r.In(() => bag.foldedbag_instance));
        Assert.Null(r.In(() => folded.loc));
        Assert.False(r.In(() => Dm.truthy(bag.density)));

        r.In(() => Waiting.NoSleep(bob.forceMove(locate(3, 4, 1)))); // or he is zipped in with the body
        r.Step(2);

        // unzipped it will not fold
        r.In(() => Assert.False(Dm.truthy(bag.attempt_fold(bob))));
        Assert.True(feed.Any("won't fold while unzipped"));

        // a body on the bag's tile is zipped in and lies floored there
        r.In(() => Waiting.NoSleep(ann.forceMove(bag.loc)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bag.close(bob)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(bag.opened)));
        Assert.False(r.In(() => Dm.truthy(bag.density)));
        Assert.Same(bag, r.In(() => ann.loc));
        Assert.True(r.In(() => HAS_TRAIT(ann, TRAIT_FLOORED)));
        r.In(() => Assert.False(Dm.truthy(bag.attempt_fold(bob))));
        Assert.True(feed.Any("too many things inside"));

        // the zip: open it, the body comes out free, close it empty and fold it: the same bag is back in the hand
        r.In(() => Waiting.NoSleep(bag.open(bob)));
        r.Step(2);
        Assert.False(r.In(() => HAS_TRAIT(ann, TRAIT_FLOORED)));
        r.In(() => Waiting.NoSleep(ann.forceMove(locate(6, 4, 1))));
        r.In(() => Waiting.NoSleep(bag.close(bob)));
        r.Step(2);
        r.Command(() => Waiting.Detach(bag.attack_hand_secondary(bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(bag)));
        Assert.Same(bob, r.In(() => folded.loc));
        Assert.Equal("bodybag_folded", r.In(() => folded.icon_state));

        // the bag's name tag and the zip's sound
        obj_structure_closet_body_bag tagged = Make<obj_structure_closet_body_bag>(r, bob.loc as atom);
        r.In(() => Waiting.NoSleep(tagged.nameformat("John")));
        r.Step(2);
        Assert.Contains("bodybag_label", OverlayStates(r, tagged));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("zip/zip") == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePowerToolsSwitchModeAndWorkAsTheTwoToolsTheyAre()
    {
        var (r, feed, bob) = Room(9503);
        obj_item_screwdriver_power drill = Hold<obj_item_screwdriver_power>(r, bob);
        Assert.Equal(TOOL_SCREWDRIVER, r.In(() => drill.tool_behaviour));
        UseInHand(r, bob, drill);
        Assert.Equal(TOOL_WRENCH, r.In(() => drill.tool_behaviour));
        Assert.True(feed.Any("attached bolt bit"));
        Assert.Contains(r.In(() => Dm.each(Waiting.NoSleep(drill.examine(bob)) as DmList).Select(Dm.str).ToList()), line => line.Contains("bolt bit"));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("change_drill") == true);
        UseInHand(r, bob, drill);
        Assert.Equal(TOOL_SCREWDRIVER, r.In(() => drill.tool_behaviour));

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(drill)));
        obj_item_crowbar_power jaws = Hold<obj_item_crowbar_power>(r, bob);
        Assert.Equal(TOOL_CROWBAR, r.In(() => jaws.tool_behaviour));
        Assert.True(r.In(() => Dm.truthy(jaws.force_opens)));
        UseInHand(r, bob, jaws);
        Assert.Equal(TOOL_WIRECUTTER, r.In(() => jaws.tool_behaviour));
        Assert.True(feed.Any("attached cutting"));
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("change_jaws") == true);
        UseInHand(r, bob, jaws);
        Assert.Equal(TOOL_CROWBAR, r.In(() => jaws.tool_behaviour));
        Assert.True(feed.Any("attached prying"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheExperimentalWelderRefillsItself()
    {
        var (r, _, bob) = Room(9504);
        obj_item_weldingtool_experimental welder = Hold<obj_item_weldingtool_experimental>(r, bob);
        Assert.Equal(40f, r.In(() => Dm.num(welder.get_fuel())));
        // lit once it keeps processing, on or off (can_off_process)
        UseInHand(r, bob, welder);
        Assert.True(r.In(() => Dm.truthy(welder.welding)));
        UseInHand(r, bob, welder);
        Assert.False(r.In(() => Dm.truthy(welder.welding)));
        r.In(() => Waiting.NoSleep(((datum_reagents)welder.reagents).remove_all(Dm.num(welder.get_fuel()) - 10)));
        Assert.Equal(10f, r.In(() => Dm.num(welder.get_fuel())));
        r.Step(10 * Second);
        float refilled = r.In(() => Dm.num(welder.get_fuel()));
        Assert.True(refilled > 10f && refilled <= 40f, "fuel " + refilled);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCrewPinpointerPointsAtACrewmanWithSensorsOnAndTheMonitorReadsThem()
    {
        var (r, _, bob) = Room(9505);
        mob_living_carbon_human ann = r.Join(9, 4, "Ann");
        r.Step(Second);
        obj_item_clothing_under uniform = Make<obj_item_clothing_under_color_grey>(r, ann.loc as atom);
        Wear(r, ann, uniform, ITEM_SLOT_ICLOTHING);
        r.In(() => uniform.set_sensor_mode(SENSOR_OFF));

        obj_item_pinpointer_crew pin = Hold<obj_item_pinpointer_crew>(r, bob);
        Assert.False(r.In(() => Dm.truthy(pin.trackable(ann))));
        r.In(() => uniform.set_sensor_mode(SENSOR_COORDS));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(pin.trackable(ann))));

        // on with a target: the sprite points east, at close range
        r.In(() =>
        {
            pin.target = ann;
            pin.toggle_on();
        });
        r.Step(Second);
        Assert.Contains("pinonclose", OverlayStates(r, pin));
        Assert.Equal(EAST, r.In(() => Dm.num(pin.dir)));

        // the monitor's row for her: alive with her vitals and area
        DmList rows = r.In(() => GLOB.crewmonitor.update_data(bob.z) as DmList);
        DmList row = r.In(() => Dm.each(rows).OfType<DmList>().Single(e => Dm.eq(e["ref"], REF(ann))));
        r.In(() =>
        {
            Assert.Equal(0f, Dm.num(row["brutedam"]));
            Assert.NotNull(row["area"]);
        });

        // sensors off: the pinpointer loses her and goes out
        r.In(() => uniform.set_sensor_mode(SENSOR_OFF));
        r.Step(Second);
        r.In(() => Assert.False(Dm.truthy(pin.active)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASurgicalToolHasItsBehaviourAndSoundAndStabsEyes()
    {
        var (r, feed, bob) = Room(9506);
        mob_living_carbon_human ann = r.Join(5, 4, "Ann");
        r.Step(Second);
        (Type Type, string Behaviour)[] tools =
        {
            (typeof(obj_item_retractor), TOOL_RETRACTOR), (typeof(obj_item_hemostat), TOOL_HEMOSTAT), (typeof(obj_item_cautery), TOOL_CAUTERY), (typeof(obj_item_surgicaldrill), TOOL_DRILL),
            (typeof(obj_item_scalpel), TOOL_SCALPEL), (typeof(obj_item_circular_saw), TOOL_SAW), (typeof(obj_item_bonesetter), TOOL_BONESET), (typeof(obj_item_blood_filter), TOOL_BLOODFILTER),
        };
        foreach ((Type type, string behaviour) in tools)
            Assert.Equal(behaviour, r.In(() => ((obj_item)Dm.@new(type, bob.loc)).tool_behaviour));

        obj_item_scalpel scalpel = Hold<obj_item_scalpel>(r, bob);
        Assert.Equal("scalpel_normal", r.In(() => scalpel.get_surgery_tool_overlay()));
        Assert.Contains("bladeslice", r.In(() => Dm.str(scalpel.hitsound)));
        Assert.NotNull(r.In(() => scalpel.GetComponent(typeof(datum_component_butchering))));
        Assert.NotNull(r.In(() => scalpel.GetComponent(typeof(datum_component_alternative_sharpness))));

        // the eyes: the eye stab hurts them
        r.In(() =>
        {
            bob.zone_selected = BODY_ZONE_PRECISE_EYES;
            bob.set_combat_mode(TRUE);
        });
        Click(r, bob, ann);
        r.Step(Second);
        Assert.True(feed.Any("in the eye with"), "no eye stab");
        obj_item_organ_eyes eyes = r.In(() => ann.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes);
        Assert.True(r.In(() => Dm.num(eyes.damage)) > 0);

        // the advanced retractor is both of its tools
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(scalpel)));
        obj_item_retractor_advanced adv = Hold<obj_item_retractor_advanced>(r, bob);
        UseInHand(r, bob, adv);
        Assert.Equal(TOOL_HEMOSTAT, r.In(() => adv.tool_behaviour));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHeadsDoorRemoteLockboxAndTheSuitSensorsWork()
    {
        var (r, feed, bob) = Room(9507);
        obj_item_door_remote_chief_engineer remote = Hold<obj_item_door_remote_chief_engineer>(r, bob);
        Assert.Equal("remote_engi_open", r.In(() => remote.icon_state));
        UseInHand(r, bob, remote);
        Assert.Equal("remote_engi_bolt", r.In(() => remote.icon_state));
        Assert.True(feed.Any("mode: Toggle Bolts"));
        r.In(() => Assert.True(remote.access_list.len > 0));

        // the medal box starts locked and full of its own medals
        obj_item_storage_lockbox_medal_med box = Make<obj_item_storage_lockbox_medal_med>(r, bob.loc as atom);
        Assert.Equal("medbox+l", r.In(() => box.icon_state));
        Assert.Equal(5, r.In(() => box.contents.len));
        r.In(() => Assert.True(Dm.truthy(box.atom_storage.locked)));

        // the sensors: cutting one out of a uniform leaves it without
        obj_item_clothing_under uniform = Make<obj_item_clothing_under_color_grey>(r, bob.loc as atom);
        r.In(() => uniform.set_has_sensor(NO_SENSORS));
        Assert.Equal((float)NO_SENSORS, r.In(() => Dm.num(uniform.has_sensor)));
        obj_item_suit_sensor sensor = Make<obj_item_suit_sensor>(r, bob.loc as atom);
        r.In(() => sensor.set_mode(SENSOR_COORDS));
        r.Step(2);
        Assert.Contains("suit_sensor_tracking", OverlayStates(r, sensor));

        // earmuffs muffle a bang
        obj_item_clothing_ears_earmuffs muffs = Make<obj_item_clothing_ears_earmuffs>(r, bob.loc as atom);
        float before = r.In(() => Dm.num(bob.get_ear_protection(TRUE)));
        Wear(r, bob, muffs, ITEM_SLOT_EARS);
        Assert.Equal(before + 1, r.In(() => Dm.num(bob.get_ear_protection(TRUE))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheElectropackShocksItsWearerOnItsCode()
    {
        var (r, feed, bob) = Room(9508);
        obj_item_electropack pack = Make<obj_item_electropack>(r, bob.loc as atom);
        Wear(r, bob, pack, ITEM_SLOT_BACK);
        r.In(() =>
        {
            var signal = Dm.@new<datum_signal>(Dm.list(Dm.kv("code", 1))); // the wrong code
            pack.receive_signal(signal);
        });
        Assert.False(feed.Any("sharp shock"));
        r.In(() => pack.receive_signal(Dm.@new<datum_signal>(Dm.list(Dm.kv("code", 2)))));
        r.Step(2);
        Assert.True(feed.Any("sharp shock"));
        r.In(() => Assert.True(Dm.truthy(bob.has_status_effect(typeof(datum_status_effect_incapacitating_paralyzed)))));
        r.AssertNoRuntimes();
    }
}
