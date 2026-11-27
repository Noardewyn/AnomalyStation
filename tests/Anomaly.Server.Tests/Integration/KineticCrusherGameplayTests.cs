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
/// Through the real server: a joined miner wields a proto-kinetic crusher, marks a goliath with its right click, detonates the
/// mark with his swings until the goliath dies, the crusher having dealt the damage that earns its trophy, and heals himself with a
/// regenerative core.
/// </summary>
public class KineticCrusherGameplayTests(ITestOutputHelper output)
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

    [Fact]
    public void A_miner_kills_a_goliath_with_a_crusher_and_heals_with_a_regenerative_core()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13161, TimeoutSteps = 8000 });
        RealHeadlessWorld.Player p = w.Join("Miner");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the crusher in hand, wielded with the in-hand key
        Do(w, () => DevCommands.Give(me, "/obj/item/kinetic_crusher"));
        w.Run(5);
        var crusher = In(w, () => me.get_active_held_item()) as obj_item_kinetic_crusher;
        Assert.NotNull(crusher);
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second / 2);
        Assert.True(In(w, () => HAS_TRAIT(crusher, TRAIT_WIELDED)), "the crusher is wielded");

        // a goliath two tiles east, still (its tentacles would make the fight a matter of luck)
        var goliath = In(w, () =>
        {
            var g = Dm.@new<mob_living_basic_mining_goliath>(get_step(get_step(me, EAST), EAST));
            g.ai_controller = null;
            return g;
        });
        w.Run(5);

        // the right click fires the destabilizer, which marks the goliath
        p.Session.Click(new ClickTarget(TargetKind.Entity, goliath.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None);
        w.Run(Second);
        Assert.False(In(w, () => Dm.truthy(crusher.charged)) && In(w, () => goliath.has_status_effect(typeof(datum_status_effect_crusher_mark)) == null),
            "the crusher fired and marked the goliath");

        // step next to it and fight: mark, swing, until it dies
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(goliath, WEST))));
        p.Session.ToggleCombatMode();
        w.Run(5);
        for (int i = 0; i < 80 && In(w, () => goliath.stat != DEAD); i++)
        {
            if (In(w, () => Dm.truthy(crusher.charged)))
            {
                p.Session.Click(new ClickTarget(TargetKind.Entity, goliath.dm_id, 0, 0, 0, 16, 16), ClickButton.Right, ClickModifiers.None);
                w.Run(Second);
            }
            p.Session.Click(new ClickTarget(TargetKind.Entity, goliath.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None);
            w.Run(Second);
        }
        Assert.Equal(DEAD, In(w, () => goliath.stat));
        float crusherShare = In(w, () => (goliath.has_status_effect(typeof(datum_status_effect_crusher_damage)) as datum_status_effect_crusher_damage)?.total_damage ?? 0);
        output.WriteLine($"the crusher dealt {crusherShare} of the goliath's {In(w, () => Dm.num(goliath.maxHealth))}");
        Assert.True(crusherShare >= In(w, () => Dm.num(goliath.maxHealth)) * 0.9f, "the crusher did the work");

        // hurt, the miner smears a legion core on himself
        Do(w, () =>
        {
            Waiting.NoSleep(me.adjust_brute_loss(60));
            Waiting.NoSleep(me.dropItemToGround(crusher));
            DevCommands.Give(me, "/obj/item/organ/monster_core/regenerative_core/legion");
        });
        w.Run(5);
        float hurt = In(w, () => Dm.num(me.get_brute_loss()));
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.num(me.get_brute_loss())) < hurt, "the core healed him");
        Assert.NotNull(In(w, () => me.has_status_effect(typeof(datum_status_effect_regenerative_core))));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
