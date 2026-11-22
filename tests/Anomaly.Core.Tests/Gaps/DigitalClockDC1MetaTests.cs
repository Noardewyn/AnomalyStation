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
/// Gaps package DC1 on the converted MetaStation: the digital clocks boot live, mounted on their walls, processing on their own
/// subsystem, and the powered ones show their digits, with no runtime from DC1's files.
/// </summary>
public class DigitalClockDC1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public DigitalClockDC1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files = { "digital_clock.cs" };

    [MetaFact]
    public void Metas_digital_clocks_boot_live()
    {
        for (int tick = 0; tick < 40; tick++) // two seconds: the clocks' subsystem fires
            _meta.World.Step();
        using (_meta.World.Enter())
        {
            int clocks = 0, inert = 0, unmounted = 0, idle = 0, showing = 0;
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
                            if (thing is not obj_machinery_digital_clock clock)
                                continue;
                            clocks++;
                            if (InertSeam.IsInert(clock))
                                inert++;
                            if (clock.GetComponent(typeof(datum_component_atom_mounted)) == null)
                                unmounted++;
                            if (SSdigital_clock == null || !Dm.@in(clock, SSdigital_clock.processing))
                                idle++;
                            if (Dm.each(clock.overlays).OfType<IDmAppearance>().Any(o => o.icon_state == "+separator"))
                                showing++;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine($"Meta: {clocks} digital clocks, {inert} inert, {unmounted} unmounted, {idle} not processing, {showing} showing the time; DC1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(clocks >= 10, "too few digital clocks");
            Assert.Equal(0, inert);
            Assert.Equal(0, unmounted);
            Assert.Equal(0, idle);
            Assert.True(showing >= 1, "no clock shows the time");
            Assert.Empty(runtimes);
        }
    }
}
