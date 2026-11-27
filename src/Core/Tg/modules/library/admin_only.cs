// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/admin_only.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The admins' library console (LibraryAdmin) and its book viewer (AdminBookViewer): the archive's moderation, hiding and restoring
// uploaded books with a logged reason. All of it is tg's SQL library table (SSdbcore): the port connects to no tg database, so the
// console's window closes at once (ui_status, tg's own no-connection path) and the queries behind a connection are not translated
// (rules 13). The var blocks are generated.

// tg: code/modules/library/admin_only.dm:5
public partial class obj_machinery_computer_libraryconsole_admin_only_do_not_map_in_you_fucker : obj_machinery_computer_libraryconsole
{
    // tg: code/modules/library/admin_only.dm:1-3  the file's own #defines
    private const string BOOK_ADMIN_DELETE = "deleted";
    private const string BOOK_ADMIN_RESTORE = "undeleted";
    private const string BOOK_ADMIN_REPORT = "reported";

    // tg: code/modules/library/admin_only.dm:17
    public override object can_db_request()
    {
        if (Dm.truthy(sending_request))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/library/admin_only.dm:22
    public override object hash_search_info()
    {
        object dot = base.hash_search_info();
        return Dm.text("[]-[]-[]", dot, ckey, show_deleted);
    }

    // tg: code/modules/library/admin_only.dm:26
    public override async DmTask update_page_contents()
    {
        if (Dm.truthy(sending_request)) //Final defense against nerds spamming db requests
            return;
        sending_request = TRUE;
        search_page = clamp(search_page, 0, page_count);
        // tg: :31-44  the library table's page query with the deleted books and the uploaders' ckeys (SSdbcore.NewQuery, Execute):
        // SQL is not translated (rules 13), and nothing reaches it without a connection; the rest is tg's failed query
        Dm.NotTranslated("code/modules/library/admin_only.dm:31", "update_page_contents: the library table's admin page query (SQL)");
        await DmTask.CompletedTask;
        sending_request = FALSE;
        page_content.Cut();
    }

    // tg: code/modules/library/admin_only.dm:62
    public override async DmTask update_page_count()
    {
        // tg: :63-79  the library table's admin count query (SSdbcore.NewQuery, warn_execute): SQL is not translated (rules 13), and
        // nothing reaches it without a connection; tg returns on a failed query
        Dm.NotTranslated("code/modules/library/admin_only.dm:64", "update_page_count: the library table's admin count query (SQL)");
        await DmTask.CompletedTask;
    }

    // tg: code/modules/library/admin_only.dm:84
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!Dm.truthy(check_rights_for(user.client, R_BAN)))
            return UI_CLOSE;
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            can_connect = FALSE;
            return UI_CLOSE;
        }
        return UI_INTERACTIVE;
    }

    // tg: code/modules/library/admin_only.dm:92
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
        {
            // We'll always trigger a search attempt if the parent does something, this ensures the ui is v fast to update
            Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
            return dot;
        }
        switch (action)
        {
            case "set_search_ckey":
                ckey = (string)@params["ckey"];
                Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
                return TRUE;
            case "refresh":
                last_search_hash = "";
                Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
                return TRUE;
            case "hide_book":
            {
                object reason = @params["delete_reason"];
                object id = @params["book_id"];
                client actor = ui.user?.client;
                if (actor == null)
                    return dot;
                Waiting.Detach(hide_book(id, reason, actor)); // INVOKE_ASYNC(src, PROC_REF(hide_book), id, reason, actor)
                return TRUE;
            }
            case "unhide_book":
            {
                object reason = @params["free_reason"];
                object id = @params["book_id"];
                client actor = ui.user?.client;
                if (actor == null)
                    return dot;
                Waiting.Detach(unhide_book(id, reason, actor)); // INVOKE_ASYNC(src, PROC_REF(unhide_book), id, reason, actor)
                return TRUE;
            }
            case "get_history":
            {
                object id = @params["book_id"];
                book_history[Dm.text("[]", id)] = await get_book_history(id);
                return TRUE;
            }
            case "view_book":
            {
                object id = @params["book_id"];
                await view_book(id, ui.user);
                return TRUE;
            }
            case "toggle_raw":
                view_raw = Dm.num(!Dm.truthy(view_raw));
                return TRUE;
            case "toggle_deleted":
                show_deleted = Dm.num(!Dm.truthy(show_deleted));
                Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/library/admin_only.dm:137
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["view_raw"] = view_raw;
        dot["show_deleted"] = show_deleted;
        DmList histories = Dm.list();
        foreach (object id in Dm.each(book_history))
        {
            DmList insert = Dm.list();
            foreach (datum_book_history_entry entry in Dm.each<datum_book_history_entry>(book_history[id]))
                Dm.append(ref insert, Dm.list(entry.serialize()));
            histories[id] = insert;
        }
        dot["history"] = histories;
        return dot;
    }

    // tg: code/modules/library/admin_only.dm:150
    public virtual async DmTask view_book(object id = null, mob show_to = null)
    {
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            can_connect = FALSE;
            message_admins(I18n.T("Failed to establish database connection."));
            return;
        }

        // tg: :156-179  the book's query (SSdbcore.NewQuery, Execute) filling a /datum/admin_book_viewer that opens its window:
        // SQL is not translated (rules 13), and nothing reaches it without a connection
        Dm.NotTranslated("code/modules/library/admin_only.dm:156", "view_book: the library table's book query (SQL)");
        await DmTask.CompletedTask;
    }

    // tg: code/modules/library/admin_only.dm:181
    public virtual async DmTask<object> get_book_history(object id = null)
    {
        // tg: :182-204  the library_action table's query (SSdbcore.NewQuery, Execute) made /datum/book_history_entry records: SQL is not
        // translated (rules 13), and nothing reaches it without a connection (the window that asks for it closes, ui_status); a
        // failed query gives tg's empty list
        Dm.NotTranslated("code/modules/library/admin_only.dm:182", "get_book_history: the library_action table's query (SQL)");
        await DmTask.CompletedTask;
        return Dm.list();
    }

    // tg: code/modules/library/admin_only.dm:206
    public virtual async DmTask hide_book(object id = null, object reason = null, client admin = null)
    {
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            can_connect = FALSE;
            to_chat(admin, span_danger(I18n.T("Failed to establish database connection.")));
            return;
        }
        if (!Dm.truthy(check_rights_for(admin, R_BAN)))
        {
            log_admin_private(Dm.text("[] tried to hide a book without the required perms", admin.ckey));
            message_admins(Dm.text("[] tried to hide a book without the required perms", admin.ckey));
            return;
        }

        // tg: :217-239  the UPDATE that hides the book, the library_action log's INSERT (BOOK_ADMIN_DELETE), log_admin_private,
        // library_updated() and update_db_info(): SQL is not translated (rules 13), and nothing reaches it without a connection
        Dm.NotTranslated("code/modules/library/admin_only.dm:217", "hide_book: the library table's UPDATE and the library_action log (SQL)");
        await DmTask.CompletedTask;
    }

    // tg: code/modules/library/admin_only.dm:241
    public virtual async DmTask unhide_book(object id = null, object reason = null, client admin = null)
    {
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            can_connect = FALSE;
            to_chat(admin, span_danger(I18n.T("Failed to establish database connection.")));
            return;
        }
        if (!Dm.truthy(check_rights_for(admin, R_BAN)))
        {
            log_admin_private(Dm.text("[] tried to unhide a book without the required perms", admin.ckey));
            message_admins(Dm.text("[] tried to unhide a book without the required perms", admin.ckey));
            return;
        }

        // tg: :252-275  the UPDATE that restores the book, the library_action log's INSERT (BOOK_ADMIN_RESTORE), log_admin_private,
        // library_updated() and update_db_info(): SQL is not translated (rules 13), and nothing reaches it without a connection
        Dm.NotTranslated("code/modules/library/admin_only.dm:252", "unhide_book: the library table's UPDATE and the library_action log (SQL)");
        await DmTask.CompletedTask;
    }
}

/// This mostly exists to document the form of the library_action table, since it doesn't do that good a job on its own
// tg: code/modules/library/admin_only.dm:277
public partial class datum_book_history_entry : datum
{
    // tg: code/modules/library/admin_only.dm:292
    public virtual object serialize()
    {
        DmList data = Dm.list();
        data["id"] = id;
        data["book"] = book;
        data["reason"] = reason;
        data["ckey"] = ckey;
        data["datetime"] = datetime;
        data["action"] = action;
        data["address"] = ip_addr;
        return data;
    }
}

/// Weaps around a book's sql data, feeds it into a ui that allows us to at base view the contents of the book
// tg: code/modules/library/admin_only.dm:305
public partial class datum_admin_book_viewer : datum
{
    // tg: code/modules/library/admin_only.dm:330
    public virtual void set_owner(obj_machinery_computer_libraryconsole_admin_only_do_not_map_in_you_fucker owner = null)
    {
        owner_ref = WEAKREF(owner) as datum_weakref;
        view_raw = owner.view_raw;
    }

    // tg: code/modules/library/admin_only.dm:334
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AdminBookViewer");
            ui.set_autoupdate(FALSE); // Nothing is changing here brother
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/library/admin_only.dm:342
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        if (!Dm.truthy(check_rights_for(user.client, R_BAN)))
            return UI_CLOSE;
        return UI_INTERACTIVE;
    }

    // tg: code/modules/library/admin_only.dm:347
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["view_raw"] = view_raw;
        data["id"] = id;
        data["author"] = author;
        data["title"] = title;
        data["content"] = content;
        data["category"] = category;
        data["author_ckey"] = author_ckey;
        data["creation_time"] = creation_time;
        data["deleted"] = deleted;
        data["creation_round"] = creation_round;
        data["history"] = Dm.list();
        foreach (datum_book_history_entry entry in Dm.each_cast<datum_book_history_entry>(history))
            Dm.append_at(data, "history", Dm.list(entry.serialize()));

        return data;
    }
}
