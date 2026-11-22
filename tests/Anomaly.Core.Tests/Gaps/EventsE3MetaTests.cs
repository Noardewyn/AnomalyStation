using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package E3 (D327): the shuttle catastrophe on MetaStation. The emergency shuttle at CentCom is unloaded and a purchasable
/// template is loaded in its place at its dock (DV188: the mounted purchasable template is Meta's own), and no shuttle can be bought
/// after it; the corpse spawners on the mounted levels leave their bodies at the boot (Lavaland's miner).
/// </summary>
public class EventsE3MetaTests
{
    private readonly ITestOutputHelper _output;

    public EventsE3MetaTests(ITestOutputHelper output) => _output = output;

    [MetaFact]
    public void The_shuttle_catastrophe_replaces_the_emergency_shuttle_at_its_dock()
    {
        World world = MetaMaps.Boot(out TgTestWorld.Log log);
        int bootErrors = log.Runtimes.Count(e => !e.Message.Contains("not translated yet"));
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        obj_docking_port_mobile_emergency old;
        object dock;
        using (world.Enter())
        {
            // the boot's live corpse spawners made their bodies and left (CentCom's intern, whose own special() waits, stays inert), but
            // Lavaland's skeleton: a skeleton's random name reads its species' language holder, which DV25 leaves out of the prototypes
            // (an open question for the lead, gaps E3's report)
            var left = Dm.each<obj_effect_mob_spawn_corpse>(Dm.world).Where(s => !InertSeam.IsInert(s) && s is not obj_effect_mob_spawn_corpse_human_skeleton).ToList();
            Assert.True(left.Count == 0, string.Join("; ", left.Select(s => $"{s.type} at {s.x},{s.y},{s.z}")));
            Assert.NotEmpty(Dm.each<mob_living_carbon_human>(Dm.world).Where(h => h.z == 3 && h.stat == DEAD && Dm.eq(h.job, JOB_SHAFT_MINER)).ToList()); // Lavaland's miner
            old = SSshuttle.emergency;
            dock = old.get_docked();
            Assert.NotNull(dock);
            var control = (datum_round_event_control)Dm.each<datum_round_event_control>(SSevents.control).First(c => c is datum_round_event_control_shuttle_catastrophe);
            Waiting.Detach(control.run_event());
        }
        for (int i = 0; i < 400; i++)
        {
            using (world.Enter())
            {
                if (SSshuttle.emergency != old && SSshuttle.emergency != null)
                    break;
            }
            world.Step();
        }
        using (world.Enter())
        {
            Assert.Equal((float)SHUTTLEPURCHASE_FORCED, SSshuttle.shuttle_purchased);
            Assert.NotSame(old, SSshuttle.emergency);
            Assert.True(QDELETED(old), "the old shuttle is gone");
            Assert.Same(dock, SSshuttle.emergency.get_docked());
            List<string> errors = log.Runtimes.Where(e => !e.Message.Contains("not translated yet")).Skip(bootErrors).Select(e => e.ToString().Split('\n')[0]).ToList();
            _output.WriteLine($"boot errors {bootErrors}; after {errors.Count}");
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
    }
}
