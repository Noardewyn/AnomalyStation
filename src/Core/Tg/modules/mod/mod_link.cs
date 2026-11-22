// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/mod_link.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file whole (gaps MO1, D333; before it only the scryer's equipped and dropped, M4a clothing). The MODlink: a MODsuit
// control unit or a MODlink scryer worn on the neck holds a link with a random id and a frequency (copied to and imprinted from
// a multitool's buffer); its wearer calls another link on the same frequency (an alert on the called mob, accepted with a left
// click, denied with a right one), and while the call lasts each side sees the other as a holographic picture of the caller
// beside them, turned with them and speaking what they say; the call ends when either side can no longer call (no charge, no
// wearer, dead, frequencies apart) or hangs up. The scryer runs on a power cell put in with a hand and taken out with a right
// click, and can be given a name tag. The var blocks are generated (D111).

public static partial class GlobalProcs
{
    // tg: code/modules/mod/mod_link.dm:1
    public static async DmTask<object> make_link_visual_generic(datum_mod_link mod_link = null, object proc_path = null)
    {
        mob_living user = await mod_link.get_user_callback.Invoke() as mob_living;
        obj_effect_overlay link_visual = Dm.@new<obj_effect_overlay>();
        link_visual.name = I18n.T("holocall ([])", mod_link.id);
        link_visual.mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        LAZYADD(ref mod_link.holder.update_on_z, link_visual);
        link_visual.appearance_flags = Dm.num(Dm.bitor(link_visual.appearance_flags, KEEP_TOGETHER));
        link_visual.makeHologram(0.75f);
        mod_link.visual_overlays = Dm.sub(user.overlays, user.active_thinking_indicator) as DmList;
        link_visual.add_overlay(mod_link.visual_overlays);
        mod_link.visual = link_visual;
        mod_link.holder.become_hearing_sensitive(REF(mod_link) as string);
        mod_link.holder.RegisterSignals(user, Dm.list(COMSIG_CARBON_APPLY_OVERLAY, COMSIG_CARBON_REMOVE_OVERLAY), proc_path);
        return link_visual;
    }

    // tg: code/modules/mod/mod_link.dm:16
    public static async DmTask get_link_visual_generic(datum_mod_link mod_link = null, atom_movable visuals = null, object proc_path = null)
    {
        mob_living user = await mod_link.get_user_callback.Invoke() as mob_living;
        playsound(mod_link.holder, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 50, vary: TRUE);
        visuals.add_overlay(mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "static_base", ABOVE_NORMAL_TURF_LAYER));
        visuals.add_overlay(mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "modlink", ABOVE_ALL_MOB_LAYER));
        visuals.add_filter("crop_square", 1, alpha_mask_filter(icon: icon(Dm.resource("icons/effects/effects.dmi"), "modlink_filter")) as DmList);
        visuals.maptext_height = 6;
        visuals.alpha = 0;
        Dm.append(user.vis_contents, visuals);
        await visuals.forceMove(user);
        animate(visuals, 0.5f * SECONDS, Dm.named("alpha", 255));
        datum_callback setdir_callback = Dm.@new<datum_callback>(mod_link.holder, proc_path);
        await setdir_callback.Invoke(user, user.dir, user.dir);
        mod_link.holder.RegisterSignal(mod_link.holder.loc, COMSIG_ATOM_DIR_CHANGE, proc_path);
    }

    // tg: code/modules/mod/mod_link.dm:31
    public static void delete_link_visual_generic(datum_mod_link mod_link = null, mob_living old_user = null)
    {
        playsound((mod_link.get_other() as datum_mod_link).holder, Dm.resource("sound/machines/terminal/terminal_processing.ogg"), 50, vary: TRUE, frequency: -1);
        LAZYREMOVE(ref mod_link.holder.update_on_z, mod_link.visual);
        mod_link.holder.lose_hearing_sensitivity(REF(mod_link) as string);
        mod_link.holder.UnregisterSignal(old_user, Dm.list(COMSIG_CARBON_APPLY_OVERLAY, COMSIG_CARBON_REMOVE_OVERLAY, COMSIG_ATOM_DIR_CHANGE));
        QDEL_NULL(ref mod_link.visual);
    }

    // tg: code/modules/mod/mod_link.dm:38
    public static async DmTask on_user_set_dir_generic(datum_mod_link mod_link = null, object newdir = null)
    {
        atom other_visual = (mod_link.get_other() as datum_mod_link).visual;
        if (!Dm.truthy(newdir)) //can sometimes be null or 0
            return;
        await other_visual.setDir(SOUTH);
        other_visual.pixel_x = 0;
        other_visual.pixel_y = 0;
        matrix new_transform = Dm.matrix();
        if (Dm.truthy(Dm.bitand(newdir, NORTH)))
        {
            other_visual.pixel_y = 13;
            other_visual.layer = BELOW_MOB_LAYER;
        }
        if (Dm.truthy(Dm.bitand(newdir, SOUTH)))
        {
            other_visual.pixel_y = -24;
            other_visual.layer = ABOVE_ALL_MOB_LAYER;
            new_transform.Scale(-1, 1);
            new_transform.Translate(-1, 0);
        }
        if (Dm.truthy(Dm.bitand(newdir, EAST)))
        {
            other_visual.pixel_x = 14;
            other_visual.layer = BELOW_MOB_LAYER;
            new_transform.Shear(0.5f, 0);
            new_transform.Scale(0.65f, 1);
        }
        if (Dm.truthy(Dm.bitand(newdir, WEST)))
        {
            other_visual.pixel_x = -14;
            other_visual.layer = BELOW_MOB_LAYER;
            new_transform.Shear(-0.5f, 0);
            new_transform.Scale(0.65f, 1);
        }
        other_visual.transform = new_transform;
    }

    // tg: code/modules/mod/mod_link.dm:493
    public static async DmTask call_link(mob user = null, datum_mod_link calling_link = null)
    {
        if (!Dm.truthy(calling_link.frequency))
            return;
        DmList callers = Dm.list();
        foreach (object id in Dm.each(GLOB.mod_link_ids))
        {
            datum_mod_link link = GLOB.mod_link_ids[id] as datum_mod_link;
            if (!Dm.eq(link.frequency, calling_link.frequency))
                continue;
            if (link == calling_link)
                continue;
            if (!Dm.truthy(await link.can_call_callback.Invoke()))
                continue;
            callers[Dm.text("[] ([])", link.holder, id)] = id;
        }
        if (!Dm.truthy(length(callers)))
        {
            calling_link.holder.balloon_alert(user, I18n.T("no targets on freq []!", calling_link.frequency));
            return;
        }
        object chosen_link = await tgui_input_list(user, I18n.T("Choose ID to call from [] frequency", calling_link.frequency), I18n.T("MODlink"), callers);
        if (!Dm.truthy(chosen_link))
            return;
        await calling_link.call_link(GLOB.mod_link_ids[callers[chosen_link]] as datum_mod_link, user);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/mod.dm:70  GLOBAL_LIST_EMPTY(mod_link_ids)
    public DmList mod_link_ids;

    // tg: code/__DEFINES/mod.dm:70  (GLOBAL_MANAGED(mod_link_ids, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmod_link_ids()
    {
        mod_link_ids = Dm.list();
        Dm.append(ref gvars_datum_init_order, nameof(mod_link_ids));
    }
}

public partial class obj_item_mod_control
{
    // tg: code/modules/mod/mod_link.dm:66 (the second definition of the control unit's Initialize; the first, mod_control.dm:88,
    // is Initialize__d0)
    [DmParams("mapload", "new_theme", "new_skin", "new_core")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = Initialize__d0(mapload, __extra);
        mod_link = Dm.@new<datum_mod_link>(
            this,
            starting_frequency,
            Dm.@new<datum_callback>(this, nameof(get_wearer)),
            Dm.@new<datum_callback>(this, nameof(can_call)),
            Dm.@new<datum_callback>(this, nameof(make_link_visual)),
            Dm.@new<datum_callback>(this, nameof(get_link_visual)),
            Dm.@new<datum_callback>(this, nameof(delete_link_visual))
        );
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:78
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        object dot = NONE;

        object tool_frequency = null;
        if (istype(multitool.buffer, typeof(datum_mod_link)))
        {
            datum_mod_link buffer_link = multitool.buffer as datum_mod_link;
            tool_frequency = buffer_link.frequency;
            balloon_alert(user, I18n.T("frequency set"));
            dot = ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(tool_frequency) && Dm.truthy(mod_link.frequency))
        {
            multitool.set_buffer(mod_link);
            balloon_alert(user, I18n.T("frequency copied"));
            dot = ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.truthy(tool_frequency) && !Dm.truthy(mod_link.frequency))
        {
            mod_link.frequency = tool_frequency;
            dot = ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.truthy(tool_frequency) && Dm.truthy(mod_link.frequency))
        {
            object response = await tgui_alert(user, I18n.T("Would you like to copy or imprint the frequency?"), I18n.T("MODlink Frequency"), Dm.list(I18n.T("Copy"), I18n.T("Imprint")));
            if (!Dm.truthy(user.is_holding(multitool)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.eq(response, I18n.T("Copy")))
            {
                multitool.set_buffer(mod_link);
                balloon_alert(user, I18n.T("frequency copied"));
                dot = ITEM_INTERACT_SUCCESS;
            }
            else if (Dm.eq(response, I18n.T("Imprint")))
            {
                mod_link.frequency = tool_frequency;
                balloon_alert(user, I18n.T("frequency set"));
                dot = ITEM_INTERACT_SUCCESS;
            }
        }
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:108
    public virtual object can_call()
    {
        object and__1 = get_charge();
        if (!Dm.truthy(and__1))
            return and__1;
        if (wearer == null)
            return wearer;
        return Dm.num(!Dm.eq(wearer.stat, DEAD));
    }

    // tg: code/modules/mod/mod_link.dm:111
    public virtual async DmTask<object> make_link_visual()
    {
        return await make_link_visual_generic(mod_link, nameof(on_overlay_change));
    }

    // tg: code/modules/mod/mod_link.dm:114
    public virtual async DmTask<object> get_link_visual(atom_movable visuals = null)
    {
        await get_link_visual_generic(mod_link, visuals, nameof(on_wearer_set_dir));
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:117
    public virtual object delete_link_visual(mob_living old_user = null)
    {
        delete_link_visual_generic(mod_link, old_user);
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:120
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (speaker != wearer && speaker != ai_assistant)
            return dot;
        await mod_link.visual.say(raw_message, spans: spans, sanitize: FALSE, language: message_language, message_range: 2, message_mods: message_mods);
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:126
    [SignalHandler]
    public virtual void on_overlay_change(atom source = null, object cache_index = null, object overlay = null)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_link_visual)), 1 * world.tick_lag, TIMER_UNIQUE, file: "code/modules/mod/mod_link.dm", line: 128);
    }

    // tg: code/modules/mod/mod_link.dm:130
    public virtual void update_link_visual()
    {
        if (QDELETED(mod_link.link_call))
            return;
        mod_link.visual.cut_overlay(mod_link.visual_overlays);
        mod_link.visual_overlays = Dm.sub(wearer.overlays, wearer.active_thinking_indicator) as DmList;
        mod_link.visual.add_overlay(mod_link.visual_overlays);
    }

    // tg: code/modules/mod/mod_link.dm:137
    [SignalHandler]
    public virtual void on_wearer_set_dir(atom source = null, object dir = null, object newdir = null)
    {
        Waiting.NoSleep(on_user_set_dir_generic(mod_link, Dm.truthy(newdir) ? newdir : SOUTH));
    }
}

// tg: code/modules/mod/mod_link.dm:141
public partial class obj_item_clothing_neck_link_scryer : obj_item_clothing_neck
{
    // tg: code/modules/mod/mod_link.dm:156
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        mod_link = Dm.@new<datum_mod_link>(
            this,
            starting_frequency,
            Dm.@new<datum_callback>(this, nameof(get_user)),
            Dm.@new<datum_callback>(this, nameof(can_call)),
            Dm.@new<datum_callback>(this, nameof(make_link_visual)),
            Dm.@new<datum_callback>(this, nameof(get_link_visual)),
            Dm.@new<datum_callback>(this, nameof(delete_link_visual))
        );
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:169
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref cell);
        QDEL_NULL(ref mod_link);
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_link.dm:175
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (cell != null)
            Dm.append(ref dot, span_notice(I18n.T("The battery charge reads []%. <b>Right-click</b> with an empty hand to remove it.", cell.percent())));
        else
            Dm.append(ref dot, span_notice(I18n.T("It is missing a battery, one can be installed by clicking with a power cell on it.")));
        Dm.append(ref dot, span_notice(I18n.T("The MODlink ID is [], frequency is []. <b>Right-click</b> with multitool to copy/imprint frequency.", mod_link.id, mod_link.frequency is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("unset"))));
        Dm.append(ref dot, span_notice(I18n.T("Use in hand to set name.")));
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:184
    [DmParams("user", "slot")]
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.eq(slot, ITEM_SLOT_NECK))
            mod_link?.end_call();
    }

    // tg: code/modules/mod/mod_link.dm:189
    [DmParams("user")]
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        mod_link?.end_call();
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:193
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object new_label = reject_bad_text(await tgui_input_text(user, I18n.T("Change the visible name"), I18n.T("Set Name"), label, MAX_NAME_LEN));
        if (!Dm.truthy(user.is_holding(this)))
            return null;
        if (!Dm.truthy(new_label))
        {
            balloon_alert(user, I18n.T("invalid name!"));
            return null;
        }
        label = new_label;
        balloon_alert(user, I18n.T("name set"));
        update_name();
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:204
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (mod_link.link_call == null)
            return null;
        Waiting.NoSleep(cell.use(0.02f * STANDARD_CELL_RATE * Dm.num(seconds_per_tick), Dm.named("force", TRUE)));
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:209
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (cell != null || !istype(tool, typeof(obj_item_stock_parts_power_store_cell)))
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        cell = tool as obj_item_stock_parts_power_store;
        balloon_alert(user, I18n.T("cell installed"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mod/mod_link.dm:220
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[][]", Dm.initial(this, nameof(name)), Dm.truthy(label) ? Dm.text(" - []", label) : "");
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:224
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell)
            cell = null;
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:229
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        if (cell == null)
            return SECONDARY_ATTACK_CONTINUE_CHAIN;
        balloon_alert(user, I18n.T("cell removed"));
        await user.put_in_hands(cell);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/mod/mod_link.dm:236
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        obj_item_multitool multitool = tool as obj_item_multitool;
        object dot = NONE;

        object tool_frequency = null;
        if (istype(multitool.buffer, typeof(datum_mod_link)))
        {
            datum_mod_link buffer_link = multitool.buffer as datum_mod_link;
            tool_frequency = buffer_link.frequency;
            balloon_alert(user, I18n.T("frequency set"));
            dot = ITEM_INTERACT_SUCCESS;
        }
        if (!Dm.truthy(tool_frequency) && Dm.truthy(mod_link.frequency))
        {
            multitool.set_buffer(mod_link);
            balloon_alert(user, I18n.T("frequency copied"));
            dot = ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.truthy(tool_frequency) && !Dm.truthy(mod_link.frequency))
        {
            mod_link.frequency = tool_frequency;
            dot = ITEM_INTERACT_SUCCESS;
        }
        else if (Dm.truthy(tool_frequency) && Dm.truthy(mod_link.frequency))
        {
            object response = await tgui_alert(user, I18n.T("Would you like to copy or imprint the frequency?"), I18n.T("MODlink Frequency"), Dm.list(I18n.T("Copy"), I18n.T("Imprint")));
            if (!Dm.truthy(user.is_holding(multitool)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.eq(response, I18n.T("Copy")))
            {
                multitool.set_buffer(mod_link);
                balloon_alert(user, I18n.T("frequency copied"));
                dot = ITEM_INTERACT_SUCCESS;
            }
            else if (Dm.eq(response, I18n.T("Imprint")))
            {
                mod_link.frequency = tool_frequency;
                balloon_alert(user, I18n.T("frequency set"));
                dot = ITEM_INTERACT_SUCCESS;
            }
        }
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:266
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        object dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape);
        if (!QDELETED(mod_link.link_call))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/mob/clothing/neck.dmi"), "modlink_active"));
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:271
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (mod_link.link_call != null)
            mod_link.end_call();
        else if (QDELETED(cell))
            user.balloon_alert(user, I18n.T("no cell installed!"));
        else if (!Dm.truthy(cell.charge))
            user.balloon_alert(user, I18n.T("no charge!"));
        else
            await call_link(user, mod_link);
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:281
    public virtual object get_user()
    {
        mob_living user = loc as mob_living;
        return istype(user, typeof(mob_living)) && user.get_item_by_slot(ITEM_SLOT_NECK) == this ? user : null;
    }

    // tg: code/modules/mod/mod_link.dm:285
    public virtual object can_call()
    {
        mob_living user = loc as mob_living;
        if (!istype(user, typeof(mob_living)))
            return FALSE;
        object and__1 = cell?.charge;
        if (!Dm.truthy(and__1))
            return and__1;
        return Dm.num(!Dm.eq(user.stat, DEAD));
    }

    // tg: code/modules/mod/mod_link.dm:289
    public virtual async DmTask<object> make_link_visual()
    {
        mob_living user = await mod_link.get_user_callback.Invoke() as mob_living;
        await user.update_worn_neck();
        return await make_link_visual_generic(mod_link, nameof(on_overlay_change));
    }

    // tg: code/modules/mod/mod_link.dm:294
    public virtual async DmTask<object> get_link_visual(atom_movable visuals = null)
    {
        await get_link_visual_generic(mod_link, visuals, nameof(on_user_set_dir));
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:297
    public virtual async DmTask<object> delete_link_visual(mob_living old_user = null)
    {
        if (!QDELETED(old_user))
            await old_user.update_worn_neck();
        delete_link_visual_generic(mod_link, old_user);
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:302
    public override async DmTask<object> Hear(atom_movable speaker = null, object message_language = null, object raw_message = null, object radio_freq = null, object radio_freq_name = null, object radio_freq_color = null, DmList spans = null, DmList message_mods = null, float? message_range = null)
    {
        object dot = await base.Hear(speaker, message_language, raw_message, radio_freq, radio_freq_name, radio_freq_color, spans, message_mods, message_range);
        if (speaker != loc)
            return dot;
        await mod_link.visual.say(raw_message, spans: spans, sanitize: FALSE, language: message_language, message_range: 3, message_mods: message_mods);
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:308
    [SignalHandler]
    public virtual void on_overlay_change(atom source = null, object cache_index = null, object overlay = null)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_link_visual)), 1 * world.tick_lag, TIMER_UNIQUE, file: "code/modules/mod/mod_link.dm", line: 310);
    }

    // tg: code/modules/mod/mod_link.dm:312
    public virtual void update_link_visual()
    {
        if (QDELETED(mod_link.link_call))
            return;
        mob_living user = loc as mob_living;
        mod_link.visual.cut_overlay(mod_link.visual_overlays);
        mod_link.visual_overlays = Dm.sub(user.overlays, user.active_thinking_indicator) as DmList;
        mod_link.visual.add_overlay(mod_link.visual_overlays);
    }

    // tg: code/modules/mod/mod_link.dm:320
    [SignalHandler]
    public virtual void on_user_set_dir(atom source = null, object dir = null, object newdir = null)
    {
        Waiting.NoSleep(on_user_set_dir_generic(mod_link, Dm.truthy(newdir) ? newdir : SOUTH));
    }
}

// tg: code/modules/mod/mod_link.dm:324
public partial class obj_item_clothing_neck_link_scryer_loaded : obj_item_clothing_neck_link_scryer
{
    // tg: code/modules/mod/mod_link.dm:327
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        cell = Dm.@new<obj_item_stock_parts_power_store_cell_high>(this);
        return dot;
    }
}

/// A MODlink datum, used to handle unique functions that will be used in the MODlink call.
// tg: code/modules/mod/mod_link.dm:335
public partial class datum_mod_link : datum
{
    // tg: code/modules/mod/mod_link.dm:363
    [DmParams("holder", "frequency", "get_user_callback", "can_call_callback", "make_visual_callback", "get_visual_callback", "delete_visual_callback")]
    public override async DmTask New(params object[] __extra)
    {
        atom holder = Dm.arg<atom>(__extra, 0);
        object frequency = Dm.arg(__extra, 1);
        datum_callback get_user_callback = Dm.arg<datum_callback>(__extra, 2);
        datum_callback can_call_callback = Dm.arg<datum_callback>(__extra, 3);
        datum_callback make_visual_callback = Dm.arg<datum_callback>(__extra, 4);
        datum_callback get_visual_callback = Dm.arg<datum_callback>(__extra, 5);
        datum_callback delete_visual_callback = Dm.arg<datum_callback>(__extra, 6);
        float attempts = 0;
        float digits_to_make = 3;
        do
        {
            if (attempts == 10)
            {
                attempts = 0;
                digits_to_make++;
            }
            id = "";
            for (float i = 1; i <= digits_to_make; i++)
                id += num2text(rand(0, 9));
            attempts++;
        }
        while (Dm.truthy(GLOB.mod_link_ids[id]));
        GLOB.mod_link_ids[id] = this;
        this.frequency = frequency;
        this.holder = holder as atom_movable;
        this.get_user_callback = get_user_callback;
        this.can_call_callback = can_call_callback;
        this.make_visual_callback = make_visual_callback;
        this.get_visual_callback = get_visual_callback;
        this.delete_visual_callback = delete_visual_callback;
        RegisterSignal(holder, COMSIG_QDELETING, nameof(on_holder_delete));
    }

    // tg: code/modules/mod/mod_link.dm:393
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.mod_link_ids, id);
        if (link_call != null)
            end_call();
        get_user_callback = null;
        make_visual_callback = null;
        get_visual_callback = null;
        delete_visual_callback = null;
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_link.dm:403
    public virtual object get_other()
    {
        if (link_call == null)
            return null;
        return link_call.link_caller == this ? link_call.link_receiver : link_call.link_caller;
    }

    // tg: code/modules/mod/mod_link.dm:409
    public virtual async DmTask call_link(datum_mod_link called = null, mob user = null)
    {
        if (!Dm.truthy(frequency))
            return;
        if (!istype(called, typeof(datum_mod_link)))
        {
            holder.balloon_alert(user, I18n.T("invalid target!"));
            return;
        }
        mob_living link_user = await get_user_callback.Invoke() as mob_living;
        if (link_user == null)
            return;
        if (HAS_TRAIT(link_user, TRAIT_IN_CALL))
        {
            holder.balloon_alert(user, I18n.T("already calling!"));
            return;
        }
        mob_living link_target = await called.get_user_callback.Invoke() as mob_living;
        if (link_target == null)
        {
            holder.balloon_alert(user, I18n.T("invalid target!"));
            return;
        }
        if (HAS_TRAIT(link_target, TRAIT_IN_CALL))
        {
            holder.balloon_alert(user, I18n.T("target already in call!"));
            return;
        }
        if (!Dm.truthy(await can_call_callback.Invoke()) || !Dm.truthy(await called.can_call_callback.Invoke()))
        {
            holder.balloon_alert(user, I18n.T("can't call!"));
            return;
        }
        link_target.playsound_local(get_turf(called.holder) as turf, Dm.resource("sound/items/weapons/ring.ogg"), 15, vary: TRUE);
        atom_movable_screen_alert_modlink_call alert = link_target.throw_alert(Dm.text("[]_modlink", REF(this)), typeof(atom_movable_screen_alert_modlink_call)) as atom_movable_screen_alert_modlink_call;
        alert.desc = I18n.T("[] ([]) is calling you! Left-click this to accept the call. Right-click to deny it.", holder, id);
        alert.link_caller_ref = WEAKREF(this) as datum_weakref;
        alert.link_receiver_ref = WEAKREF(called) as datum_weakref;
        alert.user_ref = WEAKREF(user) as datum_weakref;
    }

    // tg: code/modules/mod/mod_link.dm:438
    public virtual void end_call()
    {
        QDEL_NULL(ref link_call);
    }

    // tg: code/modules/mod/mod_link.dm:441
    public virtual async DmTask entered_call(datum_mod_link other_link = null)
    {
        mob_living user = await get_user_callback.Invoke() as mob_living;
        user_in_call_ref = WEAKREF(user) as datum_weakref;
        ADD_TRAIT(user, TRAIT_IN_CALL, REF(this));

        object other_visual = await other_link.make_visual_callback.Invoke();
        await get_visual_callback.Invoke(other_visual);
    }

    // tg: code/modules/mod/mod_link.dm:449
    public virtual async DmTask exiting_call()
    {
        mob_living old_user = user_in_call_ref?.resolve() as mob_living;
        user_in_call_ref = null;
        if (old_user != null)
            REMOVE_TRAIT(old_user, TRAIT_IN_CALL, REF(this));

        await delete_visual_callback.Invoke(old_user);
    }

    // tg: code/modules/mod/mod_link.dm:457
    [SignalHandler]
    public virtual void on_holder_delete(atom source = null)
    {
        qdel(this);
    }
}

/// A MODlink call datum, used to handle the call between two MODlinks.
// tg: code/modules/mod/mod_link.dm:462
public partial class datum_mod_link_call : datum
{
    // tg: code/modules/mod/mod_link.dm:468
    [DmParams("link_caller", "link_receiver")]
    public override async DmTask New(params object[] __extra)
    {
        datum_mod_link link_caller = Dm.arg<datum_mod_link>(__extra, 0);
        datum_mod_link link_receiver = Dm.arg<datum_mod_link>(__extra, 1);
        this.link_caller = link_caller;
        this.link_receiver = link_receiver;
        link_caller.link_call = this;
        link_receiver.link_call = this;
        await link_caller.entered_call(link_receiver);
        await link_receiver.entered_call(link_caller);
        START_PROCESSING(SSprocessing, this);
    }

    // tg: code/modules/mod/mod_link.dm:477
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(link_caller.exiting_call());
        Waiting.NoSleep(link_receiver.exiting_call());
        link_caller.link_call = null;
        link_receiver.link_call = null;
        STOP_PROCESSING(SSprocessing, this);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/mod_link.dm:485
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(Waiting.NoSleep(can_continue_call())))
            return null;
        qdel(this);
        return null;
    }

    // tg: code/modules/mod/mod_link.dm:490
    public virtual async DmTask<object> can_continue_call()
    {
        if (!Dm.eq(link_caller.frequency, link_receiver.frequency))
            return FALSE;
        object and__1 = await link_caller.can_call_callback.Invoke();
        if (!Dm.truthy(and__1))
            return and__1;
        return await link_receiver.can_call_callback.Invoke();
    }
}

// tg: code/modules/mod/mod_link.dm:515
public partial class atom_movable_screen_alert_modlink_call : atom_movable_screen_alert
{
    // tg: code/modules/mod/mod_link.dm:528
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = await base.Click(location, control, @params);
        if (usr != owner)
            return dot;
        datum_mod_link link_caller = link_caller_ref.resolve() as datum_mod_link;
        datum_mod_link link_receiver = link_receiver_ref.resolve() as datum_mod_link;
        if (link_caller == null || link_receiver == null)
            return dot;
        if (link_caller.link_call != null || link_receiver.link_call != null)
            return dot;
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            end_message = I18n.T("call denied!");
            owner.clear_alert(Dm.text("[]_modlink", REF(link_caller)));
            return dot;
        }
        end_message = I18n.T("call accepted");
        await Dm.new_async<datum_mod_link_call>(link_caller, link_receiver);
        owner.clear_alert(Dm.text("[]_modlink", REF(link_caller)));
        return dot;
    }

    // tg: code/modules/mod/mod_link.dm:547
    public override object Destroy(float? force = null)
    {
        mob_living user = user_ref?.resolve() as mob_living;
        datum_mod_link link_caller = link_caller_ref?.resolve() as datum_mod_link;
        if (user == null || link_caller == null)
            return base.Destroy(force);
        link_caller.holder.balloon_alert(user, end_message);
        return base.Destroy(force);
    }
}
