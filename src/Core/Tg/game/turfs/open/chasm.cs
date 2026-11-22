// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/chasm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D304).

// Base chasm, defaults to oblivion but can be overridden
// tg: code/game/turfs/open/chasm.dm:2
public partial class turf_open_chasm : turf_open
{
    // tg: code/game/turfs/open/chasm.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_components(mapload);
        return dot;
    }

    /// Lets people walk into chasms.
    // tg: code/game/turfs/open/chasm.dm:22
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        await base.CanAllowThrough(mover, border_dir);
        return TRUE;
    }

    // tg: code/game/turfs/open/chasm.dm:26
    public virtual void set_target(turf target = null)
    {
        var chasm_component = (datum_component_chasm)GetComponent(typeof(datum_component_chasm));
        chasm_component.target_turf = target;
    }

    // tg: code/game/turfs/open/chasm.dm:30
    public virtual async DmTask drop(atom_movable AM = null)
    {
        var chasm_component = (datum_component_chasm)GetComponent(typeof(datum_component_chasm));
        await chasm_component.drop(AM);
    }

    // tg: code/game/turfs/open/chasm.dm:34
    public override async DmTask MakeSlippery(float? wet_setting = null, float? min_wet_time = null, float? wet_time_to_add = null, float? max_wet_time = null, float? permanent = null, float? should_display_overlay = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/game/turfs/open/chasm.dm:37
    public override void MakeDry(float? wet_setting = null, float? immediate = null, float? amount = null)
    {
    }

    // tg: code/game/turfs/open/chasm.dm:40
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF) && Dm.eq(the_rcd.rcd_design_path, typeof(turf_open_floor_plating_rcd)))
            return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 3));
        return FALSE;
    }

    // tg: code/game/turfs/open/chasm.dm:45
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(rcd_data[RCD_DESIGN_MODE], RCD_TURF) && Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(turf_open_floor_plating_rcd)))
        {
            await place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/chasm.dm:53
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi"); // /turf/open/misc/asteroid/basalt::icon
        underlay_appearance.icon_state = "basalt"; // /turf/open/misc/asteroid/basalt::icon_state
        return TRUE;
    }

    // tg: code/game/turfs/open/chasm.dm:58
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (istype(tool, typeof(obj_item_stack_rods)))
        {
            await build_with_rods((obj_item_stack_rods)tool, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (ismetaltile(tool))
        {
            await build_with_floor_tiles((obj_item_stack_tile_iron)tool, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    /// Handles adding the chasm component to the turf (So stuff falls into it!)
    // tg: code/game/turfs/open/chasm.dm:73
    public virtual void apply_components(object mapload = null)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_chasm), GET_TURF_BELOW(this), mapload));
    }

    // tg: code/game/turfs/open/chasm.dm:76
    public override object can_cross_safely(atom_movable crossing = null)
    {
        return HAS_TRAIT(this, TRAIT_CHASM_STOPPED) || HAS_TRAIT(crossing, TRAIT_MOVE_FLYING) ? TRUE : FALSE;
    }
}

// tg: code/game/turfs/open/chasm.dm:89
public partial class turf_open_chasm_icemoon : turf_open_chasm
{
    // tg: code/game/turfs/open/chasm.dm:100
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi"); // /turf/open/misc/asteroid/snow/icemoon::icon
        underlay_appearance.icon_state = "snow"; // /turf/open/misc/asteroid/snow/icemoon::icon_state
        return TRUE;
    }
}

// tg: code/game/turfs/open/chasm.dm:106
public partial class turf_open_chasm_jungle : turf_open_chasm
{
    // tg: code/game/turfs/open/chasm.dm:113
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi"); // /turf/open/misc/dirt::icon
        underlay_appearance.icon_state = "dirt"; // /turf/open/misc/dirt::icon_state
        return TRUE;
    }
}

// Chasm that doesn't do any z-level nonsense and just kills/stores whoever steps into it.
// tg: code/game/turfs/open/chasm.dm:119
public partial class turf_open_chasm_true : turf_open_chasm
{
    // tg: code/game/turfs/open/chasm.dm:123
    public override void apply_components(object mapload = null)
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_chasm), null, mapload)); //Don't pass anything for below_turf.
    }
}

// tg: code/game/turfs/open/chasm.dm:126
public partial class turf_open_chasm_true_no_smooth : turf_open_chasm_true
{
    // tg: code/game/turfs/open/chasm.dm:129
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/turfs/open/chasm.dm:132
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/game/turfs/open/chasm.dm:135
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_stack_rods)))
            return ITEM_INTERACT_BLOCKING;

        if (ismetaltile(tool))
            return ITEM_INTERACT_BLOCKING;

        return await base.item_interaction(user, tool, modifiers, __extra);
    }
}

// Chasm produced by the Pride ruin. Leads to a random space Z-level at the same x/y coordinate.
// tg: code/game/turfs/open/chasm.dm:145
public partial class turf_open_chasm_pride : turf_open_chasm
{
    // tg: code/game/turfs/open/chasm.dm:152
    public override void apply_components(object mapload = null)
    {
        DmList levels = SSmapping.levels_by_trait(ZTRAIT_SPACE_RUINS) as DmList;
        turf space_turf = null;
        if (Dm.truthy(length(levels)))
            space_turf = locate(x, y, Dm.num(pick(levels)));

        Waiting.NoSleep(AddComponent(typeof(datum_component_chasm), space_turf, mapload));
    }
}
