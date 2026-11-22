using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Playtest;

/// <summary>
/// The runtimes of the play test of 2026-11-14 (the server's journal): an inert gun fired (the pin its Initialize never made), an
/// inert drink used in hand (no reagents), an inert laptop blown up (no physical), a closet shaking for a gun inside it, the chaplain's
/// whip null rod (a material slot nobody translated), the stat panel's Cancel Camera View (no verb body), and a turf replaced with
/// CHANGETURF_SKIP that a stale list still named (Lavaland's rivers under a colossus).
/// </summary>
public class PlayTest20261114Tests
{
    private const int LineY = 4;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(12, 8, seed: seed);
        r.Step(20);
        return r;
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human holder) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(holder.put_in_active_hand(item)));
        r.Step();
        return item;
    }

    [Fact]
    public void AnInertGunIsRefusedInsteadOfFiringWithoutItsPin()
    {
        AudioRoom r = Room(16101);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human target = r.Dummy(8, LineY);
        // the CTF instakill rifle: still inert (gaps GU1 ported the captain's antique laser this test fired first)
        var laser = Held<obj_item_gun_energy_laser_instakill>(r, bob);
        Assert.True(r.In(() => InertSeam.IsInert(laser) && ispath(laser.pin)));

        object fired = r.In(() => Waiting.NoSleep(laser.try_fire_gun(target, bob, "icon-x=16;icon-y=16")));
        Assert.False(Dm.truthy(fired));
        Assert.Empty(r.In(() => Dm.each<obj_projectile>().ToList()));
        r.In(() => Waiting.NoSleep(laser.examine(bob)));
        r.In(() => qdel(laser));

        var ctfRifle = Held<obj_item_gun_energy_laser_instakill_ctf>(r, bob); // was the scatter shot laser, live since gaps GU1
        Assert.True(r.In(() => InertSeam.IsInert(ctfRifle)));
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(ctfRifle.attack_self(bob)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInertDrinkUsedInHandIsRefused()
    {
        AudioRoom r = Room(16102);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        // the Lavaland flight potion: still inert (the soda cans are ported now, the jerrycans since gaps OD1)
        var can = Held<obj_item_reagent_containers_cup_bottle_potion_flight>(r, bob);
        Assert.True(r.In(() => InertSeam.IsInert(can) && can.reagents == null));
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(can.attack_self(bob)))));
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(can.attack_self_secondary(bob)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnInertLaptopBrokenByABlastIsDeletedAsItIs()
    {
        AudioRoom r = Room(16103);
        var laptop = r.In(() => Dm.@new<obj_item_modular_computer_laptop>(locate(5, LineY, 1)));
        Assert.True(r.In(() => InertSeam.IsInert(laptop) && laptop.physical == null));
        r.In(() => Waiting.NoSleep(laptop.deconstruct(FALSE)));
        Assert.True(r.In(() => QDELETED(laptop)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClosetShakesOnlyForTheLivingInIt()
    {
        AudioRoom r = Room(16104);
        var closet = r.In(() => Dm.@new<obj_structure_closet>(locate(5, LineY, 1)));
        r.In(() => Dm.@new<obj_item_gun_energy_e_gun_hos>(closet)); // tg's for(var/mob/living/mob in contents) passes over it
        Assert.False(Dm.truthy(r.In(() => closet.check_if_shake())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWhipNullRodTakesItsMaterialsWithoutItsUntranslatedSlots()
    {
        AudioRoom r = Room(16105);
        var whip = r.In(() => Dm.@new<obj_item_nullrod_whip>(locate(5, LineY, 1)));
        Assert.True(r.In(() => length(whip.custom_materials) > 0));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStatPanelsCancelCameraViewRunsItsBody()
    {
        AudioRoom r = Room(16106);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        Assert.Contains(r.In(() => CommandVerbs.For(bob).ToList()), v => v.Path == "/mob/verb/cancel_camera");
        r.Command(() => ClientSeam.RunVerb(bob.client, "/mob/verb/cancel_camera"));
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATurfReplacedWithoutChangeTurfsTailStillNamesItsCell()
    {
        AudioRoom r = Room(16108);
        r.In(() =>
        {
            turf old = locate(5, LineY, 1);
            var replacement = Waiting.NoSleep(old.ChangeTurf(typeof(turf_open_lava_smooth), null, CHANGETURF_SKIP)) as turf;
            Assert.NotSame(old, replacement);
            Assert.Same(locate(5, LineY, 1), turf.dm_identity(old)); // spawn_rivers' SKIP: a stale reference reaches the river
        });
    }
}
