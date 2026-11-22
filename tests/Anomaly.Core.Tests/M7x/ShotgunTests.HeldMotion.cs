using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tests.M7x;

public partial class ShotgunTests
{
    private sealed class Motions : IHeldMotionSink
    {
        public readonly List<(mob User, HeldMotion Motion)> Seen = new();

        public void Moved(mob user, HeldMotion motion) => Seen.Add((user, motion));
    }

    // DV213: the pump, an unload and a shell put in each tell the host seam who worked the gun and how (the server's HeldMotion message)
    [Fact]
    public void TheShotgunsPumpUnloadAndShellsAreHeldMotionsOfItsUser()
    {
        AudioRoom r = Room(7969);
        var motions = new Motions();
        HeldMotionSeam.Install(r.World, motions);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_gun_ballistic_shotgun_riot gun = Arm<obj_item_gun_ballistic_shotgun_riot>(r, bob);

        r.In(() => Waiting.NoSleep(gun.attack_self(bob)));
        Assert.Equal((bob, HeldMotion.Rack), Assert.Single(motions.Seen));

        motions.Seen.Clear();
        r.In(() => Waiting.NoSleep(gun.unload_ammo(bob, TRUE)));
        Assert.Equal((bob, HeldMotion.Unload), Assert.Single(motions.Seen));

        motions.Seen.Clear();
        obj_item_ammo_casing_shotgun_buckshot shell = r.In(() => Dm.@new<obj_item_ammo_casing_shotgun_buckshot>(locate(3, LineY, 1)));
        Assert.Equal(ITEM_INTERACT_SUCCESS, Click(r, bob, gun, shell));
        Assert.Equal((bob, HeldMotion.Load), Assert.Single(motions.Seen));
        r.AssertNoRuntimes();
    }
}
