using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RD4 on the converted MetaStation: the science department's E.X.P.E.R.I-MENTOR boots live, linked to the station's
/// techweb with its reaction handlers loaded, the relics left in maintenance boot live too, and no runtime from the experimentor's or
/// the relics' files.
/// </summary>
public class ExperimentorRD4MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ExperimentorRD4MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "experimentor", "relics", "shielded", "magic.dm" };

    [MetaFact]
    public void Metas_experimentor_boots_live_and_linked()
    {
        using (_meta.World.Enter())
        {
            int experimentors = 0, relics = 0, inert = 0, unlinked = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in Dm.each<atom>(locate(x, y, 1)))
                    {
                        Count(thing);
                        foreach (atom inner in Dm.each<atom>(thing)) // a relic in a closet or a crate
                            Count(inner);
                    }
                }
            }

            void Count(atom thing)
            {
                if (thing is obj_machinery_rnd_experimentor experimentor)
                {
                    experimentors++;
                    if (experimentor.stored_research == null || Dm.length(experimentor.experimentor_result_handlers) != 8)
                        unlinked++;
                }
                else if (thing is obj_item_assembly_relic)
                    relics++;
                else
                    return;
                if (InertSeam.IsInert(thing))
                    inert++;
            }

            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {experimentors} experimentor(s), {relics} relic(s); {inert} inert, {unlinked} unlinked; experimentor runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Equal(1, experimentors);
            Assert.Equal(0, inert);
            Assert.Equal(0, unlinked);
            Assert.Empty(runtimes);
        }
    }
}
