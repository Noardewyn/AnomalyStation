// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/mobile_port/variants/assault_pod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N2c, D316: the operatives' assault pod and its targeting device; the medieval pirates' designator with it).
// The var blocks are generated (D111).

// tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:1
public partial class obj_docking_port_mobile_assault_pod : obj_docking_port_mobile
{
    // tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:5
    [DmParams("S")]
    public override object request(obj_docking_port_stationary destination_port = null, area signal_origin = null, object reason = null, object red_alert = null, object set_coefficient = null)
    {
        if (!Dm.@in(z, SSmapping.levels_by_trait(ZTRAIT_STATION))) //No launching pods that have already launched
            return base.request(destination_port, signal_origin, reason, red_alert, set_coefficient);
        return null;
    }

    // tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:10  (tg names the slot's second parameter force: it holds the
    // movement direction, and ..() passes every argument on)
    [DmParams("S1", "force")]
    public override async DmTask<object> initiate_docking(obj_docking_port_stationary new_dock = null, object movement_direction = null, object force = null)
    {
        object dot = await base.initiate_docking(new_dock, movement_direction, force);
        if (!istype(new_dock, typeof(obj_docking_port_stationary_transit)))
            playsound(get_turf(loc as atom), Dm.resource("sound/effects/explosion/explosion1.ogg"), 50, TRUE);
        return dot;
    }
}

// tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:17
public partial class obj_item_assault_pod : obj_item
{
    // tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:34
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object target_area = await tgui_input_list(user, "Area to land", "Landing Zone", GLOB.teleportlocs);
        if (target_area == null)
            return null;
        if (GLOB.teleportlocs[target_area] == null)
            return null;
        area picked_area = GLOB.teleportlocs[target_area] as area;
        if (QDELETED(this))
            return null;

        DmList turfs = get_area_turfs(picked_area) as DmList;
        if (!Dm.truthy(length(turfs)))
            return null;
        turf T = pick(turfs) as turf;
        var landing_zone = Dm.@new<obj_docking_port_stationary>(T);
        landing_zone.shuttle_id = Dm.text("[]([])", lzname, REF(this));
        landing_zone.port_destinations = Dm.text("[]([])", lzname, REF(this));
        landing_zone.name = "Landing Zone";
        landing_zone.dwidth = dwidth;
        landing_zone.dheight = dheight;
        landing_zone.width = width;
        landing_zone.height = height;
        await landing_zone.setDir(lz_dir);

        foreach (obj_machinery_computer_shuttle S in Dm.each<obj_machinery_computer_shuttle>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_shuttle))))
        {
            if (Dm.eq(S.shuttleId, shuttle_id))
                S.possible_destinations = Dm.text("[]", landing_zone.shuttle_id);
        }

        to_chat(user, span_notice(I18n.T("Landing zone set.")));

        qdel(this);
        return null;
    }
}

// tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:66
public partial class obj_item_assault_pod_medieval : obj_item_assault_pod
{
    // tg: code/modules/shuttle/mobile_port/variants/assault_pod.dm:79
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        float counter = Dm.num(length(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_computer_shuttle_pirate))));
        if (counter != 1)
        {
            shuttle_id = Dm.text("[]_[]", shuttle_id, counter);
            lzname = Dm.text("[] []", lzname, counter);
        }
        return dot;
    }
}
