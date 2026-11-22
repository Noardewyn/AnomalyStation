// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/iv_drip.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C (D192): the whole file (the IV drip: a container hung on it, dragged onto a patient to inject or draw blood at
// its rate, its states, fill and beam to the patient, the needle ripped out at a distance, the saline drip, the connected
// alert). Deferred leaves: the attached container's attack log line (M10). The two verbs' bodies run through the verb's
// INVOKE_ASYNC as observer.cs's reenter_corpse (a caller without a client does nothing; the verb panel is M9). The var
// blocks are generated.

///Universal IV that can drain blood or feed reagents over a period of time from or to a replaceable container
// tg: code/game/machinery/iv_drip.dm:13
public partial class obj_machinery_iv_drip : obj_machinery
{
    // tg: code/game/machinery/iv_drip.dm:2  #define IV_TRANSFER_RATE_STEP 0.01 (file-local)
    private const float IV_TRANSFER_RATE_STEP = 0.01f;
    // tg: code/game/machinery/iv_drip.dm:4  #define MIN_IV_TRANSFER_RATE 0 (file-local)
    private const float MIN_IV_TRANSFER_RATE = 0;
    // tg: code/game/machinery/iv_drip.dm:6  #define MAX_IV_TRANSFER_RATE 5 (file-local)
    private const float MAX_IV_TRANSFER_RATE = 5;
    // tg: code/game/machinery/iv_drip.dm:10  #define ALERT_IV_CONNECTED "iv_connected" (file-local)
    private const string ALERT_IV_CONNECTED = "iv_connected";

    /// Typecache of containers we accept.
    // tg: code/game/machinery/iv_drip.dm:42
    public static DmList drip_containers => TgState.Current.obj_machinery_iv_drip__drip_containers ??= (DmList)typecacheof(Dm.list(
        typeof(obj_item_reagent_containers_blood),
        typeof(obj_item_reagent_containers_cup),
        typeof(obj_item_reagent_containers_chem_pack)
    ));

    // tg: code/game/machinery/iv_drip.dm:48
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(use_internal_storage))
        {
            create_reagents(internal_volume_maximum, TRANSPARENT);
            if (Dm.truthy(internal_list_reagents))
                Waiting.NoSleep(((datum_reagents)reagents).add_reagent_list(internal_list_reagents as DmList));
        }
        interaction_flags_machine = Dm.num(Dm.bitor(interaction_flags_machine, INTERACT_MACHINE_OFFLINE));
        register_context();
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement)));
        return dot;
    }

    // tg: code/game/machinery/iv_drip.dm:58
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref attachment);
        QDEL_NULL(ref reagent_container);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/iv_drip.dm:63
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "IVDrip", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/machinery/iv_drip.dm:69
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (held_item == null)
        {
            if (attachment != null)
                context[SCREENTIP_CONTEXT_RMB] = "Take needle out";
            else if (reagent_container != null && !Dm.truthy(use_internal_storage))
                context[SCREENTIP_CONTEXT_RMB] = "Eject container";
            else if (!Dm.truthy(inject_only))
                context[SCREENTIP_CONTEXT_RMB] = Dm.text("Set to []", Dm.eq(mode, IV_INJECTING) ? "take blood" : "inject");
        }
        else
        {
            if (!Dm.truthy(use_internal_storage))
            {
                if (Dm.truthy(is_type_in_typecache(held_item, drip_containers)) || Dm.truthy(held_item.GetComponent(typeof(datum_component_edible)))) // IS_EDIBLE(held_item)
                    context[SCREENTIP_CONTEXT_LMB] = "Load container";
            }
        }

        if (transfer_rate > MIN_IV_TRANSFER_RATE)
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Set flow to min";
        else
            context[SCREENTIP_CONTEXT_ALT_LMB] = "Set flow to max";

        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/machinery/iv_drip.dm:89
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["transferStep"] = IV_TRANSFER_RATE_STEP;
        dot["maxTransferRate"] = MAX_IV_TRANSFER_RATE;
        dot["minTransferRate"] = MIN_IV_TRANSFER_RATE;
        return dot;
    }

    // tg: code/game/machinery/iv_drip.dm:95
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = Dm.list();
        dot["hasInternalStorage"] = use_internal_storage;
        dot["hasContainer"] = reagent_container != null ? TRUE : FALSE;
        dot["canRemoveContainer"] = Dm.not(use_internal_storage);
        dot["mode"] = Dm.eq(mode, IV_INJECTING) ? TRUE : FALSE;
        dot["canDraw"] = Dm.truthy(inject_only) || (attachment != null && !isliving(attachment.attached_to)) ? FALSE : TRUE;
        dot["transferRate"] = transfer_rate;
        dot["hasObjectAttached"] = attachment != null ? TRUE : FALSE;
        if (attachment != null)
            dot["objectName"] = attachment.attached_to.name;
        datum_reagents drip_reagents = get_reagents() as datum_reagents;
        if (drip_reagents != null)
        {
            dot["containerCurrentVolume"] = round(drip_reagents.total_volume, IV_TRANSFER_RATE_STEP);
            dot["containerMaxVolume"] = drip_reagents.maximum_volume;
            dot["containerReagentColor"] = mix_color_from_reagents(drip_reagents.reagent_list);
        }
        return dot;
    }

    // tg: code/game/machinery/iv_drip.dm:116
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.eq(action, "changeMode"))
        {
            toggle_mode();
            return TRUE;
        }
        else if (Dm.eq(action, "eject"))
        {
            eject_beaker();
            return TRUE;
        }
        else if (Dm.eq(action, "detach"))
        {
            detach_iv();
            return TRUE;
        }
        else if (Dm.eq(action, "changeRate"))
        {
            set_transfer_rate(text2num(@params["rate"]));
            return TRUE;
        }
        return dot;
    }

    /// Sets the transfer rate to the provided value
    // tg: code/game/machinery/iv_drip.dm:136
    public virtual void set_transfer_rate(object new_rate = null)
    {
        transfer_rate = round(clamp(Dm.num(new_rate), MIN_IV_TRANSFER_RATE, MAX_IV_TRANSFER_RATE), IV_TRANSFER_RATE_STEP);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/machinery/iv_drip.dm:140
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (transfer_rate > 0 && attachment != null)
            icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(mode) ? "injecting" : "donating");
        else
            icon_state = Dm.text("[]_[]", base_icon_state, Dm.truthy(mode) ? "injectidle" : "donateidle");
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/iv_drip.dm:147
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);

        if (reagent_container == null)
            return dot;

        Dm.append(ref dot, attachment != null ? "beakeractive" : "beakeridle");
        datum_reagents container_reagents = get_reagents() as datum_reagents;
        if (container_reagents == null)
            return dot;

        //The thresholds used to determine the reagent fill icon
        DmList fill_icon_thresholds = TgState.Current.obj_machinery_iv_drip__update_overlays__fill_icon_thresholds ??= Dm.list(0, 10, 25, 50, 75, 80, 90);

        object threshold = null;
        for (float i = 1; i <= fill_icon_thresholds.len; i++)
        {
            if (ROUND_UP(100 * container_reagents.total_volume / container_reagents.maximum_volume) >= Dm.num(fill_icon_thresholds[i]))
                threshold = i;
        }

        if (Dm.truthy(threshold))
        {
            string fill_name = Dm.text("reagent[]", fill_icon_thresholds[threshold]);
            mutable_appearance filling = (mutable_appearance)mutable_appearance(icon, fill_name);
            filling.color = mix_color_from_reagents(container_reagents.reagent_list);
            Dm.append(ref dot, filling);
        }
        return dot;
    }

    // tg: code/game/machinery/iv_drip.dm:172
    public override async DmTask<object> mouse_drop_dragged(atom target = null, mob user = null, object src_location = null, object over_location = null, object @params = null)
    {
        if (!isliving(user))
        {
            to_chat(user, span_warning(I18n.T("You can't do that!")));
            return null;
        }
        if (get_reagents() == null)
        {
            to_chat(user, span_warning(I18n.T("There's nothing attached to the IV drip!")));
            return null;
        }
        if (!Dm.truthy(target.is_injectable(user)))
        {
            to_chat(user, span_warning(I18n.T("Can't inject into this!")));
            return null;
        }
        if (attachment != null)
        {
            visible_message(span_warning(I18n.T("[] is detached from [].", attachment.attached_to, this)));
            QDEL_NULL(ref attachment);
            update_appearance(UPDATE_ICON);
        }
        user.visible_message(span_warning(I18n.T("[] attaches [] to [].", user, this, target)), span_notice(I18n.T("You attach [] to [].", this, target)));
        await attach_iv(target, user);
        return null;
    }

    // tg: code/game/machinery/iv_drip.dm:189
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(use_internal_storage))
            return NONE;

        if (!Dm.truthy(is_type_in_typecache(tool, drip_containers)) && !Dm.truthy(tool.GetComponent(typeof(datum_component_edible)))) // IS_EDIBLE(tool)
            return NONE;

        if (reagent_container != null)
        {
            balloon_alert(user, I18n.T("not empty!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        reagent_container = tool;
        balloon_alert(user, I18n.T("attached"));
        user.log_message(Dm.text("attached a [] to [] at [] containing ([])", tool, this, AREACOORD(this), ((datum_reagents)reagent_container.reagents).get_reagent_log_string()), LOG_ATTACK);
        add_fingerprint(user);
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/iv_drip.dm:207
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (transfer_rate > MIN_IV_TRANSFER_RATE)
        {
            balloon_alert(user, I18n.T("flow minimized"));
            set_transfer_rate(MIN_IV_TRANSFER_RATE);
        }
        else
        {
            balloon_alert(user, I18n.T("flow maximized"));
            set_transfer_rate(MAX_IV_TRANSFER_RATE);
        }
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/machinery/iv_drip.dm:217
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc);
        return null;
    }

    // tg: code/game/machinery/iv_drip.dm:220
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (attachment == null)
            return PROCESS_KILL;

        atom attached_to = attachment.attached_to;

        if (!(Dm.num(get_dist(this, attached_to)) <= 1 && isturf(attached_to.loc)))
        {
            if (isliving(attached_to))
            {
                mob_living_carbon attached_mob = attached_to as mob_living_carbon;
                to_chat(attached_to, span_userdanger(I18n.T("The IV drip needle is ripped out of you, leaving an open bleeding wound!")));
                DmList arm_zones = shuffle(Dm.list(BODY_ZONE_R_ARM, BODY_ZONE_L_ARM)) as DmList;
                obj_item_bodypart chosen_limb = (attached_mob.get_bodypart(arm_zones[1] as string) is var l1 && Dm.truthy(l1) ? l1 : attached_mob.get_bodypart(arm_zones[2] as string) is var l2 && Dm.truthy(l2) ? l2 : attached_mob.get_bodypart(BODY_ZONE_CHEST)) as obj_item_bodypart;
                Waiting.NoSleep(attached_mob.apply_damage(3, BRUTE, chosen_limb, wound_bonus: CANT_WOUND));
                Waiting.NoSleep(attached_mob.cause_wound_of_type_and_severity(WOUND_PIERCE, chosen_limb, WOUND_SEVERITY_MODERATE, wound_source: "IV needle"));
            }
            else
                visible_message(span_warning(I18n.T("[] is detached from [].", attached_to, this)));

            detach_iv();
            return PROCESS_KILL;
        }

        datum_reagents drip_reagents = get_reagents() as datum_reagents;
        if (drip_reagents == null)
            return PROCESS_KILL;

        if (!Dm.truthy(transfer_rate))
            return null;

        // Give reagents
        if (Dm.truthy(mode))
        {
            if (Dm.truthy(drip_reagents.total_volume))
            {
                Waiting.NoSleep(drip_reagents.trans_to(attached_to, transfer_rate * Dm.num(seconds_per_tick), methods: INJECT, show_message: FALSE)); //make reagents reacts, but don't spam messages
                update_appearance(UPDATE_ICON);
            }
        }
        // Take blood
        else if (isliving(attached_to))
        {
            mob_living attached_mob = (mob_living)attached_to;
            float amount = min(transfer_rate * Dm.num(seconds_per_tick), drip_reagents.maximum_volume - drip_reagents.total_volume);
            // If the beaker is full, ping
            if (!Dm.truthy(amount))
            {
                set_transfer_rate(MIN_IV_TRANSFER_RATE);
                audible_message(span_hear(I18n.T("[] pings.", this)));
                return null;
            }

            // If the human is losing too much blood, beep.
            if (Dm.num(attached_mob.get_blood_volume(apply_modifiers: TRUE)) < BLOOD_VOLUME_SAFE && prob(5))
            {
                audible_message(span_hear(I18n.T("[] beeps loudly.", this)));
                playsound(loc, Dm.resource("sound/machines/beep/twobeep_high.ogg"), 50, TRUE);
            }
            atom_movable target = Dm.truthy(use_internal_storage) ? this : reagent_container;
            Waiting.NoSleep(attached_mob.transfer_blood_to(target, amount));
            update_appearance(UPDATE_ICON);
        }
        return null;
    }

    // tg: code/game/machinery/iv_drip.dm:270
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || !ishuman(user))
            return dot;
        if (Dm.truthy(quick_toggle(user)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return dot;
    }

    // tg: code/game/machinery/iv_drip.dm:277
    public virtual object quick_toggle(mob user = null)
    {
        if (attachment != null)
        {
            visible_message(span_notice(I18n.T("[] is detached from [].", attachment.attached_to, this)));
            detach_iv();
        }
        else if (reagent_container != null)
            eject_beaker(user);
        else
            toggle_mode();
        return TRUE;
    }

    ///called when an IV is attached
    // tg: code/game/machinery/iv_drip.dm:288
    public virtual async DmTask attach_iv(atom target = null, mob user = null)
    {
        if (isliving(target))
        {
            user.visible_message(span_warning(I18n.T("[] begins attaching [] to []...", usr, this, target)), span_warning(I18n.T("You begin attaching [] to [].", this, target)));
            if (!Dm.truthy(await do_after(usr, 1 * SECONDS, target)))
                return;
        }
        else
            mode = IV_INJECTING;
        usr.visible_message(span_warning(I18n.T("[] attaches [] to [].", usr, this, target)), span_notice(I18n.T("You attach [] to [].", this, target)));
        datum_reagents container = (datum_reagents)get_reagents();
        log_combat(usr, target, "attached", this, Dm.text("containing: ([])", container.get_reagent_log_string()));
        add_fingerprint(usr);
        if (isliving(target))
        {
            mob_living target_mob = (mob_living)target;
            target_mob.throw_alert(ALERT_IV_CONNECTED, typeof(atom_movable_screen_alert_iv_connected));
        }
        qdel(attachment);
        attachment = Dm.@new<datum_iv_drip_attachment>(this, target);
        START_PROCESSING(SSmachines, this);
        update_appearance(UPDATE_ICON);

        if (Dm.truthy(_listen_lookup?[COMSIG_IV_ATTACH]))
            _SendSignal(COMSIG_IV_ATTACH, Dm.list(this, target));
    }

    ///Called when an iv is detached. doesnt include chat stuff because there's multiple options and its better handled by the caller
    // tg: code/game/machinery/iv_drip.dm:312
    public virtual void detach_iv()
    {
        if (attachment != null)
        {
            visible_message(span_notice(I18n.T("[] is detached from [].", attachment.attached_to, this)));
            if (isliving(attachment.attached_to))
            {
                mob_living attached_mob = (mob_living)attachment.attached_to;
                attached_mob.clear_alert(ALERT_IV_CONNECTED, TRUE); // tg passes /atom/movable/screen/alert/iv_connected, a true value
            }
        }
        if (Dm.truthy(_listen_lookup?[COMSIG_IV_DETACH]))
            _SendSignal(COMSIG_IV_DETACH, Dm.list(this, attachment?.attached_to));
        QDEL_NULL(ref attachment);
        update_appearance(UPDATE_ICON);
    }

    /// Get the reagents used by IV drip
    // tg: code/game/machinery/iv_drip.dm:323
    public virtual object get_reagents()
    {
        return Dm.truthy(use_internal_storage) ? reagents : reagent_container?.reagents;
    }

    // tg: code/game/machinery/iv_drip.dm:326  GAME_VERB_SRC(/obj/machinery/iv_drip, eject_beaker, view(1), "Remove IV Container", null)
    // The verb hands its arguments to SSverbs.invoke_verb, which runs the body for a caller with a client (observer.cs's
    // reenter_corpse); the verb panel and SSverbs are M9.
    public virtual void eject_beaker(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_eject_beaker(Dm.list()));
    }

    // tg: code/game/machinery/iv_drip.dm:326  (the verb's body, __gvb_eject_beaker)
    public virtual async DmTask __gvb_eject_beaker(DmList structured_args = null)
    {
        if (!isliving(usr))
        {
            to_chat(usr, span_warning(I18n.T("You can't do that!")));
            return;
        }
        if (!Dm.truthy(await usr.can_perform_action(this)))
            return;
        if (Dm.truthy(usr.incapacitated))
            return;
        if (reagent_container != null)
        {
            if (attachment != null)
            {
                visible_message(span_warning(I18n.T("[] is detached from [].", attachment?.attached_to, this)));
                detach_iv();
            }
            await reagent_container.forceMove((atom)drop_location());
            reagent_container = null;
            update_appearance(UPDATE_ICON);
        }
    }

    // tg: code/game/machinery/iv_drip.dm:343  GAME_VERB_SRC(/obj/machinery/iv_drip, toggle_mode, view(1), "Toggle Mode", null)
    public virtual void toggle_mode(params object[] __extra)
    {
        mob resolved_caller = usr;
        if (resolved_caller?.client == null)
            return;
        Waiting.Detach(__gvb_toggle_mode(Dm.list()));
    }

    // tg: code/game/machinery/iv_drip.dm:343  (the verb's body, __gvb_toggle_mode)
    public virtual async DmTask __gvb_toggle_mode(DmList structured_args = null)
    {
        if (!isliving(usr))
        {
            to_chat(usr, span_warning(I18n.T("You can't do that!")));
            return;
        }
        if (!Dm.truthy(await usr.can_perform_action(this)) || Dm.truthy(usr.incapacitated))
            return;
        if (Dm.truthy(inject_only))
        {
            mode = IV_INJECTING;
            return;
        }
        // Prevent blood draining from non-living
        if (attachment != null && !isliving(attachment.attached_to))
        {
            mode = IV_INJECTING;
            return;
        }
        mode = Dm.not(mode);
        update_appearance(UPDATE_ICON);
        to_chat(usr, span_notice(I18n.T("The IV drip is now [].", Dm.truthy(mode) ? I18n.T("injecting") : I18n.T("taking blood"))));
    }

    // tg: code/game/machinery/iv_drip.dm:361
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.num(get_dist(user, this)) > 2)
            return dot;

        Dm.append(ref dot, I18n.T("[] is [].", this, Dm.truthy(mode) ? I18n.T("injecting") : I18n.T("taking blood")));

        if (reagent_container != null)
        {
            datum_reagents container_reagents = reagent_container.reagents as datum_reagents;
            if (container_reagents != null && Dm.truthy(container_reagents.reagent_list.len))
                Dm.append(ref dot, span_notice(I18n.T("Attached is \\a [] with [] units of liquid.", reagent_container, container_reagents.total_volume)));
            else
                Dm.append(ref dot, span_notice(I18n.T("Attached is an empty [].", reagent_container.name)));
        }
        else if (Dm.truthy(use_internal_storage))
            Dm.append(ref dot, span_notice(I18n.T("It has an internal chemical storage.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("No chemicals are attached.")));

        Dm.append(ref dot, span_notice(I18n.T("[] is connected.", attachment != null ? attachment.attached_to : I18n.T("Nothing"))));
        return dot;
    }
}

/// Information and effects about where an IV drip is attached to
// Lifetime is managed by the iv_drip, which will delete the iv_drip_attachment after
// a process if the attached object is invalid.
// iv_drip_attachment should never outlive iv_drip.
// tg: code/game/machinery/iv_drip.dm:381
public partial class datum_iv_drip_attachment : datum
{
    // tg: code/game/machinery/iv_drip.dm:389
    [DmParams("iv_drip", "attached_to")]
    public override async DmTask New(params object[] __extra)
    {
        obj_machinery_iv_drip iv_drip = Dm.arg(__extra, 0) as obj_machinery_iv_drip;
        atom attached_to = Dm.arg(__extra, 1) as atom;
        this.iv_drip = iv_drip;
        this.attached_to = attached_to;

        tug_to_me = await attached_to.AddComponent(typeof(datum_component_tug_towards), iv_drip) as datum_component_tug_towards;

        beam = iv_drip.Beam(
            attached_to,
            icon_state: "1-full",
            beam_color: COLOR_SILVER,
            layer: BELOW_MOB_LAYER,
            // Come out from the spout
            override_origin_pixel_x: 9,
            override_origin_pixel_y: 2
        ) as datum_beam;
    }

    // tg: code/game/machinery/iv_drip.dm:409
    public override object Destroy(float? force = null)
    {
        tug_to_me.remove_tug_target(iv_drip);
        tug_to_me = null;

        iv_drip = null;
        attached_to = null;

        QDEL_NULL(ref beam);

        return base.Destroy(force);
    }
}

// tg: code/game/machinery/iv_drip.dm:432
public partial class obj_machinery_iv_drip_saline : obj_machinery_iv_drip
{
    // tg: code/game/machinery/iv_drip.dm:444
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        return base.Initialize(mapload, __extra);
    }
}

public sealed partial class TgState
{
    // tg: code/game/machinery/iv_drip.dm:42  the static typecache of the containers a drip takes
    internal DmList obj_machinery_iv_drip__drip_containers;
    // tg: code/game/machinery/iv_drip.dm:159  update_overlays' var/static/list/fill_icon_thresholds (a proc-level static, rules 10.6)
    internal DmList obj_machinery_iv_drip__update_overlays__fill_icon_thresholds;
}
