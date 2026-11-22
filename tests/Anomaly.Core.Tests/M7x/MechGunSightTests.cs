using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// DV361 and DV96 (the owner, 2026-11-21: a mech's guns are a shooter's guns): Bob pilots a loaded dark Gygax, its left arm tg's scattershot,
/// its right arm a laser. His crosshair is the wider arm's cone (half the scattershot's variance); the laser fired at Rob marks the right arm
/// on its firing effect (its line starts to the right of the heading, ShotGeometry.MarkArm) and its hit counts for Bob's hit marker (tg's
/// firer is the chassis).
/// </summary>
public class MechGunSightTests
{
    private const int LineY = 4, Second = 20;

    [Fact]
    public void APilotsSightIsHisArmsAndTheArmThatFiredIsMarked()
    {
        var r = new AudioRoom(16, 9, seed: 4243);
        mob_living_carbon_human bob = r.Join(2, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(8, LineY);
        var mech = r.In(() => Dm.@new<obj_vehicle_sealed_mecha_gygax_dark_loaded>(Builtins.locate(3, LineY, 1)));
        r.Step(Second);
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(mech.mob_enter(bob)))));
        r.Step(2);
        Assert.Same(mech, r.In(() => bob.loc));
        var left = r.In(() => mech.equip_by_category[Defines.MECHA_L_ARM] as obj_item_mecha_parts_mecha_equipment_weapon);
        Assert.NotNull(left);
        var laser = r.In(() => Dm.@new<obj_item_mecha_parts_mecha_equipment_weapon_energy_laser>(mech.loc));
        r.In(() => Waiting.NoSleep(laser.attach(mech, Dm.TRUE)));
        r.Step();
        Assert.Same(laser, r.In(() => mech.equip_by_category[Defines.MECHA_R_ARM]));
        float variance = r.In(() => left.variance);
        Assert.True(variance > 0);
        Assert.Equal(0.5f * variance, r.In(() => GunSight.ConeOf(bob)).Value, 3);

        string aim = "icon-x=16;icon-y=16;left=1;button=right;" + ClientSeam.AimParam(3 * 32, LineY * 32, 28.8, 90, -4.5); // the pilot's eye (1.8 m) down at Rob's middle
        r.In(() => { mech.weapons_safety = Dm.FALSE; return 0; });
        r.In(() => { Waiting.Detach(laser.action(bob, rob, Dm.params2list(aim))); return 0; });
        r.Step();
        obj_effect firing = r.In(() => Dm.each<obj_effect_temp_visual_dir_setting_firing_effect>(mech.vis_contents).FirstOrDefault());
        Assert.NotNull(firing);
        Assert.True(r.In(() => firing.dm_shot));
        Assert.Equal(3 * 32f, r.In(() => firing.dm_origin_x), 1);
        Assert.Equal(LineY * 32f - ShotGeometry.MechArmSidePixels, r.In(() => firing.dm_origin_y), 1); // east's right is south
        r.Step(2 * Second);
        Assert.True(r.In(() => Dm.num(rob.get_fire_loss())) > 0, "the laser missed Rob");
        Assert.True(r.In(() => bob.dm_shots_landed) >= 1);
        r.AssertNoRuntimes();
    }
}
