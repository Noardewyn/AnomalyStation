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
/// Syndicate stage G1b (D316): the chameleon kit, the broken one, the agent card and the no-slips sell; worn chameleon clothing takes
/// a picked look, the outfit action dresses every chameleon item (and the agent card) as a job, an EMP scrambles the look for a while
/// and the broken kit never stops; the agent card copies a swiped card's access through its window and hides its wearer from cameras;
/// the chameleon laser looks and sounds like the gun it mimics but its shots harm nobody; the chameleon scanner copies a person's
/// outfit and puts it on.
/// </summary>
public class SyndicateChameleonTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 91)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Worn<T>(AudioRoom r, mob_living_carbon_human who, object slot) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.equip_to_slot_or_del(item, slot))), typeof(T).Name + " was not equipped");
        return item;
    });

    [Fact]
    public void TheChameleonGearIsLiveAndSells()
    {
        foreach (Type type in new[]
        {
            typeof(datum_action_item_action_chameleon_change), typeof(datum_action_chameleon_outfit), typeof(datum_action_item_action_chameleon_change_jumpsuit),
            typeof(datum_action_item_action_chameleon_change_id), typeof(datum_action_item_action_chameleon_change_id_trim), typeof(obj_item_card_id_advanced_chameleon),
            typeof(obj_item_card_id_advanced_chameleon_elite), typeof(obj_item_clothing_shoes_chameleon_noslip), typeof(obj_item_clothing_mask_chameleon),
            typeof(obj_item_clothing_under_chameleon_broken), typeof(obj_item_gun_energy_laser_chameleon), typeof(obj_item_chameleon_scanner),
            typeof(obj_item_storage_backpack_chameleon), typeof(obj_item_modular_computer_pda_chameleon_broken),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_stealthy_tools_chameleon), typeof(datum_uplink_item_badass_costumes_obvious_chameleon),
            typeof(datum_uplink_item_stealthy_tools_agent_card), typeof(datum_uplink_item_stealthy_tools_syndigaloshes),
        })
            Assert.False(r.In(() => uplink_item_hidden((datum_uplink_item)Dm.@new(entry))), entry.Name + " is hidden");
    }

    [Fact]
    public void ChameleonClothingTakesALookTheOutfitActionDressesItAsAJobAndAnEmpScramblesIt()
    {
        var (r, feed, bob) = Room(91);
        var suit = Worn<obj_item_clothing_under_chameleon>(r, bob, ITEM_SLOT_ICLOTHING);
        var shoes = Worn<obj_item_clothing_shoes_chameleon_noslip>(r, bob, ITEM_SLOT_FEET);
        var card = Worn<obj_item_card_id_advanced_chameleon>(r, bob, ITEM_SLOT_ID);
        r.Step(2);
        // each chameleon item grants its change action, and the wearer gets one outfit action
        Assert.Equal(1, r.In(() => Dm.each<datum_action_chameleon_outfit>(bob.actions).Count()));
        var jumpsuit_action = r.In(() => Dm.locate<datum_action_item_action_chameleon_change_jumpsuit>(bob.actions) as datum_action_item_action_chameleon_change_jumpsuit);
        Assert.NotNull(jumpsuit_action);
        Assert.Equal("Change Jumpsuit Appearance", r.In(() => jumpsuit_action.name));
        Assert.Equal("black jumpsuit", r.In(() => suit.name)); // its preset look
        Assert.True(r.In(() => jumpsuit_action.chameleon_list.len) > 50, "the jumpsuit has few disguises");
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_NO_SLIP_WATER)), "the no-slips do not stop slipping");

        // a picked look
        r.In(() => Waiting.NoSleep(jumpsuit_action.update_look(typeof(obj_item_clothing_under_rank_engineering_engineer))));
        Assert.Equal(Dm.initial(typeof(obj_item_clothing_under_rank_engineering_engineer), (atom p) => p.name), r.In(() => suit.name));
        Assert.Equal(typeof(obj_item_clothing_under_rank_engineering_engineer), r.In(() => jumpsuit_action.active_type));

        // the outfit action dresses everything as a security officer, the agent card included
        var outfit_action = r.In(() => Dm.locate<datum_action_chameleon_outfit>(bob.actions) as datum_action_chameleon_outfit);
        r.In(() => Waiting.Detach(outfit_action.apply_outfit(Dm.@new<datum_outfit_job_security>())));
        r.Step(2 * Second);
        r.AssertNoRuntimes();
        Assert.Equal(typeof(obj_item_clothing_under_rank_security_officer), r.In(() => jumpsuit_action.active_type));
        Assert.Equal(Dm.initial(typeof(obj_item_clothing_shoes_jackboots_sec), (atom p) => p.name), r.In(() => shoes.name));
        Assert.Equal("Security Officer", r.In(() => Dm.str(card.assignment)));
        Assert.Equal(1f, r.In(() => card.forged));

        // an EMP scrambles the look for 30 seconds, then it stays put
        r.In(() => Waiting.NoSleep(suit.emp_act(EMP_HEAVY)));
        var looks = new HashSet<Type>();
        for (int i = 0; i < 20; i++)
        {
            r.Step(Second);
            looks.Add(r.In(() => jumpsuit_action.active_type));
        }
        Assert.True(looks.Count > 3, "the EMP did not scramble the look");
        r.Step(15 * Second);
        Type settled = r.In(() => jumpsuit_action.active_type);
        r.Step(5 * Second);
        Assert.Equal(settled, r.In(() => jumpsuit_action.active_type));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAgentCardCopiesASwipedCardsAccessAndHidesItsWearerFromCameras()
    {
        var (r, feed, bob) = Room(92);
        Worn<obj_item_clothing_under_chameleon>(r, bob, ITEM_SLOT_ICLOTHING);
        var card = Worn<obj_item_card_id_advanced_chameleon>(r, bob, ITEM_SLOT_ID);
        // worn in the ID slot it answers the camera tracking check
        Assert.Equal(COMPONENT_CANT_TRACK, r.In(() => Dm.num(bob._SendSignal(COMSIG_LIVING_CAN_TRACK, Dm.list(bob, bob)))));

        var officer_card = r.In(() =>
        {
            var c = Dm.@new<obj_item_card_id_advanced>(locate(4, 4, 1));
            Waiting.NoSleep(SSid_access.apply_trim_to_card(c, typeof(datum_id_trim_job_security_officer), copy_access: TRUE));
            return c;
        });
        Assert.Contains(ACCESS_BRIG, r.In(() => Dm.each(officer_card.access).Select(Dm.str).ToList()));
        Assert.DoesNotContain(ACCESS_BRIG, r.In(() => Dm.each(card.access).Select(Dm.str).ToList()));

        // a second agent card in hand swipes the officer's card
        card = r.In(() =>
        {
            var held = Dm.@new<obj_item_card_id_advanced_chameleon>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(held));
            Waiting.NoSleep(held.interact_with_atom(officer_card, bob));
            return held;
        });
        Assert.Same(officer_card, r.In(() => card.theft_target.resolve()));
        Assert.NotNull(r.In(() => SStgui.get_open_ui(bob, card))); // the ChameleonCard window
        Assert.Equal(UI_INTERACTIVE, r.In(() => Dm.num(Waiting.NoSleep(card.ui_status(bob)))));
        var data = r.In(() => (DmList)Waiting.NoSleep(card.ui_data(bob)));
        Assert.Contains(ACCESS_BRIG, r.In(() => Dm.each((DmList)data["theftAccess"]).Select(Dm.str).ToList()));

        r.In(() => { mob caller = usr; usr = bob; try { return Waiting.NoSleep(card.ui_act("mod_access", Dm.list(Dm.kv("access_target", ACCESS_BRIG), Dm.kv("access_wildcard", ACCESS_FLAG_COMMON_NAME)), SStgui.get_open_ui(bob, card) as datum_tgui)); } finally { usr = caller; } });
        Assert.Contains(ACCESS_BRIG, r.In(() => Dm.each(card.access).Select(Dm.str).ToList()));
        // and once more takes it away
        r.In(() => { mob caller = usr; usr = bob; try { return Waiting.NoSleep(card.ui_act("mod_access", Dm.list(Dm.kv("access_target", ACCESS_BRIG), Dm.kv("access_wildcard", ACCESS_FLAG_COMMON_NAME)), SStgui.get_open_ui(bob, card) as datum_tgui)); } finally { usr = caller; } });
        Assert.DoesNotContain(ACCESS_BRIG, r.In(() => Dm.each(card.access).Select(Dm.str).ToList()));

        // out of reach, the window closes
        r.In(() => Waiting.NoSleep(officer_card.forceMove(locate(9, 7, 1))));
        Assert.Equal(UI_CLOSE, r.In(() => Dm.num(Waiting.NoSleep(card.ui_status(bob)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChameleonLaserMimicsAPistolButItsShotsHarmNobody()
    {
        var (r, feed, bob) = Room(93);
        var gun = r.In(() =>
        {
            var g = Dm.@new<obj_item_gun_energy_laser_chameleon>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(g));
            return g;
        });
        Assert.Equal(Dm.initial(typeof(obj_item_gun_energy_laser), (atom p) => p.name), r.In(() => gun.name));
        var gun_action = r.In(() => Dm.locate<datum_action_item_action_chameleon_change_gun>(gun.actions) as datum_action_item_action_chameleon_change_gun);
        r.In(() => Waiting.NoSleep(gun_action.update_look(typeof(obj_item_gun_ballistic_automatic_pistol))));
        Assert.Equal(Dm.initial(typeof(obj_item_gun_ballistic_automatic_pistol), (atom p) => p.name), r.In(() => gun.name));
        Assert.Contains("pistol", r.In(() => Dm.str(gun.fire_sound)));
        Assert.Equal(Dm.initial(typeof(obj_projectile_bullet_c9mm), (atom p) => p.name), r.In(() => gun.chambered.loaded_projectile.name));

        mob_living_carbon_human alice = r.Dummy(7, 4);
        r.Step(2);
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(alice, bob, "")));
        r.Step(2 * Second);
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("gun/pistol/shot"));
        Assert.Equal(0f, r.In(() => Dm.num(alice.get_brute_loss()) + Dm.num(alice.get_fire_loss())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheScannerCopiesAnOutfitIntoTheChameleonClothingAndTheBrokenKitNeverStopsChanging()
    {
        var (r, feed, bob) = Room(94);
        var suit = Worn<obj_item_clothing_under_chameleon>(r, bob, ITEM_SLOT_ICLOTHING);
        var scanner = r.In(() =>
        {
            var s = Dm.@new<obj_item_chameleon_scanner>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(s));
            return s;
        });
        mob_living_carbon_human alice = r.Dummy(6, 4);
        r.In(() => Waiting.NoSleep(alice.equipOutfit(typeof(datum_outfit_job_security))));
        r.Step(2);

        r.In(() => Waiting.Detach(scanner.ranged_interact_with_atom_secondary(alice, bob)));
        r.Step(2 * Second);
        Assert.True(feed.Any("balloon: outfit saved in slot 1"));
        var jumpsuit_action = r.In(() => Dm.locate<datum_action_item_action_chameleon_change_jumpsuit>(bob.actions) as datum_action_item_action_chameleon_change_jumpsuit);
        Assert.Equal(typeof(obj_item_clothing_under_rank_security_officer), r.In(() => jumpsuit_action.active_type));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("ping"));

        // a broken chameleon item keeps changing and shrugs off EMPs
        var broken = r.In(() => Dm.@new<obj_item_clothing_under_chameleon_broken>(locate(5, 5, 1)));
        var broken_action = r.In(() => Dm.locate<datum_action_item_action_chameleon_change>(broken.actions) as datum_action_item_action_chameleon_change);
        var looks = new HashSet<Type>();
        for (int i = 0; i < 10; i++)
        {
            r.Step(5 * Second);
            looks.Add(r.In(() => broken_action.active_type));
        }
        Assert.True(looks.Count > 3, "the broken jumpsuit stopped changing");
        Assert.True(float.IsPositiveInfinity(r.In(() => broken_action.emp_timer) - r.In(() => Dm.world.time)), "the broken jumpsuit's scramble ends");
        r.AssertNoRuntimes();
    }
}
