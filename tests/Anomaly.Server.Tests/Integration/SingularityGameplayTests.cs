using System.Diagnostics;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M5x slice 5 (D149, D150) through the real server on MetaStation with a headless Godot client: a player's dev
/// <c>singularity 146 178 1</c> (600 energy by default: stage 3) in the Recreation Area. For 30 s it roams and eats: the turfs around
/// it scraped to space, its energy grown by what it ate, the items pulled toward it; the client sees its entity with tg's stage-3
/// picture (icons/effects/160x160.dmi "singularity_s5" at -64, -64). The server's tick times over the run go to the test output.
/// </summary>
public class SingularityGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public SingularityGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void A_dev_singularity_in_the_recreation_area_roams_eats_and_grows()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 43, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        ReplicaWorld replica = p.Session.Replica;
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        using (w.World.Enter())
            Waiting.NoSleep(w.MobOf(p).forceMove(Builtins.locate(146, 160, 1))); // 18 tiles south: outside its pull
        w.Run(Second);

        int SpaceAround() => In(w, () => Enumerable.Range(126, 41).SelectMany(x => Enumerable.Range(158, 41).Select(y => Builtins.locate(x, y, 1))).Count(t => t is turf_open_space));
        int spaceBefore = SpaceAround();
        p.Session.DevCommand("singularity 146 178 1");
        for (int i = 0; i < 20 && texts.All(t => t.Contains("Payday", StringComparison.Ordinal)); i++) // a payday line may come first
            w.Run(1);
        _output.WriteLine("reply: " + string.Join(" | ", texts));
        Assert.Contains(texts, t => t.Contains("stage 3 (5 x 5 tiles)", StringComparison.Ordinal)); // STAGE_THREE is 5 tiles (gravity.dm)
        obj_singularity singulo = In(w, () => Dm.each_cast<atom>(Builtins.locate(146, 178, 1).contents).OfType<obj_singularity>().FirstOrDefault()
            ?? Dm.each_cast<datum_component_singularity>(GLOB.singularities).Select(c => c.parent as obj_singularity).FirstOrDefault());
        Assert.NotNull(singulo);
        float energyStart = In(w, () => singulo.energy);

        w.Run(2);
        ReplicaEntity seen = replica.Entities.FirstOrDefault(e => e.Id == singulo.dm_id);
        Assert.NotNull(seen);
        Assert.True(replica.TryGetAppearance(seen.State.Appearance, out AppearanceEntry look));
        Assert.Equal("singularity_s5", replica.ResolveString(look.IconState));
        Assert.Equal("icons/effects/160x160.dmi", replica.ResolveResource(look.Icon));
        Assert.Equal(-64, look.PixelX);

        var sw = new Stopwatch();
        double worst = 0, total = 0;
        int startX = In(w, () => (int)singulo.x), startY = In(w, () => (int)singulo.y);
        for (int t = 0; t < 30 * Second; t++)
        {
            sw.Restart();
            w.Run(1);
            double ms = sw.Elapsed.TotalMilliseconds;
            worst = Math.Max(worst, ms);
            total += ms;
            if (In(w, () => QDELETED(singulo)))
                break;
        }
        int spaceAfter = SpaceAround();
        _output.WriteLine($"30 s: energy {energyStart} -> {In(w, () => singulo.energy)}, stage {In(w, () => singulo.current_size)}, at ({In(w, () => singulo.x)},{In(w, () => singulo.y)}) from ({startX},{startY}); space tiles around {spaceBefore} -> {spaceAfter}; ticks: mean {total / (30 * Second):0.0} ms, worst {worst:0.0} ms");
        Assert.False(In(w, () => QDELETED(singulo)), "a stage 3 with 600 energy does not collapse in 30 s");
        Assert.True(spaceAfter > spaceBefore + 20, "it ate the floor around it");
        Assert.True(In(w, () => singulo.energy) > energyStart, "it grew by what it ate");
    }
}
