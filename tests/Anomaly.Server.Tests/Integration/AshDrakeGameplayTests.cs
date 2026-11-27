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
/// Through the real server: a joined miner takes a wielded proto-kinetic crusher to a worn-down ash drake, marks it and detonates the
/// marks until it falls, so the drake leaves the crusher's chest; he opens the chest and takes out the drake's tail spike.
/// </summary>
public class AshDrakeGameplayTests(ITestOutputHelper output)
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
    public void A_miner_brings_down_an_ash_drake_with_a_crusher_and_takes_its_tail_spike_from_the_chest()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13361, TimeoutSteps = 10000 });
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

        // a drake two tiles east, its mind off and worn down to a crusher's fight (its flames and swoops would make it luck)
        var drake = In(w, () =>
        {
            var d = Dm.@new<mob_living_simple_animal_hostile_megafauna_dragon>(get_step(get_step(me, EAST), EAST));
            d.toggle_ai(AI_OFF);
            d.maxHealth = 150;
            d.health = 150;
            return d;
        });
        w.Run(5);

        p.Session.Click(At(drake), ClickButton.Right, ClickModifiers.None);
        w.Run(Second);
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(drake, WEST))));
        p.Session.ToggleCombatMode();
        w.Run(5);
        for (int i = 0; i < 120 && In(w, () => drake.stat != DEAD && !QDELETED(drake)); i++)
        {
            if (In(w, () => Dm.truthy(crusher.charged)))
            {
                p.Session.Click(At(drake), ClickButton.Right, ClickModifiers.None);
                w.Run(Second);
            }
            p.Session.Click(At(drake), ClickButton.Left, ClickModifiers.None);
            w.Run(Second);
        }
        Assert.True(In(w, () => drake.stat == DEAD || QDELETED(drake)), "the drake fell");
        w.Run(Second);

        // the crusher's kill leaves the chest with the trophy; the miner puts the crusher down, opens the chest and takes the spike
        var chest = In(w, () => Dm.each<obj_structure_closet_crate_necropolis_dragon_crusher>(range(2, me)).FirstOrDefault());
        Assert.NotNull(chest);
        p.Session.ToggleCombatMode();
        Do(w, () => Waiting.NoSleep(me.dropItemToGround(crusher)));
        Do(w, () => Waiting.NoSleep(me.forceMove(get_step(chest, WEST))));
        w.Run(5);
        p.Session.Click(At(chest), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(chest.opened)), "the chest opened");
        var spike = In(w, () => Dm.each<obj_item_crusher_trophy_tail_spike>(range(1, chest)).FirstOrDefault());
        Assert.NotNull(spike);
        p.Session.Click(At(spike), ClickButton.Left, ClickModifiers.None);
        w.Run(Second);
        output.WriteLine($"the miner holds {In(w, () => (me.get_active_held_item() as obj_item)?.name)}");
        Assert.True(Dm.truthy(In(w, () => me.is_holding(spike))), "the miner took the spike");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
