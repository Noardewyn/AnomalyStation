// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/miningradio.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J6 (D298): the file whole; the weather it reports stays quiet without a mining level (E16; SSweather is gaps E2a).

/// Portable mining radio purchasable by miners
// tg: code/modules/mining/equipment/miningradio.dm:3
public partial class obj_item_radio_weather_monitor : obj_item_radio
{
    // tg: code/modules/mining/equipment/miningradio.dm:11
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        Dm.Deferred("code/modules/mining/equipment/miningradio.dm:13", "update_overlays: emissive_appearance of the radio (DEVIATION DV12)");
        return dot;
    }

    // tg: code/modules/mining/equipment/miningradio.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_weather_announcer),
            Dm.named("state_normal", "weatherwarning"),
            Dm.named("state_warning", "urgentwarning"),
            Dm.named("state_danger", "direwarning"),
            Dm.named("radar_z_trait", ZTRAIT_MINING)));
        set_frequency(FREQ_SUPPLY);
        return dot;
    }
}
