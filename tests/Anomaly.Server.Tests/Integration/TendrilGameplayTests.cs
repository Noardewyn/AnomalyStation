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
/// Through the real server: a joined miner hacks a worn-down necropolis tendril apart with a kinetic crusher, so it dies into its
/// locked chest; he unlocks the chest with a skeleton key, opens it and finds its loot inside.
/// </summary>
public class TendrilGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget At(datum d) => new ClickTarget(TargetKind.Entity, d.dm_id, 0, 0, 0, 16, 16);

    [Fact]
    public void A_miner_destroys_a_tendril_and_opens_its_chest_with_a_skeleton_key()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13490, TimeoutSteps = 10000 });
        RealHeadlessWorld.Player p = w.Join("Miner");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        Do(w, () => DevCommands.Give(me, "/obj/item/kinetic_crusher"));
        w.Run(5);
        var crusher = In(w, () => me.get_active_held_item()) as obj_item_kinetic_crusher;
        Assert.NotNull(crusher);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second / 2);
        Assert.True(In(w, () => HAS_TRAIT(crusher, TRAIT_WIELDED)), "the crusher is wielded");

        // a tendril east of the miner, its mind off and worn down to a short fight (its lash and spikes would make it luck)
        var tendril = In(w, () =>
        {
            var t = Dm.@new<mob_living_basic_mining_tendril>(get_step(get_step(me, EAST), EAST));
            Waiting.NoSleep((t.ai_controller as datum_ai_controller).set_ai_status(AI_STATUS_OFF));
            t.maxHealth = 100;
            t.health = 100;
            return t;
        });
        w.Run(5);
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(tendril, WEST))));
        p.Session.ToggleCombatMode();
        w.Run(5);
        for (int i = 0; i < 120 && In(w, () => !QDELETED(tendril) && tendril.stat != DEAD); i++)
        {
            p.Session.Click(At(tendril), ClickButton.Left, ClickModifiers.None);
            w.Run(Second);
        }
        Assert.True(In(w, () => QDELETED(tendril) || tendril.stat == DEAD), "the tendril fell");
        w.Run(Second);

        // the chest it left is locked; the key opens it
        var chest = In(w, () => Dm.each<obj_structure_closet_crate_necropolis_tendril>(range(3, me)).FirstOrDefault());
        Assert.NotNull(chest);
        p.Session.ToggleCombatMode();
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(crusher)));
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(chest, WEST))));
        w.Run(5);
        p.Session.Click(At(chest), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(chest.opened)), "the chest is locked");

        Do(w, () => DevCommands.Give(me, "/obj/item/skeleton_key"));
        w.Run(5);
        Assert.True(In(w, () => me.get_active_held_item() is obj_item_skeleton_key), "the miner holds the key");
        p.Session.Click(At(chest), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(chest.spawned_loot)), "the key disabled the lock");
        p.Session.Click(At(chest), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(chest.opened)), "the chest opened");
        var loot = In(w, () => Dm.each<atom_movable>(chest.loc).Where(a => a is obj_item || a is obj_structure_closet_crate_necropolis_puzzle).Select(a => a.name).ToList());
        output.WriteLine($"the chest held {string.Join(", ", loot)}");
        Assert.NotEmpty(loot);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
