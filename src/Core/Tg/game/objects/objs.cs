// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/objs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of package C (wave 4): the var block and tg's overrides of BYOND's /obj defaults
// (dm_tg_vars, rules 10.2 "Builtin types", written by the W4.0 contracts), Initialize, Destroy, the air passthroughs,
// get_dumping_location, check_on_table and the small defaults are translated; SScameras and SStgui calls are deferred
// leaves (security cameras, M9; tgui, rules 13). M5 slice 1: handle_internal_lifeform (a mob breathing in a locker). Procs of later milestones are Dm.NotTranslated with the milestone named:
// freezing (M10+, the frozen element), wrenching (M5), ricochets (M6), reagents (M8), VV (rules 13); shock is the M7 shocks slice's. Overrides
// whose roots are not declared in the port yet (vv_edit_var, singularity_pull,
// apply_single_mat_effect, remove_single_mat_effect) and the procs whose parameter types
// are not ported (plunger_act, change_material_strength) are not declared; each has a comment at its place. The
// [DmPath] of a BYOND builtin type is on its engine part in src/Core/Tg/Builtin/. attack_ghost came with the ghost's click (E2, D287).

// tg: code/game/objects/objs.dm:2
public partial class obj : atom_movable
{
    // tg: code/game/objects/objs.dm:2-5, :51  tg's overrides of BYOND's /obj defaults (rules 10.2, "Builtin types")
    private partial void dm_tg_vars()
    {
        abstract_type = typeof(obj);
        animate_movement = SLIDE_STEPS;
        speech_span = SPAN_ROBOT;
        uses_integrity = TRUE;
    }

    // tg: code/game/objects/objs.dm:6
    public float obj_flags = CAN_BE_HIT;

    /// Extra examine line to describe controls, such as right-clicking, left-clicking, etc.
    // tg: code/game/objects/objs.dm:9
    public object desc_controls;

    /// The context returned when an attack against this object doesn't deal any traditional damage to the object.
    // tg: code/game/objects/objs.dm:12
    public string no_damage_feedback = "without leaving a mark";
    /// Icon to use as a 32x32 preview in crafting menus and such
    // tg: code/game/objects/objs.dm:14
    public object icon_preview;
    // tg: code/game/objects/objs.dm:15
    public object icon_state_preview;
    /// The vertical pixel_z offset applied when the object is anchored on a tile with table
    /// Ignored when set to 0 - to avoid shifting directional wall-mounted objects above tables
    // tg: code/game/objects/objs.dm:18
    public float anchored_tabletop_offset = 0;

    // tg: code/game/objects/objs.dm:20
    public string damtype = BRUTE;
    // tg: code/game/objects/objs.dm:21
    public float force = 0;

    /// How good a given object is at causing wounds on carbons. Higher values equal better shots at creating serious wounds.
    // tg: code/game/objects/objs.dm:24
    public float wound_bonus = 0;
    /// If this attacks a human with no wound armor on the affected body part, add this to the wound mod. Some attacks may be significantly worse at wounding if there's even a slight layer of armor to absorb some of it vs bare flesh
    // tg: code/game/objects/objs.dm:26
    public float exposed_wound_bonus = 0;

    /// A multiplier to an object's force when used against a structure, vehicle, machine, or robot.
    /// Use [/obj/proc/get_demolition_modifier] to get the value.
    // tg: code/game/objects/objs.dm:30
    public float demolition_mod = 1;

    /// Cached custom fire overlay
    // tg: code/game/objects/objs.dm:33
    public object custom_fire_overlay;
    /// Particles this obj uses when burning, if any
    // tg: code/game/objects/objs.dm:35
    public Type burning_particles;

    // tg: code/game/objects/objs.dm:37
    public object drag_slowdown; // Amount of multiplicative slowdown applied if pulled. >1 makes you slower, <1 makes you faster.

    /// Map tag for something.  Tired of it being used on snowflake items.  Moved here for some semblance of a standard.
    /// Next pr after the network fix will have me refactor door interactions, so help me god.
    // tg: code/game/objects/objs.dm:41
    public object id_tag;

    /// The sound this obj makes when something is buckled to it
    // tg: code/game/objects/objs.dm:44
    public object buckle_sound;

    /// The sound this obj makes when something is unbuckled from it
    // tg: code/game/objects/objs.dm:47
    public object unbuckle_sound;

    // tg: code/game/objects/objs.dm:49
    public float generate_map_preview = FALSE;

    // tg: code/game/objects/objs.dm:53  /obj/vv_edit_var(vname, vval): not declared, its root /datum/proc/vv_edit_var
    // (datums/datumvars.dm, VV) is not translated (rules 13).
}

public partial class datum_controller_global_vars
{
    /// A list of all /obj by their id_tag
    // tg: code/game/objects/objs.dm:60  GLOBAL_LIST_EMPTY(objects_by_id_tag)
    public DmList objects_by_id_tag;

    // tg: code/game/objects/objs.dm:60  (GLOBAL_MANAGED(objects_by_id_tag, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalobjects_by_id_tag()
    {
        objects_by_id_tag = Dm.list();
        Dm.append(ref gvars_datum_init_order, "objects_by_id_tag");
    }
}

// tg: code/game/objects/objs.dm:62
public partial class obj : atom_movable
{
    // tg: code/game/objects/objs.dm:62
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        check_on_table();

        if (Dm.truthy(id_tag))
            GLOB.objects_by_id_tag[id_tag] = this;
        if (Dm.truthy(opacity))
            Dm.Deferred("code/game/objects/objs.dm:70", "Initialize: SScameras.update_visibility(src) (security cameras and their visibility are translated in M10+)");
        return dot;
    }

    // tg: code/game/objects/objs.dm:72
    public override object Destroy(float? force = null)
    {
        object dot = null;
        if (!ismachinery(this))
            STOP_PROCESSING(SSobj, this); // TODO: Have a processing bitflag to reduce on unnecessary loops through the processing lists
        if (Dm.truthy(opacity))
            Dm.Deferred("code/game/objects/objs.dm:76", "Destroy: SScameras.update_visibility(src) (security cameras and their visibility are translated in M10+)");
        SStgui?.close_uis(this); // M5 slice 7 (tgui is translated since M5 slice 3; rules 10.6: a test harness world may lack SStgui)
        Dm.remove(ref GLOB.objects_by_id_tag, id_tag);
        dot = base.Destroy(force);
        return dot;
    }

    // tg: code/game/objects/objs.dm:81  (M5 slice 1: a melee hit on an object)
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        // DEVIATION(DV27): a hit on a type whose own attacked_by or damage procs are not translated is refused to the user with
        // "not ported yet" (D132); the attack chain stops there, as tg's ATTACK_FAILED stops it.
        if (InertSeam.MissingOverride(this, "attacked_by", user) || InertSeam.MissingDamageOverride(this, user))
            return ATTACK_FAILED;
        if (!Dm.truthy(attacking_item.force))
            return 0;

        object demo_mod = attacking_item.get_demolition_modifier(this);
        float total_force = CALCULATE_FORCE(attacking_item, attack_modifiers) * Dm.num(demo_mod);
        object damage = await take_damage(total_force, attacking_item.damtype, MELEE, TRUE, get_dir(this, user), attacking_item.armour_penetration);

        if (!Dm.truthy(LAZYACCESS(attack_modifiers, SILENCE_DEFAULT_MESSAGES)))
        {
            // Sanity in case one is null for some reason
            float picked_index = rand(max(length(attacking_item.attack_verb_simple), length(attacking_item.attack_verb_continuous)));

            object message_verb_continuous = "attacks";
            object message_verb_simple = "attack";
            // Sanity in case one is... longer than the other?
            if (Dm.truthy(picked_index) && length(attacking_item.attack_verb_continuous) >= picked_index)
                message_verb_continuous = attacking_item.attack_verb_continuous[picked_index];
            if (Dm.truthy(picked_index) && length(attacking_item.attack_verb_simple) >= picked_index)
                message_verb_simple = attacking_item.attack_verb_simple[picked_index];

            if (Dm.num(demo_mod) > 1 && prob(Dm.num(damage) * 5))
            {
                if (HAS_TRAIT(this, TRAIT_INVERTED_DEMOLITION))
                {
                    message_verb_simple = "shred";
                    message_verb_continuous = "shreds";
                }
                else
                {
                    message_verb_simple = "pulverise";
                    message_verb_continuous = "pulverises";
                }
            }

            if (Dm.num(demo_mod) < 1)
            {
                message_verb_simple = Dm.add("ineffectively ", message_verb_simple);
                message_verb_continuous = Dm.add("ineffectively ", message_verb_continuous);
            }

            user.visible_message(
                span_danger(I18n.T("[] [] [] with [][]", user, message_verb_continuous, this, attacking_item, Dm.truthy(damage) ? "." : Dm.text(", []!", no_damage_feedback))),
                span_danger(I18n.T("You [] [] with [][]", message_verb_simple, this, attacking_item, Dm.truthy(damage) ? "." : Dm.text(", []!", no_damage_feedback))),
                null,
                COMBAT_MESSAGE_RANGE
            );
        }

        log_combat(user, this, "attacked", attacking_item);
        return damage;
    }

    // tg: code/game/objects/objs.dm:123
    public override async DmTask<object> assume_air(datum_gas_mixture giver = null)
    {
        if (Dm.truthy(loc))
            return await loc.assume_air(giver);
        else
            return null;
    }

    // tg: code/game/objects/objs.dm:129
    public override object remove_air(object amount = null)
    {
        if (Dm.truthy(loc))
            return loc.remove_air(amount);
        else
            return null;
    }

    // tg: code/game/objects/objs.dm:135
    public override async DmTask<object> return_air()
    {
        if (Dm.truthy(loc))
            return await loc.return_air();
        else
            return null;
    }

    // tg: code/game/objects/objs.dm:141
    public virtual async DmTask<object> handle_internal_lifeform(mob lifeform_inside_me = null, object breath_request = null)
    {
        //Return: (NONSTANDARD)
        // null if object handles breathing logic for lifeform
        // datum/air_group to tell lifeform to process using that breath return
        //DEFAULT: Take air from turf to give to have mob process

        if (Dm.gt(breath_request, 0))
        {
            datum_gas_mixture environment = await return_air() as datum_gas_mixture;
            float breath_percentage = BREATH_VOLUME / Dm.num(environment.return_volume());
            return remove_air(Dm.num(environment.total_moles()) * breath_percentage);
        }
        else
            return null;
    }

    // tg: code/game/objects/objs.dm:154
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(this._listen_lookup?[COMSIG_ATOM_UI_INTERACT]))
            this._SendSignal(COMSIG_ATOM_UI_INTERACT, Dm.list(this, user));
        await ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/objs.dm:161  /obj/singularity_pull: at the file's end (M5x slice 5).

    // tg: code/game/objects/objs.dm:168
    public override object get_dumping_location()
    {
        return get_turf(this);
    }

    // tg: code/game/objects/objs.dm:171
    public override object vv_get_dropdown()
    {
        Dm.NotTranslated("code/game/objects/objs.dm:171", "vv_get_dropdown: VV is admin tooling (translation-rules.md 13)");
        return null;
    }

    // tg: code/game/objects/objs.dm:177
    public override async DmTask<object> vv_do_topic(DmList href_list = null)
    {
        Dm.NotTranslated("code/game/objects/objs.dm:177", "vv_do_topic: VV is admin tooling (translation-rules.md 13)");
        return null;
    }

    // tg: code/game/objects/objs.dm:223
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(desc_controls))
            Dm.append(ref dot, span_notice(Dm.str(desc_controls)));
        return dot;
    }

    // tg: code/game/objects/objs.dm:228
    public override object examine_tags(mob user = null)
    {
        DmList dot = base.examine_tags(user) as DmList;
        if (Dm.truthy(Dm.bitand(obj_flags, UNIQUE_RENAME)))
            dot[I18n.T("renameable")] = I18n.T("Use a pen on it to rename it or change its description.");
        if (Dm.truthy(Dm.bitand(obj_flags, CONDUCTS_ELECTRICITY)))
            dot[I18n.T("conductive")] = I18n.T("It appears to be a good conductor of electricity.");
        return dot;
    }

    // tg: code/game/objects/objs.dm:235  (M7 slice T3: the gas analyzer's scan of an object)
    public override object analyzer_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(atmos_scan(user: user, target: this, silent: FALSE)))
            return TRUE;
        return base.analyzer_act(user, tool);
    }

    // tg: code/game/objects/objs.dm:240  (E15: the plumbing machines' plunger_act; the plunger itself is not ported yet)
    public virtual async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        return !Dm.truthy(_listen_lookup?[COMSIG_PLUNGER_ACT]) ? (object)NONE : _SendSignal(COMSIG_PLUNGER_ACT, Dm.list(this, attacking_plunger, user, reinforced));
    }

    // Should move all contained objects to its location.
    // tg: code/game/objects/objs.dm:244
    public virtual async DmTask dump_contents(params object[] __extra)
    {
        throw Dm.CRASH("Unimplemented.", "code/game/objects/objs.dm", 246);
    }

    // tg: code/game/objects/objs.dm:248
    public override async DmTask<object> handle_ricochet(obj_projectile proj = null)
    {
        // The M7 projectiles slice
        object dot = null;
        dot = await base.handle_ricochet(proj);
        if (Dm.truthy(dot) && Dm.truthy(receive_ricochet_damage_coeff))
            await take_damage(Dm.num(proj.damage) * receive_ricochet_damage_coeff, proj.damage_type, proj.armor_flag, 0, REVERSE_DIR(proj.dir), proj.armour_penetration); // pass along receive_ricochet_damage_coeff damage to the structure for the ricochet
        return dot;
    }

    /// Handles exposing an object to reagents.
    // tg: code/game/objects/objs.dm:254
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

        if (Dm.truthy(source._listen_lookup?[COMSIG_REAGENTS_EXPOSE_OBJ]))
            source._SendSignal(COMSIG_REAGENTS_EXPOSE_OBJ, Dm.list(source, this, reagents, methods, volume_modifier, own_show_message));
        foreach (datum_reagent reagent in Dm.each_cast<datum_reagent>(reagents))
        {
            object reac_volume = reagents[reagent];
            dot = Dm.bitor(dot, await reagent.expose_obj(this, reac_volume, methods, own_show_message));
        }
        return dot;
    }

    /// Attempt to freeze this obj if possible. returns TRUE if it succeeded, FALSE otherwise.
    // tg: code/game/objects/objs.dm:265
    public virtual async DmTask<object> freeze()
    {
        // gaps SR1 (D333): the organ transport box freezes what it holds with it
        if (HAS_TRAIT(this, TRAIT_FROZEN))
            return FALSE;
        if (Dm.truthy(Dm.bitand(resistance_flags, FREEZE_PROOF)))
            return FALSE;

        await AddElement(typeof(datum_element_frozen));
        return TRUE;
    }

    /// Unfreezes this obj if its frozen
    // tg: code/game/objects/objs.dm:275
    public virtual void unfreeze()
    {
        if (Dm.truthy(_listen_lookup?[COMSIG_OBJ_UNFREEZE]))
            _SendSignal(COMSIG_OBJ_UNFREEZE, Dm.list(this));
    }

    /// If we can unwrench this object; returns SUCCESSFUL_UNFASTEN and FAILED_UNFASTEN, which are both TRUE, or CANT_UNFASTEN, which isn't.
    // tg: code/game/objects/objs.dm:279
    public virtual object can_be_unfasten_wrench(mob user = null, object silent = null)
    {
        // M5 slice 7
        if (!(isfloorturf(loc) || isindestructiblefloor__s7(loc)) && !Dm.truthy(anchored)) // is_anchorable_floor (__DEFINES/is_helpers.dm:74)
        {
            if (!Dm.truthy(silent))
                to_chat(user, span_warning(I18n.T("[] needs to be on the floor to be secured!", this)));
            return FAILED_UNFASTEN;
        }
        return SUCCESSFUL_UNFASTEN;
    }

    // tg: code/__DEFINES/is_helpers.dm:72  #define isindestructiblefloor(A) (istype(A, /turf/open/indestructible))
    private static bool isindestructiblefloor__s7(object A) => A is turf_open_indestructible;

    /// Try to unwrench an object in a WONDERFUL DYNAMIC WAY
    // tg: code/game/objects/objs.dm:287
    public virtual async DmTask<object> default_unfasten_wrench(mob user = null, obj_item wrench = null, float? time = null)
    {
        // M5 slice 7
        time ??= 2 * SECONDS;
        if (!Dm.eq(wrench.tool_behaviour, TOOL_WRENCH))
            return CANT_UNFASTEN;

        turf ground = get_turf(this) as turf;
        if (!Dm.truthy(anchored) && Dm.truthy(await ground.is_blocked_turf(exclude_mobs: TRUE, source_atom: this)))
        {
            to_chat(user, span_notice(I18n.T("You fail to secure [].", this)));
            return CANT_UNFASTEN;
        }
        object can_be_unfasten = can_be_unfasten_wrench(user);
        if (!Dm.truthy(can_be_unfasten) || Dm.eq(can_be_unfasten, FAILED_UNFASTEN))
            return can_be_unfasten;
        if (Dm.truthy(time))
            to_chat(user, span_notice(I18n.T("You begin []securing []...", Dm.truthy(anchored) ? "un" : "", this)));
        wrench.play_tool_sound(this, 50);
        float prev_anchored = anchored;
        //as long as we're the same anchored state and we're either on a floor or are anchored, toggle our anchored state
        if (!Dm.truthy(await wrench.use_tool(this, user as mob_living, time, extra_checks: Dm.@new<datum_callback>(this, nameof(unfasten_wrench_check), prev_anchored, user))))
            return FAILED_UNFASTEN;
        if (!Dm.truthy(anchored) && Dm.truthy(await ground.is_blocked_turf(exclude_mobs: TRUE, source_atom: this)))
        {
            to_chat(user, span_notice(I18n.T("You fail to secure [].", this)));
            return CANT_UNFASTEN;
        }
        to_chat(user, span_notice(I18n.T("You []secure [].", Dm.truthy(anchored) ? "un" : "", this)));
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        check_on_table();
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE); // tg: code/game/objects/objs.dm:311
        return SUCCESSFUL_UNFASTEN;
    }

    /// For the do_after, this checks if unfastening conditions are still valid
    // tg: code/game/objects/objs.dm:315
    public virtual object unfasten_wrench_check(object prev_anchored = null, mob user = null)
    {
        if (!Dm.eq(anchored, prev_anchored))
            return FALSE;
        if (!Dm.eq(can_be_unfasten_wrench(user, TRUE), SUCCESSFUL_UNFASTEN)) //if we aren't explicitly successful, cancel the fuck out
            return FALSE;
        return TRUE;
    }

    /// Adjusts the vertical pixel_z offset when the object is anchored on a tile with table
    // tg: code/game/objects/objs.dm:323
    public virtual void check_on_table()
    {
        if (anchored_tabletop_offset == 0)
            return;
        if (this is obj_structure_table)
            return;

        if (Dm.truthy(anchored) && Dm.truthy(Dm.locate<obj_structure_table>(loc)))
            pixel_z = anchored_tabletop_offset;
        else
            pixel_z = Dm.initial(this, (obj p) => p.pixel_z);
    }

    // tg: code/game/objects/objs.dm:334  /obj/apply_single_mat_effect(...), :339 /obj/remove_single_mat_effect(...) and
    // :345 /obj/proc/change_material_strength(datum/material/material, ...): not declared, their roots
    // (game/atom/atom_materials.dm) and the type /datum/material are not ported yet (materials, M5).

    /// Returns modifier to how much damage this object does to a target considered vulnerable to "demolition" (other objects, robots, etc)
    // tg: code/game/objects/objs.dm:362
    public virtual object get_demolition_modifier(obj target = null)
    {
        if (HAS_TRAIT(target, TRAIT_IGNORE_DEMOLITION))
            return 1;
        if (HAS_TRAIT(target, TRAIT_INVERTED_DEMOLITION))
            return (Dm.div(1, demolition_mod));
        return demolition_mod;
    }

    /// Checks performed by a renamable object(through UNIQUE_RENAME obj_flag) before renaming begins.
    // tg: code/game/objects/objs.dm:370
    public virtual object rename_checks(mob_living user = null)
    {
        return TRUE;
    }

    /// Returns the final name of the object, and does any side effects of renaming, such as sounds.
    // tg: code/game/objects/objs.dm:374
    public virtual async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        return input;
    }

    /// Same as nameformat, but for desc.
    // tg: code/game/objects/objs.dm:378
    public virtual async DmTask<object> descformat(object input = null, mob_living user = null)
    {
        return input;
    }

    /// Called when UNIQUE_RENAME is reset
    // tg: code/game/objects/objs.dm:382
    public virtual async DmTask rename_reset()
    {
        return;
    }

    /**
     * Used to deliver a shock to a mob from this object
     * The target must be adjacent to this object, or else the shock will fail
     *
     * * shocking - who are we zapping
     * * chance - probability the shock succeeds
     * defaults to 100 (guaranteed to shock)
     * * shock_source - used for determining where to get the power to zap them.
     * can be an apc, a cable, an area, a cell, or even a powernet datum
     * subtypes may override this proc to pass this up to the parent
     * defaults to our cell or our current area/apc
     * * siemens_coeff - multiplier to how much shock is delivered
     * default to a 1x modifier
     *
     * Returns TRUE if the shock was successfully delivered
     * Returns FALSE if the shock failed for any reason
     */
    // tg: code/game/objects/objs.dm:402  (shock__proc: /obj also has a var named shock, rules 2.4)
    [ShouldCallParent]
    public virtual async DmTask<object> shock__proc(mob_living shocking = null, float? chance = null, object shock_source = null, float? siemens_coeff = null)
    {
        chance ??= 100;
        siemens_coeff ??= 1;
        if (!isliving(shocking))
            return FALSE;
        if (!prob(Dm.num(chance)))
            return FALSE; // you lucked out, no shock for you

        do_sparks(5, TRUE, this);
        return await electrocute_mob(shocking as mob_living_carbon, ((shock_source is var or__1 && Dm.truthy(or__1) ? or__1 : get_cell()) is var or__2 && Dm.truthy(or__2) ? or__2 : get_area(this)), this, siemens_coeff, TRUE);
    }

    // tg: code/game/objects/objs.dm:334
    public override async DmTask apply_single_mat_effect(datum_material material = null, object mat_amount = null, object multiplier = null)
    {
        await base.apply_single_mat_effect(material, mat_amount, multiplier);
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)))
            change_material_strength(material, mat_amount, multiplier);
    }

    // tg: code/game/objects/objs.dm:339
    public override async DmTask remove_single_mat_effect(datum_material material = null, object mat_amount = null, object multiplier = null)
    {
        await base.remove_single_mat_effect(material, mat_amount, multiplier);
        if (Dm.truthy(Dm.bitand(material_flags, MATERIAL_AFFECT_STATISTICS)))
            change_material_strength(material, mat_amount, multiplier, remove: TRUE);
    }

    /// Changes force and throwforce of an item based on its properties. Split into a separate proc as to allow items to change theirs based on sharpness and behavior
    // tg: code/game/objects/objs.dm:345
    public virtual void change_material_strength(datum_material material = null, object mat_amount = null, object multiplier = null, float? remove = null)
    {
        remove ??= FALSE;
        float density = Dm.num(material.get_property(MATERIAL_DENSITY));
        float hardness = Dm.num(material.get_property(MATERIAL_HARDNESS));
        float flexibility = Dm.num(material.get_property(MATERIAL_FLEXIBILITY));
        // Dense and hard objects make for good melee weapons, bendy ones not so much
        float force_mod = (1 + (density - 4) * 0.05f + (hardness - 4) * 0.05f) * (1 - flexibility * 0.1f);
        // Hardness doesn't matter much when we're just whacking someone in the back of the head
        float throwforce_mod = 1 + (density - 4) * 0.1f - flexibility * 0.1f;

        if (!Dm.truthy(remove))
        {
            force *= GET_MATERIAL_MODIFIER(force_mod, multiplier);
            throwforce *= GET_MATERIAL_MODIFIER(throwforce_mod, multiplier);
        }
        else
        {
            force /= GET_MATERIAL_MODIFIER(force_mod, multiplier);
            throwforce /= GET_MATERIAL_MODIFIER(throwforce_mod, multiplier);
        }
    }
}

// tg: code/game/objects/objs.dm:161 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj : atom_movable
{
    // tg: code/game/objects/objs.dm:161
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (move_resist == INFINITY)
            return null;
        if (!Dm.truthy(anchored) || Dm.num(current_size) >= STAGE_FIVE)
            await step_towards(this, singularity);
        return null;
    }
}
