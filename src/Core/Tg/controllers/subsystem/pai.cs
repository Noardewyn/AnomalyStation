// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/pai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6 (D326): the whole file.

// tg: code/controllers/subsystem/pai.dm:1
public partial class datum_controller_subsystem_pai : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(pai) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSpai) (__DEFINES/MC.dm:29) expanded inline
    // tg: code/controllers/subsystem/pai.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSpai != this)
        {
            if (SSpai is datum_controller_subsystem_pai)
            {
                Recover();
                qdel(SSpai);
            }
            SSpai = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/pai.dm:20
    public override void Recover()
    {
        base.Recover();
        candidates = SSpai.candidates;
        pai_card_list = SSpai.pai_card_list;
    }

    // tg: code/controllers/subsystem/pai.dm:12
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "PaiSubmit");
            await ui.open();
            ui.set_autoupdate(FALSE);
        }
        return null;
    }

    // tg: code/controllers/subsystem/pai.dm:25
    public override object ui_state(mob user = null) => GLOB.observer_state;

    // tg: code/controllers/subsystem/pai.dm:28
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        var candidate = candidates[user.ckey] as datum_pai_candidate;
        if (candidate == null)
            return data;
        data["comments"] = candidate.comments;
        data["description"] = candidate.description;
        data["name"] = candidate.name;
        return data;
    }

    // tg: code/controllers/subsystem/pai.dm:39
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return TRUE;
        mob user = ui.user;
        var candidate = candidates[user.ckey] as datum_pai_candidate;
        if (Dm.truthy(is_banned_from(user.ckey, ROLE_PAI)))
        {
            to_chat(user, span_warning(I18n.T("You are banned from playing pAI!")));
            ui.close();
            return FALSE;
        }
        if (candidate == null)
        {
            to_chat(user, span_warning(I18n.T("There was an error. Please resubmit.")));
            ui.close();
            return FALSE;
        }
        switch (Dm.str(action))
        {
            case "submit":
                read_fields(candidate, @params);
                candidate.ckey = user.ckey;
                candidate.ready = TRUE;
                ui.close();
                submit_alert(user);
                return TRUE;
            case "save":
                read_fields(candidate, @params);
                candidate.savefile_save(user);
                return TRUE;
            case "load":
                await candidate.savefile_load(user);
                await ui.send_full_update();
                return TRUE;
            case "withdraw":
                if (!Dm.truthy(candidate.ready))
                {
                    to_chat(user, span_warning(I18n.T("You need to submit an application before you can withdraw one.")));
                    return FALSE;
                }
                candidate.ready = FALSE;
                to_chat(user, span_notice(I18n.T("Your pAI candidacy has been withdrawn.")));
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/controllers/subsystem/pai.dm:55-57 and :64-66 (the same three lines in "submit" and "save")
    private static void read_fields(datum_pai_candidate candidate, DmList @params)
    {
        // reject_bad_name(...) || "Unknown"
        static object or_unknown(object v) => Dm.truthy(v) ? v : "Unknown";
        candidate.comments = or_unknown(reject_bad_name(@params["comments"], allow_numbers: TRUE, max_length: MAX_BROADCAST_LEN, strict: TRUE, cap_after_symbols: FALSE));
        candidate.description = or_unknown(reject_bad_name(@params["description"], allow_numbers: TRUE, max_length: MAX_BROADCAST_LEN, strict: TRUE, cap_after_symbols: FALSE));
        candidate.name = or_unknown(reject_bad_name(@params["name"], allow_numbers: TRUE, max_length: MAX_NAME_LEN, strict: TRUE, cap_after_symbols: FALSE));
    }

    /**
     * This is the primary window proc when the pAI candidate
     * hud menu is pressed by observers.
     *
     * @params {mob} user The ghost doing the pressing.
     */
    // tg: code/controllers/subsystem/pai.dm:88
    public virtual async DmTask recruit_window(mob user = null)
    {
        /// Searches for a previous candidate upon opening the menu
        var candidate = candidates[user.ckey] as datum_pai_candidate;
        if (candidate == null)
        {
            candidate = Dm.@new<datum_pai_candidate>(user.ckey);
            candidates[user.ckey] = candidate;
        }
        await ui_interact(user);
    }

    // tg: code/controllers/subsystem/pai.dm:109  VARSET_CALLBACK(src, submit_spam, FALSE)
    public virtual void reset_submit_spam() => submit_spam = FALSE;

    /**
     * Pings all pAI cards on the station that new candidates are available.
     */
    // tg: code/controllers/subsystem/pai.dm:100
    public virtual object submit_alert(mob user = null)
    {
        if (Dm.truthy(submit_spam))
        {
            to_chat(user, span_warning(I18n.T("Your candidacy has been submitted, but pAI cards have been alerted too recently.")));
            return FALSE;
        }
        submit_spam = TRUE;
        foreach (obj_item_pai_card pai_card in Dm.each<obj_item_pai_card>(pai_card_list))
        {
            if (pai_card.pai == null)
                pai_card.alert_update();
        }
        to_chat(user, span_notice(I18n.T("Your pAI candidacy has been submitted!")));
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_submit_spam)), PAI_SPAM_TIME, Dm.bitor(TIMER_UNIQUE, TIMER_DELETE_ME), file: "code/controllers/subsystem/pai.dm", line: 109);
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/pai.dm:1  SUBSYSTEM_DEF(pai): GLOBAL_REAL(SSpai, /datum/controller/subsystem/pai)
    internal datum_controller_subsystem_pai SSpai;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_pai SSpai => ref TgState.Current.SSpai;
}
