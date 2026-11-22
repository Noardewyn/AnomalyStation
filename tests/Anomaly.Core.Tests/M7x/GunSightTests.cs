using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV96 (the owner, 2026-11-21: a shooter's crosshair): the own gun's sight. Bob holds the smart SMG: its cone is tg's widest roll of its spread
/// (0.5 x 1.4 x spread) at the server's share; empty-handed he has none; each of his bullets that hits Rob counts for his crosshair's hit marker.
/// </summary>
public class GunSightTests
{
    private const int LineY = 4, Second = 20;

    [Fact]
    public void TheHeldGunsConeAndTheShotsThatHitAreTheSights()
    {
        var r = new AudioRoom(16, 9, seed: 4242);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(6, LineY);
        r.Step(Second);
        Assert.Null(r.In(() => GunSight.ConeOf(bob)));
        var gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_smartgun>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gun)));
        r.Step();
        float spread = r.In(() => gun.spread);
        Assert.True(spread > 0);
        Assert.Equal(0.7f * spread, r.In(() => GunSight.ConeOf(bob)).Value, 3);
        r.In(() => ShotSpread.Of(r.World).Set(0.5f));
        Assert.Equal(0.35f * spread, r.In(() => GunSight.ConeOf(bob)).Value, 3);

        string aim = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(3 * 32, LineY * 32, 20.8, 90, 0);
        r.In(() => { bob.next_click = -1; bob.next_move = -1; gun.fire_cd = 0; });
        Assert.True(Dm.truthy(r.In(() => Waiting.NoSleep(gun.try_fire_gun(rob, bob, aim)))));
        r.Step(2 * Second);
        float brute = r.In(() => Dm.num(rob.get_brute_loss()));
        Assert.True(brute > 0, "no bullet hit Rob");
        Assert.True(r.In(() => bob.dm_shots_landed) >= 1);
        Assert.Equal(0, r.In(() => rob.dm_shots_landed));
        r.AssertNoRuntimes();
    }
}
