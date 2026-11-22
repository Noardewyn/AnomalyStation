using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// Determinism (D31): the same seed, damage and input script gives the same world state hash and the same trace, twice; another
/// seed gives another hash (the random zone rolls, the random lying angles and the plain humans' random setup differ).
/// </summary>
public class DeterminismTests
{
    private const int Tps = MedRoom.TicksPerSecond;

    private static (ulong Hash, string Trace) Run(ulong seed)
    {
        var room = new MedRoom(seed: seed, size: 12);
        var a = room.PlainHuman(4, 4);
        var b = room.PlainHuman(5, 4);
        var c = room.Human(8, 8);
        var trace = new List<string>();
        void Sample(string what) => trace.Add(room.In(() =>
            $"{what} t{room.Tick} " + string.Join(" ", new[] { a, b, c }.Select(h =>
                $"[{h.health:0.##} {h.stat} {h.body_position} {h.lying_angle} {h.dir} {MedRoom.Brute(h):0.##}/{MedRoom.Burn(h):0.##}/{MedRoom.Tox(h):0.##}/{MedRoom.Oxy(h):0.##} {h.cached_multiplicative_slowdown:0.###} {h.loc?.x},{h.loc?.y}]"))));
        room.In(() =>
        {
            // untargeted damage rolls the zone (80% chest, else weighted) with the world's RNG
            for (int i = 0; i < 10; i++)
                MedRoom.Apply(a, 7, BRUTE);
            MedRoom.Apply(a, 12, BURN, BODY_ZONE_L_ARM);
            Waiting.NoSleep(a.adjust_tox_loss(15));
            Waiting.NoSleep(a.adjust_oxy_loss(20));
            Waiting.NoSleep(b.take_overall_damage(60, 30));
            Waiting.NoSleep(c.take_bodypart_damage(20, 10));
        });
        Sample("damage");
        room.In(() => Waiting.NoSleep(a.disarm(b)));
        Sample("shove");
        room.In(() =>
        {
            Waiting.NoSleep(c.toggle_resting());
            Waiting.NoSleep(c.Stun(3 * SECONDS));
        });
        for (int i = 0; i < 6; i++)
        {
            room.Step(10 * Tps);
            Sample("t" + i);
        }
        room.In(() =>
        {
            Waiting.NoSleep(b.Knockdown(2 * SECONDS, daze_amount: 3 * SECONDS));
            Waiting.NoSleep(a.apply_damage(160, TOX));
            Waiting.NoSleep(c.toggle_resting());
        });
        for (int i = 0; i < 12; i++)
        {
            room.Step(10 * Tps);
            Sample("u" + i);
        }
        room.AssertNoRuntimes();
        return (room.World.ComputeStateHash(), string.Join("\n", trace));
    }

    [Fact]
    public void TheSameSeedAndScriptGiveTheSameHashAndTrace()
    {
        (ulong h1, string t1) = Run(801);
        (ulong h2, string t2) = Run(801);
        Assert.Equal(t1, t2);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void AnotherSeedGivesAnotherRun()
    {
        (ulong h1, string t1) = Run(801);
        (ulong h2, string t2) = Run(802);
        Assert.NotEqual(t1, t2);
        Assert.NotEqual(h1, h2);
    }
}
