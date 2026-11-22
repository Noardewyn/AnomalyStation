// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/processor_overload.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1b (D327): the whole file, the processor overload (the telecomms processors take a heavy EMP, one in ten blows up instead).
// The var blocks are generated (D111).

// tg: code/modules/events/processor_overload.dm:9
public partial class datum_round_event_processor_overload : datum_round_event
{
    // tg: code/modules/events/processor_overload.dm:12
    public override async DmTask announce__proc(object fake = null)
    {
        object alert = pick("Exospheric bubble inbound. Processor overload is likely. Please contact you*%xp25)`6cq-BZZT",
            "Exospheric bubble inbound. Processor overload is likel*1eta;c5;'1v¬-BZZZT",
            "Exospheric bubble inbound. Processor ov#MCi46:5.;@63-BZZZZT",
            "Exospheric bubble inbo'Fz\\k55_@-BZZZZZT",
            "Exospheri:%£ QCbyj^j</.3-BZZZZZZT",
            "!!hy%;f3l7e,<$^-BZZZZZZZT");

        //AIs are always aware of processor overload
        foreach (mob_living_silicon_ai ai in Dm.each<mob_living_silicon_ai>(GLOB.ai_list))
            to_chat(ai, Dm.text("<br>[]<br>", span_warning(Dm.text("<b>[]</b>", alert))));

        // Announce most of the time, but leave a little gap so people don't know
        // whether it's, say, a tesla zapping tcomms, or some selective
        // modification of the tcomms bus
        if (prob(80) || Dm.truthy(fake))
            priority_announce(alert, "Anomaly Alert");
    }

    // tg: code/modules/events/processor_overload.dm:31
    public override async DmTask<object> start()
    {
        foreach (obj_machinery_telecomms_processor spinny_thing in Dm.each<obj_machinery_telecomms_processor>(GLOB.telecomm_machines))
        {
            if (!prob(10))
            {
                await spinny_thing.emp_act(EMP_HEAVY);
                continue;
            }
            announce_to_ghosts(spinny_thing);
            // Damage the surrounding area to indicate that it popped (explosion_cause = src: the event datum is no atom, so the
            // explosion's logs name no cause)
            await explosion(spinny_thing, light_impact_range: 2);
            // Only a level 1 explosion actually damages the machine
            // at all
            Dm.append(ref SSexplosions.high_mov_atom, spinny_thing);
        }
        return null;
    }
}
