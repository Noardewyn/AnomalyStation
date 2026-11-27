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
/// The manned machine gun turrets (structures/deployable_turret.dm) in a booted toy room with Bob as a player: he buckles himself into
/// the heavy machine gun, his hands fill with its controls, a click through them turns the gun and him to the target and fires a
/// three-shot burst that hurts the man there, a click too soon after it only overheats the gun, and after the cooldown it fires again;
/// unbuckled, the controls go. A wrench folds the gun into the folded heavy machine gun in his hands, which deploys it again in front
/// of him (the deployable component). The machine gun turret fires its forty-shot burst until its gunner leaves it. Every newly live
/// type is made and deleted cleanly.
/// </summary>
public class DeployableTurretTests
{
    private const int LineY = 4, GunX = 4, ManX = 9;

    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(14, 7, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    // what the gunner heard of a sound (the dummies have no client)
    private static int Heard(AudioRoom r, mob who, string file) => r.Sounds.Of(who, file).Count;

    private static List<obj_item_gun_control> Controls(AudioRoom r, mob who) => r.In(() => Dm.each<obj_item_gun_control>(who.held_items).ToList());

    private static void Buckle(AudioRoom r, obj_machinery_deployable_turret gun, mob_living_carbon_human who)
    {
        r.In(() => Waiting.Detach(gun.user_buckle_mob(who, who)));
        r.Step(2);
        Assert.Same(gun, r.In(() => who.buckled));
    }

    [Fact]
    public void A_gunner_mans_the_heavy_machine_gun_fires_its_bursts_overheats_it_and_leaves_it()
    {
        var (r, feed, bob) = Room(14910);
        var gun = r.In(() => Dm.@new<obj_machinery_deployable_turret_hmg>(locate(GunX, LineY, 1)));
        mob_living_carbon_human man = r.Dummy(ManX, LineY);
        r.Step(2);

        // manning it (deployable_turret.dm:87): onto the gun's tile, both hands its controls, the mech's servo sound
        Buckle(r, gun, bob);
        Assert.Same(r.In(() => gun.loc), r.In(() => bob.loc));
        Assert.Equal(2, Controls(r, bob).Count);
        Assert.Equal(14f, r.In(() => Dm.num(bob.pixel_y)));
        Assert.Equal(SOUTH, r.In(() => Dm.num(gun.dir)));
        Assert.True(r.In(() => Dm.truthy(gun.anchored)));
        Assert.Equal(1, Heard(r, bob, "sound/vehicles/mecha/mechmove01.ogg"));

        // a click at the man: the gun and its gunner turn east, three shots of the HMG's sound, 20 brute each where they land
        obj_item_gun_control control = Controls(r, bob)[0];
        r.In(() => Waiting.Detach(control.ranged_interact_with_atom(man, bob)));
        Assert.Equal(EAST, r.In(() => Dm.num(gun.dir)));
        Assert.Equal(EAST, r.In(() => Dm.num(bob.dir)));
        Assert.Equal(-14f, r.In(() => Dm.num(bob.pixel_x)));
        r.Step(Second);
        Assert.Equal(3, Heard(r, bob, "sound/items/weapons/gun/hmg/hmg.ogg"));
        float hurt = r.In(() => Dm.num(man.get_brute_loss()));
        Assert.True(hurt > 0 && hurt <= 60.01f, "the burst hurt him by " + hurt);

        // a click before the two seconds' cooldown is over: no shot, the overheat's rack sound once
        r.In(() => Waiting.Detach(control.ranged_interact_with_atom(man, bob)));
        r.In(() => Waiting.Detach(control.ranged_interact_with_atom(man, bob)));
        r.Step(4);
        Assert.Equal(3, Heard(r, bob, "sound/items/weapons/gun/hmg/hmg.ogg"));
        Assert.Equal(1, Heard(r, bob, "sound/items/weapons/gun/smg/smgrack.ogg"));

        // after the cooldown the next burst
        r.Step(2 * Second);
        r.In(() => Waiting.Detach(control.ranged_interact_with_atom(man, bob)));
        r.Step(Second);
        Assert.Equal(6, Heard(r, bob, "sound/items/weapons/gun/hmg/hmg.ogg"));
        Assert.True(r.In(() => Dm.num(man.get_brute_loss())) > hurt, "the second burst hurt him more");

        // leaving it: the controls go, the offsets reset, the gun no longer anchored
        r.In(() => Waiting.Detach(gun.unbuckle_mob(bob, TRUE)));
        r.Step(2);
        Assert.Null(r.In(() => bob.buckled));
        Assert.Empty(Controls(r, bob));
        Assert.Equal(0f, r.In(() => Dm.num(bob.pixel_x)));
        Assert.Equal(0f, r.In(() => Dm.num(bob.pixel_y)));
        Assert.False(r.In(() => Dm.truthy(gun.anchored)));
        Assert.Equal(2, Heard(r, bob, "sound/vehicles/mecha/mechmove01.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wrench_folds_the_heavy_machine_gun_and_the_folded_gun_deploys_it_again()
    {
        var (r, feed, bob) = Room(14911);
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var gun = r.In(() => Dm.@new<obj_machinery_deployable_turret_hmg>(locate(GunX, LineY, 1)));
        var wrench = r.In(() => Dm.@new<obj_item_wrench>(get_turf(bob)));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(wrench)))));
        r.Step(2);

        // undeploying (deployable_turret.dm:53): three seconds, then the folded gun in his free hand, as worn as the gun was
        r.In(() => Waiting.Detach(gun.wrench_act(bob, wrench)));
        r.Step(4 * Second);
        Assert.True(feed.Any("balloon: undeploying..."));
        Assert.True(r.In(() => QDELETED(gun)));
        var folded = r.In(() => Dm.each<obj_item_deployable_turret_folded>(bob.held_items).FirstOrDefault());
        Assert.NotNull(folded);
        Assert.Equal(250f, r.In(() => Dm.num(folded.max_integrity)));
        var examine = r.In(() => Dm.each_cast<object>(Waiting.NoSleep(folded.examine(bob)) as DmList).Select(Dm.str).ToList());
        Assert.Contains(examine, line => line.Contains("It can be used <b>in hand</b> to deploy into a heavy machine gun turret.", StringComparison.Ordinal));

        // deploying it (components/deployable.dm:56): on the tile he faces, after five seconds, with the ratchet's sound
        r.In(() => Waiting.NoSleep(bob.setDir(EAST)));
        r.In(() => Waiting.Detach(folded.attack_self(bob)));
        r.Step(6 * Second);
        Assert.True(feed.Any("balloon: deploying..."));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/tools/ratchet.ogg");
        Assert.True(r.In(() => QDELETED(folded)));
        var redeployed = r.In(() => Dm.locate<obj_machinery_deployable_turret_hmg>(locate(4, LineY, 1)));
        Assert.NotNull(redeployed);
        Assert.Equal(EAST, r.In(() => Dm.num(redeployed.dir)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_machine_gun_turret_fires_its_long_burst_until_its_gunner_leaves()
    {
        var (r, feed, bob) = Room(14912);
        var gun = r.In(() => Dm.@new<obj_machinery_deployable_turret>(locate(GunX, LineY, 1)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(gun.anchored)));
        Buckle(r, gun, bob);
        Assert.True(r.In(() => Dm.truthy(gun.anchored)));
        obj_item_gun_control control = Controls(r, bob)[0];

        // a click at a far floor: one shot a tick, the SMG's sound each; the gunner gets out a second in, and the burst stops
        r.In(() => Waiting.Detach(control.ranged_interact_with_atom(locate(12, LineY, 1), bob)));
        r.Step(Second);
        int fired = Heard(r, bob, "sound/items/weapons/gun/smg/shot.ogg");
        Assert.InRange(fired, 5, 12);
        r.In(() => Waiting.Detach(gun.unbuckle_mob(bob, TRUE)));
        r.Step(5 * Second);
        Assert.Equal(fired, Heard(r, bob, "sound/items/weapons/gun/smg/shot.ogg"));

        // manned again after its nine seconds' cooldown: the whole forty-shot burst
        r.Step(5 * Second);
        Buckle(r, gun, bob);
        control = Controls(r, bob)[0];
        r.In(() => Waiting.Detach(control.ranged_interact_with_atom(locate(12, LineY, 1), bob)));
        r.Step(6 * Second);
        Assert.Equal(fired + 40, Heard(r, bob, "sound/items/weapons/gun/smg/shot.ogg"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_infinity_gun_fires_a_shot_a_click_and_a_lone_control_deletes_itself()
    {
        var (r, feed, bob) = Room(14913);
        var gun = r.In(() => Dm.@new<obj_machinery_deployable_turret_ultimate>(locate(GunX, LineY, 1)));
        r.Step(2);
        Buckle(r, gun, bob);
        obj_item_gun_control control = Controls(r, bob)[0];
        for (int i = 0; i < 3; i++)
            r.In(() => Waiting.Detach(control.ranged_interact_with_atom(locate(12, LineY, 1), bob)));
        r.Step(2);
        Assert.Equal(3, Heard(r, bob, "sound/items/weapons/gun/smg/shot.ogg"));
        Assert.True(r.In(() => Dm.truthy(control.CanItemAutoclick())));

        // the controls belong in a turret: one made anywhere else is deleted at its Initialize
        var lone = r.In(() => Dm.@new<obj_item_gun_control>(get_turf(bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(lone)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_newly_live_turret_and_trait_job_type_is_made_and_deleted_cleanly()
    {
        var r = new AudioRoom(13, 11, seed: 14914);
        r.In(() => (get_area(locate(6, 6, 1)) as area).requires_power = FALSE);
        Type[] types =
        {
            typeof(obj_machinery_deployable_turret), typeof(obj_machinery_deployable_turret_hmg), typeof(obj_machinery_deployable_turret_ultimate),
            typeof(obj_item_deployable_turret_folded), typeof(obj_item_secure_camera_console_pod), typeof(obj_item_paper_default_lawset_list),
            typeof(obj_machinery_computer_camera_advanced_human_ai),
        };
        var made = new List<atom>();
        foreach (Type type in types)
        {
            atom thing = r.In(() => (atom)Dm.@new(type, locate(6, 6, 1)));
            Assert.False(InertSeam.IsInert(thing), type.Name + " is live");
            made.Add(thing);
            r.Step(2);
        }
        r.Step(Second);
        r.In(() =>
        {
            foreach (atom thing in made)
                if (!QDELETED(thing))
                    qdel(thing);
        });
        r.Step(2 * Second);
        Assert.All(made, thing => Assert.True(QDELETED(thing), thing.GetType().Name + " was deleted"));
        r.AssertNoRuntimes();
    }
}
