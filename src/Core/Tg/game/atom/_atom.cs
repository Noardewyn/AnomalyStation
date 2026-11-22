// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/_atom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of docs/ai/specs/m0-reading/atom.md section 11: the procs that need modules of later
// milestones (projectiles, crafting, chemistry, shuttles, the click and stat panel UI) are Dm.NotTranslated with the
// milestone named. The W4.0 contracts completed CanPass, CanAllowThrough and has_gravity once the members of
// /atom/movable, /turf and /area that they read were declared (wave 4).

/**
 * The base type for nearly all physical objects in SS13

 * Lots and lots of functionality lives here, although in general we are striving to move
 * as much as possible to the components/elements system
 */
// tg: code/game/atom/_atom.dm:7
// The [DmPath] of a BYOND builtin type is on its engine part in src/Core/Tg/Builtin/.
public partial class atom : datum
{
    // tg: code/game/atom/_atom.dm:8
    public atom()
    {
        abstract_type = typeof(atom);
        layer = ABOVE_NORMAL_TURF_LAYER;
        plane = GAME_PLANE;
        appearance_flags = Dm.bitor(TILE_BOUND, LONG_GLIDE);
    }

    /// pass_flags that we are. If any of this matches a pass_flag on a moving thing, by default, we let them through.
    // tg: code/game/atom/_atom.dm:14
    public float pass_flags_self = NONE;

    ///First atom flags var
    // tg: code/game/atom/_atom.dm:17
    public float? flags_1 = NONE;
    ///Intearaction flags
    // tg: code/game/atom/_atom.dm:19
    public float interaction_flags_atom = NONE;

    // tg: code/game/atom/_atom.dm:21
    public float flags_ricochet = NONE;

    ///When a projectile tries to ricochet off this atom, the projectile ricochet chance is multiplied by this
    // tg: code/game/atom/_atom.dm:24
    public float receive_ricochet_chance_mod = 1;
    ///When a projectile ricochets off this atom, it deals the normal damage * this modifier to this atom
    // tg: code/game/atom/_atom.dm:26
    public float receive_ricochet_damage_coeff = 0.33f;

    ///Reagents holder
    // tg: code/game/atom/_atom.dm:29  (object, rules 3.2: subtypes set a typepath that Initialize replaces with the holder,
    // modules/plumbing/plumbers/simple_machines.dm:20)
    public object reagents = null;

    ///all of this atom's HUD (med/sec, etc) images. Associative list of the form: list(hud category = hud image or images for that category).
    ///most of the time hud category is associated with a single image, sometimes its associated with a list of images.
    ///not every hud in this list is actually used. for ones available for others to see, look at active_hud_list.
    // tg: code/game/atom/_atom.dm:34
    public DmList hud_list = null;
    ///all of this atom's HUD images which can actually be seen by players with that hud
    // tg: code/game/atom/_atom.dm:36
    public DmList active_hud_list = null;
    ///HUD images that this atom can provide.
    // tg: code/game/atom/_atom.dm:38
    public DmList hud_possible;

    ///How much this atom resists explosions by, in the end
    // tg: code/game/atom/_atom.dm:41
    public float explosive_resistance = 0;

    ///vis overlays managed by SSvis_overlays to automaticaly turn them like other overlays.
    // tg: code/game/atom/_atom.dm:44
    public DmList managed_vis_overlays;

    /// Lazylist of all images (or atoms, I'm sorry) (hopefully attached to us) to update when we change z levels
    /// You will need to manage adding/removing from this yourself, but I'll do the updating for you
    // tg: code/game/atom/_atom.dm:48
    public DmList update_on_z;

    /// Lazylist of all overlays attached to us to update when we change z levels
    /// You will need to manage adding/removing from this yourself, but I'll do the updating for you
    /// Oh and note, if order of addition is important this WILL break that. so mind yourself
    // tg: code/game/atom/_atom.dm:53
    public DmList update_overlays_on_z;

    ///Cooldown tick timer for buckle messages
    // tg: code/game/atom/_atom.dm:56
    public float buckle_message_cooldown = 0;
    ///Last fingerprints to touch this atom
    // tg: code/game/atom/_atom.dm:58
    public object fingerprintslast;

    /// Radiation insulation types
    // tg: code/game/atom/_atom.dm:61
    public float? rad_insulation = RAD_NO_INSULATION;

    /// The icon state intended to be used for the acid component. Used to override the default acid overlay icon state.
    // tg: code/game/atom/_atom.dm:64
    public object custom_acid_overlay = null;

    // tg: code/game/atom/_atom.dm:66
    public datum_wires wires = null;

    ///Light systems, both shouldn't be active at the same time.
    // tg: code/game/atom/_atom.dm:69
    public float light_system = COMPLEX_LIGHT;
    ///Range of the light in tiles. Zero means no light.
    // tg: code/game/atom/_atom.dm:71
    public float light_range = 0;
    ///Intensity of the light. The stronger, the less shadows you will see on the lit area.
    // tg: code/game/atom/_atom.dm:73
    public float light_power = 1;
    ///Hexadecimal RGB string representing the colour of the light. White by default.
    // tg: code/game/atom/_atom.dm:75
    public string light_color = COLOR_WHITE;
    ///Boolean variable for toggleable lights. Has no effect without the proper light_system, light_range and light_power values.
    // tg: code/game/atom/_atom.dm:77
    public float light_on = TRUE;
    ///Bitflags to determine lighting-related atom properties.
    // tg: code/game/atom/_atom.dm:79
    public float light_flags = NONE;

    // OVERLAY_LIGHT only values
    /// An optional render_source to apply to this atom's light overlay
    // tg: code/game/atom/_atom.dm:83
    public string light_render_source = "";

    // COMPLEX_LIGHT only values
    /// Angle of light to show in light_dir
    /// 360 is a circle, 90 is a cone, etc.
    // tg: code/game/atom/_atom.dm:88
    public float light_angle = 360;
    /// What angle to project light in
    // tg: code/game/atom/_atom.dm:90
    public float light_dir = NORTH;
    /// How many tiles "up" this light is. 1 is typical, should only really change this if it's a floor light
    // tg: code/game/atom/_atom.dm:92
    public float light_height = LIGHTING_HEIGHT;
    ///Our light source. Don't fuck with this directly unless you have a good reason!
    // tg: code/game/atom/_atom.dm:94  (var/tmp)
    public datum_light_source light;
    ///Any light sources that are "inside" of us, for example, if src here was a mob that's carrying a flashlight, that flashlight's light source would be part of this list.
    // tg: code/game/atom/_atom.dm:96  (var/tmp)
    public DmList light_sources;

    /// Last name used to calculate a color for the chatmessage overlays
    // tg: code/game/atom/_atom.dm:99
    public object chat_color_name;
    /// Last color calculated for the the chatmessage overlays
    // tg: code/game/atom/_atom.dm:101
    public object chat_color;
    /// A luminescence-shifted value of the last color calculated for chatmessage overlays
    // tg: code/game/atom/_atom.dm:103
    public object chat_color_darkened;

    // Use SET_BASE_PIXEL(x, y) to set these in typepath definitions, it'll handle pixel_x and y for you
    ///Default pixel x shifting for the atom's icon.
    // tg: code/game/atom/_atom.dm:107
    public float base_pixel_x = 0;
    ///Default pixel y shifting for the atom's icon.
    // tg: code/game/atom/_atom.dm:109
    public float base_pixel_y = 0;
    // Use SET_BASE_VISUAL_PIXEL(x, y) to set these in typepath definitions, it'll handle pixel_w and z for you
    ///Default pixel w shifting for the atom's icon.
    // tg: code/game/atom/_atom.dm:112
    public float base_pixel_w = 0;
    ///Default pixel z shifting for the atom's icon.
    // tg: code/game/atom/_atom.dm:114
    public float base_pixel_z = 0;
    ///Used for changing icon states for different base sprites.
    // tg: code/game/atom/_atom.dm:116
    public object base_icon_state;

    ///Icon-smoothing behavior.
    // tg: code/game/atom/_atom.dm:119
    public float smoothing_flags = NONE;
    ///What directions this is currently smoothing with. IMPORTANT: This uses the smoothing direction flags as defined in icon_smoothing.dm, instead of the BYOND flags.
    // tg: code/game/atom/_atom.dm:121
    public object smoothing_junction = null;
    ///What smoothing groups does this atom belongs to, to match canSmoothWith. If null, nobody can smooth with it. Must be sorted.
    // tg: code/game/atom/_atom.dm:123  (object, rules 3.5: types set it to the group text of S_TURF/S_OBJ, which
    // SETUP_SMOOTHING turns into a list)
    public object smoothing_groups = null;
    ///List of smoothing groups this atom can smooth with. If this is null and atom is smooth, it smooths only with itself. Must be sorted.
    // tg: code/game/atom/_atom.dm:125  (object, as smoothing_groups)
    public object canSmoothWith = null;

    ///AI controller that controls this atom. type on init, then turned into an instance during runtime
    // tg: code/game/atom/_atom.dm:128  (object, rules 3.2: a typepath until Initialize, then the controller)
    public object ai_controller;

    /// forensics datum, contains fingerprints, fibres, blood_dna and hiddenprints on this atom
    // tg: code/game/atom/_atom.dm:131
    public datum_forensics forensics = null;
    /// Cached color for all blood on us to avoid doing constant math
    // tg: code/game/atom/_atom.dm:133
    public object cached_blood_color = null;
    /// Cached emissive alpha for all blood on us to avoid doing constant math
    // tg: code/game/atom/_atom.dm:135
    public object cached_blood_emissive = null;

    /// How this atom should react to having its astar blocking checked
    // tg: code/game/atom/_atom.dm:138
    public float can_astar_pass = CANASTARPASS_DENSITY;
    ///whether ghosts can see screentips on it
    // tg: code/game/atom/_atom.dm:140
    public float ghost_screentips = FALSE;

    /// Flags to check for in can_perform_action. Used in alt-click & ctrl-click checks
    // tg: code/game/atom/_atom.dm:143
    public float interaction_flags_click = NONE;
    /// Flags to check for in can_perform_action for mouse drag & drop checks. To bypass checks see interaction_flags_atom mouse drop flags
    // tg: code/game/atom/_atom.dm:145
    public float interaction_flags_mouse_drop = NONE;

    /// Generally for niche objects, atoms blacklisted can spawn if enabled by spawner.
    // tg: code/game/atom/_atom.dm:148
    public float spawn_blacklisted = FALSE;

    /// What color this shows up as on the tactical map
    // tg: code/game/atom/_atom.dm:151
    public string tacmap_color = TACMAP_SOLID;

    /**
     * Top level of the destroy chain for most atoms
     *
     * Cleans up the following:
     * * Removes alternate apperances from huds that see them
     * * qdels the reagent holder from atoms if it exists
     * * clears the orbiters list
     * * clears overlays and priority overlays
     * * clears the light object
     */
    // tg: code/game/atom/_atom.dm:163
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(alternate_appearances))
        {
            foreach (object current_alternate_appearance in Dm.each(alternate_appearances))
            {
                datum_atom_hud_alternate_appearance selected_alternate_appearance = alternate_appearances[current_alternate_appearance] as datum_atom_hud_alternate_appearance;
                selected_alternate_appearance.remove_atom_from_hud(this);
            }
        }

        if (Dm.truthy(reagents))
            QDEL_NULL(ref reagents);

        if (Dm.truthy(forensics))
            QDEL_NULL(ref forensics);

        if (Dm.truthy(atom_storage))
            QDEL_NULL(ref atom_storage);

        if (Dm.truthy(wires))
            QDEL_NULL(ref wires);

        orbiters = null; // The component is attached to us normaly and will be deleted elsewhere

        // Checking length(overlays) before cutting has significant speed benefits
        if (Dm.truthy(length(overlays)))
            overlays.Cut();

        LAZYNULL(ref managed_overlays);
        // GAP(D112, the mob AI milestone): an inert atom never ran /atom/Initialize, which makes its ai_controller type path an
        // instance (atoms_initializing_EXPENSIVE.dm:154-155), so there is nothing to qdel yet.
        if (ispath(ai_controller) && InertSeam.IsInert(this))
            ai_controller = null;
        if (Dm.truthy(ai_controller))
            QDEL_NULL(ref ai_controller);
        if (Dm.truthy(light))
            QDEL_NULL(ref light);
        if (Dm.truthy(length(light_sources)))
            light_sources.Cut();

        if (Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_QUEUED)))
            SSicon_smooth.remove_from_queues(this);

        // tg: #ifndef DISABLE_DREAMLUAU ... #endif not translated (inactive in production)

        return base.Destroy(force);
    }

    // tg: code/game/atom/_atom.dm:209
    public virtual async DmTask<object> handle_ricochet(obj_projectile ricocheting_projectile = null)
    {
        // The M7 projectiles slice
        turf p_turf = get_turf(ricocheting_projectile);
        float face_direction = (get_dir(this, p_turf) is var or__1 && Dm.truthy(or__1) ? or__1 : get_dir(this, ricocheting_projectile));
        object face_angle = dir2angle(face_direction);
        float incidence_s = GET_ANGLE_OF_INCIDENCE(Dm.num(face_angle), (Dm.num(ricocheting_projectile.angle) + 180));
        float a_incidence_s = abs(incidence_s);
        if (a_incidence_s > 90 && a_incidence_s < 270)
            return FALSE;
        if ((Dm.@in(ricocheting_projectile.armor_flag, Dm.list(BULLET, BOMB))) && Dm.truthy(ricocheting_projectile.ricochet_incidence_leeway))
        {
            if ((a_incidence_s < 90 && a_incidence_s < 90 - ricocheting_projectile.ricochet_incidence_leeway) || (a_incidence_s > 270 && a_incidence_s - 270 > ricocheting_projectile.ricochet_incidence_leeway))
                return FALSE;
        }
        float new_angle_s = SIMPLIFY_DEGREES(Dm.num(face_angle) + incidence_s);
        ricocheting_projectile.set_angle(new_angle_s);
        return TRUE;
    }

    /// Whether the mover object can avoid being blocked by this atom, while arriving from (or leaving through) the border_dir.
    // tg: code/game/atom/_atom.dm:225
    [ShouldCallParent, ShouldBePure]
    public virtual async DmTask<object> CanPass(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_TRIED_PASS]) ? NONE : this._SendSignal(COMSIG_ATOM_TRIED_PASS, Dm.list(this, mover, border_dir))), COMSIG_COMPONENT_PERMIT_PASSAGE)))
            return TRUE;
        if (Dm.truthy(Dm.bitand(mover.movement_type, PHASING)))
            return TRUE;
        dot = await CanAllowThrough(mover, border_dir);
        // This is cheaper than calling the proc every time since most things dont override CanPassThrough
        if (!Dm.truthy(mover.generic_canpass))
            return mover.CanPassThrough(this, REVERSE_DIR(Dm.num(border_dir)), dot);
        return dot;
    }

    /**
     * Returns true or false to allow the mover to move through src
     * @params
     * 	mover: The mob trying to move into this atom.
     * 	border_dir: Typically the direction that mover has in relation to src.
     */
    // tg: code/game/atom/_atom.dm:243
    [ShouldCallParent]
    public virtual async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        //SHOULD_BE_PURE(TRUE)
        if (Dm.truthy(Dm.bitand(mover.pass_flags, pass_flags_self)))
            return TRUE;
        if (Dm.truthy(mover.throwing) && Dm.truthy(Dm.bitand(pass_flags_self, LETPASSTHROW)))
            return TRUE;
        return Dm.not(density);
    }

    /**
     * Is this atom currently located on centcom (or riding off into the sunset on a shuttle)
     *
     * Specifically, is it on the z level and within the centcom areas.
     * You can also be in a shuttle during endgame transit.
     *
     * Used in gamemode to identify mobs who have escaped and for some other areas of the code
     * who don't want atoms where they shouldn't be
     *
     * Returns TRUE if this atom is on centcom or an escape shuttle, or FALSE if not
     */
    // tg: code/game/atom/_atom.dm:263
    public virtual object onCentCom()
    {
        turf current_turf = get_turf(this);
        if (current_turf == null)
            return FALSE;

        // This doesn't necessarily check that we're at central command,
        // but it checks for any shuttles which have finished are still in hyperspace
        // (IE, stuff like the whiteship which fly off into the sunset and "escape")
        if (Dm.truthy(is_reserved_level(current_turf.z)))
        {
            // DEVIATION(DV190): CentCom's docks stand in the reserved level, so a shuttle that has escaped to one of them
            // (ENDGAME_LAUNCHED, which tg checks on the CentCom level below) counts too
            return Dm.truthy(on_escaped_shuttle(ENDGAME_TRANSIT)) || Dm.truthy(on_escaped_shuttle(ENDGAME_LAUNCHED)) ? TRUE : FALSE;
        }

        // From here on we only concern ourselves with people actually on the centcom Z
        if (!Dm.truthy(is_centcom_level(current_turf.z)))
            return FALSE;

        if (current_turf.loc is area_centcom)
            return TRUE;

        // Finally, check if we're on an escaped shuttle
        return on_escaped_shuttle();
    }

    /**
     * Is the atom in any of the syndicate areas
     *
     * Either in the syndie base, or any of their shuttles
     *
     * Also used in gamemode code for win conditions
     *
     * Returns TRUE if this atom is on the syndicate recon base, any of its shuttles, or an escape shuttle, or FALSE if not
     */
    // tg: code/game/atom/_atom.dm:293
    public virtual object onSyndieBase()
    {
        turf current_turf = get_turf(this);
        if (current_turf == null)
            return FALSE;

        // Syndicate base is loaded in a reserved level. If not reserved, we don't care.
        if (!Dm.truthy(is_reserved_level(current_turf.z)))
            return FALSE;

        // var/static/list/syndie_typecache = typecacheof(...): the syndicate base and its shuttles
        if (current_turf.loc is area_centcom_syndicate_mothership or area_shuttle_assault_pod or area_shuttle_syndicate)
            return TRUE;

        // Finally, check if we're on an escaped shuttle
        return on_escaped_shuttle();
    }

    /**
     * Checks that we're on a shuttle that's escaped
     *
     * * check_for_launch_status - What launch status do we check for? Generally the two you want to check for are ENDGAME_LAUNCHED or ENDGAME_TRANSIT
     *
     * Returns TRUE if this atom is on a shuttle which is escaping or has escaped, or FALSE otherwise
     */
    // tg: code/game/atom/_atom.dm:321
    public virtual object on_escaped_shuttle(float? check_for_launch_status = null)
    {
        check_for_launch_status ??= ENDGAME_LAUNCHED;
        turf current_turf = get_turf(this);
        if (current_turf == null)
            return FALSE;

        foreach (obj_docking_port_mobile mobile_docking_port in Dm.each<obj_docking_port_mobile>(SSshuttle.mobile_docking_ports))
        {
            if (!Dm.eq(mobile_docking_port.launch_status, check_for_launch_status))
                continue;
            foreach (area_shuttle shuttle_area in Dm.each<area_shuttle>(mobile_docking_port.shuttle_areas))
            {
                if (shuttle_area == current_turf.loc)
                    return TRUE;
            }
        }

        return FALSE;
    }

    /**
     * Is the atom in an away mission
     *
     * Must be in the away mission z-level to return TRUE
     *
     * Also used in gamemode code for win conditions
     */
    // tg: code/game/atom/_atom.dm:342
    public virtual object onAwayMission()
    {
        Dm.NotTranslated("code/game/atom/_atom.dm:342", "onAwayMission: z-level traits are translated in a later milestone");
        return null;
    }

    /**
     * Called whenever an item is crafted, either via stack recipes or crafting recipes from the crafting menu
     *
     * By default, it just cycles through the list of movables used in the recipe and calls used_in_craft() for each of them,
     * then it either moves them inside the object if they're in the list of parts for the recipe
     * or deletes them if they're not.
     * The proc can be overriden by subtypes, as long as it always call parent.
     */
    // tg: code/game/atom/_atom.dm:360
    [ShouldCallParent]
    public virtual async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        if (isliving(crafter))
        {
            mob_living person = crafter as mob_living;
            if (Dm.truthy(person.mind))
                ADD_TRAIT(this, TRAIT_HANDMADE, REF(person.mind));
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ON_CRAFT]))
            this._SendSignal(COMSIG_ATOM_ON_CRAFT, Dm.list(this, components, current_recipe));

        DmList remaining_parts = LAZYLISTDUPLICATE(current_recipe?.parts);
        DmList parts_by_type = LAZYLISTDUPLICATE(remaining_parts);
        foreach (object parttype in Dm.each(parts_by_type?.Copy())) //necessary for our is_type_in_list() call with the zebra arg set to true
            parts_by_type[parttype] = parttype;
        foreach (object movable__it in Dm.each(components)) // machinery or structure objects in the list are guaranteed to be used up. We only check items.
        {
            atom_movable movable = (atom_movable)movable__it;
            // GAP(DV27, M5 slice 8): a component whose type has its own untranslated used_in_craft (an inert item, D132) is not
            // called rather than running its parent's.
            if (!InertSeam.MissingCraftOverride(movable, "used_in_craft"))
                await movable.used_in_craft(this, current_recipe);
            object matched_type = is_type_in_list(movable, parts_by_type, zebra: TRUE);
            if (!Dm.truthy(matched_type))
                continue;

            if (isliving(movable.loc) && isitem(movable))
            {
                mob_living living = movable.loc as mob_living;
                await living.transferItemToLoc(movable as obj_item, this);
            }
            else
                await movable.forceMove(this);

            if (Dm.truthy(matched_type))
            {
                Dm.remove_at(remaining_parts, matched_type, 1);
                if (Dm.num(remaining_parts[matched_type]) <= 0)
                    remaining_parts.Remove(matched_type);
            }
        }
    }

    ///Take air from the passed in gas mixture datum
    // tg: code/game/atom/_atom.dm:392
    public virtual async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        return null;
    }

    ///Remove air from this atom
    // tg: code/game/atom/_atom.dm:396
    public virtual object remove_air(object amount = null)
    {
        return null;
    }

    ///Return the current air environment in this atom
    // tg: code/game/atom/_atom.dm:400
    public virtual async DmTask<object> return_air()
    {
        // RULE-GAP: DM loc?.return_air() calls a DmTask slot; C# cannot await the null of a ?. call, so the null test is
        // written out (loc is read twice, a plain read).
        return (loc == null ? null : await loc.return_air());
    }

    ///Return the air if we can analyze it
    // tg: code/game/atom/_atom.dm:404
    public virtual object return_analyzable_air()
    {
        return null;
    }

    // tg: code/game/atom/_atom.dm:407
    // set waitfor = FALSE: this body cannot sleep (a signal handler never sleeps), so it needs no wrapper
    // (waiting-model.md 5.4); the overrides that may sleep get one.
    public virtual object Bumped(atom_movable bumped_atom = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_BUMPED]))
            this._SendSignal(COMSIG_ATOM_BUMPED, Dm.list(this, bumped_atom));
        return null;
    }

    /// Convenience proc to see if a container is open for chemistry handling
    // tg: code/game/atom/_atom.dm:412
    public virtual object is_open_container()
    {
        return (is_refillable() is var and__1 && !Dm.truthy(and__1) ? and__1 : is_drainable());
    }

    /// Is this atom injectable into other atoms
    // tg: code/game/atom/_atom.dm:416
    public virtual object is_injectable(params object[] __extra)
    {
        return (reagents is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.bitand(((datum_reagents)reagents).flags, Dm.bitor(INJECTABLE, REFILLABLE)));
    }

    /// Can we draw from this atom with an injectable atom
    // tg: code/game/atom/_atom.dm:420
    public virtual object is_drawable(params object[] __extra)
    {
        return (reagents is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.bitand(((datum_reagents)reagents).flags, Dm.bitor(DRAWABLE, DRAINABLE)));
    }

    /// Can this atoms reagents be refilled
    // tg: code/game/atom/_atom.dm:424
    public virtual object is_refillable()
    {
        return (reagents is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.bitand(((datum_reagents)reagents).flags, REFILLABLE));
    }

    /// Is this atom drainable of reagents
    // tg: code/game/atom/_atom.dm:428
    public virtual object is_drainable()
    {
        return (reagents is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.bitand(((datum_reagents)reagents).flags, DRAINABLE));
    }

    /// Can we dunk stuff into this container?
    // tg: code/game/atom/_atom.dm:432
    public virtual object is_dunkable()
    {
        return (reagents is var and__1 && !Dm.truthy(and__1) ? and__1 : Dm.bitand(((datum_reagents)reagents).flags, DUNKABLE));
    }

    /** Handles exposing this atom to a list of reagents.
     *
     * Sends COMSIG_ATOM_EXPOSE_REAGENTS
     * Calls expose_atom() for every reagent in the reagent list.
     *
     * Arguments:
     * - [reagents][/list]: The list of reagents the atom is being exposed to.
     * - [source][/datum/reagents]: The reagent holder the reagents are being sourced from.
     * - methods: How the atom is being exposed to the reagents. Bitflags.
     * - show_message: Whether to display anything to mobs when they are exposed.
     */
    // tg: code/game/atom/_atom.dm:446
    public virtual async DmTask<object> expose_reagents(DmList reagents = null, datum_reagents source = null, float? methods = null, float? show_message = null, params object[] __extra)
    {
        methods ??= TOUCH;
        show_message ??= TRUE;
        object dot = !Dm.truthy(_listen_lookup?[COMSIG_ATOM_EXPOSE_REAGENTS]) ? NONE : _SendSignal(COMSIG_ATOM_EXPOSE_REAGENTS, Dm.list(this, reagents, source, methods, show_message));
        if (Dm.truthy(Dm.bitand(dot, COMPONENT_NO_EXPOSE_REAGENTS)))
            return dot;

        foreach (datum_reagent current_reagent in Dm.each_cast<datum_reagent>(reagents))
            dot = Dm.bitor(dot, await current_reagent.expose_atom(this, reagents[current_reagent], methods));
        return dot;
    }

    /// Are you allowed to drop stuff inside this atom
    // tg: code/game/atom/_atom.dm:455
    public virtual object AllowDrop()
    {
        return FALSE;
    }

    ///Is this atom within 1 tile of another atom
    // tg: code/game/atom/_atom.dm:459
    public virtual async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        return null;
    }

    /// has a previously nearby atom moved away
    // tg: code/game/atom/_atom.dm:463
    public virtual async DmTask OnProximityExit(atom_movable proximity_check_mob = null)
    {
        return;
    }

    /// Sets the wire datum of an atom
    // tg: code/game/atom/_atom.dm:467
    public virtual void set_wires(datum_wires new_wires = null)
    {
        wires = new_wires;
    }

    ///Return true if we're inside the passed in atom
    // tg: code/game/atom/_atom.dm:471
    public virtual object in_contents_of(object container = null)//can take class or object instance as argument
    {
        if (ispath(container))
        {
            if (istype(this.loc, container as Type))
                return TRUE;
        }
        else if (Dm.@in(this, container))
            return TRUE;
        return FALSE;
    }

    /**
     * Checks the atom's loc and calls update_held_items on it if it is a mob.
     *
     * This should only be used in situations when you are unable to use /datum/element/update_icon_updates_onmob for whatever reason.
     * Check code/datums/elements/update_icon_updates_onmob.dm before using this. Adding that to the atom and calling update_appearance will work for most cases.
     *
     * Arguments:
     * * mob/target - The mob to update the icons of. Optional argument, use if the atom's loc is not the mob you want to update.
     */
    // tg: code/game/atom/_atom.dm:488
    [ShouldCallParent]
    public virtual async DmTask update_inhand_icon(mob target = null)
    {
        target ??= loc as mob; // the DM default target = loc, a non-mob is refused by the istype below
        if (target == null)
            return;

        await target.update_held_items();

        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_UPDATE_INHAND_ICON]))
            _SendSignal(COMSIG_ATOM_UPDATE_INHAND_ICON, Dm.list(this, target));
    }

    /**
     * An atom we are buckled or is contained within us has tried to move
     *
     * Default behaviour is to send a warning that the user can't move while buckled as long
     * as the [buckle_message_cooldown][/atom/var/buckle_message_cooldown] has expired (50 ticks)
     */
    // tg: code/game/atom/_atom.dm:503
    public virtual async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_RELAYMOVE]) ? NONE : this._SendSignal(COMSIG_ATOM_RELAYMOVE, Dm.list(this, user, direction))), COMSIG_BLOCK_RELAYMOVE)))
            return null;
        if (buckle_message_cooldown <= world.time)
        {
            buckle_message_cooldown = world.time + 25;
            balloon_alert(user, I18n.T("can't move while buckled!"));
        }
        return null;
    }

    /**
     * A special case of relaymove() in which the person relaying the move may be "driving" this atom
     *
     * This is a special case for vehicles and ridden animals where the relayed movement may be handled
     * by the riding component attached to this atom. Returns TRUE as long as there's nothing blocking
     * the movement, or FALSE if the signal gets a reply that specifically blocks the movement
     */
    // tg: code/game/atom/_atom.dm:518
    public virtual object relaydrive(mob_living user = null, object direction = null)
    {
        return Dm.not(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_RIDDEN_DRIVER_MOVE]) ? NONE : this._SendSignal(COMSIG_RIDDEN_DRIVER_MOVE, Dm.list(this, user, direction))), COMPONENT_DRIVER_BLOCK_MOVE));
    }
}

// tg: code/game/atom/_atom.dm:522 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    ///returns the mob's dna info as a list, to be inserted in an object's blood_DNA list
    // tg: code/game/atom/_atom.dm:522
    public virtual object get_blood_dna_list()
    {
        // M2 P2: translated (an organ's insertion copies its owner's blood DNA, organ_movement.dm:78).
        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (blood_type == null)
            return null;

        return Dm.list(Dm.kv(blood_type.dna_string, blood_type));
    }
}

// tg: code/game/atom/_atom.dm:530
public partial class mob_living_carbon : mob_living
{
    ///Get the mobs dna list
    // tg: code/game/atom/_atom.dm:530
    public override object get_blood_dna_list()
    {
        // M2 P2: translated (an organ's insertion copies its owner's blood DNA, organ_movement.dm:78).
        datum_blood_type blood_type = get_bloodtype() as datum_blood_type;
        if (blood_type == null)
            return null;

        if (Dm.truthy(dna?.unique_enzymes))
            return Dm.list(Dm.kv(dna.unique_enzymes, blood_type));
        return Dm.list(Dm.kv(blood_type.dna_string, blood_type));
    }
}

// tg: code/game/atom/_atom.dm:539
public partial class mob_living_silicon : mob_living
{
    // tg: code/game/atom/_atom.dm:539
    public override object get_blood_dna_list()
    {
        return null;
    }
}

// tg: code/game/atom/_atom.dm:543
public partial class atom : datum
{
    ///Is this atom in space
    // tg: code/game/atom/_atom.dm:543
    public virtual object isinspace()
    {
        if (isspaceturf(get_turf(this)))
            return TRUE;
        else
            return FALSE;
    }

    /**
     * If someone's trying to dump items onto our atom, where should they be dumped to?
     *
     * Return a loc to place objects, or null to stop dumping.
     */
    // tg: code/game/atom/_atom.dm:554
    public virtual object get_dumping_location()
    {
        return null;
    }

    /**
     * the vision impairment to give to the mob whose perspective is set to that atom
     *
     * (e.g. an unfocused camera giving you an impaired vision when looking through it)
     */
    // tg: code/game/atom/_atom.dm:562
    public virtual void get_remote_view_fullscreens(mob user = null)
    {
        return;
    }

    /**
     * the sight changes to give to the mob whose perspective is set to that atom
     *
     * (e.g. A mob with nightvision loses its nightvision while looking through a normal camera)
     */
    // tg: code/game/atom/_atom.dm:570
    public virtual object update_remote_sight(mob_living user = null, DmList new_sight = null)
    {
        return null;
    }


    /**
     * Hook for running code when a dir change occurs
     *
     * Not recommended to use, listen for the [COMSIG_ATOM_DIR_CHANGE] signal instead (sent by this proc)
     */
    // tg: code/game/atom/_atom.dm:579
    [ShouldCallParent]
    public virtual async DmTask setDir(object newdir = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_PRE_DIR_CHANGE]) ? NONE : this._SendSignal(COMSIG_ATOM_PRE_DIR_CHANGE, Dm.list(this, dir, newdir))), COMPONENT_ATOM_BLOCK_DIR_CHANGE)))
        {
            newdir = dir;
            return;
        }
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_DIR_CHANGE]))
            this._SendSignal(COMSIG_ATOM_DIR_CHANGE, Dm.list(this, dir, newdir));
        float oldDir = dir;
        dir = Dm.num(newdir);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_POST_DIR_CHANGE]))
            this._SendSignal(COMSIG_ATOM_POST_DIR_CHANGE, Dm.list(this, oldDir, newdir));
        if (Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_BORDER_OBJECT)))
            QUEUE_SMOOTH_NEIGHBORS(this);
    }

    /**
     * Wash this atom
     *
     * This will clean it off any temporary stuff like blood. Override this in your item to add custom cleaning behavior.
     * Returns true if any washing was necessary and thus performed
     * Arguments:
     * * clean_types: any of the CLEAN_ constants
     * Returns: A bitflag if it successfully cleaned something: e.g. COMPONENT_CLEANED, or NONE if not. COMPONENT_CLEANED_GAIN_XP being flipped on signals whether the cleaning should yield cleaning xp.
     */
    // tg: code/game/atom/_atom.dm:600
    [ShouldCallParent]
    public virtual async DmTask<object> wash(object clean_types = null, params object[] __extra)
    {
        // SHOULD_CALL_PARENT(TRUE)
        object dot = !Dm.truthy(_listen_lookup?[COMSIG_COMPONENT_CLEAN_ACT]) ? NONE : _SendSignal(COMSIG_COMPONENT_CLEAN_ACT, Dm.list(this, clean_types));
        // Basically "if has washable coloration"
        if (length(atom_colours) >= WASHABLE_COLOUR_PRIORITY && Dm.truthy(atom_colours[WASHABLE_COLOUR_PRIORITY]))
        {
            await remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
            dot = Dm.bitor(dot, Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP));
        }
        return dot;
    }

    ///Where atoms should drop if taken from this atom
    // tg: code/game/atom/_atom.dm:609
    public virtual object drop_location()
    {
        atom location = loc;
        if (!Dm.truthy(location))
            return null;
        return (Dm.truthy(location.AllowDrop()) ? (object)location : location.drop_location());
    }

    /**
     * An atom has entered this atom's contents
     *
     * Default behaviour is to send the [COMSIG_ATOM_ENTERED]
     */
    // tg: code/game/atom/_atom.dm:620
    public virtual async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ENTERED]))
            this._SendSignal(COMSIG_ATOM_ENTERED, Dm.list(this, arrived, old_loc, old_locs));
        if (Dm.truthy(arrived._listen_lookup?[COMSIG_ATOM_ENTERING]))
            arrived._SendSignal(COMSIG_ATOM_ENTERING, Dm.list(arrived, this, old_loc, old_locs));
        return null;
    }

    /**
     * An atom is attempting to exit this atom's contents
     *
     * Default behaviour is to send the [COMSIG_ATOM_EXIT]
     */
    // tg: code/game/atom/_atom.dm:629
    public virtual async DmTask<object> Exit(atom_movable leaving = null, object direction = null)
    {
        // Don't call `..()` here, otherwise `Uncross()` gets called.
        // See the doc comment on `Uncross()` to learn why this is bad.

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXIT]) ? NONE : this._SendSignal(COMSIG_ATOM_EXIT, Dm.list(this, leaving, direction))), COMPONENT_ATOM_BLOCK_EXIT)))
            return FALSE;

        return TRUE;
    }

    /**
     * An atom has exited this atom's contents
     *
     * Default behaviour is to send the [COMSIG_ATOM_EXITED]
     */
    // tg: code/game/atom/_atom.dm:643
    public virtual async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_EXITED]))
            this._SendSignal(COMSIG_ATOM_EXITED, Dm.list(this, gone, direction));
        if (Dm.truthy(gone._listen_lookup?[COMSIG_ATOM_EXITING]))
            gone._SendSignal(COMSIG_ATOM_EXITING, Dm.list(gone, this, direction));
        return null;
    }

    ///Return atom temperature
    // tg: code/game/atom/_atom.dm:648
    public virtual async DmTask<object> return_temperature()
    {
        return null;
    }

    // tg: code/game/atom/_atom.dm:651
    public virtual async DmTask process_recipes(mob_living user = null, obj_item processed_object = null, DmList processing_recipes = null)
    {
        //Only one recipe? use the first
        if (processing_recipes.len == 1)
        {
            await StartProcessingAtom(user, processed_object, processing_recipes[1] as DmList);
            return;
        }
        //Otherwise, select one with a radial
        await ShowProcessingGui(user, processed_object, processing_recipes);
    }

    ///Creates the radial and processes the selected option
    // tg: code/game/atom/_atom.dm:660
    public virtual async DmTask ShowProcessingGui(mob_living user = null, obj_item processed_object = null, DmList possible_options = null)
    {
        DmList choices_to_options = Dm.list(); //Dict of object name | dict of object processing settings
        DmList choices = Dm.list();

        foreach (object current_option__it in Dm.each(possible_options))
        {
            DmList current_option = (DmList)current_option__it;
            Type current_option_type = current_option[TOOL_PROCESSING_RESULT] as Type;
            choices_to_options[Dm.initial(current_option_type, (atom a) => a.name)] = current_option;
            object option_image = image(icon: Dm.initial(current_option_type, (atom a) => a.icon), icon_state: Dm.initial(current_option_type, (atom a) => a.icon_state));
            Dm.append(ref choices, Dm.list(Dm.kv(Dm.text("[]", Dm.initial(current_option_type, (atom a) => a.name)), option_image)));
        }

        object pick = await show_radial_menu(user, this, choices, radius: 36, require_near: TRUE);
        if (!Dm.truthy(pick))
            return;

        await StartProcessingAtom(user, processed_object, choices_to_options[pick] as DmList);
    }


    // tg: code/game/atom/_atom.dm:677
    public virtual async DmTask StartProcessingAtom(mob_living user = null, obj_item process_item = null, DmList chosen_option = null)
    {
        object processing_time = chosen_option[TOOL_PROCESSING_TIME];
        object sound_to_play = chosen_option[TOOL_PROCESSING_SOUND];
        to_chat(user, span_notice(I18n.T("You start working on [].", this)));
        if (Dm.truthy(sound_to_play))
            playsound(this, sound_to_play, 50, TRUE);
        if (!Dm.truthy(await process_item.use_tool(this, user, processing_time, volume: 50)))
            return;
        Type atom_to_create = chosen_option[TOOL_PROCESSING_RESULT] as Type;
        DmList created_atoms = Dm.list();
        object amount_to_create = chosen_option[TOOL_PROCESSING_AMOUNT];
        for (float i = 1, i__end = Dm.num(amount_to_create); i <= i__end; i++)
        {
            atom created_atom = Dm.@new(atom_to_create, drop_location()) as atom;
            await created_atom.OnCreatedFromProcessing(user, process_item, chosen_option, this);
            if (Dm.truthy(custom_materials) || IS_EDIBLE(this)) //materials are ALWAYS inherited for food, even if the source has none
                await created_atom.set_custom_materials(custom_materials, 1 / Dm.num(amount_to_create));
            created_atom.pixel_x = pixel_x;
            created_atom.pixel_y = pixel_y;
            if (i > 1)
            {
                created_atom.pixel_x += rand(-8, 8);
                created_atom.pixel_y += rand(-8, 8);
            }
            Dm.append(ref created_atoms, created_atom);
        }
        to_chat(user, span_notice(I18n.T("You manage to create [] [] from [].", amount_to_create, Dm.eq(Dm.initial(atom_to_create, (atom a) => a.gender), PLURAL) ? Dm.text("[]", Dm.initial(atom_to_create, (atom a) => a.name)) : Dm.text("[][]", Dm.initial(atom_to_create, (atom a) => a.name), plural_s(Dm.initial(atom_to_create, (atom a) => a.name))), this)));
        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_PROCESSED]))
            _SendSignal(COMSIG_ATOM_PROCESSED, Dm.list(this, user, process_item, created_atoms));
        await UsedforProcessing(user, process_item, chosen_option, created_atoms);
    }

    // tg: code/game/atom/_atom.dm:703
    public virtual async DmTask UsedforProcessing(mob_living user = null, obj_item used_item = null, DmList chosen_option = null, DmList created_atoms = null)
    {
        qdel(this);
        return;
    }

    // tg: code/game/atom/_atom.dm:707
    [ShouldCallParent]
    public virtual async DmTask OnCreatedFromProcessing(mob_living user = null, obj_item work_tool = null, DmList chosen_option = null, atom original_atom = null)
    {
        if (HAS_TRAIT(original_atom, TRAIT_FOOD_SILVER))
            ADD_TRAIT(this, TRAIT_FOOD_SILVER, INNATE_TRAIT); // stinky food always stinky

        if (Dm.truthy(_listen_lookup?[COMSIG_ATOM_CREATEDBY_PROCESSING]))
            _SendSignal(COMSIG_ATOM_CREATEDBY_PROCESSING, Dm.list(this, original_atom, chosen_option));
        if (user.mind != null)
            ADD_TRAIT(this, TRAIT_HANDMADE, REF(user.mind));
    }

    ///Connect this atom to a shuttle
    // tg: code/game/atom/_atom.dm:718
    public virtual async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        return null;
    }

    // tg: code/game/atom/_atom.dm:721
    [ShouldCallParent]
    public virtual async DmTask<object> intercept_zImpact(DmList falling_movables = null, float? levels = null)
    {
        levels ??= 1;
        object dot = null;
        Dm.combine(ref dot, (!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_INTERCEPT_Z_FALL]) ? NONE : this._SendSignal(COMSIG_ATOM_INTERCEPT_Z_FALL, Dm.list(this, falling_movables, levels))));
        return dot;
    }

    ///Setter for the `density` variable to append behavior related to its changing.
    // tg: code/game/atom/_atom.dm:726
    [ShouldCallParent]
    public virtual object set_density(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(density, new_value))
            return dot;
        dot = density;
        density = Dm.num(new_value);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_DENSITY_CHANGED]))
            this._SendSignal(COMSIG_ATOM_DENSITY_CHANGED, Dm.list(this));
        return dot;
    }

    ///Setter for the `base_pixel_x` variable to append behavior related to its changing.
    // tg: code/game/atom/_atom.dm:735
    public virtual object set_base_pixel_x(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(base_pixel_x, new_value))
            return dot;
        dot = base_pixel_x;
        base_pixel_x = Dm.num(new_value);

        pixel_x = Dm.num(Dm.sub(pixel_x + base_pixel_x, dot));
        return dot;
    }

    ///Setter for the `base_pixel_y` variable to append behavior related to its changing.
    // tg: code/game/atom/_atom.dm:744
    public virtual object set_base_pixel_y(object new_value = null)
    {
        object dot = null;
        if (Dm.eq(base_pixel_y, new_value))
            return dot;
        dot = base_pixel_y;
        base_pixel_y = Dm.num(new_value);

        pixel_y = Dm.num(Dm.sub(pixel_y + base_pixel_y, dot));
        return dot;
    }

    // Not a valid operation, turfs and movables handle block differently
    // tg: code/game/atom/_atom.dm:753
    public virtual void set_explosion_block(object explosion_block = null)
    {
        return;
    }

    /**
     * Returns true if this atom has gravity for the passed in turf
     *
     * Sends signals [COMSIG_ATOM_HAS_GRAVITY] and [COMSIG_TURF_HAS_GRAVITY], both can force gravity with
     * the forced gravity var.
     *
     * micro-optimized to hell because this proc is very hot, being called several times per movement every movement.
     *
     * HEY JACKASS, LISTEN
     * IF YOU ADD SOMETHING TO THIS PROC, MAKE SURE /mob/living ACCOUNTS FOR IT
     * Living mobs treat gravity in an event based manner. We've decomposed this proc into different checks
     * for them to use. If you add more to it, make sure you do that, or things will behave strangely
     *
     * Gravity situations:
     * * No gravity if you're not in a turf
     * * No gravity if this atom is in is a space turf
     * * No gravity if the area has NO_GRAVITY flag (space, ordnance bomb site, nearstation, solars)
     * * Gravity if the area it's in always has gravity
     * * Gravity if there's a gravity generator on the z level
     * * Gravity if the Z level has an SSMappingTrait for ZTRAIT_GRAVITY
     * * otherwise no gravity
     */
    // tg: code/game/atom/_atom.dm:778
    public virtual object has_gravity(turf gravity_turf = null)
    {
        if (!isturf(gravity_turf))
        {
            gravity_turf = get_turf(this);

            if (!Dm.truthy(gravity_turf))//no gravity in nullspace
                return FALSE;
        }

        DmList forced_gravity = Dm.list();
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_HAS_GRAVITY]))
            this._SendSignal(COMSIG_ATOM_HAS_GRAVITY, Dm.list(this, gravity_turf, forced_gravity));
        if (Dm.truthy(gravity_turf._listen_lookup?[COMSIG_TURF_HAS_GRAVITY]))
            gravity_turf._SendSignal(COMSIG_TURF_HAS_GRAVITY, Dm.list(gravity_turf, this, forced_gravity));
        if (Dm.truthy(length(forced_gravity)))
        {
            object positive_grav = max(forced_gravity);
            object negative_grav = min(min(forced_gravity), 0); //negative grav needs to be below or equal to 0

            //our gravity is sum of the most massive positive and negative numbers returned by the signal
            //so that adding two forced_gravity elements with an effect size of 1 each doesnt add to 2 gravity
            //but negative force gravity effects can cancel out positive ones

            return Dm.add(positive_grav, negative_grav);
        }

        area turf_area = gravity_turf.loc as area;

        return (Dm.num(!Dm.truthy(gravity_turf.force_no_gravity) && !Dm.truthy(Dm.bitand(turf_area.area_flags, NO_GRAVITY))) is var and__1 && !Dm.truthy(and__1) ? and__1 : (SSmapping.gravity_by_z_level[gravity_turf.z] is var or__1 && Dm.truthy(or__1) ? or__1 : (object)turf_area.default_gravity));
    }

    /**
     * Used to set something as 'open' if it's being used as a supplypod
     *
     * Override this if you want an atom to be usable as a supplypod.
     */
    // tg: code/game/atom/_atom.dm:807
    public virtual void set_opened()
    {
        return;
    }

    /**
     * Used to set something as 'closed' if it's being used as a supplypod
     *
     * Override this if you want an atom to be usable as a supplypod.
     */
    // tg: code/game/atom/_atom.dm:815
    public virtual void set_closed()
    {
        return;
    }

    ///Called after the atom is 'tamed' for type-specific operations, Usually called by the tameable component but also other things.
    // tg: code/game/atom/_atom.dm:819
    [ShouldCallParent]
    public virtual async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        ADD_TRAIT(this, TRAIT_TAMED, INNATE_TRAIT);
        return;
    }

    /**
     * Used to attempt to charge an object with a payment component.
     *
     * Use this if an atom needs to attempt to charge another atom.
     */
    // tg: code/game/atom/_atom.dm:829
    public virtual object attempt_charge(atom sender = null, atom target = null, float? extra_fees = null)
    {
        extra_fees ??= 0;
        return (!Dm.truthy(sender._listen_lookup?[COMSIG_OBJ_ATTEMPT_CHARGE]) ? NONE : sender._SendSignal(COMSIG_OBJ_ATTEMPT_CHARGE, Dm.list(sender, target, extra_fees)));
    }

    ///Passes Stat Browser Panel clicks to the game and calls client click on an atom
    // tg: code/game/atom/_atom.dm:833
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        object dot = await base.Topic(href, href_list);
        if (usr?.client == null)
            return dot;
        client usr_client = usr.client;
        DmList paramslist = Dm.list();

        // the stat panel's clicks (the stat panel is replaced, translation-rules 13: no link of the port carries these keys)
        if (Dm.truthy(href_list["statpanel_item_click"]))
        {
            object button = href_list["statpanel_item_click"];
            if (Dm.eq(button, "left"))
                paramslist[LEFT_CLICK] = "1";
            else if (Dm.eq(button, "right"))
                paramslist[RIGHT_CLICK] = "1";
            else if (Dm.eq(button, "middle"))
                paramslist[MIDDLE_CLICK] = "1";
            else
                return dot;

            if (Dm.truthy(href_list["statpanel_item_shiftclick"]))
                paramslist[SHIFT_CLICK] = "1";
            if (Dm.truthy(href_list["statpanel_item_ctrlclick"]))
                paramslist[CTRL_CLICK] = "1";
            if (Dm.truthy(href_list["statpanel_item_altclick"]))
                paramslist[ALT_CLICK] = "1";

            string mouseparams = list2params(paramslist);
            await usr_client.Click(this, loc as atom, null, mouseparams);
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/atom/_atom.dm:862
    public virtual async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        // M4a P1 (D125): BYOND's MouseEntered comes from the host's Hover action (ClientSeam.Hover, interaction.md 6.1).
        SSmouse_entered.hovers[usr.client] = this;
    }

    /// Fired whenever this atom is the most recent to be hovered over in the tick.
    /// Preferred over MouseEntered if you do not need information such as the position of the mouse.
    /// Especially because this is deferred over a tick, do not trust that `client` is not null.
    // tg: code/game/atom/_atom.dm:868
    [ShouldNotSleep]
    public virtual void on_mouse_enter(client client = null)
    {
        mob user = client?.mob;
        if (user == null)
            return;

        if (Dm.truthy(user._listen_lookup?[COMSIG_ATOM_MOUSE_ENTERED]))
            user._SendSignal(COMSIG_ATOM_MOUSE_ENTERED, Dm.list(user, this));

        // Screentips
        datum_hud active_hud = user.hud_used;
        if (!Dm.truthy(active_hud))
            return;

        object screentips_enabled = active_hud.screentips_enabled;
        if (Dm.eq(screentips_enabled, SCREENTIP_PREFERENCE_DISABLED) || Dm.truthy(Dm.bitand(flags_1, NO_SCREENTIPS_1)))
        {
            // DEVIATION(DV28): tg empties active_hud.screentip_text.maptext; the seam publishes "no screentip".
            ScreentipSeam.Publish(client, this, null, null);
            return;
        }

        string lmb_rmb_line = "";
        string ctrl_lmb_ctrl_rmb_line = "";
        string alt_lmb_alt_rmb_line = "";
        string shift_lmb_ctrl_shift_lmb_line = "";
        float extra_lines = 0;
        string extra_context = "";
        object used_name = name;
        // The context list tg shows (the seam's input, DV28): set where tg builds its lines from it.
        DmList shown_context = null;

        // tg: :897  isovermind(user) and iscameramob(user) (__DEFINES/is_helpers.dm) are istype tests of /mob/eye/blob and
        // /mob/eye/camera, written as such (rules 11.1: their mirror has no members for them yet).
        if (isliving(user) || istype(user, typeof(mob_eye_blob)) || istype(user, typeof(mob_eye_camera)) || (Dm.truthy(ghost_screentips) && isobserver(user)))
        {
            obj_item held_item = user.get_active_held_item() as obj_item;

            if (Dm.truthy(Dm.bitand(user.mob_flags, MOB_HAS_SCREENTIPS_NAME_OVERRIDE)))
            {
                DmList returned_name = Dm.list(used_name);

                object name_override_returns = (!Dm.truthy(user._listen_lookup?[COMSIG_MOB_REQUESTING_SCREENTIP_NAME_FROM_USER]) ? (object)NONE : user._SendSignal(COMSIG_MOB_REQUESTING_SCREENTIP_NAME_FROM_USER, Dm.list(user, returned_name, held_item, this)));
                if (Dm.truthy(Dm.bitand(name_override_returns, SCREENTIP_NAME_SET)))
                    used_name = returned_name[1];
            }

            if (Dm.truthy(Dm.bitand(flags_1, HAS_CONTEXTUAL_SCREENTIPS_1)) || Dm.truthy(Dm.bitand(held_item?.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS)))
            {
                DmList context = Dm.list();

                object contextual_screentip_returns =
                    Dm.bitor((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM]) ? (object)NONE : this._SendSignal(COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM, Dm.list(this, context, held_item, user))),
                    (held_item is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : (!Dm.truthy(held_item._listen_lookup?[COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET]) ? (object)NONE : held_item._SendSignal(COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET, Dm.list(held_item, context, this, user)))));

                if (Dm.truthy(Dm.bitand(contextual_screentip_returns, CONTEXTUAL_SCREENTIP_SET)))
                {
                    shown_context = context;
                    object screentip_images = active_hud.screentip_images;
                    // LMB and RMB on one line...
                    string lmb_text = Dm.str(build_context(context, SCREENTIP_CONTEXT_LMB, screentip_images));
                    string rmb_text = Dm.str(build_context(context, SCREENTIP_CONTEXT_RMB, screentip_images));

                    if (lmb_text != "")
                    {
                        lmb_rmb_line = lmb_text;
                        if (rmb_text != "")
                            lmb_rmb_line += Dm.text(" | []", rmb_text);
                    }
                    else if (rmb_text != "")
                        lmb_rmb_line = rmb_text;

                    // Ctrl-LMB, Ctrl-RMB on one line...
                    if (lmb_rmb_line != "")
                    {
                        lmb_rmb_line += "<br>";
                        extra_lines++;
                    }
                    if (Dm.@in(SCREENTIP_CONTEXT_CTRL_LMB, context))
                        ctrl_lmb_ctrl_rmb_line += Dm.str(build_context(context, SCREENTIP_CONTEXT_CTRL_LMB, screentip_images));

                    if (Dm.@in(SCREENTIP_CONTEXT_CTRL_RMB, context))
                    {
                        if (ctrl_lmb_ctrl_rmb_line != "")
                            ctrl_lmb_ctrl_rmb_line += " | ";
                        ctrl_lmb_ctrl_rmb_line += Dm.str(build_context(context, SCREENTIP_CONTEXT_CTRL_RMB, screentip_images));
                    }

                    // Alt-LMB, Alt-RMB on one line...
                    if (ctrl_lmb_ctrl_rmb_line != "")
                    {
                        ctrl_lmb_ctrl_rmb_line += "<br>";
                        extra_lines++;
                    }
                    if (Dm.@in(SCREENTIP_CONTEXT_ALT_LMB, context))
                        alt_lmb_alt_rmb_line += Dm.str(build_context(context, SCREENTIP_CONTEXT_ALT_LMB, screentip_images));
                    if (Dm.@in(SCREENTIP_CONTEXT_ALT_RMB, context))
                    {
                        if (alt_lmb_alt_rmb_line != "")
                            alt_lmb_alt_rmb_line += " | ";
                        alt_lmb_alt_rmb_line += Dm.str(build_context(context, SCREENTIP_CONTEXT_ALT_RMB, screentip_images));
                    }

                    // Shift-LMB, Ctrl-Shift-LMB on one line...
                    if (alt_lmb_alt_rmb_line != "")
                    {
                        alt_lmb_alt_rmb_line += "<br>";
                        extra_lines++;
                    }
                    if (Dm.@in(SCREENTIP_CONTEXT_SHIFT_LMB, context))
                        shift_lmb_ctrl_shift_lmb_line += Dm.str(build_context(context, SCREENTIP_CONTEXT_SHIFT_LMB, screentip_images));
                    if (Dm.@in(SCREENTIP_CONTEXT_CTRL_SHIFT_LMB, context))
                    {
                        if (shift_lmb_ctrl_shift_lmb_line != "")
                            shift_lmb_ctrl_shift_lmb_line += " | ";
                        shift_lmb_ctrl_shift_lmb_line += Dm.str(build_context(context, SCREENTIP_CONTEXT_CTRL_SHIFT_LMB, screentip_images));
                    }

                    if (shift_lmb_ctrl_shift_lmb_line != "")
                        extra_lines++;

                    if (Dm.truthy(extra_lines))
                        extra_context = Dm.text("<br><span class='subcontext'>[][][][]</span>", lmb_rmb_line, ctrl_lmb_ctrl_rmb_line, alt_lmb_alt_rmb_line, shift_lmb_ctrl_shift_lmb_line);
                }
            }
        }

        // tg: :948-976  new_maptext: empty for SCREENTIP_PREFERENCE_CONTEXT_ONLY without context, else the name and the context
        // lines in screentip_color; written into active_hud.screentip_text (through set_hover_maptext for a name longer than
        // the text's width, which measures it on the client).
        // DEVIATION(DV28): the seam carries tg's used_name and context list (ScreentipSeam.Publish, interaction.md 6.1) instead
        // of the composed maptext, its height and its y offset; screentip_text's maptext stays empty.
        if (Dm.eq(screentips_enabled, SCREENTIP_PREFERENCE_CONTEXT_ONLY) && extra_context == "")
            ScreentipSeam.Publish(client, this, null, null);
        else
            ScreentipSeam.Publish(client, this, Dm.str(used_name), shown_context);
    }

    // tg: code/game/atom/_atom.dm:979
    public virtual async DmTask set_hover_maptext(client client = null, datum_hud active_hud = null, object new_maptext = null)
    {
        // M4a P1: a deferred leaf (presentation, DV28): on_mouse_enter publishes through the screentip seam and never calls it.
        Dm.Deferred("code/game/atom/_atom.dm:979", "set_hover_maptext: the screentip maptext measured on the client (client.MeasureText, maptext_y; presentation, DV28)");
    }

    /**
     * This proc is used for telling whether something can pass by this atom in a given direction, for use by the pathfinding system.
     *
     * Trying to generate one long path across the station will call this proc on every single object on every single tile that we're seeing if we can move through, likely
     * multiple times per tile since we're likely checking if we can access said tile from multiple directions, so keep these as lightweight as possible.
     *
     * For turfs this will only be used if pathing_pass_method is TURF_PATHING_PASS_PROC
     *
     * Arguments:
     * * to_dir - What direction we're trying to move in, relevant for things like directional windows that only block movement in certain directions
     * * pass_info - Datum that stores info about the thing that's trying to pass us
     *
     * IMPORTANT NOTE: /turf/proc/LinkBlockedWithAccess assumes that overrides of CanAStarPass will always return true if density is FALSE
     * If this is NOT you, ensure you edit your can_astar_pass variable. Check __DEFINES/path.dm
     **/
    // tg: code/game/atom/_atom.dm:1000
    public virtual object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        object dot = null;
        if (Dm.truthy(Dm.bitand(pass_info.pass_flags, pass_flags_self)))
            return TRUE;
        dot = Dm.not(density);
        return dot;
    }

    /// Logic for adding reskin components goes here. Override for atom-specific reskin setups.
    // tg: code/game/atom/_atom.dm:1006
    public virtual async DmTask setup_reskins()
    {
        return;
    }
}
