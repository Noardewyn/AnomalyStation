using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave3.Timers;

/// <summary>
/// A 3000-tick random workload of SStimer: bucket timers with waits up to 75 s (secondary queue, migrations, two wraps of
/// the buckets), client-time timers, fractional waits, stoppable timers deleted at random. Every timer that is not
/// deleted runs exactly once at its time, in tg's order (client-time timers first and first-created-first, then the
/// buckets last-created-first, timer.dm:106-216), the bookkeeping holds at every check (bucket_count is the number of
/// timers in the buckets, the secondary queue is sorted and beyond TIMER_MAX), and the same seed gives the same order and
/// the same world state.
/// </summary>
public class TimerFuzzTests
{
    private sealed class Planned
    {
        public int K;
        public object Id;
        public float Ttr;
        public bool Client;
    }

    private sealed class Outcome
    {
        public List<string> Log;
        public List<string> Expected;
        public ulong Hash;
        public int Timers;
        public int Deleted;
    }

    private static Outcome Run(int seed)
    {
        var t = new TimerWorld(seed: (ulong)seed);
        var rnd = new Random(seed); // test input only; the world's RNG is not drawn from
        var planned = new List<Planned>();
        var live = new List<Planned>(); // stoppable, not deleted, not run yet
        int deleted = 0;
        test_timers_bell bell = t.In(() => t.Bell());
        for (int tick = 0; tick < 3000; tick++)
        {
            t.In(() =>
            {
                float now = Dm.world.time;
                for (int j = 0; j < 3; j++)
                {
                    var p = new Planned { K = planned.Count };
                    bool stoppable = rnd.Next(2) == 0;
                    float flags = stoppable ? TIMER_STOPPABLE : 0;
                    if (rnd.Next(7) == 0)
                    {
                        int ds = rnd.Next(0, 200);
                        p.Client = true;
                        // max(ceil(wait), 1) (timer.dm:623) after REALTIMEOFDAY, whole deciseconds (world.timeofday)
                        p.Ttr = MathF.Floor(now) + Math.Max(ds, 1);
                        p.Id = TimerWorld.Ring(bell, p.K, ds, flags + TIMER_CLIENT_TIME);
                    }
                    else
                    {
                        int halves = rnd.Next(0, 1500);
                        bool fraction = rnd.Next(5) == 0;
                        // CEILING(wait, world.tick_lag) with a tick_lag minimum (timer.dm:625)
                        p.Ttr = now + Math.Max(halves + (fraction ? 1 : 0), 1) * 0.5f;
                        p.Id = TimerWorld.Ring(bell, p.K, halves * 0.5f + (fraction ? 0.2f : 0f), flags);
                    }
                    planned.Add(p);
                    if (stoppable)
                        live.Add(p);
                }
                live.RemoveAll(p => p.Ttr <= now);
                if (live.Count > 0 && rnd.Next(4) == 0)
                {
                    Planned victim = live[rnd.Next(live.Count)];
                    Assert.True(Dm.eq(deltimer(victim.Id), TRUE));
                    live.Remove(victim);
                    planned.Remove(victim);
                    deleted++;
                }
            });
            t.Tick(1);
            if (tick % 100 == 99)
                t.In(() => CheckBookkeeping(planned, Dm.world.time));
        }
        t.Tick(1600); // past the last time to run: 1500 + 749.5 ds
        t.In(() =>
        {
            CheckBookkeeping(planned, Dm.world.time);
            Assert.Equal(0f, SStimer.bucket_count);
            Assert.Equal(0, SStimer.second_queue.len);
            Assert.Equal(0, SStimer.clienttime_timers.len);
            Assert.Equal(0, SStimer.timer_id_dict.len);
            Assert.Equal(0, SStimer.hashes.len);
        });
        Assert.Empty(t.Runtimes);

        // the order tg runs them in: by time; in one fire the client-time timers first, first-created-first (their list
        // is sorted with ties after the equal ones), then the bucket, last-created-first
        var expected = planned
            .OrderBy(p => p.Ttr)
            .ThenBy(p => p.Client ? 0 : 1)
            .ThenBy(p => p.Client ? p.K : -p.K)
            .Select(p => Dm.str(p.K) + "@" + Dm.str(p.Ttr))
            .ToList();
        return new Outcome { Log = t.Log, Expected = expected, Hash = t.World.ComputeStateHash(), Timers = planned.Count, Deleted = deleted };
    }

    /// <summary>The bookkeeping of SStimer after a fire at <paramref name="now"/>.</summary>
    private static void CheckBookkeeping(List<Planned> planned, float now)
    {
        datum_controller_subsystem_timer ss = SStimer;
        int inBuckets = 0;
        for (int pos = 1; pos <= 1200; pos++)
        {
            datum_timedevent node = ss.bucket_list[pos] as datum_timedevent;
            Assert.True(node == null || node.prev == null);
            for (; node != null; node = node.next)
            {
                inBuckets++;
                Assert.Equal((float)pos, node.bucket_pos);
                Assert.Equal(TRUE, node.bucket_joined);
                Assert.True(node.next == null || node.next.prev == node);
            }
        }
        Assert.Equal((float)inBuckets, ss.bucket_count);

        float timerMax = datum_controller_subsystem_timer.TIMER_MAX(ss);
        float last = float.MinValue;
        foreach (datum_timedevent q in Dm.each_cast<datum_timedevent>(ss.second_queue))
        {
            float ttr = Dm.num(q.timeToRun);
            Assert.True(ttr >= timerMax, "a secondary-queue timer below TIMER_MAX");
            Assert.True(ttr >= last, "the secondary queue is not sorted");
            last = ttr;
        }

        int pendingBucket = planned.Count(p => !p.Client && p.Ttr > now);
        int pendingClient = planned.Count(p => p.Client && p.Ttr > now);
        Assert.Equal(pendingBucket, inBuckets + ss.second_queue.len);
        Assert.Equal(pendingClient, ss.clienttime_timers.len);
        Assert.Equal(planned.Count(p => p.Ttr > now && p.Id is string), ss.timer_id_dict.len);
    }

    [Fact]
    public void EveryTimerRunsOnceAtItsTimeInTgsOrderOverThreeThousandTicks()
    {
        Outcome o = Run(17);
        Assert.Equal(9000 - o.Deleted, o.Timers);
        Assert.True(o.Deleted > 500);
        Assert.Equal(o.Expected.Count, o.Log.Count);
        Assert.Equal(o.Expected, o.Log);
    }

    [Fact]
    public void TheSameSeedGivesTheSameOrderAndWorldStateAndAnotherSeedDoesNot()
    {
        Outcome a = Run(5), b = Run(5), c = Run(6);
        Assert.Equal(a.Log, b.Log);
        Assert.Equal(a.Hash, b.Hash);
        Assert.NotEqual(a.Log, c.Log);
        Assert.NotEqual(a.Hash, c.Hash);
    }
}
