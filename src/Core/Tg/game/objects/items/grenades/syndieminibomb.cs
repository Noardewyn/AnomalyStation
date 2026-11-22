// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/syndieminibomb.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign G2 (D316): the syndicate minibomb, the HE grenade, the frag and FRAG grenades (their shrapnel is the
// grenade's pellet cloud) and the gluon grenade. The explosion ranges are the grenades' ex_ vars, read by the base detonate.

// tg: code/game/objects/items/grenades/syndieminibomb.dm:1
public partial class obj_item_grenade_syndieminibomb : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/syndieminibomb.dm:13
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/syndieminibomb.dm:30
public partial class obj_item_grenade_frag : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/syndieminibomb.dm:45
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        qdel(this);
        return dot;
    }
}

// tg: code/game/objects/items/grenades/syndieminibomb.dm:52
public partial class obj_item_grenade_gluon : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/syndieminibomb.dm:65
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        await update_mob();
        playsound(loc, Dm.resource("sound/effects/empulse.ogg"), 50, TRUE);
        radiation_pulse(get_turf(this) as atom, max_range: rad_range, threshold: rad_threshold, chance: 100); // Activate on the turf, because we'll be gone before SSradiation fires
        foreach (turf_open_floor floor in Dm.each<turf_open_floor>(view(freeze_range, loc)))
        {
            await floor.MakeSlippery(TURF_WET_PERMAFROST, 6 * MINUTES);
            foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(floor))
            {
                await victim.adjust_stamina_loss(stamina_damage);
                victim.adjust_bodytemperature(temp_adjust);
            }
        }
        qdel(this);
        return dot;
    }
}
