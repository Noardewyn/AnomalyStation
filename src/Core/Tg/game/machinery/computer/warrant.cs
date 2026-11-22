// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/warrant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps SC2, D333): the security warrant console in tgui's WarrantConsole window: the crew's outstanding citations,
// paid from the payer's bank account into the security budget (the citation's owner is told on their PDA), and a printed bounty
// paper for a fine. The var block is generated (D111).

// tg: code/game/machinery/computer/warrant.dm:1
public partial class obj_machinery_computer_warrant : obj_machinery_computer
{
    // tg: code/game/machinery/computer/warrant.dm:12
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui, __extra);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "WarrantConsole", name);
            ui.set_autoupdate(FALSE);
            await ui.open();
        }
        return dot;
    }

    // tg: code/game/machinery/computer/warrant.dm:20
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        DmList records = Dm.list();

        foreach (datum_record_crew target in Dm.each<datum_record_crew>(GLOB.manifest.general))
        {
            if (!Dm.truthy(length(target.citations)))
                continue;

            DmList citations = Dm.list();

            foreach (datum_crime_citation warrant in Dm.each_cast<datum_crime_citation>(target.citations))
            {
                if (!Dm.truthy(warrant.valid))
                    continue;

                DmList entry = Dm.list(Dm.list(
                    Dm.kv("author", warrant.author),
                    Dm.kv("details", warrant.details),
                    Dm.kv("fine", warrant.fine),
                    Dm.kv("fine_name", warrant.name),
                    Dm.kv("fine_ref", REF(warrant)),
                    Dm.kv("paid", warrant.paid),
                    Dm.kv("time", warrant.time)));

                Dm.append(ref citations, entry);
            }

            DmList record = Dm.list(Dm.list(
                Dm.kv("citations", citations),
                Dm.kv("crew_name", target.name),
                Dm.kv("crew_ref", REF(target)),
                Dm.kv("notes", target.security_note),
                Dm.kv("rank", target.rank)));

            Dm.append(ref records, record);
        }
        data["records"] = records;

        return data;
    }

    // tg: code/game/machinery/computer/warrant.dm:60
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return FALSE;

        object sw__1 = action;
        if (Dm.eq(sw__1, "pay"))
        {
            await pay_fine(usr, @params);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "print"))
        {
            ui.close();
            await print_bounty(usr, @params);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "refresh"))
            return TRUE;

        return FALSE;
    }

    /// Pays towards a listed fine.
    // tg: code/game/machinery/computer/warrant.dm:81
    public virtual async DmTask<object> pay_fine(mob user = null, DmList @params = null)
    {
        datum_record_crew target = Dm.locate_ref(@params["crew_ref"]) is datum_record_crew crew__1 && Dm.@in(crew__1, GLOB.manifest.general) ? crew__1 : null; // locate(params["crew_ref"]) in GLOB.manifest.general
        if (target == null)
            return FALSE;

        datum_crime_citation warrant = Dm.locate_ref(@params["fine_ref"]) is datum_crime_citation fine__1 && Dm.@in(fine__1, target.citations) ? fine__1 : null; // locate(params["fine_ref"]) in target.citations
        if (warrant == null)
            return FALSE;

        if (!isliving(user) || issilicon(user))
        {
            to_chat(user, span_warning(I18n.T("ACCESS DENIED")));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 100, TRUE);
            return FALSE;
        }

        mob_living player = (mob_living)user;
        obj_item_card_id auth = player.get_idcard(TRUE) as obj_item_card_id;
        if (auth == null)
        {
            to_chat(user, span_warning(I18n.T("ACCESS DENIED: No ID card detected.")));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 100, TRUE);
            return FALSE;
        }

        datum_bank_account account = auth.registered_account;
        if (!Dm.truthy(account?.account_holder) || Dm.eq(account.account_holder, "Unassigned"))
        {
            to_chat(user, span_warning(I18n.T("ACCESS DENIED: No account linked to ID.")));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 100, TRUE);
            return FALSE;
        }

        object amount = @params["amount"];
        if (!Dm.truthy(amount) || !isnum(amount) || Dm.num(amount) <= 0 || Dm.gt(amount, warrant.fine) || !Dm.truthy(account.adjust_money(-Dm.num(amount), I18n.T("Paid fine for []", target.name))))
        {
            to_chat(user, span_warning(I18n.T("ACCESS DENIED: Invalid amount.")));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 100, TRUE);
            return FALSE;
        }

        await account.bank_card_talk(I18n.T("You have paid [][] towards []'s fine of [][].", amount, MONEY_SYMBOL, target.name, warrant.fine, MONEY_SYMBOL));
        log_econ(Dm.text("[][] was transferred from []'s transaction to []'s [][] fine", amount, MONEY_SYMBOL, user, target.name, warrant.fine, MONEY_SYMBOL));
        SSblackbox.record_feedback("amount", "credits_transferred", amount);
        warrant.pay_fine(amount);

        if (Dm.num(amount) >= 100 && !Dm.eq(target?.name, user))
        {
            DmList titles = Dm.list(
                I18n.T("An anonymous benefactor"),
                I18n.T("A generous citizen"),
                I18n.T("A kind soul"),
                I18n.T("A good samaritan"),
                I18n.T("A friendly face"),
                I18n.T("A helpful stranger"));
            warrant.alert_owner(user, this, target.name, I18n.T("[] has paid [][] towards your fine.", pick(titles), amount, MONEY_SYMBOL));
        }

        datum_bank_account sec_account = SSeconomy.get_dep_account(ACCOUNT_SEC);
        sec_account.adjust_money(amount);
        // SSblackbox.ReportCitation(REF(warrant), paid = warrant.paid): the database's citation table (rules 13, not-translated.md 198)

        if (!Dm.eq(warrant.fine, 0) || Dm.eq(target.name, user))
            return TRUE;

        warrant.alert_owner(user, this, target.name, I18n.T("One of your outstanding warrants has been completely paid."));
        warrant.valid = FALSE;
        return TRUE;
    }

    /// Finishes printing, resets the printer.
    // tg: code/game/machinery/computer/warrant.dm:142
    public virtual async DmTask<object> print_finish(obj_item_paper bounty = null)
    {
        printing = FALSE;
        playsound(this, Dm.resource("sound/machines/terminal/terminal_eject.ogg"), 100, TRUE);
        await bounty.forceMove(loc);

        return TRUE;
    }

    /// Prints a bounty for a listed fine.
    // tg: code/game/machinery/computer/warrant.dm:150
    public virtual async DmTask<object> print_bounty(mob user = null, DmList @params = null)
    {
        if (Dm.truthy(printing))
        {
            balloon_alert(user, I18n.T("printer busy"));
            playsound(this, Dm.resource("sound/machines/terminal/terminal_error.ogg"), 100, TRUE);
            return FALSE;
        }

        datum_record_crew target = Dm.locate_ref(@params["crew_ref"]) is datum_record_crew crew__1 && Dm.@in(crew__1, GLOB.manifest.general) ? crew__1 : null; // locate(params["crew_ref"]) in GLOB.manifest.general
        if (target == null)
            return FALSE;

        datum_crime_citation warrant = Dm.locate_ref(@params["fine_ref"]) is datum_crime_citation fine__1 && Dm.@in(fine__1, target.citations) ? fine__1 : null; // locate(params["fine_ref"]) in target.citations
        if (!Dm.truthy(warrant?.fine))
            return FALSE;

        // the paper's text is the paper's own (as get_rapsheet's, record.cs): Dm.text, its name and description I18n.T
        string bounty_text = Dm.text("<center><h2><b>Bounty for []</b><h2></center><BR>", target.name);
        bounty_text += Dm.text("<center>Wanted for []</h2></center><br><br>", warrant.name);
        bounty_text += Dm.text("<b>Details:</b><br>[]<br>", warrant.details);
        bounty_text += Dm.text("<b>Issued to:</b><br>[]<br>", usr);
        bounty_text += Dm.text("<b>Issued on:</b><br>[]<br>", warrant.time);
        bounty_text += Dm.text("<b>Comments:</b><br>[]<br><br>", !Dm.truthy(target.security_note) ? "None." : target.security_note);
        bounty_text += Dm.text("<center><b>FINE:</b> [] []</center>", warrant.fine, MONEY_NAME);

        printing = TRUE;
        balloon_alert(user, I18n.T("printing"));
        playsound(this, Dm.resource("sound/machines/printer.ogg"), 100, TRUE);

        obj_item_paper bounty = Dm.@new<obj_item_paper>(null);
        bounty.name = I18n.T("Bounty for []", target.name);
        bounty.desc = I18n.T("A [][] bounty for [].", warrant.fine, MONEY_SYMBOL, target.name);
        bounty.add_raw_text(bounty_text);
        await bounty.update_icon();

        _addtimer(Dm.@new<datum_callback>(this, nameof(print_finish), bounty), 2 * SECONDS, file: "code/game/machinery/computer/warrant.dm", line: 182);

        return TRUE;
    }
}
