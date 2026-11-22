// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/space/space.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the var block of /turf/open/space (with the space_gas static, M2 integration) and the type
// /turf/open/space/basic (tg's /world/turf, code/world.dm) with its var override (W4.0 contracts, rules 10.2) and
// /turf/open/space/basic/New (:79, package B, wave 4) translated, and (M4b slice 1, the air that moves) the atmos overrides
// TakeTemperature, RemoveLattice, AfterChange, Assimilate_Air and remove_air (:88-102), and (M4c slice 8) attack_paw, handle_slip,
// MakeSlippery, can_have_cabling, get_smooth_underlay_icon and can_cross_safely. M7 lumcount: the starlight (:1-43, Destroy,
// update_starlight, enable_starlight): a lit space turf is a light source of the server's lumcount, and its light_on is the
// starlit flag of the client's cell (D116); set_starlight's starlight objects and COMSIG_STARLIGHT_COLOR_CHANGED are presentation;
// (M5 slice 4) building on space and its turf changes (item_interaction with rods and tiles, ChangeTurf's nearstation area,
// attempt_lattice_replacement); E13: rcd_vals and rcd_act (the RCD); not here: replace_floor (its callers are
// M10+, as /turf/open's); acid_act (silicons SI1c) and singularity_act; /turf/open/space/openspace is at the end of this file (DV436). The [DmPath] of /turf/open/space is on
// controllers/subsystem/air.cs, the earliest .dme file that declares it (rules 2.3).

// tg: code/game/turfs/open/space/space.dm:1-43  the starlight globals and procs (rules 7.8, 10.6)
public partial class datum_controller_global_vars : datum_controller
{
    ///The base color of light space emits
    // tg: code/game/turfs/open/space/space.dm:2  GLOBAL_VAR_INIT(base_starlight_color, default_starlight_color())
    public object base_starlight_color;

    // tg: code/game/turfs/open/space/space.dm:2
    public virtual void InitGlobalbase_starlight_color()
    {
        base_starlight_color = default_starlight_color();
        Dm.append(ref gvars_datum_init_order, nameof(base_starlight_color));
    }

    ///The color of light space is currently emitting
    // tg: code/game/turfs/open/space/space.dm:4  GLOBAL_VAR_INIT(starlight_color, default_starlight_color())
    public object starlight_color;

    // tg: code/game/turfs/open/space/space.dm:4
    public virtual void InitGlobalstarlight_color()
    {
        starlight_color = default_starlight_color();
        Dm.append(ref gvars_datum_init_order, nameof(starlight_color));
    }

    ///The range of the light space is displaying
    // tg: code/game/turfs/open/space/space.dm:10  GLOBAL_VAR_INIT(starlight_range, default_starlight_range())
    public object starlight_range;

    // tg: code/game/turfs/open/space/space.dm:10
    public virtual void InitGlobalstarlight_range()
    {
        starlight_range = default_starlight_range();
        Dm.append(ref gvars_datum_init_order, nameof(starlight_range));
    }

    ///The power of the light space is throwin out
    // tg: code/game/turfs/open/space/space.dm:16  GLOBAL_VAR_INIT(starlight_power, default_starlight_power())
    public object starlight_power;

    // tg: code/game/turfs/open/space/space.dm:16
    public virtual void InitGlobalstarlight_power()
    {
        starlight_power = default_starlight_power();
        Dm.append(ref gvars_datum_init_order, nameof(starlight_power));
    }

    // tg: code/game/turfs/open/space/space.dm:43  GLOBAL_LIST_EMPTY(starlight)
    public DmList starlight;

    // tg: code/game/turfs/open/space/space.dm:43
    public virtual void InitGlobalstarlight()
    {
        starlight = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(starlight));
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/turfs/open/space/space.dm:5
    public static object default_starlight_color()
    {
        return Dm.initial(typeof(turf_open_space), (turf_open_space p) => p.light_color);
    }

    // tg: code/game/turfs/open/space/space.dm:11
    public static object default_starlight_range()
    {
        return Dm.initial(typeof(turf_open_space), (turf_open_space p) => p.light_range);
    }

    // tg: code/game/turfs/open/space/space.dm:17
    public static object default_starlight_power()
    {
        return Dm.initial(typeof(turf_open_space), (turf_open_space p) => p.light_power);
    }

    // tg: code/game/turfs/open/space/space.dm:21
    public static async DmTask set_base_starlight(object star_color = null, object range = null, object power = null)
    {
        GLOB.base_starlight_color = star_color;
        await set_starlight(star_color, range, power);
    }

    // tg: code/game/turfs/open/space/space.dm:25
    public static async DmTask set_starlight(object star_color = null, object range = null, object power = null)
    {
        if (star_color == null)
            star_color = GLOB.starlight_color;
        object old_star_color = GLOB.starlight_color;
        GLOB.starlight_color = star_color;
        // set light color on all lit turfs
        foreach (turf_open_space spess in Dm.each_cast<turf_open_space>(GLOB.starlight))
            await spess.set_light(l_range: range, l_power: power, l_color: star_color);

        if (Dm.eq(star_color, old_star_color))
            return;

        // tg: :38-42  the starlight objects' colour (GLOB.starlight_objects, the 2D starlight appearances) and
        // COMSIG_STARLIGHT_COLOR_CHANGED, which only presentation listens to (the areas' base light, material glow): the client
        // draws the starlight from the cells (D116)
    }
}

// tg: code/game/turfs/open/space/space.dm:45
public partial class turf_open_space : turf_open
{
    // tg: code/game/turfs/open/space/space.dm:46-74
    public turf_open_space()
    {
        icon = Dm.resource("icons/turf/space.dmi");
        icon_state = "space"; // MAP_SWITCH("space", "space_map"): the compile-time side (CBT defined, rules 1.7)
        name = Dm.text("\\proper space");
        overfloor_placed = FALSE;
        underfloor_accessibility = UNDERFLOOR_INTERACTABLE;
        turf_flags = NO_RUST;
        rust_resistance = RUST_RESISTANCE_ABSOLUTE;

        temperature = TCMB;
        thermal_conductivity = OPEN_HEAT_TRANSFER_COEFFICIENT;
        heat_capacity = 700000;

        // We do NOT want atmos adjacent turfs
        init_air = FALSE;
        run_later = TRUE;
        plane = PLANE_SPACE;
        layer = SPACE_LAYER;
        light_power = 1;
        light_range = 2;
        light_color = COLOR_STARLIGHT;
        light_height = LIGHTING_HEIGHT_SPACE;
        light_on = FALSE;
        space_lit = TRUE;
        bullet_bounce_sound = null;
        vis_flags = VIS_INHERIT_ID; //when this be added to vis_contents of something it be associated with something on clicking, important for visualisation of turf in openspace and interraction with openspace that show you turf.

        force_no_gravity = TRUE;
        skip_minimap_rendering = TRUE;
    }

    // tg: code/game/turfs/open/space/space.dm:58  var/static/datum/gas_mixture/immutable/space/space_gas = new (an object-creating
    // static initializer: assigned in TgState.InitStatics, rules 10.6)
    public static ref datum_gas_mixture_immutable_space space_gas => ref TgState.Current.turf_open_space__space_gas;
}

public sealed partial class TgState
{
    // tg: code/game/turfs/open/space/space.dm:58  /turf/open/space/var/static/datum/gas_mixture/immutable/space/space_gas
    internal datum_gas_mixture_immutable_space turf_open_space__space_gas;
}

// tg: code/game/turfs/open/space/space.dm:76
[DmPath("/turf/open/space/basic")]
public partial class turf_open_space_basic : turf_open_space
{
    // tg: code/game/turfs/open/space/space.dm:77
    public turf_open_space_basic()
    {
        icon_state = "space"; // MAP_SWITCH("space", "space_basic_map"): the compile-time side (CBT defined, rules 1.7)
    }

    // tg: code/game/turfs/open/space/space.dm:79
    public override async DmTask New(params object[] __extra) //Do not convert to Initialize
    {
        // SHOULD_CALL_PARENT(FALSE) is dropped (rules 7.9)
        //This is used to optimize the map loader
        return;
    }
}

// tg: code/game/turfs/open/space/space.dm:88  (interleaved types, rules 1.9)
public partial class turf_open_space : turf_open
{
    // tg: code/game/turfs/open/space/space.dm:84
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.starlight, this);
        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/space/space.dm:88
    public override void TakeTemperature(object temp = null)
    {
    }

    // tg: code/game/turfs/open/space/space.dm:90
    public override void RemoveLattice()
    {
        return;
    }

    // tg: code/game/turfs/open/space/space.dm:93
    public override void AfterChange(object flags = null, object oldType = null)
    {
        base.AfterChange(flags, oldType);
        atmos_overlay_types = null;
    }

    // tg: code/game/turfs/open/space/space.dm:97
    public override void Assimilate_Air()
    {
        return;
    }

    //IT SHOULD RETURN NULL YOU MONKEY, WHY IN TARNATION WHAT THE FUCKING FUCK
    // tg: code/game/turfs/open/space/space.dm:101
    public override object remove_air(object amount = null)
    {
        return null;
    }

    /// Updates starlight. Called when we're unsure of a turf's starlight state
    /// Returns TRUE if we succeed, FALSE otherwise
    // tg: code/game/turfs/open/space/space.dm:106
    public virtual async DmTask<object> update_starlight()
    {
        foreach (turf t in Dm.each<turf>(RANGE_TURFS(1, this))) //RANGE_TURFS is in code\__HELPERS\game.dm
        {
            // I've got a lot of cordons near spaceturfs, be good kids
            if (isspaceturf(t) || t is turf_cordon)
                //let's NOT update this that much pls
                continue;
            await enable_starlight();
            return TRUE;
        }
        Dm.remove(ref GLOB.starlight, this);
        await set_light(l_on: FALSE);
        return FALSE;
    }

    /// Turns on the stars, if they aren't already
    // tg: code/game/turfs/open/space/space.dm:119
    public virtual async DmTask enable_starlight()
    {
        if (!Dm.truthy(light_on))
        {
            await set_light(l_on: TRUE, l_range: GLOB.starlight_range, l_power: GLOB.starlight_power, l_color: GLOB.starlight_color);
            Dm.append(ref GLOB.starlight, this);
        }
    }

    // tg: code/game/turfs/open/space/space.dm:124  (M4c slice 8, with the space turf's small overrides below)
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/turfs/open/space/space.dm:127
    public override async DmTask<object> handle_slip(mob_living_carbon slipped_carbon = null, object knockdown_amount = null, atom slipping_object = null, object lube = null, object paralyze = null, object daze = null, object force_drop = null)
    {
        return FALSE;
    }

    // tg: code/game/turfs/open/space/space.dm:130  (M5 slice 4: rods and floor tiles on space)
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (!Dm.truthy(CanBuildHere()))
            return dot;

        if (istype(tool, typeof(obj_item_stack_rods)))
        {
            await build_with_rods(tool as obj_item_stack_rods, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (ismetaltile(tool))
        {
            await build_with_floor_tiles(tool as obj_item_stack_tile_iron, user);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/turfs/open/space/space.dm:146
    public override async DmTask MakeSlippery(float? wet_setting = null, float? min_wet_time = null, float? wet_time_to_add = null, float? max_wet_time = null, float? permanent = null, float? should_display_overlay = null)
    {
        return;
    }

    // tg: code/game/turfs/open/space/space.dm:152
    public override object can_have_cabling()
    {
        if (Dm.truthy(Dm.locate<obj_structure_lattice_catwalk>(this)))
            return TRUE;
        return FALSE;
    }

    // tg: code/game/turfs/open/space/space.dm:160
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        generate_space_underlay(underlay_appearance, asking_turf);
        return TRUE;
    }

    // tg: code/game/turfs/open/space/space.dm:200  (M5 slice 4: a turf built on deep space joins the nearstation area)
    public override async DmTask<object> ChangeTurf(object path = null, DmList new_baseturfs = null, object flags = null)
    {
        object dot = await base.ChangeTurf(path, new_baseturfs, flags);
        if (!Dm.truthy(dot) || isspaceturf(dot))
            return dot;

        if (Dm.truthy(Dm.bitand(flags, CHANGETURF_NO_AREA_CHANGE)))
            return dot;

        area new_turf_area = get_area(dot) as area;
        if (istype(new_turf_area, typeof(area_space)) && !istype(new_turf_area, typeof(area_space_nearstation)))
            set_turf_to_area(dot as turf, GLOB.areas_by_type[typeof(area_space_nearstation)] as area);
        return dot;
    }

    // tg: code/game/turfs/open/space/space.dm:212  (M5 slice 4)
    public override async DmTask attempt_lattice_replacement(float? amount = null)
    {
        object dest_x = destination_x;
        object dest_y = destination_y;
        object dest_z = destination_z;
        float here_x = x, here_y = y, here_z = z;
        await base.attempt_lattice_replacement(amount);
        // TURF-IDENTITY: tg's src is the turf the replacement left here
        if (locate(here_x, here_y, here_z) is turf_open now)
        {
            now.destination_x = dest_x;
            now.destination_y = dest_y;
            now.destination_z = dest_z;
        }
    }

    // tg: code/game/turfs/open/space/space.dm:221
    public override object can_cross_safely(atom_movable crossing = null)
    {
        return Dm.num(HAS_TRAIT(crossing, TRAIT_SPACEWALK));
    }
}

// tg: code/game/turfs/open/space/space.dm:149 (interleaved types, rules 1.9; M5x slice 5)
public partial class turf_open_space : turf_open
{
    // tg: code/game/turfs/open/space/space.dm:157
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/turfs/open/space/space.dm:149
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}

// tg: code/game/turfs/open/space/space.dm:165 (E13: the RCD)
public partial class turf_open_space : turf_open
{
    // tg: code/game/turfs/open/space/space.dm:165
    public override object rcd_vals(mob user = null, obj_item_construction_rcd the_rcd = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF))
        {
            if (the_rcd.rcd_design_path == typeof(turf_open_floor_plating_rcd))
            {
                obj_structure_lattice lattice = Dm.locate<obj_structure_lattice>(this);
                if (Dm.truthy(lattice))
                    return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 1));
                else
                    return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 3));
            }
            else if (the_rcd.rcd_design_path == typeof(obj_structure_lattice_catwalk))
            {
                obj_structure_lattice lattice = Dm.locate<obj_structure_lattice>(this);
                if (Dm.truthy(lattice))
                    return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 2));
                else
                    return Dm.list(Dm.kv("delay", 0), Dm.kv("cost", 4));
            }
            else
                return FALSE;
        }

        return FALSE;
    }

    // tg: code/game/turfs/open/space/space.dm:184
    public override async DmTask<object> rcd_act(mob user = null, obj_item_construction_rcd the_rcd = null, DmList rcd_data = null, params object[] __extra)
    {
        if (Dm.eq(the_rcd.mode, RCD_TURF))
        {
            if (Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(turf_open_floor_plating_rcd)))
            {
                await place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR);
                return TRUE;
            }
            else if (Dm.eq(rcd_data[RCD_DESIGN_PATH], typeof(obj_structure_lattice_catwalk)))
            {
                obj_structure_lattice lattice = Dm.locate<obj_structure_lattice>(this);
                if (Dm.truthy(lattice))
                    qdel(lattice);
                Dm.@new<obj_structure_lattice_catwalk>(this);
                return TRUE;
            }
            else
                return FALSE;
        }

        return FALSE;
    }
}

// tg: code/game/turfs/open/space/space.dm:224-280  /turf/open/space/openspace: open space with space's air, the levels' passage (DV436: the
// multi-z debug map's space levels are made of it); not here: its Initialize (the planes and the mapping log), LateInitialize (transparency is
// the client's, TurfStyle), Destroy and the starlight procs over its turf below (:282-305, lighting, D24)
public partial class turf_open_space_openspace : turf_open_space
{
    // tg: code/game/turfs/open/space/space.dm:249
    [DmParams()]
    public override object zAirIn(object direction = null, turf source = null)
    {
        return TRUE;
    }

    // tg: code/game/turfs/open/space/space.dm:252
    [DmParams()]
    public override object zAirOut(object direction = null, turf source = null)
    {
        return TRUE;
    }

    // tg: code/game/turfs/open/space/space.dm:255
    public override object zPassIn(object direction = null)
    {
        if (Dm.eq(direction, DOWN))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_IN_DOWN)))
                    return FALSE;
            }
            return TRUE;
        }
        if (Dm.eq(direction, UP))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_IN_UP)))
                    return FALSE;
            }
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/space/space.dm:268
    public override object zPassOut(object direction = null)
    {
        if (Dm.eq(direction, DOWN))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_OUT_DOWN)))
                    return FALSE;
            }
            return TRUE;
        }
        if (Dm.eq(direction, UP))
        {
            foreach (obj contained_object in Dm.each<obj>(contents))
            {
                if (Dm.truthy(Dm.bitand(contained_object.obj_flags, BLOCK_Z_OUT_UP)))
                    return FALSE;
            }
            return TRUE;
        }
        return FALSE;
    }
}
