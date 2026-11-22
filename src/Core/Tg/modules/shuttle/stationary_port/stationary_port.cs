// Translated from tgstation (snapshot 1d99bd7f): code/modules/shuttle/stationary_port/stationary_port.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G3 (D272): the whole file but get_save_vars (the map export) and highlight (#ifdef TESTING).

// tg: code/modules/shuttle/stationary_port/stationary_port.dm:1
public partial class obj_docking_port_stationary : obj_docking_port
{
    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register();
        if (area_type == null)
        {
            var place = get_area(this) as area;
            area_type = place?.GetType(); // We might be created in nullspace
        }

        if (Dm.truthy(mapload))
        {
            foreach (turf T in Dm.each<turf>(return_turfs()))
                T.turf_flags = Dm.bitor(T.turf_flags, NO_RUINS);
        }

        initial_x = x;
        initial_y = y;
        initial_z = z;

        if (Dm.truthy(SSshuttle.initialized))
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }

    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:41
    public override void LateInitialize(params object[] __extra)
    {
        Waiting.Detach(setup_shuttles_async()); // INVOKE_ASYNC
    }

    // INVOKE_ASYNC(SSshuttle, setup_shuttles, list(src)). A dock late-initializes in a template's load, whose tick tg has spent, so tg's
    // call sleeps at the first CHECK_TICK of the turf reservation (space_reservation.dm:163) and the loading InitializeAtoms finishes
    // its late loaders before the shuttle's own load starts; the port's tick is never spent (DV3), so the call yields here first (else
    // the shuttle template's InitializeAtoms runs the dock's LateInitialize again: the syndicate base's assault pod recursed).
    private async DmTask setup_shuttles_async()
    {
        await stoplag();
        await SSshuttle.setup_shuttles(Dm.list(this));
    }

    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:48
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(force))
            unregister();
        return base.Destroy(force);
    }

    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:53
    public override object register(object replace = null, object custom = null)
    {
        replace ??= FALSE;
        object dot = base.register(replace, custom);
        if (!Dm.truthy(shuttle_id))
            shuttle_id = "dock";
        else
            port_destinations = shuttle_id;

        if (!Dm.truthy(name))
            name = "dock";

        object counter = SSshuttle.assoc_stationary[shuttle_id];
        if (!Dm.truthy(replace) || !Dm.truthy(counter))
        {
            if (Dm.truthy(counter))
            {
                float next = Dm.num(counter) + 1;
                SSshuttle.assoc_stationary[shuttle_id] = next;
                shuttle_id = Dm.text("[]_[]", shuttle_id, next);
                name = Dm.text("[] []", name, next);
            }
            else
            {
                SSshuttle.assoc_stationary[shuttle_id] = 1f;
            }
        }

        if (!Dm.truthy(port_destinations))
            port_destinations = shuttle_id;

        Dm.append(ref SSshuttle.stationary_docking_ports, this);
        return dot;
    }

    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:79
    public override object unregister()
    {
        object dot = base.unregister();
        Dm.remove(ref SSshuttle.stationary_docking_ports, this);
        return dot;
    }

    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:83
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (area_type != null) // We already have one
            return dot;
        var newarea = get_area(this) as area;
        area_type = newarea?.GetType();
        return dot;
    }

    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:90
    public virtual async DmTask load_roundstart()
    {
        if (Dm.truthy(json_key))
        {
            object sid = SSmapping.current_map.shuttles[json_key];
            shuttle_template_id = SSmapping.shuttle_templates[sid];
            if (!Dm.truthy(shuttle_template_id))
                throw Dm.CRASH(Dm.text("json_key:[] value \\[[]\\] resulted in a null shuttle template for []", json_key, sid, this), "code/modules/shuttle/stationary_port/stationary_port.dm", 95);
        }
        else if (roundstart_template != null) // passed a PATH
        {
            object sid = Dm.text("[]_[]",
                Dm.initial(roundstart_template, (datum_map_template_shuttle p) => p.port_id),
                Dm.initial(roundstart_template, (datum_map_template_shuttle p) => p.suffix));

            shuttle_template_id = SSmapping.shuttle_templates[sid];
            if (!Dm.truthy(shuttle_template_id))
                throw Dm.CRASH(Dm.text("Invalid path ([]/[]) passed to docking port.", sid, shuttle_template_id), "code/modules/shuttle/stationary_port/stationary_port.dm", 102);
        }

        if (Dm.truthy(shuttle_template_id))
            await SSshuttle.action_load((datum_map_template_shuttle)shuttle_template_id, this);
    }

    //returns first-found touching shuttleport
    // tg: code/modules/shuttle/stationary_port/stationary_port.dm:108
    public override object get_docked()
    {
        return locate<obj_docking_port_mobile>(loc);
    }
}
