using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV602: an alien is drawn as one icon, so the client sends no zone for a click, a throw or a hover on it. A click or a throw on it while the
/// zone selected names a part it lacks (a human's leg looked at before, on a larva) counts as aimed at the chest, where tg's
/// <c>aimed_limb_missing</c> refused the hit; a zone it has (a larva's head, an adult's leg) stands as tg would use it. A hover never changes the
/// zone, and a zoneless click on a human or on a thing that is no carbon leaves it alone.
/// </summary>
public class AimZoneOneIconGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom target, AimZone zone) => new(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 20, zone);

    // The zone picked on tg's zone selector (cursor mode's pick on the HUD), with tg's log
    private static void Select(RealHeadlessWorld w, mob who, string zone)
        => In(w, () => ((atom_movable_screen_zone_sel)who.hud_used.screen_objects[HUD_MOB_ZONE_SELECTOR]).set_selected_zone(zone, who));

    private sealed record Rig(RealHeadlessWorld W, RealHeadlessWorld.Player Alice, mob_living_carbon_human AliceMob, mob_living_carbon_human BobMob,
        mob_living_carbon_alien Alien, List<string> Texts, List<string> Balloons);

    // Alice with Bob east of her and an alien west of her (its AI off), Alice's hands empty, out of combat mode; the chat lines and the balloons
    // Alice's client receives.
    private static Rig Scene(ulong seed, Type alienType)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        mob_living_carbon_alien alien = null;
        In(w, () =>
        {
            Waiting.NoSleep(bobMob.forceMove(get_step(aliceMob, Dm.EAST)));
            alien = (mob_living_carbon_alien)DevCommands.Spawn(alienType, get_step(aliceMob, Dm.WEST) as turf, 1, 8, out _).Single();
            if (alien.ai_controller is datum ai)
                qdel(ai);
        });
        var texts = new List<string>();
        var balloons = new List<string>();
        alice.Client.ServerTextReceived += t => (t.Type == "balloon" ? balloons : texts).Add(t.Html);
        w.Run(Second);
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));
        Assert.False(In(w, () => Dm.truthy(aliceMob.combat_mode)));
        return new Rig(w, alice, aliceMob, bobMob, alien, texts, balloons);
    }

    private static void ArmWithToolbox(Rig r)
    {
        r.W.Host.EnqueueConsoleLine("give Alice /obj/item/storage/toolbox/mechanical");
        r.Alice.Session.ToggleCombatMode();
        r.W.Run(10);
        Assert.True(In(r.W, () => Dm.truthy(r.AliceMob.combat_mode)));
        Assert.IsAssignableFrom<obj_item_storage_toolbox>(In(r.W, () => r.AliceMob.get_active_held_item()));
    }

    // A zoneless toolbox hit on the alien that lands: its health drops and Alice reads tg's hit line naming the part
    private void HitLands(Rig r, string part)
    {
        float before = In(r.W, () => Dm.num(r.Alien.health));
        r.Texts.Clear();
        Assert.True(r.Alice.Session.Click(On(r.Alien, AimZone.None), ClickButton.Left, ClickModifiers.None));
        r.W.Run(Second);
        float after = In(r.W, () => Dm.num(r.Alien.health));
        output.WriteLine($"health {before} -> {after}; {string.Join(" | ", r.Texts)}");
        Assert.True(after < before, $"the hit landed ({before} -> {after})");
        Assert.Contains(r.Texts, t => t.Contains($"in the {part} with the mechanical toolbox", StringComparison.Ordinal));
    }

    [Fact]
    public void After_a_look_at_a_humans_left_leg_toolbox_hits_on_a_larva_land_on_its_chest()
    {
        Rig r = Scene(71, typeof(mob_living_carbon_alien_larva));
        using (r.W)
        {
            Assert.False(In(r.W, () => Dm.truthy(r.Alien.get_bodypart(BODY_ZONE_L_LEG)))); // what tg's aimed_limb_missing refused the hit for
            ArmWithToolbox(r);
            for (int i = 0; i < 3; i++)
            {
                Assert.True(r.Alice.Session.Hover(On(r.BobMob, AimZone.LeftLeg), r.W.Now + i));
                r.W.Run(4);
                Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));
                HitLands(r, "chest");
                Assert.Equal(BODY_ZONE_CHEST, In(r.W, () => r.AliceMob.zone_selected));
                if (In(r.W, () => Dm.num(r.Alien.stat)) == DEAD)
                    break;
            }
            Assert.DoesNotContain(r.Balloons, b => b.StartsWith("no ", StringComparison.Ordinal)); // tg's refusal, "no left leg!"
        }
    }

    [Fact]
    public void With_the_head_selected_a_hit_on_a_larva_lands_on_its_head()
    {
        Rig r = Scene(72, typeof(mob_living_carbon_alien_larva));
        using (r.W)
        {
            Assert.True(In(r.W, () => Dm.truthy(r.Alien.get_bodypart(BODY_ZONE_HEAD))));
            ArmWithToolbox(r);
            Select(r.W, r.AliceMob, BODY_ZONE_HEAD);
            HitLands(r, "head");
            Assert.Equal(BODY_ZONE_HEAD, In(r.W, () => r.AliceMob.zone_selected));
        }
    }

    [Fact]
    public void With_the_left_leg_selected_a_hit_on_an_adult_alien_lands_on_its_left_leg()
    {
        Rig r = Scene(73, typeof(mob_living_carbon_alien_adult_drone));
        using (r.W)
        {
            Assert.True(In(r.W, () => Dm.truthy(r.Alien.get_bodypart(BODY_ZONE_L_LEG))));
            ArmWithToolbox(r);
            Select(r.W, r.AliceMob, BODY_ZONE_L_LEG);
            HitLands(r, "left leg");
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));
        }
    }

    [Fact]
    public void A_zoneless_hover_on_a_larva_leaves_the_left_leg_with_and_without_a_do_after()
    {
        Rig r = Scene(74, typeof(mob_living_carbon_alien_larva));
        using (r.W)
        {
            Session s = r.W.Host.SessionOf(1);
            Assert.True(r.Alice.Session.Hover(On(r.BobMob, AimZone.LeftLeg), r.W.Now));
            r.W.Run(4);
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));

            int applied = s.HoversApplied;
            Assert.True(r.Alice.Session.Hover(On(r.Alien, AimZone.None), r.W.Now + 1));
            r.W.Run(4);
            Assert.Equal(applied + 1, s.HoversApplied);
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));

            // during tg's do-after and after it ends
            Assert.True(r.Alice.Session.Hover(On(r.BobMob, AimZone.LeftLeg), r.W.Now + 2));
            r.W.Run(4);
            In(r.W, () => Waiting.Detach(do_after(r.AliceMob, 40, r.BobMob)));
            Assert.True(In(r.W, () => LAZYLEN(r.AliceMob.do_afters)) > 0);
            applied = s.HoversApplied;
            Assert.True(r.Alice.Session.Hover(On(r.Alien, AimZone.None), r.W.Now + 3));
            r.W.Run(4);
            Assert.Equal(applied + 1, s.HoversApplied);
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));
            r.W.Run(5 * Second);
            Assert.Equal(0f, In(r.W, () => LAZYLEN(r.AliceMob.do_afters)));
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));
        }
    }

    [Fact]
    public void A_zoneless_throw_at_a_larva_aims_at_the_chest_for_a_leg_and_keeps_the_head()
    {
        Rig r = Scene(75, typeof(mob_living_carbon_alien_larva));
        using (r.W)
        {
            foreach ((string selected, string expected) in new[] { (BODY_ZONE_L_LEG, BODY_ZONE_CHEST), (BODY_ZONE_HEAD, BODY_ZONE_HEAD) })
            {
                In(r.W, () => DevCommands.Give(r.AliceMob, "/obj/item/pen"));
                r.W.Run(Second);
                Assert.IsAssignableFrom<obj_item_pen>(In(r.W, () => r.AliceMob.get_active_held_item()));
                Select(r.W, r.AliceMob, selected);
                Assert.True(r.Alice.Session.Throw(On(r.Alien, AimZone.None), null));
                r.W.Run(Second);
                Assert.Null(In(r.W, () => r.AliceMob.get_active_held_item())); // the throw was applied: the pen left the hand
                Assert.Equal(expected, In(r.W, () => r.AliceMob.zone_selected));
            }
        }
    }

    [Fact]
    public void A_zoneless_click_on_a_locker_or_a_human_is_applied_and_leaves_the_zone()
    {
        Rig r = Scene(76, typeof(mob_living_carbon_alien_larva));
        using (r.W)
        {
            Session s = r.W.Host.SessionOf(1);
            var locker = In(r.W, () => Dm.@new<obj_structure_closet>(get_step(r.AliceMob, Dm.NORTH)));
            r.W.Run(2);
            Assert.True(r.Alice.Session.Hover(On(r.BobMob, AimZone.LeftLeg), r.W.Now));
            r.W.Run(4);
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));
            int rejected = s.ClicksRejected;

            // the locker: tg's attack_hand opens it
            Assert.False(In(r.W, () => Dm.truthy(locker.opened)));
            int actions = s.ActionsApplied;
            Assert.True(r.Alice.Session.Click(On(locker, AimZone.None), ClickButton.Left, ClickModifiers.None));
            r.W.Run(Second);
            Assert.Equal(actions + 1, s.ActionsApplied);
            Assert.True(In(r.W, () => Dm.truthy(locker.opened)));
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));

            // Bob, with no zone (no pixel of a body part): tg's help on him
            r.Texts.Clear();
            actions = s.ActionsApplied;
            Assert.True(r.Alice.Session.Click(new ClickTarget(TargetKind.Entity, r.BobMob.dm_id, 0, 0, 0, 0, 0), ClickButton.Left, ClickModifiers.None));
            r.W.Run(Second);
            Assert.Equal(actions + 1, s.ActionsApplied);
            output.WriteLine(string.Join(" | ", r.Texts));
            Assert.NotEmpty(r.Texts);
            Assert.Equal(BODY_ZONE_L_LEG, In(r.W, () => r.AliceMob.zone_selected));
            Assert.Equal(rejected, s.ClicksRejected);
        }
    }
}
