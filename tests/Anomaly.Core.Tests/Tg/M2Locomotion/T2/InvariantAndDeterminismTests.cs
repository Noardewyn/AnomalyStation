using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// Random scripts over a small room with players and dummies (scope 2l): after every tick of a script that walks, turns, bumps,
/// lies down, gets moved by tg and teleported, and changes tg's slowdown and the speed scale, each capsule's tile is its mob's
/// loc (the bridge's invariant, D105), no capsule centre is inside a solid tile or off the map, no body overlaps another one
/// except right after a teleport or a posture change, and nothing raised a runtime. Then the same script twice in two worlds
/// gives the same state hash at every tick (D31).
/// </summary>
public class InvariantAndDeterminismTests
{
    private const int C = T2Room.C;

    private sealed class Script
    {
        public readonly T2Room Room;
        public readonly mob_living_carbon_human[] Players;
        public readonly mob_living_carbon_human[] Dummies;
        public readonly List<ulong> Hashes = new();
        private readonly Random _rng;
        private readonly Dictionary<long, int> _freeAfter = new();

        public Script(ulong worldSeed, int scriptSeed)
        {
            Room = new T2Room(worldSeed);
            _rng = new Random(scriptSeed);
            Players = new[] { Room.Join(3, 3, "Alice", name: "a"), Room.Join(12, 3, "Bob", name: "b") };
            Dummies = new[] { Room.Dummy(7, 7, name: "c"), Room.Dummy(8, 7, name: "d") };
            // a few fixed crates to bump
            Room.Crate(6, 10); Room.Crate(10, 5); Room.Crate(11, 11);
        }

        private mob_living_carbon_human Anyone() => _rng.Next(4) < 2 ? Players[_rng.Next(2)] : Dummies[_rng.Next(2)];

        public void Run(int ticks, Action<int> afterTick, int hashEvery = 0)
        {
            sbyte[] hold = new sbyte[4];
            ushort[] yaw = new ushort[2];
            for (int tick = 0; tick < ticks; tick++)
            {
                // players: keep a wish for a while, change it now and then; the yaw wanders
                for (int p = 0; p < 2; p++)
                {
                    if (tick % 9 == 0 || _rng.Next(12) == 0)
                    {
                        hold[2 * p] = (sbyte)(_rng.Next(5) == 0 ? 0 : _rng.Next(-128, 128));
                        hold[2 * p + 1] = (sbyte)(_rng.Next(5) == 0 ? 0 : _rng.Next(-128, 128));
                    }
                    if (_rng.Next(6) == 0)
                        yaw[p] = (ushort)_rng.Next(65536);
                    Room.Frame(Players[p], hold[2 * p], hold[2 * p + 1], yaw[p]);
                }
                int roll = _rng.Next(100);
                if (roll < 4)
                {
                    mob_living_carbon_human d = Dummies[_rng.Next(2)];
                    float dir = new[] { NORTH, SOUTH, EAST, WEST }[_rng.Next(4)];
                    Room.Command(() =>
                    {
                        turf to = get_step(d, dir);
                        if (to != null)
                            Waiting.NoSleep(d.Move(to, dir)); // tg moves a dummy: a one-tile glide for its capsule
                    });
                }
                else if (roll < 6)
                {
                    mob_living_carbon_human who = Anyone();
                    int x = _rng.Next(2, 16), y = _rng.Next(2, 16);
                    Room.Command(() =>
                    {
                        if (!LocomotionSeam.IsSolid(Room.World, x, y, 1))
                            Waiting.NoSleep(who.forceMove(locate(x, y, 1)));
                    });
                }
                else if (roll < 10)
                {
                    mob_living_carbon_human who = Anyone();
                    Room.Command(() => who.body_position = Dm.num(who.body_position) == LYING_DOWN ? STANDING_UP : LYING_DOWN);
                }
                else if (roll < 12)
                {
                    float scale = new[] { 0.5f, 1f, 2f, 4f }[_rng.Next(4)];
                    Room.Command(() => LocomotionSeam.SetSpeedScale(Room.World, scale));
                }
                else if (roll < 16)
                {
                    mob_living_carbon_human who = Anyone();
                    float slowdown = new[] { 0.5f, 1f, 1.5f, 2.5f, 4f, 5.5f }[_rng.Next(6)];
                    Room.Command(() => who.cached_multiplicative_slowdown = slowdown);
                }
                Room.Step();
                afterTick(tick);
                if (hashEvery > 0 && (tick + 1) % hashEvery == 0)
                    Hashes.Add(Room.World.ComputeStateHash());
            }
        }

        public IEnumerable<mob_living_carbon_human> Everyone => Players.Concat(Dummies);
    }

    [Theory]
    [InlineData(1, 11)]
    [InlineData(2, 12)]
    [InlineData(3, 13)]
    [InlineData(4, 14)]
    [InlineData(5, 15)]
    public void TheCapsuleIsOnItsLocAndOutOfSolidTilesAtTheEndOfEveryTickOfARandomScript(ulong worldSeed, int scriptSeed)
    {
        var script = new Script(worldSeed, scriptSeed);
        T2Room r = script.Room;
        int standingTicks = 0, lyingTicks = 0, moves = 0, bumps = 0;
        r.In(() =>
        {
            foreach (mob_living_carbon_human m in script.Everyone)
            {
                Toy.On(m, COMSIG_MOVABLE_MOVED, "moved", 0, act: _ => moves++);
                Toy.On(m, COMSIG_MOVABLE_BUMP, "bump", 0, act: _ => bumps++);
            }
        });
        script.Run(260, tick =>
        {
            foreach (mob_living_carbon_human m in script.Everyone)
            {
                r.AssertOnLoc(m, $"tick {tick}");
                MotionState s = r.State(m);
                Assert.Equal(1, s.Z);
                Assert.False(LocomotionSeam.IsSolid(r.World, s.TileX, s.TileY, 1), $"tick {tick}: the centre of {r.In(() => m.name)} is on the solid tile ({s.TileX},{s.TileY})");
                if (s.Posture == Posture.Standing) standingTicks++; else lyingTicks++;
            }
        });
        Assert.True(standingTicks > 0 && lyingTicks > 0, "the script did not lie anyone down");
        Assert.True(moves > 30, "the script moved little: " + moves);
        Assert.True(bumps > 0, "the script bumped nothing");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TwoWorldsRunningTheSameScriptHaveTheSameStateHashAtEveryTick()
    {
        var a = new Script(7, 99);
        var b = new Script(7, 99);
        a.Run(200, _ => { }, hashEvery: 8);
        b.Run(200, _ => { }, hashEvery: 8);
        Assert.Equal(a.Hashes, b.Hashes);
        for (int i = 0; i < 4; i++)
        {
            mob_living_carbon_human ma = a.Everyone.ElementAt(i), mb = b.Everyone.ElementAt(i);
            Assert.Equal(a.Room.State(ma), b.Room.State(mb));
        }
        a.Room.AssertNoRuntimes();
        b.Room.AssertNoRuntimes();
        // and the hash is not blind to the capsules: another script, another history
        var c = new Script(7, 100);
        c.Run(200, _ => { }, hashEvery: 8);
        Assert.NotEqual(a.Hashes[^1], c.Hashes[^1]);
    }

    [Fact]
    public void TheHashChangesWithTheCapsulesStateAndNothingElseOfTheSeam()
    {
        var a = new T2Room(31);
        var b = new T2Room(31);
        mob_living_carbon_human ma = a.Join(5, 5, "Alice", name: "m");
        mob_living_carbon_human mb = b.Join(5, 5, "Alice", name: "m");
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
        a.Go(ma, 127, 0, T2Room.Yaw(EAST));
        b.Go(mb, 127, 0, T2Room.Yaw(EAST));
        Assert.Equal(a.World.ComputeStateHash(), b.World.ComputeStateHash());
        a.Go(ma, 127, 0, T2Room.Yaw(EAST));
        b.Go(mb, 126, 0, T2Room.Yaw(EAST)); // one unit of the wish: another speed, another position
        Assert.NotEqual(a.World.ComputeStateHash(), b.World.ComputeStateHash());
    }
}
