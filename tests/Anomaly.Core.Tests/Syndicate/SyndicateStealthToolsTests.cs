using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage G1c (D316): the EMP flashlight, the polarized contacts, the smuggler's satchel, the AI detector and the fake
/// identity kit sell; the flashlight blinks an EMP at a target with its four charges and recharges; the contacts make the eyes
/// welder-proof; the mulligan serum gives a new face and the kit a crew record and an Assistant's ID card for it; the satchel hides
/// under a floor tile, holds no other smuggler's satchel and comes with its tools; the AI detector shows its HUD and stays green
/// without an AI.
/// </summary>
public class SyndicateStealthToolsTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 101)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    [Fact]
    public void TheStealthToolsAreLiveAndSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_flashlight_emp), typeof(obj_item_syndicate_contacts), typeof(datum_reagent_mulligan), typeof(obj_item_fake_identity_kit),
            typeof(obj_item_arrival_announcer), typeof(obj_item_storage_backpack_satchel_flat_with_tools), typeof(datum_storage_backpack_satchel_flat),
            typeof(obj_item_multitool_ai_detect),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_stealthy_tools_emplight), typeof(datum_uplink_item_device_tools_syndicate_contacts), typeof(datum_uplink_item_stealthy_tools_smugglersatchel),
            typeof(datum_uplink_item_stealthy_tools_ai_detector), typeof(datum_uplink_item_stealthy_tools_mulligan),
        })
            Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(entry))), entry.Name + " is hidden");
    }

    [Fact]
    public void TheEmpFlashlightBlinksFourEmpsThenRecharges()
    {
        var (r, feed, bob) = Room(101);
        var light = Held<obj_item_flashlight_emp>(r, bob);
        var target = r.In(() => Dm.@new<obj_item_clothing_under_chameleon>(locate(4, 4, 1)));
        var action = r.In(() => Dm.locate<datum_action_item_action_chameleon_change>(target.actions) as datum_action_item_action_chameleon_change);
        r.Step(2);

        r.In(() => Waiting.NoSleep(light.interact_with_atom(target, bob)));
        Assert.True(feed.Any("blinks the"));
        Assert.True(feed.Any("now has 3 charges."));
        Assert.True(r.In(() => action.emp_timer) > r.In(() => (float)Dm.world.time), "the EMP did not reach the target");
        for (int i = 0; i < 3; i++)
            r.In(() => Waiting.NoSleep(light.interact_with_atom(target, bob)));
        Assert.Equal(0f, r.In(() => light.emp_cur_charges));
        r.In(() => Waiting.NoSleep(light.interact_with_atom(target, bob)));
        Assert.True(feed.Any("needs time to recharge!"));
        r.Step(21 * Second);
        Assert.Equal(1f, r.In(() => light.emp_cur_charges));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheContactsMakeTheEyesWelderProof()
    {
        var (r, feed, bob) = Room(102);
        var eyes = r.In(() => bob.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes);
        float before = r.In(() => eyes.flash_protect);
        var contacts = Held<obj_item_syndicate_contacts>(r, bob);
        r.In(() => Waiting.Detach(contacts.attack_self(bob)));
        r.Step(4 * Second);
        Assert.True(feed.Any("The contacts seamlessly merge with your iris."));
        Assert.Equal(before + FLASH_PROTECTION_WELDER, r.In(() => eyes.flash_protect));
        Assert.True(r.In(() => QDELETED(contacts)), "the contacts were not used up");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMulliganSerumGivesANewFaceAndTheKitACrewRecordAndAnIdForIt()
    {
        var (r, feed, bob) = Room(103);
        string old_name = r.In(() => Dm.str(bob.real_name));
        r.In(() => Waiting.NoSleep(((datum_reagents)bob.reagents).add_reagent(typeof(datum_reagent_mulligan), 1)));
        r.Step(3 * Second);
        Assert.True(feed.Any("suddenly transforms!"));
        string new_name = r.In(() => Dm.str(bob.real_name));
        Assert.NotEqual(old_name, new_name);
        Assert.Null(r.In(() => find_record(new_name)));

        var kit = Held<obj_item_fake_identity_kit>(r, bob);
        r.In(() => Waiting.Detach(kit.attack_self(bob)));
        r.Step(Second);
        r.AssertNoRuntimes();
        Assert.NotNull(r.In(() => find_record(new_name)));
        var card = r.In(() => bob.get_idcard(hand_first: FALSE) as obj_item_card_id);
        Assert.NotNull(card);
        Assert.Equal(new_name, r.In(() => Dm.str(card.registered_name)));
        Assert.True(r.In(() => Dm.each<obj_item_arrival_announcer>(bob.held_items).Any()), "no arrival signaller in hand");
        Assert.True(r.In(() => QDELETED(kit)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSmugglersSatchelHidesUnderAFloorTileAndHoldsItsTools()
    {
        var (r, feed, bob) = Room(104);
        var satchel = r.In(() => Dm.@new<obj_item_storage_backpack_satchel_flat_with_tools>(locate(5, 5, 1)));
        r.Step(2);
        Assert.Equal(0f, r.In(() => (float)satchel.invisibility));
        Assert.True(r.In(() => Dm.each<obj_item_crowbar>(satchel.contents).Any()), "no crowbar");
        Assert.True(r.In(() => Dm.each<obj_item_stack_tile_iron_base>(satchel.contents).Any()), "no floor tile");
        Assert.True(r.In(() => HAS_TRAIT(satchel, TRAIT_CONTRABAND_BLOCKER)));
        // a floor tile laid over it (the turf's levelupdate sends COMSIG_OBJ_HIDE; the toy room's turfs have no baseturfs to change):
        // it hides, anchored, seen by T-rays only
        r.In(() => satchel._SendSignal(COMSIG_OBJ_HIDE, Dm.list(satchel, UNDERFLOOR_HIDDEN)));
        r.Step(2);
        Assert.Equal(1f, r.In(() => Dm.num(satchel.anchored)));
        Assert.Equal((float)INVISIBILITY_OBSERVER, r.In(() => (float)satchel.invisibility));
        Assert.True(r.In(() => HAS_TRAIT(satchel, TRAIT_T_RAY_VISIBLE)));
        // it holds no other smuggler's satchel
        var other = r.In(() => Dm.@new<obj_item_storage_backpack_satchel_flat_empty>(bob.loc));
        Assert.False(r.In(() => Dm.truthy(satchel.atom_storage.can_insert(other, bob, messages: FALSE))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAiDetectorShowsItsHudAndStaysGreenWithoutAnAi()
    {
        var (r, feed, bob) = Room(105);
        var detector = Held<obj_item_multitool_ai_detect>(r, bob);
        r.In(() => Waiting.NoSleep(detector.attack_self(bob)));
        Assert.True(feed.Any("You toggle the ai detection feature on"));
        Assert.Equal(1f, r.In(() => detector.hud_on));
        var hud = r.In(() => GLOB.huds[DATA_HUD_AI_DETECT] as datum_atom_hud);
        Assert.Contains(bob, r.In(() => Dm.each(hud.hud_users_all_z_levels).ToList()));
        r.Step(Second);
        Assert.Equal("", r.In(() => detector.detect_state));
        var lines = r.In(() => Dm.each((DmList)Waiting.NoSleep(detector.examine(bob))).Select(Dm.str).ToList());
        Assert.Contains(lines, l => l.Contains("No AI should be currently looking at you."));
        r.In(() => Waiting.NoSleep(detector.attack_self_secondary(bob)));
        Assert.True(feed.Any("balloon: nearby unseen spots shown"));
        r.In(() => Waiting.NoSleep(detector.attack_self(bob)));
        Assert.DoesNotContain(bob, r.In(() => Dm.each(hud.hud_users_all_z_levels).ToList()));
        r.AssertNoRuntimes();
    }
}
