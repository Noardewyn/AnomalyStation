// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/plumbing/hydroponics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (a plumbed hydroponics tray takes water and nutrients from ducts).

/// Special demand connector that consumes as normal, but redirects water into the magical water space.
// tg: code/datums/components/plumbing/hydroponics.dm:2
public partial class datum_component_plumbing_hydroponics : datum_component_plumbing
{
    // tg: code/datums/components/plumbing/hydroponics.dm:9
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);

        if (parent is not obj_machinery_hydroponics_constructable hydro_parent)
            return COMPONENT_INCOMPATIBLE;

        water_reagents = Dm.@new<datum_reagents>(hydro_parent.maxwater);
        water_reagents.my_atom = hydro_parent;
        return dot;
    }

    // tg: code/datums/components/plumbing/hydroponics.dm:20
    public override object Destroy(float? force = null)
    {
        qdel(water_reagents);
        water_reagents = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/plumbing/hydroponics.dm:24
    public override object recipient_reagents_holder()
    {
        return Dm.truthy(receive_water) ? water_reagents : reagents;
    }

    // tg: code/datums/components/plumbing/hydroponics.dm:27
    public override async DmTask send_request(object dir = null)
    {
        obj_machinery_hydroponics_constructable hydro_parent = parent as obj_machinery_hydroponics_constructable;

        float initial_nutri_amount = Dm.num(reagents.total_volume);
        if (initial_nutri_amount < Dm.num(reagents.maximum_volume))
        {
            // Well boy howdy, we have no way to tell a supply to not mix the water with everything else,
            // So we'll let it leak in, and move the water over.
            receive_water = FALSE;
            await process_request(dir: dir, round_robin: FALSE);

            // Move the leaked water from nutrients to... water
            float leaking_water_amount = Dm.num(reagents.get_reagent_amount(typeof(datum_reagent_water)));
            if (Dm.truthy(leaking_water_amount))
                await reagents.trans_to(water_reagents, leaking_water_amount, target_id: typeof(datum_reagent_water));
        }

        // We should only take MACHINE_REAGENT_TRANSFER every tick; this is the remaining amount we can take
        float remaining_transfer_amount = Dm.max(MACHINE_REAGENT_TRANSFER - (Dm.num(reagents.total_volume) - initial_nutri_amount), 0);

        // How much extra water we should gather this tick to try to fill the water tray.
        float extra_water_to_gather = Dm.clamp(hydro_parent.maxwater - hydro_parent.waterlevel - Dm.num(water_reagents.total_volume), 0, remaining_transfer_amount);
        if (extra_water_to_gather > 0)
        {
            receive_water = TRUE;
            await process_request(
                amount: extra_water_to_gather,
                reagent: typeof(datum_reagent_water),
                dir: dir);
        }

        // Now transfer all remaining water in that buffer and clear it out.
        float final_water_amount = Dm.num(water_reagents.total_volume);
        if (Dm.truthy(final_water_amount))
        {
            hydro_parent.adjust_waterlevel(Dm.round(final_water_amount));
            // Using a pipe doesn't afford you extra water storage and the baseline behavior for trays is that excess water goes into the shadow realm.
            await water_reagents.clear_reagents();
        }
    }
}
