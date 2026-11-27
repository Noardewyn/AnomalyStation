// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/lib_machines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The library's machines: the visitor console and its archive search (LibraryVisitor), the book management console with its
// inventory, check-outs, archive, upload, printing and forbidden lore (LibraryConsole), the scanner (LibraryScanner) and the book
// binder. The archive is tg's SQL library table (SSdbcore): the port connects to no tg database, so SSdbcore.Connect() fails and
// the consoles take tg's own no-connection paths (the window's "unable to retrieve book listings", the "connection to Archive has
// been severed" lines); the queries behind a connection are not translated (rules 13). The var blocks are generated.

/*
 * Library Machines
 *
 * Contains:
 * Borrowbook datum
 * Library Public Computer
 * Book Info datum
 * Library Computer
 * Library Scanner
 * Book Binder
 */

// tg: code/modules/library/lib_machines.dm:12
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/library/lib_machines.dm:12  GLOBAL_VAR_INIT(library_table_modified, 0)
    public float library_table_modified;

    // tg: code/modules/library/lib_machines.dm:12  (GLOBAL_MANAGED(library_table_modified, 0), __DEFINES/__globals.dm:5)
    public virtual void InitGloballibrary_table_modified()
    {
        library_table_modified = 0;
        Dm.append(ref gvars_datum_init_order, "library_table_modified");
    }
}

public static partial class GlobalProcs
{
    /// Increments every time WE update the library db table, causes all existing consoles to repull when they next check
    // tg: code/modules/library/lib_machines.dm:15
    public static void library_updated()
    {
        GLOB.library_table_modified = WRAP_UID(GLOB.library_table_modified + 1);
    }
}

/*
 * Library Public Computer
 */
// tg: code/modules/library/lib_machines.dm:21
public partial class obj_machinery_computer_libraryconsole : obj_machinery_computer
{
    // tg: code/modules/library/lib_machines.dm:56
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        category = DEFAULT_SEARCH_CATAGORY;
        Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:61
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        object dot = await base.ui_interact(user, ui);
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, interface_type);
            await ui.open();
        }
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:68
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["can_db_request"] = can_db_request();
        data["search_categories"] = SSlibrary.search_categories;
        data["category"] = category;
        data["author"] = author;
        data["title"] = title;
        data["book_id"] = book_id;
        data["page_count"] = page_count + 1; //Increase these by one so it looks like we're not indexing at 0
        data["our_page"] = search_page + 1;
        data["pages"] = page_content;
        data["can_connect"] = can_connect;
        data["params_changed"] = params_changed;
        return data;
    }

    // tg: code/modules/library/lib_machines.dm:83
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "set_search_id":
            {
                object newid = text2num(@params["id"]);
                if (!Dm.eq(newid, book_id))
                    params_changed = TRUE;
                book_id = newid;
                return TRUE;
            }
            case "set_search_title":
            {
                object newtitle = @params["title"];
                if (!Dm.eq(newtitle, title))
                    params_changed = TRUE;
                title = (string)newtitle;
                return TRUE;
            }
            case "set_search_category":
            {
                object newcategory = @params["category"];
                if (!Dm.@in(newcategory, SSlibrary.search_categories)) //Nice try
                    newcategory = DEFAULT_SEARCH_CATAGORY;
                if (!Dm.eq(newcategory, category))
                    params_changed = TRUE;
                category = (string)newcategory;
                return TRUE;
            }
            case "set_search_author":
            {
                object newauthor = @params["author"];
                if (!Dm.eq(newauthor, author))
                    params_changed = TRUE;
                author = (string)newauthor;
                return TRUE;
            }
            case "search":
                if (!Dm.truthy(prevent_db_spam()))
                {
                    await say(I18n.T("Database cables refreshing. Please wait a moment."));
                    return dot;
                }
                Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
                return TRUE;
            case "switch_page":
                if (!Dm.truthy(prevent_db_spam()))
                {
                    await say(I18n.T("Database cables refreshing. Please wait a moment."));
                    return dot;
                }
                search_page = Dm.num(sanitize_page_input(@params["page"], search_page, page_count));
                Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
                return TRUE;
            case "clear_data": //The cap just walked in on your browsing, quick! delete it!
                if (!Dm.truthy(prevent_db_spam()))
                {
                    await say(I18n.T("Database cables refreshing. Please wait a moment."));
                    return dot;
                }
                title = Dm.initial(this, (obj_machinery_computer_libraryconsole p) => p.title);
                author = Dm.initial(this, (obj_machinery_computer_libraryconsole p) => p.author);
                category = DEFAULT_SEARCH_CATAGORY;
                search_page = 0;
                Waiting.Detach(update_db_info()); // INVOKE_ASYNC(src, PROC_REF(update_db_info))
                return TRUE;
        }
        return dot;
    }

    ///Checks if the machine is alloweed to make another db request yet. TRUE if so, FALSE otherwise
    // tg: code/modules/library/lib_machines.dm:139
    public virtual object prevent_db_spam()
    {
        object allowed = can_db_request();
        if (!Dm.truthy(allowed))
            return FALSE;
        // COOLDOWN_START(src, db_request_cooldown, 1 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        db_request_cooldown = world.time + 1 * SECONDS;
        return TRUE;
    }

    // tg: code/modules/library/lib_machines.dm:146
    public virtual object can_db_request()
    {
        if (Dm.truthy(sending_request)) //Absolutely not
            return FALSE;
        if (!(db_request_cooldown <= world.time)) // COOLDOWN_FINISHED(src, db_request_cooldown)
            return FALSE;
        return TRUE;
    }

    ///Returns a santized page input, so converted from num/text to num, and properly maxed
    // tg: code/modules/library/lib_machines.dm:154
    public virtual object sanitize_page_input(object input = null, object @default = null, object max = null)
    {
        input = convert_ambiguous_input(input, Dm.num(@default) + 1); // + 1 to invert the below reasons
        //We expect the search page to be one greater then it should be, because we're lying about indexing at 1
        return clamp(Dm.num(input) - 1, 0, Dm.num(max));
    }

    ///Takes input that could either be a number, or a string that represents a number and returns a number
    // tg: code/modules/library/lib_machines.dm:160
    public virtual object convert_ambiguous_input(object input = null, object @default = null)
    {
        if (isnum(input))
            return input;
        if (!istext(input))
            return @default;
        object hidden_number = text2num(input);
        if (!isnum(hidden_number))
            return @default;
        return hidden_number;
    }

    // tg: code/modules/library/lib_machines.dm:170
    public virtual async DmTask update_db_info()
    {
        if (!Dm.truthy(has_anything_changed())) //You're not allowed to make the same search twice, waste of resources
            return;
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            can_connect = FALSE;
            page_count = 0;
            page_content = Dm.list();
            return;
        }
        can_connect = TRUE;
        params_changed = FALSE;
        last_search_hash = (string)hash_search_info();

        await update_page_count();
        await update_page_contents();
        SStgui.update_uis(this); //We need to do this because we sleep here, so we've gotta update manually
    }

    //Returns true if there's been an update worth refreshing our pages for, false otherwise
    // tg: code/modules/library/lib_machines.dm:187
    public virtual object has_anything_changed()
    {
        if (Dm.eq(last_search_hash, hash_search_info()))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/library/lib_machines.dm:192
    public virtual object hash_search_info()
    {
        return Dm.text("[]-[]-[]-[]-[]-[]-[]", GLOB.library_table_modified, book_id, title, author, category, search_page, page_count);
    }

    // tg: code/modules/library/lib_machines.dm:195
    public virtual async DmTask update_page_contents()
    {
        if (Dm.truthy(sending_request)) //Final defense against nerds spamming db requests
            return;
        sending_request = TRUE;
        search_page = clamp(search_page, 0, page_count);
        // tg: :200-212  the library table's page query (SSdbcore.NewQuery, Execute): SQL is not translated (rules 13), and nothing
        // reaches it without a connection (update_db_info); the rest is tg's failed query
        Dm.NotTranslated("code/modules/library/lib_machines.dm:200", "update_page_contents: the library table's page query (SQL)");
        await DmTask.CompletedTask;
        sending_request = FALSE;
        page_content.Cut();
    }

    // tg: code/modules/library/lib_machines.dm:227
    public virtual async DmTask update_page_count()
    {
        // tg: :228-243  the library table's count query (SSdbcore.NewQuery, warn_execute): SQL is not translated (rules 13), and
        // nothing reaches it without a connection (update_db_info); tg returns on a failed query
        Dm.NotTranslated("code/modules/library/lib_machines.dm:229", "update_page_count: the library table's count query (SQL)");
        await DmTask.CompletedTask;
    }
}

/*
 * Borrowbook datum
 */
// tg: code/modules/library/lib_machines.dm:251  /datum/borrowbook: a var-only datum, its var block generated

/*
 * Library Computer
 * After 860 days, it's finally a buildable computer.
 */
// tg: code/modules/library/lib_machines.dm:277
public partial class obj_machinery_computer_libraryconsole_bookmanagement : obj_machinery_computer_libraryconsole
{
    // tg: code/modules/library/lib_machines.dm:257-268  the file's own #defines
    private const float PRINTER_COOLDOWN = 6 * SECONDS;
    private const float NEWSCASTER_COOLDOWN = 10 * SECONDS;
    private const string LIBRARY_NEWSFEED = "Nanotrasen Book Club";
    //The different states the computer can be in, only send the info we need yeah?
    private const int LIBRARY_INVENTORY = 1;
    private const int LIBRARY_CHECKOUT = 2;
    private const int LIBRARY_ARCHIVE = 3;
    private const int LIBRARY_UPLOAD = 4;
    private const int LIBRARY_PRINT = 5;
    private const int LIBRARY_TOP_SNEAKY = 6;
    private const int MIN_LIBRARY = LIBRARY_INVENTORY;
    private const int MAX_LIBRARY = LIBRARY_TOP_SNEAKY;

    // tg: code/modules/library/lib_machines.dm:319
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            dynamic_inv_load = TRUE; //Only load in stuff if we were placed during mapload
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:324
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        data["inventory"] = Dm.list();
        float inventory_len = length(inventory);
        if (Dm.truthy(inventory_len))
        {
            for (float id = (INVENTORY_PER_PAGE * inventory_page) + 1, id__end = min(INVENTORY_PER_PAGE * (inventory_page + 1), inventory_len); id <= id__end; id++)
            {
                object book_ref = inventory[id];
                datum_book_info info = inventory[book_ref] as datum_book_info;
                Dm.append_at(data, "inventory", Dm.list(Dm.list(
                    Dm.kv("id", id),
                    Dm.kv("ref", book_ref),
                    Dm.kv("title", info.get_title()),
                    Dm.kv("author", info.get_author()))));
            }
        }
        data["has_inventory"] = Dm.num(Dm.truthy(inventory_len));
        data["inventory_page"] = inventory_page + 1;
        data["inventory_page_count"] = inventory_page_count + 1;
        return data;
    }

    // tg: code/modules/library/lib_machines.dm:343
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["can_db_request"] = can_db_request();
        data["screen_state"] = screen_state;
        data["show_dropdown"] = show_dropdown;
        data["display_lore"] = Dm.num(Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(can_spawn_lore));
        if (Dm.truthy(dynamic_inv_load)) //Time to load those area books in
        {
            dynamic_inv_load = FALSE;
            load_nearby_books();
        }

        switch (screen_state)
        {
            case LIBRARY_CHECKOUT:
            {
                data["checkouts"] = Dm.list();
                float checkout_len = length(checkouts);
                if (Dm.truthy(checkout_len))
                {
                    for (float id = (CHECKOUTS_PER_PAGE * checkout_page) + 1, id__end = min(CHECKOUTS_PER_PAGE * (checkout_page + 1), checkout_len); id <= id__end; id++)
                    {
                        object checkout_ref = checkouts[id];
                        datum_borrowbook loan = checkouts[checkout_ref] as datum_borrowbook;
                        float timedue = (Dm.num(loan.duedate) - world.time) / (1 * MINUTES);
                        timedue = max(round(timedue, 0.1f), 0);
                        Dm.append_at(data, "checkouts", Dm.list(Dm.list(
                            Dm.kv("id", id),
                            Dm.kv("ref", checkout_ref),
                            Dm.kv("borrower", loan.loanedto is var or__1 && Dm.truthy(or__1) ? or__1 : "N/A"),
                            Dm.kv("overdue", Dm.num(timedue <= 0)),
                            Dm.kv("due_in_minutes", timedue),
                            Dm.kv("title", loan.book_data.get_title()),
                            Dm.kv("author", loan.book_data.get_author()))));
                    }
                }
                data["has_checkout"] = Dm.num(Dm.truthy(checkout_len));
                data["checkout_page"] = checkout_page + 1;
                data["checkout_page_count"] = checkout_page_count + 1;
                data["checkout_title"] = checking_out_book?.get_title() is var or__2 && Dm.truthy(or__2) ? or__2 : null;
                break;
            }

            //Copypasta from the visitor console
            case LIBRARY_ARCHIVE:
                Dm.combine(ref data, await base.ui_data(user)); //I am so sorry
                break;

            case LIBRARY_UPLOAD:
            {
                data["upload_categories"] = SSlibrary.upload_categories;
                data["default_category"] = DEFAULT_UPLOAD_CATAGORY;
                obj_machinery_libraryscanner scan = get_scanner() as obj_machinery_libraryscanner;
                data["has_scanner"] = Dm.num(Dm.truthy(scan));
                data["has_cache"] = Dm.num(Dm.truthy(scan?.cache));

                // COOLDOWN_TIMELEFT(src, newscaster_cooldown) (__DEFINES/cooldowns.dm:115), inline (rules 11.1 (a))
                data["cooldown_string"] = Dm.text("[]", DisplayTimeText(max(0, newscaster_cooldown - world.time)));
                data["active_newscaster_cooldown"] = Dm.num(newscaster_cooldown <= world.time); // COOLDOWN_FINISHED(src, newscaster_cooldown)

                if (Dm.truthy(scan?.cache))
                {
                    data["cache_title"] = scan.cache.get_title();
                    data["cache_author"] = scan.cache.get_author();
                    data["cache_content"] = scan.cache.get_content();
                }
                break;
            }

            case LIBRARY_PRINT:
            {
                data["deity"] = GLOB.deity is var or__3 && Dm.truthy(or__3) ? or__3 : DEFAULT_DEITY;
                object reigion = GLOB.religion is var or__4 && Dm.truthy(or__4) ? or__4 : DEFAULT_RELIGION;
                data["religion"] = reigion;
                object bible_name = GLOB.bible_name;
                if (!Dm.truthy(bible_name))
                    bible_name = Dm.text("The Holy Book of []", reigion); // DEFAULT_BIBLE_REPLACE(reigion) (__DEFINES/jobs.dm:23)
                data["bible_name"] = bible_name;
                data["bible_sprite"] = Dm.text("display-[]", GLOB.bible_icon_state is var or__5 && Dm.truthy(or__5) ? or__5 : "bible");
                data["posters"] = Dm.list();
                foreach (object poster_name in Dm.each(SSlibrary.printable_posters))
                    Dm.append_at(data, "posters", poster_name);
                break;
            }
        }

        return data;
    }

    // tg: code/modules/library/lib_machines.dm:411
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/bibles)): the asset transport is the client's (DV44); the
        // LibraryConsole window draws the bible's picture from its own copy of tg's .dmi file, scaled up as the sheet is.
        return Dm.list();
    }

    // tg: code/modules/library/lib_machines.dm:414
    public virtual void load_nearby_books()
    {
        foreach (datum_book_info book in Dm.each_cast<datum_book_info>(SSlibrary.get_area_books(get_area(this) as area)))
            inventory[@ref(book)] = book;
        inventory_update();
    }

    // tg: code/modules/library/lib_machines.dm:419
    public virtual object get_scanner(object viewrange = null)
    {
        if (scanner != null)
        {
            obj_machinery_libraryscanner potential_scanner = scanner.resolve() as obj_machinery_libraryscanner;
            if (potential_scanner != null)
                return potential_scanner;
            scanner = null;
        }

        foreach (obj_machinery_libraryscanner foundya in Dm.each<obj_machinery_libraryscanner>(range(viewrange, get_turf(this))))
        {
            scanner = WEAKREF(foundya) as datum_weakref;
            return foundya;
        }
        return null;
    }

    // tg: code/modules/library/lib_machines.dm:430
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        //The parent call takes care of stuff like searching, don't forget about that yeah?
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "set_screen":
            {
                object window = @params["screen_index"];
                set_screen_state(window);
                return TRUE;
            }
            case "toggle_dropdown":
                show_dropdown = Dm.num(!Dm.truthy(show_dropdown));
                return TRUE;
            case "inventory_remove":
            {
                object id = @params["book_id"];
                Dm.remove(ref inventory, id);
                inventory_update();
                await update_static_data_for_all_viewers();
                return TRUE;
            }
            case "switch_inventory_page":
                inventory_page = Dm.num(sanitize_page_input(@params["page"], inventory_page, inventory_page_count));
                inventory_update();
                await update_static_data_for_all_viewers();
                return TRUE;
            case "checkout":
            {
                if (checking_out_book == null)
                    return TRUE;
                datum_borrowbook loan = Dm.@new<datum_borrowbook>();
                string loan_to = copytext(sanitize(@params["loaned_to"]), 1, MAX_NAME_LEN);
                object checkoutperiod = max(@params["checkout_time"], 1);
                loan.book_data = checking_out_book.return_copy() as datum_book_info;
                loan.loanedto = loan_to;
                loan.checkout = world.time;
                loan.duedate = world.time + Dm.num(checkoutperiod) * MINUTES;
                checkouts[@ref(loan)] = loan;
                checkout_update();
                return TRUE;
            }
            case "set_checkout":
            {
                DmList available = Dm.list();
                foreach (object id in Dm.each(inventory))
                {
                    datum_book_info book_infos = inventory[id] as datum_book_info;
                    available[book_infos.title] = book_infos;
                }
                object book_name = @params["book_name"];
                if (QDELETED(this) || !Dm.truthy(book_name))
                    return dot;
                datum_book_info book_info = available[book_name] as datum_book_info;
                if (!(book_info is datum_book_info))
                    return dot;
                checking_out_book = book_info;
                return TRUE;
            }
            case "checkin":
            {
                object id = @params["checked_out_id"];
                Dm.remove(ref checkouts, id);
                checkout_update();
                return TRUE;
            }
            case "switch_checkout_page":
                checkout_page = Dm.num(sanitize_page_input(@params["page"], checkout_page, checkout_page_count));
                checkout_update();
                return TRUE;
            case "set_cache_title":
            {
                obj_machinery_libraryscanner scan = get_scanner() as obj_machinery_libraryscanner;
                if (Dm.truthy(scan?.cache) && Dm.truthy(@params["title"]))
                    scan.cache.set_title(@params["title"]);
                return TRUE;
            }
            case "set_cache_author":
            {
                obj_machinery_libraryscanner scan = get_scanner() as obj_machinery_libraryscanner;
                if (Dm.truthy(scan?.cache) && Dm.truthy(@params["author"]))
                    scan.cache.set_author(@params["author"]);
                return TRUE;
            }
            case "upload":
            {
                if (!Dm.truthy(prevent_db_spam()))
                {
                    await say(I18n.T("Database cables refreshing. Please wait a moment."));
                    return dot;
                }
                object upload_category = @params["category"];
                if (!Dm.@in(upload_category, SSlibrary.upload_categories)) //Nice try
                    upload_category = DEFAULT_UPLOAD_CATAGORY;

                Waiting.Detach(upload_from_scanner(upload_category)); // INVOKE_ASYNC(src, PROC_REF(upload_from_scanner), upload_category)
                return TRUE;
            }
            case "news_post":
            {
                // We grey out the button UI-side, but let's just be safe to guard against spammy spammers.
                if (!(newscaster_cooldown <= world.time)) // COOLDOWN_FINISHED(src, newscaster_cooldown)
                {
                    await say(I18n.T("Not enough time has passed since the last news post. Please wait."));
                    return dot;
                }
                if (GLOB.news_network == null)
                    await say(I18n.T("No news network found on station. Aborting."));
                datum_feed_channel library_channel = GLOB.news_network.network_channels_by_name[LIBRARY_NEWSFEED] as datum_feed_channel;
                if (library_channel == null)
                    GLOB.news_network.create_feed_channel(LIBRARY_NEWSFEED, "Library", I18n.T("The official station book club!"), null);

                obj_machinery_libraryscanner scan = get_scanner() as obj_machinery_libraryscanner;
                if (scan == null)
                {
                    await say(I18n.T("No nearby scanner detected. Aborting."));
                    return dot;
                }
                GLOB.news_network.submit_article(scan.cache.content, Dm.text("[]: []", scan.cache.author, scan.cache.title), LIBRARY_NEWSFEED, null);
                await say(I18n.T("Upload complete. Your uploaded title is now available on station newscasters."));
                newscaster_cooldown = world.time + NEWSCASTER_COOLDOWN; // COOLDOWN_START(src, newscaster_cooldown, NEWSCASTER_COOLDOWN)
                return TRUE;
            }
            case "print_book":
            {
                object id = @params["book_id"];
                await attempt_print(Dm.@new<datum_callback>(this, nameof(print_book), id));
                return TRUE;
            }
            case "print_bible":
                await attempt_print(Dm.@new<datum_callback>(this, nameof(print_bible)));
                return TRUE;
            case "print_poster":
            {
                object poster_name = @params["poster_name"];
                await attempt_print(Dm.@new<datum_callback>(this, nameof(print_poster), poster_name));
                return TRUE;
            }
            case "lore_spawn":
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(can_spawn_lore))
                    print_forbidden_lore(usr);
                set_screen_state(MIN_LIBRARY);
                return TRUE;
            case "lore_deny":
                if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(can_spawn_lore))
                    shun_the_corp(usr);
                set_screen_state(MIN_LIBRARY);
                return TRUE;
        }
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:550
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_barcodescanner)))
            return NONE;

        obj_item_barcodescanner scanner = (obj_item_barcodescanner)tool;
        if (scanner.computer_ref?.resolve() == this)
        {
            balloon_alert(user, I18n.T("already connected!"));
            return ITEM_INTERACT_BLOCKING;
        }

        scanner.computer_ref = WEAKREF(this) as datum_weakref;
        balloon_alert(user, I18n.T("scanner connected"));
        audible_message(span_hear(I18n.T("[] lets out a low, short blip.", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/library/lib_machines.dm:564
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (!Dm.truthy(density) || Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        balloon_alert(user, I18n.T("forbidden knowledge unlocked"));
        return TRUE;
    }

    // tg: code/modules/library/lib_machines.dm:571
    public virtual void set_screen_state(object new_state = null)
    {
        screen_state = Dm.num(clamp(new_state, MIN_LIBRARY, MAX_LIBRARY));
    }

    // tg: code/modules/library/lib_machines.dm:574
    public virtual void inventory_update()
    {
        inventory_page_count = round(max(length(inventory) - 1, 0) / INVENTORY_PER_PAGE); //This is just floor()
        inventory_page = clamp(inventory_page, 0, inventory_page_count);
    }

    // tg: code/modules/library/lib_machines.dm:578
    public virtual void checkout_update()
    {
        checkout_page_count = round(max(length(checkouts) - 1, 0) / CHECKOUTS_PER_PAGE); //This is just floor()
        checkout_page = clamp(checkout_page, 0, checkout_page_count);
    }

    // tg: code/modules/library/lib_machines.dm:582
    public virtual void print_forbidden_lore(mob user = null)
    {
        can_spawn_lore = FALSE;
        Dm.@new<obj_item_melee_cultblade_dagger>(get_turf(this));
        to_chat(user, span_warning(I18n.T("Your sanity barely endures the seconds spent in the vault's browsing window. The only thing to remind you of this when you stop browsing is a sinister dagger sitting on the desk. You don't even remember where it came from...")));
        user.visible_message(span_warning(I18n.T("[] stares at the blank screen for a few moments, [] expression frozen in fear. When [] finally awaken[] from it, [] look[] a lot older.", user, user.p_their(), user.p_they(), user.p_s(), user.p_they(), user.p_s())), vision_distance: 2);
        if (ishuman(user))
        {
            mob_living_carbon_human fool = (mob_living_carbon_human)user;
            fool.age = clamp(Dm.num(fool.age) + 10, AGE_MIN, AGE_MAX); //Fuck you
        }
    }

    // tg: code/modules/library/lib_machines.dm:591
    public virtual void shun_the_corp(mob user = null)
    {
        can_spawn_lore = FALSE;
        to_chat(user, span_warning(I18n.T("You click off the page in a rush, and the machine hums back to normal, the tab gone...")));
    }

    // tg: code/modules/library/lib_machines.dm:595
    public virtual async DmTask upload_from_scanner(object upload_category = null)
    {
        obj_machinery_libraryscanner scan = get_scanner() as obj_machinery_libraryscanner;
        if (scan == null)
        {
            await say(I18n.T("No nearby scanner detected."));
            return;
        }
        if (scan.cache == null)
        {
            await say(I18n.T("No cached book found. Aborting upload."));
            return;
        }
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            await say(I18n.T("Connection to Archive has been severed. Aborting."));
            return;
        }
        datum_book_info book = scan.cache;
        if (!Dm.truthy(book.title))
        {
            await say(I18n.T("No title detected. Aborting"));
            return;
        }
        if (!Dm.truthy(book.author))
        {
            await say(I18n.T("No author detected. Aborting"));
            return;
        }
        if (!Dm.truthy(book.content))
        {
            await say(I18n.T("No content detected. Aborting"));
            return;
        }
        // tg: :616-629  the upload's INSERT into the library table (SSdbcore.NewQuery, Execute), its log, library_updated() and
        // update_db_info(): SQL is not translated (rules 13), and nothing reaches it without a connection
        Dm.NotTranslated("code/modules/library/lib_machines.dm:617", "upload_from_scanner: the library table's INSERT (SQL)");
    }

    /// Call this proc to attempt a print. It will return false if the print failed, true otherwise, longside some ux
    /// Accepts a callback to call when the print "finishes"
    // tg: code/modules/library/lib_machines.dm:633
    public virtual async DmTask<object> attempt_print(datum_callback call_after = null)
    {
        if (!(printer_cooldown <= world.time)) // COOLDOWN_FINISHED(src, printer_cooldown)
        {
            await say(I18n.T("Printer currently unavailable, please wait a moment."));
            return FALSE;
        }
        printer_cooldown = world.time + PRINTER_COOLDOWN; // COOLDOWN_START(src, printer_cooldown, PRINTER_COOLDOWN)
        playsound(this, Dm.resource("sound/machines/printer.ogg"), 50);
        _addtimer(call_after, 4.1f * SECONDS, file: "code/modules/library/lib_machines.dm", line: 639);
        return TRUE;
    }

    // tg: code/modules/library/lib_machines.dm:642
    public virtual void print_bible()
    {
        obj_item_book_bible holy_book = Dm.@new<obj_item_book_bible>(loc);
        if (!Dm.truthy(GLOB.bible_icon_state) || !Dm.truthy(GLOB.bible_inhand_icon_state))
            return;
        holy_book.icon_state = (string)GLOB.bible_icon_state;
        holy_book.inhand_icon_state = GLOB.bible_inhand_icon_state;
        holy_book.name = (string)GLOB.bible_name;
        holy_book.deity_name = (string)GLOB.deity;
    }

    // tg: code/modules/library/lib_machines.dm:651
    public virtual async DmTask print_poster(object poster_name = null)
    {
        Type poster_type = SSlibrary.printable_posters[poster_name] as Type;
        if (poster_type == null)
            return;
        Dm.@new<obj_item_poster>(loc, Dm.@new(poster_type));
        await DmTask.CompletedTask;
    }

    // tg: code/modules/library/lib_machines.dm:657
    public virtual async DmTask print_book(object id = null)
    {
        if (!Dm.truthy(SSdbcore.Connect()))
        {
            await say(I18n.T("Connection to Archive has been severed. Aborting."));
            can_connect = FALSE;
            return;
        }

        // tg: :663-688  the book's query (SSdbcore.NewQuery, Execute) and the printed book named "Book: <title>" with its author,
        // content and a random cover: SQL is not translated (rules 13), and nothing reaches it without a connection
        Dm.NotTranslated("code/modules/library/lib_machines.dm:663", "print_book: the library table's book query (SQL)");
    }
}

/*
 * Library Scanner
 */
// tg: code/modules/library/lib_machines.dm:693
public partial class obj_machinery_libraryscanner : obj_machinery
{
    // tg: code/modules/library/lib_machines.dm:705
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/library/lib_machines.dm:708
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/library/lib_machines.dm:711
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]2", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:715
    public override object Destroy(float? force = null)
    {
        held_book = null;
        cache = null;
        return base.Destroy(force);
    }

    // tg: code/modules/library/lib_machines.dm:720
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_book)))
            return NONE;

        await user.transferItemToLoc(tool, this);
        if (held_book != null)
            await user.put_in_hands(held_book);
        held_book = (obj_item_book)tool;
        playsound(this, Dm.resource("sound/machines/eject.ogg"), 70);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/library/lib_machines.dm:731
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == held_book)
            held_book = null;
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:736
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "LibraryScanner");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/library/lib_machines.dm:742
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        DmList cached_info = Dm.list();
        data["has_book"] = Dm.num(held_book != null);
        data["has_cache"] = Dm.num(cache != null);
        if (cache != null)
        {
            cached_info["title"] = cache.get_title();
            cached_info["author"] = cache.get_author();
        }
        data["book"] = cached_info;

        return data;
    }

    // tg: code/modules/library/lib_machines.dm:754
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        switch (action)
        {
            case "scan":
                if (Dm.truthy(cache?.compare(held_book.book_data)))
                {
                    await say(I18n.T("This book is already in my internal cache"));
                    return dot;
                }
                cache = held_book.book_data.return_copy() as datum_book_info;
                flick("bigscanner1", this);
                playsound(this, Dm.resource("sound/machines/scanner/scanner.ogg"), vol: 50, vary: TRUE);
                return TRUE;
            case "clear":
                cache = null;
                return TRUE;
            case "eject":
                await ui.user.put_in_hands(held_book);
                playsound(this, Dm.resource("sound/machines/eject.ogg"), 70);
                return TRUE;
        }
        return dot;
    }
}

/*
 * Book binder
 */
// tg: code/modules/library/lib_machines.dm:778
public partial class obj_machinery_bookbinder : obj_machinery
{
    // tg: code/modules/library/lib_machines.dm:793
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/library/lib_machines.dm:796
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/library/lib_machines.dm:799
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]2", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/modules/library/lib_machines.dm:803
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_paper)))
        {
            await prebind_book(user, (obj_item_paper)tool);
            return ITEM_INTERACT_SUCCESS;
        }

        if (isidcard(tool))
        {
            obj_item_card_id idcard = (obj_item_card_id)tool;
            scanned_name = idcard.registered_name;
            balloon_alert(user, I18n.T("scanned"));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/library/lib_machines.dm:816
    public virtual async DmTask prebind_book(mob user = null, obj_item_paper draw_from = null)
    {
        if (Dm.truthy(machine_stat))
            return;

        if (Dm.truthy(busy))
        {
            to_chat(user, span_warning(I18n.T("The book binder is busy. Please wait for completion of previous operation.")));
            return;
        }

        if (!Dm.truthy(scanned_name))
        {
            scanned_name = I18n.T("unknown author");
            await say(I18n.T("No ID detected. Please scan your ID if you would like to be credited for this book. Otherwise please enter your paper again."));
            return;
        }

        if (!Dm.truthy(await user.transferItemToLoc(draw_from, this)))
            return;

        user.visible_message(span_notice(I18n.T("[] loads some paper into [].", user, this)), span_notice(I18n.T("You load some paper into [].", this)));
        audible_message(span_hear(I18n.T("[] begins to hum as it warms up its printing drums.", this)));
        busy = TRUE;
        playsound(this, Dm.resource("sound/machines/printer.ogg"), 50);
        flick("binder1", this);
        _addtimer(Dm.@new<datum_callback>(this, nameof(bind_book), draw_from), 4.1f * SECONDS, file: "code/modules/library/lib_machines.dm", line: 837);
    }

    // tg: code/modules/library/lib_machines.dm:839
    public virtual async DmTask bind_book(obj_item_paper draw_from = null)
    {
        busy = FALSE;
        if (draw_from == null) //What the fuck did you do
            return;

        if (Dm.truthy(machine_stat))
        {
            await draw_from.forceMove(drop_location() as atom);
            return;
        }

        visible_message(span_notice(I18n.T("[] whirs as it prints and binds a new book.", this)));
        obj_item_book bound_book = Dm.@new<obj_item_book>(loc);
        bound_book.book_data.set_content_using_paper(draw_from);
        bound_book.book_data.set_author(scanned_name, trusted: FALSE);
        bound_book.name = I18n.T("Print Job #[]", rand(100, 999));
        bound_book.gen_random_icon_state();
        scanned_name = null;

        qdel(draw_from);
    }
}
