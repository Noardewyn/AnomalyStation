using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Through the real server: a joined miner lays his hand on a pulsing tumor; it convulses, an elite climbs out of it and the
/// arena's walls rise around the two of them.
/// </summary>
public class EliteTumorGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget At(datum d) => new ClickTarget(TargetKind.Entity, d.dm_id, 0, 0, 0, 16, 16);

    [Fact]
    public void A_miner_wakes_an_elite_tumor_and_an_elite_climbs_out_into_its_arena()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13491, TimeoutSteps = 10000 });
        RealHeadlessWorld.Player p = w.Join("Miner");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        var tumor = In(w, () => Dm.@new<obj_structure_elite_tumor>(get_step(me, EAST)));
        w.Run(5);
        p.Session.Click(At(tumor), ClickButton.Left, ClickModifiers.None);
        w.Run(Second / 2);
        Assert.Equal(obj_structure_elite_tumor.TUMOR_ACTIVE, In(w, () => tumor.activity));
        Assert.True(In(w, () => HAS_TRAIT(me, TRAIT_ELITE_CHALLENGER)), "the miner is the challenger");

        w.Run(4 * Second);
        var elite = In(w, () => tumor.mychild);
        Assert.NotNull(elite);
        output.WriteLine($"{In(w, () => elite.name)} climbed out of the tumor");
        Assert.Equal("tumor_popped", In(w, () => tumor.icon_state));
        Assert.True(In(w, () => Dm.each<obj_effect_temp_visual_elite_tumor_wall>(range(13, tumor)).Any()), "the arena's walls rose");
        // the miner gets out of it before the elite gets him
        Do(w, () => elite.toggle_ai(AI_OFF));
        Do(w, () => qdel(elite));
        w.Run(Second * 6);
        Assert.True(In(w, () => QDELETED(tumor)), "the tumor gave way once its elite was gone");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
