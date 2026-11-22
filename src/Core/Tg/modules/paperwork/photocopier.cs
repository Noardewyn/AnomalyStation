// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/photocopier.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298; the toner cartridges came with engineering E14, their examine with X1). Left: the map export's
// get_save_vars (a live-partial row); the AI's photo printing came with silicons SI4 (D326). Copies after the first are paid
// (the payment component, gaps P4a). A copied photo has no image (the camera's picture, gaps J3).
// The var blocks are generated.

// tg: code/modules/paperwork/photocopier.dm:41  GLOBAL_LIST_INIT(paper_blanks, init_paper_blanks())
public partial class datum_controller_global_vars
{
    /// Paper blanks (form templates, basically). Loaded from `config/blanks.json`.
    /// If invalid or not found, set to null.
    // tg: code/modules/paperwork/photocopier.dm:43
    public DmList paper_blanks;

    // tg: code/modules/paperwork/photocopier.dm:43  (GLOBAL_MANAGED(paper_blanks, init_paper_blanks()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalpaper_blanks()
    {
        paper_blanks = init_paper_blanks() as DmList;
        Dm.append(ref gvars_datum_init_order, "paper_blanks");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/paperwork/photocopier.dm:45
    public static object init_paper_blanks()
    {
        const string BLANKS_FILE_NAME = "config/blanks.json"; // tg: photocopier.dm:2 (file-local #define)
        if (!Dm.truthy(Dm.fexists(BLANKS_FILE_NAME)))
            return null;
        DmList blanks_json = Dm.json_decode(Dm.file2text(BLANKS_FILE_NAME)) as DmList;
        if (!Dm.truthy(length(blanks_json)))
            return null;

        DmList parsed_blanks = Dm.list();
        foreach (DmList paper_blank in Dm.each<DmList>(blanks_json))
            Dm.append(parsed_blanks, Dm.list(Dm.kv(Dm.text("[]", paper_blank["code"]), paper_blank)));

        return parsed_blanks;
    }
}

// tg: code/modules/paperwork/photocopier.dm:61
public partial class obj_machinery_photocopier : obj_machinery
{
    // tg: photocopier.dm:4-38 (file-local #defines)
    private const string PHOTO_GREYSCALE = "Greyscale";
    private const string PHOTO_COLOR = "Color";
    private const float PAPER_TONER_USE = 0.125f;
    private const float PHOTO_TONER_USE = 0.625f;
    private const float DOCUMENT_TONER_USE = PAPER_TONER_USE * DOCUMENT_PAPER_USE;
    private const float ASS_TONER_USE = PHOTO_TONER_USE;
    private const float PAPERWORK_TONER_USE = PAPER_TONER_USE * PAPERWORK_PAPER_USE;
    private const float TONER_CHARGE_LOW_AMOUNT = 2;
    private const int PAPER_PAPER_USE = 1;
    private const int PHOTO_PAPER_USE = 1;
    private const int DOCUMENT_PAPER_USE = 20;
    private const int ASS_PAPER_USE = PHOTO_PAPER_USE;
    private const int PAPERWORK_PAPER_USE = 10;
    private const int MATTER_BIN_PAPER_CAPACITY = 30;
    private const int MAX_COPIES_AT_ONCE = 10;

    /// Typecache of objects that can be inserted and scanned into the photocopier for copying
    // tg: code/modules/paperwork/photocopier.dm:104  var/static/list/whitelist_scannable_objects (rules 10.6)
    public static ref DmList whitelist_scannable_objects => ref TgState.Current.obj_machinery_photocopier__whitelist_scannable_objects;
    /// List of paper types that can be inserted as blank paper
    // tg: code/modules/paperwork/photocopier.dm:111  var/static/list/valid_paper_types (rules 10.6)
    public static ref DmList valid_paper_types => ref TgState.Current.obj_machinery_photocopier__valid_paper_types;

    // tg: code/modules/paperwork/photocopier.dm:128
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(setup_components());
        Waiting.NoSleep(AddElement(typeof(datum_element_elevation), Dm.named("pixel_shift", 8))); //enough to look like your bums are on the machine.
        if (Dm.truthy(starting_paper))
            paper_stack[created_paper] = starting_paper;
        if (starting_toner != null)
            toner_cartridge = Dm.@new(starting_toner, this) as obj_item_toner;
        return dot;
    }

    /// Simply adds the necessary components for this to function.
    // tg: code/modules/paperwork/photocopier.dm:138
    public virtual async DmTask setup_components()
    {
        await AddComponent(typeof(datum_component_payment), usage_cost, null, PAYMENT_CLINICAL);
    }

    // tg: code/modules/paperwork/photocopier.dm:141
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        float capacity = 0;
        foreach (datum_stock_part_matter_bin matter_bin in Dm.each<datum_stock_part_matter_bin>(component_parts))
            capacity += MATTER_BIN_PAPER_CAPACITY * Dm.num(matter_bin.tier);
        max_paper_capacity = capacity;

        float efficiency = 1;
        foreach (datum_stock_part_micro_laser micro_laser in Dm.each<datum_stock_part_micro_laser>(component_parts))
            efficiency += Dm.num(micro_laser.tier);
        toner_efficiency = efficiency;

        float print_time = 5 * SECONDS;
        foreach (datum_stock_part_scanning_module scanning_module in Dm.each<datum_stock_part_scanning_module>(component_parts))
            print_time -= Dm.num(scanning_module.tier) * SECONDS;
        time_to_print = print_time;
        return dot;
    }

    // tg: code/modules/paperwork/photocopier.dm:155
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == object_copy)
            object_copy = null;
        if (gone == toner_cartridge)
            toner_cartridge = null;
        return dot;
    }

    // tg: code/modules/paperwork/photocopier.dm:162
    public override async DmTask dump_contents(params object[] __extra)
    {
        atom dump_location = drop_location() as atom;

        // object_copy can be a traitor objective, don't qdel
        if (object_copy != null)
            await object_copy.forceMove(dump_location);
        if (toner_cartridge != null)
            await toner_cartridge.forceMove(dump_location);

        foreach (Type paper_path in Dm.each<Type>(paper_stack?.Copy()))
        {
            float paper_amount = Dm.num(paper_stack[paper_path]);
            if (paper_amount <= 0)
            {
                stack_trace(Dm.text("Detected zero or negative [] amount inside photocopier. There should be at least 1 or more of paper amount inside the list", paper_path), "code/modules/paperwork/photocopier.dm", 173);
                continue;
            }

            for (int i = 1; i <= paper_amount; i++)
            {
                obj_item_paper new_paper = Dm.@new(paper_path, dump_location) as obj_item_paper;
                if (!Dm.truthy(new_paper.pixel_y))
                    new_paper.pixel_y = rand(-3, 3);
                if (!Dm.truthy(new_paper.pixel_x))
                    new_paper.pixel_x = rand(-3, 3);
            }

            paper_stack.Remove(paper_path);
        }

        update_appearance();
    }

    // tg: code/modules/paperwork/photocopier.dm:186
    public override object Destroy(float? force = null)
    {
        // object_copy can be a traitor objective, don't qdel
        if (object_copy != null)
            Waiting.NoSleep(object_copy.forceMove(drop_location() as atom));

        QDEL_NULL(ref toner_cartridge);
        paper_stack = null;
        ass = null; //the mob isn't actually contained and just referenced, no need to delete it.
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/photocopier.dm:195
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (Dm.truthy(disassembled))
            await dump_contents();
        return await base.on_deconstruction(disassembled);
    }

    // tg: code/modules/paperwork/photocopier.dm:200
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (object_copy != null)
            Dm.append(ref dot, span_notice(I18n.T("There is something inside the scanner tray.")));
        Dm.append(ref dot, span_notice(I18n.T("You can put any type of blank paper inside to print a form onto it or to copy something onto it.")));
        return dot;
    }

    // tg: code/modules/paperwork/photocopier.dm:206
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Photocopier");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/paperwork/photocopier.dm:212
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList static_data = Dm.list();

        DmList blank_infos = Dm.list();
        DmList category_names = Dm.list();
        if (Dm.truthy(GLOB.paper_blanks))
        {
            foreach (object blank_id in Dm.each(GLOB.paper_blanks))
            {
                DmList paper_blank = GLOB.paper_blanks[blank_id] as DmList;
                Dm.append(blank_infos, Dm.list(Dm.list(
                    Dm.kv("name", paper_blank["name"]),
                    Dm.kv("category", paper_blank["category"]),
                    Dm.kv("code", blank_id))));
                Dm.combine(category_names, paper_blank["category"]);
            }
        }

        static_data["blanks"] = blank_infos;
        static_data["categories"] = category_names;
        static_data["max_paper_count"] = max_paper_capacity;
        static_data["max_copies"] = MAX_COPIES_AT_ONCE;

        return static_data;
    }

    // tg: code/modules/paperwork/photocopier.dm:234
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["has_item"] = Dm.num(!Dm.truthy(copier_empty()));
        data["num_copies"] = num_copies;
        data["copies_left"] = copies_left;

        if (istype(object_copy, typeof(obj_item_photo)))
        {
            data["is_photo"] = TRUE;
            data["color_mode"] = color_mode;
        }

        if (HAS_AI_ACCESS(user))
        {
            data["isAI"] = TRUE;
            data["can_AI_print"] = Dm.num(toner_cartridge != null && (toner_cartridge.charges >= PHOTO_TONER_USE) && (Dm.num(get_paper_count(created_paper)) >= PHOTO_PAPER_USE));
        }
        else
            data["isAI"] = FALSE;

        if (toner_cartridge != null)
        {
            data["has_toner"] = TRUE;
            data["current_toner"] = toner_cartridge.charges;
            data["max_toner"] = toner_cartridge.max_charges;
        }
        else
            data["has_toner"] = FALSE;

        data["created_paper"] = created_paper;
        data["paper_stack"] = paper_stack;
        data["paper_count"] = get_paper_count();

        DmList paper_types = Dm.list();
        foreach (Type paper in Dm.each<Type>(valid_paper_types))
        {
            Dm.append(paper_types, Dm.list(Dm.list(
                Dm.kv("name", Dm.initial(paper, (obj_item_paper p) => p.name)),
                Dm.kv("icon", Dm.initial(paper, (obj_item_paper p) => p.icon)),
                Dm.kv("icon_state", Dm.initial(paper, (obj_item_paper p) => p.icon_state)),
                Dm.kv("amount", paper_stack[paper] is var amount && Dm.truthy(amount) ? amount : 0),
                Dm.kv("type", paper))));
        }
        data["paper_types"] = paper_types;

        return data;
    }

    // tg: code/modules/paperwork/photocopier.dm:271
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            return dot;

        switch (action)
        {
            // Copying paper, photos, documents and asses.
            case "make_copy":
                if (Dm.truthy(check_busy(usr)))
                    return FALSE;
                // ASS COPY. By Miauw
                if (ass != null)
                {
                    if (ishuman(ass) && (Dm.truthy(ass.get_item_by_slot(ITEM_SLOT_ICLOTHING)) || Dm.truthy(ass.get_item_by_slot(ITEM_SLOT_OCLOTHING))))
                    {
                        if (ass == usr)
                            to_chat(usr, span_notice(I18n.T("You feel kind of silly, copying your ass with your clothes on.")));
                        else
                            to_chat(usr, span_notice(I18n.T("You feel kind of silly, copying []'s ass with [] clothes on.", ass, ass.p_their())));
                        return FALSE;
                    }
                    do_copies(Dm.@new<datum_callback>(this, nameof(make_ass_copy)), usr, ASS_PAPER_USE, ASS_TONER_USE, num_copies);
                    return TRUE;
                }
                else
                {
                    // Basic paper
                    if (istype(object_copy, typeof(obj_item_paper)))
                    {
                        do_copies(Dm.@new<datum_callback>(this, nameof(make_paper_copy), object_copy), usr, PAPER_PAPER_USE, PAPER_TONER_USE, num_copies);
                        return TRUE;
                    }
                    // Copying photo.
                    if (istype(object_copy, typeof(obj_item_photo)))
                    {
                        obj_item_photo photo_copy = (obj_item_photo)object_copy;
                        do_copies(Dm.@new<datum_callback>(this, nameof(make_photo_copy), photo_copy.picture, color_mode), usr, PHOTO_PAPER_USE, PHOTO_TONER_USE, num_copies);
                        return TRUE;
                    }
                    // Copying Documents.
                    if (istype(object_copy, typeof(obj_item_documents)))
                    {
                        do_copies(Dm.@new<datum_callback>(this, nameof(make_document_copy), object_copy), usr, DOCUMENT_PAPER_USE, DOCUMENT_TONER_USE, num_copies);
                        return TRUE;
                    }
                    // Copying paperwork
                    if (istype(object_copy, typeof(obj_item_paperwork)))
                    {
                        do_copies(Dm.@new<datum_callback>(this, nameof(make_paperwork_copy), object_copy), usr, PAPERWORK_PAPER_USE, PAPERWORK_TONER_USE, num_copies);
                        return TRUE;
                    }
                }
                break;

            // Remove the paper/photo/document from the photocopier.
            case "remove":
                if (object_copy != null)
                {
                    await remove_photocopy(usr, object_copy as obj_item);
                    object_copy = null;
                }
                else if (Dm.truthy(check_ass()))
                    to_chat(ass, span_notice(I18n.T("You feel a slight pressure on your ass.")));
                return TRUE;

            // AI printing photos from their saved images.
            case "ai_photo":
                if (Dm.truthy(check_busy(usr)))
                    return FALSE;
                var tempAI = (mob_living_silicon_ai)usr;
                if (!Dm.truthy(Dm.length(tempAI.aicamera.stored)))
                {
                    balloon_alert(usr, "no images saved!");
                    return FALSE;
                }
                var selection = await tempAI.aicamera.selectpicture(usr) as datum_picture;
                do_copies(Dm.@new<datum_callback>(this, nameof(make_photo_copy), selection, PHOTO_COLOR), usr, PHOTO_PAPER_USE, PHOTO_TONER_USE, 1);
                return TRUE;

            // Switch between greyscale and color photos
            case "color_mode":
                if (Dm.@in(@params["mode"], Dm.list(PHOTO_GREYSCALE, PHOTO_COLOR)))
                    color_mode = @params["mode"] as string;
                return TRUE;

            // Remove the toner cartridge from the copier.
            case "remove_toner":
            {
                if (Dm.truthy(check_busy(usr)))
                    return FALSE;
                object success = await usr.put_in_hands(toner_cartridge);
                if (!Dm.truthy(success))
                    await toner_cartridge.forceMove(drop_location() as atom);

                toner_cartridge = null;
                return TRUE;
            }

            // Set the number of copies to be printed with 1 click of the "copy" button.
            case "set_copies":
                num_copies = clamp(Dm.num(text2num(@params["num_copies"])), 1, MAX_COPIES_AT_ONCE);
                return TRUE;
            // Called when you press print blank
            case "print_blank":
            {
                if (Dm.truthy(check_busy(usr)))
                    return FALSE;
                if (!Dm.@in(@params["code"], GLOB.paper_blanks))
                    return FALSE;
                DmList blank = GLOB.paper_blanks[@params["code"]] as DmList;
                do_copies(Dm.@new<datum_callback>(this, nameof(make_blank_print), blank), usr, PAPER_PAPER_USE, PAPER_TONER_USE, num_copies);
                return TRUE;
            }
            case "select_paper_type":
            {
                if (Dm.truthy(check_busy(usr)))
                    return FALSE;

                Type paper_path = text2path(Dm.str(@params["created_paper"]));

                if (!ispath(paper_path, typeof(obj_item_paper)))
                    return FALSE;

                if (!Dm.truthy(paper_stack[paper_path]))
                    return FALSE;

                created_paper = paper_path;
                return TRUE;
            }
        }
        return dot;
    }

    /// Returns the color used for the printing operation. If the color is below TONER_LOW_PERCENTAGE, it returns a gray color.
    // tg: code/modules/paperwork/photocopier.dm:374
    public virtual object get_toner_color()
    {
        return toner_cartridge.charges > TONER_CHARGE_LOW_AMOUNT ? COLOR_FULL_TONER_BLACK : COLOR_GRAY;
    }

    /// Will invoke `do_copy_loop` asynchronously. Passes the supplied arguments on to it.
    // tg: code/modules/paperwork/photocopier.dm:379
    public virtual void do_copies(datum_callback copy_cb = null, mob user = null, object paper_use = null, object toner_use = null, object copies_amount = null)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
            return;

        busy = TRUE;
        update_use_power(ACTIVE_POWER_USE);
        // fucking god proc
        Waiting.Detach(do_copy_loop(user, copy_cb, paper_use, toner_use, copies_amount));
    }

    // tg: code/modules/paperwork/photocopier.dm:388
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        obj_flags = Dm.bitor(obj_flags, EMAGGED);

        playsound(this, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        visible_message(span_warning(I18n.T("Sparks fly out of []!", this)));
        balloon_alert(user, I18n.T("payment system shorted"));
        return TRUE;
    }

    /**
     * Will invoke the passed in `copy_cb` callback in 4 second intervals, and charge the user 5 credits for each copy made.
     *
     * Arguments:
     * * user - the mob who clicked copy.
     * * copy_cb - a callback for which proc to call. Should only be one of the `make_x_copy()` procs, such as `make_paper_copy()`.
     * * paper_use - the amount of paper used in this operation
     * * toner_use - the amount of toner used in this operation
     * * copies_amount - the amount of copies we should make
     */
    // tg: code/modules/paperwork/photocopier.dm:409
    public virtual async DmTask do_copy_loop(mob user = null, datum_callback copy_cb = null, object paper_use = null, object toner_use = null, object copies_amount = null)
    {
        object error_message = null;
        float amount = Dm.num(copies_amount);
        float toner_per_copy = Dm.num(toner_use) / Dm.num(toner_efficiency);
        if (toner_cartridge == null)
        {
            amount = 0;
            error_message = span_warning(I18n.T("An error message flashes across \\the []'s screen: \"No toner cartridge found. Aborting.\"", this));
        }
        else if (toner_cartridge.charges < toner_per_copy * amount)
        {
            amount = FLOOR(toner_cartridge.charges / toner_per_copy, 1);
            error_message = span_warning(I18n.T("An error message flashes across \\the []'s screen: \"Not enough toner to perform []operation.\"", this, amount >= 1 ? "full " : ""));
        }
        if (Dm.num(get_paper_count(created_paper)) < Dm.num(paper_use) * amount)
        {
            amount = FLOOR(Dm.num(get_paper_count(created_paper)) / Dm.num(paper_use), 1);
            error_message = span_warning(I18n.T("An error message flashes across \\the []'s screen: \"Not enough paper to perform []operation.\"", this, amount >= 1 ? "full " : ""));
        }

        copies_left = amount;

        if (amount <= 0)
        {
            to_chat(user, error_message);
            reset_busy();
            return;
        }

        if (error_message != null)
            to_chat(user, error_message);

        // if you managed to cancel the copy operation, tough luck. you aren't getting your money back.
        DmList copies_made = Dm.list();
        for (int i = 1; i <= amount; i++)
        {
            if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(BROKEN, NOPOWER))))
                break;

            if (toner_cartridge == null)
                break;

            // arguments to copy_cb have been set at callback instantiation
            atom_movable copied_obj = await copy_cb.Invoke() as atom_movable;
            if (copied_obj == null) // something went wrong, so other copies will go wrong too
                break;

            playsound(this, Dm.resource("sound/machines/printer.ogg"), 50, vary: FALSE);
            await sleep(Dm.num(time_to_print));

            // reveal our copied item
            if (!QDELETED(copied_obj))
            {
                await copied_obj.forceMove(drop_location() as atom);
                give_pixel_offset(copied_obj as obj_item);
                Dm.append(copies_made, copied_obj);
            }
            copies_left--;
        }

        if (Dm.truthy(length(copies_made)))
        {
            if (!Dm.truthy(Dm.bitand(obj_flags, EMAGGED)) && Dm.truthy(Dm.bitand(attempt_charge(this, user, (length(copies_made) - 1) * usage_cost), COMPONENT_OBJ_CANCEL_CHARGE)))
            {
                visible_message(
                    span_warning(I18n.T("An error message flashes across \\the []'s screen.", this)),
                    span_warning(I18n.T("Failed to charge bank account. Scrapping copies.")));
                QDEL_LIST(copies_made);
            }
        }
        else
            to_chat(user, span_warning(I18n.T("Failed to copy object!")));

        copies_left = 0;
        reset_busy();
    }

    /// Sets busy to `FALSE`.
    // tg: code/modules/paperwork/photocopier.dm:464
    public virtual void reset_busy()
    {
        update_use_power(IDLE_POWER_USE);
        busy = FALSE;
    }

    /// Determines if the printer is currently busy, informs the user if it is.
    // tg: code/modules/paperwork/photocopier.dm:469
    public virtual object check_busy(mob user = null)
    {
        if (Dm.truthy(busy))
        {
            balloon_alert(user, I18n.T("printer is busy!"));
            return TRUE;
        }
        return FALSE;
    }

    /**
     * Gives items a random x and y pixel offset, between -10 and 10 for each.
     *
     * This is done that when someone prints multiple papers, we dont have them all appear to be stacked in the same exact location.
     *
     * Arguments:
     * * copied_item - The paper, document, or photo that was just spawned on top of the printer.
     */
    // tg: code/modules/paperwork/photocopier.dm:483
    public virtual void give_pixel_offset(obj_item copied_item = null)
    {
        copied_item.pixel_x = copied_item.base_pixel_x + rand(-10, 10);
        copied_item.pixel_y = copied_item.base_pixel_y + rand(-10, 10);
    }

    /**
     * Gets the total amount of paper this printer has stored
     *
     * Returns the amount of paper stored in the photocopier if passed with no args
     * If paper_type is supplied will only return the amount of that paper type
     *
     * Arguments:
     * * paper_type - The paper type to check to see quantity stored
     */
    // tg: code/modules/paperwork/photocopier.dm:496
    public virtual object get_paper_count(object paper_type = null)
    {
        if (paper_type != null)
            return paper_stack[paper_type] is var count && Dm.truthy(count) ? count : 0;

        float total_amount = 0;
        foreach (object paper_path in Dm.each(paper_stack))
        {
            float paper_amount = Dm.num(paper_stack[paper_path]);
            if (paper_amount <= 0)
            {
                stack_trace(Dm.text("Detected zero or negative [] amount inside photocopier. There should be at least 1 or more of paper amount inside the list", paper_path), "code/modules/paperwork/photocopier.dm", 504);
                continue;
            }

            total_amount += paper_amount;
        }

        return total_amount;
    }

    /**
     * Returns an empty paper, used for blanks and paper copies.
     * Prioritizes `paper_stack`, creates new paper in case `paper_stack` is empty.
     */
    // tg: code/modules/paperwork/photocopier.dm:515
    public virtual object get_empty_paper(object paper_type = null)
    {
        obj_item_paper new_paper = Dm.@new(paper_type as Type) as obj_item_paper;
        return new_paper;
    }

    /**
     * Removes an amount of paper from the printer's storage.
     * This lets us pretend we actually consumed paper when we were actually printing something that wasn't paper.
     */
    // tg: code/modules/paperwork/photocopier.dm:523
    public virtual void delete_paper(object number = null)
    {
        if (!Dm.truthy(paper_stack[created_paper]) || (Dm.num(number) > Dm.num(paper_stack[created_paper])))
            throw Dm.CRASH("Trying to delete more paper than is stored in the photocopier", "code/modules/paperwork/photocopier.dm", 525);

        paper_stack[created_paper] = Dm.num(paper_stack[created_paper]) - Dm.num(number);
        if (Dm.num(paper_stack[created_paper]) <= 0)
            paper_stack.Remove(created_paper);
    }

    /**
     * Handles the copying of paper. Transfers all the text, stamps and so on from the old paper, to the copy.
     *
     * Checks first if `paper_copy` exists. Since this proc is called from a timer, it's possible that it was removed.
     */
    // tg: code/modules/paperwork/photocopier.dm:536
    public virtual object make_paper_copy(obj_item_paper paper_copy = null)
    {
        if (paper_copy == null)
            return null;

        obj_item_paper empty_paper = get_empty_paper(created_paper) as obj_item_paper;
        delete_paper(PAPER_PAPER_USE);
        use_toner(PAPER_TONER_USE);

        object copy_colour = get_toner_color();

        obj_item_paper copied_paper = paper_copy.copy(empty_paper, this, FALSE, copy_colour) as obj_item_paper;
        copied_paper.name = paper_copy.name;
        return copied_paper;
    }

    /**
     * Handles the copying of photos, which can be printed in either color or greyscale.
     *
     * Checks first if `picture` exists. Since this proc is called from a timer, it's possible that it was removed.
     */
    // tg: code/modules/paperwork/photocopier.dm:555
    public virtual object make_photo_copy(datum_picture photo = null, object photo_color = null)
    {
        if (photo == null)
            return null;
        obj_item_photo copied_pic = Dm.@new<obj_item_photo>(this, photo.Copy(Dm.eq(photo_color, PHOTO_GREYSCALE) ? TRUE : FALSE));
        delete_paper(PHOTO_PAPER_USE);
        use_toner(PHOTO_TONER_USE);
        return copied_pic;
    }

    /**
     * Handles the copying of documents.
     *
     * Checks first if `document_copy` exists. Since this proc is called from a timer, it's possible that it was removed.
     */
    // tg: code/modules/paperwork/photocopier.dm:568
    public virtual object make_document_copy(obj_item_documents document_copy = null)
    {
        if (document_copy == null)
            return null;
        obj_item_documents_photocopy copied_doc = Dm.@new<obj_item_documents_photocopy>(this, document_copy);
        delete_paper(DOCUMENT_PAPER_USE);
        use_toner(DOCUMENT_TONER_USE);
        return copied_doc;
    }

    /**
     * Handles the copying of documents.
     *
     * Checks first if `paperwork_copy` exists. Since this proc is called from a timer, it's possible that it was removed.
     * Copies the stamp from a given piece of paperwork if it is already stamped, allowing for you to sell photocopied paperwork at the risk of losing budget money.
     */
    // tg: code/modules/paperwork/photocopier.dm:582
    public virtual object make_paperwork_copy(obj_item_paperwork paperwork_copy = null)
    {
        if (paperwork_copy == null)
            return null;
        obj_item_paperwork_photocopy copied_paperwork = Dm.@new<obj_item_paperwork_photocopy>(this, paperwork_copy);
        copied_paperwork.copy_stamp_info(paperwork_copy);
        if (Dm.truthy(paperwork_copy.stamped))
        {
            copied_paperwork.stamp_icon = "paper_stamp-pc"; //Override with the photocopy overlay sprite
            copied_paperwork.add_stamp();
        }
        delete_paper(PAPERWORK_PAPER_USE);
        use_toner(PAPERWORK_TONER_USE);
        return copied_paperwork;
    }

    /// Handles the copying of blanks. No mutating state, so this should not fail.
    // tg: code/modules/paperwork/photocopier.dm:596
    public virtual object make_blank_print(DmList blank = null)
    {
        object copy_colour = get_toner_color();
        obj_item_paper printblank = get_empty_paper(created_paper) as obj_item_paper;

        object printname = blank["name"];
        object printinfo = null;
        foreach (object infoline in Dm.each(blank["info"]))
            Dm.append(ref printinfo, infoline);

        printblank.name = I18n.T("paper - '[]'", printname);
        printblank.add_raw_text(printinfo, color: copy_colour);
        printblank.update_appearance();
        use_toner(PAPER_TONER_USE);
        return printblank;
    }

    /**
     * Handles the copying of an ass photo.
     *
     * Calls `check_ass()` first to make sure that `ass` exists, among other conditions. Since this proc is called from a timer, it's possible that it was removed.
     * Additionally checks that the mob has their clothes off.
     */
    // tg: code/modules/paperwork/photocopier.dm:617
    public virtual async DmTask<object> make_ass_copy()
    {
        if (!Dm.truthy(check_ass()))
            return null;
        object temp_img = ass.get_butt_sprite();
        if (temp_img == null)
            return null;
        obj_item_photo copied_ass = Dm.@new<obj_item_photo>(this);
        datum_picture toEmbed = Dm.@new<datum_picture>(I18n.T("[]'s Ass", ass), I18n.T("You see []'s ass on the photo.", ass), null, null, null, temp_img);
        toEmbed.psize_x = 128;
        toEmbed.psize_y = 128;
        await copied_ass.set_picture(toEmbed, TRUE, TRUE);
        delete_paper(ASS_PAPER_USE);
        use_toner(ASS_TONER_USE);
        return copied_ass;
    }

    /**
     * Called when someone hits the "remove item" button on the copier UI.
     *
     * If the user is a silicon, it drops the object at the location of the copier. If the user is not a silicon, it tries to put the object in their hands first.
     * Sets `busy` to `FALSE` because if the inserted item is removed, the copier should halt copying.
     *
     * Arguments:
     * * object - the item we're trying to remove.
     * * user - the user removing the item.
     */
    // tg: code/modules/paperwork/photocopier.dm:644
    public virtual async DmTask remove_photocopy(mob user = null, obj_item @object = null)
    {
        if (issilicon(user))
        {
            await @object.forceMove(drop_location() as atom);
            return;
        }

        await @object.forceMove(user.loc as atom);
        await user.put_in_hands(@object);

        to_chat(user, span_notice(I18n.T("You take [] out of []. []", @object, this, Dm.truthy(busy) ? I18n.T("The [] comes to a halt.", this) : "")));
    }

    // tg: code/modules/paperwork/photocopier.dm:654
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        icon_state = Dm.truthy(panel_open) ? Dm.text("[]2", base_icon_state) : (string)base_icon_state;
        return dot;
    }

    // tg: code/modules/paperwork/photocopier.dm:658
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/modules/paperwork/photocopier.dm:661
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/modules/paperwork/photocopier.dm:664
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        await default_unfasten_wrench(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/photocopier.dm:669
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        // No infinite paper chain. You need the original paperwork to make more copies.
        if (istype(tool, typeof(obj_item_paperwork_photocopy)))
        {
            balloon_alert(user, I18n.T("too blurry!"));
            to_chat(user, span_warning(I18n.T("The [] is far too messy to produce a good copy!", tool)));
            return ITEM_INTERACT_FAILURE;
        }

        if (istype(tool, typeof(obj_item_paper_paperslip)))
        {
            balloon_alert(user, I18n.T("too small!"));
            return ITEM_INTERACT_FAILURE;
        }

        if (istype(tool, typeof(obj_item_blueprints)))
        {
            balloon_alert(user, I18n.T("too large!"));
            to_chat(user, span_warning(I18n.T("\\The [] is too large to put into the copier. You need to find something else to record the document.", tool)));
            return ITEM_INTERACT_FAILURE;
        }

        if (istype(tool, typeof(obj_item_toner)))
        {
            if (toner_cartridge != null)
            {
                balloon_alert(user, I18n.T("another cartridge inside!"));
                return ITEM_INTERACT_FAILURE;
            }

            await tool.forceMove(this);
            toner_cartridge = (obj_item_toner)tool;
            balloon_alert(user, I18n.T("cartridge inserted"));
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_paperplane)))
        {
            balloon_alert(user, I18n.T("flatten paper first!"));
            return ITEM_INTERACT_FAILURE;
        }

        if (istype(tool, typeof(obj_item_paper)))
        {
            obj_item_paper paper = (obj_item_paper)tool;

            if (Dm.truthy(Dm.bitand(paper.resistance_flags, ON_FIRE)))
            {
                balloon_alert(user, I18n.T("paper on fire!"));
                return ITEM_INTERACT_FAILURE;
            }

            if (Dm.truthy(paper.is_empty())) // if not empty it gets inserted as an object to be copied
            {
                if (!Dm.truthy(has_room_for_paper()))
                {
                    balloon_alert(user, I18n.T("cannot hold more paper!"));
                    return ITEM_INTERACT_FAILURE;
                }

                insert_empty_paper(user, paper.GetType());
                qdel(paper);
                return ITEM_INTERACT_SUCCESS;
            }
        }

        if (istype(tool, typeof(obj_item_paper_bin)))
        {
            obj_item_paper_bin paper_bin = (obj_item_paper_bin)tool;

            if (!Dm.truthy(paper_bin.total_paper))
            {
                balloon_alert(user, I18n.T("paper bin empty!"));
                return ITEM_INTERACT_FAILURE;
            }

            float paper_inserted = 0;
            foreach (obj_item_paper stacked_paper in Dm.each<obj_item_paper>(paper_bin.paper_stack?.Copy())) // insert all paper that is already initialized
            {
                bool is_empty = Dm.truthy(stacked_paper.is_empty());
                bool is_room = Dm.truthy(has_room_for_paper());
                if (!is_empty || !is_room)
                    continue;

                insert_empty_paper(user, stacked_paper.GetType(), silent: TRUE);
                paper_bin.paper_stack.Remove(stacked_paper);
                paper_bin.total_paper -= 1;
                paper_inserted++;
                qdel(stacked_paper);
            }

            if (Dm.truthy(paper_bin.total_paper)) // then insert non-initialized paper that is always considered empty paper
            {
                float noninitialized_paper_total = paper_bin.total_paper - length(paper_bin.paper_stack);
                float paper_to_take = min(Dm.num(max_paper_capacity) - Dm.num(get_paper_count()), noninitialized_paper_total);
                if (paper_to_take != 0)
                {
                    insert_empty_paper(user, paper_bin.papertype, paper_to_take, silent: TRUE);
                    paper_inserted += paper_to_take;
                    paper_bin.total_paper -= paper_to_take;
                }
            }

            if (paper_inserted == 0 && !Dm.truthy(has_room_for_paper())) // no paper was inserted because it was full
            {
                balloon_alert(user, I18n.T("cannot hold more paper!"));
                return ITEM_INTERACT_FAILURE;
            }

            paper_bin.update_appearance();
            // we use silent for insert_empty_paper() so that we don't spam balloon_alerts and instead condense them into one alert here
            balloon_alert(user, I18n.T("[] paper inserted", paper_inserted));
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(is_type_in_typecache(tool, whitelist_scannable_objects)))
        {
            await insert_copy_object(user, tool);
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    /// Check if there is enough room to insert paper
    // tg: code/modules/paperwork/photocopier.dm:770
    public virtual object has_room_for_paper(mob user = null, float? amount = null)
    {
        return Dm.num(Dm.num(get_paper_count()) < Dm.num(max_paper_capacity));
    }

    /// Proc that handles insertion of empty paper, useful for copying later.
    // tg: code/modules/paperwork/photocopier.dm:774
    public virtual void insert_empty_paper(mob user = null, object paper_type = null, float? amount = null, float? silent = null)
    {
        amount ??= 1;
        silent ??= FALSE;
        if (!Dm.truthy(paper_stack[paper_type]))
            paper_stack[paper_type] = 0;
        paper_stack[paper_type] = Dm.num(paper_stack[paper_type]) + amount.Value;
        if (!Dm.truthy(silent))
            balloon_alert(user, I18n.T("paper inserted"));
    }

    // tg: code/modules/paperwork/photocopier.dm:781
    public virtual async DmTask insert_copy_object(mob user = null, obj_item @object = null)
    {
        if (!Dm.truthy(copier_empty()))
        {
            balloon_alert(user, I18n.T("scanner tray occupied!"));
            return;
        }
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(@object)))
            return;
        object_copy = @object;
        await @object.forceMove(this);
        balloon_alert(user, I18n.T("copy object inserted"));
        flick("photocopier1", this);
    }

    // tg: code/modules/paperwork/photocopier.dm:792
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot) && Dm.truthy(toner_cartridge?.charges))
        {
            Dm.@new<obj_effect_decal_cleanable_blood_oil>(get_turf(this));
            toner_cartridge.charges = 0;
        }
        return dot;
    }

    // tg: code/modules/paperwork/photocopier.dm:798
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        mob target = dropped as mob;
        if (target == null || Dm.truthy(target.anchored) || target.buckled != null || target == ass || Dm.truthy(copier_blocked()))
            return null;
        add_fingerprint(user);
        if (target == user)
            user.visible_message(span_notice(I18n.T("[] starts climbing onto the photocopier!", user)), span_notice(I18n.T("You start climbing onto the photocopier...")));
        else
            user.visible_message(span_warning(I18n.T("[] starts putting [] onto the photocopier!", user, target)), span_notice(I18n.T("You start putting [] onto the photocopier...", target)));

        if (Dm.truthy(await do_after(user, 2 * SECONDS, target: this)))
        {
            if (target == null || QDELETED(target) || QDELETED(this) || !Dm.truthy(await Adjacent(target))) //check if the photocopier/target still exists.
                return null;

            if (target == user)
                user.visible_message(span_notice(I18n.T("[] climbs onto the photocopier!", user)), span_notice(I18n.T("You climb onto the photocopier.")));
            else
                user.visible_message(span_warning(I18n.T("[] puts [] onto the photocopier!", user, target)), span_notice(I18n.T("You put [] onto the photocopier.", target)));

            await target.forceMove(drop_location() as atom);
            ass = target as mob_living;

            if (object_copy != null)
            {
                await object_copy.forceMove(drop_location() as atom);
                visible_message(span_warning(I18n.T("[] is shoved out of the way by []!", object_copy, ass)));
                object_copy = null;
            }
        }
        return null;
    }

    /**
     * Checks the living mob `ass` exists and its location is the same as the photocopier.
     *
     * Returns FALSE if `ass` doesn't exist or is not at the copier's location. Returns TRUE otherwise.
     */
    // tg: code/modules/paperwork/photocopier.dm:831
    public virtual object check_ass() //I'm not sure wether I made this proc because it's good form or because of the name.
    {
        if (!isliving(ass))
            return FALSE;
        if (ass.loc != loc)
        {
            ass = null;
            return FALSE;
        }
        return TRUE;
    }

    /**
     * Checks if the copier is deleted, or has something dense at its location. Called in `mouse_drop_receive()`
     */
    // tg: code/modules/paperwork/photocopier.dm:842
    public virtual object copier_blocked()
    {
        if (QDELETED(this))
            return null;
        if (Dm.truthy(((atom)loc).density))
            return TRUE;
        foreach (atom_movable AM in Dm.each<atom_movable>(loc))
        {
            if (AM == this)
                continue;
            if (Dm.truthy(AM.density))
                return TRUE;
        }
        return FALSE;
    }

    /**
     * Removes a certain amount of toner that is affected by the efficiency of stock parts
     */
    // tg: code/modules/paperwork/photocopier.dm:857
    public virtual void use_toner(object amount = null)
    {
        toner_cartridge.charges -= Dm.num(amount) / Dm.num(toner_efficiency);
    }

    /**
     * Checks if there is an item inserted into the copier or a mob sitting on top of it.
     *
     * Return `FALSE` is the copier has something inside of it. Returns `TRUE` if it doesn't.
     */
    // tg: code/modules/paperwork/photocopier.dm:865
    public virtual object copier_empty()
    {
        if (object_copy != null || Dm.truthy(check_ass()))
            return FALSE;
        else
            return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/paperwork/photocopier.dm:104  /obj/machinery/photocopier's var/static/list/whitelist_scannable_objects
    internal DmList obj_machinery_photocopier__whitelist_scannable_objects = typecacheof(Dm.list(
        typeof(obj_item_paper),
        typeof(obj_item_photo),
        typeof(obj_item_documents),
        typeof(obj_item_paperwork))) as DmList;

    // tg: code/modules/paperwork/photocopier.dm:111  /obj/machinery/photocopier's var/static/list/valid_paper_types
    internal DmList obj_machinery_photocopier__valid_paper_types = Dm.list(
        typeof(obj_item_paper),
        typeof(obj_item_paper_carbon),
        typeof(obj_item_paper_construction),
        typeof(obj_item_paper_natural));
}

/*
 * Toner cartridge
 */
// tg: code/modules/paperwork/photocopier.dm:879
public partial class obj_item_toner : obj_item
{
    // tg: code/modules/paperwork/photocopier.dm:889
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iodine), 40), Dm.kv(typeof(datum_reagent_iron), 10));
    }

    // tg: code/modules/paperwork/photocopier.dm:892
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The ink level gauge on the side reads []%", round(charges / max_charges * 100))));
        return dot;
    }
}

// tg: code/modules/paperwork/photocopier.dm:896
public partial class obj_item_toner_large : obj_item_toner
{
    // tg: code/modules/paperwork/photocopier.dm:903
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_iodine), 90), Dm.kv(typeof(datum_reagent_iron), 10));
    }
}
