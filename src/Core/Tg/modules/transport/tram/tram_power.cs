// Translated from tgstation (snapshot 1d99bd7f): code/modules/transport/tram/tram_power.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The tram port (2026-11-12): the whole file. The emissive overlays are lighting (DEVIATION DV12).

// tg: code/modules/transport/tram/tram_power.dm:1
public partial class obj_machinery_transport_power_rectifier : obj_machinery_transport
{
    // tg: code/modules/transport/tram/tram_power.dm:18
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        RegisterSignal(SStransport, COMSIG_TRANSPORT_UPDATED, nameof(power_tram));
        find_platform();
    }

    /**
     * The mapper should have placed the rectifier in the area containing the station, the object will search for a landmark within
     * its control area and set its idle position.
     */
    // tg: code/modules/transport/tram/tram_power.dm:27
    public virtual void find_platform()
    {
        area my_area = get_area(this) as area;
        foreach (object candidate in Dm.each(SStransport.nav_beacons[configured_transport_id]))
        {
            if (candidate is not obj_effect_landmark_transport_nav_beacon_tram_platform candidate_platform)
                continue;
            if (get_area(candidate_platform) == my_area)
            {
                connected_platform = candidate_platform;
                RegisterSignal(connected_platform, COMSIG_QDELETING, nameof(on_landmark_qdel));
                log_transport(Dm.text("[]: Power rectifier linked to landmark []", id_tag, connected_platform.name));
                return;
            }
        }
    }

    // tg: code/modules/transport/tram/tram_power.dm:36
    public virtual void power_tram(datum source = null, datum_transport_controller_linear_tram controller = null, object controller_active = null, object controller_status = null, object travel_direction = null, obj_effect_landmark_transport_nav_beacon_tram_platform destination_platform = null)
    {
        if (Dm.truthy(controller_active) && destination_platform == connected_platform)
            update_use_power(ACTIVE_POWER_USE);
        else
            update_use_power(IDLE_POWER_USE);

        update_appearance();
    }

    /**
     * Update the lights based on the rectifier status.
     */
    // tg: code/modules/transport/tram/tram_power.dm:48
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
        {
            Dm.append(ref dot, mutable_appearance(icon, "rec-power-0"));
            return dot;
        }

        Dm.append(ref dot, mutable_appearance(icon, "rec-power-1"));

        bool is_active = Dm.eq(use_power, ACTIVE_POWER_USE);
        Dm.append(ref dot, mutable_appearance(icon, Dm.text("rec-active-[]", is_active ? 1 : 0)));
        return dot;
    }

    /**
     * Clear reference to the connected landmark if it gets destroyed.
     */
    // tg: code/modules/transport/tram/tram_power.dm:66
    public virtual void on_landmark_qdel()
    {
        log_transport(Dm.text("[]: Power rectifier received QDEL from landmark []", id_tag, connected_platform.name));
        connected_platform = null;
    }
}
