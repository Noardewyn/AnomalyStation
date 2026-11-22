using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV96 (the owner, 2026-11-21: most guns kicked like nothing): a gun's first-person kick is its kind's (FeltRecoil), scaled by its round's damage,
/// harder fired one-handed, and it is added to tg's own recoil on each shot (simulate_recoil), so even a laser kicks the view a little.
/// </summary>
public class FeltRecoilTests
{
    private const int LineY = 4, Second = 20;

    [Fact]
    public void AGunKicksByItsKindItsRoundAndItsHands()
    {
        var r = new AudioRoom(14, 7, seed: 9612);
        r.Step(Second);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        T Make<T>() where T : obj_item_gun => r.In(() => Dm.@new<T>(locate(3, LineY, 1)));
        var makarov = Make<obj_item_gun_ballistic_automatic_pistol>();
        var revolver = Make<obj_item_gun_ballistic_revolver_c38>();
        var laser = Make<obj_item_gun_energy_laser>();
        var shotgun = Make<obj_item_gun_ballistic_shotgun_riot>();
        r.Step();
        Assert.Equal(1.2f, r.In(() => FeltRecoil.DegreesOf(makarov, bob)), 3); // its kind's usual round: 9mm, 30 damage
        Assert.Equal(2f * MathF.Sqrt(25f / 60f), r.In(() => FeltRecoil.DegreesOf(revolver, bob)), 3); // a .38 kicks less than the .357 revolvers are set by
        Assert.Equal(0.4f, r.In(() => FeltRecoil.DegreesOf(laser, bob)), 3);
        Assert.Equal(6f, r.In(() => FeltRecoil.DegreesOf(shotgun, bob)), 3);
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(Dm.@new<obj_item_crowbar>(locate(3, LineY, 1)))));
        Assert.Equal(6f * FeltRecoil.OneHandFactor, r.In(() => FeltRecoil.DegreesOf(shotgun, bob)), 3); // tg's WEAPON_MEDIUM with the other hand full
        Assert.Equal(1.2f, r.In(() => FeltRecoil.DegreesOf(makarov, bob)), 3); // a pistol is a one-hand gun
        r.AssertNoRuntimes();
    }
}
