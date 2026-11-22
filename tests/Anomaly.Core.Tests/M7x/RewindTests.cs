using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV527: an aimed shot whose click says how far behind its world the shooter's client drew the others (<c>anomaly-rewind</c>) is tested against a
/// walking capsule where it stood then. Bob's capsule stood at (8, 4) and has been at (8, 6) for two ticks; Alice fires a level beam east along
/// row 4 at the head's height.
/// </summary>
public class RewindTests
{
    private const int LineY = 4, From = 3, Target = 8;

    private static (AudioRoom R, mob_living_carbon_human Bob) MovedBob()
    {
        var r = new AudioRoom(14, 7, seed: 7911);
        r.Step(20);
        r.In(() => LocomotionSeam.Install(r.World));
        mob_living_carbon_human bob = r.Dummy(Target, LineY);
        r.In(() => LocomotionSeam.Register(bob));
        r.Step(4);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(Target, LineY + 2, 1))));
        r.Step(2);
        return (r, bob);
    }

    private static void Fire(AudioRoom r, mob shooter, float rewind)
    {
        string @params = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(From * 32, LineY * 32, 20.8, 90, 0)
            + (rewind > 0 ? ";" + ClientSeam.RewindParam(rewind) : "");
        r.In(() =>
        {
            var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(get_turf(shooter));
            p.firer = shooter;
            p.fired_from = shooter;
            Waiting.NoSleep(p.aim_projectile(locate(14, LineY, 1), shooter, params2list(@params), 0f));
            Assert.Equal(Math.Min(rewind, ShotRewind.MaxTicks), p.dm_rewind);
            Waiting.NoSleep(p.fire());
        });
        r.Step();
    }

    [Theory]
    [InlineData(0f, false)] // tested where he is: the line passes two tiles south of him
    [InlineData(1f, false)] // one tick back he was at (8, 6) already
    [InlineData(4f, true)] // four ticks back he stood on the line
    [InlineData(40f, true)] // a claim past the cap is cut to it, still in the kept ticks
    public void AShotRewoundToWhenTheShooterSawTheMobHitsItThere(float rewind, bool hit)
    {
        (AudioRoom r, mob_living_carbon_human bob) = MovedBob();
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        Fire(r, alice, rewind);
        Assert.Equal(hit, r.In(() => Dm.num(bob.get_fire_loss())) > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARewoundShotPassesTheBodyWhereItStandsNow()
    {
        // Bob moved onto the line from (8, 6): a shot rewound to before he came flies through him to the east wall
        var r = new AudioRoom(14, 7, seed: 7912);
        r.Step(20);
        r.In(() => LocomotionSeam.Install(r.World));
        mob_living_carbon_human bob = r.Dummy(Target, LineY + 2);
        r.In(() => LocomotionSeam.Register(bob));
        r.Step(4);
        r.In(() => Waiting.NoSleep(bob.forceMove(locate(Target, LineY, 1))));
        r.Step(2);
        mob_living_carbon_human alice = r.Dummy(From, LineY);
        Fire(r, alice, 4f);
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_fire_loss())));
        Fire(r, alice, 0f);
        Assert.True(r.In(() => Dm.num(bob.get_fire_loss())) > 0, "a shot not rewound hits him where he stands");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ThePointsBetweenKeptTicksAreInterpolated()
    {
        (AudioRoom r, mob_living_carbon_human bob) = MovedBob();
        r.In(() =>
        {
            long now = r.World.Tick;
            Assert.False(ShotRewind.TryPointAt(r.World, bob.dm_id, now, out _, out _, out _)); // the last kept tick: where he is applies
            Assert.True(ShotRewind.TryPointAt(r.World, bob.dm_id, now - 5, out double x, out double y, out int z));
            Assert.Equal((Target * 32.0, LineY * 32.0, 1), (x, y, z));
            // the move of two tiles between the kept ticks now - 2 and now - 1 is drawn, half way at now - 1.5
            Assert.True(ShotRewind.TryPointAt(r.World, bob.dm_id, now - 1.5, out _, out y, out _));
            Assert.Equal((LineY + 1) * 32.0, y, 3);
        });
    }
}
