// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/airlock_control.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the airlock sensor (:42-94, the engineering package E8, D287) is here; the airlock's part of the file (:1-40:
// secure_open, secure_close, its var overrides) lives in doors/airlock.cs (M4b), its mouse_drop_receive and on_magic_unlock are
// rows of live-partial.txt. The sensor's var blocks are generated.

// tg: code/game/machinery/airlock_control.dm:42
public partial class obj_machinery_airlock_sensor : obj_machinery
{
    // tg: code/game/machinery/airlock_control.dm:68
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(on))
            icon_state = Dm.text("[]_off", base_icon_state);
        else
        {
            if (Dm.truthy(alert))
                icon_state = Dm.text("[]_alert", base_icon_state);
            else
                icon_state = Dm.text("[]_standby", base_icon_state);
        }
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/airlock_control.dm:78
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        var airlock_controller = GLOB.objects_by_id_tag[master_tag] as obj_machinery_airlock_controller;
        airlock_controller?.cycle();

        flick("airlock_sensor_cycle", this);
        return dot;
    }

    // tg: code/game/machinery/airlock_control.dm:88
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(on))
        {
            // return_air is coloured (an override can sleep); the turf's own does not (waiting-model.md 5.3)
            var air_sample = Waiting.NoSleep(return_air()) as datum_gas_mixture;
            float pressure = round(Dm.num(air_sample.return_pressure()), 0.1f);
            alert = Dm.num(pressure < ONE_ATMOSPHERE * 0.8f);
        }

        update_appearance();
        return null;
    }
}
