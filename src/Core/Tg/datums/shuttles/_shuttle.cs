// Translated from tgstation (snapshot 1d99bd7f): code/datums/shuttles/_shuttle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G2 (D272): the whole file. The shuttle templates of the round-start docks (their vars are generated).

// tg: code/datums/shuttles/_shuttle.dm:1
[DmPath("/datum/map_template/shuttle")]
public partial class datum_map_template_shuttle : datum_map_template
{
    // tg: code/datums/shuttles/_shuttle.dm:34
    public virtual object prerequisites_met()
    {
        return TRUE;
    }

    // tg: code/datums/shuttles/_shuttle.dm:37
    public override async DmTask New(params object[] __extra)
    {
        // DEVIATION(DV506): the shuttle this template is being made for from its map's file (SSmapping.preloadShuttleTemplates): its port id and suffix,
        // and the data of the json beside the file. None: a template of a type of its own, as tg has them.
        if (MapSeam.ShuttleBeingMade(World.Current) is { } made)
        {
            (port_id, suffix) = (made.PortId, made.Suffix);
            prefix = "_maps/shuttles/" + made.PortId + "/"; // (where the maps folder's shuttles/<port id> is mounted: tg's types name their family's folder)
            name = made.Data.ShuttleName ?? made.PortId + "_" + made.Suffix;
            description = made.Data.ShuttleDescription ?? description;
            if (made.Data.ShuttleCost is { } cost)
                credit_cost = cost;
            else
                who_can_purchase = null; // (not for sale)
        }
        shuttle_id = Dm.text("[]_[]", port_id, suffix);
        mappath = Dm.text("[][].dmm", prefix, shuttle_id);
        await base.New(__extra);
    }

    // tg: code/datums/shuttles/_shuttle.dm:42
    public override async DmTask<object> preload_size(object path = null, object cache = null)
    {
        object dot = await base.preload_size(path, TRUE); // Done this way because we still want to know if someone actually wanted to cache the map
        if (cached_map == null)
            return dot;

        var offset = (DmList)discover_offset(typeof(obj_docking_port_mobile));

        port_x_offset = offset[1];
        port_y_offset = offset[2];

        if (!Dm.truthy(cache))
            cached_map = null;
        return dot;
    }

    // tg: code/datums/shuttles/_shuttle.dm:55
    [DmParams("T", "centered", "register")]
    public override async DmTask<object> load(turf T = null, object centered = null, params object[] __extra)
    {
        object register = __extra.Length > 0 ? __extra[0] : TRUE;
        object dot = await base.load(T, centered);
        if (!Dm.truthy(dot))
            return dot;

        var bounds = (DmList)dot;
        DmList turfs = block(Dm.num(bounds[MAP_MINX]), Dm.num(bounds[MAP_MINY]), Dm.num(bounds[MAP_MINZ]),
            Dm.num(bounds[MAP_MAXX]), Dm.num(bounds[MAP_MAXY]), Dm.num(bounds[MAP_MAXZ]));

        await dispatch(turfs, register);
        return dot;
    }

    // tg: code/datums/shuttles/_shuttle.dm:65
    public virtual async DmTask dispatch(DmList turfs = null, object register = null)
    {
        register ??= TRUE;
        while (true)
        {
            bool found = false;
            foreach (turf current_turf in Dm.each<turf>(turfs))
            {
                if (Dm.truthy(is_type_on_turf(current_turf, typeof(obj_modular_map_root))))
                    found = true;
            }
            if (found)
                await sleep(5); // 5 DECISECONDS
            else
                break;
        }

        for (float i = 1; i <= turfs.len; i++)
        {
            var place = (turf)turf.dm_identity((turf)turfs[i]); // TURF-IDENTITY: the block's turfs name their locations
            if (isspaceturf(place)) // This assumes all shuttles are loaded in a single spot then moved to their real destination.
                continue;

            if (Dm.num(place.count_baseturfs()) < 2) // Some snowflake shuttle shit
                continue;

            await place.insert_baseturf(3, typeof(turf_baseturf_skipover_shuttle));

            foreach (obj_docking_port_mobile port in Dm.each<obj_docking_port_mobile>(place))
            {
                port.calculate_docking_port_information(this);
                // initTemplateBounds explicitly ignores the shuttle's docking port, to ensure that it calculates the bounds of the shuttle correctly
                // so we need to manually initialize it here
                SSatoms.InitializeAtoms(Dm.list(port));
                if (Dm.truthy(register))
                    port.register();
            }
        }
    }

    //Whatever special stuff you want
    // tg: code/datums/shuttles/_shuttle.dm:95
    [DmParams("M")]
    public override async DmTask post_load(params object[] __extra)
    {
        var M = Dm.arg<obj_docking_port_mobile>(__extra, 0);
        if (movement_force != null)
            M.movement_force = movement_force.Copy();
        await M.linkup();
    }
}
