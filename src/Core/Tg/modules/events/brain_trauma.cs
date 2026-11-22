// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/brain_trauma.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E4 (D327): the whole file: a living crewman with a brain gains a random mild, severe or special trauma. The var blocks are
// generated (D111).

// tg: code/modules/events/brain_trauma.dm:11
public partial class datum_round_event_brain_trauma : datum_round_event
{
    // tg: code/modules/events/brain_trauma.dm:14
    public override async DmTask<object> start()
    {
        foreach (mob_living_carbon_human H in Dm.each<mob_living_carbon_human>(shuffle(GLOB.alive_mob_list)))
        {
            if (H.client == null)
                continue;
            if (H.stat == DEAD) // What are you doing in this list
                continue;
            if (H.get_organ_by_type(typeof(obj_item_organ_brain)) == null) // If only I had a brain
                continue;
            if (!Dm.truthy(Dm.bitand(H.mind?.assigned_role?.job_flags, JOB_CREW_MEMBER))) //please stop giving my centcom admin gimmicks full body paralysis
                continue;
            await traumatize(H);
            announce_to_ghosts(H);
            break;
        }
        return null;
    }

    // tg: code/modules/events/brain_trauma.dm:28
    public virtual async DmTask traumatize(mob_living_carbon_human H = null)
    {
        object resistance = Dm.pick_weighted(
            (50, TRAUMA_RESILIENCE_BASIC),
            (30, TRAUMA_RESILIENCE_SURGERY),
            (15, TRAUMA_RESILIENCE_LOBOTOMY),
            (5, TRAUMA_RESILIENCE_MAGIC));

        var trauma_type = (Type)pick_weight(Dm.list(
            Dm.kv(typeof(datum_brain_trauma_mild), 60), // BRAIN_TRAUMA_MILD
            Dm.kv(typeof(datum_brain_trauma_severe), 30), // BRAIN_TRAUMA_SEVERE
            Dm.kv(typeof(datum_brain_trauma_special), 10) // BRAIN_TRAUMA_SPECIAL
        ));

        await H.gain_trauma_type(trauma_type, resistance);
    }
}
