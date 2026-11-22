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
/// Gaps package GU1 (D333): the firing pins left of pins.dm in a booted toy room. The DNA-keyed pin locks to the first man who pulls
/// the trigger and refuses everyone else; the monkeylock pin screeches at a man and lets a simian fire; the explorers' outback pin
/// refuses on a station level; the paywall pin is linked to a card's account with a deposit amount, refuses to go into a gun before
/// that, writes its price into the gun's description, and sells a second man its license through the purchase prompt.
/// </summary>
public class FiringPinsGU1Tests
{
    private static readonly Type[] AllTypes =
    {
        typeof(obj_item_firing_pin_dna), typeof(obj_item_firing_pin_dna_dredd), typeof(obj_item_firing_pin_paywall),
        typeof(obj_item_firing_pin_paywall_luxury), typeof(obj_item_firing_pin_explorer), typeof(obj_item_firing_pin_monkey),
    };

    private const int LineY = 4;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(14, 7, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    // a laser whose standard pin is swapped for the pin under test
    private static obj_item_gun_energy_laser Gun<TPin>(AudioRoom r, mob_living_carbon_human holder, out TPin pin) where TPin : obj_item_firing_pin
    {
        obj_item_gun_energy_laser gun = r.In(() => Dm.@new<obj_item_gun_energy_laser>(holder.loc));
        TPin made = r.In(() =>
        {
            var old = (obj_item_firing_pin)gun.pin;
            old.gun_remove();
            qdel(old);
            var p = Dm.@new<TPin>(holder.loc);
            Waiting.NoSleep(p.gun_insert(null, gun, TRUE));
            Waiting.NoSleep(holder.put_in_active_hand(gun));
            return p;
        });
        Assert.Same(made, r.In(() => gun.pin));
        pin = made;
        return gun;
    }

    private static List<obj_projectile> Shots(AudioRoom r) => r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).ToList());

    // the trigger pulled at a target down the line (tg's try_fire_gun, as the click chain calls it); the beam flies out and is gone
    private static bool Pull(AudioRoom r, mob_living_carbon_human shooter, obj_item_gun gun, atom target)
    {
        int before = Shots(r).Count;
        r.In(() => Waiting.NoSleep(gun.try_fire_gun(target, shooter, "icon-x=16;icon-y=16")));
        bool fired = Shots(r).Count > before;
        r.Step(2 * Second);
        return fired;
    }

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    [Fact]
    public void EveryPinSpawnsLiveIsExaminedAndGoes()
    {
        var (r, _, bob) = Room(9701);
        foreach (Type type in AllTypes)
        {
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
            var pin = r.In(() => (obj_item)Dm.@new(type, locate(5, LineY, 1)));
            r.Step(2);
            string text = r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(pin.examine(bob))).Select(o => Dm.str(o))));
            Assert.Contains("firing pin", text, StringComparison.OrdinalIgnoreCase);
            r.In(() => qdel(pin));
            r.Step(2);
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDnaPinLocksToTheFirstManWhoPullsTheTriggerAndRefusesTheNext()
    {
        var (r, feed, bob) = Room(9702);
        mob_living_carbon_human rob = r.Join(5, 2, "Rob");
        mob_living_carbon_human target = r.Dummy(11, LineY);
        obj_item_gun_energy_laser gun = Gun<obj_item_firing_pin_dna>(r, bob, out var pin);
        Assert.Null(r.In(() => pin.unique_enzymes));

        // the first pull links the pin to Bob's DNA and does not fire
        Assert.False(Pull(r, bob, gun, target));
        Assert.Equal(r.In(() => bob.dna.unique_enzymes), r.In(() => pin.unique_enzymes));
        Assert.True(feed.Any("balloon: dna lock set"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        // from then on Bob fires
        Assert.True(Pull(r, bob, gun, target));

        // Rob's DNA is not the lock's
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(gun));
            Waiting.NoSleep(rob.put_in_active_hand(gun));
        });
        Assert.False(Pull(r, rob, gun, target));
        Assert.True(feed.Any("balloon: dna check failed!"));
        Assert.Equal(r.In(() => bob.dna.unique_enzymes), r.In(() => pin.unique_enzymes));

        // a pin pressed on a man takes his DNA at once
        var second = r.In(() => Dm.@new<obj_item_firing_pin_dna>(rob.loc));
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(second.interact_with_atom(rob, rob, null))));
        Assert.Equal(r.In(() => rob.dna.unique_enzymes), r.In(() => second.unique_enzymes));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMonkeylockPinScreechesAtAManAndLetsASimianFire()
    {
        var (r, feed, bob) = Room(9703);
        mob_living_carbon_human target = r.Dummy(11, LineY);
        obj_item_gun_energy_laser gun = Gun<obj_item_firing_pin_monkey>(r, bob, out _);

        Assert.False(Pull(r, bob, gun, target));
        Assert.True(feed.Any("balloon: not a monkey!"));
        Assert.True(Heard(r, "screech"), "no screech: " + string.Join(", ", r.Sounds.Heard.Select(h => h.File)));

        r.In(() => ADD_TRAIT(bob, TRAIT_SIMIAN, "test"));
        Assert.True(Pull(r, bob, gun, target));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOutbackPinRefusesOnAStationLevel()
    {
        var (r, feed, bob) = Room(9704);
        mob_living_carbon_human target = r.Dummy(11, LineY);
        obj_item_gun_energy_laser gun = Gun<obj_item_firing_pin_explorer>(r, bob, out var pin);
        bool station = r.In(() => Dm.truthy(is_station_level(bob.z)));

        Assert.Equal(!station, Pull(r, bob, gun, target));
        Assert.Equal(station, feed.Any("balloon: cannot fire while on station, mate!"));
        // the pin's own rule, whatever the toy room's level is
        Assert.Equal(Dm.num(!station), r.In(() => Dm.num(Waiting.NoSleep(pin.pin_auth(bob)))));
        Assert.Equal(FALSE, r.In(() => Dm.num(Waiting.NoSleep(pin.pin_auth(null)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePaywallPinIsLinkedToACardAndSellsItsLicense()
    {
        var (r, feed, bob) = Room(9705);
        // no payday inside the purchase (a paycheck landed in Rob's account once gaps CI1's subsystems moved the MC's ticks)
        r.In(() => SSeconomy.can_fire = FALSE);
        mob_living_carbon_human rob = r.Join(3, 2, "Rob");
        mob_living_carbon_human target = r.Dummy(11, LineY);
        datum_bank_account bobs = VendingTests.Funds(r, bob, 0);
        datum_bank_account robs = VendingTests.Funds(r, rob, 100);
        var pin = r.In(() => Dm.@new<obj_item_firing_pin_paywall>(bob.loc));
        var gun = r.In(() =>
        {
            var made = Dm.@new<obj_item_gun_energy_laser>(bob.loc);
            var old = (obj_item_firing_pin)made.pin;
            old.gun_remove();
            qdel(old);
            return made;
        });

        // a pin with no account refuses to go in and comes back to the hand
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(pin)));
        r.In(() => Waiting.NoSleep(pin.interact_with_atom(gun, bob, null)));
        Assert.True(feed.Any("ERROR: Please swipe valid identification card before installing firing pin!"));
        Assert.Null(r.In(() => gun.pin));
        Assert.Same(pin, r.In(() => bob.get_active_held_item()));

        // per shot or once: the pin's switch in hand
        r.In(() => Waiting.NoSleep(pin.attack_self(bob)));
        Assert.True(feed.Any("You set the pin to process payment for every shot."));
        r.In(() => Waiting.NoSleep(pin.attack_self(bob)));
        Assert.True(feed.Any("You set the pin to one-time license payment."));

        // Bob's card swiped on the pin, a deposit amount asked in a number window
        var card = r.In(() => (obj_item_card_id)bob.wear_id);
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(pin));
            Waiting.NoSleep(bob.temporarilyRemoveItemFromInventory(card));
            Waiting.NoSleep(bob.put_in_active_hand(card));
            Waiting.Detach(pin.item_interaction(bob, card));
        });
        ActIn(r, bob, "NumberInputModal", "submit", ("entry", 20));
        Assert.True(feed.Any("You link the card to the firing pin."), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Same(bobs, r.In(() => pin.pin_owner));
        Assert.Equal(20f, r.In(() => Dm.num(pin.payment_amount)));
        Assert.Contains("authorized to pay into the account of", r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(pin.examine(bob))).Select(o => Dm.str(o)))));

        // into the gun now, its price written into the gun's description
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(card));
            Waiting.NoSleep(bob.put_in_active_hand(pin));
            Waiting.NoSleep(pin.interact_with_atom(gun, bob, null));
        });
        Assert.Same(pin, r.In(() => gun.pin));
        Assert.Contains("license permit cost of 20 credits", r.In(() => gun.desc));

        // Rob pulls the trigger: the purchase prompt; he pays the license into Bob's account and fires from then on
        r.In(() =>
        {
            Waiting.NoSleep(rob.put_in_active_hand(gun));
            Waiting.Detach(gun.try_fire_gun(target, rob, "icon-x=16;icon-y=16"));
        });
        ActIn(r, rob, "AlertModal", "choose", ("choice", "Yes"));
        Assert.True(feed.Any("Gun license purchased, have a secure day!"), string.Join(" | ", feed.Chat.Select(c => c.Html)));
        Assert.Equal(80f, r.In(() => robs.account_balance));
        Assert.Equal(20f, r.In(() => bobs.account_balance));
        Assert.Empty(Shots(r)); // tg: the prompt's pull never fires
        r.Step(2 * Second);
        Assert.True(Pull(r, rob, gun, target));
        Assert.Equal(80f, r.In(() => robs.account_balance)); // a license is paid once

        // taken out, the gun's description is its own again
        r.In(() => pin.gun_remove(rob));
        Assert.Equal(r.In(() => Dm.initial(gun, (obj_item_gun p) => p.desc)), r.In(() => gun.desc));
        r.AssertNoRuntimes();
    }

    // an act in the newest window of that interface the player has open
    private static void ActIn(AudioRoom r, mob who, string iface, string action, params (string Key, object Value)[] args)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(args.Select(a => (object)Dm.kv(a.Key, a.Value)).ToArray())));
        r.Step(4);
    }
}
