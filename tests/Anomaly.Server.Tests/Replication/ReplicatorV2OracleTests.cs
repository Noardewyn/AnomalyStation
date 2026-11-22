using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Anomaly.Server.Tests.Protocol;

namespace Anomaly.Server.Tests.Replication;

/// <summary>
/// The M1 oracle, extended to version 2 (scope 2l): the replica of every session equals a full scan of the world at every
/// tick, with humans moving continuously and taking damage, and the D40 tests (scope 2h): another session's private block
/// never appears in a session's bytes, no health or damage number ever leaves the server, and the shared body is the same
/// bytes for everyone.
/// </summary>
public class ReplicatorV2OracleTests
{
    private static (V2Run Run, HumanScenario Humans, SessionView A, SessionView B) Setup(int seed, int humans = 20, int size = 22, int sweepDivisor = 1, int minSweep = 0, bool churn = true, bool realMedicine = false)
    {
        V2Run run = V2Run.Quiet(size: size, seed: seed, sweepDivisor: sweepDivisor, minSweep: minSweep, cellSweep: 1000);
        var scenario = new HumanScenario(run.World, run.Locomotion, humans, seed, churn: churn, realMedicine: realMedicine);
        SessionView a = run.Connect("Alice", scenario.Humans[0]);
        SessionView b = run.Connect("Bob", scenario.Humans[1]);
        return (run, scenario, a, b);
    }

    [Fact]
    public void TheReplicaEqualsTheWorldEveryTickWithHumansMovingAndTakingDamage()
    {
        var (run, scenario, a, b) = Setup(seed: 71);
        int checkedTicks = 0, capsuleChanges = 0, appearanceChanges = 0, postureChanges = 0;
        for (int tick = 0; tick < 1500; tick++)
        {
            scenario.Act();
            DeltaFrame frame = run.Step();
            SortedDictionary<long, EntityViewV2> truth = run.Truth;
            foreach (SessionView s in new[] { a, b })
            {
                List<string> differences = V2Views.Differences(s.Replica, truth, s, run.World.World);
                Assert.True(differences.Count == 0, V2Views.Describe(run.World.World.Tick, differences));
            }

            Delta delta = Wire.DecodeDelta(frame);
            capsuleChanges += a.LastMotionEntries; // version 42: the capsules' positions are in the Motion messages
            foreach (EntityChange c in delta.Changed)
            {
                if ((c.Mask & NetField.Pos) != 0) capsuleChanges++;
                if ((c.Mask & NetField.Appearance) != 0) appearanceChanges++;
                if ((c.Mask & NetField.Posture) != 0) postureChanges++;
            }

            checkedTicks++;
        }

        Assert.Empty(run.World.Runtimes);
        Assert.True(capsuleChanges > 5000 && appearanceChanges > 100 && postureChanges > 20, $"the scenario exercised the replica: pos {capsuleChanges}, appearance {appearanceChanges}, posture {postureChanges}");
        Assert.Empty(V2Views.CellDifferences(a.Replica, run.World.World));
        Assert.Equal(1500, checkedTicks);
        // Both sessions hold the same public tables.
        Assert.Equal(a.Replica.Resources, b.Replica.Resources);
        Assert.Equal(a.Replica.Strings, b.Replica.Strings);
        Assert.Equal(a.Replica.Appearances.Count, b.Replica.Appearances.Count);
        // The table is a few hundred rows at most: the damage overlays repeat.
        Assert.InRange(a.Replica.Appearances.Count, 10, 4000);
    }

    // M2 integration: the same oracle with tg's own medicine (P1, P2 and P3 landed): apply_damage to zones, toxin and
    // oxygen, full heals, knockdowns and getting up; the replica equals the world every tick.
    [Fact]
    public void TheReplicaEqualsTheWorldEveryTickWithRealHumansTakingTgsDamage()
    {
        var (run, scenario, a, b) = Setup(seed: 81, realMedicine: true);
        int appearanceChanges = 0, postureChanges = 0;
        for (int tick = 0; tick < 1500; tick++)
        {
            scenario.Act();
            DeltaFrame frame = run.Step();
            SortedDictionary<long, EntityViewV2> truth = run.Truth;
            foreach (SessionView s in new[] { a, b })
            {
                List<string> differences = V2Views.Differences(s.Replica, truth, s, run.World.World);
                Assert.True(differences.Count == 0, V2Views.Describe(run.World.World.Tick, differences));
            }
            foreach (EntityChange c in Wire.DecodeDelta(frame).Changed)
            {
                if ((c.Mask & NetField.Appearance) != 0) appearanceChanges++;
                if ((c.Mask & NetField.Posture) != 0) postureChanges++;
            }
        }
        Assert.Empty(run.World.Runtimes);
        Assert.True(appearanceChanges > 100 && postureChanges > 20, $"tg's damage and knockdowns changed the look: appearance {appearanceChanges}, posture {postureChanges}");
        Assert.Equal(a.Replica.Strings, b.Replica.Strings);
        // tg's damage overlays (dam_mob.dmi "<type>_<zone>_<brute><burn>") reached the public tables.
        Assert.Contains(a.Replica.Strings, s => s.Contains("_chest_", StringComparison.Ordinal) || s.Contains("_head_", StringComparison.Ordinal));
    }

    [Fact]
    public void WithTheDefaultSweepEveryHintedChangeIsExactAndInPlaceEditsAreWithinThePeriod()
    {
        var (run, scenario, a, _) = Setup(seed: 72, sweepDivisor: 64, minSweep: 32);
        run.World.AddClutter(600); // more slots, so the sweep's period is several ticks and not one
        run.Step();
        Assert.InRange(run.Replicator.SweepPeriodTicks, 4, 64);
        var pending = new Dictionary<long, long>();
        int late = 0;
        for (int tick = 0; tick < 1200; tick++)
        {
            scenario.Act();
            run.Step();
            long now = run.World.World.Tick;
            SortedDictionary<long, EntityViewV2> truth = run.Truth;
            // Everything but the appearance and the posture of a human at rest is exact every tick (the capsules' own are polled).
            List<string> differences = V2Views.Differences(a.Replica, truth, a, run.World.World);
            foreach (string line in differences.ToList())
                if (!line.StartsWith("replica", StringComparison.Ordinal)) Assert.Fail(V2Views.Describe(now, differences));
            foreach (EntityViewV2 t in truth.Values)
            {
                a.Replica.TryGet(t.Base.Id, out ReplicaEntity e);
                bool same = a.Replica.ViewV2(e) == t;
                if (same)
                {
                    if (pending.Remove(t.Base.Id)) late++;
                    continue;
                }

                if (!pending.ContainsKey(t.Base.Id)) pending[t.Base.Id] = now;
                long age = now - pending[t.Base.Id];
                Assert.True(age <= run.Replicator.SweepPeriodTicks + 1, $"tick {now}: #{t.Base.Id} is {age} ticks late, the sweep period is {run.Replicator.SweepPeriodTicks}\n{a.Replica.ViewV2(e)}\n{t}");
            }

            foreach (long gone in pending.Keys.Where(id => !truth.ContainsKey(id)).ToList()) pending.Remove(gone);
        }

        // M2 P3's overlay owner hints every in-place overlays edit (replication.md 5.3), so no edit is left for the sweep:
        // before it, this asserted that the sweep caught more than five.
        Assert.True(late == 0, "the sweep caught " + late + " in-place edits that the hint should have named");
        Assert.Empty(run.World.Runtimes);
    }

    [Fact]
    public void ALateJoinerHoldsTheSameTablesAndStaysEqualToTheWorld()
    {
        var (run, scenario, a, _) = Setup(seed: 73);
        for (int i = 0; i < 400; i++)
        {
            scenario.Act();
            run.Step();
        }

        SessionView late = run.Connect("Carol", scenario.Humans[2]);
        Assert.Equal(a.Replica.Resources, late.Replica.Resources);
        Assert.Equal(a.Replica.Strings, late.Replica.Strings);
        Assert.Equal(a.Replica.Appearances.Count, late.Replica.Appearances.Count);
        for (int i = 0; i < a.Replica.Appearances.Count; i++)
            Msg.AssertEqual(a.Replica.Appearances[i], late.Replica.Appearances[i]);
        for (int i = 0; i < 300; i++)
        {
            scenario.Act();
            run.Step();
            SortedDictionary<long, EntityViewV2> truth = run.Truth;
            foreach (SessionView s in new[] { a, late })
            {
                List<string> differences = V2Views.Differences(s.Replica, truth, s, run.World.World);
                Assert.True(differences.Count == 0, V2Views.Describe(run.World.World.Tick, differences));
            }
        }
    }

    // ---- hash neutrality ----

    [Fact]
    public void ReplicatingTheHumansWithPrivateBlocksAndCellsChangesNoStateHash()
    {
        ulong[] plain = Hashes(replicate: false);
        ulong[] replicated = Hashes(replicate: true);
        Assert.Equal(plain, replicated);
    }

    private static ulong[] Hashes(bool replicate)
    {
        var hashes = new List<ulong>();
        if (replicate)
        {
            var (run, scenario, _, _) = Setup(seed: 74, humans: 12, size: 18, sweepDivisor: 8, minSweep: 4);
            for (int i = 1; i <= 600; i++)
            {
                scenario.Act();
                run.Step();
                if (i % 200 == 0) hashes.Add(run.World.World.ComputeStateHash());
            }

            Assert.Empty(run.World.Runtimes);
            return hashes.ToArray();
        }

        // The same world and the same scripted people without a replicator and without sessions' replicas; the two avatars
        // log in the same way (their client/New is part of the world).
        ReplicationWorld world = ReplicationWorld.Boot(size: 18, movers: 0, seed: 74, containers: 0, notTranslatedThrows: false);
        world.ScenarioOn = false;
        var loco = new FakeLocomotion();
        var plain = new HumanScenario(world, loco, 12, 74);
        ConnectPlain(world, "Alice", plain.Humans[0]);
        ConnectPlain(world, "Bob", plain.Humans[1]);
        for (int i = 1; i <= 600; i++)
        {
            plain.Act();
            world.Step();
            if (i % 200 == 0) hashes.Add(world.World.ComputeStateHash());
        }

        Assert.Empty(world.Runtimes);
        return hashes.ToArray();
    }

    private static void ConnectPlain(ReplicationWorld world, string key, mob avatar)
    {
        ClientConnection connection = null;
        using (world.World.Enter())
        {
            ClientSeam.KeyMob(avatar, key);
            connection = ClientSeam.Connect(key);
        }

        for (int i = 0; i < 20 && !connection.Done; i++) world.Step();
    }

    // ---- D40 ----

    private static bool Contains(IEnumerable<byte[]> messages, byte[] pattern) => messages.Any(m => m.AsSpan().IndexOf(pattern) >= 0);

    private static IEnumerable<byte[]> HiddenPatterns()
    {
        foreach (float v in HumanScenario.HiddenValues)
        {
            yield return BitConverter.GetBytes(v);
            yield return BitConverter.GetBytes((double)v);
            yield return Encoding.UTF8.GetBytes(v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            yield return Encoding.UTF8.GetBytes(v.ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void NoHealthOrDamageNumberEverLeavesTheServer()
    {
        var (run, scenario, a, b) = Setup(seed: 75, humans: 14, size: 20);
        // The owners' dolls, tg's screen objects: the level of damage is a picture (an icon state), never a number.
        run.In(() =>
        {
            foreach ((SessionView s, string tag) in new[] { (a, "A"), (b, "B") })
            {
                var doll = Dm.@new<obj>(null);
                doll.icon = Dm.resource("icons/hud/screen_gen.dmi");
                doll.icon_state = "DOLL_" + tag + "_head0";
                doll.screen_loc = "EAST-1:28,CENTER-1:15";
                s.Client.screen.Add(doll);
            }
        });
        int doses = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            scenario.Act();
            if (tick % 3 == 0)
            {
                run.In(() =>
                {
                    foreach ((SessionView s, string tag) in new[] { (a, "A"), (b, "B") })
                    {
                        mob_living_carbon_human human = (mob_living_carbon_human)s.Avatar;
                        int level = Math.Min(5, (int)Math.Ceiling(human.bruteloss / 50f));
                        atom_movable doll = (atom_movable)s.Client.screen[1f];
                        doll.icon_state = "DOLL_" + tag + "_head" + level;
                    }
                });
                doses++;
            }

            run.Step();
        }

        Assert.Empty(run.World.Runtimes);
        Assert.True(doses > 100);
        // Everything every session was sent: not one hidden number, as text, as an f32 or as an f64.
        foreach (SessionView s in new[] { a, b })
            foreach (byte[] pattern in HiddenPatterns())
                Assert.False(Contains(s.Bytes, pattern) || Contains(s.MotionBytes, pattern), $"session {s.Ordinal} was sent a hidden number ({BitConverter.ToString(pattern)})");
        // The world did hold them (the scan is meaningful): the humans carry the numbers.
        Assert.Contains(scenario.Humans, h => HumanScenario.HiddenValues.Contains(h.bruteloss));
        // The picture of the damage is public: the others see the overlays.
        Assert.Contains(a.Replica.Strings, st => st.Contains('_', StringComparison.Ordinal) && st.StartsWith("head", StringComparison.Ordinal));
    }

    [Fact]
    public void AnotherSessionsPrivateBlockNeverAppearsInASessionsBytes()
    {
        var (run, scenario, a, b) = Setup(seed: 76, humans: 10, size: 18);
        // Each session's secret screen object (the players' real HUDs are on their screens too, since Login makes them).
        var secretOf = new Dictionary<SessionView, obj>();
        run.In(() =>
        {
            foreach ((SessionView s, string tag) in new[] { (a, "A"), (b, "B") })
            {
                var doll = Dm.@new<obj>(null);
                secretOf[s] = doll;
                doll.icon = Dm.resource("icons/hud/ONLY_" + tag + ".dmi");
                doll.icon_state = "SECRET_" + tag + "_head3";
                doll.screen_loc = "SCREENLOC_" + tag;
                doll.overlays = Dm.list(ReplicatorV2Tests.Layered("icons/hud/ONLY_" + tag + ".dmi", "SECRET_" + tag + "_outline"));
                s.Client.screen.Add(doll);
            }
        });
        for (int tick = 0; tick < 200; tick++)
        {
            scenario.Act();
            if (tick % 10 == 0)
            {
                run.In(() =>
                {
                    foreach ((SessionView s, string tag) in new[] { (a, "A"), (b, "B") })
                        secretOf[s].icon_state = "SECRET_" + tag + "_head" + (tick / 10 % 6);
                });
            }

            run.Step();
        }

        foreach ((SessionView mine, SessionView theirs, string myTag, string theirTag) in new[] { (a, b, "A", "B"), (b, a, "B", "A") })
        {
            foreach (string secret in new[] { "SECRET_" + theirTag, "ONLY_" + theirTag, "SCREENLOC_" + theirTag })
                Assert.False(Contains(mine.Bytes, Encoding.UTF8.GetBytes(secret)), $"session {mine.Ordinal} was sent {secret}");
            // Its own are there (the test would pass vacuously otherwise) ...
            foreach (string own in new[] { "SECRET_" + myTag + "_head", "ONLY_" + myTag, "SCREENLOC_" + myTag })
                Assert.True(Contains(mine.Bytes, Encoding.UTF8.GetBytes(own)), $"session {mine.Ordinal} was not sent its own {own}");
            // ... in its private tables and its screen entries, and not in the public ones everyone shares.
            Assert.DoesNotContain(mine.Replica.Strings, st => st.Contains("SECRET", StringComparison.Ordinal));
            Assert.DoesNotContain(mine.Replica.Resources, r => r.Contains("ONLY_", StringComparison.Ordinal));
            Assert.Contains(mine.Replica.PrivateStrings, st => st.StartsWith("SECRET_" + myTag, StringComparison.Ordinal));
            Assert.DoesNotContain(mine.Replica.PrivateStrings, st => st.Contains(theirTag + "_", StringComparison.Ordinal));
            Assert.Contains(secretOf[mine].dm_id, mine.Replica.Screen.Keys);
            Assert.DoesNotContain(secretOf[theirs].dm_id, mine.Replica.Screen.Keys);
            Assert.Empty(mine.Replica.Screen.Keys.Intersect(theirs.Replica.Screen.Keys)); // every screen object is one client's
        }

        // The public part of every Delta is the same bytes for everyone: the tick's body, encoded once.
        SessionView x = a, y = b;
        for (int i = 2; i < x.Bytes.Count; i += 37)
        {
            Assert.True(Codec.TryDecode(x.Bytes[i], out Delta dx, out _));
            Assert.True(Codec.TryDecode(y.Bytes[i], out Delta dy, out _));
            Assert.Equal(dx.Tick, dy.Tick);
            Assert.Equal(Codec.EncodeDeltaBody(dx), Codec.EncodeDeltaBody(dy));
            Assert.NotEqual(dx.OwnEntity, dy.OwnEntity); // only the header and the private block differ
        }
    }

    [Fact]
    public void ASessionsPrivateStateIsGoneWithItWhenItLeaves()
    {
        var (run, _, a, b) = Setup(seed: 77, humans: 4, size: 16);
        run.In(() =>
        {
            var doll = Dm.@new<obj>(null);
            doll.icon = Dm.resource("icons/hud/screen_gen.dmi");
            doll.icon_state = "GONE_head3";
            a.Client.screen.Add(doll);
        });
        run.Step();
        run.Step();
        Assert.Contains(a.Replica.PrivateStrings, st => st == "GONE_head3");
        run.Leave(a);
        for (int i = 0; i < 20; i++) run.Step();
        // Whatever B was sent after A's block went, it never held it.
        Assert.False(Contains(b.Bytes, Encoding.UTF8.GetBytes("GONE_head3")));
        Assert.DoesNotContain(run.Replicator.Strings, st => st == "GONE_head3");
    }
}
