using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV56 through the real server on MetaStation (the owner, 2026-10-20): a thing a 3D model draws keeps its facing when it is pushed, and a free-standing
/// one is turned by hand with tg's simple_rotation, Alt-click a quarter left and Alt-right-click a quarter right, which the radial shows as tg's
/// context entries; a chair keeps its own element (one turn a click, not two).
/// </summary>
public class RotateByHandGameplayTests
{
    private const int Second = 20; // ticks per second
    private const int StandX = 45, StandY = 154; // the Auxiliary Base Construction zone's plating (FurnitureGameplayTests)

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static int DirOf(RealHeadlessWorld w, atom a) => In(w, () => (int)Dm.num(a.dir));

    private static void AltClick(RealHeadlessWorld w, RealHeadlessWorld.Player p, atom a, ClickButton button)
    {
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16), button, ClickModifiers.Alt));
        w.Run(Second);
    }

    [MetaServerFact]
    public void A_canister_turns_by_hand_and_keeps_its_facing_when_pushed_and_a_chair_turns_once_a_click()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        int z = In(w, () => (int)me.z);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(StandX, StandY, z)));
        w.Run(Second);

        var canister = In(w, () => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(Builtins.locate(StandX + 1, StandY, z)));
        var chair = In(w, () => Dm.@new<obj_structure_chair>(Builtins.locate(StandX - 1, StandY, z)));
        w.Run(Second);
        Assert.Equal(Dm.FALSE, In(w, () => Dm.num(canister.set_dir_on_move)));
        int start = DirOf(w, canister);

        // A canister's Alt-click is its own (tg: the held tank comes out; with none it does nothing), so it turns by Alt-right-click, a quarter clockwise.
        AltClick(w, p, canister, ClickButton.Left);
        Assert.Equal(start, DirOf(w, canister));
        AltClick(w, p, canister, ClickButton.Right);
        Assert.Equal(In(w, () => (int)Dm.turn(start, Defines.ROTATION_CLOCKWISE)), DirOf(w, canister));
        for (int i = 0; i < 3; i++)
            AltClick(w, p, canister, ClickButton.Right);
        Assert.Equal(start, DirOf(w, canister)); // four clicks go round

        // Its context entry (the radial's): "Rotate right" only.
        List<string> labels = In(w, () =>
        {
            var list = Dm.list();
            canister._SendSignal(Defines.COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, Dm.list(canister, list, null, me));
            return Dm.each(list).Select(k => list[k] as string).ToList();
        });
        Assert.Contains("Rotate right", labels);
        Assert.DoesNotContain("Rotate left", labels);

        // A push (a Move as a bump makes) does not turn it.
        using (w.World.Enter())
            Waiting.NoSleep(canister.Move(Builtins.get_step(canister, Dm.EAST), Dm.EAST));
        Assert.Equal(start, DirOf(w, canister));

        // The chair has tg's own element; the catalog adds none, so one click is one quarter.
        int chairStart = DirOf(w, chair);
        AltClick(w, p, chair, ClickButton.Left);
        Assert.Equal(In(w, () => (int)Dm.turn(chairStart, 90)), DirOf(w, chair));

        // A microwave is bolted down (tg's machinery, anchored): tg's simple_rotation refuses it ("need to unwrench!").
        var microwave = In(w, () => Dm.@new<obj_machinery_microwave>(Builtins.locate(StandX + 1, StandY, z)));
        w.Run(Second);
        int ovenStart = DirOf(w, microwave);
        AltClick(w, p, microwave, ClickButton.Left);
        Assert.Equal(ovenStart, DirOf(w, microwave));
    }
}
