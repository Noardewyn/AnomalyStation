// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/trash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 I1d, the map-preview spawners): only the procs that kept Meta's spawners inert, so they spawn their loot and
// delete themselves as tg's do (tg: "If you see this while in a shift, please create a bug report"). The var blocks are generated.
// Gaps G3 (D291, the ROADMAP's follow-up of Meta's spawners): the deluxe garbage, grime and moisture spawners' Initialize
// (trash.dm:63, :225, :248), so they scatter their loot and go; the mice, snails, cockroaches, frogs and axolotls they may lay
// are inert props until the basic mobs (deleting one no longer fails, which had kept these three inert).

// tg: code/game/objects/effects/spawners/random/trash.dm:37
public partial class obj_effect_spawner_random_trash_deluxe_garbage : obj_effect_spawner_random_trash
{
    // tg: code/game/objects/effects/spawners/random/trash.dm:63
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(mapload))
        {
            turf location = get_turf(loc) as turf;
            if (!Dm.eq(location.initial_gas_mix, OPENTURF_DEFAULT_ATMOS) && !Dm.eq(location.initial_gas_mix, OPENTURF_DIRTY_ATMOS))
            {
                Dm.remove(ref loot, typeof(mob_living_basic_mouse));
                Dm.remove(ref loot, typeof(mob_living_basic_snail));
            }
        }
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/random/trash.dm:212
public partial class obj_effect_spawner_random_trash_grime : obj_effect_spawner_random_trash
{
    // tg: code/game/objects/effects/spawners/random/trash.dm:225
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(mapload))
        {
            turf location = get_turf(loc) as turf;
            if (!Dm.eq(location.initial_gas_mix, OPENTURF_DEFAULT_ATMOS) && !Dm.eq(location.initial_gas_mix, OPENTURF_DIRTY_ATMOS))
                Dm.remove(ref loot, Dm.list(typeof(mob_living_basic_cockroach), typeof(mob_living_basic_cockroach_bloodroach)));
        }
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/random/trash.dm:232
public partial class obj_effect_spawner_random_trash_moisture : obj_effect_spawner_random_trash
{
    // tg: code/game/objects/effects/spawners/random/trash.dm:248
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(mapload))
        {
            turf location = get_turf(loc) as turf;
            if (!Dm.eq(location.initial_gas_mix, OPENTURF_DEFAULT_ATMOS) && !Dm.eq(location.initial_gas_mix, OPENTURF_DIRTY_ATMOS))
                Dm.remove(ref loot, Dm.list(typeof(obj_effect_spawner_random_frog), typeof(mob_living_basic_axolotl)));
        }
        return base.Initialize(mapload, __extra);
    }
}

// tg: code/game/objects/effects/spawners/random/trash.dm:265
public partial class obj_effect_spawner_random_trash_graffiti : obj_effect_spawner_random_trash
{
    // tg: code/game/objects/effects/spawners/random/trash.dm:289
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object graffiti_decal = await base.make_item(spawn_loc, type_path_to_make);
        if (graffiti_decal is obj_effect_decal_cleanable_crayon decal)
        {
            // color = random_color && "#[random_short_color()]" || color
            object picked_color = Dm.truthy(random_color) ? Dm.text("#[]", random_short_color()) : random_color;
            color = Dm.truthy(picked_color) ? picked_color : color;
            // icon_state = random_icon && pick(graffiti_icons) || icon_state
            object picked_state = Dm.truthy(random_icon) ? pick(graffiti_icons) : random_icon;
            icon_state = Dm.truthy(picked_state) ? picked_state as string : icon_state;

            await decal.add_atom_colour(color, FIXED_COLOUR_PRIORITY);
            decal.icon_state = icon_state;
        }

        return graffiti_decal;
    }
}

// tg: code/game/objects/effects/spawners/random/trash.dm:383
public partial class obj_effect_spawner_random_trash_crushed_can : obj_effect_spawner_random_trash
{
    // tg: code/game/objects/effects/spawners/random/trash.dm:396
    public override async DmTask<object> make_item(object spawn_loc = null, object type_path_to_make = null)
    {
        object crushed_can = await base.make_item(spawn_loc, type_path_to_make);
        if (crushed_can is obj_item_trash_can can)
            can.icon_state = pick(soda_icons) as string;
        return crushed_can;
    }
}
