using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package T, slice T4a (protocol 99): a click aimed at the eyes, the mouth or the groin is recorded in the command log with its zone (a plain byte,
/// 7 to 9) and replayed into a fresh world to the same state, where tg's selected zone is that precise zone again (D31).
/// </summary>
public class PreciseZoneReplayTests
{
    [Fact]
    public void A_click_with_a_precise_zone_is_recorded_and_replayed_to_the_same_hash_and_zone()
    {
        CommandLog log;
        ulong hash;
        long end;
        using (var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 400 }))
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            RealHeadlessWorld.Player bob = w.Join("Bob");
            mob_living aliceMob = w.MobOf(alice);
            mob_living bobMob = w.MobOf(bob);
            w.Run(10);
            // side by side, by console commands (everything that changes the world is a command, so the replay does it too)
            w.Host.EnqueueConsoleLine("teleport Alice 6 6");
            w.Host.EnqueueConsoleLine("teleport Bob 7 6");
            w.Run(10);
            using (w.World.Enter())
                Assert.Equal((7f, 6f), (Dm.num(bobMob.x), Dm.num(bobMob.y)));
            foreach (AimZone zone in new[] { AimZone.Groin, AimZone.Eyes, AimZone.Mouth })
            {
                Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 24, zone), ClickButton.Left, ClickModifiers.None));
                w.Run(20);
            }
            using (w.World.Enter())
                Assert.Equal(BODY_ZONE_PRECISE_MOUTH, aliceMob.zone_selected);
            w.Host.RequestStop();
            for (int i = 0; i < 10 && !w.Host.Finished; i++)
                w.Run(1);
            Assert.True(w.Host.Finished);
            (hash, end) = (w.Host.FinalHash, w.Host.Tick);
            string text = w.Host.Log.ToText();
            Assert.Contains(",16,24,0,0,7", text); // the eyes' zone byte in the action's text form
            log = CommandLog.Parse(text, out string error);
            Assert.Null(error);
        }
        Assert.Equal(new[] { AimZone.Groin, AimZone.Eyes, AimZone.Mouth },
            log.Commands.Where(c => c.Kind == CommandKind.Action && c.Action == ActionKind.Click && c.AimZone != AimZone.None).Select(c => c.AimZone));

        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths); // the live world's table (RealHeadlessWorld)
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        Assert.Equal(hash, result.FinalHash);
        using (replay.World.Enter())
            Assert.Equal(BODY_ZONE_PRECISE_MOUTH, (replay.World.Objects.TryGet(replay.AvatarOf("Alice"), out object o) ? o as mob : null)?.zone_selected);
    }
}
