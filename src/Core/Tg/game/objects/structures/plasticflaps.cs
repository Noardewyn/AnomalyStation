// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/plasticflaps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P4: /obj/structure/plasticflaps is live. The init, the passage rule (only a lying or small mob gets through),
// the exit sound (M4b audio slice 6), the overlay, the dir change and the atmos flag are translated; M5 slice 1: melting in a
// nearby fire (check_melt), atom_break, atom_destruction and atom_deconstruct (its five plastic sheets); M5 slice 7: the screwdriver and the
// wirecutters; examine is a generated live-partial stub (docs/ai/specs/live-partial.txt's P4 section); gaps ST5: vv_edit_var.
// The var block is generated.

// tg: code/game/objects/structures/plasticflaps.dm:1
public partial class obj_structure_plasticflaps : obj_structure
{
    // tg: code/game/objects/structures/plasticflaps.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        alpha = 0;
        gen_overlay();
        air_update_turf(TRUE, TRUE);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        Waiting.NoSleep(AddComponent(typeof(datum_component_connect_range), this, Initialize__adjacent_loc_connections, 1, FALSE));
        return dot;
    }

    // tg: code/game/objects/structures/plasticflaps.dm:54  var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_structure_plasticflaps__Initialize__loc_connections;

    // tg: code/game/objects/structures/plasticflaps.dm:58  var/static/list/adjacent_loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__adjacent_loc_connections => ref TgState.Current.obj_structure_plasticflaps__Initialize__adjacent_loc_connections;

    // tg: code/game/objects/structures/plasticflaps.dm:63
    [SignalHandler]
    public virtual void play_plastic_sound(obj source = null, atom_movable exiting = null)
    {
        if (isitem(exiting))
        {
            obj_item item_exiter = exiting as obj_item;
            if (Dm.le(item_exiter.w_class, WEIGHT_CLASS_NORMAL))
                return;
            if (Dm.truthy(Dm.bitand(item_exiter.item_flags, ABSTRACT)))
                return;
        }
        if (isliving(exiting))
        {
            mob_living living_exiter = exiting as mob_living;
            if (Dm.le(living_exiter.mob_size, MOB_SIZE_TINY))
                return;
            // you're crawling under them
            if (living_exiter.body_position == LYING_DOWN)
                return;
            if (Dm.truthy(living_exiter.incorporeal_move))
                return;
        }
        if (HAS_TRAIT(exiting, TRAIT_MAGICALLY_PHASED))
            return;
        if (Dm.truthy(Dm.locate<obj_structure_plasticflaps>(exiting.loc)))
            return;
        playsound(this, Dm.resource("sound/effects/plasticflaps.ogg"), 50, TRUE, ignore_walls: FALSE, falloff_exponent: 8, extrarange: SHORT_RANGE_SOUND_EXTRARANGE); // tg: code/game/objects/structures/plasticflaps.dm:84
    }

    // tg: code/game/objects/structures/plasticflaps.dm:86
    [SignalHandler]
    public virtual void check_melt(turf source = null, datum_gas_mixture air = null, object temperature = null)
    {
        if (Dm.num(temperature) < FIRE_MINIMUM_TEMPERATURE_TO_EXIST * 1.5f)
            return;
        if (!(burn_damage_cd <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return;

        burn_damage_cd = world.time + 1 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))

        float percent_damage_taken = clamp(0.2f * (Dm.num(temperature) / (FIRE_MINIMUM_TEMPERATURE_TO_EXIST * 2.5f)), 0.05f, 0.25f);
        Waiting.NoSleep(take_damage(max_integrity * percent_damage_taken, BURN, FIRE, sound_effect: FALSE));
    }

    // tg: code/game/objects/structures/plasticflaps.dm:98
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        if (Dm.eq(damage_flag, FIRE))
            visible_message(span_warning(I18n.T("[] start\\s to melt from the heat!", this)));
        return await base.atom_break(damage_flag);
    }

    // tg: code/game/objects/structures/plasticflaps.dm:103
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        if (Dm.eq(damage_flag, FIRE))
            visible_message(span_warning(I18n.T("[] melt\\s away into plastic goo!", this)));
        await base.atom_destruction(damage_flag);
    }

    // tg: code/game/objects/structures/plasticflaps.dm:235
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_plastic_five>(loc);
    }

    // tg: code/game/objects/structures/plasticflaps.dm:108  (M4c slice 8; reached only by a z-level change, which the one loaded level, D108, never makes)
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (!Dm.truthy(same_z_layer))
        {
            SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
            gen_overlay();
        }
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/game/objects/structures/plasticflaps.dm:114
    public override async DmTask setDir(object newdir = null)
    {
        await base.setDir(newdir);
        SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
        gen_overlay();
    }

    // tg: code/game/objects/structures/plasticflaps.dm:119
    public virtual void gen_overlay()
    {
        turf our_turf = get_turf(this);
        //you see mobs under it, but you hit them like they are above it
        SSvis_overlays.add_vis_overlay(this, icon, icon_state,
            layer: flaps_layer,
            plane: MUTATE_PLANE(GAME_PLANE, our_turf),
            dir: dir,
            alpha: flaps_alpha,
            add_appearance_flags: RESET_ALPHA);
    }

    // M5 slice 7: screwing the flaps to the floor and back, and cutting the unscrewed flaps apart.
    // tg: code/game/objects/structures/plasticflaps.dm:143
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("[] are <b>screwed</b> to the floor.", this)));
        else
            Dm.append(ref dot, span_notice(I18n.T("[] are no longer <i>screwed</i> to the floor, and the flaps can be <b>cut</b> apart.", this)));
        return dot;
    }

    // tg: code/game/objects/structures/plasticflaps.dm:150
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item W = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, W)))
            return TRUE;
        // tg: code/game/objects/structures/plasticflaps.dm:153
        add_fingerprint(user);
        string action = Dm.truthy(anchored) ? Dm.text("unscrews [] from", this) : Dm.text("screws [] to", this);
        string uraction = Dm.truthy(anchored) ? Dm.text("unscrew [] from", this) : Dm.text("screw [] to", this);
        user.visible_message(span_warning(I18n.T("[] [] the floor.", user, action)), span_notice(I18n.T("You start to [] the floor...", uraction)), span_hear(I18n.T("You hear rustling noises.")));
        if (!Dm.truthy(await W.use_tool(this, user, 100, volume: 100, extra_checks: Dm.@new<datum_callback>(this, nameof(check_anchored_state), anchored))))
            return TRUE;
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        update_atmos_behaviour();
        air_update_turf(TRUE);
        to_chat(user, span_notice(I18n.T("You [] the floor.", uraction)));
        return TRUE;
    }

    // tg: code/game/objects/structures/plasticflaps.dm:169
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item W = null)
    {
        object dot = await base.wirecutter_act(user, W);
        if (!Dm.truthy(anchored))
        {
            user.visible_message(span_warning(I18n.T("[] cuts apart [].", user, this)), span_notice(I18n.T("You start to cut apart [].", this)), span_hear(I18n.T("You hear cutting.")));
            if (Dm.truthy(await W.use_tool(this, user, 50, volume: 100)))
            {
                if (Dm.truthy(anchored))
                    return TRUE;
                to_chat(user, span_notice(I18n.T("You cut apart [].", this)));
                obj_item_stack_sheet_plastic_five P = Dm.@new<obj_item_stack_sheet_plastic_five>(loc);
                if (!QDELETED(P))
                    // tg: code/game/objects/structures/plasticflaps.dm:179
                    P.add_fingerprint(user);
                qdel(this);
            }
            return TRUE;
        }
        return dot;
    }

    ///Update the flaps behaviour to gases, if not anchored will let air pass through
    // tg: code/game/objects/structures/plasticflaps.dm:166
    public virtual void update_atmos_behaviour()
    {
        can_atmos_pass = Dm.truthy(anchored) ? ATMOS_PASS_NO : ATMOS_PASS_YES;
    }

    // tg: code/game/objects/structures/plasticflaps.dm:183
    public virtual object check_anchored_state(object check_anchored = null)
    {
        if (!Dm.eq(anchored, check_anchored))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/plasticflaps.dm:203
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(Dm.bitand(mover.pass_flags, PASSFLAPS))) //For anything specifically engineered to cross plastic flaps.
            return TRUE;
        if (Dm.truthy(Dm.bitand(mover.pass_flags, PASSGLASS)) && prob(60))
            return TRUE;
        if (!Dm.truthy(require_resting))
            return TRUE;

        if (mover is obj_structure_bed)
        {
            obj_structure_bed bed_mover = mover as obj_structure_bed;
            if (Dm.truthy(bed_mover.density) || Dm.truthy(bed_mover.has_buckled_mobs()))//if it's a bed/chair and is dense or someone is buckled, it will not pass
                return FALSE;
        }

        else if (mover is obj_structure_closet_cardboard)
        {
            obj_structure_closet_cardboard cardboard_mover = mover as obj_structure_closet_cardboard;
            if (Dm.truthy(cardboard_mover.move_delay))
                return FALSE;
        }

        else if (ismecha(mover))
            return FALSE;

        else if (isliving(mover)) // You Shall Not Pass!
        {
            mob_living living_mover = mover as mob_living;
            if (living_mover.buckled is mob_living_basic_bot_mulebot) // mulebot passenger gets a free pass.
                return TRUE;

            if (living_mover.body_position == STANDING_UP && !Dm.eq(living_mover.mob_size, MOB_SIZE_TINY) && !(HAS_TRAIT(living_mover, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(living_mover, TRAIT_VENTCRAWLER_NUDE)))
                return FALSE; //If you're not laying down, or a small creature, or a ventcrawler, then no pass.
        }

        return dot;
    }

    // tg: code/game/objects/structures/plasticflaps.dm:238
    public override object Destroy(float? force = null)
    {
        object dot = null;
        atom oldloc = loc;
        dot = base.Destroy(force);
        if (Dm.truthy(oldloc))
            oldloc.air_update_turf(TRUE, FALSE);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/plasticflaps.dm:54  var/static/list/loc_connections = list(COMSIG_ATOM_EXITED = PROC_REF(play_plastic_sound))
    internal DmList obj_structure_plasticflaps__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXITED, nameof(obj_structure_plasticflaps.play_plastic_sound)));

    // tg: code/game/objects/structures/plasticflaps.dm:58  var/static/list/adjacent_loc_connections = list(COMSIG_TURF_EXPOSE = PROC_REF(check_melt))
    internal DmList obj_structure_plasticflaps__Initialize__adjacent_loc_connections = Dm.list(
        Dm.kv(COMSIG_TURF_EXPOSE, nameof(obj_structure_plasticflaps.check_melt)));
}

// Gaps package C1 (the AI's pathfinding and movement).
public partial class obj_structure_plasticflaps : obj_structure
{
    // tg: code/game/objects/structures/plasticflaps.dm:188
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        if (!Dm.truthy(require_resting))
            return TRUE;
        if (Dm.truthy(pass_info.is_living))
        {
            if (Dm.truthy(pass_info.is_bot))
                return TRUE;
            if (Dm.truthy(pass_info.can_ventcrawl) && !Dm.eq(pass_info.mob_size, MOB_SIZE_TINY))
                return FALSE;
        }
        if (Dm.truthy(Dm.bitand(pass_info.pass_flags, PASSFLAPS)))
            return TRUE;
        if (pass_info.pulling_info != null)
            return CanAStarPass(to_dir, pass_info.pulling_info);
        return TRUE; //diseases, stings, etc can pass
    }
}

// Gaps ST5 (D333): the admins' variable editing redraws the flaps.
public partial class obj_structure_plasticflaps : obj_structure
{
    // tg: code/game/objects/structures/plasticflaps.dm:130
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_val = null)
    {
        object dot = await base.vv_edit_var(var_name, var_val);
        DmList relevant_vars = Dm.list(
            nameof(flaps_layer),
            nameof(flaps_alpha),
            nameof(dir),
            nameof(icon),
            nameof(icon_state));
        if (Dm.@in(var_name, relevant_vars))
        {
            SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
            gen_overlay();
        }
        return dot;
    }
}
