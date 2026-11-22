using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// The owner (2026-11-21, revising DV528's lob of 2026-11-18): a 40mm grenade flies tg's straight line, the DV94 3D line of the crosshair.
/// Bob, on (3, 4), fires the grenade launcher with his crosshair on the floor of (9, 4) beside Carol on (10, 4): the shell flies the line down
/// to that floor, hits it (DV97) and goes off by tg's on_hit (the light blast reaches Carol).
/// </summary>
public class GrenadeShellTests
{
    private const int Second = 20;

    [Fact]
    public void AFortyMillimetreGrenadeFliesTheCrosshairsLineToTheFloorAndGoesOff()
    {
        var r = new AudioRoom(16, 9, seed: 1621);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        mob_living_carbon_human carol = r.Dummy(10, 4);
        obj_item_gun launcher = r.In(() =>
        {
            Waiting.NoSleep(Dm.@new<obj_item_implant_weapons_auth>().implant(bob, null, TRUE, TRUE));
            var gun = Dm.@new<obj_item_gun_ballistic_revolver_grenadelauncher_unrestricted>(bob.loc);
            Waiting.NoSleep(bob.put_in_active_hand(gun));
            return (obj_item_gun)gun;
        });
        r.Step(2);
        string @params = "icon-x=16;icon-y=16;left=1;button=left;" + ClientSeam.AimParam(3 * 32, 4 * 32, 20.8, 90, -Math.Atan2(20.8, 6 * 32) * 180 / Math.PI);
        r.In(() =>
        {
            FlightSeam.NoteAim(bob, params2list(@params)); // the click's aim, as ClientSeam.Click notes it
            Waiting.NoSleep(launcher.try_fire_gun(locate(9, 4, 1), bob, @params));
        });
        r.Step(1);
        obj_projectile_bullet_a40mm shell = r.In(() => Enumerable.Range(1, 16).SelectMany(x => Dm.each<obj_projectile_bullet_a40mm>(locate(x, 4, 1))).SingleOrDefault());
        Assert.NotNull(shell);
        Assert.True(r.In(() => shell.dm_shot)); // the 3D line the client draws it on
        Assert.Equal(1.25f * obj_projectile_bullet_a40mm.ShellSpeedShare, r.In(() => shell.speed)); // DV579: half tg's speed
        Assert.Equal(0.625f, r.In(() => Dm.@new<obj_projectile_bullet_rocket>(locate(1, 1, 1)).speed));
        Assert.True(r.In(() => shell.dm_slope) < 0, "it flies down toward the crosshair's floor");
        Assert.False(r.In(() => shell.dm_flight).IsMoving); // not thrown
        double before = r.In(() => Dm.num(carol.get_brute_loss()) + Dm.num(carol.get_fire_loss()));
        r.Step(2 * Second);
        Assert.True(r.In(() => QDELETED(shell)));
        Assert.True(r.In(() => Dm.num(carol.get_brute_loss()) + Dm.num(carol.get_fire_loss())) > before, "the blast hurt Carol");
        r.AssertNoRuntimes();
    }
}
