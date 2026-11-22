// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the closet (docs/ai/specs/m4a-ownership.md, P3's list for closets.dm): creation and the lazy fill (tg fills a closet
// at its first open, dump_contents), the door and its overlays, examine and the context tips, open and close with the
// contents dumped and taken, insert, the lock and its access, the weld (the path P4's welder takes), the hand acts and the
// secure closet's spear bonus. M5 slice 7: deconstruction (handle_deconstruct, the welder's and the cutting tool's cut),
// atom_break (bust_open), anchoring, the airlock electronics and the card reader (install, unscrew, pry out, the
// multitool's access panel) and the crafted closet's access. M7 EMP: emp_act. Gaps ST5: get_save_vars, vv_edit_var, attack_self_tk
// and get_remote_view_fullscreens. The later procs stay Dm.NotTranslated roots or docs/ai/specs/live-partial.txt rows with their
// milestone: air (M4b), resisting and shaking (M9), the verb (M9), the knock spell and
// the shove into a locker (after the core version). Sounds are translated (M4b audio slice 6); emissives are DV12's; the paint
// and access pickers are tgui (M9).

// tg: code/game/objects/structures/crates_lockers/closets.dm:1  #define LOCKER_FULL -1 (file-local, rules 11)
public partial class obj_structure_closet : obj_structure
{
    private const float LOCKER_FULL = -1;

    // tg: code/game/objects/structures/crates_lockers/closets.dm:122  (proc-level var/static: rules 10.6, TgState fields)
    private static ref DmList Initialize__closet_paint_jobs => ref TgState.Current.obj_structure_closet__Initialize__closet_paint_jobs;
    private static ref DmList Initialize__card_reader_choices => ref TgState.Current.obj_structure_closet__Initialize__card_reader_choices;

    // tg: code/game/objects/structures/crates_lockers/closets.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);

        if (Initialize__closet_paint_jobs == null)
        {
            Initialize__closet_paint_jobs = Dm.list(
                Dm.kv("Cargo", Dm.list(Dm.kv("icon_state", "qm"))),
                Dm.kv("Engineering", Dm.list(Dm.kv("icon_state", "ce"))),
                Dm.kv("Engineering Secure", Dm.list(Dm.kv("icon_state", "eng_secure"))),
                Dm.kv("Radiation", Dm.list(Dm.kv("icon_state", "eng"), Dm.kv("icon_door", "eng_rad"))),
                Dm.kv("Tool Storage", Dm.list(Dm.kv("icon_state", "eng"), Dm.kv("icon_door", "eng_tool"))),
                Dm.kv("Fire Equipment", Dm.list(Dm.kv("icon_state", "fire"))),
                Dm.kv("Emergency", Dm.list(Dm.kv("icon_state", "emergency"))),
                Dm.kv("Hydroponics", Dm.list(Dm.kv("icon_state", "hydro"))),
                Dm.kv("Medical", Dm.list(Dm.kv("icon_state", "med"))),
                Dm.kv("Science", Dm.list(Dm.kv("icon_state", "rd"))),
                Dm.kv("Security", Dm.list(Dm.kv("icon_state", "cap"))),
                Dm.kv("Mining", Dm.list(Dm.kv("icon_state", "mining"))),
                Dm.kv("Virology", Dm.list(Dm.kv("icon_state", "bio_viro")))
            );
        }
        if (Dm.truthy(paint_jobs))
            paint_jobs = Initialize__closet_paint_jobs;

        if (Initialize__card_reader_choices == null)
        {
            Initialize__card_reader_choices = Dm.list(
                "Personal",
                "Job",
                "None"
            );
        }
        if (Dm.truthy(access_choices))
            access_choices = Initialize__card_reader_choices;

        if (Dm.truthy(is_station_level(z)) && Dm.truthy(mapload))
            add_to_roundstart_list();

        closet_see_inside = Dm.@new<datum_closet_see_inside>(this);
        Dm.combine(ref obj_flags, UNIQUE_RENAME);

        // if closed, any item at the crate's loc is put in the contents
        if (Dm.truthy(mapload))
            is_maploaded = TRUE;
        dot = INITIALIZE_HINT_LATELOAD;

        Waiting.NoSleep(populate_contents_immediate());
        // var/static/list/loc_connections (COMSIG_LIVING_DISARM_COLLIDE: locker_living, COMSIG_ATOM_MAGICALLY_UNLOCKED:
        // on_magic_unlock) and AddElement(/datum/element/connect_loc, loc_connections): both handlers are after the core
        // version (a shove into an open locker, the knock spell), so the element is not attached until they are translated.
        Dm.Deferred("code/game/objects/structures/crates_lockers/closets.dm:165", "Initialize: AddElement(/datum/element/connect_loc) for locker_living and on_magic_unlock (the shove into a locker and the knock spell, after the core version)");
        register_context();

        if (Dm.truthy(opened))
        {
            opened = FALSE; //necessary because open() proc will early return if its true
            if (Dm.truthy(Waiting.NoSleep(open(special_effects: FALSE)))) //closets which are meant to be open by default don't need to be animated open
                return dot;
        }
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:177
    public override void LateInitialize(params object[] __extra)
    {
        if (Dm.truthy(is_maploaded))
        {
            // very rare chance that a maintenance closet gets linked to another closet at roundstart
            // 0.1% chance -> metastation has ~36 maintenance closets -> P(links>=1) = 3.54% per round -> about once every 30 rounds
            if (prob(0.1f) && get_area(this) is area_station_maintenance)
            {
                DmList targets = Dm.sub(GLOB.roundstart_station_closets.Copy(), this) as DmList;
                DmList picked = Dm.list(this);
                object pick_count = pick(1, 2);
                for (float i = 1; i <= Dm.num(pick_count); i++)
                    Dm.append(ref picked, pick_n_take(targets));
                GLOB.closet_teleport_controller.create_new_link(picked, subtle: TRUE);
                if (!Dm.truthy(opened))
                    Waiting.NoSleep(open(force: TRUE, special_effects: FALSE));
            }

            if (!Dm.truthy(opened))
                Waiting.NoSleep(take_contents());
        }

        if (Dm.truthy(@sealed))
        {
            datum_gas_mixture external_air = loc == null ? null : Waiting.NoSleep(loc.return_air()) as datum_gas_mixture;
            if (external_air != null && Dm.truthy(is_maploaded))
                internal_air = external_air.copy() as datum_gas_mixture;
            else
                internal_air = Dm.@new<datum_gas_mixture>();
            START_PROCESSING(SSobj, this);
        }
    }

    //USE THIS TO FILL IT, NOT INITIALIZE OR NEW
    // tg: code/game/objects/structures/crates_lockers/closets.dm:208
    public virtual async DmTask PopulateContents(params object[] __extra)
    {
        return;
    }

    /// Populate the closet with stuff that needs to be added before it is opened.
    /// This is useful for things like traitor objectives.
    // tg: code/game/objects/structures/crates_lockers/closets.dm:213
    public virtual async DmTask populate_contents_immediate()
    {
        return;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:216
    public override object Destroy(float? force = null)
    {
        id_card = null;
        QDEL_NULL(ref internal_air);
        QDEL_NULL(ref door_obj);
        QDEL_NULL(ref closet_see_inside);
        Dm.remove(ref GLOB.roundstart_station_closets, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:201  (M5 slice 1: the air a mob in the closet breathes,
    // obj/handle_internal_lifeform; a sealed closet's own air is LateInitialize's deferred leaf)
    public override async DmTask<object> return_air()
    {
        if (Dm.truthy(@sealed))
            return internal_air;
        else
            return await base.return_air();
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:224
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(@sealed))
            return PROCESS_KILL;
        Waiting.NoSleep(process_internal_air(seconds_per_tick));
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:229
    public virtual async DmTask process_internal_air(object seconds_per_tick = null)
    {
        if (Dm.truthy(opened))
        {
            datum_gas_mixture current_exposed_air = await loc.return_air() as datum_gas_mixture;
            if (current_exposed_air == null)
                return;
            if (Dm.truthy(current_exposed_air.equalize(internal_air)))
            {
                turf location = get_turf(this) as turf;
                location.air_update_turf();
            }
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1271
    public override async DmTask<object> return_temperature()
    {
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:238
    public override object update_appearance(float? updates = null)
    {
        updates ??= ALL;
        object dot = null;
        dot = base.update_appearance(updates);
        if (Dm.truthy(opened) || Dm.truthy(broken) || !Dm.truthy(secure))
        {
            luminosity = 0;
            return dot;
        }
        luminosity = 1;
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:245
    [DmParams()]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        if (this is obj_structure_closet_supplypod) // issupplypod(src) (is_helpers.dm:267)
            return dot;
        layer = Dm.truthy(opened) ? BELOW_OBJ_LAYER : OBJ_LAYER;
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:251
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        closet_update_overlays(dot as DmList);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:255
    public virtual object closet_update_overlays(DmList new_overlays = null)
    {
        DmList dot = null;
        dot = new_overlays;
        if (Dm.truthy(enable_door_overlay) && !Dm.truthy(is_animating_door))
        {
            object overlay_state = base_icon_state == null ? Dm.initial<obj_structure_closet, string>(this, c => c.icon_state) : base_icon_state;
            if (Dm.truthy(opened) && Dm.truthy(has_opened_overlay))
            {
                object door_overlay = mutable_appearance(icon, Dm.text("[]_open", overlay_state), alpha: this.alpha);
                Dm.append(ref dot, door_overlay);
                // DEVIATION(DV12): door_overlay.overlays += emissive_blocker(...): emissives are not ported (D24, lead decision D2).
                Dm.Deferred("code/game/objects/structures/crates_lockers/closets.dm:262", "closet_update_overlays: emissive_blocker over the open door overlay (DEVIATION DV12)");
            }
            else if (Dm.truthy(has_closed_overlay))
            {
                object door_overlay = mutable_appearance(icon, Dm.text("[]_door", Dm.truthy(icon_door) ? icon_door : overlay_state), alpha: this.alpha);
                Dm.append(ref dot, door_overlay);
            }
        }

        if (Dm.truthy(opened))
            return dot;

        if (Dm.truthy(welded))
            Dm.append(ref dot, icon_welded);

        if (Dm.truthy(broken) && Dm.truthy(secure))
        {
            Dm.append(ref dot, mutable_appearance(icon, icon_broken, alpha: alpha));
            // DEVIATION(DV12): . += emissive_appearance(icon, icon_broken, src, alpha = alpha)
            Dm.Deferred("code/game/objects/structures/crates_lockers/closets.dm:275", "closet_update_overlays: emissive_appearance of the broken lock (DEVIATION DV12)");
            return dot;
        }

        if (Dm.truthy(broken) || !Dm.truthy(secure))
            return dot;
        //Overlay is similar enough for both that we can use the same mask for both
        // DEVIATION(DV12): . += emissive_appearance(icon, "locked", src, alpha = src.alpha)
        Dm.Deferred("code/game/objects/structures/crates_lockers/closets.dm:282", "closet_update_overlays: emissive_appearance of the lock light (DEVIATION DV12)");
        Dm.append(ref dot, Dm.truthy(locked) ? "locked" : "unlocked");
        return dot;
    }

    /// Animates the closet door opening and closing
    // tg: code/game/objects/structures/crates_lockers/closets.dm:309
    public virtual void animate_door(float? closing = null)
    {
        closing ??= FALSE;
        if (!Dm.truthy(door_anim_time))
            return;
        if (!Dm.truthy(door_obj))
            door_obj = Dm.@new<obj_effect_overlay_closet_door>();
        if (Dm.truthy(closing) && Dm.truthy(Dm.length(contents)))
        {
            // :314-321: the clones of the contents (/obj/effect/appearance_clone/closet_item) masked to the closet's door
            // with an alpha mask filter, shown during the closing animation: the clone's New assigns BYOND's appearance var
            // (camera_image_capturing.dm:3, not ported: not-translated.md), and the client draws a closet as a 3D model, which
            // does not show its vis_contents pictures.
            Dm.Deferred("code/game/objects/structures/crates_lockers/closets.dm:314", "animate_door: the contents' masked clones during the closing animation (appearance_clone needs BYOND's appearance assignment; a closet is a 3D model)");
        }
        string default_door_icon = Dm.text("[]_door", Dm.truthy(icon_door) ? icon_door : icon_state);
        Dm.append(vis_contents, door_obj);
        door_obj.icon = icon;
        door_obj.icon_state = default_door_icon;
        is_animating_door = TRUE;
        float num_steps = door_anim_time / world.tick_lag;
        for (float step = 0; step <= num_steps; step++)
        {
            float angle = door_anim_angle * (Dm.truthy(closing) ? 1 - (step / num_steps) : (step / num_steps));
            matrix door_transform = get_door_transform(angle) as matrix;
            string door_state;
            float door_layer;
            if (angle >= 90)
            {
                door_state = Dm.text("[]_back", icon_state);
                door_layer = FLOAT_LAYER;
            }
            else
            {
                door_state = default_door_icon;
                door_layer = ABOVE_MOB_LAYER;
            }
            if (step == 0)
            {
                door_obj.transform = door_transform;
                door_obj.icon_state = door_state;
                door_obj.layer = door_layer;
            }
            else if (step == 1)
                animate(door_obj, Dm.named("transform", door_transform), Dm.named("icon_state", door_state), Dm.named("layer", door_layer), Dm.named("time", world.tick_lag), Dm.named("flags", ANIMATION_END_NOW));
            else
                animate(Dm.named("transform", door_transform), Dm.named("icon_state", door_state), Dm.named("layer", door_layer), Dm.named("time", world.tick_lag));
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_door_animation)), door_anim_time, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_CLIENT_TIME), file: "code/game/objects/structures/crates_lockers/closets.dm", line: 354);
    }

    /// Ends the door animation and removes the animated overlay
    // tg: code/game/objects/structures/crates_lockers/closets.dm:357
    public virtual async DmTask end_door_animation()
    {
        is_animating_door = FALSE;
        Dm.remove(vis_contents, door_obj);
        foreach (obj_effect_appearance_clone_closet_item clone in Dm.each<obj_effect_appearance_clone_closet_item>(vis_contents))
        {
            Dm.remove(vis_contents, clone);
            qdel(clone);
        }
        await update_icon();
    }

    /// Calculates the matrix to be applied to the animated door overlay
    // tg: code/game/objects/structures/crates_lockers/closets.dm:366
    public virtual object get_door_transform(object angle = null)
    {
        matrix door_matrix = matrix();
        door_matrix.Translate(-door_hinge_x, 0);
        door_matrix.Multiply(matrix(cos(Dm.num(angle)), 0, 0, -sin(Dm.num(angle)) * door_anim_squish, 1, 0));
        door_matrix.Translate(door_hinge_x, 0);
        return door_matrix;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:373
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (Dm.truthy(id_card))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] with a pen.", EXAMINE_HINT(I18n.T("marked")))));
        if (Dm.truthy(can_weld_shut) && !Dm.truthy(welded))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] shut.", EXAMINE_HINT(I18n.T("welded")))));
        if (Dm.truthy(welded))
            Dm.append(ref dot, span_notice(I18n.T("It's [] shut.", EXAMINE_HINT(I18n.T("welded")))));
        if (Dm.truthy(anchorable) && !Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] to the ground.", EXAMINE_HINT(I18n.T("bolted")))));
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's [] to the ground.", Dm.truthy(anchorable) ? EXAMINE_HINT(I18n.T("bolted")) : I18n.T("attached firmly"))));
        if (Dm.truthy(Dm.length(paint_jobs)))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] with another texture.", EXAMINE_HINT(I18n.T("painted")))));
        if (HAS_TRAIT(user, TRAIT_SKITTISH) && Dm.truthy(divable))
        {
            // GAP(lead): /datum/proc/p_them (pronouns) is not declared; a closet is neuter, for which tg's p_them gives "it".
            Dm.append(ref dot, span_notice(I18n.T("If you bump into [] while running, you will jump inside.", "it")));
        }
        if (Dm.truthy(can_install_electronics))
        {
            if (!Dm.truthy(secure))
                Dm.append(ref dot, span_notice(I18n.T("You can install airlock electronics for access control.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("Its airlock electronics are [] in place.", EXAMINE_HINT(I18n.T("screwed")))));
            if (!Dm.truthy(card_reader_installed) && Dm.truthy(Dm.length(access_choices)))
                Dm.append(ref dot, span_notice(I18n.T("You can install a card reader for further access control.")));
            else if (Dm.truthy(card_reader_installed))
            {
                Dm.append(ref dot, span_notice(I18n.T("The card reader could be [] out.", EXAMINE_HINT(I18n.T("pried")))));
                Dm.append(ref dot, span_notice(I18n.T("Swipe your PDA with an ID card/Just ID to change access levels.")));
                Dm.append(ref dot, span_notice(I18n.T("Use multitool to [] the access panel.", Dm.truthy(access_locked) ? I18n.T("unlock") : I18n.T("lock"))));
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:402
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = null;
        dot = base.add_context(source, context, held_item, user);
        bool screentip_change = false;

        if (held_item == null)
        {
            if (!Dm.truthy(welded))
            {
                context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(opened) ? "Close" : "Open";
                context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(opened) ? "Close" : "Open";
            }
            if (Dm.truthy(secure) && !Dm.truthy(broken))
            {
                if (Dm.truthy(opened))
                    context[SCREENTIP_CONTEXT_RMB] = "Close";
                else
                {
                    context[SCREENTIP_CONTEXT_RMB] = !Dm.truthy(locked) ? "Lock" : "Unlock";
                    if (Dm.truthy(locked))
                        context[SCREENTIP_CONTEXT_LMB] = "Unlock";
                }
            }
            screentip_change = true;
        }

        if (Dm.truthy(secure) && !Dm.truthy(opened) && held_item is obj_item_card_id)
        {
            context[SCREENTIP_CONTEXT_LMB] = !Dm.truthy(locked) ? "Lock" : "Unlock";
            context[SCREENTIP_CONTEXT_RMB] = !Dm.truthy(locked) ? "Lock" : "Unlock";
            screentip_change = true;
        }

        if (istype(held_item, cutting_tool))
        {
            if (Dm.truthy(opened))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Deconstruct";
                screentip_change = true;
            }
            else
            {
                if (!Dm.truthy(welded) && Dm.truthy(can_weld_shut))
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Weld";
                    screentip_change = true;
                }
                else if (Dm.truthy(welded))
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Unweld";
                    screentip_change = true;
                }
            }
        }

        if ((held_item is obj_item) && Dm.eq(held_item.tool_behaviour, TOOL_WRENCH) && Dm.truthy(anchorable))
        {
            context[SCREENTIP_CONTEXT_RMB] = Dm.truthy(anchored) ? "Unanchor" : "Anchor";
            screentip_change = true;
        }

        if (!Dm.truthy(locked) && !Dm.truthy(opened) && (Dm.truthy(welded) || !Dm.truthy(can_weld_shut)))
        {
            if (!Dm.truthy(secure))
            {
                if (!Dm.truthy(broken) && Dm.truthy(can_install_electronics) && held_item is obj_item_electronics_airlock)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Install Electronics";
                    screentip_change = true;
                }
            }
            else
            {
                if ((held_item is obj_item) && Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER))
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Remove Electronics";
                    screentip_change = true;
                }
                if (!Dm.truthy(card_reader_installed) && Dm.truthy(Dm.length(access_choices)) && !Dm.truthy(broken) && Dm.truthy(can_install_electronics) && held_item is obj_item_stock_parts_card_reader)
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Install Reader";
                    screentip_change = true;
                }
            }
            if (Dm.truthy(card_reader_installed) && (held_item is obj_item) && Dm.eq(held_item.tool_behaviour, TOOL_CROWBAR))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Remove Reader";
                screentip_change = true;
            }
        }

        if (!Dm.truthy(locked) && !Dm.truthy(opened))
        {
            if (Dm.truthy(id_card) && IS_WRITING_UTENSIL(held_item))
            {
                context[SCREENTIP_CONTEXT_LMB] = "Rename";
                screentip_change = true;
            }
            if (Dm.truthy(secure) && Dm.truthy(card_reader_installed) && !Dm.truthy(broken))
            {
                if (!Dm.truthy(access_locked) && (held_item is obj_item) && !(held_item.GetID() == null))
                {
                    context[SCREENTIP_CONTEXT_LMB] = "Change Access";
                    screentip_change = true;
                }
                if ((held_item is obj_item) && (held_item is obj_item) && Dm.eq(held_item.tool_behaviour, TOOL_MULTITOOL))
                {
                    context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] Access Panel", Dm.truthy(access_locked) ? "Unlock" : "Lock");
                    screentip_change = true;
                }
            }
        }

        return screentip_change ? CONTEXTUAL_SCREENTIP_SET : NONE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:469
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = null;
        dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(wall_mounted))
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:474
    public virtual object can_open(object user = null, float? force = null)
    {
        force ??= FALSE;
        if (Dm.truthy(force))
            return TRUE;
        if (Dm.truthy(welded) || Dm.truthy(locked))
            return FALSE;
        if (isliving(user))
        {
            if (!Dm.truthy(Dm.bitand(((mob_living)user).mobility_flags, MOBILITY_USE)))
                return FALSE;
        }
        if (Dm.truthy(pulledby) && Dm.truthy(user) && HAS_TRAIT(this, TRAIT_STRONGPULL) && user != pulledby)
        {
            to_chat(user, span_danger(I18n.T("[] has an incredibly strong grip on [], preventing it from opening.", pulledby, this)));
            return FALSE;
        }
        turf T = get_turf(this);
        foreach (mob_living L in Dm.each<mob_living>(T))
        {
            if (Dm.truthy(L.anchored) || Dm.truthy(horizontal) && Dm.num(L.mob_size) > MOB_SIZE_TINY && Dm.truthy(L.density))
            {
                if (Dm.truthy(user))
                    to_chat(user, span_danger(I18n.T("There's something large on top of [], preventing it from opening.", this)));
                return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:493
    public virtual object can_close(mob_living user = null)
    {
        turf T = get_turf(this);
        foreach (obj_structure_closet closet in Dm.each<obj_structure_closet>(T))
        {
            if (closet != this && !Dm.truthy(closet.wall_mounted))
            {
                if (Dm.truthy(user))
                    balloon_alert(user, I18n.T("[] is in the way!", closet.name));
                return FALSE;
            }
        }
        foreach (mob_living L in Dm.each<mob_living>(T))
        {
            if (Dm.truthy(L.anchored) || Dm.truthy(horizontal) && Dm.num(L.mob_size) > MOB_SIZE_TINY && Dm.truthy(L.density))
            {
                if (Dm.truthy(user))
                    to_chat(user, span_danger(I18n.T("There's something too large in [], preventing it from closing.", this)));
                return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:507
    [DmParams()]
    public override async DmTask dump_contents(params object[] __extra)
    {
        if (!Dm.truthy(contents_initialized))
        {
            contents_initialized = TRUE;
            await PopulateContents();
            if (Dm.truthy(this._listen_lookup?[COMSIG_CLOSET_CONTENTS_INITIALIZED]))
                this._SendSignal(COMSIG_CLOSET_CONTENTS_INITIALIZED, Dm.list(this));
        }
        atom L = drop_location() as atom;
        foreach (atom_movable AM in Dm.each<atom_movable>(this))
        {
            await AM.forceMove(L);
            if (Dm.truthy(throwing)) // you keep some momentum when getting out of a thrown closet
                await step(AM, dir);
        }
        if (Dm.truthy(throwing))
            throwing.finalize(FALSE);
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:521
    // mapload is object (decided, syndicate G4c: the supply pod's override takes its holder there, supplypod.dm:438)
    public virtual async DmTask take_contents(object mapload = null)
    {
        mapload ??= FALSE;
        atom location = drop_location() as atom;
        if (!Dm.truthy(location))
            return;
        foreach (atom_movable AM in Dm.each<atom_movable>(location))
        {
            if (AM != this && Dm.eq(await insert(AM, mapload), LOCKER_FULL)) // limit reached
            {
                if (Dm.truthy(mapload)) // Yea, it's a mapping issue. Blame mappers.
                {
                    // GAP(lead): AREACOORD (__DEFINES/admin.dm:67) needs /atom/proc/Admin_Coordinates_Readable (admin, M10); the
                    // coordinates are written plainly.
                    log_mapping(Dm.text("Closet storage capacity of [] exceeded on mapload at [] ([], [], [])", type, get_area(this) is area a ? a.name : null, x, y, z));
                }
                break;
            }
        }
        foreach (object i in Dm.each(reverse_range(location.get_all_contents() as DmList)))
        {
            // var/atom/movable/thing = i: get_all_contents includes the location itself (a turf), whose atom_storage DM reads too.
            atom thing = i as atom;
            thing.atom_storage?.close_all();
        }
    }

    ///Proc to write checks before opening a door
    // tg: code/game/objects/structures/crates_lockers/closets.dm:535
    public virtual async DmTask<object> before_open(mob_living user = null, object force = null)
    {
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:538
    public virtual async DmTask<object> open(mob_living user = null, float? force = null, float? special_effects = null)
    {
        force ??= FALSE;
        special_effects ??= TRUE;
        if (Dm.truthy(opened) || !Dm.truthy(can_open(user, force)))
            return FALSE;
        if (!Dm.truthy(await before_open(user, force)) || Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_CLOSET_PRE_OPEN]) ? (object)NONE : this._SendSignal(COMSIG_CLOSET_PRE_OPEN, Dm.list(this, user, force))), BLOCK_OPEN)))
            return FALSE;
        welded = FALSE;
        unlock();
        if (Dm.truthy(special_effects))
            playsound(loc, open_sound, open_sound_volume, TRUE, -3); // tg: code/game/objects/structures/crates_lockers/closets.dm:546
        opened = TRUE;
        if (!Dm.truthy(dense_when_open))
            set_density(FALSE);
        await dump_contents();
        if (Dm.truthy(special_effects))
            animate_door(FALSE);
        update_appearance();

        await after_open(user, force);
        if (Dm.truthy(this._listen_lookup?[COMSIG_CLOSET_POST_OPEN]))
            this._SendSignal(COMSIG_CLOSET_POST_OPEN, Dm.list(this, user, force));
        return TRUE;
    }

    ///Proc to override for effects after opening a door
    // tg: code/game/objects/structures/crates_lockers/closets.dm:559
    public virtual async DmTask after_open(mob_living user = null, float? force = null)
    {
        return;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:562
    // mapload is object (decided, syndicate G4c: the supply pod's override takes its holder there, supplypod.dm:445)
    public virtual async DmTask<object> insert(object inserted = null, object mapload = null)
    {
        mapload ??= FALSE;
        if (Dm.length(contents) >= storage_capacity)
        {
            if (!Dm.truthy(mapload))
                return LOCKER_FULL;
            //For maploading, we only return LOCKER_FULL if the movable was otherwise insertable. This way we can avoid logging false flags.
            return Dm.truthy(await insertion_allowed(inserted as atom)) ? LOCKER_FULL : FALSE;
        }
        if (!Dm.truthy(await insertion_allowed(inserted as atom)))
            return FALSE;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_CLOSET_INSERT]) ? (object)NONE : this._SendSignal(COMSIG_CLOSET_INSERT, Dm.list(this, inserted))), COMPONENT_CLOSET_INSERT_INTERRUPT)))
            return TRUE;
        await ((atom_movable)inserted).forceMove(this);
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:575
    // AM is an atom (decided, syndicate G4c: the supply pod asks it of the turf it lands on, supplypod.dm:444)
    public virtual async DmTask<object> insertion_allowed(atom AM = null)
    {
        if (ismob(AM))
        {
            if (!isliving(AM)) //let's not put ghosts or eye mobs inside closets...
                return FALSE;
            mob_living L = AM as mob_living;
            if (Dm.truthy(L.anchored) || Dm.truthy(L.buckled) || Dm.truthy(L.incorporeal_move) || Dm.truthy(L.has_buckled_mobs()))
                return FALSE;
            if (Dm.num(L.mob_size) > MOB_SIZE_TINY) // Tiny mobs are treated as items.
            {
                if (Dm.truthy(horizontal) && Dm.truthy(L.density))
                    return FALSE;
                if (Dm.num(L.mob_size) > max_mob_size)
                    return FALSE;
                float mobs_stored = 0;
                foreach (mob_living M in Dm.each<mob_living>(contents))
                {
                    mobs_stored++;
                    if (mobs_stored >= mob_storage_capacity)
                        return FALSE;
                }
            }
            await L.stop_pulling();
        }
        else if (AM is obj_structure_closet)
            return FALSE;
        else if (isobj(AM))
        {
            obj O = (obj)AM;
            if ((!Dm.truthy(allow_dense) && Dm.truthy(O.density)) || Dm.truthy(O.anchored) || Dm.truthy(O.has_buckled_mobs()) || ismecha(O))
                return FALSE;
            else if (isitem(AM) && !HAS_TRAIT(AM, TRAIT_NODROP))
                return TRUE;
            else if (!Dm.truthy(allow_objects) && !(AM is obj_effect_dummy_chameleon))
                return FALSE;
        }
        else
            return FALSE;

        return TRUE;
    }

    ///Proc to write checks before closing a door
    // tg: code/game/objects/structures/crates_lockers/closets.dm:609
    public virtual object before_close(mob_living user = null)
    {
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:612
    // user is an atom/movable (decided, syndicate G4c: the supply pod closes over its holder, itself, supplypod.dm:428)
    public virtual async DmTask<object> close(atom_movable user = null)
    {
        if (!Dm.truthy(opened) || !Dm.truthy(can_close(user as mob_living)))
            return FALSE;
        if (!Dm.truthy(before_close(user as mob_living)) || Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_CLOSET_PRE_CLOSE]) ? (object)NONE : this._SendSignal(COMSIG_CLOSET_PRE_CLOSE, Dm.list(this, user))), BLOCK_CLOSE)))
            return FALSE;
        await take_contents();
        playsound(loc, close_sound, close_sound_volume, TRUE, -3); // tg: code/game/objects/structures/crates_lockers/closets.dm:618
        opened = FALSE;
        set_density(TRUE);
        animate_door(TRUE);
        update_appearance();
        await after_close(user as mob_living);
        if (Dm.truthy(this._listen_lookup?[COMSIG_CLOSET_POST_CLOSE]))
            this._SendSignal(COMSIG_CLOSET_POST_CLOSE, Dm.list(this, user));
        return TRUE;
    }

    ///Proc to do effects after closet has closed
    // tg: code/game/objects/structures/crates_lockers/closets.dm:628
    public virtual async DmTask after_close(mob_living user = null, params object[] __extra)
    {
        return;
    }

    /**
     * Toggles a closet open or closed, to the opposite state. Does not respect locked or welded states, however.
     */
    // tg: code/game/objects/structures/crates_lockers/closets.dm:634
    public virtual async DmTask<object> toggle(mob_living user = null)
    {
        if (Dm.truthy(opened))
            return await close(user);
        else
            return await open(user);
    }

    // M5 slice 7 (closets): deconstruction into sheets and the airlock electronics and card reader it held, the break that
    // busts it open, the crafted closet's access.
    // tg: code/game/objects/structures/crates_lockers/closets.dm:642
    public override async DmTask handle_deconstruct(float? disassembled = null)
    {
        await dump_contents();
        if (Dm.truthy(Dm.bitand(obj_flags, NO_DEBRIS_AFTER_DECONSTRUCTION)))
            return;

        if (ispath(material_drop) && Dm.truthy(material_drop_amount))
            Dm.@new(material_drop, loc, material_drop_amount);
        if (Dm.truthy(secure))
        {
            obj_item_electronics_airlock electronics = Dm.@new<obj_item_electronics_airlock>(drop_location());
            if (Dm.truthy(Dm.length(req_one_access)))
            {
                electronics.one_access = TRUE;
                electronics.accesses = req_one_access;
            }
            else
                electronics.accesses = req_access;
        }
        if (Dm.truthy(card_reader_installed))
            Dm.@new<obj_item_stock_parts_card_reader>(drop_location());
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:657
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (!Dm.truthy(broken))
            bust_open();
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:662
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        obj_item_electronics_airlock access_control = Dm.locate<obj_item_electronics_airlock>(components);
        if (QDELETED(access_control))
        {
            await base.on_craft_completion(components, current_recipe, crafter);
            return;
        }

        inherit_airlock_electronics_access(access_control);

        await base.on_craft_completion(components, current_recipe, crafter);
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:671
    public virtual void inherit_airlock_electronics_access(obj_item_electronics_airlock access_control = null)
    {
        if (Dm.truthy(access_control.one_access))
        {
            req_one_access = access_control.accesses;
            req_access = null;
        }
        else
        {
            req_access = access_control.accesses;
            req_one_access = null;
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:680
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(secure) || !Dm.truthy(card_reader_installed) || Dm.truthy(broken) || Dm.truthy(locked) || Dm.truthy(opened))
            return null;
        access_locked = Dm.truthy(access_locked) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("access panel []", Dm.truthy(access_locked) ? "locked" : "unlocked"));
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:687
    public virtual void set_access(DmList accesses = null)
    {
        if (Dm.truthy(Dm.length(req_one_access)))
        {
            req_one_access = accesses;
            req_access = null;
        }
        else
        {
            req_access = accesses;
            req_one_access = null;
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:695
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.@in(user, contents))
            return ITEM_INTERACT_BLOCKING; // can't even attack it from inside

        if (!Dm.truthy(opened) && tool is obj_item_airlock_painter)
        {
            if (!Dm.truthy(Dm.length(paint_jobs)))
                return ITEM_INTERACT_BLOCKING;

            // engineering E14 (the airlock painter)
            object choice = await tgui_input_list(user, I18n.T("Set Closet Paintjob"), I18n.T("Paintjob"), paint_jobs as DmList);
            if (choice == null)
                return ITEM_INTERACT_BLOCKING;

            obj_item_airlock_painter painter = tool as obj_item_airlock_painter;
            if (!Dm.truthy(painter.use_paint(user)))
                return ITEM_INTERACT_BLOCKING;

            DmList paint_job = (paint_jobs as DmList)[choice] as DmList;
            icon_state = Dm.str(paint_job["icon_state"]);
            base_icon_state = icon_state;
            icon_door = paint_job["icon_door"];

            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_electronics_airlock && Dm.truthy(can_install_airlock_electronics(user)))
        {
            user.visible_message(span_notice(I18n.T("[] installs the electronics into the [].", user, this)),
                span_notice(I18n.T("You start to install electronics into the []...", this)));

            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(can_install_airlock_electronics), user))))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;

            inherit_airlock_electronics_access((obj_item_electronics_airlock)tool);
            qdel(tool);
            secure = TRUE;
            balloon_alert(user, I18n.T("electronics installed"));

            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stock_parts_card_reader && Dm.truthy(can_install_card_reader(user)))
        {
            user.visible_message(span_notice(I18n.T("[] is installing a card reader.", user)),
                span_notice(I18n.T("You begin installing the card reader.")));

            if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: this, extra_checks: Dm.@new<datum_callback>(this, nameof(can_install_card_reader), user))))
                return ITEM_INTERACT_BLOCKING;

            qdel(tool);
            card_reader_installed = TRUE;

            balloon_alert(user, I18n.T("card reader installed"));
            return ITEM_INTERACT_SUCCESS;
        }

        obj_item_card_id card = tool.GetID() as obj_item_card_id;
        if (Dm.truthy(secure) && !Dm.truthy(broken) && Dm.truthy(card_reader_installed) && !Dm.truthy(locked) && !Dm.truthy(opened) && !Dm.truthy(access_locked) && Dm.truthy(card))
        {
            float num_choices = Dm.length(access_choices);
            if (!Dm.truthy(num_choices))
                return ITEM_INTERACT_BLOCKING;

            object choice = null;
            if (num_choices == 1)
                choice = ((DmList)access_choices)[1];
            else
            {
                Dm.NotTranslated("code/game/objects/structures/crates_lockers/closets.dm:747", "item_interaction: tgui_input_list of the card reader's access types (tgui, M10+)");
                choice = null;
            }
            if (choice == null)
                return ITEM_INTERACT_BLOCKING;

            id_card = null;
            if (Dm.eq(choice, "Personal")) //only the player who swiped their id has access.
            {
                id_card = WEAKREF(card) as datum_weakref;
                name = Dm.text("[]'s locker", card.registered_name);
                desc = Dm.text("[] It has been ID locked to [].", Dm.initial<obj_structure_closet, string>(this, c => c.desc), card.registered_name);
            }
            else if (Dm.eq(choice, "Job")) //anyone who has the same access permissions as this id has access. Does NOT apply to the whole department.
            {
                name = Dm.text("[]'s locker", card.assignment);
                desc = Dm.text("[] It has been access locked to []s.", Dm.initial<obj_structure_closet, string>(this, c => c.desc), card.assignment);
                set_access(card.GetAccess() as DmList);
            }
            else if (Dm.eq(choice, "None")) //free for all
            {
                name = Dm.initial<obj_structure_closet, string>(this, c => c.name);
                desc = Dm.initial<obj_structure_closet, string>(this, c => c.desc);
                req_access = Dm.list();
                req_one_access = null;
                set_access(Dm.list());
            }

            if (!(id_card == null))
                balloon_alert(user, I18n.T("now owned by []", card.registered_name));
            else
                balloon_alert(user, I18n.T("set to []", choice));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(opened))
        {
            if (istype(tool, cutting_tool) && !Dm.eq(tool.tool_behaviour, TOOL_WELDER)) // for example cardboard box is cut with wirecutters
            {
                user.visible_message(span_notice(I18n.T("[] cut apart \\the [].", user, this)),
                    span_notice(I18n.T("You cut \\the [] apart with \\the [].", this, tool)));
                await deconstruct(TRUE);
                return ITEM_INTERACT_SUCCESS;
            }
            if (Dm.truthy(user.combat_mode))
                return ITEM_INTERACT_SKIP_TO_ATTACK;
            if (!Dm.truthy(await user.transfer_item_to_turf(tool, drop_location() as turf))) // so we put in unlit welder too
                return ITEM_INTERACT_BLOCKING;
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
        {
            if (!Dm.truthy(card))
                return NONE;
            if (Dm.truthy(opened))
                return ITEM_INTERACT_BLOCKING;
            await togglelock(user);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:813
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (Dm.truthy(await attack_hand(user, modifiers)))
            return ITEM_INTERACT_SUCCESS;
        return NONE;
    }

    /// check if we can install airlock electronics in this closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:819
    public virtual object can_install_airlock_electronics(mob user = null)
    {
        if (Dm.truthy(secure) || !Dm.truthy(can_install_electronics) || Dm.truthy(opened))
            return FALSE;

        if (Dm.truthy(broken))
        {
            balloon_alert(user, I18n.T("its broken!"));
            return FALSE;
        }

        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }

        return TRUE;
    }

    /// check if we can unscrew airlock electronics from this closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:834
    public virtual object can_unscrew_airlock_electronics(mob user = null)
    {
        if (!Dm.truthy(secure) || Dm.truthy(opened))
            return FALSE;
        if (Dm.truthy(card_reader_installed))
        {
            balloon_alert(user, I18n.T("attached to reader!"));
            return FALSE;
        }
        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }

        return TRUE;
    }

    /// check if we can install card reader in this closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:847
    public virtual object can_install_card_reader(mob user = null)
    {
        if (Dm.truthy(card_reader_installed) || !Dm.truthy(can_install_electronics) || !Dm.truthy(Dm.length(access_choices)) || Dm.truthy(opened))
            return FALSE;

        if (Dm.truthy(broken))
        {
            balloon_alert(user, I18n.T("its broken!"));
            return FALSE;
        }

        if (!Dm.truthy(secure))
        {
            balloon_alert(user, I18n.T("no electronics inside!"));
            return FALSE;
        }

        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }

        return TRUE;
    }

    /// check if we can pry out the card reader from this closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:866
    public virtual object can_pryout_card_reader(mob user = null)
    {
        if (!Dm.truthy(card_reader_installed) || Dm.truthy(opened))
            return FALSE;

        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:875
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.@in(user, contents))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(can_unscrew_airlock_electronics(user)))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] begins to remove the electronics from the [].", user, this)),
            span_notice(I18n.T("You begin to remove the electronics from the []...", this)));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50, extra_checks: Dm.@new<datum_callback>(this, nameof(can_unscrew_airlock_electronics), user))))
            return ITEM_INTERACT_BLOCKING;

        obj_item_electronics_airlock airlock_electronics = Dm.@new<obj_item_electronics_airlock>(drop_location());
        if (Dm.truthy(Dm.length(req_one_access)))
        {
            airlock_electronics.one_access = TRUE;
            airlock_electronics.accesses = req_one_access;
        }
        else
            airlock_electronics.accesses = req_access;

        req_access = Dm.list();
        req_one_access = null;
        id_card = null;
        secure = FALSE;
        balloon_alert(user, I18n.T("electronics removed"));

        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:905
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.@in(user, contents))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(can_pryout_card_reader(user)))
            return NONE;

        user.visible_message(span_notice(I18n.T("[] begins to pry the card reader out from [].", user, this)),
            span_notice(I18n.T("You begin to pry the card reader out from []...", this)));

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, extra_checks: Dm.@new<datum_callback>(this, nameof(can_pryout_card_reader), user))))
            return ITEM_INTERACT_BLOCKING;

        Dm.@new<obj_item_stock_parts_card_reader>(drop_location());
        card_reader_installed = FALSE;

        balloon_alert(user, I18n.T("card reader removed"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:924
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.@in(user, contents))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(opened) && istype(tool, cutting_tool)) // not all of them take welders
        {
            if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            {
                to_chat(user, span_warning(I18n.T("You can't cut [] apart!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
                return ITEM_INTERACT_BLOCKING;

            to_chat(user, span_notice(I18n.T("You begin cutting \\the [] apart...", this)));
            if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
                return ITEM_INTERACT_BLOCKING;

            if (!Dm.truthy(opened))
                return ITEM_INTERACT_BLOCKING;
            user.visible_message(span_notice(I18n.T("[] slices apart \\the [].", user, this)),
                span_notice(I18n.T("You cut \\the [] apart with \\the [].", this, tool)),
                span_hear(I18n.T("You hear welding.")));
            await deconstruct(TRUE);
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(can_weld_shut))
            return NONE;

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1)))
            return ITEM_INTERACT_BLOCKING;

        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(opened))
            return ITEM_INTERACT_BLOCKING;

        welded = Dm.truthy(welded) ? FALSE : TRUE;
        after_weld(welded);
        user.visible_message(span_notice(I18n.T("[] [] \\the [].", user, Dm.truthy(welded) ? "welds shut" : "unwelded", this)),
            span_notice(I18n.T("You [] \\the [] with \\the [].", Dm.truthy(welded) ? "weld" : "unwelded", this, tool)),
            span_hear(I18n.T("You hear welding.")));
        user.log_message(Dm.text("[] closet [] with []", Dm.truthy(welded) ? "welded" : "unwelded", this, tool), LOG_GAME);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:971
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(anchorable))
        {
            balloon_alert(user, I18n.T("no anchor bolts!"));
            return TRUE;
        }
        if (Dm.truthy(isinspace()) && !Dm.truthy(anchored)) // We want to prevent anchoring a locker in space, but we should still be able to unanchor it there
        {
            balloon_alert(user, I18n.T("nothing to anchor to!"));
            return TRUE;
        }
        await set_anchored(Dm.truthy(anchored) ? FALSE : TRUE);
        tool.play_tool_sound(this, 75);
        user.balloon_alert_to_viewers(I18n.T("[]", Dm.truthy(anchored) ? "anchored" : "unanchored"));
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:984
    public virtual void after_weld(object weld_state = null)
    {
        return;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:987
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom_movable O = dropped as atom_movable;
        if (!(dropped is atom_movable) || Dm.truthy(O.anchored) || dropped is atom_movable_screen)
            return null;
        if (user == O) //try to climb onto it
            return await base.mouse_drop_receive(dropped, user, @params);
        if (!Dm.truthy(opened))
            return null;
        if (!isturf(O.loc))
            return null;

        float actuallyismob = 0;
        if (isliving(O))
            actuallyismob = 1;
        else if (!isitem(O))
            return null;
        turf T = get_turf(this);
        // tg: code/game/objects/structures/crates_lockers/closets.dm:1001
        add_fingerprint(user);
        user.visible_message(span_warning(I18n.T("[] []stuff [] into [].", user, Dm.truthy(actuallyismob) ? "tries to " : "", O, this)),
            span_warning(I18n.T("You []stuff [] into [].", Dm.truthy(actuallyismob) ? "try to " : "", O, this)),
            span_hear(I18n.T("You hear clanging.")));
        if (Dm.truthy(actuallyismob))
        {
            if (Dm.truthy(await do_after(user, 4 * SECONDS, O)))
            {
                user.visible_message(span_notice(I18n.T("[] stuffs [] into [].", user, O, this)),
                    span_notice(I18n.T("You stuff [] into [].", O, this)),
                    span_hear(I18n.T("You hear a loud metal bang.")));
                mob_living L = O as mob_living;
                if (!issilicon(L))
                    await L.Paralyze(40);
                if (this is obj_structure_closet_supplypod_extractionpod)
                    await O.forceMove(this);
                else
                {
                    await O.forceMove(T);
                    await close();
                }
                log_combat(user, O, "stuffed", addition: Dm.text("inside of []", this));
            }
        }
        else
            await O.forceMove(T);
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1024
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user) || !isturf(loc))
            return null;
        if (Dm.truthy(locked))
        {
            if (Dm.num(message_cooldown) <= world.time)
            {
                message_cooldown = world.time + 50;
                to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
            }
            return null;
        }
        await container_resist_act(user);
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1034
    [DmParams("user", "modifiers")]
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        if (((mob_living)user).body_position == LYING_DOWN && get_dist(this, user) > 0)
            return dot;

        if (Dm.truthy(await toggle(user as mob_living)))
            return TRUE;
        if (!Dm.truthy(opened))
            return await togglelock(user as mob_living);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1048
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1066
    public virtual async DmTask<object> __gvb_verb_toggleopen(DmList structured_args = null)
    {
        Dm.NotTranslated("code/game/objects/structures/crates_lockers/closets.dm:1066", "__gvb_verb_toggleopen: the Toggle Open verb (verbs, M10+)");
        return default;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1066
    public virtual void verb_toggleopen(params object[] __extra)
    {
        Dm.NotTranslated("code/game/objects/structures/crates_lockers/closets.dm:1066", "verb_toggleopen: the Toggle Open verb (verbs, M10+)");
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1079
    public override async DmTask<object> Exit(atom_movable leaving = null, object direction = null)
    {
        await open();
        if (leaving.loc == this)
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1085
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        object loc_required = __extra != null && __extra.Length > 0 ? __extra[0] : TRUE;
        if (Dm.truthy(opened))
            return null;
        if (loc is atom_movable movable_parent)
        {
            user.change_next_special_move(CLICK_CD_BREAKOUT);
            await movable_parent.relay_container_resist_act(user, this);
            return null;
        }
        if (!Dm.truthy(welded) && !Dm.truthy(locked))
        {
            await open(user);
            return null;
        }

        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this)))
            return null;
        //okay, so the closet is either welded or locked... resist!!!
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(span_warning(I18n.T("[] begins to shake violently!", this)),
            span_notice(I18n.T("You lean on the back of [] and start pushing the door open... (this will take about [].)", this, DisplayTimeText(breakout_time))),
            span_hear(I18n.T("You hear banging from [].", this)));

        _addtimer(Dm.@new<datum_callback>(this, nameof(check_if_shake)), 1 * SECONDS, file: "code/game/objects/structures/crates_lockers/closets.dm", line: 1106);

        if (Dm.truthy(await do_after(user, breakout_time, target: this, cog_icon: null)))
        {
            if (!Dm.truthy(user) || IS_UNCONSCIOUS_OR_CRIT(user) || (Dm.truthy(loc_required) && user.loc != this) || Dm.truthy(opened) || (!Dm.truthy(locked) && !Dm.truthy(welded)))
                return null;
            //we check after a while whether there is a point of resisting anymore and whether the user is capable of resisting
            user.visible_message(span_danger(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            bust_open();
        }
        else
        {
            if (user.loc == this) //so we don't get the message if we resisted multiple times and succeeded.
                to_chat(user, span_warning(I18n.T("You fail to break out of []!", this)));
        }
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1118
    public override async DmTask<object> relay_container_resist_act(mob_living user = null, obj container = null)
    {
        await container_resist_act(user);
        return null;
    }

    /// Check if someone is still resisting inside, and choose to either keep shaking or stop shaking the closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1122
    public virtual object check_if_shake()
    {
        // Assuming we decide to shake again, how long until we check to shake again
        float next_check_time = 1 * SECONDS;

        // How long we shake between different calls of Shake(), so that it starts shaking and stops, instead of a steady shake
        float shake_duration = 0.3f * SECONDS;

        foreach (mob_living mob in Dm.each<mob_living>(contents))
        {
            if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(mob, this)))
            {
                // Shake and queue another check_if_shake
                Shake(x_shake_pixel_shift, y_shake_pixel_shift, shake_duration, shake_interval: 0.1f * SECONDS);
                _addtimer(Dm.@new<datum_callback>(this, nameof(check_if_shake)), next_check_time, file: "code/game/objects/structures/crates_lockers/closets.dm", line: 1131);
                return TRUE;
            }
        }

        // If we reach here, nobody is resisting, so don't shake
        return FALSE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1139  (M5 slice 7: a broken closet bursts open, atom_break)
    [SignalHandler]
    public virtual void bust_open()
    {
        welded = FALSE; //applies to all lockers
        unlock(); //applies to critter crates and secure lockers only
        broken = TRUE; //applies to secure lockers only
        Waiting.NoSleep(open(force: TRUE, special_effects: FALSE));
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1146
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = null;
        dot = await base.attack_hand_secondary(user, modifiers);
        if (!Dm.truthy(await user.can_perform_action(this)) || !isturf(loc))
            return dot;
        if (!Dm.truthy(opened) && Dm.truthy(secure))
        {
            await togglelock(user as mob_living);
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        return dot;
    }

    /**
     * returns TRUE if the closet is allowed to unlock
     * * user: the player trying to unlock this closet
     * * player_id: the id of the player trying to unlock this closet
     * * registered_id: the id registered to this closet, null if no one registered
     */
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1162
    public virtual object can_unlock(mob_living user = null, obj_item_card_id player_id = null, obj_item_card_id registered_id = null)
    {
        if (registered_id == null)
            return Waiting.NoSleep(allowed__proc(user));
        return player_id == registered_id ? TRUE : FALSE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1167
    public virtual async DmTask<object> togglelock(mob_living user = null, object silent = null)
    {
        if (!Dm.truthy(secure) || Dm.truthy(broken))
            return FALSE;

        if (Dm.truthy(locked)) //only apply checks while unlocking else allow anyone to lock it
        {
            string error_msg = "";
            if (!(id_card == null))
            {
                obj_item_card_id registered_id = id_card.resolve() as obj_item_card_id;
                if (!Dm.truthy(registered_id)) //id was deleted at some point. make this closet public access again
                {
                    name = Dm.initial<obj_structure_closet, string>(this, c => c.name);
                    desc = Dm.initial<obj_structure_closet, string>(this, c => c.desc);
                    id_card = null;
                    req_access = Dm.list();
                    req_one_access = null;
                    return await togglelock(user, silent);
                }
                if (!Dm.truthy(can_unlock(user, user.get_idcard() as obj_item_card_id, registered_id)))
                    error_msg = I18n.T("not your locker!");
            }
            else if (!Dm.truthy(can_unlock(user, user.get_idcard() as obj_item_card_id)))
                error_msg = I18n.T("access denied!");
            if (Dm.truthy(error_msg))
            {
                if (!Dm.truthy(silent))
                    balloon_alert(user, error_msg);
                return TRUE;
            }
        }

        if (iscarbon(user))
            // tg: code/game/objects/structures/crates_lockers/closets.dm:1192
            add_fingerprint(user);
        locked = Dm.truthy(locked) ? FALSE : TRUE;
        play_closet_lock_sound();
        user.visible_message(
            span_notice(I18n.T("[] [] [].", user, Dm.truthy(locked) ? "locks" : "unlocks", this)),
            span_notice(I18n.T("You [] [].", Dm.truthy(locked) ? "locked" : "unlocked", this))
        );
        update_appearance();
        return TRUE;
    }

    /// toggles the lock state of a closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1203
    public virtual void @lock()
    {
        if (Dm.truthy(locked))
            return;
        locked = TRUE;
        play_closet_lock_sound();
        update_appearance();
    }

    /// unlocks the closet
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1211
    public virtual void unlock()
    {
        if (!Dm.truthy(locked))
            return;
        locked = FALSE;
        play_closet_lock_sound();
        update_appearance();
    }

    /// plays the closet's lock/unlock sound, this should be placed AFTER you've changed the lock state
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1219
    public virtual void play_closet_lock_sound()
    {
        playsound(this, Dm.truthy(locked) ? lock_sound : unlock_sound, 50, TRUE); // tg: code/game/objects/structures/crates_lockers/closets.dm:1220
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1222
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(secure) && !Dm.truthy(broken))
        {
            visible_message(span_warning(I18n.T("Sparks fly from []!", this)), blind_message: span_hear(I18n.T("You hear a faint electrical spark.")));
            balloon_alert(user, "lock broken open");
            playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
            broken = TRUE;
            locked = FALSE;
            update_appearance();
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1237
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;
        if (!Dm.truthy(Dm.bitand(dot, EMP_PROTECT_CONTENTS)))
        {
            foreach (obj O in Dm.each<obj>(this))
                await O.emp_act(severity);
        }
        if (Dm.truthy(secure) && !Dm.truthy(broken) && !Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
        {
            if (prob(50 / Dm.num(severity)))
            {
                locked = !Dm.truthy(locked) ? TRUE : FALSE;
                update_appearance();
            }
            if (prob(20 / Dm.num(severity)) && !Dm.truthy(opened))
            {
                if (!Dm.truthy(locked))
                {
                    await open();
                }
                else
                {
                    req_access = Dm.list();
                    Dm.append(ref req_access, pick(SSid_access.get_region_access_list(Dm.list(REGION_ALL_STATION)) as DmList));
                }
            }
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1255  (M5x slice 1: explosions)
    public override async DmTask contents_explosion(object severity = null, object target = null)
    {
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                Dm.append(ref SSexplosions.high_mov_atom, contents);
                break;
            case EXPLODE_HEAVY:
                Dm.append(ref SSexplosions.med_mov_atom, contents);
                break;
            case EXPLODE_LIGHT:
                Dm.append(ref SSexplosions.low_mov_atom, contents);
                break;
        }
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1268
    public override object AllowDrop()
    {
        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1274
    public virtual object locker_living(datum source = null, mob_living shover = null, mob_living target = null, object shove_flags = null, obj_item weapon = null)
    {
        Dm.NotTranslated("code/game/objects/structures/crates_lockers/closets.dm:1274", "locker_living: shoving a mob into an open locker (after the core version)");
        return default;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1297
    public virtual void on_magic_unlock(datum source = null, datum_action_cooldown_spell_aoe_knock spell = null, atom caster = null)
    {
        Dm.NotTranslated("code/game/objects/structures/crates_lockers/closets.dm:1297", "on_magic_unlock: the knock spell (after the core version)");
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1307
    public virtual void add_to_roundstart_list()
    {
        Dm.append(ref GLOB.roundstart_station_closets, this);
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1310  (gaps S3: renaming a closet with a pen)
    public override object rename_checks(mob_living user = null)
    {
        object dot = TRUE;
        if (Dm.truthy(locked))
        {
            this.balloon_alert(user, I18n.T("unlock first!"));
            return FALSE;
        }

        if (id_card == null && Dm.truthy(secure))
        {
            this.balloon_alert(user, I18n.T("not yours to rename!"));
            return FALSE;
        }
        return dot;
    }

}

// tg: code/game/objects/structures/crates_lockers/closets.dm:1321
public partial class obj_structure_closet_secure_closet : obj_structure_closet
{
    ///Spears deal bonus damages to lockers
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1321
    public override async DmTask<object> attacked_by(obj_item attacking_item = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (attacking_item is obj_item_spear)
        {
            // HIDE_ATTACK_MESSAGES(attack_modifiers) (__DEFINES/combat.dm:438)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            attack_modifiers[SILENCE_DEFAULT_MESSAGES] = TRUE;
            // MODIFY_ATTACK_FORCE_MULTIPLIER(attack_modifiers, 2) (__DEFINES/combat.dm:427)
            if (!islist(attack_modifiers)) { attack_modifiers = Dm.list(); }
            if (!Dm.@in(FORCE_MULTIPLIER, attack_modifiers)) { attack_modifiers[FORCE_MULTIPLIER] = 1; }
            attack_modifiers[FORCE_MULTIPLIER] = Dm.mul(attack_modifiers[FORCE_MULTIPLIER], 2);
            user.visible_message(
                span_danger(I18n.T("[] stabs with precision []'s electronics with []!", user, this, attacking_item)),
                span_danger(I18n.T("You stab with precision []'s electronics with []!", this, attacking_item)),
                null,
                COMBAT_MESSAGE_RANGE
            );
        }
        return await base.attacked_by(attacking_item, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1334
    public override async DmTask<object> hitby(atom_movable hit_by = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        if (!(hit_by is obj_item_spear))
            return await base.hitby(hit_by, skipcatch, hitpush, blocked, throwingdatum);
        // We have to manually tweak throwforce for now
        obj_item spear = hit_by as obj_item;
        spear.throwforce *= 2;
        object dot = null;
        dot = await base.hitby(hit_by, skipcatch, hitpush, blocked, throwingdatum);
        spear.throwforce /= 2;
        return dot;
    }
}

public partial class datum_controller_global_vars
{
    ///A comprehensive list of all closets (NOT CRATES) in the game world
    // tg: code/game/objects/structures/crates_lockers/closets.dm:4  GLOBAL_LIST_EMPTY(roundstart_station_closets)
    public DmList roundstart_station_closets;

    // tg: code/game/objects/structures/crates_lockers/closets.dm:4  (GLOBAL_MANAGED(roundstart_station_closets, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalroundstart_station_closets()
    {
        roundstart_station_closets = Dm.list();
        Dm.append(ref gvars_datum_init_order, "roundstart_station_closets");
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/crates_lockers/closets.dm:122  /obj/structure/closet/Initialize's var/static/list/closet_paint_jobs
    internal DmList obj_structure_closet__Initialize__closet_paint_jobs;
    // tg: code/game/objects/structures/crates_lockers/closets.dm:141  /obj/structure/closet/Initialize's var/static/list/card_reader_choices
    internal DmList obj_structure_closet__Initialize__card_reader_choices;
}

// tg: code/game/objects/structures/crates_lockers/closets.dm:1264 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_structure_closet : obj_structure
{
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1264
    public override async DmTask<object> singularity_act()
    {
        await dump_contents();
        return await base.singularity_act();
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/objects/structures/crates_lockers/closets.dm:1051
public partial class obj_structure_closet : obj_structure
{
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1051
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (Dm.truthy(await user.Adjacent(this)))
            return await attack_hand(user);
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1055
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await user.Adjacent(this)))
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        await togglelock(user as mob_living);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}

// Gaps ST5 (D333): the map export's saved vars, the admin's var edit (opening and welding through it), telekinesis and the view from
// inside a closet.
public partial class obj_structure_closet : obj_structure
{
    // tg: code/game/objects/structures/crates_lockers/closets.dm:111
    public override object get_save_vars()
    {
        object dot = base.get_save_vars();
        Dm.append(ref dot, nameof(welded));
        Dm.append(ref dot, nameof(opened));
        Dm.append(ref dot, nameof(locked));
        Dm.append(ref dot, nameof(anchorable));
        Dm.append(ref dot, Dm.truthy(locked) ? "locked" : "unlocked");
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:284
    public override async DmTask<object> vv_edit_var(object vname = null, object vval = null)
    {
        if (Dm.eq(vname, nameof(opened)))
        {
            if (Dm.eq(vval, opened))
                return FALSE;
            if (Dm.truthy(vval) && !Dm.truthy(opened) && Dm.truthy(await open(force: TRUE)))
            {
                datum_flags = Dm.num(Dm.bitor(datum_flags, DF_VAR_EDITED));
                return TRUE;
            }
            else if (!Dm.truthy(vval) && Dm.truthy(opened) && Dm.truthy(await close()))
            {
                datum_flags = Dm.num(Dm.bitor(datum_flags, DF_VAR_EDITED));
                return TRUE;
            }
            return FALSE;
        }
        object dot = await base.vv_edit_var(vname, vval);
        if (Dm.eq(vname, nameof(welded)) && Dm.truthy(welded) && !Dm.truthy(can_weld_shut))
            can_weld_shut = TRUE;
        else if (Dm.eq(vname, nameof(can_weld_shut)) && !Dm.truthy(can_weld_shut) && Dm.truthy(welded))
        {
            welded = FALSE;
            update_appearance();
        }
        return dot;
    }

    // tk grab then use on self
    // tg: code/game/objects/structures/crates_lockers/closets.dm:1062
    public override async DmTask<object> attack_self_tk(mob user = null)
    {
        if (Dm.truthy(await attack_hand(user)))
            return ITEM_INTERACT_BLOCKING;
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets.dm:1233
    public override void get_remote_view_fullscreens(mob user = null)
    {
        if (user.stat == DEAD || !Dm.truthy(Dm.bitand(user.sight, Dm.bitor(SEEOBJS, SEEMOBS))))
            user.overlay_fullscreen("remote_view", typeof(atom_movable_screen_fullscreen_impaired), 1);
    }
}
