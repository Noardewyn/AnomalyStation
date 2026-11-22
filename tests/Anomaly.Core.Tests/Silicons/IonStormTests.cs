using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI11 (D326): the ion storm round event. Its start scrambles every law rack (an ion law for the rack's lawset, as the event's
/// chances roll) and its announcement is tg's anomaly alert; the round event control is tg's, AI category, weight 15.
/// </summary>
public class IonStormTests
{
    private const int Second = 20;

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[] { typeof(datum_round_event_ion_storm), typeof(datum_round_event_ion_storm_add_law_only), typeof(datum_round_event_control_ion_storm) })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AnIonStormGivesTheRacksAnIonLaw()
    {
        var r = new AudioRoom(11, 9, seed: 9961);
        r.Join(3, 5, "Bob");
        r.In(() => (get_area(locate(5, 5, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        var rack = r.In(() => Dm.@new<obj_machinery_ai_law_rack_base_core>(locate(6, 5, 1)));
        r.In(() =>
        {
            rack.anchored = TRUE;
            rack.secured = TRUE;
            Waiting.NoSleep(rack.load_config_law());
        });
        r.Step(2);
        float before = r.In(() => Dm.length(rack.combined_lawset.inherent));
        r.In(() =>
        {
            var storm = Dm.@new<datum_round_event_ion_storm_add_law_only>();
            storm.ionMessage = "THE STATION IS A SPACESHIP";
            Waiting.NoSleep(storm.start());
            Waiting.NoSleep(storm.announce__proc(TRUE));
        });
        r.In(() =>
        {
            Assert.True(Dm.truthy(rack.combined_lawset.inherent["THE STATION IS A SPACESHIP"]), "the rack took the ion law");
            Assert.True(Dm.length(rack.combined_lawset.inherent) >= before);
        });
        r.AssertNoRuntimes();
    }
}
