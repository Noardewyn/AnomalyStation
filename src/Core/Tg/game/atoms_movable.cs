// Translated from tgstation (snapshot 1d99bd7f): code/game/atoms_movable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of the wave-4 scope (package A, the Move chain; docs/ai/specs/move-chain.md): the
// var block and dm_tg_vars are the W4.0 contracts' (rules 10.2), the procs that need modules of later milestones
// (space underlays, throwing, push and crush, storage, attack animations, languages, factions and allies, deadchat,
// view variables) are Dm.NotTranslated with the milestone named. The emissive blocker is deferred (DEVIATION DV12, lead
// decision D2). M7 lumcount: the light_blocking element (the opacity bookkeeping of the server's lumcount). The types
// interleave in tg's order (rules 1.9): /atom/movable, the global procs, /mob/living and /mob blocks of this file.
// The [DmPath] of a BYOND builtin type is on its engine part in src/Core/Tg/Builtin/.

// tg: code/game/atoms_movable.dm:1
public partial class atom_movable : atom
{
    // tg: code/game/atoms_movable.dm:1-5  tg's overrides of BYOND's /atom/movable defaults (rules 10.2, "Builtin types")
    private partial void dm_tg_vars()
    {
        abstract_type = typeof(atom_movable);
        layer = OBJ_LAYER;
        glide_size = 8;
        appearance_flags = Dm.bitor(Dm.bitor(TILE_BOUND, PIXEL_SCALE), LONG_GLIDE);
    }

    // tg: code/game/atoms_movable.dm:7
    public object last_move;
    /// A list containing arguments for Moved().
    // tg: code/game/atoms_movable.dm:9  VAR_PRIVATE/tmp/list/active_movement
    [DmPrivate]
    public DmList active_movement;
    // tg: code/game/atoms_movable.dm:10
    public float anchored = FALSE;
    // tg: code/game/atoms_movable.dm:11
    public float? move_resist = MOVE_RESIST_DEFAULT;
    // tg: code/game/atoms_movable.dm:12
    public float? move_force = MOVE_FORCE_DEFAULT;
    // tg: code/game/atoms_movable.dm:13
    public float? pull_force = PULL_FORCE_DEFAULT;
    // tg: code/game/atoms_movable.dm:14
    public datum_thrownthing throwing;
    // tg: code/game/atoms_movable.dm:15
    public float throw_speed = 2; //How many tiles to move per ds when being thrown. Float values are fully supported
    // tg: code/game/atoms_movable.dm:16
    public float throw_range = 7;
    ///Max range this atom can be thrown via telekinesis
    // tg: code/game/atoms_movable.dm:18
    public float tk_throw_range = 10;
    // tg: code/game/atoms_movable.dm:19
    public mob pulledby;
    /// What language holder type to init as
    // tg: code/game/atoms_movable.dm:21
    public Type initial_language_holder = typeof(datum_language_holder_atom_basic);
    /// Holds all languages this mob can speak and understand
    // tg: code/game/atoms_movable.dm:23  VAR_PRIVATE/datum/language_holder/language_holder
    [DmPrivate]
    public datum_language_holder language_holder;
    /// The list of factions this atom belongs to (used for cacheable faction strings - these tend to not change very often)
    // tg: code/game/atoms_movable.dm:25  VAR_PROTECTED/list/faction
    [DmProtected]
    public DmList faction;
    /// The list of allies this atom has (used for anything too dynamic for string_list() - typically mob refs, each mob starts with themselves as an ally)
    // tg: code/game/atoms_movable.dm:27
    public DmList allies;

    /// Use get_default_say_verb() in say.dm instead of reading verb_say.
    // tg: code/game/atoms_movable.dm:30
    public string verb_say = "says";
    // tg: code/game/atoms_movable.dm:31
    public string verb_ask = "asks";
    // tg: code/game/atoms_movable.dm:32
    public string verb_exclaim = "exclaims";
    // tg: code/game/atoms_movable.dm:33
    public string verb_whisper = "whispers";
    // tg: code/game/atoms_movable.dm:34
    public string verb_sing = "sings";
    // tg: code/game/atoms_movable.dm:35
    public string verb_yell = "yells";
    // tg: code/game/atoms_movable.dm:36
    public object speech_span;
    ///Are we moving with inertia? Mostly used as an optimization
    // tg: code/game/atoms_movable.dm:38
    public float inertia_moving = FALSE;
    /// Multiplies speed the movable drifts when unaffected by gravity.
    /// "Passive" is used for referring "base drift speed" - only the smaller of the two are used.
    // tg: code/game/atoms_movable.dm:41
    public float inertia_move_multiplier_passive = 1;
    /// Multiplies speed the movable drifts when unaffected by gravity.
    /// "Active" is used for referring to things boosting our drift speed, like jetpacks - only the smaller of the two are used.
    // tg: code/game/atoms_movable.dm:44
    public float inertia_move_multiplier_active = 1;
    /// Object "weight", higher weight reduces acceleration applied to the object
    // tg: code/game/atoms_movable.dm:46
    public float inertia_force_weight = 1;
    ///The last time we pushed off something
    ///This is a hack to get around dumb him him me scenarios
    // tg: code/game/atoms_movable.dm:49
    public object last_pushoff;
    /// Things we can pass through while moving. If any of this matches the thing we're trying to pass's [pass_flags_self], then we can pass through.
    // tg: code/game/atoms_movable.dm:51
    public float pass_flags = NONE;
    /// If false makes [CanPass][/atom/proc/CanPass] call [CanPassThrough][/atom/movable/proc/CanPassThrough] on this type instead of using default behaviour
    // tg: code/game/atoms_movable.dm:53
    public float generic_canpass = TRUE;
    ///0: not doing a diagonal move. 1 and 2: doing the first/second step of the diagonal move
    // tg: code/game/atoms_movable.dm:55
    public float moving_diagonally = 0;
    ///attempt to resume grab after moving instead of before.
    // tg: code/game/atoms_movable.dm:57
    public atom_movable moving_from_pull;
    ///Holds information about any movement loops currently running/waiting to run on the movable. Lazy, will be null if nothing's going on
    // tg: code/game/atoms_movable.dm:59
    public datum_movement_packet move_packet;
    /**
    * an associative lazylist of relevant nested contents by "channel", the list is of the form: list(channel = list(important nested contents of that type))
    * each channel has a specific purpose and is meant to replace potentially expensive nested contents iteration.
    * do NOT add channels to this for little reason as it can add considerable memory usage.
    */
    // tg: code/game/atoms_movable.dm:65
    public DmList important_recursive_contents;

    /// String representing the spatial grid groups we want to be held in.
    /// acts as a key to the list of spatial grid contents types we exist in via SSspatial_grid.spatial_grid_categories.
    /// We do it like this to prevent people trying to mutate them and to save memory on holding the lists ourselves
    // tg: code/game/atoms_movable.dm:70
    public object spatial_grid_key;

    /**
    * In case you have multiple types, you automatically use the most useful one.
    * IE: Skating on ice, flippers on water, flying over chasm/space, etc.
    * I recommend you use the movetype_handler system and not modify this directly, especially for living mobs.
    */
    // tg: code/game/atoms_movable.dm:77
    public float? movement_type = GROUND;

    // tg: code/game/atoms_movable.dm:79
    public atom_movable pulling;
    // tg: code/game/atoms_movable.dm:80
    public float grab_state = GRAB_PASSIVE;
    /// The strongest grab we can acomplish
    // tg: code/game/atoms_movable.dm:82
    public float max_grab = GRAB_PASSIVE;
    // tg: code/game/atoms_movable.dm:83
    public float throwforce = 0;
    // tg: code/game/atoms_movable.dm:84
    public datum_component_orbiter orbiting;

    ///is the mob currently ascending or descending through z levels?
    // tg: code/game/atoms_movable.dm:87
    public object currently_z_moving;

    /// Either [EMISSIVE_BLOCK_NONE], [EMISSIVE_BLOCK_GENERIC], or [EMISSIVE_BLOCK_UNIQUE]
    // tg: code/game/atoms_movable.dm:90
    public float blocks_emissive = EMISSIVE_BLOCK_NONE;
    ///Internal holder for emissive blocker object, do not use directly use blocks_emissive
    // tg: code/game/atoms_movable.dm:92
    public atom_movable_render_step_emissive_blocker em_block;

    ///Lazylist to keep track on the sources of illumination.
    // tg: code/game/atoms_movable.dm:95
    public DmList affected_dynamic_lights;
    ///Highest-intensity light affecting us, which determines our visibility.
    // tg: code/game/atoms_movable.dm:97
    public float affecting_dynamic_lumi = 0;

    /// Whether this atom should have its dir automatically changed when it moves. Setting this to FALSE allows for things such as directional windows to retain dir on moving without snowflake code all of the place.
    // tg: code/game/atoms_movable.dm:100
    public float set_dir_on_move = TRUE;

    /// The degree of thermal insulation that mobs in list/contents have from the external environment, between 0 and 1
    // tg: code/game/atoms_movable.dm:103
    public float contents_thermal_insulation = 0;
    /// The degree of pressure protection that mobs in list/contents have from the external environment, between 0 and 1
    // tg: code/game/atoms_movable.dm:105
    public float contents_pressure_protection = 0;

    /// The voice that this movable makes when speaking
    // tg: code/game/atoms_movable.dm:108
    public object voice;

    /// The pitch adjustment that this movable uses when speaking.
    // tg: code/game/atoms_movable.dm:111
    public float pitch = 0;

    /// The base set of blips to use for blip calculation.
    // tg: code/game/atoms_movable.dm:114
    public string blip_base = "male";

    /// The blip variant to use for blip calculation.
    // tg: code/game/atoms_movable.dm:117
    public string blip_number = "1";

    /// Datum that keeps all data related to zero-g drifting and handles related code/comsigs
    // tg: code/game/atoms_movable.dm:120
    public datum_drift_handler drift_handler;

    /// The filter to apply to the voice when processing the TTS audio message.
    // tg: code/game/atoms_movable.dm:123
    public string voice_filter = "";

    /// Set to anything other than "" to activate the silicon voice effect for TTS messages.
    // tg: code/game/atoms_movable.dm:126
    public string tts_silicon_voice_effect = "";

    /// Value used to increment ex_act() if reactionary_explosions is on
    /// How much we as a source block explosions by
    /// Will not automatically apply to the turf below you, you need to apply /datum/element/block_explosives in conjunction with this
    // tg: code/game/atoms_movable.dm:131
    public float explosion_block = 0;

    // Access levels, used in modules\jobs\access.dm
    /// List of accesses needed to use this object: The user must possess all accesses in this list in order to use the object.
    /// Example: If req_access = list(ACCESS_ENGINE, ACCESS_CE)- then the user must have both ACCESS_ENGINE and ACCESS_CE in order to use the object.
    // tg: code/game/atoms_movable.dm:136
    public DmList req_access;
    /// List of accesses needed to use this object: The user must possess at least one access in this list in order to use the object.
    /// Example: If req_one_access = list(ACCESS_ENGINE, ACCESS_CE)- then the user must have either ACCESS_ENGINE or ACCESS_CE in order to use the object.
    // tg: code/game/atoms_movable.dm:139
    public DmList req_one_access;


    // tg: code/game/atoms_movable.dm:141-147  /mutable_appearance/emissive_blocker and its New: not declared,
    // mutable_appearance is not created in M0 (lead decisions D2, D3; DEVIATION DV12).

    // tg: code/game/atoms_movable.dm:149
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        if (Dm.truthy(LAZYLEN(faction)))
            faction = string_list(faction) as DmList;

        dot = base.Initialize(mapload, __extra);
        // tg: #ifdef UNIT_TESTS ... #endif not translated (inactive in production)

        // tg: #if EMISSIVE_BLOCK_GENERIC != 0 ... #endif: a compile-time check (EMISSIVE_BLOCK_GENERIC is 0)

        // This one is incredible.
        // `if (x) else { /* code */ }` is surprisingly fast, and it's faster than a switch, which is seemingly not a jump table.
        // From what I can tell, a switch case checks every single branch individually, although sane, is slow in a hot proc like this.
        // So, we make the most common `blocks_emissive` value, EMISSIVE_BLOCK_GENERIC, 0, getting to the fast else branch quickly.
        // If it fails, then we can check over every value it can be (here, EMISSIVE_BLOCK_UNIQUE is the only one that matters).
        // This saves several hundred milliseconds of init time.
        if (Dm.truthy(blocks_emissive))
        {
            if (blocks_emissive == EMISSIVE_BLOCK_UNIQUE)
            {
                // DEVIATION(DV12): the unique emissive blocker (render_target = ref(src), em_block = new(null, src), added to
                // overlays and managed_overlays, atoms_movable.dm:172-182) is not created: lighting planes and emissives are
                // not ported (D24, lead decision D2). M3 P3, by the lead's decision: a deferred presentation leaf (doors,
                // windoors and pod doors set EMISSIVE_BLOCK_UNIQUE).
                Dm.Deferred("code/game/atoms_movable.dm:172", "Initialize: the EMISSIVE_BLOCK_UNIQUE emissive blocker (render_target, em_block, overlays; DEVIATION DV12)");
            }
        }
        else
        {
            // DEVIATION(DV12): the generic emissive blocker (a static mutable_appearance/emissive_blocker copying this
            // atom's icon, flattened into overlays and managed_overlays, atoms_movable.dm:183-203) is not created:
            // lighting planes are not ported (D24) and mutable_appearance is not created in M0 (lead decision D2).
            Dm.Deferred("code/game/atoms_movable.dm:183", "Initialize: the generic emissive blocker overlay (DEVIATION DV12)");
        }

        if (Dm.truthy(opacity))
            Waiting.NoSleep(AddElement(typeof(datum_element_light_blocking)));
        switch (light_system)
        {
            case OVERLAY_LIGHT:
                Waiting.NoSleep(AddComponent(typeof(datum_component_overlay_lighting)));
                break;
            case OVERLAY_LIGHT_DIRECTIONAL:
                Waiting.NoSleep(AddComponent(typeof(datum_component_overlay_lighting), Dm.named("is_directional", TRUE)));
                break;
            case OVERLAY_LIGHT_BEAM:
                Waiting.NoSleep(AddComponent(typeof(datum_component_overlay_lighting), Dm.named("is_directional", TRUE), Dm.named("is_beam", TRUE)));
                break;
        }
        return dot;
    }

    // tg: code/game/atoms_movable.dm:215
    public override object Destroy(float? force = null)
    {
        object dot = null;
        QDEL_NULL(ref language_holder);
        QDEL_NULL(ref em_block);
        QDEL_NULL(ref drift_handler);

        unbuckle_all_mobs(force: TRUE);

        if (Dm.truthy(loc))
        {
            //Restore air flow if we were blocking it (movables with ATMOS_PASS_PROC will need to do this manually if necessary)
            if (((can_atmos_pass == ATMOS_PASS_DENSITY && Dm.truthy(density)) || can_atmos_pass == ATMOS_PASS_NO) && isturf(loc))
            {
                can_atmos_pass = ATMOS_PASS_YES;
                air_update_turf(TRUE, FALSE);
            }
        }

        if (Dm.truthy(opacity))
            RemoveElement(typeof(datum_element_light_blocking));

        invisibility = INVISIBILITY_ABSTRACT;

        if (Dm.truthy(pulledby))
            Waiting.NoSleep(pulledby.stop_pulling());
        if (Dm.truthy(pulling))
            Waiting.NoSleep(stop_pulling());

        if (Dm.truthy(orbiting))
        {
            Waiting.NoSleep(orbiting.end_orbit(this));
            orbiting = null;
        }

        if (Dm.truthy(move_packet))
        {
            if (!QDELETED(move_packet))
                qdel(move_packet);
            move_packet = null;
        }

        if (Dm.truthy(spatial_grid_key))
            SSspatial_grid.force_remove_from_grid(this);

        // tg: #ifndef DISABLE_DREAMLUAU ... #endif not translated (inactive in production: DISABLE_DREAMLUAU is defined, rules 1.7)

        dot = base.Destroy(force);

        foreach (object movable_content in Dm.each(contents))
            qdel(movable_content);

        Waiting.NoSleep(moveToNullspace());

        //This absolutely must be after moveToNullspace()
        //We rely on Entered and Exited to manage this list, and the copy of this list that is on any /atom/movable "Containers"
        //If we clear this before the nullspace move, a ref to this object will be hung in any of its movable containers
        LAZYNULL(ref important_recursive_contents);


        vis_locs = null; //clears this atom out of all viscontents

        // Checking length(vis_contents) before cutting has significant speed benefits
        if (Dm.truthy(length(vis_contents)))
            vis_contents.Cut();
        return dot;
    }

    // tg: code/game/atoms_movable.dm:275
    public virtual object update_emissive_block()
    {
        // This one is incredible.
        // `if (x) else { /* code */ }` is surprisingly fast, and it's faster than a switch, which is seemingly not a jump table.
        // From what I can tell, a switch case checks every single branch individually, although sane, is slow in a hot proc like this.
        // So, we make the most common `blocks_emissive` value, EMISSIVE_BLOCK_GENERIC, 0, getting to the fast else branch quickly.
        // If it fails, then we can check over every value it can be (here, EMISSIVE_BLOCK_UNIQUE is the only one that matters).
        // This saves several hundred milliseconds of init time.
        if (Dm.truthy(blocks_emissive))
        {
            if (blocks_emissive == EMISSIVE_BLOCK_UNIQUE)
            {
                // DEVIATION(DV12): the unique emissive blocker (SET_PLANE of em_block, or render_target and a new em_block,
                // returned for update_overlays, atoms_movable.dm:284-291) is not created (lighting planes and emissives are
                // not ported, D24, lead decision D2). M3 P3, by the lead's decision: a deferred presentation leaf; em_block
                // stays null, so update_overlays adds nothing.
                Dm.Deferred("code/game/atoms_movable.dm:284", "update_emissive_block: the EMISSIVE_BLOCK_UNIQUE emissive blocker (SET_PLANE of em_block, or render_target and a new em_block; DEVIATION DV12)");
                return em_block;
            }
            // Implied else if (blocks_emissive == EMISSIVE_BLOCK_NONE) -> return
        }
        // EMISSIVE_BLOCK_GENERIC == 0
        else
        {
            // DEVIATION(DV12): fast_emissive_blocker (__HELPERS/lighting.dm:36) builds the generic emissive blocker
            // appearance; it is not created (lighting planes are not ported, D24; no mutable_appearance in M0).
            Dm.Deferred("code/game/atoms_movable.dm:293", "update_emissive_block: fast_emissive_blocker, the generic emissive blocker (DEVIATION DV12)");
            return null;
        }
        return null;
    }
}

public static partial class GlobalProcs
{
    /// Generates a space underlay for a turf
    /// This provides proper lighting support alongside just looking nice
    /// Accepts the appearance to make "spaceish", and the turf we're doing this for
    // tg: code/game/atoms_movable.dm:298  (M4c slice 8: the space look of the underlay; reached by the diagonal walls over space,
    // walls.dm:55, and by a space neighbour's get_smooth_underlay_icon, space.dm:160)
    public static void generate_space_underlay(mutable_appearance underlay_appearance = null, turf generate_for = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/space.dmi");
        underlay_appearance.icon_state = "space";
        SET_PLANE(underlay_appearance, PLANE_SPACE, generate_for);
        // tg: :302-342  the 2D lighting of the underlay: the turf's render_target, an /atom/movable/render_step/emissive_blocker
        // overlay, the starlight object (GLOB.starlight_objects) masked by a BLEND_MULTIPLY copy of the turf's appearance, and that
        // mask again on the LIGHTING_PLANE. Never as such: BYOND's render relays, emissives and lighting planes are not ported
        // (D24, DV12); the 3D client lights the hull from space by its starlight cells (D116, DV22).
        Dm.Deferred("code/game/atoms_movable.dm:302", "generate_space_underlay: the underlay's render relay, emissive blocker and starlight masks (never as such: D24, DV12; the client's starlight, D116/DV22)");
    }
}

// tg: code/game/atoms_movable.dm:346
public partial class atom_movable : atom
{
    // tg: code/game/atoms_movable.dm:346
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList overlays = (await base.update_overlays(__extra)) as DmList;
        object emissive_block = update_emissive_block();
        if (Dm.truthy(emissive_block))
            // Emissive block should always go at the beginning of the list
            overlays.Insert(1, emissive_block);
        return overlays;
    }

    // tg: code/game/atoms_movable.dm:354
    [ShouldCallParent]
    public virtual async DmTask<object> onZImpact(turf impacted_turf = null, object levels = null, float? impact_flags = null)
    {
        impact_flags ??= NONE;
        if (!Dm.truthy(Dm.bitand(impact_flags, ZIMPACT_NO_MESSAGE)))
        {
            visible_message(
                span_danger(I18n.T("[] crashes into []!", this, impacted_turf)),
                span_userdanger(I18n.T("You crash into []!", impacted_turf))
            );
        }
        if (!Dm.truthy(Dm.bitand(impact_flags, ZIMPACT_NO_SPIN)))
            Waiting.DetachSync(() => SpinAnimation(5, 2));
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_ON_Z_IMPACT]))
            this._SendSignal(COMSIG_ATOM_ON_Z_IMPACT, Dm.list(this, impacted_turf, levels));
        return TRUE;
    }

    /*
     * Attempts to move using zMove if direction is UP or DOWN, step if not
     *
     * Args:
     * direction: The direction to go
     * z_move_flags: bitflags used for checks in zMove and can_z_move
    */
    // tg: code/game/atoms_movable.dm:373
    public virtual async DmTask<object> try_step_multiz(object direction = null, float? z_move_flags = null)
    {
        z_move_flags ??= ZMOVE_FLIGHT_FLAGS;
        if (Dm.eq(direction, UP) || Dm.eq(direction, DOWN))
            return await zMove(direction, null, z_move_flags);
        return await step(this, Dm.num(direction));
    }

    /*
     * The core multi-z movement proc. Used to move a movable through z levels.
     * If target is null, it'll be determined by the can_z_move proc, which can potentially return null if
     * conditions aren't met (see z_move_flags defines in __DEFINES/movement.dm for info) or if dir isn't set.
     * Bear in mind you don't need to set both target and dir when calling this proc, but at least one or two.
     * This will set the currently_z_moving to CURRENTLY_Z_MOVING_GENERIC if unset, and then clear it after
     * Forcemove().
     *
     *
     * Args:
     * * dir: the direction to go, UP or DOWN, only relevant if target is null.
     * * target: The target turf to move the src to. Set by can_z_move() if null.
     * * z_move_flags: bitflags used for various checks in both this proc and can_z_move(). See __DEFINES/movement.dm.
     */
    // tg: code/game/atoms_movable.dm:392
    public virtual async DmTask<object> zMove(object dir = null, turf target = null, float? z_move_flags = null, params object[] __extra)
    {
        z_move_flags ??= ZMOVE_FLIGHT_FLAGS;
        if (!Dm.truthy(target))
        {
            target = can_z_move(dir, get_turf(this), null, z_move_flags) as turf;
            if (!Dm.truthy(target))
            {
                set_currently_z_moving(FALSE, TRUE);
                return FALSE;
            }
        }

        DmList moving_movs = get_z_move_affected(z_move_flags) as DmList;

        foreach (atom_movable movable in Dm.each_cast<atom_movable>(moving_movs))
        {
            movable.currently_z_moving = (currently_z_moving is var or__1 && Dm.truthy(or__1) ? or__1 : (object)CURRENTLY_Z_MOVING_GENERIC);
            await movable.forceMove(target);
            movable.set_currently_z_moving(FALSE, TRUE);
        }
        // This is run after ALL movables have been moved, so pulls don't get broken unless they are actually out of range.
        if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_CHECK_PULLS)))
        {
            foreach (atom_movable moved_mov in Dm.each_cast<atom_movable>(moving_movs))
            {
                if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_CHECK_PULLEDBY)) && Dm.truthy(moved_mov.pulledby) && (moved_mov.z != moved_mov.pulledby.z || get_dist(moved_mov, moved_mov.pulledby) > 1))
                    await moved_mov.pulledby.stop_pulling();
                if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_CHECK_PULLING)))
                    await moved_mov.check_pulling(TRUE);
            }
        }
        return TRUE;
    }

    /// Returns a list of movables that should also be affected when src moves through zlevels, and src.
    // tg: code/game/atoms_movable.dm:415
    public virtual object get_z_move_affected(object z_move_flags = null, DmList returning_list = null)
    {
        returning_list ??= Dm.list();
        DmList dot = null;
        dot = returning_list;
        Dm.combine(ref dot, this);
        if (Dm.truthy(buckled_mobs))
            Dm.combine(ref dot, buckled_mobs);
        if (!Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_INCLUDE_PULLED)))
            return dot;
        foreach (mob_living buckled in Dm.each_cast<mob_living>(buckled_mobs))
        {
            if (Dm.truthy(buckled.pulling))
                Dm.combine(ref dot, buckled.pulling);
        }
        //makes conga lines work with ladders and flying up and down; checks if the guy you are pulling is pulling someone,
        //then uses recursion to run the same function again
        //we pass in the list from this proc to ensure we dont reach an infinite loop due to mobs grabbed in a loop or two mobs grabing eachother.
        if (Dm.truthy(pulling) && !Dm.@in(pulling, dot))
            Dm.combine(ref dot, pulling.get_z_move_affected(z_move_flags, dot));
        return dot;
    }

    /**
     * Checks if the destination turf is elegible for z movement from the start turf to a given direction and returns it if so.
     * Args:
     * * direction: the direction to go, UP or DOWN, only relevant if target is null.
     * * start: Each destination has a starting point on the other end. This is it. Most of the times the location of the source.
     * * z_move_flags: bitflags used for various checks. See __DEFINES/movement.dm.
     * * rider: A living mob in control of the movable. Only non-null when a mob is riding a vehicle through z-levels.
     */
    // tg: code/game/atoms_movable.dm:439
    public virtual object can_z_move(object direction = null, turf start = null, turf destination = null, float? z_move_flags = null, mob_living rider = null)
    {
        z_move_flags ??= ZMOVE_FLIGHT_FLAGS;
        if (!Dm.truthy(start))
        {
            start = get_turf(this);
            if (!Dm.truthy(start))
                return FALSE;
        }
        if (!Dm.truthy(direction))
        {
            if (!Dm.truthy(destination))
                return FALSE;
            direction = get_dir_multiz(start, destination);
        }
        if (!Dm.eq(direction, UP) && !Dm.eq(direction, DOWN))
            return FALSE;
        if (!Dm.truthy(destination))
        {
            destination = get_step_multiz(start, direction) as turf;
            if (!Dm.truthy(destination))
            {
                if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                    to_chat((rider is var or__1 && Dm.truthy(or__1) ? or__1 : (object)this), span_warning(I18n.T("There's nowhere to go in that direction!")));
                return FALSE;
            }
        }
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_CAN_Z_MOVE]) ? NONE : this._SendSignal(COMSIG_CAN_Z_MOVE, Dm.list(this, start, destination))), COMPONENT_CANT_Z_MOVE)))
            return FALSE;
        if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FALL_CHECKS)) && (Dm.truthy(throwing) || Dm.truthy(Dm.bitand(movement_type, Dm.bitor(FLYING, FLOATING))) || !Dm.truthy(has_gravity(start))))
            return FALSE;
        if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_CAN_FLY_CHECKS)) && !Dm.truthy(Dm.bitand(movement_type, Dm.bitor(FLYING, FLOATING))) && Dm.truthy(has_gravity(start)))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
            {
                if (Dm.truthy(rider))
                    // GAP(lead): "[src] [p_are()] incapable of flight.": the root /datum/proc/p_are
                    // (__HELPERS/pronouns.dm:33) is not declared yet.
                    Dm.NotTranslated("code/game/atoms_movable.dm:463", "can_z_move: the rider's feedback needs p_are (__HELPERS/pronouns.dm:33, not declared yet; M2)");
                else
                    to_chat(this, span_warning(I18n.T("You are not Superman.")));
            }
            return FALSE;
        }
        if ((!Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_IGNORE_OBSTACLES)) && !(Dm.truthy(start.zPassOut(direction)) && Dm.truthy(destination.zPassIn(direction)))) || (!Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_ALLOW_ANCHORED)) && Dm.truthy(anchored)))
        {
            if (Dm.truthy(Dm.bitand(z_move_flags, ZMOVE_FEEDBACK)))
                to_chat((rider is var or__2 && Dm.truthy(or__2) ? or__2 : (object)this), span_warning(I18n.T("You can't move there!")));
            return FALSE;
        }
        return destination; //used by some child types checks and zMove()
    }

    // tg: code/game/atoms_movable.dm:473  /atom/movable/vv_edit_var: not declared. View variables are admin tooling
    // (M10, rules 13), and the root /datum/proc/vv_edit_var (datums/datumvars.dm:16) is not declared yet.

    // tg: code/game/atoms_movable.dm:525
    public virtual async DmTask<object> start_pulling(atom_movable pulled_atom = null, object state = null, object force = null, float? supress_message = null)
    {
        force ??= move_force;
        supress_message ??= FALSE;
        if (QDELETED(pulled_atom))
            return FALSE;
        if (!Dm.truthy(pulled_atom.can_be_pulled(this, force)))
            return FALSE;

        // If we're pulling something then drop what we're currently pulling and pull this instead.
        if (Dm.truthy(pulling))
        {
            if (Dm.eq(state, 0))
            {
                await stop_pulling();
                return FALSE;
            }
            // Are we trying to pull something we are already pulling? Then enter grab cycle and end.
            if (pulled_atom == pulling)
            {
                await setGrabState(state);
                if (pulled_atom is mob_living)
                {
                    mob_living pulled_mob = pulled_atom as mob_living;
                    await pulled_mob.grabbedby(this as mob_living);
                }
                return TRUE;
            }
            await stop_pulling();
        }

        if (Dm.truthy(pulled_atom.pulledby))
        {
            log_combat(pulled_atom, pulled_atom.pulledby, "pulled from", this);
            await pulled_atom.pulledby.stop_pulling(); //an object can't be pulled by two mobs at once.
        }
        await set_pulling(pulled_atom);
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_START_PULL]))
            this._SendSignal(COMSIG_ATOM_START_PULL, Dm.list(this, pulled_atom, state, force));
        await setGrabState(state);
        if (ismob(pulled_atom))
        {
            mob pulled_mob = pulled_atom as mob;
            log_combat(this, pulled_mob, "grabbed", addition: "passive grab");
            if (!Dm.truthy(supress_message))
            {
                pulled_mob.visible_message(
                    span_warning(I18n.T("[] grabs [] passively.", this, pulled_mob)),
                    span_danger(I18n.T("[] grabs you passively.", this))
                );
            }
        }


        return TRUE;
    }

    // tg: code/game/atoms_movable.dm:563
    public virtual async DmTask stop_pulling()
    {
        if (!Dm.truthy(pulling))
            return;
        atom_movable old_pulling = (await set_pulling(null)) as atom_movable;
        if (Dm.truthy(old_pulling._listen_lookup?[COMSIG_ATOM_NO_LONGER_PULLED]))
            old_pulling._SendSignal(COMSIG_ATOM_NO_LONGER_PULLED, Dm.list(old_pulling, this));
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_NO_LONGER_PULLING]))
            this._SendSignal(COMSIG_ATOM_NO_LONGER_PULLING, Dm.list(this, old_pulling));
    }

    // tg: code/game/atoms_movable.dm:570  #define PULLED_WHILE_CRIT_TRAIT "pulled_while_softcrit" (file-local, #undef at :616)
    private const string PULLED_WHILE_CRIT_TRAIT = "pulled_while_softcrit";

    // tg: code/game/atoms_movable.dm:572
    [SignalHandler]
    public virtual void pulled_mob_stat_change(mob_living pulled_mob = null, object new_stat = null, params object[] __extra)
    {

        if (Dm.ge(new_stat, SOFT_CRIT))
            ADD_TRAIT(pulled_mob, TRAIT_IMMOBILIZED, PULLED_WHILE_CRIT_TRAIT);
        else
            REMOVE_TRAIT(pulled_mob, TRAIT_IMMOBILIZED, PULLED_WHILE_CRIT_TRAIT);
    }

    // tg: code/game/atoms_movable.dm:580
    public virtual async DmTask<object> set_pulling(object new_pulling = null)
    {
        object dot = null;
        if (Dm.eq(new_pulling, pulling))
            return FALSE; //null signals there was a change, be sure to return FALSE if none happened here.

        if (new_pulling == null)
            await setGrabState(GRAB_PASSIVE);

        dot = pulling;
        pulling = new_pulling as atom_movable;

        if (ismovable(dot))
        {
            atom_movable was_pulling = dot as atom_movable;
            was_pulling.set_pulledby(null);
        }

        if (ismovable(new_pulling))
        {
            atom_movable pulled_thing = new_pulling as atom_movable;
            pulled_thing.set_pulledby(this);
        }

        if (ismob(dot))
        {
            mob was_pulled_mob = dot as mob;
            UnregisterSignal(was_pulled_mob, COMSIG_MOB_STATCHANGE);
            REMOVE_TRAIT(was_pulled_mob, TRAIT_IMMOBILIZED, PULLED_WHILE_CRIT_TRAIT);
        }

        if (ismob(pulling))
        {
            mob pulled_mob = pulling as mob;
            RegisterSignal(pulling, COMSIG_MOB_STATCHANGE, nameof(pulled_mob_stat_change));
            if (pulled_mob.stat >= SOFT_CRIT)
                ADD_TRAIT(pulled_mob, TRAIT_IMMOBILIZED, PULLED_WHILE_CRIT_TRAIT);
        }
        return dot;
    }

    ///Reports the event of the change in value of the pulledby variable.
    // tg: code/game/atoms_movable.dm:610
    public virtual object set_pulledby(object new_pulledby = null)
    {
        object dot = null;
        if (Dm.eq(new_pulledby, pulledby))
            return FALSE; //null signals there was a change, be sure to return FALSE if none happened here.
        dot = pulledby;
        pulledby = new_pulledby as mob;
        return dot;
    }

    // tg: code/game/atoms_movable.dm:618
    public virtual async DmTask<object> Move_Pulled(atom moving_atom = null)
    {
        if (!Dm.truthy(pulling))
            return FALSE;
        if (Dm.truthy(pulling.anchored) || Dm.num(pulling.move_resist) > Dm.num(move_force) || !Dm.truthy(await pulling.Adjacent(this, this, pulling)))
        {
            await stop_pulling();
            return FALSE;
        }
        if (isliving(pulling))
        {
            mob_living pulling_mob = pulling as mob_living;
            if (Dm.truthy(pulling_mob.buckled) && Dm.truthy(pulling_mob.buckled.buckle_prevents_pull)) //if they're buckled to something that disallows pulling, prevent it
            {
                await stop_pulling();
                return FALSE;
            }
        }
        if (get_turf(moving_atom) == loc && Dm.truthy(pulling.density))
            return FALSE;
        float move_dir = get_dir(pulling.loc, moving_atom);
        if (!Dm.truthy(await Process_Spacemove(move_dir)))
            return FALSE;
        await pulling.Move(get_step(pulling.loc, move_dir), move_dir, glide_size);
        return TRUE;
    }
}

// tg: code/game/atoms_movable.dm:637  (a /mob/living proc defined in this file, rules 1.9 "interleaved types")
public partial class mob_living : mob
{
    // tg: code/game/atoms_movable.dm:637
    public override async DmTask<object> Move_Pulled(atom moving_atom = null)
    {
        object dot = null;
        dot = await base.Move_Pulled(moving_atom);
        if (!Dm.truthy(dot) || !isliving(moving_atom))
            return dot;
        mob_living pulled_mob = moving_atom as mob_living;
        await set_pull_offsets(pulled_mob, grab_state, animate: FALSE);
        return dot;
    }
}

// tg: code/game/atoms_movable.dm:648
public partial class atom_movable : atom
{
    /**
     * Checks if the pulling and pulledby should be stopped because they're out of reach.
     * If z_allowed is TRUE, the z level of the pulling will be ignored.This is to allow things to be dragged up and down stairs.
     */
    // tg: code/game/atoms_movable.dm:648
    public virtual async DmTask check_pulling(float? only_pulling = null, float? z_allowed = null)
    {
        only_pulling ??= FALSE;
        z_allowed ??= FALSE;
        if (Dm.truthy(pulling))
        {
            if (get_dist(this, pulling) > 1 || (z != pulling.z && !Dm.truthy(z_allowed)))
                await stop_pulling();
            else if (!isturf(loc))
                await stop_pulling();
            else if (Dm.truthy(pulling) && !isturf(pulling.loc) && pulling.loc != loc) //to be removed once all code that changes an object's loc uses forceMove().
            {
                log_game(Dm.text("DEBUG:[]'s pull on [] wasn't broken despite [] being in []. Pull stopped manually.", this, pulling, pulling, pulling.loc));
                await stop_pulling();
            }
            else if (Dm.truthy(pulling.anchored) || Dm.num(pulling.move_resist) > Dm.num(move_force))
                await stop_pulling();
        }
        if (!Dm.truthy(only_pulling) && Dm.truthy(pulledby) && moving_diagonally != FIRST_DIAG_STEP && (get_dist(this, pulledby) > 1 || (z != pulledby.z && !Dm.truthy(z_allowed)))) //separated from our puller and not in the middle of a diagonal move.
            await pulledby.stop_pulling();
    }

    // tg: code/game/atoms_movable.dm:662
    public virtual void set_glide_size(float? target = null)
    {
        target ??= 8;
        if (HAS_TRAIT(this, TRAIT_NO_GLIDE))
            return;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_UPDATE_GLIDE_SIZE]))
            this._SendSignal(COMSIG_MOVABLE_UPDATE_GLIDE_SIZE, Dm.list(this, target));
        glide_size = Dm.num(target);

        foreach (mob buckled_mob in Dm.each_cast<mob>(buckled_mobs))
            buckled_mob.set_glide_size(target);
    }

    /**
     * meant for movement with zero side effects. only use for objects that are supposed to move "invisibly" (like eye mobs or ghosts)
     * if you want something to move onto a tile with a beartrap or recycler or tripmine or mouse without that object knowing about it at all, use this
     * most of the time you want forceMove()
     */
    // tg: code/game/atoms_movable.dm:676
    public virtual async DmTask<object> abstract_move(atom new_loc = null)
    {
        // RESOLVE_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:15, inline): This should NEVER happen, but, just in case...
        if (Dm.truthy(active_movement))
        {
            object __move_args = active_movement;
            active_movement = null;
            await Dm.Call(this, nameof(Moved), Dm.arglist(__move_args));
        }
        atom old_loc = loc;
        float direction = get_dir(old_loc, new_loc);
        loc = new_loc;
        await Moved(old_loc, direction, TRUE, momentum_change: FALSE);
        return null;
    }

    ////////////////////////////////////////
    // Here's where we rewrite how byond handles movement except slightly different
    // To be removed on step_ conversion
    // All this work to prevent a second bump
    // tg: code/game/atoms_movable.dm:687  (the first of the two definitions of Move on /atom/movable: rules 2.5, 7.5;
    // the second one's ..() calls it)
    public async DmTask<object> Move__d0(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        glide_size_override ??= 0;
        update_dir ??= TRUE;
        object dot = null;
        dot = FALSE;
        if (!Dm.truthy(newloc) || newloc == loc)
            return dot;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_ATTEMPTED_MOVE]))
            this._SendSignal(COMSIG_MOVABLE_ATTEMPTED_MOVE, Dm.list(this, newloc, direction));
        // A mid-movement... movement... occurred, resolve that first.
        // RESOLVE_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:15, inline)
        if (Dm.truthy(active_movement))
        {
            object __move_args = active_movement;
            active_movement = null;
            await Dm.Call(this, nameof(Moved), Dm.arglist(__move_args));
        }

        if (!Dm.truthy(direction))
            direction = get_dir(this, newloc);

        if (Dm.truthy(set_dir_on_move) && !Dm.eq(dir, direction) && Dm.truthy(update_dir))
            await setDir(direction);

        float is_multi_tile_object = Dm.num(GlobalProcs.is_multi_tile_object(this));

        DmList old_locs = null;
        if (Dm.truthy(is_multi_tile_object) && isturf(loc))
        {
            old_locs = locs; // locs is a special list, this is effectively the same as .Copy() but with less steps
            foreach (atom exiting_loc in Dm.each_cast<atom>(old_locs))
            {
                if (!Dm.truthy(await exiting_loc.Exit(this, direction)))
                    return dot;
            }
        }
        else
        {
            if (!Dm.truthy(await loc.Exit(this, direction)))
                return dot;
        }

        DmList new_locs = null;
        if (Dm.truthy(is_multi_tile_object) && isturf(newloc))
        {
            float dx = newloc.x;
            float dy = newloc.y;
            float dz = newloc.z;
            new_locs = block(
                dx, dy, dz,
                dx + ceil(bound_width / 32), dy + ceil(bound_height / 32), dz
            ); // If this is a multi-tile object then we need to predict the new locs and check if they allow our entrance.
            foreach (atom entering_loc in Dm.each_cast<atom>(new_locs))
            {
                if (!Dm.truthy(await entering_loc.Enter(this)))
                    return dot;
                if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_PRE_MOVE]) ? NONE : this._SendSignal(COMSIG_MOVABLE_PRE_MOVE, Dm.list(this, entering_loc))), COMPONENT_MOVABLE_BLOCK_PRE_MOVE)))
                    return dot;
            }
        }
        else // Else just try to enter the single destination.
        {
            if (!Dm.truthy(await newloc.Enter(this)))
                return dot;
            if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_PRE_MOVE]) ? NONE : this._SendSignal(COMSIG_MOVABLE_PRE_MOVE, Dm.list(this, newloc))), COMPONENT_MOVABLE_BLOCK_PRE_MOVE)))
                return dot;
            // TURF-IDENTITY (object-model.md 7.1; M5x slice 5): a PRE_MOVE handler may replace the destination turf (the singularity
            // eats the turf ahead of it, components/singularity.dm:243); tg's newloc, a turf reference, then names the cell's new turf
            newloc = turf.dm_identity(newloc) as atom;
        }

        // Past this is the point of no return
        atom oldloc = loc;
        area oldarea = get_area(oldloc) as area;
        area newarea = get_area(newloc) as area;

        // SET_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:7, inline)
        active_movement = Dm.list(oldloc, direction, FALSE, old_locs);
        loc = newloc;

        dot = TRUE;

        if (Dm.truthy(old_locs)) // This condition will only be true if it is a multi-tile object.
        {
            foreach (atom exited_loc in Dm.each_cast<atom>(Dm.sub(old_locs, new_locs)))
                await exited_loc.Exited(this, direction);
        }
        else // Else there's just one loc to be exited.
            await oldloc.Exited(this, direction);
        if (oldarea != newarea)
            await oldarea.Exited(this, direction);

        if (Dm.truthy(new_locs)) // Same here, only if multi-tile.
        {
            foreach (atom entered_loc in Dm.each_cast<atom>(Dm.sub(new_locs, old_locs)))
                await entered_loc.Entered(this, oldloc, old_locs);
        }
        else
            await newloc.Entered(this, oldloc, old_locs);
        if (oldarea != newarea)
            await newarea.Entered(this, oldarea);

        // RESOLVE_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:15, inline)
        if (Dm.truthy(active_movement))
        {
            object __move_args = active_movement;
            active_movement = null;
            await Dm.Call(this, nameof(Moved), Dm.arglist(__move_args));
        }
        return dot;
    }

    ////////////////////////////////////////

    // tg: code/game/atoms_movable.dm:763  (the second definition, the slot's virtual method: rules 2.5, 7.5)
    [DmParams("newloc", "direct", "glide_size_override", "update_dir")]
    public virtual async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        glide_size_override ??= 0;
        update_dir ??= TRUE;
        object dot = null;
        atom_movable pullee = pulling;
        turf current_turf = loc as turf;
        if (!Dm.truthy(moving_from_pull))
            await check_pulling(z_allowed: TRUE);
        if (!Dm.truthy(loc) || !Dm.truthy(newloc))
            return FALSE;
        atom oldloc = loc;
        //Early override for some cases like diagonal movement
        if (Dm.truthy(glide_size_override) && !Dm.eq(glide_size, glide_size_override))
            set_glide_size(glide_size_override);

        if (loc != newloc)
        {
            if (!Dm.truthy(Dm.bitand(direct, Dm.sub(direct, 1)))) //Cardinal move
                dot = await Move__d0(newloc, direct, glide_size_override, update_dir);
            else //Diagonal move, split it into cardinal moves
            {
                moving_diagonally = FIRST_DIAG_STEP;
                float first_step_dir = 0;
                // The `&& moving_diagonally` checks are so that a forceMove taking
                // place due to a Crossed, Bumped, etc. call will interrupt
                // the second half of the diagonal movement, or the second attempt
                // at a first half if step() fails because we hit something.
                if (Dm.truthy(Dm.bitand(direct, NORTH)))
                {
                    if (Dm.truthy(Dm.bitand(direct, EAST)))
                    {
                        if (Dm.truthy(await step(this, NORTH)) && Dm.truthy(moving_diagonally))
                        {
                            first_step_dir = NORTH;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, EAST);
                        }
                        else if (Dm.truthy(moving_diagonally) && Dm.truthy(await step(this, EAST)))
                        {
                            first_step_dir = EAST;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, NORTH);
                        }
                    }
                    else if (Dm.truthy(Dm.bitand(direct, WEST)))
                    {
                        if (Dm.truthy(await step(this, NORTH)) && Dm.truthy(moving_diagonally))
                        {
                            first_step_dir = NORTH;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, WEST);
                        }
                        else if (Dm.truthy(moving_diagonally) && Dm.truthy(await step(this, WEST)))
                        {
                            first_step_dir = WEST;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, NORTH);
                        }
                    }
                }
                else if (Dm.truthy(Dm.bitand(direct, SOUTH)))
                {
                    if (Dm.truthy(Dm.bitand(direct, EAST)))
                    {
                        if (Dm.truthy(await step(this, SOUTH)) && Dm.truthy(moving_diagonally))
                        {
                            first_step_dir = SOUTH;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, EAST);
                        }
                        else if (Dm.truthy(moving_diagonally) && Dm.truthy(await step(this, EAST)))
                        {
                            first_step_dir = EAST;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, SOUTH);
                        }
                    }
                    else if (Dm.truthy(Dm.bitand(direct, WEST)))
                    {
                        if (Dm.truthy(await step(this, SOUTH)) && Dm.truthy(moving_diagonally))
                        {
                            first_step_dir = SOUTH;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, WEST);
                        }
                        else if (Dm.truthy(moving_diagonally) && Dm.truthy(await step(this, WEST)))
                        {
                            first_step_dir = WEST;
                            moving_diagonally = SECOND_DIAG_STEP;
                            dot = await step(this, SOUTH);
                        }
                    }
                }
                if (moving_diagonally == SECOND_DIAG_STEP)
                {
                    if (!Dm.truthy(dot) && Dm.truthy(set_dir_on_move) && Dm.truthy(update_dir))
                        await setDir(first_step_dir);
                    else if (!Dm.truthy(inertia_moving))
                        await newtonian_move(LocomotionSeam.MovementInertiaAngle(this, direct)); // DV69: continuous bridge crossing angle
                }
                moving_diagonally = 0;
                return dot;
            }
        }

        if (!Dm.truthy(loc) || (loc == oldloc && oldloc != newloc))
        {
            last_move = 0;
            set_currently_z_moving(FALSE, TRUE);
            return dot;
        }

        if (Dm.truthy(dot) && Dm.truthy(pulling) && pulling == pullee && pulling != moving_from_pull) //we were pulling a thing and didn't lose it during our move.
        {
            if (Dm.truthy(pulling.anchored))
                await stop_pulling();
            else
            {
                //puller and pullee more than one tile away or in diagonal position and whatever the pullee is pulling isn't already moving from a pull as it'll most likely result in an infinite loop a la ouroborus.
                if (!Dm.truthy(pulling.pulling?.moving_from_pull))
                {
                    float pull_dir = get_dir(pulling, this);
                    object target_turf = current_turf;

                    // Pulling things down/up stairs. zMove() has flags for check_pulling and stop_pulling calls.
                    // You may wonder why we're not just forcemoving the pulling movable and regrabbing it.
                    // The answer is simple. forcemoving and regrabbing is ugly and breaks conga lines.
                    if (pulling.z != z)
                        target_turf = get_step(pulling, get_dir(pulling, current_turf));

                    if (!Dm.eq(target_turf, current_turf) || (moving_diagonally != SECOND_DIAG_STEP && Dm.truthy(ISDIAGONALDIR(pull_dir))) || get_dist(this, pulling) > 1)
                        await pulling.move_from_pull(this, target_turf as turf, glide_size);
                }
                if (Dm.truthy(pulledby))
                {
                    if (Dm.truthy(pulledby.currently_z_moving))
                        await check_pulling(z_allowed: TRUE);
                    //don't call check_pulling() here at all if there is a pulledby that is not currently z moving
                    //because it breaks stair conga lines, for some fucking reason.
                    //it's fine because the pull will be checked when this whole proc is called by the mob doing the pulling anyways
                }
                else
                    await check_pulling();
            }
        }

        //glide_size strangely enough can change mid movement animation and update correctly while the animation is playing
        //This means that if you don't override it late like this, it will just be set back by the movement update that's called when you move turfs.
        if (Dm.truthy(glide_size_override))
            set_glide_size(glide_size_override);

        last_move = direct;

        if (Dm.truthy(set_dir_on_move) && !Dm.eq(dir, direct) && Dm.truthy(update_dir))
            await setDir(direct);
        if (Dm.truthy(dot) && Dm.truthy(has_buckled_mobs()) && !Dm.truthy(await handle_buckled_mob_movement(loc, direct, glide_size_override))) //movement failed due to buckled mob(s)
            dot = FALSE;

        if (Dm.truthy(currently_z_moving))
        {
            if (Dm.truthy(dot) && loc == newloc)
            {
                turf pitfall = get_turf(this);
                await pitfall.zFall(this, falling_from_move: TRUE);
            }
            else
                set_currently_z_moving(FALSE, TRUE);
        }
        return dot;
    }

    /// Called when src is being moved to a target turf because another movable (puller) is moving around.
    // tg: code/game/atoms_movable.dm:882
    public virtual async DmTask move_from_pull(atom_movable puller = null, turf target_turf = null, object glide_size_override = null)
    {
        moving_from_pull = puller;
        await Move(target_turf, get_dir(this, target_turf), Dm.num_or_null(glide_size_override));
        moving_from_pull = null;
    }

    /**
     * Called after a successful Move(). By this point, we've already moved.
     * Arguments:
     * * old_loc is the location prior to the move. Can be null to indicate nullspace.
     * * movement_dir is the direction the movement took place. Can be NONE if it was some sort of teleport.
     * * The forced flag indicates whether this was a forced move, which skips many checks of regular movement.
     * * The old_locs is an optional argument, in case the moved movable was present in multiple locations before the movement.
     * * momentum_change represents whether this movement is due to a "new" force if TRUE or an already "existing" force if FALSE
     **/
    // tg: code/game/atoms_movable.dm:896
    [ShouldCallParent]
    public virtual async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        FlightSeam.NoteMoved(this); // DV416: a move tg makes itself ends the thing's free motion and its rest point

        if (!Dm.truthy(moving_diagonally) && !Dm.truthy(inertia_moving) && Dm.truthy(momentum_change) && Dm.truthy(movement_dir))
            await newtonian_move(LocomotionSeam.MovementInertiaAngle(this, movement_dir)); // DV69: continuous bridge crossing angle

        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_MOVED]))
            this._SendSignal(COMSIG_MOVABLE_MOVED, Dm.list(this, old_loc, movement_dir, forced, old_locs, momentum_change));

        if (Dm.truthy(old_loc))
        {
            if (Dm.truthy(old_loc._listen_lookup?[COMSIG_ATOM_ABSTRACT_EXITED]))
                old_loc._SendSignal(COMSIG_ATOM_ABSTRACT_EXITED, Dm.list(old_loc, this, movement_dir));
        }
        if (Dm.truthy(loc))
        {
            if (Dm.truthy(loc._listen_lookup?[COMSIG_ATOM_ABSTRACT_ENTERED]))
                loc._SendSignal(COMSIG_ATOM_ABSTRACT_ENTERED, Dm.list(loc, this, old_loc, old_locs));
        }

        turf old_turf = get_turf(old_loc);
        turf new_turf = get_turf(this);

        if (!Dm.eq(old_turf?.z, new_turf?.z))
        {
            float same_z_layer = Dm.num(Dm.eq(GET_TURF_PLANE_OFFSET(old_turf), GET_TURF_PLANE_OFFSET(new_turf)));
            await on_changed_z_level(old_turf, new_turf, same_z_layer);
        }

        if (Dm.truthy(HAS_SPATIAL_GRID_CONTENTS(this)))
        {
            if (Dm.truthy(old_turf) && Dm.truthy(new_turf) && (old_turf.z != new_turf.z
                || GET_SPATIAL_INDEX(old_turf.x) != GET_SPATIAL_INDEX(new_turf.x)
                || GET_SPATIAL_INDEX(old_turf.y) != GET_SPATIAL_INDEX(new_turf.y)))
            {

                SSspatial_grid.exit_cell(this, old_turf);
                SSspatial_grid.enter_cell(this, new_turf);
            }

            else if (Dm.truthy(old_turf) && !Dm.truthy(new_turf))
                SSspatial_grid.exit_cell(this, old_turf);

            else if (Dm.truthy(new_turf) && !Dm.truthy(old_turf))
                SSspatial_grid.enter_cell(this, new_turf);
        }

        return TRUE;
    }

    // Make sure you know what you're doing if you call this
    // You probably want CanPass()
    // tg: code/game/atoms_movable.dm:934
    [DmParams("crossed_atom")]
    public override async DmTask<object> Cross(atom_movable crossed_atom = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_CROSS]) ? NONE : this._SendSignal(COMSIG_MOVABLE_CROSS, Dm.list(this, crossed_atom))), COMPONENT_BLOCK_CROSS)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(crossed_atom._listen_lookup?[COMSIG_MOVABLE_CROSS_OVER]) ? NONE : crossed_atom._SendSignal(COMSIG_MOVABLE_CROSS_OVER, Dm.list(crossed_atom, this))), COMPONENT_BLOCK_CROSS)))
            return FALSE;
        return await CanPass(crossed_atom, get_dir(this, crossed_atom));
    }

    ///default byond proc that is deprecated for us in lieu of signals. do not call
    // tg: code/game/atoms_movable.dm:942
    [DmParams("crossed_atom", "oldloc"), ShouldNotOverride]
    public override void Crossed(atom_movable crossed_atom = null, params object[] __extra)
    {
        object oldloc = Dm.arg(__extra, 0);
        throw Dm.CRASH("atom/movable/Crossed() was called!", "code/game/atoms_movable.dm", 944);
    }

    /**
     * `Uncross()` is a default BYOND proc that is called when something is *going*
     * to exit this atom's turf. It is prefered over `Uncrossed` when you want to
     * deny that movement, such as in the case of border objects, objects that allow
     * you to walk through them in any direction except the one they block
     * (think side windows).
     *
     * While being seemingly harmless, most everything doesn't actually want to
     * use this, meaning that we are wasting proc calls for every single atom
     * on a turf, every single time something exits it, when basically nothing
     * cares.
     *
     * This overhead caused real problems on Sybil round #159709, where lag
     * attributed to Uncross was so bad that the entire master controller
     * collapsed and people made Among Us lobbies in OOC.
     *
     * If you want to replicate the old `Uncross()` behavior, the most apt
     * replacement is [`/datum/element/connect_loc`] while hooking onto
     * [`COMSIG_ATOM_EXIT`].
     */
    // tg: code/game/atoms_movable.dm:966
    [DmParams(), ShouldNotOverride]
    public override object Uncross(atom_movable O = null)
    {
        throw Dm.CRASH("Uncross() should not be being called, please read the doc-comment for it for why.", "code/game/atoms_movable.dm", 968);
    }

    /**
     * default byond proc that is normally called on everything inside the previous turf
     * a movable was in after moving to its current turf
     * this is wasteful since the vast majority of objects do not use Uncrossed
     * use connect_loc to register to COMSIG_ATOM_EXITED instead
     */
    // tg: code/game/atoms_movable.dm:976
    [DmParams("uncrossed_atom"), ShouldNotOverride]
    public override void Uncrossed(atom_movable uncrossed_atom = null)
    {
        throw Dm.CRASH("/atom/movable/Uncrossed() was called", "code/game/atoms_movable.dm", 978);
    }

    // tg: code/game/atoms_movable.dm:980
    public virtual async DmTask<object> Bump(atom bumped_atom = null)
    {
        object dot = null;
        if (!Dm.truthy(bumped_atom))
            throw Dm.CRASH("Bump was called with no argument.", "code/game/atoms_movable.dm", 982);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_BUMP]) ? NONE : this._SendSignal(COMSIG_MOVABLE_BUMP, Dm.list(this, bumped_atom))), COMPONENT_INTERCEPT_BUMPED)))
            return TRUE;
        dot = Bump__d0(bumped_atom);
        if (!QDELETED(throwing))
        {
            throwing.finalize(hit: TRUE, target: bumped_atom);
            dot = TRUE;
        }
        if (QDELETED(bumped_atom))
            return TRUE;
        bumped_atom.Bumped(this);
        return dot;
    }

    // tg: code/game/atoms_movable.dm:993
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);

        if (!Dm.truthy(LAZYLEN(gone.important_recursive_contents)))
            return dot;
        DmList nested_locs = Dm.add(get_nested_locs(this), this) as DmList;
        foreach (object channel in Dm.each(gone.important_recursive_contents))
        {
            foreach (atom_movable location in Dm.each_cast<atom_movable>(nested_locs))
            {
                LAZYINITLIST(ref location.important_recursive_contents);
                DmList recursive_contents = location.important_recursive_contents; // blue hedgehog velocity
                // LAZYINITLIST(recursive_contents[channel]): a list element cannot be passed by ref; its expansion
                // `recursive_contents[channel] ||= list()` (rules 4.5)
                if (!Dm.truthy(recursive_contents[channel]))
                    recursive_contents[channel] = Dm.list();
                Dm.remove_at(recursive_contents, channel, gone.important_recursive_contents[channel]);
                object sw__1 = channel;
                if (Dm.eq(sw__1, RECURSIVE_CONTENTS_CLIENT_MOBS) || Dm.eq(sw__1, RECURSIVE_CONTENTS_HEARING_SENSITIVE))
                {
                    if (!Dm.truthy(length(recursive_contents[channel])))
                        // This relies on a nice property of the linked recursive and gridmap types
                        // They're defined in relation to each other, so they have the same value
                        SSspatial_grid.remove_grid_awareness(location, channel);
                }
                ASSOC_UNSETEMPTY(ref recursive_contents, channel);
                UNSETEMPTY(ref location.important_recursive_contents);
            }
        }
        return dot;
    }

    // tg: code/game/atoms_movable.dm:1014
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = null;
        dot = await base.Entered(arrived, old_loc, old_locs);

        if (!Dm.truthy(LAZYLEN(arrived.important_recursive_contents)))
            return dot;
        DmList nested_locs = Dm.add(get_nested_locs(this), this) as DmList;
        foreach (object channel in Dm.each(arrived.important_recursive_contents))
        {
            foreach (atom_movable location in Dm.each_cast<atom_movable>(nested_locs))
            {
                LAZYINITLIST(ref location.important_recursive_contents);
                DmList recursive_contents = location.important_recursive_contents; // blue hedgehog velocity
                // LAZYINITLIST(recursive_contents[channel]): a list element cannot be passed by ref; its expansion
                // `recursive_contents[channel] ||= list()` (rules 4.5)
                if (!Dm.truthy(recursive_contents[channel]))
                    recursive_contents[channel] = Dm.list();
                object sw__1 = channel;
                if (Dm.eq(sw__1, RECURSIVE_CONTENTS_CLIENT_MOBS) || Dm.eq(sw__1, RECURSIVE_CONTENTS_HEARING_SENSITIVE))
                {
                    if (!Dm.truthy(length(recursive_contents[channel])))
                        SSspatial_grid.add_grid_awareness(location, channel);
                }
                Dm.combine_at(recursive_contents, channel, arrived.important_recursive_contents[channel]);
            }
        }
        return dot;
    }

    ///allows this movable to hear and adds itself to the important_recursive_contents list of itself and every movable loc its in
    // tg: code/game/atoms_movable.dm:1032
    public virtual void become_hearing_sensitive(string trait_source = null)
    {
        trait_source ??= TRAIT_GENERIC;
        float already_hearing_sensitive = Dm.num(HAS_TRAIT(this, TRAIT_HEARING_SENSITIVE));
        ADD_TRAIT(this, TRAIT_HEARING_SENSITIVE, trait_source);
        if (Dm.truthy(already_hearing_sensitive)) // If we were already hearing sensitive, we don't wanna be in important_recursive_contents twice, else we'll have potential issues like one radio sending the same message multiple times
            return;

        foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(this), this)))
        {
            LAZYINITLIST(ref location.important_recursive_contents);
            DmList recursive_contents = location.important_recursive_contents; // blue hedgehog velocity
            if (!Dm.truthy(length(recursive_contents[RECURSIVE_CONTENTS_HEARING_SENSITIVE])))
                SSspatial_grid.add_grid_awareness(location, SPATIAL_GRID_CONTENTS_TYPE_HEARING);
            Dm.append_at(recursive_contents, RECURSIVE_CONTENTS_HEARING_SENSITIVE, Dm.list(this));
        }

        turf our_turf = get_turf(this);
        SSspatial_grid.add_grid_membership(this, our_turf, SPATIAL_GRID_CONTENTS_TYPE_HEARING);
    }

    /**
     * removes the hearing sensitivity channel from the important_recursive_contents list of this and all nested locs containing us if there are no more sources of the trait left
     * since RECURSIVE_CONTENTS_HEARING_SENSITIVE is also a spatial grid content type, removes us from the spatial grid if the trait is removed
     *
     * * trait_source - trait source define or ALL, if ALL, force removes hearing sensitivity. if a trait source define, removes hearing sensitivity only if the trait is removed
     */
    // tg: code/game/atoms_movable.dm:1054
    public virtual void lose_hearing_sensitivity(string trait_source = null)
    {
        trait_source ??= TRAIT_GENERIC;
        if (!HAS_TRAIT(this, TRAIT_HEARING_SENSITIVE))
            return;
        REMOVE_TRAIT(this, TRAIT_HEARING_SENSITIVE, trait_source);
        if (HAS_TRAIT(this, TRAIT_HEARING_SENSITIVE))
            return;

        turf our_turf = get_turf(this);
        /// We get our awareness updated by the important recursive contents stuff, here we remove our membership
        SSspatial_grid.remove_grid_membership(this, our_turf, SPATIAL_GRID_CONTENTS_TYPE_HEARING);

        foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(this), this)))
        {
            DmList recursive_contents = location.important_recursive_contents; // blue hedgehog velocity
            Dm.remove_at(recursive_contents, RECURSIVE_CONTENTS_HEARING_SENSITIVE, this);
            if (!Dm.truthy(length(recursive_contents[RECURSIVE_CONTENTS_HEARING_SENSITIVE])))
                SSspatial_grid.remove_grid_awareness(location, SPATIAL_GRID_CONTENTS_TYPE_HEARING);
            ASSOC_UNSETEMPTY(ref recursive_contents, RECURSIVE_CONTENTS_HEARING_SENSITIVE);
            UNSETEMPTY(ref location.important_recursive_contents);
        }
    }

    ///allows this movable to know when it has "entered" another area no matter how many movable atoms its stuffed into, uses important_recursive_contents
    // tg: code/game/atoms_movable.dm:1074
    public virtual void become_area_sensitive(string trait_source = null)
    {
        trait_source ??= TRAIT_GENERIC;
        if (!HAS_TRAIT(this, TRAIT_AREA_SENSITIVE))
        {
            foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(this), this)))
                LAZYADDASSOCLIST(ref location.important_recursive_contents, RECURSIVE_CONTENTS_AREA_SENSITIVE, this);
        }
        ADD_TRAIT(this, TRAIT_AREA_SENSITIVE, trait_source);
    }

    ///removes the area sensitive channel from the important_recursive_contents list of this and all nested locs containing us if there are no more source of the trait left
    // tg: code/game/atoms_movable.dm:1081
    public virtual void lose_area_sensitivity(string trait_source = null)
    {
        trait_source ??= TRAIT_GENERIC;
        if (!HAS_TRAIT(this, TRAIT_AREA_SENSITIVE))
            return;
        REMOVE_TRAIT(this, TRAIT_AREA_SENSITIVE, trait_source);
        if (HAS_TRAIT(this, TRAIT_AREA_SENSITIVE))
            return;

        foreach (atom_movable location in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(this), this)))
            LAZYREMOVEASSOC(ref location.important_recursive_contents, RECURSIVE_CONTENTS_AREA_SENSITIVE, this);
    }
}

// tg: code/game/atoms_movable.dm:1093  (/mob procs defined in this file, rules 1.9 "interleaved types")
public partial class mob : atom_movable
{
    ///propogates ourselves through our nested contents, similar to other important_recursive_contents procs
    ///main difference is that client contents need to possibly duplicate recursive contents for the clients mob AND its eye
    // tg: code/game/atoms_movable.dm:1093
    public virtual void enable_client_mobs_in_contents()
    {
        foreach (atom_movable movable_loc in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(this), this)))
        {
            LAZYINITLIST(ref movable_loc.important_recursive_contents);
            DmList recursive_contents = movable_loc.important_recursive_contents; // blue hedgehog velocity
            if (!Dm.truthy(length(recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS])))
                SSspatial_grid.add_grid_awareness(movable_loc, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);
            // LAZYINITLIST(recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS]): a list element cannot be passed by ref; its
            // expansion `recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS] ||= list()` (rules 4.5)
            if (!Dm.truthy(recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS]))
                recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS] = Dm.list();
            Dm.combine_at(recursive_contents, RECURSIVE_CONTENTS_CLIENT_MOBS, this);
        }

        turf our_turf = get_turf(this);
        /// We got our awareness updated by the important recursive contents stuff, now we add our membership
        SSspatial_grid.add_grid_membership(this, our_turf, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);
    }

    ///Clears the clients channel of this mob
    // tg: code/game/atoms_movable.dm:1107
    public virtual void clear_important_client_contents()
    {
        turf our_turf = get_turf(this);
        SSspatial_grid.remove_grid_membership(this, our_turf, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);

        foreach (atom_movable movable_loc in Dm.each_cast<atom_movable>(Dm.add(get_nested_locs(this), this)))
        {
            LAZYINITLIST(ref movable_loc.important_recursive_contents);
            DmList recursive_contents = movable_loc.important_recursive_contents; // blue hedgehog velocity
            // LAZYINITLIST(recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS]): its expansion, as above (rules 4.5)
            if (!Dm.truthy(recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS]))
                recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS] = Dm.list();
            Dm.remove_at(recursive_contents, RECURSIVE_CONTENTS_CLIENT_MOBS, this);
            if (!Dm.truthy(length(recursive_contents[RECURSIVE_CONTENTS_CLIENT_MOBS])))
                SSspatial_grid.remove_grid_awareness(movable_loc, SPATIAL_GRID_CONTENTS_TYPE_CLIENTS);
            ASSOC_UNSETEMPTY(ref recursive_contents, RECURSIVE_CONTENTS_CLIENT_MOBS);
            UNSETEMPTY(ref movable_loc.important_recursive_contents);
        }
    }
}

// tg: code/game/atoms_movable.dm:1122
public partial class atom_movable : atom
{
    ///called when this movable becomes the parent of a storage component that is currently being viewed by a player. uses important_recursive_contents
    // tg: code/game/atoms_movable.dm:1122
    public virtual void become_active_storage(datum_storage source = null)
    {
        Dm.NotTranslated("code/game/atoms_movable.dm:1122", "become_active_storage: storage is translated in M5");
    }

    ///called when this movable's storage component is no longer viewed by any players, unsets important_recursive_contents
    // tg: code/game/atoms_movable.dm:1129
    public virtual void lose_active_storage(datum_storage source = null)
    {
        Dm.NotTranslated("code/game/atoms_movable.dm:1129", "lose_active_storage: storage is translated in M5");
    }

    ///Sets the anchored var and returns if it was successfully changed or not.
    // tg: code/game/atoms_movable.dm:1140
    [ShouldCallParent]
    public virtual async DmTask<object> set_anchored(object anchorvalue = null)
    {
        object dot = null;
        if (Dm.eq(anchored, anchorvalue))
            return dot;
        dot = anchored;
        anchored = Dm.num(anchorvalue);
        if (Dm.truthy(anchored) && Dm.truthy(pulledby))
            await pulledby.stop_pulling();
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_SET_ANCHORED]))
            this._SendSignal(COMSIG_MOVABLE_SET_ANCHORED, Dm.list(this, anchorvalue));
        return dot;
    }

    /// Sets the currently_z_moving variable to a new value. Used to allow some zMovement sources to have precedence over others.
    // tg: code/game/atoms_movable.dm:1151
    public virtual object set_currently_z_moving(object new_z_moving_value = null, float? forced = null)
    {
        forced ??= FALSE;
        if (Dm.truthy(forced))
        {
            currently_z_moving = new_z_moving_value;
            return TRUE;
        }
        object old_z_moving_value = currently_z_moving;
        currently_z_moving = max(currently_z_moving, new_z_moving_value);
        return Dm.num(Dm.gt(currently_z_moving, old_z_moving_value));
    }

    // tg: code/game/atoms_movable.dm:1159
    public virtual async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        object dot = null;
        dot = FALSE;
        if (Dm.truthy(destination))
            dot = await doMove(destination);
        else
            throw Dm.CRASH("No valid destination passed into forceMove", "code/game/atoms_movable.dm", 1164);
        return dot;
    }

    // tg: code/game/atoms_movable.dm:1166
    public virtual async DmTask<object> moveToNullspace()
    {
        return await doMove(null);
    }

    // tg: code/game/atoms_movable.dm:1169
    public virtual async DmTask<object> doMove(atom destination = null)
    {
        object dot = null;
        dot = FALSE;
        // RESOLVE_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:15, inline)
        if (Dm.truthy(active_movement))
        {
            object __move_args = active_movement;
            active_movement = null;
            await Dm.Call(this, nameof(Moved), Dm.arglist(__move_args));
        }

        atom oldloc = loc;
        float is_multi_tile = Dm.num(bound_width > ICON_SIZE_X || bound_height > ICON_SIZE_Y);

        // SET_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:7, inline)
        active_movement = Dm.list(oldloc, NONE, TRUE, null);

        if (Dm.truthy(destination))
        {
            ///zMove already handles whether a pull from another movable should be broken.
            if (Dm.truthy(pulledby) && !Dm.truthy(currently_z_moving))
                await pulledby.stop_pulling();

            float same_loc = Dm.num(oldloc == destination);
            area old_area = get_area(oldloc) as area;
            area destarea = get_area(destination) as area;
            float movement_dir = get_dir(this, destination);

            moving_diagonally = 0;

            loc = destination;

            if (!Dm.truthy(same_loc))
            {
                if (loc == oldloc)
                {
                    // when attempting to move an atom A into an atom B which already contains A, BYOND seems
                    // to silently refuse to move A to the new loc. This can really break stuff (see #77067)
                    stack_trace(Dm.text("Attempt to move [] to [] was rejected by BYOND, possibly due to cyclic contents", this, destination), "code/game/atoms_movable.dm", 1196);
                    return FALSE;
                }

                if (Dm.truthy(is_multi_tile) && isturf(destination))
                {
                    // RULE-GAP: tg passes movement_dir, a number, as the turfs' and the area's old_loc argument (:1213, :1216; a
                    // tg quirk, move-chain.md hazard 15); the slot types it atom (slots.tsv), so null goes there. As in tg, locs
                    // is read after loc is set (the new turfs), so the turfs Exited and Entered are those of tg's off-by-one block.
                    float dx = destination.x;
                    float dy = destination.y;
                    float dz = destination.z;
                    DmList new_locs = block(
                        dx, dy, dz,
                        dx + ROUND_UP(bound_width / ICON_SIZE_X), dy + ROUND_UP(bound_height / ICON_SIZE_Y), dz);
                    if (Dm.truthy(old_area) && old_area != destarea)
                        await old_area.Exited(this, movement_dir);
                    foreach (atom left_loc in Dm.each_cast<atom>(Dm.sub(locs, new_locs)))
                        await left_loc.Exited(this, movement_dir);

                    foreach (atom entering_loc in Dm.each_cast<atom>(Dm.sub(new_locs, locs)))
                        await entering_loc.Entered(this, null);

                    if (Dm.truthy(old_area) && old_area != destarea)
                        await destarea.Entered(this, null);
                }
                else
                {
                    if (Dm.truthy(oldloc))
                    {
                        await oldloc.Exited(this, movement_dir);
                        if (Dm.truthy(old_area) && old_area != destarea)
                            await old_area.Exited(this, movement_dir);
                    }
                    await destination.Entered(this, oldloc);
                    if (Dm.truthy(destarea) && old_area != destarea)
                        await destarea.Entered(this, old_area);
                }
            }
            dot = TRUE;
        }

        //If no destination, move the atom into nullspace (don't do this unless you know what you're doing)
        else
        {
            dot = TRUE;

            if (Dm.truthy(oldloc))
            {
                loc = null;
                area old_area = get_area(oldloc) as area;
                if (Dm.truthy(is_multi_tile) && isturf(oldloc))
                {
                    foreach (atom old_loc in Dm.each_cast<atom>(locs))
                        await old_loc.Exited(this, NONE);
                }
                else
                    await oldloc.Exited(this, NONE);

                if (Dm.truthy(old_area))
                    await old_area.Exited(this, NONE);
            }
        }

        // RESOLVE_ACTIVE_MOVEMENT (__DEFINES/movement_info.dm:15, inline)
        if (Dm.truthy(active_movement))
        {
            object __move_args = active_movement;
            active_movement = null;
            await Dm.Call(this, nameof(Moved), Dm.arglist(__move_args));
        }
        return dot;
    }

    /**
     * Called when a movable changes z-levels.
     *
     * Arguments:
     * * old_turf - The previous turf they were on before.
     * * new_turf - The turf they have now entered.
     * * same_z_layer - If their old and new z levels are on the same level of plane offsets or not
     * * notify_contents - Whether or not to notify the movable's contents that their z-level has changed. NOTE, IF YOU SET THIS, YOU NEED TO MANUALLY SET PLANE OF THE CONTENTS LATER
     */
    // tg: code/game/atoms_movable.dm:1255
    [ShouldCallParent]
    public virtual async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        notify_contents ??= TRUE;
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_Z_CHANGED]))
            this._SendSignal(COMSIG_MOVABLE_Z_CHANGED, Dm.list(this, old_turf, new_turf, same_z_layer));

        // If our turfs are on different z "layers", recalc our planes
        if (!Dm.truthy(same_z_layer) && !QDELETED(this))
        {
            SET_PLANE(this, Dm.num(PLANE_TO_TRUE(this.plane)), new_turf);
            // a TON of overlays use planes, and thus require offsets
            // so we do this. sucks to suck
            update_appearance();

            if (Dm.truthy(update_on_z))
            {
                // I so much wish this could be somewhere else. alas, no.
                foreach (image update in Dm.each_cast<image>(update_on_z))
                    SET_PLANE(update, Dm.num(PLANE_TO_TRUE(update.plane)), new_turf);
            }
            if (Dm.truthy(update_overlays_on_z))
            {
                // This EVEN more so
                cut_overlay(update_overlays_on_z);
                // This even more so
                foreach (mutable_appearance update in Dm.each<mutable_appearance>(update_overlays_on_z))
                    SET_PLANE(update, Dm.num(PLANE_TO_TRUE(update.plane)), new_turf);
                add_overlay(update_overlays_on_z);
            }
        }

        if (!Dm.truthy(notify_contents))
            return;

        foreach (atom_movable content in Dm.each_cast<atom_movable>(this)) // Notify contents of Z-transition.
            await content.on_changed_z_level(old_turf, new_turf, same_z_layer);
    }

    /**
     * Called whenever an object moves and by mobs when they attempt to move themselves through space
     * And when an object or action applies a force on src, see [newtonian_move][/atom/movable/proc/newtonian_move]
     *
     * Return FALSE to have src start/keep drifting in a no-grav area and TRUE to stop/not start drifting
     *
     * Mobs should return 1 if they should be able to move of their own volition, see [/client/proc/Move]
     *
     * Arguments:
     * * movement_dir - 0 when stopping or any dir when trying to move
     * * continuous_move - If this check is coming from something in the context of already drifting
     */
    // tg: code/game/atoms_movable.dm:1296
    public virtual async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        movement_dir ??= 0;
        continuous_move ??= FALSE;
        if (Dm.truthy(has_gravity()))
            return TRUE;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_SPACEMOVE]) ? NONE : this._SendSignal(COMSIG_MOVABLE_SPACEMOVE, Dm.list(this, movement_dir, continuous_move))), COMSIG_MOVABLE_STOP_SPACEMOVE)))
            return TRUE;

        if (Dm.truthy(pulledby) && (pulledby.pulledby != this || Dm.truthy(moving_from_pull)))
            return TRUE;

        if (Dm.truthy(throwing))
            return TRUE;

        if (!isturf(loc))
            return TRUE;

        // DEVIATION(DV412): a thing the wind blew into weightlessness drifts on past the hull: no lattice beside it stops its drift
        if (Dm.truthy(continuous_move) && (drift_handler != null ? drift_handler.dm_blown : Dm.num(world.time) - dm_blown_at <= atom_movable.BlownWindow))
            return FALSE;

        if (Dm.truthy(handle_spacemove_grabbing()))
            return TRUE;

        return FALSE;
    }

    // tg: code/game/atoms_movable.dm:1317
    public virtual object handle_spacemove_grabbing()
    {
        if (Dm.truthy(Dm.locate<obj_structure_lattice>(range(1, get_turf(this))))) //Not realistic but makes pushing things in space easier
            return TRUE;
        return null;
    }

    /// Only moves the object if it's under no gravity
    /// Accepts the direction to move, if the push should be instant, and an optional parameter to fine tune the start delay
    /// Drift force determines how much acceleration should be applied. Controlled cap, if set, will ensure that if the object was moving slower than the cap before, it cannot accelerate past the cap from this move.
    // tg: code/game/atoms_movable.dm:1324
    public virtual async DmTask<object> newtonian_move(object inertia_angle = null, float? instant = null, float? start_delay = null, float? drift_force = null, object controlled_cap = null, float? force_loop = null)
    {
        instant ??= FALSE;
        start_delay ??= 0;
        drift_force ??= 1 * NEWTONS;
        force_loop ??= TRUE;
        if (!isturf(loc) || Dm.truthy(await Process_Spacemove(Dm.num_or_null(angle2dir(Dm.num(inertia_angle))), continuous_move: TRUE)))
            return FALSE;

        if (Dm.truthy(drift_handler?.newtonian_impulse(inertia_angle, start_delay, drift_force, Dm.num_or_null(controlled_cap), force_loop)))
            return TRUE;

        Dm.@new<datum_drift_handler>(this, inertia_angle, instant, start_delay, drift_force);
        // Qdeleted = failed to create itself = most likely we have a higher priority loop already
        return Dm.num(!QDELETED(drift_handler));
    }

    // tg: code/game/atoms_movable.dm:1335
    public override void set_explosion_block(object explosion_block = null)
    {
        float old_block = this.explosion_block;
        explosive_resistance -= old_block;
        this.explosion_block = Dm.num(explosion_block);
        explosive_resistance += Dm.num(explosion_block);
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_EXPLOSION_BLOCK_CHANGED]))
            this._SendSignal(COMSIG_MOVABLE_EXPLOSION_BLOCK_CHANGED, Dm.list(this, old_block, explosion_block));
    }

    /**
     * This proc is called when a thrown object makes contact with it's target. It then follows up by calling hitby below.
     */
    // tg: code/game/atoms_movable.dm:1345  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    public virtual object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(throw_impact__body(hit_atom, throwingdatum, dot), dot);
    }

    private async DmTask<object> throw_impact__body(atom hit_atom, datum_thrownthing throwingdatum, DotBox<object> dot)
    {
        // DEVIATION(DV27): the interaction guard (D132, interaction.md 7, P2's site) on the thrown item's own throw_impact: it
        // lands with no effect of its own (the hit atom's hitby is guarded at /atom/hitby).
        if (InertSeam.MissingOverride(this, "throw_impact", throwingdatum?.get_thrower() as mob))
            return dot.Value;
        float hitpush = TRUE;
        object impact_flags = pre_impact(hit_atom, throwingdatum);
        if (Dm.truthy(Dm.bitand(impact_flags, COMPONENT_MOVABLE_IMPACT_NEVERMIND)))
            return dot.Value; // in case a signal interceptor broke or deleted the thing before we could process our hit
        if (Dm.truthy(Dm.bitand(impact_flags, COMPONENT_MOVABLE_IMPACT_FLIP_HITPUSH)))
            hitpush = FALSE;
        object caught = await hit_atom.hitby(this, throwingdatum: throwingdatum, hitpush: hitpush);
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_IMPACT]))
            this._SendSignal(COMSIG_MOVABLE_IMPACT, Dm.list(this, hit_atom, throwingdatum, caught));
        dot.Value = caught;
        return dot.Value;
    }

    ///Called before we attempt to call hitby and send the COMSIG_MOVABLE_IMPACT signal
    // tg: code/game/atoms_movable.dm:1358
    public virtual object pre_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        object impact_flags = (!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_PRE_IMPACT]) ? NONE : this._SendSignal(COMSIG_MOVABLE_PRE_IMPACT, Dm.list(this, hit_atom, throwingdatum)));
        object target_flags = (!Dm.truthy(hit_atom._listen_lookup?[COMSIG_ATOM_PREHITBY]) ? NONE : hit_atom._SendSignal(COMSIG_ATOM_PREHITBY, Dm.list(hit_atom, this, throwingdatum)));
        if (Dm.truthy(Dm.bitand(target_flags, COMSIG_HIT_PREVENTED)))
            impact_flags = Dm.bitor(impact_flags, COMPONENT_MOVABLE_IMPACT_NEVERMIND);
        return impact_flags;
    }

    // tg: code/game/atoms_movable.dm:1365
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        hitpush ??= TRUE;
        if (HAS_TRAIT(this, TRAIT_NO_THROW_HITPUSH))
            hitpush = FALSE;
        if (!Dm.truthy(anchored) && Dm.truthy(hitpush) && (!Dm.truthy(throwingdatum) || (throwingdatum.force >= (Dm.num(move_resist) * MOVE_FORCE_PUSH_RATIO))))
            await step(this, hitting_atom.dir);
        return await base.hitby(hitting_atom, skipcatch, hitpush, blocked, throwingdatum);
    }

    // Calls throw_at after checking that the move strength is greater than the thrown atom's move resist. Identical args.
    // tg: code/game/atoms_movable.dm:1373
    public virtual async DmTask<object> safe_throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null)
    {
        spin ??= TRUE;
        diagonals_first ??= FALSE;
        force ??= MOVE_FORCE_STRONG;
        gentle ??= FALSE;
        if ((Dm.num(force) < (Dm.num(move_resist) * MOVE_FORCE_THROW_RATIO)) || Dm.eq(move_resist, INFINITY))
            return null;
        return await throw_at(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle);
    }

    ///If this returns FALSE then callback will not be called.
    // tg: code/game/atoms_movable.dm:1379
    public virtual async DmTask<object> throw_at(atom target = null, object range = null, object speed = null, atom thrower = null, float? spin = null, float? diagonals_first = null, datum_callback callback = null, float? force = null, float? gentle = null, object quickstart = null, Type throw_datum_typepath = null, params object[] __extra)
    {
        spin ??= TRUE;
        diagonals_first ??= FALSE;
        force ??= MOVE_FORCE_STRONG;
        gentle ??= FALSE;
        quickstart ??= TRUE;
        throw_datum_typepath ??= typeof(datum_thrownthing);
        object dot = null;
        dot = FALSE;

        if (QDELETED(this))
            throw Dm.CRASH("Qdeleted thing being thrown around.", "code/game/atoms_movable.dm", 1383);

        if (!Dm.truthy(target) || Dm.num(speed) <= 0)
            return dot;

        DmList args = Dm.args(target, range, speed, thrower, spin, diagonals_first, callback, force, gentle, quickstart, throw_datum_typepath, __extra);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_PRE_THROW]) ? NONE : this._SendSignal(COMSIG_MOVABLE_PRE_THROW, Dm.list(this, args))), COMPONENT_CANCEL_THROW)))
            return dot;

        if (Dm.truthy(pulledby))
            await pulledby.stop_pulling();

        //They are moving! Wouldn't it be cool if we calculated their momentum and added it to the throw?
        if (ismob(thrower))
        {
            mob thrower_mob = thrower as mob;
            if (Dm.truthy(thrower_mob.last_move) && Dm.truthy(thrower_mob.client) && thrower_mob.client.move_delay >= world.time)
            {
                float user_momentum = Dm.num(thrower_mob.cached_multiplicative_slowdown);
                if (!Dm.truthy(user_momentum)) //no movement_delay, this means they move once per byond tick, lets calculate from that instead.
                    user_momentum = world.tick_lag;

                user_momentum = 1 / user_momentum; // convert from ds to the tiles per ds that throw_at uses.

                if (Dm.truthy(Dm.bitand(get_dir(thrower_mob, target), last_move)))
                {
                    // user_momentum = user_momentum //basically a noop, but needed
                }
                else if (Dm.truthy(Dm.bitand(get_dir(target, thrower_mob), last_move)))
                    user_momentum = -user_momentum; //we are moving away from the target, lets slowdown the throw accordingly
                else
                    user_momentum = 0;

                if (Dm.truthy(user_momentum))
                {
                    //first lets add that momentum to range.
                    range = Dm.num(range) * ((user_momentum / Dm.num(speed)) + 1);
                    //then lets add it to speed
                    speed = Dm.num(speed) + user_momentum;
                    if (Dm.num(speed) <= 0)
                        return dot;//no throw speed, the user was moving too fast.
                }
            }
        }

        dot = TRUE; // No failure conditions past this point.

        object target_zone = null;
        if (QDELETED(thrower))
            thrower = null; //Let's not pass a qdeleting reference if any.
        else if (ismob(thrower))
        {
            mob thrower_mob = thrower as mob;
            target_zone = thrower_mob.zone_selected;
        }

        datum_thrownthing thrown_thing = await Dm.new_async(throw_datum_typepath, this, target, get_dir(this, target), range, speed, thrower, diagonals_first, force, gentle, callback, target_zone) as datum_thrownthing;

        float dist_x = abs(target.x - this.x);
        float dist_y = abs(target.y - this.y);
        float dx = (target.x > this.x) ? EAST : WEST;
        float dy = (target.y > this.y) ? NORTH : SOUTH;

        if (dist_x == dist_y)
            thrown_thing.pure_diagonal = 1;

        else if (dist_x <= dist_y)
        {
            float olddist_x = dist_x;
            float olddx = dx;
            dist_x = dist_y;
            dist_y = olddist_x;
            dx = dy;
            dy = olddx;
        }
        thrown_thing.dist_x = dist_x;
        thrown_thing.dist_y = dist_y;
        thrown_thing.dx = dx;
        thrown_thing.dy = dy;
        thrown_thing.diagonal_error = dist_x / 2 - dist_y;
        thrown_thing.start_time = world.time;

        if (Dm.truthy(pulledby))
            await pulledby.stop_pulling();
        if (Dm.truthy(quickstart) && (Dm.truthy(throwing) || Dm.eq(SSthrowing.state, SS_RUNNING))) //Avoid stack overflow edgecases.
            quickstart = FALSE;
        throwing = thrown_thing;
        if (Dm.truthy(spin))
        {
            // SpinAnimation(5, 1) (__HELPERS/visual_effects.dm:83: one turn of the transform in 0.5 s, as three animate() steps):
            // drawn by the client, never sent as such (M4v slice 4): D127's tumble spins every non-mob entity flagged Thrown
            // (EntityFlags.Thrown) at about two turns a second for its whole flight (ThrowVisual), which covers tg's one turn;
            // a chain sent beside it would turn the picture twice.
            Dm.Deferred("code/game/atoms_movable.dm:1450", "throw_at: SpinAnimation(5, 1): drawn by the client (the tumble of a Thrown entity, D127), never sent as such");
        }

        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_POST_THROW]))
            this._SendSignal(COMSIG_MOVABLE_POST_THROW, Dm.list(this, thrown_thing, spin));
        SSthrowing.processing[this] = thrown_thing;
        if (Dm.eq(SSthrowing.state, SS_PAUSED) && Dm.truthy(length(SSthrowing.currentrun)))
            SSthrowing.currentrun[this] = thrown_thing;
        if (Dm.truthy(quickstart))
            await thrown_thing.tick();
        return dot;
    }

    // tg: code/game/atoms_movable.dm:1467
    public virtual async DmTask<object> handle_buckled_mob_movement(object newloc = null, object direct = null, object glide_size_override = null)
    {
        foreach (mob_living buckled_mob in Dm.each_cast<mob_living>(buckled_mobs))
        {
            if (!Dm.truthy(await buckled_mob.Move(newloc as atom, direct, Dm.num_or_null(glide_size_override)))) //If a mob buckled to us can't make the same move as us
            {
                await Move(buckled_mob.loc, direct); //Move back to its location
                last_move = buckled_mob.last_move;
                return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/game/atoms_movable.dm:1475
    public virtual async DmTask<object> force_pushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        return FALSE;
    }

    // tg: code/game/atoms_movable.dm:1478
    public virtual async DmTask<object> force_push(atom_movable pushed_atom = null, object force = null, object direction = null, float? silent = null)
    {
        force ??= move_force;
        silent ??= FALSE;
        object dot = null;
        dot = await pushed_atom.force_pushed(this, Dm.num_or_null(force), direction);
        if (!Dm.truthy(silent) && Dm.truthy(dot))
            visible_message(span_warning(I18n.T("[] forcefully pushes against []!", this, pushed_atom)), span_warning(I18n.T("You forcefully push against []!", pushed_atom)));
        return dot;
    }

    // tg: code/game/atoms_movable.dm:1483
    public virtual async DmTask<object> move_crush(atom_movable crushed_atom = null, object force = null, object direction = null, float? silent = null)
    {
        force ??= move_force;
        silent ??= FALSE;
        object dot = null;
        dot = await crushed_atom.move_crushed(this, Dm.num_or_null(force), direction);
        if (!Dm.truthy(silent) && Dm.truthy(dot))
            visible_message(span_danger(I18n.T("[] crushes past []!", this, crushed_atom)), span_danger(I18n.T("You crush []!", crushed_atom)));
        return dot;
    }

    // tg: code/game/atoms_movable.dm:1488
    public virtual async DmTask<object> move_crushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        return FALSE;
    }

    // tg: code/game/atoms_movable.dm:1491
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.@in(mover, buckled_mobs))
            return TRUE;
        return dot;
    }

    /// Returns true or false to allow src to move through the blocker, mover has final say
    // tg: code/game/atoms_movable.dm:1497
    [ShouldCallParent, ShouldBePure]
    public virtual object CanPassThrough(atom blocker = null, object movement_dir = null, object blocker_opinion = null)
    {

        object blocking_signal = (!Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_CAN_PASS_THROUGH]) ? NONE : this._SendSignal(COMSIG_MOVABLE_CAN_PASS_THROUGH, Dm.list(this, blocker, movement_dir)));
        if (!Dm.truthy(blocking_signal))
            return blocker_opinion;

        if (Dm.truthy(Dm.bitand(blocking_signal, COMSIG_COMPONENT_PERMIT_PASSAGE)))
            return TRUE;
        else //we have a COMSIG_COMPONENT_REFUSE_PASSAGE but like its either this or that, unlike someone wanna adds half-passing through but fuck you
            return FALSE;
    }

    /// called when this atom is removed from a storage item, which is passed on as S. The loc variable is already set to the new destination before this is called.
    // tg: code/game/atoms_movable.dm:1511
    public virtual void on_exit_storage(datum_storage master_storage = null)
    {
        return;
    }

    /// called when this atom is added into a storage item, which is passed on as S. The loc variable is already set to the storage item.
    // tg: code/game/atoms_movable.dm:1515
    public virtual void on_enter_storage(datum_storage master_storage = null)
    {
        return;
    }

    // tg: code/game/atoms_movable.dm:1518  (translated in M4b slice 1, the zero-gravity drift)
    public virtual async DmTask<object> get_spacemove_backup(params object[] __extra)
    {
        atom secondary_backup = null;
        foreach (object checked_range in Dm.each(orange(1, get_turf(this))))
        {
            if (isarea(checked_range))
                continue;
            if (isturf(checked_range))
            {
                turf turf = checked_range as turf;
                if (!Dm.truthy(turf.density))
                    continue;
                if (Dm.@in(get_dir(this, turf), GLOB.cardinals))
                    return turf;
                secondary_backup = turf;
                continue;
            }
            atom_movable checked_atom = checked_range as atom_movable;
            if (Dm.truthy(checked_atom.density) || !Dm.truthy(await checked_atom.CanPass(this, get_dir(this, checked_atom))))
            {
                if (Dm.eq(checked_atom.last_pushoff, world.time))
                    continue;
                if (Dm.@in(get_dir(this, checked_atom), GLOB.cardinals))
                    return checked_atom;
                secondary_backup = checked_atom;
            }
        }
        return secondary_backup;
    }

    ///called when a mob resists while inside a container that is itself inside something.
    // tg: code/game/atoms_movable.dm:1541
    public virtual async DmTask<object> relay_container_resist_act(mob_living user = null, obj container = null)
    {
        return null;
    }


    // tg: code/game/atoms_movable.dm:1545
    public virtual async DmTask do_attack_animation(atom attacked_atom = null, object visual_effect_icon = null, obj_item used_item = null, object no_effect = null, float? fov_effect = null, object item_animation_override = null)
    {
        fov_effect ??= TRUE;
        if (!Dm.truthy(no_effect) && (Dm.truthy(visual_effect_icon) || Dm.truthy(used_item)))
            do_item_attack_animation(attacked_atom, visual_effect_icon, used_item, animation_type: item_animation_override);

        if (attacked_atom == this)
            return; //don't do an animation if attacking self
        float pixel_x_diff = 0;
        float pixel_y_diff = 0;
        float turn_dir = 1;

        float direction = get_dir(this, attacked_atom);
        if (Dm.truthy(Dm.bitand(direction, NORTH)))
        {
            pixel_y_diff = 8;
            turn_dir = prob(50) ? -1 : 1;
        }
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
        {
            pixel_y_diff = -8;
            turn_dir = prob(50) ? -1 : 1;
        }

        if (Dm.truthy(Dm.bitand(direction, EAST)))
            pixel_x_diff = 8;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
        {
            pixel_x_diff = -8;
            turn_dir = -1;
        }

        if (Dm.truthy(fov_effect))
        {
            // play_fov_effect(attacked_atom, 5, "attack") (modules/mob/living/fov.dm): GAP(lead): not declared; the field of
            // view's attack cue is not drawn by the 3D client (presentation, a deferred leaf).
            Dm.Deferred("code/game/atoms_movable.dm:1568", "do_attack_animation: play_fov_effect (presentation)");
        }

        matrix initial_transform = Dm.@new<matrix>(transform);
        matrix rotated_transform = (transform as matrix).Turn(15 * turn_dir);
        animate(this, Dm.named("pixel_x", pixel_x + pixel_x_diff), Dm.named("pixel_y", pixel_y + pixel_y_diff), Dm.named("transform", rotated_transform), Dm.named("time", 1), Dm.named("easing", Dm.bitor(BACK_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        animate(Dm.named("pixel_x", pixel_x - pixel_x_diff), Dm.named("pixel_y", pixel_y - pixel_y_diff), Dm.named("transform", initial_transform), Dm.named("time", 2), Dm.named("easing", SINE_EASING), Dm.named("flags", ANIMATION_PARALLEL));
    }

    /* Language procs
    * Unless you are doing something very specific, these are the ones you want to use.
    */

    /// Gets or creates the relevant language holder. For mindless atoms, gets the local one. For atom with mind, gets the mind one.
    // tg: code/game/atoms_movable.dm:1582
    public virtual datum_language_holder get_language_holder()
    {
        if (QDELING(this))
            throw Dm.CRASH("get_language_holder() called on a QDELing atom, this will try to re-instantiate the language holder that's about to be deleted, which is bad.", "code/game/atoms_movable.dm", 1585);

        if (!Dm.truthy(language_holder))
            language_holder = Dm.@new(initial_language_holder, this) as datum_language_holder;
        return language_holder;
    }

    /// Grants the supplied language and sets omnitongue true.
    // tg: code/game/atoms_movable.dm:1593
    public virtual object grant_language(object language = null, float? language_flags = null, string source = null)
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_ATOM;
        return get_language_holder().grant_language(language, language_flags, source);
    }

    /// Grants every language.
    // tg: code/game/atoms_movable.dm:1597
    public virtual object grant_all_languages(float? language_flags = null, float? grant_omnitongue = null, string source = null)
    {
        language_flags ??= ALL;
        grant_omnitongue ??= TRUE;
        source ??= LANGUAGE_MIND;
        return get_language_holder().grant_all_languages(language_flags, grant_omnitongue, source);
    }

    /// Grants partial understanding of a language.
    // tg: code/game/atoms_movable.dm:1601
    public virtual object grant_partial_language(object language = null, float? amount = null, string source = null)
    {
        amount ??= 50;
        source ??= LANGUAGE_ATOM;
        return get_language_holder().grant_partial_language(language, amount, source);
    }

    /// Removes a single language.
    // tg: code/game/atoms_movable.dm:1605
    public virtual object remove_language(object language = null, float? language_flags = null, string source = null)
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_ALL;
        return get_language_holder().remove_language(language, language_flags, source);
    }

    /// Removes every language and sets omnitongue false.
    // tg: code/game/atoms_movable.dm:1609
    public virtual object remove_all_languages(string source = null, float? remove_omnitongue = null)
    {
        source ??= LANGUAGE_ALL;
        remove_omnitongue ??= FALSE;
        return get_language_holder().remove_all_languages(source, remove_omnitongue);
    }

    /// Removes partial understanding of a language.
    // tg: code/game/atoms_movable.dm:1613
    public virtual object remove_partial_language(object language = null, string source = null)
    {
        source ??= LANGUAGE_ALL;
        return get_language_holder().remove_partial_language(language, source);
    }

    /// Removes all partial languages.
    // tg: code/game/atoms_movable.dm:1617
    public virtual object remove_all_partial_languages(string source = null)
    {
        source ??= LANGUAGE_ALL;
        return get_language_holder().remove_all_partial_languages(source);
    }

    /// Adds a language to the blocked language list. Use this over remove_language in cases where you will give languages back later.
    // tg: code/game/atoms_movable.dm:1621
    public virtual object add_blocked_language(object language = null, float? language_flags = null, string source = null)
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_ATOM;
        return get_language_holder().add_blocked_language(language, language_flags, source);
    }

    /// Removes a language from the blocked language list.
    // tg: code/game/atoms_movable.dm:1625
    public virtual object remove_blocked_language(object language = null, float? language_flags = null, string source = null)
    {
        language_flags ??= ALL;
        source ??= LANGUAGE_ATOM;
        return get_language_holder().remove_blocked_language(language, language_flags, source);
    }

    /// Checks if atom has the language. If spoken is true, only checks if atom can speak the language.
    // tg: code/game/atoms_movable.dm:1629
    public virtual object has_language(object language = null, object flags_to_check = null)
    {
        return get_language_holder().has_language(language, flags_to_check);
    }

    /// Checks if atom has the language. If spoken is true, only checks if atom can speak the language.
    // tg: code/game/atoms_movable.dm:1633
    public virtual object has_partial_language(object language = null)
    {
        return get_language_holder().has_partial_language(language);
    }

    /// Checks if atom can speak the language.
    // tg: code/game/atoms_movable.dm:1637
    public virtual object can_speak_language(object language = null)
    {
        return get_language_holder().can_speak_language(language);
    }

    /// Returns the result of tongue specific limitations on spoken languages.
    // tg: code/game/atoms_movable.dm:1641  (language_path decided Type, rules 3.3 P1: slots.tsv lists it undecided; every
    // caller passes a language typepath, _language_holder.dm:241, carbon_say.dm:13)
    public virtual object could_speak_language(Type language_path = null)
    {
        return TRUE;
    }

    /// Returns selected language, if it can be spoken, or finds, sets and returns a new selected language if possible.
    // tg: code/game/atoms_movable.dm:1645
    public virtual object get_selected_language()
    {
        return get_language_holder().get_selected_language();
    }

    /// Gets a random understood language, useful for hallucinations and such.
    // tg: code/game/atoms_movable.dm:1649
    public virtual object get_random_understood_language()
    {
        return get_language_holder().get_random_understood_language();
    }

    /// Gets a lazylist of all mutually understood languages.
    // tg: code/game/atoms_movable.dm:1653
    public virtual object get_partially_understood_languages()
    {
        return get_language_holder().best_mutual_languages;
    }

    /// Gets a random spoken language, useful for forced speech and such.
    // tg: code/game/atoms_movable.dm:1658
    public virtual object get_random_spoken_language()
    {
        return get_language_holder().get_random_spoken_language();
    }

    /// Gets a list of all understood languages, excluding any blocked languages
    // tg: code/game/atoms_movable.dm:1662
    public virtual object get_understood_languages()
    {
        return (get_language_holder().get_understood_languages() is var or__1 && Dm.truthy(or__1) ? or__1 : Dm.list());
    }

    /// Copies all languages into the supplied atom/language holder. Source should be overridden when you
    /// do not want the language overwritten by later atom updates or want to avoid blocked languages.
    // tg: code/game/atoms_movable.dm:1667
    public virtual object copy_languages(object from_holder = null, object source_override = null)
    {
        if (ismovable(from_holder))
        {
            atom_movable thing = from_holder as atom_movable;
            from_holder = thing.get_language_holder();
        }

        return get_language_holder().copy_languages(from_holder as datum_language_holder, source_override as string);
    }

    /// Sets the passed path as the active language
    /// Returns the currently selected language if successful, if the language was not valid, returns null
    // tg: code/game/atoms_movable.dm:1676
    public virtual object set_active_language(object language_path = null)
    {
        datum_language_holder our_holder = get_language_holder();
        our_holder.selected_language = language_path;

        return our_holder.get_selected_language(); // verifies its validity, returns it if successful.
    }

    /**
     * Randomizes our atom's language to an uncommon language if:
     * - They are on the station Z level
     * OR
     * - They are on the escape shuttle
     */
    // tg: code/game/atoms_movable.dm:1688
    public virtual object randomize_language_if_on_station()
    {
        turf atom_turf = get_turf(this);
        object atom_area = get_area(this);

        if (atom_turf == null) // some machines spawn in nullspace
            return FALSE;

        if (!Dm.truthy(is_station_level(atom_turf.z)) && atom_area is not area_shuttle_escape)
        {
            // Why snowflake check for escape shuttle? Well, a lot of shuttles spawn with machines
            // but docked at centcom, and I wanted those machines to also speak funny languages
            return FALSE;
        }
        grant_random_uncommon_language();
        return TRUE;
    }

    /// Teaches a random non-common language and sets it as the active language
    // tg: code/game/atoms_movable.dm:1703
    public virtual object grant_random_uncommon_language(object source = null)
    {
        // gaps L2: the list holds the roundstart species' languages but common (_species.dm:219); while the human is the only
        // roundstart species it is empty, as tg's with only humans selectable
        if (!Dm.truthy(length(GLOB.uncommon_roundstart_languages)))
            return FALSE;
        object picked = pick(GLOB.uncommon_roundstart_languages);
        grant_language(picked, source: source as string);
        set_active_language(picked);
        return null;
    }

    /* End language procs */

    /**
     * Returns an atom's power cell, if it has one. Overload for individual items.
     * Args
     *
     * * /atom/movable/interface - the atom that is trying to interact with this cell
     * * mob/user - the mob that is holding the interface
     */
    // tg: code/game/atoms_movable.dm:1720
    public virtual object get_cell(atom_movable @interface = null, mob user = null)
    {
        return null;
    }

    // tg: code/game/atoms_movable.dm:1723
    public virtual object can_be_pulled(object user = null, object force = null)
    {
        if (Dm.eq(this, user) || !isturf(loc))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_ATOM_CAN_BE_PULLED]) ? NONE : this._SendSignal(COMSIG_ATOM_CAN_BE_PULLED, Dm.list(this, user))), COMSIG_ATOM_CANT_PULL)))
            return FALSE;
        if (Dm.truthy(anchored) || Dm.truthy(throwing))
            return FALSE;
        if (Dm.lt(force, Dm.num(move_resist) * MOVE_FORCE_PULL_RATIO))
            return FALSE;
        return TRUE;
    }

    /**
     * Updates the grab state of the movable
     *
     * This exists to act as a hook for behaviour
     */
    // tg: code/game/atoms_movable.dm:1739
    public virtual async DmTask<object> setGrabState(object newstate = null)
    {
        object dot = null;
        if (Dm.eq(newstate, grab_state))
            return dot;
        dot = grab_state;
        grab_state = Dm.num(newstate);
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOVABLE_SET_GRAB_STATE]))
            this._SendSignal(COMSIG_MOVABLE_SET_GRAB_STATE, Dm.list(this, grab_state));
        if (Dm.truthy(pulling._listen_lookup?[COMSIG_MOVABLE_CHANGED_GRABBED_STATE]))
            pulling._SendSignal(COMSIG_MOVABLE_CHANGED_GRABBED_STATE, Dm.list(pulling, grab_state));
        switch (grab_state) // Current state.
        {
            case GRAB_PASSIVE:
                pulling.remove_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), CHOKEHOLD_TRAIT);
                if (Dm.ge(dot, GRAB_NECK)) // Previous state was a a neck-grab or higher.
                    REMOVE_TRAIT(pulling, TRAIT_FLOORED, CHOKEHOLD_TRAIT);
                break;
            case GRAB_AGGRESSIVE:
                if (Dm.ge(dot, GRAB_NECK)) // Grab got downgraded.
                    REMOVE_TRAIT(pulling, TRAIT_FLOORED, CHOKEHOLD_TRAIT);
                else // Grab got upgraded from a passive one.
                    pulling.add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), CHOKEHOLD_TRAIT);
                break;
            case GRAB_NECK or GRAB_KILL:
                if (Dm.le(dot, GRAB_AGGRESSIVE))
                    ADD_TRAIT(pulling, TRAIT_FLOORED, CHOKEHOLD_TRAIT);
                break;
        }
        return dot;
    }

    /**
     * Adds the deadchat_plays component to this atom with simple movement commands.
     *
     * Returns the component added.
     * Arguments:
     * * mode - Either ANARCHY_MODE or DEMOCRACY_MODE passed to the deadchat_control component. See [/datum/component/deadchat_control] for more info.
     * * cooldown - The cooldown between command inputs passed to the deadchat_control component. See [/datum/component/deadchat_control] for more info.
     */
    // tg: code/game/atoms_movable.dm:1768
    public virtual async DmTask<object> deadchat_plays(float? mode = null, float? cooldown = null)
    {
        mode ??= ANARCHY_MODE;
        cooldown ??= 12 * SECONDS;
        return await AddComponent(typeof(datum_component_deadchat_control_cardinal_movement), mode, Dm.list(), cooldown);
    }

    // tg: code/game/atoms_movable.dm:1771
    public override object vv_get_dropdown()
    {
        Dm.NotTranslated("code/game/atoms_movable.dm:1771", "vv_get_dropdown: view variables are admin tooling (M10+, rules 13)");
        return null;
    }

    // tg: code/game/atoms_movable.dm:1784
    public override async DmTask<object> vv_do_topic(DmList href_list = null)
    {
        Dm.NotTranslated("code/game/atoms_movable.dm:1784", "vv_do_topic: view variables are admin tooling (M10+, rules 13)");
        return null;
    }

    /**
    * A wrapper for setDir that should only be able to fail by living mobs.
    *
    * Called from [/atom/movable/proc/keyLoop], this exists to be overwritten by living mobs with a check to see if we're actually alive enough to change directions
    */
    // tg: code/game/atoms_movable.dm:1852
    public virtual async DmTask keybind_face_direction(object direction = null)
    {
        await setDir(direction);
    }

    ///This handles special behavior that happens when the movable is used in crafting (slapcrafting and UI, not sheets or lathes or processing with a tool)
    // tg: code/game/atoms_movable.dm:1856
    [ShouldCallParent]
    public virtual async DmTask used_in_craft(atom result = null, datum_crafting_recipe current_recipe = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_USED_IN_CRAFT]))
            this._SendSignal(COMSIG_ATOM_USED_IN_CRAFT, Dm.list(this, result));
    }
}

public static partial class GlobalProcs
{
    /*
     * Compare two lists of factions, returning true if any match
     *
     * If exact match is passed through we only return true if both faction lists match equally
     */
    // tg: code/game/atoms_movable.dm:1865
    public static object faction_check(DmList faction_A = null, DmList faction_B = null, DmList allies_A = null, DmList allies_B = null, object exact_match = null)
    {
        if (!Dm.truthy(exact_match))
            return Dm.truthy(LAZYLEN(Dm.bitand(faction_A, faction_B))) ? LAZYLEN(Dm.bitand(faction_A, faction_B)) : LAZYLEN(Dm.bitand(allies_A, allies_B));
        else
        {
            if (LAZYLEN(Dm.bitand(faction_A, faction_B)) != LAZYLEN(faction_A))
                return FALSE; //if they're not the same len(gth) or we don't have a len, then this isn't an exact match.
            return LAZYLEN(Dm.bitand(allies_A, allies_B));
        }
    }
}

// tg: code/game/atoms_movable.dm:1878
public partial class atom_movable : atom
{
    /**
     * Check if the other atom/movable has any factions the same as us. Defined at the atom/movable level so it can be defined for just about anything.
     *
     * If exact match is set, then all our factions must match exactly
     */
    // tg: code/game/atoms_movable.dm:1878
    public virtual object faction_check_atom(atom_movable target = null, object exact_match = null)
    {
        if (Dm.truthy(exact_match))
        {
            DmList allies_src = LAZYCOPY(allies);
            DmList allies_target = LAZYCOPY(target.allies);
            if (!Dm.@in(Dm.text("[]", REF(this)), allies_target)) //if they don't have our ref faction, remove it from our factions list.
                Dm.remove(ref allies_src, Dm.text("[]", REF(this))); //if we don't do this, we'll never have an exact match.
            if (!Dm.@in(Dm.text("[]", REF(target)), allies_src))
                Dm.remove(ref allies_target, Dm.text("[]", REF(target))); //same thing here.
            return FAST_FACTION_CHECK(faction, target.faction, allies, target.allies, TRUE);
        }
        else
            return FAST_FACTION_CHECK(faction, target.faction, allies, target.allies, FALSE);
    }

    /*
     * Sets atom's allies list to be the provided list of faction strings. Returns TRUE if successful.
     */
    // tg: code/game/atoms_movable.dm:1893
    public virtual object set_allies(object ally_list = null)
    {
        if (!islist(ally_list) && ally_list != null)
        {
            stack_trace(Dm.text("Tried to call set_allies on [] with a non-list arg. Use add_ally([]) instead.", this, ally_list), "code/game/atoms_movable.dm", 1895);
            return FALSE;
        }

        if (length(ally_list) == 0 && ally_list != null) // empty list, should just null it in that case
        {
            allies = null;
            return TRUE;
        }

        allies = ally_list as DmList;
        return TRUE;
    }

    /*
     * Adds an ally or list of allies to the allies list. Automatically converts target to ref if it is an atom.
     * Returns TRUE if something was actually added, false otherwise
     */
    // tg: code/game/atoms_movable.dm:1910
    public virtual object add_ally(object ally_or_allies = null)
    {
        float old_length = LAZYLEN(allies);
        if (!isatom(ally_or_allies))
            LAZYOR(ref allies, ally_or_allies);
        else
            LAZYOR(ref allies, Dm.text("[]", REF(ally_or_allies)));

        return Dm.num(LAZYLEN(allies) != old_length);
    }

    /*
     * Removes an ally or list of allies from the allies list. Automatically converts target to ref if it is an atom.
     * Returns TRUE if something was actually added, false otherwise
     */
    // tg: code/game/atoms_movable.dm:1923
    public virtual object remove_ally(object target = null)
    {
        float old_length = LAZYLEN(allies);
        if (!Dm.truthy(old_length))
            return FALSE;

        if (!isatom(target))
            LAZYREMOVE(ref allies, target);
        else
            LAZYREMOVE(ref allies, Dm.text("[]", REF(target)));

        return Dm.num(old_length != LAZYLEN(allies));
    }

    /*
     * Returns TRUE if the ally or allies in list are in our allies list
     * If match_all is set, we have to match everything in the provided list arg.
     */
    // tg: code/game/atoms_movable.dm:1939
    public virtual object has_ally(object ally_or_allies = null, object match_all = null)
    {
        if (!Dm.truthy(LAZYLEN(allies)))
            return FALSE;

        if (islist(ally_or_allies))
        {
            if (Dm.truthy(match_all))
            {
                float match_count = FAST_FACTION_CHECK(null, null, allies, ally_or_allies, TRUE);
                return Dm.num(match_count == LAZYLEN(ally_or_allies));
            }
            else
                return FAST_FACTION_CHECK(null, null, allies, ally_or_allies, FALSE);
        }
        else if (isatom(ally_or_allies))
            return Dm.num(Dm.@in(Dm.text("[]", REF(ally_or_allies)), allies));
        else
            return Dm.num(Dm.@in(ally_or_allies, allies));
    }

    /**
     * Returns the faction list of this atom/movable
     */
    // tg: code/game/atoms_movable.dm:1957
    public virtual object get_faction()
    {
        return faction;
    }

    /**
     * Sets atom's faction list to be the provided list of faction strings. Returns TRUE if successful.
     */
    // tg: code/game/atoms_movable.dm:1963
    public virtual object set_faction(object factions = null)
    {
        if (factions == faction) // Same list in memory - early return
            return TRUE;
        if (!islist(factions) && factions != null)
        {
            stack_trace(Dm.text("Tried to call set_faction on [] with a non-list arg. Use add_faction([]) instead.", this, factions), "code/game/atoms_movable.dm", 1966);
            return FALSE;
        }

        if (length(factions) == 0) // empty list, should just null it in that case
        {
            faction = null;
            return TRUE;
        }

        faction = string_list(factions as DmList) as DmList;
        return TRUE;
    }

    /**
     * Adds a single faction string or list of faction strings to the atom's faction list. Returns TRUE if something was added.
     */
    // tg: code/game/atoms_movable.dm:1980
    public virtual object add_faction(object faction_or_factions = null)
    {
        DmList faction_copy = LAZYLISTDUPLICATE(faction); // Copy so we are not mutating the cached list
        LAZYOR(ref faction_copy, faction_or_factions);

        // If OR didn't add anything, do nothing
        if (LAZYLEN(faction_copy) == LAZYLEN(faction))
            return FALSE;

        faction = string_list(faction_copy) as DmList;
        return TRUE;
    }

    /**
     * Removes a single faction string or list of faction strings from the atom's faction list. Returns TRUE if something was removed.
     */
    // tg: code/game/atoms_movable.dm:1994
    public virtual object remove_faction(object faction_or_factions = null)
    {
        float old_length = LAZYLEN(faction);
        if (!Dm.truthy(old_length))
            return FALSE;

        DmList faction_copy = LAZYLISTDUPLICATE(faction); // Copy so we are not mutating the cached list
        LAZYREMOVE(ref faction_copy, faction_or_factions);

        float new_length = LAZYLEN(faction_copy);
        // If nothing remains in the copy, null the actual list too.
        if (!Dm.truthy(new_length))
        {
            faction = null;
            return TRUE;
        }

        if (old_length == new_length) // Nothing was removed, do nothing
            return FALSE;

        faction = string_list(faction_copy) as DmList;

        return TRUE;
    }

    /**
     * Returns TRUE if the faction or factions in list are in our faction list.
     * If match_all is set, we have to match everything in the provided list arg.
     */
    // tg: code/game/atoms_movable.dm:2019
    public virtual object has_faction(object faction_or_factions = null, object match_all = null)
    {
        if (!Dm.truthy(LAZYLEN(faction)))
            return FALSE;

        if (islist(faction_or_factions))
        {
            if (Dm.truthy(match_all))
            {
                float match_count = FAST_FACTION_CHECK(faction, faction_or_factions, null, null, TRUE);
                return Dm.num(match_count == LAZYLEN(faction_or_factions));
            }
            else
                return FAST_FACTION_CHECK(faction, faction_or_factions, null, null, FALSE);
        }
        else
            return Dm.num(Dm.@in(faction_or_factions, faction));
    }

    /**
     * Returns TRUE if any of the factions or allies are in our faction list.
     * If match_all is set, we have to match everything in the provided list arg.
     */
    // tg: code/game/atoms_movable.dm:2037
    public virtual object has_faction_or_allies(object faction_or_factions = null, object allies_list = null, object match_all = null)
    {
        if (!Dm.truthy(LAZYLEN(faction_or_factions)))
            return FALSE;

        if (islist(faction_or_factions))
        {
            if (Dm.truthy(match_all))
            {
                float match_count = FAST_FACTION_CHECK(faction, faction_or_factions, allies, allies_list, TRUE);
                return Dm.num(match_count == LAZYLEN(faction_or_factions) + LAZYLEN(allies_list));
            }
            else
                return FAST_FACTION_CHECK(faction, faction_or_factions, allies, allies_list, FALSE);
        }
        else
            return Dm.num(Dm.@in(faction_or_factions, faction) && Dm.truthy(FAST_FACTION_CHECK(null, null, allies, allies_list, match_all)));
    }

    /**
     * Opens the modify faction ui.
     */
    // tg: code/game/atoms_movable.dm:2054
    public virtual async DmTask<object> edit_faction(mob user = null)
    {
        Dm.NotTranslated("code/game/atoms_movable.dm:2054", "edit_faction: an admin tgui prompt (M10+)");
        return null;
    }

    /**
     * Outputs the factions list as text
     */
    // tg: code/game/atoms_movable.dm:2083
    public virtual object faction_to_text()
    {
        Dm.NotTranslated("code/game/atoms_movable.dm:2083", "faction_to_text: factions and allies are translated in M2 (mob AI)");
        return null;
    }
}
