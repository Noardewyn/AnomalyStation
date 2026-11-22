using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package MD1 on the converted MetaStation: its DeForest first aid stations, vitals displays and medical kiosk boot live, the
/// stations full and mounted, the displays connected to their operating tables, stasis beds and cryo cells, the kiosk with its wand,
/// and with no runtime from their files.
/// </summary>
public class MedicalMD1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public MedicalMD1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "wall_healer", "wall_vitals", "medical_kiosk", "scanner_wand" };

    [MetaFact]
    public void Metas_first_aid_stations_vitals_displays_and_kiosk_boot_live()
    {
        using (_meta.World.Enter())
        {
            int healers = 0, vitals = 0, kiosks = 0, inert = 0, unconnected = 0, empty = 0, wandless = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in locate(x, y, 1).dm_held)
                    {
                        switch (thing)
                        {
                            case obj_machinery_wall_healer healer:
                                healers++;
                                if (healer.num_bandages != 5 || healer.brute_healing <= 0)
                                    empty++;
                                break;
                            case obj_machinery_vitals_reader reader:
                                vitals++;
                                if (reader.connected == null)
                                    unconnected++;
                                break;
                            case obj_machinery_medical_kiosk kiosk:
                                kiosks++;
                                if (kiosk.scanner_wand == null)
                                    wandless++;
                                break;
                            default:
                                continue;
                        }
                        if (InertSeam.IsInert(thing))
                            inert++;
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {healers} first aid stations ({empty} not full), {vitals} vitals displays ({unconnected} unconnected), {kiosks} kiosks ({wandless} without a wand); {inert} inert; runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.Equal(16, healers);
            Assert.Equal(6, vitals);
            Assert.Equal(1, kiosks);
            Assert.Equal(0, inert);
            Assert.Equal(0, empty);
            Assert.Equal(0, wandless);
            Assert.Equal(0, unconnected);
            Assert.Empty(runtimes);
        }
    }
}
