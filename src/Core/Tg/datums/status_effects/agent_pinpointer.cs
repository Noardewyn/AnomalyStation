// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/agent_pinpointer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/status_effects/agent_pinpointer.dm:11
public partial class datum_status_effect_agent_pinpointer : datum_status_effect
{
    // tg: code/datums/status_effects/agent_pinpointer.dm:27
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner == null)
        {
            qdel(this);
            return null;
        }
        scan_for_target();
        await point_to_target();
        return null;
    }

    ///Show the distance and direction of a scanned target
    // tg: code/datums/status_effects/agent_pinpointer.dm:35
    public virtual async DmTask point_to_target()
    {
        if (scan_target == null)
        {
            linked_alert.icon_state = "pinonnull";
            return;
        }

        turf here = get_turf(owner);
        turf there = get_turf(scan_target);

        if (there == null)
        {
            scan_target = null;
            return;
        }
        if (!Dm.eq(here.z, there.z))
        {
            linked_alert.icon_state = "pinonnull";
            return;
        }
        if (Dm.num(get_dist_euclidean(here, there)) <= minimum_range + Dm.num(rand(0, range_fuzz_factor)))
        {
            linked_alert.icon_state = "pinondirect";
            return;
        }
        await linked_alert.setDir(get_dir(here, there));

        float dist = Dm.num(get_dist(here, there));
        if (dist >= 1 && dist <= range_mid)
            linked_alert.icon_state = "pinonclose";
        else if (dist > range_mid && dist <= range_far)
            linked_alert.icon_state = "pinonmedium";
        else if (dist > range_far)
            linked_alert.icon_state = "pinonfar";
    }

    ///Attempting to locate a nearby target to scan and point towards.
    // tg: code/datums/status_effects/agent_pinpointer.dm:63
    public virtual void scan_for_target()
    {
        scan_target = null;
        if (owner == null || owner.mind == null)
            return;
        foreach (datum_objective_assassinate objective_datums in Dm.each<datum_objective_assassinate>(owner.mind.get_all_objectives()))
        {
            if (objective_datums.target == null || objective_datums.target.current == null || Dm.eq(objective_datums.target.current.stat, DEAD))
                continue;
            mob tracked_target = objective_datums.target.current;
            //JUUUST in case.
            if (tracked_target == null)
                continue;

            //Catch the first one we find, then stop. We want to point to the most recent one we've got.
            scan_target = tracked_target;
            break;
        }
    }
}
