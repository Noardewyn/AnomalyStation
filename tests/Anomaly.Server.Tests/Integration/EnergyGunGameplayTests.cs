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
/// M7x slice B (the gun core and the energy guns, docs/ai/specs/weapons.md 3) on a real server with two players: Alice, dressed as tg's
/// security officer (the vest, the disabler hanging on it in suit storage; the outfit's items are made by hand, because the outfit's
/// mindshield implant is not translated before M8), draws the disabler from her suit storage with a click on it, shoots Bob by
/// clicks (the beam is heard by Bob's client, each hit costs 30 stamina and a twentieth of the cell, the fourth takes him to stamina
/// crit and he lies on his client), and a recharger charges the spent disabler and shows its states to the client.
/// </summary>
public class EnergyGunGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static void Click(RealHeadlessWorld.Player p, atom target)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // The first direction with `count` clear tiles in a row from the mob: its tiles, nearest first.
    private static List<turf> Line(RealHeadlessWorld w, mob me, int count) => In(w, () =>
    {
        foreach (int dir in new[] { EAST, WEST, NORTH, SOUTH })
        {
            var line = new List<turf>();
            turf at = get_turf(me);
            for (int i = 0; i < count; i++)
            {
                at = get_step(at, dir);
                if (at == null || !Clear(at))
                    break;
                line.Add(at);
            }
            if (line.Count == count)
                return line;
        }
        return null;
    });

    [Fact]
    public void An_officer_draws_the_disabler_from_suit_storage_disables_a_man_by_clicks_and_a_recharger_charges_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 21, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var heard = new List<SoundEvent>();
        var heardAlice = new List<SoundEvent>();
        bob.Session.Replica.SoundsReceived += events => heard.AddRange(events);
        alice.Session.Replica.SoundsReceived += events => heardAlice.AddRange(events);
        w.Run(10);

        // --- the officer's kit: the vest, and the disabler hanging on it (the outfit's suit and suit_store)
        obj_item_clothing_suit_armor_vest_alt_sec vest = null;
        obj_item_gun_energy_disabler disabler = null;
        In(w, () =>
        {
            vest = Dm.@new<obj_item_clothing_suit_armor_vest_alt_sec>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.equip_to_slot_or_del(vest, ITEM_SLOT_OCLOTHING));
            disabler = Dm.@new<obj_item_gun_energy_disabler>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.equip_to_slot_or_del(disabler, ITEM_SLOT_SUITSTORE));
        });
        w.Run(Second);
        Assert.Same(vest, In(w, () => aliceMob.wear_suit));
        Assert.Same(disabler, In(w, () => aliceMob.s_store));
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));
        float full = In(w, () => disabler.cell.maxcharge);
        Assert.Equal(full, In(w, () => disabler.cell.charge));

        // --- she draws it: a click on the gun in its slot with the empty hand
        bool Drawn() => alice.Session.Replica.Screen.Values.Any(e => e.VisChildren != null && e.VisChildren.Contains(disabler.dm_id));
        if (!Drawn())
        {
            // the suit storage slot is in the toggleable inventory (human.dm:15): open it with its button, then the gun drawn in its slot is a click target
            long toggle = In(w, () => ((atom)aliceMob.hud_used.screen_objects[HUD_HUMAN_TOGGLE_INVENTORY]).dm_id);
            Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, toggle, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
        }
        Assert.True(Drawn(), "the gun is drawn in its slot");
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Screen, disabler.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(disabler, In(w, () => aliceMob.get_active_held_item()));
        Assert.Null(In(w, () => aliceMob.s_store));

        // --- Bob stands four tiles away in the open; Alice clicks him until he drops (tg's number of 30-point hits)
        List<turf> line = Line(w, aliceMob, 4);
        Assert.NotNull(line);
        In(w, () => Waiting.NoSleep(bobMob.forceMove(line[3])));
        w.Run(Second);
        float maxHealth = In(w, () => bobMob.maxHealth);
        float crit = In(w, () => bobMob.crit_threshold);
        int expected = (int)Math.Ceiling((maxHealth - crit) / 30f);
        float stamina = In(w, () => Dm.num(bobMob.get_stamina_loss()));
        int hits = 0;
        for (; hits < 8 && !In(w, () => HAS_TRAIT_FROM(bobMob, TRAIT_INCAPACITATED, STAMINA)); hits++)
        {
            heard.Clear();
            Click(alice, bobMob);
            w.Run(3 * Second);
            float now = In(w, () => Dm.num(bobMob.get_stamina_loss()));
            output.WriteLine($"shot {hits + 1}: Bob's stamina damage {stamina} -> {now}, the cell {In(w, () => disabler.cell.charge)}/{full}");
            Assert.Equal(stamina + 30, now, 0.01);
            stamina = now;
            Assert.Contains(heard, e => e.File == "sound/items/weapons/taser2.ogg");
        }
        Assert.Equal(expected, hits);
        Assert.Equal(1f - hits / 20f, In(w, () => disabler.cell.charge / disabler.cell.maxcharge), 0.001);
        Assert.True(In(w, () => HAS_TRAIT_FROM(bobMob, TRAIT_INCAPACITATED, STAMINA)), "stamina crit");
        w.Run(Second);
        Assert.True(PostureBits.IsLying(bob.Session.Replica.Entities.Single(e => e.Id == bobMob.dm_id).State.Posture), "Bob's client sees him lying");
        // the gun's picture follows its charge: the bars of a cell with 16 shots left (the client sees the held gun's overlays)
        Assert.Equal(16f, In(w, () => Dm.num(disabler.cell.charge / (disabler.cell.maxcharge / 20f))), 0.01);

        // --- a recharger beside her: the spent disabler goes in with a click and the cell fills by tg's cycles
        In(w, () => ((area)get_area(aliceMob)).requires_power = FALSE); // the sandbox has no APC: free power, as use_energy gives shuttles
        obj_machinery_recharger charger = In(w, () => Dm.@new<obj_machinery_recharger>(get_step(aliceMob, line[0] == get_step(aliceMob, EAST) ? NORTH : EAST)));
        In(w, () => Waiting.NoSleep(charger.set_anchored(TRUE)));
        w.Run(2 * Second);
        float before = In(w, () => disabler.cell.charge);
        heardAlice.Clear();
        Click(alice, charger);
        w.Run(Second);
        Assert.Same(disabler, In(w, () => charger.charging));
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));
        ReplicaEntity chargerSeen = alice.Session.Replica.Entities.Single(e => e.Id == charger.dm_id);
        List<string> states = CrewOutfitTests.Icons(alice.Session.Replica, chargerSeen.State.Appearance).Select(i => i.State).ToList();
        output.WriteLine("the recharger as the client sees it: " + string.Join(", ", states));
        Assert.Contains("recharger-charging", states);
        w.Run(6 * Second);
        float after = In(w, () => disabler.cell.charge);
        float step = In(w, () => disabler.cell.chargerate * charger.recharge_coeff * Dm.num(SSmachines.wait) / 10f);
        output.WriteLine($"the cell {before} -> {after} in six seconds (a cycle gives {step})");
        Assert.True(after > before, "the recharger charges the gun");
        Assert.Equal(0f, (after - before) % step, 0.01);

        // it fills up and pings; the client hears it and sees the full state
        for (int i = 0; i < 60 && In(w, () => disabler.cell.charge) < full; i++)
            w.Run(2 * Second);
        Assert.Equal(full, In(w, () => disabler.cell.charge));
        w.Run(Second);
        chargerSeen = alice.Session.Replica.Entities.Single(e => e.Id == charger.dm_id);
        states = CrewOutfitTests.Icons(alice.Session.Replica, chargerSeen.State.Appearance).Select(i => i.State).ToList();
        Assert.Contains("recharger-full", states);
        Assert.DoesNotContain("recharger-charging", states);
        Assert.Contains(heardAlice, e => e.File == "sound/machines/ping.ogg");

        // the hand on the recharger takes the gun out
        Click(alice, charger);
        w.Run(Second);
        Assert.Null(In(w, () => charger.charging));
        Assert.Same(disabler, In(w, () => aliceMob.get_active_held_item()));
        output.WriteLine("recharged ok");
    }
}
