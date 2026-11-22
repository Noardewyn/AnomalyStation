using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV422 (the owner, 2026-11-13) on tg's multi-z debug map through the real server: Space and Ctrl are tg's Move Upwards and Move Downwards for a
/// mob that can change level (a flyer, a ghost), and for one that cannot Space holds tg's throw mode and Ctrl does nothing.
/// </summary>
public class LevelKeyGameplayTests
{
    [Fact]
    public void SpaceAndCtrlTakeAFlyerAndAGhostBetweenLevelsAndAWalkersSpaceHoldsThrowMode()
    {
        string maps = MultizDebugBreachTests.RepoMaps();
        if (maps == null)
            return; // the converted maps are not here (tools/convert-maps.sh)
        using RealHeadlessWorld w = MultizDebugBreachTests.Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Lev");
        mob_living body = w.MobOf(p);
        float Z(mob m) { using (w.World.Enter()) return m.z; }
        using (w.World.Enter())
        {
            // under the station's inner open space of level 8 (MultizDebugBreachTests), on level 7's floor
            Assert.IsType<turf_open_openspace>(locate(13, 30, 8));
            Assert.IsAssignableFrom<turf_open_floor>(locate(13, 30, 7));
            Waiting.NoSleep(body.forceMove(locate(13, 30, 7)));
        }
        w.Run(20);

        // a walker: Space holds throw mode while held, Ctrl does nothing
        p.Session.Keybinding(KeybindingCode.ZLevelUp);
        w.Run(4);
        using (w.World.Enter())
            Assert.NotEqual(THROW_MODE_DISABLED, Dm.num(body.throw_mode));
        p.Session.Keybinding(KeybindingCode.ZLevelUp, up: true);
        p.Session.Keybinding(KeybindingCode.ZLevelDown);
        w.Run(40);
        using (w.World.Enter())
            Assert.Equal(THROW_MODE_DISABLED, Dm.num(body.throw_mode));
        Assert.Equal(7, Z(body));

        // a flyer: Space goes up through the open space after tg's second, Ctrl back down
        using (w.World.Enter())
            DevCommands.Fly(body);
        p.Session.Keybinding(KeybindingCode.ZLevelUp);
        w.Run(40);
        Assert.Equal(8, Z(body));
        using (w.World.Enter())
            Assert.Equal(THROW_MODE_DISABLED, Dm.num(body.throw_mode));
        p.Session.Keybinding(KeybindingCode.ZLevelDown);
        w.Run(40);
        Assert.Equal(7, Z(body));

        // a ghost: at once, through the floors
        using (w.World.Enter())
            body.ghostize(Dm.TRUE);
        w.Run(10);
        var ghost = Assert.IsType<mob_dead_observer>(w.World.Objects.TryGet(p.Client.OwnEntity, out object o) ? o : null);
        p.Session.Keybinding(KeybindingCode.ZLevelDown);
        w.Run(10);
        Assert.Equal(6, Z(ghost));
        p.Session.Keybinding(KeybindingCode.ZLevelUp);
        w.Run(10);
        Assert.Equal(7, Z(ghost));
    }

    [Fact]
    public void ThePullKeysCtrlClickWithTheCrosshairsPixelAndAimPullsACrate()
    {
        // DV422: the Pull key (H) sends what a Ctrl+left click at the crosshair sent: the target with its icon pixel and the camera's aim
        string maps = MultizDebugBreachTests.RepoMaps();
        if (maps == null)
            return;
        using RealHeadlessWorld w = MultizDebugBreachTests.Boot(maps);
        RealHeadlessWorld.Player p = w.Join("Puller");
        mob_living me = w.MobOf(p);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(locate(13, 30, 7)));
        w.Run(20);
        w.Host.EnqueueConsoleLine("spawn /obj/structure/closet/crate 12 30 7");
        w.Run(5);
        obj_structure_closet_crate crate;
        using (w.World.Enter())
            crate = Dm.each_cast<atom>(((turf)locate(12, 30, 7)).contents).OfType<obj_structure_closet_crate>().First();
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, crate.dm_id, 0, 0, 0, 17, 12), ClickButton.Left, ClickModifiers.Ctrl,
            new ShotAim(13 * 32, 30 * 32, 21, 49152, -2000)));
        w.Run(5);
        using (w.World.Enter())
            Assert.Same(crate, me.pulling);
    }
}
