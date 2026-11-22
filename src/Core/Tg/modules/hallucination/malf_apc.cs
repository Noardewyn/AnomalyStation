// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/malf_apc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var block is generated (D111).

// tg: code/modules/hallucination/malf_apc.dm:1
public partial class datum_hallucination_malf_apc : datum_hallucination
{
    // tg: code/modules/hallucination/malf_apc.dm:13
    public override async DmTask<object> start()
    {
        if (hallucinator.client == null || IS_UNCONSCIOUS(hallucinator))
            return FALSE;

        float num_ais = 0;
        foreach (mob_living_silicon_ai ai in Dm.each<mob_living_silicon_ai>(GLOB.silicon_mobs))
        {
            if (Dm.truthy(is_valid_z_level(get_turf(hallucinator), get_turf(ai))))
                num_ais += 1;
        }

        // less likely to see apcs if there are few to no ais around, but no guarantees.
        if (!prob(clamp(45 * num_ais, 10, 90)))
            return FALSE;

        DmList nearby_apcs = Dm.list();
        foreach (obj_machinery_power_apc apc in Dm.each<obj_machinery_power_apc>(view(hallucinator)))
        {
            if (!Dm.truthy(apc.is_operational) || apc.area == null || !Dm.truthy(apc.area.requires_power) || !Dm.eq(apc.opened, APC_COVER_CLOSED))
                continue;
            Dm.append(nearby_apcs, apc);
        }

        if (length(nearby_apcs) == 0)
            return FALSE;

        obj_machinery_power_apc selected_apc = pick(nearby_apcs) as obj_machinery_power_apc;
        hacked_image = image(apc_icon, selected_apc, apc_icon_state, FLOAT_LAYER);
        Dm.combine(hallucinator.client.images, hacked_image);
        QDEL_IN_HALLUCINATION(this, 1 * SECONDS, "code/modules/hallucination/malf_apc.dm", 43);
        return TRUE;
    }

    // tg: code/modules/hallucination/malf_apc.dm:46
    public override object Destroy(float? force = null)
    {
        if (hallucinator.client != null)
            Dm.remove(hallucinator.client.images, hacked_image);
        hacked_image = null;
        return base.Destroy(force);
    }
}
