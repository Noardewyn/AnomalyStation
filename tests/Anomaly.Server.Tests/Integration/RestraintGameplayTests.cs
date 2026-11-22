using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7x slice E (restraints, batons and flashes) on a real server with two players: Alice flashes Bob, whose client gets the
/// flash screen; she stuns him with a charged stun baton (tg's stamina damage, then the knockdown two seconds later, her
/// baton's pictures and its charge), handcuffs the downed man (tg's four seconds of do_after), he cannot pick a thing up with
/// his hands in cuffs, and he resists out by the resist button of his HUD (the cuffs' breakout time).
/// </summary>
public class RestraintGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static T Make<T>(RealHeadlessWorld w, mob_living holder) where T : obj_item
    {
        using (w.World.Enter())
        {
            T item = Dm.@new<T>(get_turf(holder));
            Waiting.NoSleep(holder.put_in_hands(item));
            return item;
        }
    }

    private static void Click(RealHeadlessWorld.Player p, mob_living target, AimZone zone = AimZone.Chest)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 18, zone), ClickButton.Left, ClickModifiers.None));

    [Fact]
    public void A_flash_blinds_a_baton_stuns_cuffs_hold_and_the_resist_button_frees()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 11, TimeoutSteps = 6000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        using (w.World.Enter())
        {
            Waiting.NoSleep(bobMob.forceMove(get_step(aliceMob, EAST)));
            aliceMob.face_atom(bobMob);
            bobMob.face_atom(aliceMob);
        }

        // --- the flash: the victim's client gets tg's flash screen, Bob is knocked down head-on and the flash shows its own picture
        obj_item_assembly_flash_handheld flash = Make<obj_item_assembly_flash_handheld>(w, aliceMob);
        Assert.Equal("flash", In(w, () => flash.icon_state));
        Click(alice, bobMob);
        w.Run(6);
        atom_movable_screen_fullscreen flashScreen = In(w, () => bobMob.screens?["flash"] as atom_movable_screen_fullscreen);
        Assert.NotNull(flashScreen);
        Assert.Contains(bob.Session.Replica.Screen.Values, e => e.Id == flashScreen.dm_id);
        Assert.True(In(w, () => Dm.num(bobMob.get_stamina_loss())) > 0, "the head-on flash's stamina damage");
        Assert.True(In(w, () => Dm.truthy(bobMob.IsKnockdown())), "and its knockdown");
        Assert.Equal(1f, In(w, () => flash.flashing)); // the bulb lights for half a second (the flash-f overlay)
        w.Run(Second);
        Assert.Equal(0f, In(w, () => flash.flashing)); // and goes out
        output.WriteLine("flash ok");

        // --- the baton: Bob has got up; Alice turns the baton on (its pictures), and a hit costs charge and stuns
        w.Run(8 * Second);
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.drop_all_held_items());
            Assert.False(Dm.truthy(bobMob.IsKnockdown()));
            Waiting.NoSleep(bobMob.adjust_stamina_loss(-1000));
        }
        obj_item_melee_baton_security baton = Make<obj_item_melee_baton_security_loaded>(w, aliceMob);
        Assert.Equal("stunbaton", In(w, () => baton.icon_state));
        alice.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(3);
        Assert.Equal(1f, In(w, () => baton.active));
        Assert.Equal("stunbaton_active", In(w, () => baton.icon_state));
        float charge = In(w, () => baton.cell.charge);
        Click(alice, bobMob);
        w.Run(Second);
        Assert.True(In(w, () => Dm.num(bobMob.get_stamina_loss())) >= 50, "the baton's stamina damage");
        Assert.True(In(w, () => baton.cell.charge) < charge, "a hit costs charge");
        Assert.False(In(w, () => Dm.truthy(bobMob.IsKnockdown())), "the knockdown comes after two seconds (apply_stun_effect_end)");
        w.Run(Second + 4);
        Assert.True(In(w, () => Dm.truthy(bobMob.IsKnockdown())), "your muscles seize");
        Assert.True(PostureBits.IsLying(bob.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id).State.Posture), "Bob's client sees him lying");
        output.WriteLine("baton ok");

        // --- the cuffs: tg's four seconds of do_after on the downed man
        using (w.World.Enter())
            Waiting.NoSleep(aliceMob.drop_all_held_items());
        obj_item_restraints_handcuffs cuffs = Make<obj_item_restraints_handcuffs>(w, aliceMob);
        Click(alice, bobMob);
        w.Run(2 * Second);
        Assert.Null(In(w, () => bobMob.handcuffed));
        w.Run(3 * Second);
        Assert.Same(cuffs, In(w, () => bobMob.handcuffed));
        Assert.True(In(w, () => HAS_TRAIT(bobMob, TRAIT_RESTRAINED)));
        Assert.NotNull(In(w, () => bobMob.overlays_standing[HANDCUFF_LAYER]));
        Assert.True(In(w, () => Dm.truthy(bobMob.alerts?[ALERT_HANDCUFFED])));
        output.WriteLine("cuffed ok");

        // --- he cannot use his hands: Bob is up again and a pen beside him stays where it is
        w.Run(4 * Second);
        obj_item_pen pen = In(w, () => Dm.@new<obj_item_pen>(get_step(bobMob, NORTH)));
        Assert.False(In(w, () => Dm.truthy(Waiting.NoSleep(bobMob.put_in_hands(pen)))), "restrained hands hold nothing");
        bob.Session.Click(new ClickTarget(TargetKind.Entity, pen.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        Assert.NotSame(bobMob, In(w, () => pen.loc));
        Assert.Equal(0, In(w, () => Dm.each_cast<obj_item>(bobMob.held_items).Count(i => i != null)));

        // --- he resists with the HUD's resist button: the cuffs' breakout time, shortened from a minute to keep the test short
        In(w, () => cuffs.breakouttime = 4 * SECONDS);
        long resist = In(w, () => ((datum)bobMob.hud_used.screen_objects[HUD_MOB_RESIST]).dm_id);
        Assert.True(bob.Session.Click(new ClickTarget(TargetKind.Screen, resist, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.Same(cuffs, In(w, () => bobMob.handcuffed));
        w.Run(4 * Second);
        Assert.Null(In(w, () => bobMob.handcuffed));
        Assert.False(In(w, () => HAS_TRAIT(bobMob, TRAIT_RESTRAINED)));
        Assert.False(In(w, () => Dm.truthy(bobMob.alerts?[ALERT_HANDCUFFED])));
        // free hands hold things again
        Assert.True(In(w, () => Dm.truthy(Waiting.NoSleep(bobMob.put_in_hands(pen)))), "the freed hands hold the pen");
        output.WriteLine("resisted out ok");
    }
}
