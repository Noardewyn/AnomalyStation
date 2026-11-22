using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package RD0 (D333, the research machines' base): an R&amp;D device takes the techweb a multitool buffers, opens its panel with
/// a screwdriver (tg's "_t" picture), and its wires hack and disable it while the panel is open.
/// </summary>
public class ResearchRD0Tests
{
    [Fact]
    public void AnRndDeviceLinksABufferedTechwebOpensItsPanelAndItsWiresHackIt()
    {
        var r = new AudioRoom(13, 9, seed: 7501);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var device = Place<obj_machinery_rnd>(r, 5, 4); // the base device itself: its fabricators and analyzers come with RD1 and RD3
        datum_techweb web = r.In(() => Dm.each<datum_techweb>(SSresearch.techwebs).FirstOrDefault());
        Assert.NotNull(web);

        // a multitool holding the station's techweb links it (the toy room has no research server to link at round start)
        var multitool = r.In(() => Dm.@new<obj_item_multitool>(bob.loc));
        r.In(() => multitool.buffer = web);
        r.In(() => Waiting.NoSleep(device.connect_techweb(null)));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(device.multitool_act(bob, multitool))));
        r.In(() => Assert.Same(web, device.stored_research));

        // the screwdriver opens the panel, and the picture shows it
        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(bob.loc));
        string closed = r.In(() => device.icon_state);
        r.In(() => Waiting.NoSleep(device.screwdriver_act(bob, screwdriver)));
        r.In(() =>
        {
            Assert.True(Dm.truthy(device.panel_open));
            Assert.Equal(closed + "_t", device.icon_state);
        });

        // its wires: the hack wire's pulse hacks it, cutting the disable wire disables it
        r.In(() =>
        {
            var wires = (datum_wires_rnd)device.wires;
            Assert.True(wires.interactable(bob));
            Waiting.NoSleep(wires.on_pulse(WIRE_HACK));
            Assert.True(Dm.truthy(device.hacked));
            Waiting.NoSleep(wires.on_cut(WIRE_DISABLE, FALSE));
            Assert.True(Dm.truthy(device.disabled));
            Assert.False(Dm.truthy(device.is_insertion_ready(bob)));
        });
        r.AssertNoRuntimes();
    }
}
