// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/tram_malfunction.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E2c (D327): the whole file: Tramstation's tram comes to an emergency stop for four to seven minutes unless engineering resets
// it (the tram controller's malfunction, live since the trams). The var blocks are generated (D111).

public partial class datum_round_event_control_tram_malfunction : datum_round_event_control
{
    //Check if there's a tram we can cause to malfunction.
    // tg: code/modules/events/tram_malfunction.dm:16
    public override object can_spawn_event(object players_amt = null, float? allow_magic = null)
    {
        object dot = base.can_spawn_event(players_amt, allow_magic);
        if (!Dm.truthy(dot))
            return FALSE;

        foreach (object tram_id in Dm.each(SStransport.transports_by_type))
        {
            var tram_ref = ((DmList)SStransport.transports_by_type[tram_id])[1] as datum_transport_controller_linear_tram;
            if (tram_ref != null && Dm.eq(tram_ref.specific_transport_id, TRAMSTATION_LINE_1))
                return dot;
        }

        return FALSE;
    }
}

// tg: code/modules/events/tram_malfunction.dm:29
public partial class datum_round_event_tram_malfunction : datum_round_event
{
    private const int TRAM_MALFUNCTION_TIME_UPPER = 210;
    private const int TRAM_MALFUNCTION_TIME_LOWER = 120;

    // tg: code/modules/events/tram_malfunction.dm:35
    public override async DmTask<object> setup()
    {
        end_when = rand(TRAM_MALFUNCTION_TIME_LOWER, TRAM_MALFUNCTION_TIME_UPPER);
        return null;
    }

    // tg: code/modules/events/tram_malfunction.dm:38
    public override async DmTask<object> start()
    {
        foreach (datum_transport_controller_linear_tram malfunctioning_controller in Dm.each_cast<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(malfunctioning_controller.specific_transport_id, specific_transport_id))
            {
                malfunctioning_controller.start_malf_event();
                return null;
            }
        }
        return null;
    }

    // tg: code/modules/events/tram_malfunction.dm:44
    public override async DmTask end()
    {
        foreach (datum_transport_controller_linear_tram malfunctioning_controller in Dm.each_cast<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
        {
            if (Dm.eq(malfunctioning_controller.specific_transport_id, specific_transport_id) && !Dm.eq(malfunctioning_controller.malf_active, TRANSPORT_SYSTEM_NORMAL))
            {
                malfunctioning_controller.end_malf_event();
                return;
            }
        }
    }
}
