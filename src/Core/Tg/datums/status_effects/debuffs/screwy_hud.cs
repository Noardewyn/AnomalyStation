// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/screwy_hud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var blocks are generated (D111).

/**
 * Screwy hud status.
 *
 * Applied to carbons, it will make their health bar look like it's incorrect -
 * in crit (SCREWYHUD_CRIT), dead (SCREWYHUD_DEAD), or fully healthy (SCREWYHUD_HEALTHY)
 *
 * Grouped status effect, so multiple sources can add a screwyhud without
 * accidentally removing another source's hud.
 */
// tg: code/datums/status_effects/debuffs/screwy_hud.dm:10
public partial class datum_status_effect_grouped_screwy_hud : datum_status_effect_grouped
{
    // tg: code/datums/status_effects/debuffs/screwy_hud.dm:16
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!iscarbon(owner))
            return FALSE;

        RegisterSignal(owner, COMSIG_CARBON_UPDATING_HEALTH_HUD, nameof(on_health_hud_updated));
        owner.update_health_hud();
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/screwy_hud.dm:23
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_CARBON_UPDATING_HEALTH_HUD);
        owner.update_health_hud();
        return null;
    }

    // tg: code/datums/status_effects/debuffs/screwy_hud.dm:28
    [SignalHandler]
    public virtual object on_health_hud_updated(mob_living_carbon source = null, object shown_health_amount = null)
    {
        // Shouldn't even be running if we're dead, but just in case...
        if (source.stat == DEAD)
            return null;

        // It's entirely possible we have multiple screwy huds on one mob.
        // Defer to priority to determine which to show. If our's is lower, don't show it.
        foreach (datum_status_effect_grouped_screwy_hud other_screwy_hud in Dm.each<datum_status_effect_grouped_screwy_hud>(source.status_effects))
        {
            if (other_screwy_hud.priority > priority)
                return null;
        }

        atom_movable_screen health_screen = source.hud_used?.screen_objects?[HUD_MOB_HEALTH] as atom_movable_screen;
        if (health_screen != null)
            health_screen.icon_state = override_icon as string;
        return COMPONENT_OVERRIDE_HEALTH_HUD;
    }
}
