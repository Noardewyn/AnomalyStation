// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of the wave-4 scope (package B): the procs of later milestones are
// Dm.NotTranslated with the milestone named, and overrides of slots whose roots are not translated yet are listed as not
// declared. The W4.0 contracts wrote the var block (rules 10.2) and tg's overrides of BYOND's /turf defaults
// (dm_tg_vars, rules 10.2 "Builtin types", including can_atmos_pass from
// modules/atmospherics/environmental/LINDA_system.dm:15). The [DmPath] of a BYOND builtin type is on its engine part in
// src/Core/Tg/Builtin/.

public partial class datum_controller_global_vars
{
    // tg: code/game/turfs/turf.dm:1  GLOBAL_LIST_EMPTY(station_turfs)
    public DmList station_turfs;

    // tg: code/game/turfs/turf.dm:1  (GLOBAL_MANAGED(station_turfs, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_turfs()
    {
        station_turfs = Dm.list();
        Dm.append(ref gvars_datum_init_order, "station_turfs");
    }
}

/// Any floor or wall. What makes up the station and the rest of the map.
// tg: code/game/turfs/turf.dm:4
public partial class turf : atom
{
    // tg: code/game/turfs/turf.dm:5-10, modules/atmospherics/environmental/LINDA_system.dm:15  tg's overrides of BYOND's
    // /turf defaults (rules 10.2, "Builtin types"), in .dme order
    private partial void dm_tg_vars()
    {
        icon = Dm.resource("icons/turf/floors.dmi");
        abstract_type = typeof(turf);
        datum_flags = DF_STATIC_OBJECT;
        vis_flags = VIS_INHERIT_ID; // Important for interaction with and visualization of openspace.
        luminosity = 1;
        light_height = LIGHTING_HEIGHT_FLOOR;
        can_atmos_pass = ATMOS_PASS_NO;
    }


    ///what /mob/oranges_ear instance is already assigned to us as there should only ever be one.
    ///used for guaranteeing there is only one oranges_ear per turf when assigned, speeds up view() iteration
    // tg: code/game/turfs/turf.dm:14
    public mob_oranges_ear assigned_oranges_ear;

    /// Turf bitflags, see code/__DEFINES/flags.dm
    // tg: code/game/turfs/turf.dm:17
    public float turf_flags = NONE;

    /// If there's a tile over a basic floor that can be ripped out
    // tg: code/game/turfs/turf.dm:20
    public float overfloor_placed = FALSE;
    /// How accessible underfloor pieces such as wires, pipes, etc are on this turf. Can be HIDDEN, VISIBLE, or INTERACTABLE.
    // tg: code/game/turfs/turf.dm:22
    public float underfloor_accessibility = UNDERFLOOR_HIDDEN;
    /// If there is a lattice underneat this turf. Used for the attempt_lattice_replacement proc to determine if it should place lattice.
    // tg: code/game/turfs/turf.dm:24
    public float lattice_underneath = TRUE;

    // baseturfs can be either a list or a single turf type.
    // In class definition like here it should always be a single type.
    // A list will be created in initialization that figures out the baseturf's baseturf etc.
    // In the case of a list it is sorted from bottom layer to top.
    // This shouldn't be modified directly, use the helper procs.
    // tg: code/game/turfs/turf.dm:31
    // (object: a typepath here, and assemble_baseturfs stores the list of baseturfs in it, turf.dm:490-511; rules 3.5, a
    // "-" cell)
    public object baseturfs = typeof(turf_baseturf_bottom);

    // tg: code/game/turfs/turf.dm:33
    public float temperature = T20C;
    ///Used for fire, if a melting temperature was reached, it will be destroyed
    // tg: code/game/turfs/turf.dm:35
    public float to_be_destroyed = 0;
    ///The max temperature of the fire which it was subjected to, determines the melting point of turf
    // tg: code/game/turfs/turf.dm:37
    public float max_fire_temperature_sustained = 0;

    // tg: code/game/turfs/turf.dm:39
    public float blocks_air = FALSE;
    // If this turf should initialize atmos adjacent turfs or not
    // Optimization, not for setting outside of initialize
    // tg: code/game/turfs/turf.dm:42
    public float init_air = TRUE;

    // tg: code/game/turfs/turf.dm:44
    public DmList blueprint_data; //for the station blueprints, images of objects eg: pipes

    // tg: code/game/turfs/turf.dm:46
    public DmList explosion_throw_details;

    // tg: code/game/turfs/turf.dm:48
    public object requires_activation; //add to air processing after initialize?
    // tg: code/game/turfs/turf.dm:49
    public float changing_turf = FALSE;

    // tg: code/game/turfs/turf.dm:51
    public object bullet_bounce_sound = Dm.resource("sound/items/weapons/gun/general/mag_bullet_remove.ogg"); //sound played when a shell casing is ejected ontop of the turf.
    // tg: code/game/turfs/turf.dm:52
    public float bullet_sizzle = FALSE; //used by ammo_casing/bounce_away() to determine if the shell casing should make a sizzle sound when it's ejected over the turf
    //IE if the turf is supposed to be water, set TRUE.

    // tg: code/game/turfs/turf.dm:55
    public float tiled_turf = FALSE; // use tiled water and dirt decals

    ///Icon-smoothing variable to map a diagonal wall corner with a fixed underlay.
    // tg: code/game/turfs/turf.dm:58
    public DmList fixed_underlay;

    ///Bool, whether this turf will always be illuminated no matter what area it is in
    ///Makes it look blue, be warned
    // tg: code/game/turfs/turf.dm:62
    public float space_lit = FALSE;

    // tg: code/game/turfs/turf.dm:64
    public float lighting_corners_initialised = FALSE;

    ///Our lighting object.
    // tg: code/game/turfs/turf.dm:67
    public atom_movable_lighting_object lighting_object;
    ///Lighting Corner datums.
    // tg: code/game/turfs/turf.dm:69
    public datum_lighting_corner lighting_corner_NE;
    // tg: code/game/turfs/turf.dm:70
    public datum_lighting_corner lighting_corner_SE;
    // tg: code/game/turfs/turf.dm:71
    public datum_lighting_corner lighting_corner_SW;
    // tg: code/game/turfs/turf.dm:72
    public datum_lighting_corner lighting_corner_NW;


    ///Which directions does this turf block the vision of, taking into account both the turf's opacity and the movable opacity_sources.
    // tg: code/game/turfs/turf.dm:76
    public float directional_opacity = NONE;
    ///Lazylist of movable atoms providing opacity sources.
    // tg: code/game/turfs/turf.dm:78
    public DmList opacity_sources;

    ///the holodeck can load onto this turf if TRUE
    // tg: code/game/turfs/turf.dm:81
    public float holodeck_compatible = FALSE;

    /// If this turf contained an RCD'able object (or IS one, for walls)
    /// but is now destroyed, this will preserve the value.
    /// See __DEFINES/construction.dm for RCD_MEMORY_*.
    // tg: code/game/turfs/turf.dm:86
    public object rcd_memory;
    ///whether or not this turf forces movables on it to have no gravity (unless they themselves have forced gravity)
    // tg: code/game/turfs/turf.dm:88
    public float force_no_gravity = FALSE;

    ///This turf's resistance to getting rusted
    // tg: code/game/turfs/turf.dm:91
    public float rust_resistance = RUST_RESISTANCE_BASIC;

    /// How pathing algorithm will check if this turf is passable by itself (not including content checks). By default it's just density check.
    /// WARNING: Currently to use a density shortcircuiting this does not support dense turfs with special allow through function
    // tg: code/game/turfs/turf.dm:95
    public float pathing_pass_method = TURF_PATHING_PASS_DENSITY;

    // tg: code/game/turfs/turf.dm:98-102  #if defined(UNIT_TESTS) || defined(SPACEMAN_DMM) ... #endif not translated
    // (inactive in production): var/area/in_contents_of
    /// How much explosive resistance this turf is providing to itself
    /// Defaults to -1, interpreted as initial(explosive_resistance)
    /// This is an optimization to prevent turfs from needing to set these on init
    /// This would either be expensive, or impossible to manage. Let's just avoid it yes?
    /// Never directly access this, use get_explosive_block() instead
    // tg: code/game/turfs/turf.dm:108
    public float inherent_explosive_resistance = -1;

    ///The typepath we use for lazy fishing on turfs, to save on world init time.
    // tg: code/game/turfs/turf.dm:111
    public object fish_source;

    /// If TRUE, then this turf will be skipped entirely by minimap rendering.
    // tg: code/game/turfs/turf.dm:114
    public float skip_minimap_rendering = FALSE;

    /// <summary>
    /// TURF-IDENTITY (object-model.md 7.1; M5x slice 6): the turf that replaced this one at its cell (ChangeTurf), or null. BYOND's turf
    /// reference is its location, so a reference to a replaced turf names the cell's turf in tg; <see cref="dm_identity"/> follows these
    /// links where the port uses an old reference in a way the location matters (registering and unregistering signals on it).
    /// </summary>
    internal turf dm_replaced_by;

    /// <summary>The datum itself, or for a replaced turf the turf now at its cell (the end of its <see cref="dm_replaced_by"/> links).</summary>
    internal static datum dm_identity(datum d)
    {
        if (d is not turf t)
            return d;
        while (t.dm_replaced_by != null)
            t = t.dm_replaced_by;
        return t;
    }

    /// ChangeTurf passes the old signal tables here so initialization can update them directly.
    /// In particular, occupant initialization can move or delete existing contents before New() returns.
    // tg: code/game/turfs/turf.dm:119
    [DmParams("loc", "inherited_listen_lookup", "inherited_signal_procs")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        DmList inherited_listen_lookup = Dm.arg<DmList>(__extra, 1);
        DmList inherited_signal_procs = Dm.arg<DmList>(__extra, 2);
        _listen_lookup = inherited_listen_lookup;
        _signal_procs = inherited_signal_procs;
        // TURF-IDENTITY (object-model.md 7.1; M5x slice 6): the listeners of the replaced turf key it in their _signal_procs; in tg
        // that key is this turf (the same location reference), so they become this turf's here, before Initialize sends this turf's
        // first signals (a decal element's atom_init_success, an atmos-sensitive window's turf_expose)
        if (loc is turf replaced && replaced != this && inherited_listen_lookup != null)
            replaced.dm_replace_signal_turf(inherited_listen_lookup, this);
        await base.New(loc);
    }

    // tg: code/game/turfs/turf.dm:124  vv_edit_var(var_name, new_value): not declared, its root /datum/proc/vv_edit_var
    // (datums/datumvars.dm, VV) is not translated (rules 13).

    /**
     * Turf Initialize
     *
     * Doesn't call parent, see [/atom/proc/Initialize]
     * Please note, space tiles do not run this code.
     * This is done because it's called so often that any extra code just slows things down too much
     * If you add something relevant here add it there too
     * [/turf/open/space/Initialize]
     */
    // tg: code/game/turfs/turf.dm:139  (SHOULD_CALL_PARENT(FALSE) is dropped, rules 7.9)
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            stack_trace(Dm.text("Warning: []([]) initialized multiple times!", this, type), "code/game/turfs/turf.dm", 142);
        Dm.combine(ref flags_1, INITIALIZED_1);

        /// We do NOT use the shortcut here, because this is faster
        if (Dm.truthy(SSmapping.max_plane_offset))
        {
            if (!Dm.truthy(SSmapping.plane_offset_blacklist[Dm.text("[]", plane)]))
                plane = Dm.num(Dm.sub(plane, Dm.mul(PLANE_RANGE, SSmapping.z_level_to_plane_offset[z])));

            turf T = GET_TURF_ABOVE(this) as turf;
            if (Dm.truthy(T))
                T.multiz_turf_new(this, DOWN);
            T = GET_TURF_BELOW(this) as turf;
            if (Dm.truthy(T))
                T.multiz_turf_new(this, UP);
        }

        // by default, vis_contents is inherited from the turf that was here before.
        // Checking length(vis_contents) in a proc this hot has huge wins for performance.
        if (Dm.truthy(length(vis_contents)))
            vis_contents.Cut();

        Waiting.NoSleep(assemble_baseturfs());

        levelupdate();

        // SETUP_SMOOTHING() (__DEFINES/icon_smoothing.dm:236), expanded inline (rules 11.1 (d): it reads src's vars)
        if (Dm.truthy(smoothing_groups))
        {
            // PARSE_SMOOTHING_GROUPS(smoothing_groups, smoothing_groups) (__DEFINES/icon_smoothing.dm:243)
            if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/sorted_smoothing_groups) is FALSE in production (rules 1.7)
            {
                // ASSERT_SORTED_SMOOTHING_GROUPS(smoothing_groups) (__DEFINES/icon_smoothing.dm:265)
                DmList unwrapped = json_decode(Dm.text("\\[[]0\\]", smoothing_groups)) as DmList;
                unwrapped.len--;
                assert_sorted(unwrapped, Dm.text("smoothing_groups ([])", type));
            }
            SET_SMOOTHING_GROUPS(smoothing_groups, ref smoothing_groups);
        }
        if (Dm.truthy(canSmoothWith))
        {
            // PARSE_CAN_SMOOTH_WITH(canSmoothWith, canSmoothWith, smoothing_flags) (__DEFINES/icon_smoothing.dm:250)
            if (Dm.truthy(FALSE)) // PERFORM_ALL_TESTS(focus_only/sorted_smoothing_groups) is FALSE in production (rules 1.7)
            {
                // ASSERT_SORTED_SMOOTHING_GROUPS(canSmoothWith) (__DEFINES/icon_smoothing.dm:265)
                DmList unwrapped = json_decode(Dm.text("\\[[]0\\]", canSmoothWith)) as DmList;
                unwrapped.len--;
                assert_sorted(unwrapped, Dm.text("canSmoothWith ([])", type));
            }
            /* S_OBJ is always negative, and we are guaranteed to be sorted. */
            // SHIM-GAP: BYOND 515 text indexing (canSmoothWith[1], the character at a position) is not in the shim;
            // copytext(canSmoothWith, 1, 2) gives the same one-character text.
            if (Dm.eq(copytext(canSmoothWith, 1, 2), "-"))
                Dm.combine(ref smoothing_flags, SMOOTH_OBJ);
            SET_SMOOTHING_GROUPS(canSmoothWith, ref canSmoothWith);
        }

        if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
            QUEUE_SMOOTH(this);

        foreach (atom_movable content in Dm.each_cast<atom_movable>(this))
            Waiting.NoSleep(initialize_occupant(content));

        area our_area = loc as area;
        if (!Dm.truthy(our_area.area_has_base_lighting) && Dm.truthy(space_lit)) //Only provide your own lighting if the area doesn't for you
        {
            // add_overlay(GLOB.starlight_overlays[GET_TURF_PLANE_OFFSET(src) + 1]): GLOB.starlight_overlays
            // (modules/lighting/static_lighting_area.dm:27) is lighting, which is not ported (D24, rules 13).
            Dm.Deferred("code/game/turfs/turf.dm:176", "add_overlay(GLOB.starlight_overlays[...]): the starlight overlay is lighting, not ported (D24, rules 13)");
        }

        if (Dm.truthy(requires_activation))
            CALCULATE_ADJACENT_TURFS(this, KILL_EXCITED);

        if (Dm.truthy(light_power) && Dm.truthy(light_range))
            update_light();

        if (Dm.truthy(opacity))
            directional_opacity = ALL_CARDINALS;

        if (Dm.truthy(uses_integrity))
            atom_integrity = max_integrity;

        // apply materials properly from the default custom_materials value
        if (Dm.truthy(length(custom_materials)))
            Waiting.NoSleep(set_custom_materials(custom_materials));

        return INITIALIZE_HINT_NORMAL;
    }

    /// Applies this turf to an existing occupant during Initialize(), without announcing movement.
    /// Call the parent first so the occupant can update turf-dependent state before subtype effects run.
    // tg: code/game/turfs/turf.dm:198
    [ShouldCallParent]
    public virtual async DmTask initialize_occupant(atom_movable occupant = null)
    {
        if (Dm.truthy(occupant._listen_lookup?[COMSIG_MOVABLE_TURF_INITIALIZING]))
            occupant._SendSignal(COMSIG_MOVABLE_TURF_INITIALIZING, Dm.list(occupant, this));
    }

    /// Initializes our adjacent turfs. If you want to avoid this, do not override it, instead set init_air to FALSE
    // tg: code/game/turfs/turf.dm:203
    public virtual void Initalize_Atmos(object time = null)
    {
        CALCULATE_ADJACENT_TURFS(this, NORMAL_TURF);
    }

    // tg: code/game/turfs/turf.dm:206
    public override object Destroy(float? force = null)
    {
        object dot = null;
        dot = QDEL_HINT_IWILLGC;
        if (!Dm.truthy(changing_turf))
            stack_trace("Incorrect turf deletion", "code/game/turfs/turf.dm", 209);

        changing_turf = FALSE;
        if (Dm.truthy(GET_LOWEST_STACK_OFFSET(z)))
        {
            turf T = GET_TURF_ABOVE(this) as turf;
            if (Dm.truthy(T))
                T.multiz_turf_del(this, DOWN);
            T = GET_TURF_BELOW(this) as turf;
            if (Dm.truthy(T))
                T.multiz_turf_del(this, UP);
        }

        if (Dm.truthy(force))
        {
            base.Destroy(force);
            //this will completely wipe turf state
            turf B = Dm.@new(world.turf, this) as turf;
            foreach (object A in Dm.each(B.contents))
                qdel(A);
            return dot;
        }

        LAZYCLEARLIST(blueprint_data);
        Dm.mask(ref flags_1, Dm.bitnot(INITIALIZED_1));
        requires_activation = FALSE;
        base.Destroy(force);

        if (Dm.truthy(length(vis_contents)))
            vis_contents.Cut();
        return dot;
    }

    /// WARNING WARNING
    /// Turfs DO NOT lose their external signal connections when they get replaced, REMEMBER THIS
    /// It's possible because turfs are fucked, and if you have one in a list and it's replaced with another one, the list ref points to the new turf
    /// We do it to avoid unregistering and re-registering every signal in a very commonly used bit of code
    /// Signals registered by the turf on itself are cleared so the new type can register its own
    // tg: code/game/turfs/turf.dm:241
    public override void _clear_signal_refs()
    {
        UnregisterSignal(this, _signal_procs?[this]);
    }

    // tg: code/game/turfs/turf.dm:244  attack_hand(mob/user, list/modifiers): not declared, its root /atom/proc/attack_hand
    // (_onclick/other_mobs.dm) is translated with the interaction layer (M2).

    /// Call to move a turf from its current area to a new one
    // tg: code/game/turfs/turf.dm:251
    public virtual void change_area(area old_area = null, area new_area = null)
    {
        //don't waste our time
        if (old_area == new_area)
            return;

        //move the turf
        // LISTASSERTLEN(old_area.turfs_to_uncontain_by_zlevel, z, list()) (__HELPERS/_lists.dm:114), expanded inline (rules 11.1 (e))
        if (length(old_area.turfs_to_uncontain_by_zlevel) < z)
        {
            float _OLD_LENGTH = length(old_area.turfs_to_uncontain_by_zlevel);
            old_area.turfs_to_uncontain_by_zlevel.len = z;
            /* Convert the optional argument to a if check */
            foreach (object _USELESS_VAR in Dm.each(Dm.list(Dm.list())))
            {
                for (float _INDEX_TO_ASSIGN_TO = _OLD_LENGTH + 1, _INDEX_TO_ASSIGN_TO__end = z; _INDEX_TO_ASSIGN_TO <= _INDEX_TO_ASSIGN_TO__end; _INDEX_TO_ASSIGN_TO++)
                    old_area.turfs_to_uncontain_by_zlevel[_INDEX_TO_ASSIGN_TO] = Dm.list();
            }
        }
        // LISTASSERTLEN(new_area.turfs_by_zlevel, z, list()) (__HELPERS/_lists.dm:114), expanded inline (rules 11.1 (e))
        if (length(new_area.turfs_by_zlevel) < z)
        {
            float _OLD_LENGTH = length(new_area.turfs_by_zlevel);
            new_area.turfs_by_zlevel.len = z;
            /* Convert the optional argument to a if check */
            foreach (object _USELESS_VAR in Dm.each(Dm.list(Dm.list())))
            {
                for (float _INDEX_TO_ASSIGN_TO = _OLD_LENGTH + 1, _INDEX_TO_ASSIGN_TO__end = z; _INDEX_TO_ASSIGN_TO <= _INDEX_TO_ASSIGN_TO__end; _INDEX_TO_ASSIGN_TO++)
                    new_area.turfs_by_zlevel[_INDEX_TO_ASSIGN_TO] = Dm.list();
            }
        }
        Dm.append_at(old_area.turfs_to_uncontain_by_zlevel, z, this);
        Dm.append_at(new_area.turfs_by_zlevel, z, this);
        Dm.append(new_area.contents, this);
        if (Dm.truthy(this._listen_lookup?[COMSIG_TURF_AREA_CHANGED]))
            this._SendSignal(COMSIG_TURF_AREA_CHANGED, Dm.list(this, old_area));
        if (Dm.truthy(new_area._listen_lookup?[COMSIG_AREA_TURF_ADDED]))
            new_area._SendSignal(COMSIG_AREA_TURF_ADDED, Dm.list(new_area, this, old_area));
        if (Dm.truthy(old_area._listen_lookup?[COMSIG_AREA_TURF_REMOVED]))
            old_area._SendSignal(COMSIG_AREA_TURF_REMOVED, Dm.list(old_area, this, new_area));

        //changes to make after turf has moved
        on_change_area(old_area, new_area);
    }

    /// Allows for reactions to an area change without inherently requiring change_area() be called (I hate maploading)
    // tg: code/game/turfs/turf.dm:270
    public virtual void on_change_area(area old_area = null, area new_area = null)
    {
        transfer_area_lighting(old_area, new_area);
    }

    // tg: code/game/turfs/turf.dm:273
    public virtual void multiz_turf_del(turf T = null, object dir = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_TURF_MULTIZ_DEL]))
            this._SendSignal(COMSIG_TURF_MULTIZ_DEL, Dm.list(this, T, dir));
    }

    // tg: code/game/turfs/turf.dm:276
    public virtual void multiz_turf_new(turf T = null, object dir = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_TURF_MULTIZ_NEW]))
            this._SendSignal(COMSIG_TURF_MULTIZ_NEW, Dm.list(this, T, dir));
    }

    /**
     * Check whether the specified turf is blocked by something dense inside it with respect to a specific atom.
     *
     * Returns truthy value TURF_BLOCKED_TURF_DENSE if the turf is blocked because the turf itself is dense.
     * Returns truthy value TURF_BLOCKED_CONTENT_DENSE if one of the turf's contents is dense and would block
     * a source atom's movement.
     * Returns falsey value TURF_NOT_BLOCKED if the turf is not blocked.
     *
     * Arguments:
     * * exclude_mobs - If TRUE, ignores dense mobs on the turf.
     * * source_atom - If this is not null, will check whether any contents on the turf can block this atom specifically. Also ignores itself on the turf.
     * * ignore_atoms - Check will ignore any atoms in this list. Useful to prevent an atom from blocking itself on the turf.
     * * type_list - are we checking for types of atoms to ignore and not physical atoms
     */
    // tg: code/game/turfs/turf.dm:293
    public virtual async DmTask<object> is_blocked_turf(float? exclude_mobs = null, object source_atom = null, DmList ignore_atoms = null, float? type_list = null)
    {
        exclude_mobs ??= FALSE;
        type_list ??= FALSE;
        if (Dm.truthy(density))
            return TRUE;

        foreach (atom_movable movable_content in Dm.each_cast<atom_movable>(contents))
        {
            // We don't want to block ourselves
            if (Dm.eq(movable_content, source_atom))
                continue;
            // don't consider ignored atoms or their types
            if (Dm.truthy(length(ignore_atoms)))
            {
                if (!Dm.truthy(type_list) && Dm.@in(movable_content, ignore_atoms))
                    continue;
                else if (Dm.truthy(type_list) && Dm.truthy(is_type_in_list(movable_content, ignore_atoms)))
                    continue;
            }

            // If the thing is dense AND we're including mobs or the thing isn't a mob AND if there's a source atom and
            // it cannot pass through the thing on the turf,  we consider the turf blocked.
            if (Dm.truthy(movable_content.density) && (!Dm.truthy(exclude_mobs) || !ismob(movable_content)))
            {
                if (Dm.truthy(source_atom) && Dm.truthy(await movable_content.CanPass(source_atom as atom_movable, get_dir(this, source_atom))))
                    continue;
                return TRUE;
            }
        }
        return FALSE;
    }

    /**
     * Checks whether the specified turf is blocked by something dense inside it, but ignores anything with the climbable trait
     *
     * Works similar to is_blocked_turf(), but ignores climbables and has less options. Primarily added for jaunting checks
     */
    // tg: code/game/turfs/turf.dm:321
    public virtual object is_blocked_turf_ignore_climbable()
    {
        if (Dm.truthy(density))
            return TRUE;

        foreach (atom_movable atom_content in Dm.each_cast<atom_movable>(contents))
        {
            if (Dm.truthy(atom_content.density) && !Dm.truthy(Dm.bitand(atom_content.flags_1, ON_BORDER_1)) && !HAS_TRAIT(atom_content, TRAIT_CLIMBABLE))
                return TRUE;
        }
        return FALSE;
    }

    //The zpass procs exist to be overridden, not directly called
    //use can_z_pass for that
    ///If we'd allow anything to travel into us
    // tg: code/game/turfs/turf.dm:333
    public virtual object zPassIn(object direction = null)
    {
        return FALSE;
    }

    ///If we'd allow anything to travel out of us
    // tg: code/game/turfs/turf.dm:337
    public virtual object zPassOut(object direction = null)
    {
        return FALSE;
    }

    //direction is direction of travel of air
    // tg: code/game/turfs/turf.dm:341
    public virtual object zAirIn(object direction = null, turf source = null)
    {
        return FALSE;
    }

    //direction is direction of travel
    // tg: code/game/turfs/turf.dm:345
    public virtual object zAirOut(object direction = null, turf source = null)
    {
        return FALSE;
    }

    /// Precipitates a movable (plus whatever buckled to it) to lower z levels if possible and then calls zImpact()
    // tg: code/game/turfs/turf.dm:349
    public virtual async DmTask<object> zFall(atom_movable falling = null, float? levels = null, float? force = null, float? falling_from_move = null)
    {
        levels ??= 1;
        force ??= FALSE;
        falling_from_move ??= FALSE;
        object direction = DOWN;
        if (Dm.le(falling.has_gravity(), NEGATIVE_GRAVITY))
            direction = UP;
        turf target = get_step_multiz(this, direction) as turf;
        if (!Dm.truthy(target))
            return FALSE;
        bool isliving = GlobalProcs.isliving(falling);
        if (!isliving && !isobj(falling))
            return null;
        if (isliving)
        {
            mob_living falling_living = falling as mob_living;
            //relay this mess to whatever the mob is buckled to.
            if (Dm.truthy(falling_living.buckled))
                falling = falling_living.buckled;
        }
        if (!Dm.truthy(falling_from_move) && Dm.truthy(falling.currently_z_moving))
            return null;
        if (FlightSeam.FallsToImpact(falling))
        {
            falling.set_currently_z_moving(FALSE, TRUE); // DV435: already falling to its impact below (an open turf it drifted onto marked it)
            return null;
        }
        if (!Dm.truthy(force) && !Dm.truthy(falling.can_z_move(direction, this, target, ZMOVE_FALL_FLAGS)))
        {
            falling.set_currently_z_moving(FALSE, TRUE);
            return FALSE;
        }

        // So it doesn't trigger other zFall calls. Cleared on zMove.
        falling.set_currently_z_moving(CURRENTLY_Z_FALLING);
        // DEVIATION(DV435): tg's zMove, then zImpact once the falling thing reaches the target's floor under gravity, not at once
        await FlightSeam.FallTo(falling, this, target, Dm.num(levels));

        return TRUE;
    }

    ///Called each time the target falls down a z level possibly making their trajectory come to a halt. see __DEFINES/movement.dm.
    // tg: code/game/turfs/turf.dm:378
    public virtual async DmTask<object> zImpact(atom_movable falling = null, float? levels = null, turf prev_turf = null, float? flags = null)
    {
        levels ??= 1;
        flags ??= NONE;
        DmList falling_movables = falling.get_z_move_affected() as DmList;
        // rules 3.4: tg's `var/list/falling_mov_names += name` makes a null list the text of the name (and the texts are joined),
        // so the loop below iterates nothing and "falls through" is never shown, as in tg
        object falling_mov_names = null;
        foreach (atom_movable falling_mov in Dm.each_cast<atom_movable>(falling_movables))
            Dm.append(ref falling_mov_names, falling_mov.name);
        foreach (object i in Dm.each(contents))
        {
            atom thing = i as atom;
            Dm.combine(ref flags, await thing.intercept_zImpact(falling_movables, levels));
            if (Dm.truthy(Dm.bitand(flags, FALL_STOP_INTERCEPTING)))
                break;
        }
        if (Dm.truthy(prev_turf) && !Dm.truthy(Dm.bitand(flags, FALL_NO_MESSAGE)))
        {
            foreach (object mov_name in Dm.each(falling_mov_names))
                prev_turf.visible_message(span_danger(I18n.T("[] falls through []!", mov_name, prev_turf)));
        }
        if (!Dm.truthy(Dm.bitand(flags, FALL_INTERCEPTED)) && Dm.truthy(await zFall(falling, Dm.num(levels) + 1)))
            return FALSE;
        foreach (atom_movable falling_mov in Dm.each_cast<atom_movable>(falling_movables))
        {
            if (!Dm.truthy(Dm.bitand(flags, FALL_RETAIN_PULL)))
                await falling_mov.stop_pulling();
            if (!Dm.truthy(Dm.bitand(flags, FALL_INTERCEPTED)))
                await falling_mov.onZImpact(this, levels);
            if (Dm.truthy(falling_mov.pulledby) && (falling_mov.z != falling_mov.pulledby.z || get_dist(falling_mov, falling_mov.pulledby) > 1))
                await falling_mov.pulledby.stop_pulling();
        }
        return TRUE;
    }

    // tg: code/game/turfs/turf.dm:402
    public virtual async DmTask handleRCL(obj_item_rcl rapid_layer = null, mob user = null)
    {
        if (!Dm.truthy(rapid_layer.loaded))
            return;
        await lay_pipe_cleaner(rapid_layer.loaded, user);
        if (Dm.truthy(rapid_layer.wiring_gui_menu))
            rapid_layer.wiringGuiUpdate(user);
        rapid_layer.is_empty(user);
    }

    // tg: code/game/turfs/turf.dm:410
    public virtual async DmTask lay_pipe_cleaner(obj_item_stack_pipe_cleaner_coil coil = null, object user = null)
    {
        foreach (obj_structure_pipe_cleaner lain_cable in Dm.each<obj_structure_pipe_cleaner>(this))
        {
            if (!Dm.truthy(lain_cable.d1) || !Dm.truthy(lain_cable.d2))
            {
                await lain_cable.item_interaction(user as mob_living, coil);
                return;
            }
        }
        await coil.place_turf(this, user as mob);
    }

    // tg: code/game/turfs/turf.dm:417  item_interaction(mob/living/user, obj/item/tool, list/modifiers): not declared, its
    // root /atom/proc/item_interaction (game/atom/atom_tool_acts.dm) is translated with the interaction layer (M4a).

    //There's a lot of QDELETED() calls here if someone can figure out how to optimize this but not runtime when something gets deleted by a Bump/CanPass/Cross call, lemme know or go ahead and fix this mess - kevinz000
    // tg: code/game/turfs/turf.dm:434
    [DmParams("mover")]
    public override async DmTask<object> Enter(atom_movable mover = null, atom oldloc = null)
    {
        // Do not call ..()
        // Byond's default turf/Enter() doesn't have the behaviour we want with Bump()
        // By default byond will call Bump() on the first dense object in contents
        // Here's hoping it doesn't stay like this for years before we finish conversion to step_
        atom first_bump = null;
        object can_pass_self = await CanPass(mover, get_dir(this, mover));

        if (Dm.truthy(can_pass_self))
        {
            atom mover_loc = mover.loc;
            object mover_is_phasing = Dm.bitand(mover.movement_type, PHASING);
            foreach (atom_movable thing in Dm.each_cast<atom_movable>(contents))
            {
                if (thing == mover || thing == mover_loc) // Multi tile objects and moving out of other objects
                    continue;
                if (!Dm.truthy(await thing.Cross(mover)))
                {
                    if (QDELETED(mover)) //deleted from Cross() (CanPass is pure so it can't delete, Cross shouldn't be doing this either though, but it can happen)
                        return FALSE;
                    if (Dm.truthy(mover_is_phasing))
                    {
                        await mover.Bump(thing);
                        if (QDELETED(mover)) //deleted from Bump()
                            return FALSE;
                        continue;
                    }
                    else
                    {
                        if (!Dm.truthy(first_bump) || ((thing.layer > first_bump.layer || Dm.truthy(Dm.bitand(thing.flags_1, ON_BORDER_1))) && !Dm.truthy(Dm.bitand(first_bump.flags_1, ON_BORDER_1))))
                            first_bump = thing;
                    }
                }
            }
        }
        if (QDELETED(mover)) //Mover deleted from Cross/CanPass/Bump, do not proceed.
            return FALSE;
        if (!Dm.truthy(can_pass_self)) //Even if mover is unstoppable they need to bump us.
            first_bump = this;
        if (Dm.truthy(first_bump))
        {
            await mover.Bump(first_bump);
            return Dm.bitand(mover.movement_type, PHASING);
        }
        return TRUE;
    }

    // A proc in case it needs to be recreated or badmins want to change the baseturfs
    // tg: code/game/turfs/turf.dm:469
    // (fake_baseturf_type is object, not tg's declared turf: tg passes a list or a typepath, told apart with length(),
    // rules 3.4; decided slot parameter, reported to the lead for Appendix A)
    public virtual async DmTask<object> assemble_baseturfs(object fake_baseturf_type = null)
    {
        Type current_target = null;
        if (Dm.truthy(fake_baseturf_type))
        {
            if (Dm.truthy(length(fake_baseturf_type))) // We were given a list, just apply it and move on
            {
                baseturfs = await baseturfs_string_list(fake_baseturf_type, this);
                return null;
            }
            current_target = fake_baseturf_type as Type;
        }
        else
        {
            if (Dm.truthy(length(baseturfs)))
                return null; // No replacement baseturf has been given and the current baseturfs value is already a list/assembled
            if (!Dm.truthy(baseturfs))
            {
                current_target = (Dm.initial(this, (turf p) => p.baseturfs) is var or__1 && Dm.truthy(or__1) ? or__1 : type) as Type; // This should never happen but just in case...
                stack_trace(Dm.text("baseturfs var was null for []. Failsafe activated and it has been given a new baseturfs value of [].", type, current_target), "code/game/turfs/turf.dm", 482);
            }
            else
                current_target = baseturfs as Type;
        }

        // If we've made the output before we don't need to regenerate it
        if (Dm.truthy(assemble_baseturfs__created_baseturf_lists[current_target]))
        {
            // (object, rules 3.4: the cache holds a typepath for a single baseturf, :499, and a list otherwise, :514)
            object premade_baseturfs = assemble_baseturfs__created_baseturf_lists[current_target];
            if (Dm.truthy(length(premade_baseturfs)))
                baseturfs = await baseturfs_string_list(((DmList)premade_baseturfs).Copy(), this);
            else
                baseturfs = await baseturfs_string_list(premade_baseturfs, this);
            return baseturfs;
        }

        Type next_target = Dm.initial(current_target, (turf p) => p.baseturfs) as Type;
        //Most things only have 1 baseturf so this loop won't run in most cases
        if (current_target == next_target)
        {
            baseturfs = current_target;
            assemble_baseturfs__created_baseturf_lists[current_target] = current_target;
            return current_target;
        }
        DmList new_baseturfs = Dm.list(current_target);
        for (float i = 0; current_target != next_target; i++)
        {
            if (i > 100)
            {
                // A baseturfs list over 100 members long is silly
                // Because of how this is all structured it will only runtime/message once per type
                stack_trace(Dm.text("A turf <[]> created a baseturfs list over 100 members long. This is most likely an infinite loop.", type), "code/game/turfs/turf.dm", 506);
                message_admins(Dm.text("A turf <[]> created a baseturfs list over 100 members long. This is most likely an infinite loop.", type));
                break;
            }
            new_baseturfs.Insert(1, next_target);
            current_target = next_target;
            next_target = Dm.initial(current_target, (turf p) => p.baseturfs) as Type;
        }

        baseturfs = await baseturfs_string_list(new_baseturfs, this);
        assemble_baseturfs__created_baseturf_lists[new_baseturfs[new_baseturfs.len]] = new_baseturfs.Copy();
        return new_baseturfs;
    }

    // tg: code/game/turfs/turf.dm:470  var/static/list/created_baseturf_lists (rules 10.6)
    private static ref DmList assemble_baseturfs__created_baseturf_lists => ref TgState.Current.turf__assemble_baseturfs__created_baseturf_lists;

    // tg: code/game/turfs/turf.dm:517
    public virtual void levelupdate()
    {
        foreach (obj O in Dm.each<obj>(this))
        {
            if (Dm.truthy(Dm.bitand(O.flags_1, INITIALIZED_1)))
            {
                if (Dm.truthy(O._listen_lookup?[COMSIG_OBJ_HIDE]))
                    O._SendSignal(COMSIG_OBJ_HIDE, Dm.list(O, underfloor_accessibility));
            }
        }
    }
}

// tg: code/game/turfs/turf.dm:523  (interleaved types, rules 1.9)
public partial class turf_open_space : turf_open
{
    // override for space turfs, since they should never hide anything
    // tg: code/game/turfs/turf.dm:523
    public override void levelupdate()
    {
        return;
    }
}

// tg: code/game/turfs/turf.dm:527  (interleaved types, rules 1.9)
public partial class turf : atom
{
    // Removes all signs of lattice on the pos of the turf -Donkieyo
    // tg: code/game/turfs/turf.dm:527
    public virtual void RemoveLattice()
    {
        // RULE-GAP: rules 10.4 names locate(Type) in C, not tg's two-argument locate(Type, C); both search C's contents
        // for the first atom of that type (BYOND accepts the argument form), so it is written as locate(Type) in C.
        obj_structure_lattice L = Dm.locate<obj_structure_lattice>(this);
        if (Dm.truthy(L) && Dm.truthy(Dm.bitand(L.flags_1, INITIALIZED_1)))
            qdel(L);
    }

    // tg: code/game/turfs/turf.dm:532
    public virtual async DmTask bless_turf(float? invisible = null)
    {
        invisible ??= FALSE;
        if (!HAS_TRAIT(this, TRAIT_TURF_BLESSED))
            await AddElement(typeof(datum_element_blessed_turf), invisible); // gaps RG3 (holy water)
    }

    //////////////////////////////
    //Distance procs
    //////////////////////////////

    //Distance associates with all directions movement
    // tg: code/game/turfs/turf.dm:541
    public virtual object Distance(turf T = null)
    {
        return get_dist(this, T);
    }

    //  This Distance proc assumes that only cardinal movement is
    //  possible. It results in more efficient (CPU-wise) pathing
    //  for bots and anything else that only moves in cardinal dirs.
    // tg: code/game/turfs/turf.dm:547
    public virtual object Distance_cardinal(turf T = null)
    {
        if (!Dm.truthy(this) || !Dm.truthy(T))
            return FALSE;
        return abs(x - T.x) + abs(y - T.y);
    }

    ////////////////////////////////////////////////////

    // tg: code/game/turfs/turf.dm:554  (M5x slice 5)
    public override async DmTask<object> singularity_act()
    {
        if (underfloor_accessibility < UNDERFLOOR_INTERACTABLE)
        {
            foreach (obj on_top in Dm.each<obj>(contents)) //this is for deleting things like wires contained in the turf
            {
                if (HAS_TRAIT(on_top, TRAIT_UNDERFLOOR))
                    await on_top.singularity_act();
            }
        }
        await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
        return (2);
    }

    // M4a P1: the turf's base click procs, which every floor click reaches (the interaction guard of D132 refuses a click
    // whose tg definition is undeclared, interaction.md 7).
    // tg: code/game/turfs/turf.dm:244
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await user.Move_Pulled(this);
        return dot;
    }

    // tg: code/game/turfs/turf.dm:417
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(can_lay_cable()) && istype(tool, typeof(obj_item_stack_cable_coil)))
        {
            obj_item_stack_cable_coil coil = tool as obj_item_stack_cable_coil;
            await coil.place_turf(this, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(can_have_cabling()) && istype(tool, typeof(obj_item_stack_pipe_cleaner_coil)))
        {
            await lay_pipe_cleaner(tool as obj_item_stack_pipe_cleaner_coil, user);
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_rcl)))
        {
            await handleRCL(tool as obj_item_rcl, user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/turfs/turf.dm:562
    public virtual object can_have_cabling()
    {
        return TRUE;
    }

    // tg: code/game/turfs/turf.dm:565
    public virtual object can_lay_cable()
    {
        return Dm.num(Dm.truthy(can_have_cabling()) && underfloor_accessibility >= UNDERFLOOR_INTERACTABLE);
    }

    // tg: code/game/turfs/turf.dm:568
    public virtual object burn_tile()
    {
        return null;
    }

    // tg: code/game/turfs/turf.dm:571
    public virtual object break_tile()
    {
        return null;
    }

    /// Checks if this turf is protected from an explosion by something
    /// Return TRUE to stop the explosion from affecting this turf
    // tg: code/game/turfs/turf.dm:576
    public virtual object is_explosion_shielded(object severity = null)
    {
        return FALSE;
    }

    // tg: code/game/turfs/turf.dm:579  (M5x slice 1: explosions)
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        foreach (atom_movable movable_thing in Dm.each_cast<atom_movable>(this))
        {
            if (QDELETED(movable_thing) || !Dm.truthy(can_propagate_explosion(movable_thing, severity)))
                continue;
            switch (Dm.num(severity))
            {
                case EXPLODE_DEVASTATE:
                    Dm.append(ref SSexplosions.high_mov_atom, movable_thing);
                    break;
                case EXPLODE_HEAVY:
                    Dm.append(ref SSexplosions.med_mov_atom, movable_thing);
                    break;
                case EXPLODE_LIGHT:
                    Dm.append(ref SSexplosions.low_mov_atom, movable_thing);
                    break;
            }
        }
    }

    /// Called when propagating an explosion through contents,.
    /// Return FALSE to prevent the passed object from being exploded.
    // tg: code/game/turfs/turf.dm:593
    public virtual object can_propagate_explosion(atom_movable some_thing = null, object severity = null)
    {
        return TRUE;
    }

    // tg: code/game/turfs/turf.dm:596  narsie_act(force, ignore_mobs, probability = 20): not declared, its root
    // /atom/proc/narsie_act (game/atom/atom_act.dm) is not in the core version (ROADMAP section 4).

    // tg: code/game/turfs/turf.dm:605  (M3 P4: icon smoothing's diagonal corners call it, __HELPERS/icon_smoothing.dm:318)
    public virtual object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = icon;
        underlay_appearance.icon_state = icon_state;
        underlay_appearance.dir = Dm.num(adjacency_dir);
        return TRUE;
    }

    // tg: code/game/turfs/turf.dm:611
    public virtual void add_blueprints(atom_movable AM = null)
    {
        // engineering E14: the station blueprints show these images to their viewer (blueprints.dm:168, client.images, DV75)
        image I = Dm.@new<image>();
        SET_PLANE(I, GAME_PLANE, this);
        I.layer = OBJ_LAYER;
        I.appearance = AM.appearance;
        I.appearance_flags = Dm.bitor(Dm.bitor(Dm.bitor(RESET_COLOR, RESET_ALPHA), RESET_TRANSFORM), KEEP_APART);
        I.loc = this;
        I.dir = AM.dir; // I.setDir(AM.dir): /image/proc/setDir is `dir = newdir` (__HELPERS/icons.dm:829)
        I.alpha = 128;
        LAZYADD(ref blueprint_data, I);
    }

    // tg: code/game/turfs/turf.dm:622
    public virtual void add_blueprints_preround(atom_movable AM = null)
    {
        // M3 P4 (the infrastructure's init reaches it)
        if (!Dm.truthy(SSicon_smooth.initialized))
        {
            if (AM.layer == WIRE_LAYER) //wires connect to adjacent positions after its parent init, meaning we need to wait (in this case, until smoothing) to take its image
                Dm.append(ref SSicon_smooth.blueprint_queue, AM);
            else
                add_blueprints(AM);
        }
    }

    // tg: code/game/turfs/turf.dm:629
    public virtual object is_transition_turf()
    {
        return null;
    }

    // tg: code/game/turfs/turf.dm:632
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        object dot = await base.acid_act(acidpwr, acid_volume, __extra);
        if (Dm.le(acidpwr, 0) || Dm.le(acid_volume, 0))
            return FALSE;

        await AddComponent(typeof(datum_component_acid), acidpwr, acid_volume, GLOB.acid_overlay);

        return Dm.truthy(dot) ? dot : TRUE;
    }

    // tg: code/game/turfs/turf.dm:641
    public virtual async DmTask acid_melt()
    {
        return;
    }

    // tg: code/game/turfs/turf.dm:645  rust_heretic_act(rust_strength = RUST_RESISTANCE_BASIC): not declared, its root
    // /atom/proc/rust_heretic_act (game/atom/atom_act.dm) is not in the core version (ROADMAP section 4).

    /// Override this to change behaviour when being rusted
    // tg: code/game/turfs/turf.dm:655
    public virtual async DmTask<object> rust_turf(float? magic = null)
    {
        Dm.NotTranslated("code/game/turfs/turf.dm:655", "rust_turf: the rust elements are not in the core version (ROADMAP section 4)");
        return null;
    }

    // tg: code/game/turfs/turf.dm:661
    public override async DmTask handle_fall(mob faller = null)
    {
        if (Dm.truthy(has_gravity(this)))
            playsound(this, SFX_BODYFALL, 50, TRUE);
        await faller.drop_all_held_items();
    }

    // tg: code/game/turfs/turf.dm:666
    public virtual object photograph(float? limit = null)
    {
        Dm.NotTranslated("code/game/turfs/turf.dm:666", "photograph: image overlays of photographs are not in the core version (ROADMAP section 4)");
        return null;
    }

    // tg: code/game/turfs/turf.dm:680
    public override object AllowDrop()
    {
        return TRUE;
    }

    // tg: code/game/turfs/turf.dm:683
    public virtual async DmTask add_vomit_floor(mob_living vomiter = null, Type vomit_type = null, object vomit_flags = null, float? purge_ratio = null)
    {
        vomit_type ??= typeof(obj_effect_decal_cleanable_vomit);
        purge_ratio ??= 0.1f;
        obj_effect_decal_cleanable_vomit throw_up = Dm.@new(vomit_type, this, vomiter == null ? null : await vomiter.get_static_viruses()) as obj_effect_decal_cleanable_vomit;

        // if the vomit combined, apply toxicity and reagents to the old vomit
        if (QDELETED(throw_up))
            throw_up = Dm.locate<obj_effect_decal_cleanable_vomit>(this);

        if (throw_up == null)
            return;

        if (!iscarbon(vomiter) || (purge_ratio == 0))
            return;

        await clear_reagents_to_vomit_pool((mob_living_carbon)vomiter, throw_up, purge_ratio);
    }
}

// tg: code/game/turfs/turf.dm:698  (a global proc between the /turf procs)
public static partial class GlobalProcs
{
    // tg: code/game/turfs/turf.dm:698
    public static async DmTask clear_reagents_to_vomit_pool(mob_living_carbon owner = null, obj_effect_decal_cleanable_vomit vomit = null, float? purge_ratio = null)
    {
        purge_ratio ??= 0.1f;
        obj_item_organ_stomach belly = owner.get_organ_slot(ORGAN_SLOT_STOMACH) as obj_item_organ_stomach;
        datum_reagents belly_reagents = belly?.reagents as datum_reagents;
        if (!Dm.truthy(belly_reagents?.total_volume))
            return;

        float chemicals_lost = belly_reagents.total_volume * purge_ratio.Value;
        if (vomit.reagents != null)
            ((datum_reagents)vomit.reagents).maximum_volume += chemicals_lost;
        else
            vomit.create_reagents(chemicals_lost);

        await belly_reagents.trans_to(vomit, chemicals_lost, transferred_by: owner);
        // Clear the stomach of anything even not food
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(belly_reagents.reagent_list.Copy()))
        {
            if (!Dm.truthy(belly.food_reagents[reagent.type]))
            {
                await belly_reagents.remove_reagent(reagent.type, min(Dm.num(reagent.volume), 10));
                continue;
            }

            float bit_vol = Dm.num(reagent.volume) - Dm.num(belly.food_reagents[reagent.type]);
            if (bit_vol > 0)
                await belly_reagents.remove_reagent(reagent.type, min(bit_vol, 10));
        }
    }
}

// tg: code/game/turfs/turf.dm:723  (interleaved types, rules 1.9)
public partial class turf : atom
{
    //Whatever happens after high temperature fire dies out or thermite reaction works.
    //Should return new turf
    // tg: code/game/turfs/turf.dm:723
    public virtual async DmTask<object> Melt()
    {
        return await ScrapeAway(flags: CHANGETURF_INHERIT_AIR);
    }

    /// Handles exposing a turf to reagents.
    // tg: code/game/turfs/turf.dm:727
    [DmParams("reagents", "source", "methods", "volume_modifier", "show_message")]
    public override async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        // DM binds the arguments by position: the fourth is volume_modifier here (the root's show_message), the fifth
        // show_message (tg's holder.expose passes four, so volume_modifier gets its show_message and show_message its default).
        float? volume_modifier = show_message;
        float? own_show_message = Dm.arg<float?>(__extra, 0);
        methods ??= TOUCH;
        volume_modifier ??= 1;
        own_show_message ??= TRUE;
        object dot = await base.expose_reagents(reagents, source, methods, volume_modifier, Dm.forward(__extra, own_show_message));
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_NO_EXPOSE_REAGENTS)))
            return dot;

        if (Dm.truthy(source._listen_lookup?[COMSIG_REAGENTS_EXPOSE_TURF]))
            source._SendSignal(COMSIG_REAGENTS_EXPOSE_TURF, Dm.list(source, this, reagents, methods, volume_modifier, own_show_message));
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagents))
        {
            object reac_volume = reagents[reagent];
            dot = Dm.bitor(dot, await reagent.expose_turf(this, reac_volume));
        }
        return dot;
    }

    // When our turf is washed, we may wash everything on top of the turf
    // By default we will only wash mopable things (like blood or vomit)
    // but you may optionally pass in all_contents = TRUE to wash everything
    // tg: code/game/turfs/turf.dm:740
    [DmParams("clean_types", "all_contents")]
    public override async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        float? all_contents = Dm.arg<float?>(__extra, 0);
        all_contents ??= FALSE;
        object dot = null;
        dot = await base.wash(clean_types, Dm.forward(__extra, all_contents));
        foreach (atom_movable to_clean in Dm.each_cast<atom_movable>(this))
        {
            if (Dm.truthy(all_contents) || HAS_TRAIT(to_clean, TRAIT_MOPABLE))
                Dm.combine(ref dot, await to_clean.wash(clean_types));
        }
        return dot;
    }

    // tg: code/game/turfs/turf.dm:746
    public override object set_density(object new_value = null)
    {
        object dot = null;
        float old_density = density;
        dot = base.set_density(new_value);
        if (old_density == density)
            return dot;

        if (Dm.truthy(old_density))
            explosive_resistance -= Dm.num(get_explosive_block());
        if (Dm.truthy(density))
            explosive_resistance += Dm.num(get_explosive_block());
        return dot;
    }

    /// Wrapper around inherent_explosive_resistance
    /// We assume this proc is cold, so we can move the "what is our block" into it
    // tg: code/game/turfs/turf.dm:759
    public virtual object get_explosive_block()
    {
        if (inherent_explosive_resistance != -1)
            return inherent_explosive_resistance;
        if (Dm.truthy(explosive_resistance))
            return Dm.initial(this, (atom p) => p.explosive_resistance);
        return 0;
    }

    /**
     * Returns adjacent turfs to this turf that are reachable, in all cardinal directions
     *
     * Arguments:
     * * requester: The movable, if one exists, being used for mobility checks to see what tiles it can reach
     * * access: A list that decides if we can gain access to doors that would otherwise block a turf
     * * simulated_only: Do we only worry about turfs with simulated atmos, most notably things that aren't space?
     * * no_id: When true, doors with public access will count as impassible
    */
    // tg: code/game/turfs/turf.dm:775
    public virtual object reachableAdjacentTurfs(atom_movable requester = null, DmList access = null, object simulated_only = null, float? no_id = null)
    {
        Dm.NotTranslated("code/game/turfs/turf.dm:775", "reachableAdjacentTurfs: pathfinding (LinkBlockedWithAccess, bots and mob AI) is translated in M2 or later");
        return null;
    }

    // tg: code/game/turfs/turf.dm:788
    public virtual object GetHeatCapacity()
    {
        object dot = null;
        dot = heat_capacity;
        return dot;
    }

    // tg: code/game/turfs/turf.dm:791
    public virtual object GetTemperature()
    {
        object dot = null;
        dot = temperature;
        return dot;
    }

    // tg: code/game/turfs/turf.dm:794
    public virtual void TakeTemperature(object temp = null)
    {
        temperature += Dm.num(temp);
    }

    // I'm sorry, this is the only way that both makes sense and is cheap
    // tg: code/game/turfs/turf.dm:798
    public override void set_explosion_block(object explosion_block = null)
    {
        explosive_resistance -= Dm.num(get_explosive_block());
        inherent_explosive_resistance = Dm.num(explosion_block);
        explosive_resistance += Dm.num(get_explosive_block());
    }

    // tg: code/game/turfs/turf.dm:803  apply_main_material_effects(datum/material/main_material, amount, multiplier): not
    // declared, its root /atom/proc/apply_main_material_effects (game/atom/atom_materials.dm) is translated with materials
    // (M5).
    // tg: code/game/turfs/turf.dm:810  remove_main_material_effects(datum/material/custom_material, amount, multiplier): not
    // declared, as apply_main_material_effects.

    /// Returns whether it is safe for an atom to move across this turf
    // tg: code/game/turfs/turf.dm:821
    public virtual object can_cross_safely(atom_movable crossing = null)
    {
        return Dm.num(!HAS_TRAIT(this, TRAIT_AI_AVOID_TURF));
    }

    /**
     * the following are some fishing-related optimizations to shave off as much
     * time we spend implementing the fishing as possible, even if that means
     * hackier code, because we've hundreds of turfs like lava, water etc every round,
     */
    // tg: code/game/turfs/turf.dm:829
    public virtual void add_lazy_fishing(object fish_source_path = null)
    {
        RegisterSignal(this, COMSIG_FISHING_ROD_CAST, nameof(add_fishing_spot_comp));
        RegisterSignal(this, COMSIG_NPC_FISHING, nameof(on_npc_fishing));
        RegisterSignal(this, COMSIG_FISH_RELEASED_INTO, nameof(on_fish_release_into));
        RegisterSignal(this, COMSIG_TURF_CHANGE, nameof(remove_lazy_fishing));
        ADD_TRAIT(this, TRAIT_FISHING_SPOT, INNATE_TRAIT);
        fish_source = fish_source_path;
    }

    // tg: code/game/turfs/turf.dm:837
    [SignalHandler]
    public virtual void remove_lazy_fishing()
    {
        UnregisterSignal(this, Dm.list(
            COMSIG_FISHING_ROD_CAST,
            COMSIG_NPC_FISHING,
            COMSIG_FISH_RELEASED_INTO,
            COMSIG_ATOM_TOOL_ACT(TOOL_MULTITOOL),
            COMSIG_TURF_CHANGE));
        REMOVE_TRAIT(this, TRAIT_FISHING_SPOT, INNATE_TRAIT);
        fish_source = null;
    }

    // tg: code/game/turfs/turf.dm:849
    [SignalHandler]
    public virtual object add_fishing_spot_comp(datum source = null, obj_item_fishing_rod rod = null, mob user = null, params object[] __extra)
    {
        datum_component_fishing_spot spot = Waiting.NoSleep(source.AddComponent(typeof(datum_component_fishing_spot), GLOB.preset_fish_sources[fish_source])) as datum_component_fishing_spot;
        remove_lazy_fishing();
        return spot.handle_cast(source, rod, user);
    }

    // tg: code/game/turfs/turf.dm:855
    [SignalHandler]
    public virtual void on_npc_fishing(datum source = null, DmList fish_spot_container = null)
    {
        fish_spot_container[NPC_FISHING_SPOT] = GLOB.preset_fish_sources[fish_source];
    }

    // tg: code/game/turfs/turf.dm:859
    [SignalHandler]
    public virtual void on_fish_release_into(datum source = null, obj_item_fish fish = null, mob_living releaser = null)
    {
        Waiting.NoSleep(((datum_fish_source)GLOB.preset_fish_sources[fish_source]).readd_fish(this, fish, releaser));
    }

    // tg: code/game/turfs/turf.dm:863
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(fish_source) || !HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISHING_SPOT))
            return dot;
        if (!Dm.truthy(((datum_fish_source)GLOB.preset_fish_sources[fish_source]).has_known_fishes(this)))
            return dot;
        Dm.append(ref dot, span_tinynoticeital(I18n.T("This is a fishing spot. You can look again to list its fishes...")));
        return dot;
    }

    // tg: code/game/turfs/turf.dm:871
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISHING_SPOT) || !Dm.truthy(fish_source))
            return dot;
        await ((datum_fish_source)GLOB.preset_fish_sources[fish_source]).get_catchable_fish_names(user, this, dot as DmList);
        return dot;
    }

    // tg: code/game/turfs/turf.dm:877  (M5x slice 1: explosions; set waitfor = FALSE, inherited: the definition cannot sleep, a
    // plain method)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        object dot = null;
        dot = base.ex_act(severity, target, __extra);
        CeilingSystem.Of(World.Current).ExAct((int)x, (int)y, (int)z, Dm.num(severity), target); // D295: the floor's odds on the ceiling
        if (!Dm.truthy(fish_source))
            return dot;
        ((datum_fish_source)GLOB.preset_fish_sources[fish_source]).spawn_reward_from_explosion(this, severity);
        return dot;
    }

    // tg: code/game/turfs/turf.dm:883
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(fish_source) || !istype((tool as obj_item_multitool)?.buffer, typeof(obj_machinery_fishing_portal_generator)))
            return await base.multitool_act(user, tool);
        obj_machinery_fishing_portal_generator portal = (obj_machinery_fishing_portal_generator)((obj_item_multitool)tool).buffer;
        return portal.link_fishing_spot((datum_fish_source)GLOB.preset_fish_sources[fish_source], this, user);
    }

    // tg: code/game/turfs/turf.dm:803
    public override async DmTask apply_main_material_effects(datum_material main_material = null, object amount = null, object multiplier = null)
    {
        await base.apply_main_material_effects(main_material, amount, multiplier);
        if (alpha < 255)
        {
            ADD_TURF_TRANSPARENCY(this, MATERIAL_SOURCE(main_material));
            await main_material.setup_glow(this);
        }
        rust_resistance = main_material.mat_rust_resistance;
    }

    // tg: code/game/turfs/turf.dm:810
    public override async DmTask remove_main_material_effects(datum_material main_material = null, object amount = null, object multipier = null)
    {
        await base.remove_main_material_effects(main_material, amount, multipier);
        rust_resistance = Dm.initial(this, (turf t) => t.rust_resistance);
        if (alpha == 255)
            return;
        REMOVE_TURF_TRANSPARENCY(this, MATERIAL_SOURCE(main_material));
        // yeets glow
        UnregisterSignal(SSdcs, COMSIG_STARLIGHT_COLOR_CHANGED);
        await set_light(0, 0, null);
    }
}

public sealed partial class TgState
{
    // tg: code/game/turfs/turf.dm:470  /turf/proc/assemble_baseturfs: var/static/list/created_baseturf_lists = list()
    internal DmList turf__assemble_baseturfs__created_baseturf_lists = Dm.list();
}
