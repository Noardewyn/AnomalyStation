// Translated from tgstation (snapshot 1d99bd7f): code/modules/procedural_mapping/mapGeneratorModules/helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327, with Mafia's mass delete): the helper modules: the repressurizer, the mass deletes and the border.
// The generators and the var blocks are generated (D111).

// Helper to repressurize the area in case it was run in space
// tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:5
public partial class datum_map_generator_module_bottom_layer_repressurize : datum_map_generator_module_bottom_layer
{
    // tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:9
    public override async DmTask generate()
    {
        await DmTask.CompletedTask;
        if (mother == null)
            return;
        DmList map = mother.map;
        foreach (turf T in Dm.each<turf>(map))
            SSair.remove_from_active(T);
        foreach (turf_open T in Dm.each<turf_open>(map))
        {
            if (T.air != null)
                T.air = T.create_gas_mixture() as datum_gas_mixture;
            SSair.add_to_active(T, TRUE);
        }
    }
}

// tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:20
public partial class datum_map_generator_module_bottom_layer_massdelete : datum_map_generator_module_bottom_layer
{
    // tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:26
    public override async DmTask generate()
    {
        if (mother == null)
            return;
        foreach (object V in Dm.each(mother.map))
        {
            turf T = V as turf;
            await T.empty(Dm.truthy(deleteturfs) ? null : T.type, null, ignore_typecache, CHANGETURF_FORCEOP);
        }
    }
}

// tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:33
public partial class datum_map_generator_module_bottom_layer_massdelete_no_delete_mobs : datum_map_generator_module_bottom_layer_massdelete
{
    // tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:33  /New 
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ignore_typecache = GLOB.typecache_mob;
    }
}

// tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:40
public partial class datum_map_generator_module_bottom_layer_massdelete_regeneration_delete : datum_map_generator_module_bottom_layer_massdelete
{
    // tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:43
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        ignore_typecache = GLOB.typecache_mob;
    }
}

//Only places atoms/turfs on area borders
// tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:48
public partial class datum_map_generator_module_border : datum_map_generator_module
{
    // tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:51
    public override async DmTask generate()
    {
        if (mother == null)
            return;
        DmList map = mother.map;
        foreach (turf T in Dm.each<turf>(map))
        {
            if (Dm.truthy(is_border(T)))
                await place(T);
        }
    }

    // tg: code/modules/procedural_mapping/mapGeneratorModules/helpers.dm:59
    public virtual object is_border(turf T = null)
    {
        foreach (object direction in Dm.each(Dm.list(SOUTH, EAST, WEST, NORTH)))
        {
            if (Dm.@in(get_step(T, Dm.num(direction)), mother.map))
                continue;
            return 1;
        }
        return 0;
    }
}
