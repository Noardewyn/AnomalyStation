// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/modular_map_loader/modular_map_loader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, for the virtual domains' safehouses (bitrunning/objects/landmarks.dm's
// /obj/modular_map_root/safehouse). The root reads its .toml through the world's file seam (rust-g's toml reader,
// rust_g.toml.builtin.cs; TgStrings mounts strings/modular_maps/), and its module map is a converted .as3dmap mounted at tg's path
// (MapSeam: _maps/safehouses/). The var blocks are generated (D111).

// tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:1
public partial class obj_modular_map_root : obj
{
    // The New() that INITIALIZE_IMMEDIATE(/obj/modular_map_root) defines (__DEFINES/subsystems.dm:96; inline, rules 11.1 (c), (d))
    // tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:14
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            DmList args = Dm.args(Dm.forward(__extra, loc));
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.Detach(load_map());
        return dot;
    }

    /// Randonly selects a map file from the TOML config specified in config_file, loads it, then deletes itself.
    // tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:21
    public virtual async DmTask load_map()
    {
        turf spawn_area = get_turf(this);

        var map = Dm.@new<datum_map_template_map_module>();

        if (!Dm.truthy(config_file))
            return;

        if (!Dm.truthy(key))
            return;

        DmList config = rustg_read_toml_file(config_file) as DmList;

        object mapfile = Dm.add(config["directory"], pick(((((config["rooms"] as DmList)[key]) as DmList)["modules"]) as DmList));

        await map.load(spawn_area, FALSE, mapfile);

        qdel(this, force: TRUE);
    }
}

// tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:41
public partial class datum_map_template_map_module : datum_map_template
{
    // tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:46
    [DmParams("mapfile")]
    public override async DmTask<object> load(turf T = null, object centered = null, params object[] __extra)
    {
        object mapfile = Dm.arg(__extra, 0);
        centered ??= FALSE;

        if (!Dm.truthy(mapfile))
            return null;

        mappath = mapfile;

        await preload_size(mappath); // We need to run this here as the map path has been null until now

        T = locate(T.x - x_offset, T.y - y_offset, T.z);
        return await base.load(T, centered);
    }

    // tg: code/modules/mapping/modular_map_loader/modular_map_loader.dm:58
    public override async DmTask<object> preload_size(object path = null, object cache = null)
    {
        object dot = await base.preload_size(path, TRUE); // Done this way because we still want to know if someone actualy wanted to cache the map
        if (cached_map == null)
            return dot;

        DmList offset = discover_offset(typeof(obj_modular_map_connector)) as DmList;

        x_offset = Dm.num(offset[1]) - 1;
        y_offset = Dm.num(offset[2]) - 1;

        if (!Dm.truthy(cache))
            cached_map = null;
        return dot;
    }
}
