// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/religious_tool.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the religious tool (the altar of the gods, the ritual totem, a tuned instrument): a bible on it opens the
// ReligiousTool window, where the high priest selects the sect and the priests invoke its rites; a wanted item on it is sacrificed.
// The var block is generated (D111).

/**
 *
 * Allows the parent to act similarly to the Altar of Gods with modularity. Invoke and Sect Selection is done via attacking with a bible. This means you cannot sacrifice Bibles (you shouldn't want to do this anyways although now that I mentioned it you probably will want to).
 *
 */
// tg: code/datums/components/religious_tool.dm:6
public partial class datum_component_religious_tool : datum_component
{
    // tg: code/datums/components/religious_tool.dm:27
    [DmParams("operation_flags", "force_catalyst_afterattack", "after_sect_select_cb", "catalyst_type", "charges", "rite_types_allowlist")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object operation_flags = Dm.arg(__extra, 0);
        object force_catalyst_afterattack = Dm.arg(__extra, 1);
        datum_callback after_sect_select_cb = Dm.arg<datum_callback>(__extra, 2);
        Type catalyst_type = Dm.arg<Type>(__extra, 3);
        object charges = Dm.arg(__extra, 4);
        DmList rite_types_allowlist = Dm.arg<DmList>(__extra, 5);
        operation_flags ??= ALL;
        force_catalyst_afterattack ??= FALSE;
        catalyst_type ??= typeof(obj_item_book_bible);
        charges ??= -1;
        object dot = await base.Initialize(__extra);
        SetGlobalToLocal(); //attempt to connect on start in case one already exists!
        this.operation_flags = operation_flags;
        this.force_catalyst_afterattack = Dm.num(force_catalyst_afterattack);
        this.after_sect_select_cb = after_sect_select_cb;
        this.catalyst_type = catalyst_type;
        this.charges = charges;
        this.rite_types_allowlist = rite_types_allowlist;
        RegisterSignal(SSdcs, COMSIG_RELIGIOUS_SECT_CHANGED, nameof(SetGlobalToLocal));
        RegisterSignal(SSdcs, COMSIG_RELIGIOUS_SECT_RESET, nameof(on_sect_reset));
        return dot;
    }

    // tg: code/datums/components/religious_tool.dm:46
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref performing_rite);
        easy_access_sect = null;
        catalyst_type = null;
        after_sect_select_cb = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/religious_tool.dm:53
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(AttemptActions));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/religious_tool.dm:57
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_EXAMINE));
        await DmTask.CompletedTask;
    }

    /**
     * Sets the easy access variable to the global if it exists.
     */
    // tg: code/datums/components/religious_tool.dm:63
    [SignalHandler]
    public virtual object SetGlobalToLocal()
    {
        if (easy_access_sect != null)
            return TRUE;
        if (GLOB.religious_sect == null)
            return FALSE;
        easy_access_sect = GLOB.religious_sect;
        if (after_sect_select_cb != null)
            Waiting.NoSleep(after_sect_select_cb.Invoke());
        return TRUE;
    }

    /// Sets the easy access variable to null in case an admin needed to change it
    // tg: code/datums/components/religious_tool.dm:74
    [SignalHandler]
    public virtual void on_sect_reset()
    {
        easy_access_sect = null;
        if (after_sect_select_cb != null)
            Waiting.NoSleep(after_sect_select_cb.Invoke());
    }

    /**
     * Since all of these involve attackby, we require mega proc. Handles Invocation, Sacrificing, And Selection of Sects.
     */
    // tg: code/datums/components/religious_tool.dm:82
    [SignalHandler]
    public virtual object AttemptActions(datum source = null, obj_item the_item = null, mob_living user = null)
    {
        if (istype(the_item, catalyst_type))
            Waiting.Detach(ui_interact(user)); //asynchronous to avoid sleeping in a signal

        /**********Sacrificing**********/
        else if (Dm.truthy(Dm.bitand(operation_flags, RELIGION_TOOL_SACRIFICE)))
        {
            if (!Dm.truthy(easy_access_sect?.can_sacrifice(the_item, user)))
                return null;
            Waiting.NoSleep(easy_access_sect.on_sacrifice(the_item, user));
            return COMPONENT_NO_AFTERATTACK;
        }
        return null;
    }

    // tg: code/datums/components/religious_tool.dm:95
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "ReligiousTool");
            await ui.open();
        }
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/components/religious_tool.dm:102
    public override object ui_state(mob user = null)
    {
        if (!iscarbon(usr))
            return GLOB.never_state;
        mob_living_carbon carbon = usr as mob_living_carbon;
        if (!Dm.truthy(carbon.is_holding_item_of_type(catalyst_type)))
            return GLOB.never_state;
        return GLOB.default_state;
    }

    // tg: code/datums/components/religious_tool.dm:110
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        //cannot find global vars, so lets offer options
        if (!Dm.truthy(SetGlobalToLocal()))
        {
            data["sects"] = generate_available_sects(user);
            data["alignment"] = ALIGNMENT_NEUT; //neutral theme if you have no sect
        }
        else
        {
            data["sects"] = null;
            data["name"] = easy_access_sect.name;
            data["desc"] = easy_access_sect.desc;
            data["quote"] = easy_access_sect.quote;
            data["alignment"] = easy_access_sect.alignment;
            data["icon"] = easy_access_sect.tgui_icon;
            data["favordesc"] = easy_access_sect.tool_examine(user as mob_living);
            data["favor"] = easy_access_sect.favor;
            data["deity"] = GLOB.deity;
            data["rites"] = generate_available_rites();
            data["wanted"] = generate_sacrifice_list();
        }

        atom atom_parent = parent as atom;
        data["toolname"] = atom_parent.name;
        data["can_select_sect"] = Dm.bitand(operation_flags, RELIGION_TOOL_SECTSELECT);
        data["can_invoke_rite"] = Dm.bitand(operation_flags, RELIGION_TOOL_INVOKE);
        data["can_sacrifice_item"] = Dm.bitand(operation_flags, RELIGION_TOOL_SACRIFICE);
        return data;
    }

    // tg: code/datums/components/religious_tool.dm:136
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_act(action, @params, ui, state);
        object sw__1 = action;
        if (Dm.eq(sw__1, "sect_select"))
        {
            await select_sect(usr as mob_living, @params["path"]);
            return TRUE; //they picked a sect lets update so some weird spammy shit doesn't happen
        }
        else if (Dm.eq(sw__1, "perform_rite"))
        {
            if (!ispath(Dm.text2path(Dm.str(@params["path"])), typeof(datum_religion_rites)))
            {
                message_admins(Dm.text("[] has tried to spawn an item when performing a rite.", ADMIN_LOOKUPFLW(usr)));
                return dot;
            }
            await perform_rite(usr as mob_living, Dm.text2path(Dm.str(@params["path"])));
        }
        return dot;
    }

    /// Select the sect, called from [/datum/component/religious_tool/proc/AttemptActions]
    // tg: code/datums/components/religious_tool.dm:149
    public virtual async DmTask select_sect(mob_living user = null, object path = null)
    {
        if (!Dm.eq(user.mind.holy_role, HOLY_ROLE_HIGHPRIEST))
        {
            to_chat(user, span_warning(I18n.T("You are not the high priest, and therefore cannot select a religious sect.")));
            return;
        }
        if (!Dm.truthy(await user.can_perform_action(parent as atom, FORBID_TELEKINESIS_REACH)))
        {
            to_chat(user, span_warning(I18n.T("You cannot select a sect at this time.")));
            return;
        }
        await set_new_religious_sect(Dm.text2path(Dm.str(path)));
    }

    /// Perform the rite, called from [/datum/component/religious_tool/proc/AttemptActions]
    // tg: code/datums/components/religious_tool.dm:159
    public virtual async DmTask perform_rite(mob_living user = null, object path = null)
    {
        if (Dm.lt(user.mind.holy_role, HOLY_ROLE_PRIEST))
        {
            if (Dm.eq(user.mind.holy_role, HOLY_ROLE_DEACON))
                to_chat(user, span_warning(I18n.T("You are merely a deacon of [], and therefore cannot perform rites.", GLOB.deity)));
            else
                to_chat(user, span_warning(I18n.T("You are not holy, and therefore cannot perform rites.")));
            return;
        }
        if (rite_types_allowlist != null && !Dm.truthy(is_path_in_list(path, rite_types_allowlist)))
        {
            to_chat(user, span_warning(I18n.T("This cannot perform that kind of rite.")));
            return;
        }
        if (!Dm.truthy(await user.can_perform_action(parent as atom, FORBID_TELEKINESIS_REACH)))
        {
            to_chat(user, span_warning(I18n.T("You are not close enough to perform the rite.")));
            return;
        }
        //we have a rite already, but we want to do a new one.
        if (performing_rite != null && !ispath(performing_rite.type, path as Type))
            QDEL_NULL(ref performing_rite);
        if (performing_rite == null)
            performing_rite = await Dm.new_async((Type)path, parent) as datum_religion_rites;

        if (!Dm.truthy(await performing_rite.perform_rite(user, parent as atom)))
        {
            if (!Dm.truthy(Dm.bitand(performing_rite.rite_flags, RITE_ALLOW_MULTIPLE_PERFORMS)))
                QDEL_NULL(ref performing_rite);
            return;
        }

        if (Dm.truthy(await performing_rite.invoke_effect(user, parent as atom)))
        {
            await performing_rite.post_invoke_effects(user, parent as atom);
            easy_access_sect.adjust_favor(-performing_rite.favor_cost);
        }

        if (!Dm.truthy(Dm.bitand(performing_rite.rite_flags, RITE_ALLOW_MULTIPLE_PERFORMS)))
        {
            if (Dm.truthy(Dm.bitand(performing_rite.rite_flags, RITE_AUTO_DELETE)))
                QDEL_NULL(ref performing_rite);
            else
                performing_rite = null;
        }

        if (Dm.truthy(charges))
        {
            Dm.dec(ref charges);
            if (!Dm.truthy(charges))
                qdel(this);
        }
    }

    /**
     * Generates a list of available sects to the user. Intended to support custom-availability sects.
     */
    // tg: code/datums/components/religious_tool.dm:201
    public virtual object generate_available_sects(mob user = null)
    {
        DmList sects_to_pick = Dm.list();
        foreach (object path in Dm.each(subtypesof(typeof(datum_religion_sect))))
        {
            DmList sect = Dm.list();
            Type not_a_real_instance_rs = path as Type;
            sect["name"] = Dm.initial(not_a_real_instance_rs, (datum_religion_sect p) => p.name);
            sect["desc"] = Dm.initial(not_a_real_instance_rs, (datum_religion_sect p) => p.desc);
            sect["alignment"] = Dm.initial(not_a_real_instance_rs, (datum_religion_sect p) => p.alignment);
            sect["quote"] = Dm.initial(not_a_real_instance_rs, (datum_religion_sect p) => p.quote);
            sect["icon"] = Dm.initial(not_a_real_instance_rs, (datum_religion_sect p) => p.tgui_icon);
            sect["path"] = path;
            Dm.append(ref sects_to_pick, Dm.list(sect));
        }
        return sects_to_pick;
    }

    /**
     * Generates available rites to pick from. It expects the sect to be picked by the time it was called (by tgui data)
     */
    // tg: code/datums/components/religious_tool.dm:218
    public virtual object generate_available_rites()
    {
        DmList rites_to_pick = Dm.list();
        foreach (object path in Dm.each(easy_access_sect.rites_list))
        {
            ///checks to invalidate
            DmList rite = Dm.list();
            Type rite_type = path as Type;
            rite["name"] = Dm.initial(rite_type, (datum_religion_rites p) => p.name);
            rite["desc"] = Dm.initial(rite_type, (datum_religion_rites p) => p.desc);
            float cost = Dm.initial(rite_type, (datum_religion_rites p) => p.favor_cost);
            rite["favor"] = cost;
            rite["can_cast"] = Dm.num(cost > easy_access_sect.favor);
            rite["path"] = path;
            Dm.append(ref rites_to_pick, Dm.list(rite));
        }
        return rites_to_pick;
    }

    /**
     * Generates an english list (so string) of wanted sac items. Returns null if no targets!
     */
    // tg: code/datums/components/religious_tool.dm:236
    public virtual object generate_sacrifice_list()
    {
        if (!Dm.truthy(length(easy_access_sect?.desired_items)))
            return null; //specifically null so the data sends as such
        DmList item_names = Dm.list();
        foreach (Type sac_type in Dm.each_cast<Type>(easy_access_sect.desired_items))
        {
            object append = easy_access_sect.desired_items[sac_type];
            string entry = I18n.T("[]s []", Dm.initial(sac_type, (atom p) => p.name), append);
            Dm.append(ref item_names, entry);
        }
        return english_list(item_names);
    }

    /**
     * Appends to examine so the user knows it can be used for religious purposes.
     */
    // tg: code/datums/components/religious_tool.dm:249
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        float can_i_see = FALSE;
        if (isobserver(user))
            can_i_see = TRUE;
        else if (isliving(user))
        {
            mob_living L = user as mob_living;
            if (Dm.truthy(L.mind?.holy_role))
                can_i_see = TRUE;
        }

        if (!Dm.truthy(can_i_see))
            return;
        Dm.append(ref examine_list, span_notice(I18n.T("Use a [] to interact with this.", Dm.initial(catalyst_type, (atom p) => p.name))));
        if (easy_access_sect == null)
        {
            if (Dm.truthy(Dm.bitand(operation_flags, RELIGION_TOOL_SECTSELECT)))
            {
                Dm.append(ref examine_list, span_notice(I18n.T("This looks like it can be used to select a sect.")));
                return;
            }
        }
        if (Dm.truthy(Dm.bitand(operation_flags, RELIGION_TOOL_SACRIFICE)))//this can be moved around if things change but usually no rites == no sacrifice
            Dm.append(ref examine_list, span_notice(I18n.T("Desired items can be used on this to increase favor.")));
        if (Dm.truthy(easy_access_sect.rites_list) && Dm.truthy(Dm.bitand(operation_flags, RELIGION_TOOL_INVOKE)))
            Dm.append(ref examine_list, span_notice(I18n.T("You can invoke rites from this.")));
    }
}
