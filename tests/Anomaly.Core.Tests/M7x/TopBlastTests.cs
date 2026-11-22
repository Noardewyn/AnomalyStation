using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV578 (the owner, 2026-11-21: a rocket or a grenade fired at the ceiling went off over it and the ceiling took nothing): a blast set off by a
/// shot at the ceiling goes off under it. Alice, on (3, 4), fires up 12 degrees east: the line meets the 2 m ceiling over (5, 4).
/// </summary>
public class TopBlastTests
{
    private const int LineY = 4;

    private static (AudioRoom R, mob_living Alice) Room(ulong seed)
    {
        var r = new AudioRoom(14, 7, seed: seed);
        r.Step(20);
        var alice = r.Dummy(3, LineY);
        r.In(() => CeilingSystem.Of(r.World).Initialize());
        return (r, alice);
    }

    private static void Fire<T>(AudioRoom r, mob_living alice) where T : obj_projectile => r.In(() =>
    {
        string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(3 * 32, LineY * 32, 20.8, 90, 12);
        T p = Dm.@new<T>(get_turf(alice));
        p.firer = alice;
        p.fired_from = alice;
        Waiting.NoSleep(p.aim_projectile(locate(14, LineY, 1), alice, params2list(aim), 0));
        Waiting.NoSleep(p.fire());
    });

    [Fact]
    public void ARocketAtTheCeilingGoesOffUnderItAndBreaksIt()
    {
        var (r, alice) = Room(9701);
        Fire<obj_projectile_bullet_rocket>(r, alice);
        var drawn = new List<(int X, float H, bool Shot)>();
        for (int t = 0; t < 20; t++)
        {
            r.Step();
            drawn.AddRange(r.In(() => Dm.each<obj_effect_explosion>().Select(e => ((int)Dm.num(e.x), e.dm_origin_h, e.dm_shot)).ToList()));
        }
        Assert.NotEmpty(drawn);
        // drawn under the 2 m (32 px) ceiling, over the cell whose top it hit
        Assert.All(drawn, d => Assert.True(d.Shot && Math.Abs(d.H - (32 - TopBlast.BelowCeilingPixels)) < 0.01f, $"a blast drawn at {d.H} px"));
        Assert.Contains(drawn, d => d.X == 5);
        // tg's heavy blast at the epicentre on the ceiling's floor block: it is gone, a lattice, scraped or broken
        CeilingState top = r.In(() => CeilingSystem.Of(r.World).At(5, LineY, 1));
        Assert.True(!top.Exists || !top.Sealed || top.Broken != 0 || top.Turf != CeilingState.Plating, $"the ceiling over the blast is {top}");
        r.AssertNoRuntimes();
    }

    // the owner (2026-11-21): a grenade at the ceiling tiles cracks them as one on the floor cracks the floor's
    [Fact]
    public void AGrenadeAtTheCeilingCracksItsTiles()
    {
        int cracked = 0, dented = 0;
        for (ulong seed = 0; seed < 3; seed++)
        {
            var (r, alice) = Room(9710 + seed);
            Fire<obj_projectile_bullet_a40mm>(r, alice);
            r.Step(20);
            cracked += r.In(() => Enumerable.Range(2, 6).SelectMany(x => Enumerable.Range(2, 5).Select(y => CeilingSystem.Of(r.World).At(x, y, 1))).Count(c => c.Broken != 0));
            // DV577: its dent (tg's 75 %) is under the ceiling it hit, and the cracks keep it
            if (r.In(() => Dm.each<mutable_appearance>((locate(5, LineY, 1) as turf_open).dm_dent_decals).Any(d => Dm.num(d.dir) == UP)))
                dented++;
            r.AssertNoRuntimes();
        }
        Assert.True(cracked >= 6, $"{cracked} ceiling tiles cracked by three grenades");
        Assert.True(dented >= 1, "no grenade left its dent under the ceiling");
    }
}
