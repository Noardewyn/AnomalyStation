// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/communications_blackout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the communications blackout (every telecomms machine and the trams' telecomms controllers take a
// heavy EMP; the AIs always hear of it, the crew a third of the time). The var blocks are generated (D111).

// tg: code/modules/events/communications_blackout.dm:10
public partial class datum_round_event_communications_blackout : datum_round_event
{
    // tg: code/modules/events/communications_blackout.dm:13
    public override async DmTask announce__proc(object fake = null)
    {
        object alert = pick("Ionospheric anomalies detected. Temporary telecommunication failure imminent. Please contact you*%fj00)`5vc-BZZT",
            "Ionospheric anomalies detected. Temporary telecommunication failu*3mga;b4;'1v¬-BZZZT",
            "Ionospheric anomalies detected. Temporary telec#MCi46:5.;@63-BZZZZT",
            "Ionospheric anomalies dete'fZ\\kg5_0-BZZZZZT",
            "Ionospheri:%£ MCayj^j<.3-BZZZZZZT",
            "#4nd%;f4y6,>£%-BZZZZZZZT");

        foreach (mob_living_silicon_ai A in Dm.each<mob_living_silicon_ai>(GLOB.ai_list)) //AIs are always aware of communication blackouts.
        {
            to_chat(A, Dm.text("<br>[]<br>", span_warning(Dm.text("<b>[]</b>", alert))));
            to_chat(A, span_notice(I18n.T("Remember, you can transmit over holopads by right clicking on them, and can speak through them with \".[]\".", Dm.initial(typeof(datum_saymode_holopad), (datum_saymode p) => p.key))));
        }

        if (prob(30) || Dm.truthy(fake)) //most of the time, we don't want an announcement, so as to allow AIs to fake blackouts.
            priority_announce(alert, "Anomaly Alert");
    }

    // tg: code/modules/events/communications_blackout.dm:30
    public override async DmTask<object> start()
    {
        foreach (obj_machinery_telecomms shhh in Dm.each_cast<obj_machinery_telecomms>(GLOB.telecomm_machines))
            await shhh.emp_act(EMP_HEAVY);
        foreach (datum_transport_controller_linear_tram transport in Dm.each_cast<datum_transport_controller_linear_tram>(SStransport?.transports_by_type?[TRANSPORT_TYPE_TRAM]))
        {
            if (transport.home_controller != null)
            {
                obj_machinery_transport_tram_controller_tcomms controller = transport.home_controller;
                await controller.emp_act(EMP_HEAVY);
            }
        }
        return null;
    }
}
