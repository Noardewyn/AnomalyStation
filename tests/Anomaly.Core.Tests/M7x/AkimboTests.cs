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
/// tg's dual wielding (gun.dm:424-437): in combat mode a light gun in the other hand fires too, a tick later, and every gun fired so
/// adds its <c>dual_wield_spread</c> (24) to the bonus spread of the shots.
/// </summary>
public class AkimboTests
{
    private const int Width = 20, Height = 9, LineY = 4;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(Width, Height, seed: seed);
        r.Step(20);
        return (r, r.Dummy(2, LineY));
    }

    private static obj_item_gun Gun(AudioRoom r, mob_living_carbon_human bob, bool left)
    {
        obj_item_gun gun = r.In(() => Dm.@new<obj_item_gun_ballistic_automatic_mini_uzi>(locate(2, LineY, 1)));
        r.In(() => Waiting.NoSleep(left ? bob.put_in_l_hand(gun) : bob.put_in_r_hand(gun)));
        return gun;
    }

    // The mean straying of each fired shot's heading from due east (90), over volleys at a wall far east.
    private static (int Shots, double Mean) Fire(bool akimbo, ulong seed)
    {
        var (r, bob) = Room(seed);
        obj_item_gun main = Gun(r, bob, left: false);
        if (akimbo)
            Gun(r, bob, left: true);
        r.In(() => bob.set_combat_mode(TRUE));
        r.In(() => bob.swap_hand(bob.get_held_index_of_item(main)));
        atom target = r.In(() => locate(Width - 1, LineY, 1));
        int shots = 0;
        double sum = 0;
        for (int volley = 0; volley < 6; volley++)
        {
            r.In(() => { bob.next_click = -1; bob.next_move = -1; });
            r.In(() => Waiting.NoSleep(main.try_fire_gun(target, bob, "icon-x=16;icon-y=16")));
            for (int i = 0; i < 8; i++)
            {
                r.Step();
                foreach (var p in r.In(() => Dm.each<obj_projectile>().Where(p => Dm.truthy(p.fired)).ToList()))
                {
                    shots++;
                    sum += Math.Abs(r.In(() => Dm.num(p.angle)) - 90);
                    r.In(() => qdel(p));
                }
            }
            r.Step(20);
        }
        r.AssertNoRuntimes();
        return (shots, shots == 0 ? 0 : sum / shots);
    }

    [Fact]
    public void BothGunsFireAndScatterWider()
    {
        var one = Fire(akimbo: false, 9101);
        var two = Fire(akimbo: true, 9101);
        Assert.True(two.Shots >= 2 * one.Shots - 2, $"one gun fired {one.Shots} shots, two fired {two.Shots}");
        Assert.True(two.Mean > one.Mean + 2, $"the mean straying: one gun {one.Mean:F1} degrees, two {two.Mean:F1}");
    }

    // The crosshair shows the penalty: 0.5 x 1.4 x 24 at the server's share (0.5), only in combat mode, as the shot takes it.
    [Fact]
    public void TheCrosshairOpensForASecondLightGunInCombatMode()
    {
        var (r, bob) = Room(9102);
        obj_item_gun main = Gun(r, bob, left: false);
        r.In(() => bob.swap_hand(bob.get_held_index_of_item(main)));
        r.In(() => bob.set_combat_mode(TRUE));
        float scale = r.In(() => ShotSpread.ScaleOf(r.World));
        Assert.Equal(0f, r.In(() => GunSight.ConeOf(bob)));
        Gun(r, bob, left: true);
        Assert.Equal(0.5f * 1.4f * 24 * scale, r.In(() => GunSight.ConeOf(bob)!.Value), 3);
        r.In(() => bob.set_combat_mode(FALSE));
        Assert.Equal(0f, r.In(() => GunSight.ConeOf(bob)));
        r.AssertNoRuntimes();
    }
}
