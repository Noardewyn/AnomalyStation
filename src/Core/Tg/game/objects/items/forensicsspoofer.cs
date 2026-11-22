// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/forensicsspoofer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298).

// tg: code/game/objects/items/forensicsspoofer.dm:2
public partial class obj_item_forensics_spoofer : obj_item
{
    // tg: code/game/objects/items/forensicsspoofer.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // most things have add_fingerprint in their item interaction because lol lmao
        // tl;dr cut off the chain before anything fires so we dont add user fingerprints to target
        RegisterSignal(this, COMSIG_ITEM_INTERACTING_WITH_ATOM, nameof(do_interact));
        return dot;
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:37
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        object dot = await base.attack_self_secondary(user, modifiers);
        if (Dm.truthy(dot))
            return dot;
        scan_mode = Dm.truthy(scan_mode) ? FALSE : TRUE;
        balloon_alert(user, I18n.T("now []", Dm.truthy(scan_mode) ? I18n.T("scanning") : I18n.T("applying")));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // ok due to shenanigans basically every item interact adds your fingerprints to it which isnt ideal so we have this
    // tg: code/game/objects/items/forensicsspoofer.dm:46
    [SignalHandler]
    public virtual object do_interact(datum source = null, mob_living user = null, atom interacting_with = null, DmList modifiers = null)
    {
        if (Dm.truthy(scan_mode))
            Waiting.Detach(scan(interacting_with, user)); // INVOKE_ASYNC
        else
            tamper(interacting_with, user, chosen_fiber != null ? TRUE : FALSE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:54
    public virtual void do_fake_scan(atom target = null, mob user = null)
    {
        if (Dm.truthy(silent_mode))
            return;
        playsound(this, SFX_INDUSTRIAL_SCAN, 20, TRUE, -2, TRUE, FALSE);
        user.visible_message(span_notice(I18n.T("\\The [] points \\the [] at \\the [] and performs a forensic scan.", user, this, target)));
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:62
    public virtual void clear_values(DmList the_list = null)
    {
        foreach (object key in Dm.each(the_list))
            the_list[key] = "";
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:66
    public virtual async DmTask<object> scan(atom target = null, mob_living user = null)
    {
        do_fake_scan(target, user);
        if (target.forensics == null)
        {
            target.balloon_alert(user, I18n.T("nothing!"));
            return ITEM_INTERACT_FAILURE;
        }
        DmList new_fibers = Dm.sub(LAZYCOPY(target.forensics.fibers), fibers) as DmList;
        DmList new_prints = Dm.sub(LAZYCOPY(target.forensics.fingerprints), fingerprints) as DmList;
        float new_len = length(new_fibers) + length(new_prints);
        balloon_alert(user, I18n.T("[] new prints/fibers", new_len > 0 ? new_len : I18n.T("no")));
        if (new_len > 0)
        {
            DmList message = Dm.list(span_bold(I18n.T("Scan results (Unstored Only):")));
            foreach (object text in Dm.each(new_fibers))
                Dm.append(ref message, span_notice(I18n.T("Fiber: []", text)));
            if (length(fibers) > max_storage)
                Dm.append(ref message, span_boldwarning(I18n.T("Fiber storage full.")));
            foreach (object text in Dm.each(new_prints))
                Dm.append(ref message, span_notice(I18n.T("Fingerprint: []", text)));
            if (length(fingerprints) > max_storage)
                Dm.append(ref message, span_boldwarning(I18n.T("Fingerprint storage full.")));
            to_chat(user, boxed_message(jointext(message, "\n")), type: MESSAGE_TYPE_INFO);
        }
        if (length(fingerprints) < max_storage)
        {
            while (length(fingerprints) + length(new_prints) > max_storage)
            {
                // tg's own slip: it offers new_fibers here, so the prints cannot be picked while there are none; kept
                object to_remove = await tgui_input_list(user, I18n.T("Too many prints, cancel to discard all"), I18n.T("What to discard"), new_fibers);
                if (to_remove == null)
                    return ITEM_INTERACT_FAILURE;
                new_prints.Remove(to_remove);
            }
            clear_values(new_prints);
            Dm.append(ref fingerprints, new_prints);
            foreach (object fingerprint in Dm.each(fingerprints))
                fingerprints[fingerprint] = get_name_from_fingerprint(fingerprint);
        }
        if (length(fibers) < max_storage)
        {
            while (length(fibers) + length(new_fibers) > max_storage)
            {
                object to_remove = await tgui_input_list(user, I18n.T("Too many prints, cancel to discard all"), I18n.T("What to discard"), new_fibers);
                if (to_remove == null)
                    return ITEM_INTERACT_FAILURE;
                new_fibers.Remove(to_remove);
            }
            clear_values(new_fibers);
            Dm.append(ref fibers, new_fibers);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:106
    public virtual object tamper(atom target = null, mob_living user = null, float? do_fibers = null)
    {
        bool fibers_mode = Dm.truthy(do_fibers);
        do_fake_scan(target, user);
        if ((!fibers_mode && chosen_fingerprint == null) || (fibers_mode && chosen_fiber == null))
        {
            balloon_alert(user, I18n.T("no [] selected!", fibers_mode ? I18n.T("fiber") : I18n.T("fingerprint"))); // we CAN automatically select it but if they dont have it selected then they likely didnt know of it in the first place so they learn it now
            return ITEM_INTERACT_FAILURE;
        }
        if (tamper_cooldown > Dm.num(world.time)) // COOLDOWN_FINISHED, inline
        {
            balloon_alert(user, I18n.T("please wait!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (target.forensics != null && Dm.truthy(LAZYFIND(fibers_mode ? target.forensics.fibers : target.forensics.fingerprints, fibers_mode ? chosen_fiber : chosen_fingerprint)))
        {
            balloon_alert(user, I18n.T("already present!"));
            return ITEM_INTERACT_FAILURE;
        }

        if (fibers_mode)
        {
            target.add_fiber_list(Dm.list(chosen_fiber));
            user.log_message(Dm.text("has tampered with the fingerprints/fibers of []. Added []", this, chosen_fiber), LOG_ATTACK);
        }
        else
        {
            target.add_fingerprint_list(Dm.list(chosen_fingerprint));
            user.log_message(Dm.text("has tampered with the fingerprints/fibers of []. Added []", this, chosen_fingerprint), LOG_ATTACK);
        }

        target.balloon_alert(user, I18n.T("[] added", fibers_mode ? I18n.T("fiber") : I18n.T("fingerprint")));
        target.add_hiddenprint(user);
        tamper_cooldown = Dm.num(world.time) + tamper_cooldown_time; // COOLDOWN_START, inline

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:131
    public virtual object get_name_from_fingerprint(object fingerprint = null)
    {
        foreach (datum_record_crew player_record in Dm.each<datum_record_crew>(GLOB.manifest?.general))
        {
            if (!Dm.eq(player_record.fingerprint, fingerprint))
                continue;
            return player_record.name;
        }
        return I18n.T("Unknown");
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:138
    public override object ui_state(mob user = null)
    {
        return GLOB.hands_state;
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:141
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ForensicsSpoofer", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:147
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        return Dm.list(Dm.kv("max_storage", max_storage));
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:152
    public override async DmTask<object> ui_data(mob user = null)
    {
        return Dm.list(
            Dm.kv("scanmode", scan_mode),
            Dm.kv("silent", silent_mode),
            Dm.kv("fibers", fibers),
            Dm.kv("fingerprints", fingerprints),
            Dm.kv("chosen_fiber", chosen_fiber),
            Dm.kv("chosen_fingerprint", chosen_fingerprint));
    }

    // tg: code/game/objects/items/forensicsspoofer.dm:162
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (@params["chosen"] != null) //fiber/print actions
        {
            object chosen = @params["chosen"];
            switch (action)
            {
                case "delete":
                    if (Dm.@in(chosen, fibers))
                    {
                        if (Dm.eq(chosen_fiber, chosen))
                            chosen_fiber = null;
                        fibers.Remove(chosen);
                    }
                    else
                    {
                        if (Dm.eq(chosen_fingerprint, chosen))
                            chosen_fingerprint = null;
                        fingerprints.Remove(chosen);
                    }
                    return TRUE;
                case "choose":
                {
                    bool is_fiber = Dm.@in(chosen, fibers);
                    chosen_fiber = is_fiber ? chosen : null;
                    chosen_fingerprint = is_fiber ? null : chosen;
                    return TRUE;
                }
                case "make_note":
                    if (Dm.@in(chosen, fibers))
                        fibers[chosen] = @params["note"];
                    else
                        fingerprints[chosen] = @params["note"];
                    return TRUE;
            }
        }
        else
        {
            switch (action)
            {
                case "scanmode":
                    scan_mode = Dm.truthy(scan_mode) ? FALSE : TRUE;
                    return TRUE;
                case "stealth":
                    silent_mode = Dm.truthy(silent_mode) ? FALSE : TRUE;
                    return TRUE;
            }
        }
        return dot;
    }
}
