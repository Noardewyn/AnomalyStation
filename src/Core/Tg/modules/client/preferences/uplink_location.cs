// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/uplink_location.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file (the character's uplink spawn location, read by the mind's give_uplink). The var block is
// generated.

// tg: code/modules/client/preferences/uplink_location.dm:1
public partial class datum_preference_choiced_uplink_location : datum_preference_choiced
{
    // tg: code/modules/client/preferences/uplink_location.dm:8
    public override DmList init_possible_values()
    {
        return Dm.list(UPLINK_PDA, UPLINK_RADIO, UPLINK_PEN, UPLINK_IMPLANT);
    }

    // tg: code/modules/client/preferences/uplink_location.dm:11
    public override object compile_constant_data()
    {
        DmList data = (DmList)base.compile_constant_data();

        data[CHOICED_PREFERENCE_DISPLAY_NAMES] = Dm.list(
            Dm.kv(UPLINK_PDA, "PDA"),
            Dm.kv(UPLINK_RADIO, "Radio"),
            Dm.kv(UPLINK_PEN, "Pen"),
            Dm.kv(UPLINK_IMPLANT, Dm.text("Implant ([]TC)", UPLINK_IMPLANT_TELECRYSTAL_COST)));

        return data;
    }

    // tg: code/modules/client/preferences/uplink_location.dm:23
    public override object create_default_value()
    {
        return UPLINK_PDA;
    }

    // tg: code/modules/client/preferences/uplink_location.dm:26
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }
}
