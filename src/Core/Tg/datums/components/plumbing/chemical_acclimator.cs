// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/chemical_acclimator.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/datums/components/plumbing/chemical_acclimator.dm:1
public partial class datum_component_plumbing_acclimator : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/chemical_acclimator.dm:5
    [DmParams("ducting_layer")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!istype(parent, typeof(obj_machinery_plumbing_acclimator)))
            return COMPONENT_INCOMPATIBLE;
        return await base.Initialize(__extra);
    }

    // tg: code/datums/components/plumbing/chemical_acclimator.dm:10
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_plumbing_acclimator myacclimator = (obj_machinery_plumbing_acclimator)parent;
        if (Dm.eq(myacclimator.acclimate_state, AC_FILLING))
        {
            if (reagents.total_volume < myacclimator.max_volume)
            {
                await base.send_request(dir);
                if (!Dm.truthy(reagents.holder_full()))
                    return;
            }
            Dm.mask(ref reagents.flags, Dm.bitnot(NO_REACT));
            myacclimator.acclimate_state = reagents.chem_temp > Dm.num(myacclimator.target_temperature) ? AC_COOLING : AC_HEATING;
            myacclimator.update_appearance(UPDATE_ICON_STATE);
        }
    }

    // tg: code/datums/components/plumbing/chemical_acclimator.dm:21
    public override object can_give(object amount = null, object reagent = null, datum_ductnet net = null)
    {
        obj_machinery_plumbing_acclimator myacclimator = (obj_machinery_plumbing_acclimator)parent;

        return Dm.eq(myacclimator.acclimate_state, AC_EMPTYING) && !Dm.truthy(reagents.is_reacting) && Dm.truthy(base.can_give(amount, reagent, net)) ? TRUE : FALSE;
    }
}
