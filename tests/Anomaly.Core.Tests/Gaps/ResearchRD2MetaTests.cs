using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RD2 on the converted MetaStation: the R&amp;D servers (the master with its hard drive), the R&amp;D consoles and the server
/// control console boot live, on the station's science techweb (the servers mining it, the consoles reading it), with no runtime from
/// the research files.
/// </summary>
public class ResearchRD2MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public ResearchRD2MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "rdconsole.cs", "server.cs", "server_control.cs", "research_disk.cs", "_design_disk.cs", "programs/techweb.cs" };

    [MetaFact]
    public void Metas_research_servers_and_consoles_boot_live_on_the_science_web()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, unlinked = 0;
            var inertKinds = new SortedSet<string>(StringComparer.Ordinal);
            datum_techweb science = Dm.locate<datum_techweb_science>(SSresearch.techwebs);
            Assert.NotNull(science);
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        (string kind, datum_techweb web) = thing switch
                        {
                            obj_machinery_rnd_server_master m => ("master server", m.stored_research),
                            obj_machinery_rnd_server s => ("server", s.stored_research),
                            obj_machinery_computer_rdconsole c => ("R&D console", c.stored_research),
                            obj_machinery_computer_rdservercontrol c => ("server control", c.stored_research),
                            _ => (null, null),
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                        {
                            inert++;
                            inertKinds.Add(thing.GetType().Name);
                        }
                        if (web != science)
                            unlinked++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key)) + $"; {inert} inert ({string.Join(", ", inertKinds)}), {unlinked} off the science web; research runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("master server") >= 1, "no master server");
            Assert.True(counts.GetValueOrDefault("R&D console") >= 1, "no R&D console");
            Assert.True(counts.GetValueOrDefault("server control") >= 1, "no server control console");
            Assert.Equal(0, inert);
            Assert.Equal(0, unlinked);
            Assert.True(Dm.each(science.techweb_servers).Count() >= 2, "the servers did not join the science web");
            Assert.True(Dm.each(science.consoles_accessing).Count() >= counts.GetValueOrDefault("R&D console"));
            Assert.Empty(runtimes);
        }
    }
}
