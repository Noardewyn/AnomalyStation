using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M10Shuttles;

/// <summary>
/// The owner (2026-11-08): tg's crew-death shuttle call (SSshuttle.CheckAutoEvac, shuttle.dm:253) on MetaStation. With the
/// threshold at 0.2 (the host's EMERGENCY_SHUTTLE_AUTOCALL_THRESHOLD), a round whose joined players are all dead calls the emergency
/// shuttle at 2.5 times its time and blocks the recall; while more than a fifth live, nothing happens.
/// </summary>
public class AutoEvacTests
{
    [MetaFact]
    public void Crew_death_below_the_threshold_calls_the_shuttle_and_blocks_the_recall()
    {
        World world = MetaMaps.Boot(out _);
        TgTestWorld.RunLevel(world, RUNLEVEL_GAME);
        using (world.Enter())
        {
            Waiting.NoSleep(config.Set(typeof(datum_config_entry_number_emergency_shuttle_autocall_threshold), 0.2f));
            SSticker.current_state = GAME_STATE_PLAYING;
            obj_docking_port_mobile_emergency shuttle = SSshuttle.emergency;
            Assert.True(datum_controller_subsystem_shuttle.EMERGENCY_IDLE_OR_RECALLED());

            // five joined, one alive (a fifth, at the threshold: tg's <=) calls it; two alive would not
            mob_living_carbon_human[] crew = Enumerable.Range(0, 5).Select(_ => Dm.@new<mob_living_carbon_human>(locate(100, 100, 1))).ToArray();
            for (int i = 0; i < crew.Length; i++)
            {
                crew[i].ckey = "crew" + i;
                Dm.append(ref GLOB.joined_player_list, crew[i].ckey);
                Dm.append(ref GLOB.player_list, crew[i]);
            }
            for (int i = 0; i < 3; i++)
                crew[i].stat = DEAD;
            SSshuttle.CheckAutoEvac();
            Assert.True(datum_controller_subsystem_shuttle.EMERGENCY_IDLE_OR_RECALLED(), "two of five alive is above a fifth");

            crew[3].stat = DEAD;
            SSshuttle.CheckAutoEvac();
            Assert.Equal(SHUTTLE_CALL, shuttle.mode);
            Assert.True(Dm.truthy(SSshuttle.emergency_no_recall));
            Assert.Equal(SSshuttle.emergency_call_time * ALERT_COEFF_AUTOEVAC_CRITICAL, Dm.num(shuttle.timeLeft(1)), 1f);
        }
    }
}
