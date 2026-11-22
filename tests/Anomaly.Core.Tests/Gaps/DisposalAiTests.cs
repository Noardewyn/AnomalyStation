using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// D325: tg's stuff_in_disposal leaf (generic_behaviors/stuff_in_disposal.dm), which the monkeys' and the bar gorilla's combat trees run
/// on a downed enemy beside a bin: after tg's 2 s stuff_mob_in the enemy is in the bin and the leaf pulls the flush.
/// </summary>
public class DisposalAiTests
{
    [Fact]
    public void AMonkeyStuffsItsDownedEnemyIntoTheBinAndFlushesIt()
    {
        var r = new AudioRoom(13, 9, seed: 3251);
        mob_living_carbon_human bob = r.Join(10, 4, "Bob");
        var bin = r.In(() => Dm.@new<obj_machinery_disposal_bin>(locate(5, 4, 1)));
        var monkey = r.In(() => Dm.@new<mob_living_carbon_human_species_monkey>(locate(4, 4, 1)));
        // only this leaf runs: the monkey's own tree would walk it off mid do_after (a random walk). tg's force_ai_off, not a bare
        // set_ai_status(AI_STATUS_OFF), which the monkey's Life (update_z) recalculates back on
        var controller = r.In(() => monkey.ai_controller as datum_ai_controller);
        r.In(() => Waiting.NoSleep(controller.force_ai_off()));
        var victim = r.In(() => Dm.@new<mob_living_carbon_human>(locate(4, 5, 1)));
        r.Step(20);
        var leaf = r.In(() =>
        {
            var node = Dm.@new<datum_bt_node_ai_behavior_stuff_in_disposal>();
            node.attack_target_key = "test_target";
            node.disposal_target_key = "test_disposal";
            node.owning_controller = controller;
            controller.blackboard["test_target"] = victim;
            controller.blackboard["test_disposal"] = bin;
            return node;
        });
        r.Sounds.Heard.Clear();
        object started = r.In(() => leaf.perform(0.1f, controller));
        Assert.True(Dm.truthy(Dm.bitand(started, AI_BEHAVIOR_DELAY)));
        bool inBin = false;
        for (int i = 0; i < 20 * 5; i++)
        {
            r.Step(1);
            inBin |= r.In(() => victim.loc == bin);
        }
        Assert.True(inBin, "stuff_mob_in put the victim in the bin after its do_after");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/machines/disposalflush.ogg");
        Assert.True(r.In(() => Dm.truthy(leaf.async_finished)));
        r.AssertNoRuntimes();
    }
}
