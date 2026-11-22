using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package TY1 on the converted MetaStation: the toys mapped there (the arcade's prizes, the clown's and the curator's toys, the
/// action figures and the dummies) boot live, with no runtime from TY1's files.
/// </summary>
public class ToysTY1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ToysTY1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "toys.cs", "toy_mechs.cs", "latexballoon.cs", "hourglass.cs", "chatter.cs" };

    [MetaFact]
    public void Metas_toys_boot_live()
    {
        var types = new HashSet<Type>(ToysTY1Tests.AllTypes);
        for (int tick = 0; tick < 40; tick++)
            _meta.World.Step();
        using (_meta.World.Enter())
        {
            var found = new Dictionary<string, int>();
            int toys = 0, inert = 0;
            for (int z = 1; z <= world.maxz; z++)
            {
                for (int y = 1; y <= 255; y++)
                {
                    for (int x = 1; x <= 255; x++)
                    {
                        if (locate(x, y, z) is not turf t)
                            continue;
                        foreach (atom thing in t.dm_held)
                        {
                            foreach (atom toy in new[] { thing }.Concat(Dm.each<atom>(thing.contents)))
                            {
                                if (!types.Contains(toy.GetType()))
                                    continue;
                                toys++;
                                found[toy.GetType().Name] = found.GetValueOrDefault(toy.GetType().Name) + 1;
                                if (InertSeam.IsInert(toy))
                                    inert++;
                            }
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {toys} toys of TY1, {inert} inert, TY1 runtimes {runtimes.Count}: " + string.Join(", ", found.OrderBy(k => k.Key).Select(k => k.Key + " x" + k.Value)));
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(toys >= 5, "too few toys");
            Assert.Equal(0, inert);
            Assert.Empty(runtimes);
        }
    }
}
