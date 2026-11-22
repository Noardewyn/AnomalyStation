// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/boulder_reactions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111).

/// The boulder machines take in many types of chems, but should only ever eject "waste" chems,
// tg: code/datums/components/plumbing/boulder_reactions.dm:2
public partial class datum_component_plumbing_boulder_reactions : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/boulder_reactions.dm:6
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_bouldertech_refinery)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/boulder_reactions.dm:11
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_bouldertech_refinery the_refinery = (obj_machinery_bouldertech_refinery)parent;
        DmList boosters = the_refinery.get_booster_reagents() as DmList;

        foreach (object booster in Dm.each(boosters))
            await process_request(MACHINE_REAGENT_TRANSFER, booster, dir, TRUE);
    }

    // tg: code/datums/components/plumbing/boulder_reactions.dm:18
    public override object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        if (!Dm.truthy(reagents.total_volume) || Dm.num(amount) <= 0)
            return FALSE;

        obj_machinery_bouldertech_refinery the_refinery = (obj_machinery_bouldertech_refinery)parent;
        if (Dm.truthy(reagent))
        {
            if (!Dm.eq(reagent, the_refinery.waste_chemical)) //Always allow waste chemicals to enter.
                return FALSE;
        }

        return Dm.truthy(reagents.has_reagent(the_refinery.waste_chemical)) ? TRUE : FALSE;
    }

    // tg: code/datums/components/plumbing/boulder_reactions.dm:29
    public override async DmTask transfer_to(datum_component_plumbing target = null, object amount = null, object reagent = null, datum_ductnet net = null, float? round_robin = null)
    {
        obj_machinery_bouldertech_refinery the_refinery = (obj_machinery_bouldertech_refinery)parent;

        await reagents.trans_to(target.recipient_reagents_holder(), Dm.num(amount), target_id: the_refinery.waste_chemical, methods: Dm.truthy(round_robin) ? LINEAR : NONE);
    }
}
