// Translated from tgstation (snapshot 1d99bd7f): code/modules/art/paintings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file but the framers' personality moods; the patronage's payment came with the economy (gaps P4a).
// A finished painting's picture is the PNG tg writes (rust-g's dmi_create_png through the file seam); the client gets it as
// a runtime resource (protocol 79). The var blocks are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/art/paintings.dm:1  GLOBAL_LIST_INIT(canvas_dimensions, init_canvas_dimensions())
    public DmList canvas_dimensions;

    // tg: code/modules/art/paintings.dm:1  (GLOBAL_MANAGED(canvas_dimensions, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcanvas_dimensions()
    {
        canvas_dimensions = init_canvas_dimensions();
        Dm.append(ref gvars_datum_init_order, "canvas_dimensions");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/art/paintings.dm:3
    public static DmList init_canvas_dimensions()
    {
        DmList dot = Dm.list();
        foreach (Type canvas_type in Dm.each_cast<Type>(typesof(typeof(obj_item_canvas))))
        {
            float width = Dm.initial(canvas_type, (obj_item_canvas c) => c.width);
            float height = Dm.initial(canvas_type, (obj_item_canvas c) => c.height);
            dot[Dm.text("[]x[]", width, height)] = Dm.list(width, height);
        }
        return dot;
    }
}

///////////
// EASEL //
///////////

// tg: code/modules/art/paintings.dm:14
public partial class obj_structure_easel : obj_structure
{
    //Adding canvases
    // tg: code/modules/art/paintings.dm:26
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_canvas canvas)
        {
            await user.transfer_item_to_turf(canvas, get_turf(this), silent: FALSE);
            painting = canvas;
            canvas.layer = layer + 0.1f;
            user.visible_message(span_notice(I18n.T("[] puts \\the [] on \\the [].", user, canvas, this)), span_notice(I18n.T("You place \\the [] on \\the [].", canvas, this)));
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    //Stick to the easel like glue
    // tg: code/modules/art/paintings.dm:37
    public override async DmTask<object> Move(atom newloc = null, object direct = null, float? glide_size_override = null, float? update_dir = null)
    {
        turf T = get_turf(this);
        object dot = await base.Move(newloc, direct, glide_size_override, update_dir);
        if (painting != null && painting.loc == T) //Only move if it's near us.
            await painting.forceMove(get_turf(this));
        else
            painting = null;
        return dot;
    }
}

// tg: code/modules/art/paintings.dm:45
public partial class obj_item_canvas : obj_item
{
    // tg: code/modules/art/paintings.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        workspace = Dm.@new<datum_sprite_editor_workspace>(width,
            height,
            Dm.named("color_mode", SPRITE_EDITOR_COLOR_MODE_RGB),
            Dm.named("config_flags", NONE),
            Dm.named("tool_flags", Dm.bitor(SPRITE_EDITOR_TOOL_PENCIL, SPRITE_EDITOR_TOOL_BUCKET)),
            Dm.named("initial_layer_color", Dm.text("[]ff", canvas_color)) // To avoid needing to handle strings of mixed lengths, sprite editor workspaces always use the alpha channel
        );
        RegisterSignal(workspace, COMSIG_SPRITE_EDITOR_VALIDATE_COLOR, nameof(validate_color));
        painting_metadata = Dm.@new<datum_painting>();
        painting_metadata.title = "Untitled Artwork";
        painting_metadata.creation_round_id = GLOB.round_id;
        painting_metadata.width = width;
        painting_metadata.height = height;
        ADD_KEEP_TOGETHER(this, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:103
    public override object Destroy(float? force = null)
    {
        last_patron = null;
        if (loc is obj_structure_sign_painting frame)
            frame.remove_art_element(painting_metadata.credit_value);
        painting_metadata = null;
        return base.Destroy(force);
    }

    // tg: code/modules/art/paintings.dm:111
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:115
    public override object ui_host(mob user = null)
    {
        if (loc is obj_structure_sign_painting)
            return loc;
        return base.ui_host(user);
    }

    // tg: code/modules/art/paintings.dm:120
    public override object ui_state(mob user = null)
    {
        if (Dm.truthy(finalized))
            return GLOB.hold_or_view_state;
        return GLOB.default_state;
    }

    // tg: code/modules/art/paintings.dm:125
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Canvas", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/art/paintings.dm:131
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        await ui_interact(user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/art/paintings.dm:135
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        var dot = await base.ui_static_data(user) as DmList ?? Dm.list();
        dot["year_offset"] = STATION_YEAR_OFFSET;
        return dot;
    }

    // tg: code/modules/art/paintings.dm:139
    public override async DmTask<object> ui_data(mob user = null)
    {
        await base.ui_data(user);
        DmList metadata = Dm.list(
            Dm.kv("title", painting_metadata.title),
            Dm.kv("author", painting_metadata.creator_name),
            Dm.kv("patron", painting_metadata.patron_name),
            Dm.kv("medium", painting_metadata.medium),
            Dm.kv("date", painting_metadata.creation_date)
        );
        DmList editor_data = workspace.sprite_editor_ui_data();
        bool can_edit = true;

        var implement = user.get_active_held_item() as obj_item;
        object implement_color = get_paint_tool_color(implement);
        object can_change_implement_color = can_change_paint_tool_color(implement);
        if (Dm.truthy(implement_color))
        {
            editor_data["serverSelectedColor"] = implement_color;
            editor_data["serverPalette"] = get_paint_tool_palette(implement);
            editor_data["maxServerColors"] = get_paint_tool_palette_capacity(implement);
            editor_data["onSelectServerColor"] = "onSelectColor";
            editor_data["onAddServerColor"] = "onAddPaletteColor";
            editor_data["onRemoveServerColor"] = "onRemovePaletteColor";
            if (Dm.truthy(can_change_implement_color))
                editor_data["toolFlags"] = Dm.bitor(editor_data["toolFlags"], SPRITE_EDITOR_TOOL_DROPPER);
        }
        else
            can_edit = false;
        return Dm.list(
            Dm.kv("metadata", metadata),
            Dm.kv("editorData", editor_data),
            Dm.kv("pixelsPerUnit", pixels_per_unit),
            Dm.kv("finalized", finalized),
            Dm.kv("allowColorPicker", can_change_implement_color),
            Dm.kv("editable", can_edit && !Dm.truthy(finalized) ? TRUE : FALSE), //Ideally you should be able to draw moustaches on existing paintings in the gallery but that's not implemented yet
            Dm.kv("showPlaque", loc is obj_structure_sign_painting ? TRUE : FALSE)
        );
    }

    // tg: code/modules/art/paintings.dm:175
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:179
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        mob user = usr;
        var implement = user.get_active_held_item() as obj_item;
        var palette_comp = implement?.GetComponent(typeof(datum_component_palette)) as datum_component_palette;
        switch (Dm.str(action))
        {
            case "spriteEditorCommand":
            {
                dot = TRUE;
                if (Dm.truthy(finalized))
                    return dot;
                object command = @params["command"];
                if (!Dm.eq(command, "transaction")) // Painting only allows transactions, no undo/redo or layer visibility toggling
                    return dot;
                if (!Dm.truthy(workspace.new_transaction(@params["transaction"])))
                    return dot;
                object medium = get_paint_tool_medium(implement);
                if (Dm.truthy(medium) && Dm.truthy(painting_metadata.medium) && !Dm.eq(painting_metadata.medium, medium))
                    painting_metadata.medium = "Mixed medium";
                else
                    painting_metadata.medium = medium;
                used = TRUE;
                update_appearance();
                break;
            }
            case "onSelectColor":
            {
                dot = TRUE;
                object paint_color = copytext(@params["color"], 1, 8);
                implement?.set_painting_tool_color(paint_color);
                break;
            }
            case "onAddPaletteColor":
            {
                dot = TRUE;
                if (palette_comp == null)
                    return dot;
                if (Dm.length(palette_comp.colors) >= Dm.num(palette_comp.max_colors))
                    return dot;
                object paint_color = copytext(@params["color"], 1, 8);
                palette_comp.colors.Add(paint_color);
                break;
            }
            case "onRemovePaletteColor":
            {
                dot = TRUE;
                if (palette_comp == null)
                    return dot;
                float color_index = Dm.num(@params["index"]);
                palette_comp.colors.Cut(color_index, color_index + 1);
                break;
            }
            case "finalize":
                dot = TRUE;
                await finalize(user);
                break;
            case "patronage":
                dot = TRUE;
                await patron(user as mob_living);
                break;
        }
        return dot;
    }

    // tg: code/modules/art/paintings.dm:228
    public virtual object validate_color(object _source = null, object paint_color = null)
    {
        paint_color = copytext(paint_color, 1, 8);
        var implement = usr.get_active_held_item() as obj_item;
        if (implement == null || !(Dm.eq(get_paint_tool_color(implement), paint_color) || Dm.@in(paint_color, get_paint_tool_palette(implement))))
            return COLOR_IS_INVALID;
        return null;
    }

    // tg: code/modules/art/paintings.dm:235
    public virtual async DmTask finalize(mob user = null)
    {
        if (Dm.truthy(finalized) || Dm.truthy(painting_metadata.loaded_from_json))
            return;
        if (!in_range(this, user))
        {
            user.balloon_alert(user, "too far away!");
            return;
        }
        if (!Dm.truthy(await try_rename(user)))
            return;

        painting_metadata.creator_ckey = user.ckey;
        painting_metadata.creator_name = user.real_name;
        painting_metadata.creation_date = time2text(world.realtime, "DDD MMM DD hh:mm:ss YYYY", TIMEZONE_UTC);
        painting_metadata.creation_round_id = GLOB.round_id;
        await generate_proper_overlay();
        finalized = TRUE;

        SStgui.update_uis(this);
    }

    // tg: code/modules/art/paintings.dm:253  #define CURATOR_PERCENTILE_CUT 0.225 (file-local, #undef at :322; gaps P4a)
    private const float CURATOR_PERCENTILE_CUT = 0.225f;
    // tg: code/modules/art/paintings.dm:254  #define SERVICE_PERCENTILE_CUT 0.125 (file-local, #undef at :323)
    private const float SERVICE_PERCENTILE_CUT = 0.125f;

    // tg: code/modules/art/paintings.dm:256
    public virtual async DmTask patron(mob_living user = null)
    {
        if (!Dm.truthy(finalized) || !isliving(user))
            return;
        if (!Dm.truthy(painting_metadata.loaded_from_json))
        {
            if (!Dm.eq(await tgui_alert(user, "The painting hasn't been archived yet and will be lost at the end of the shift if not placed in an elegible frame. Continue?", "Unarchived Painting", Dm.list("Yes", "No")), "Yes"))
                return;
        }
        mob_living living_user = user;
        var id_card = living_user.get_idcard(TRUE) as obj_item_card_id;
        if (id_card == null)
        {
            to_chat(user, span_warning(I18n.T("You don't even have a id and you want to be an art patron?")));
            return;
        }
        if (!Dm.truthy(id_card.can_be_used_in_payment(user)))
        {
            to_chat(user, span_warning(I18n.T("No valid non-departmental account found.")));
            return;
        }
        datum_bank_account account = id_card.registered_account;
        if (!account.has_money(painting_metadata.credit_value))
        {
            to_chat(user, span_warning(I18n.T("You can't afford this.")));
            return;
        }
        float sniped_amount = painting_metadata.credit_value;
        object offer_amount = await tgui_input_number(user, I18n.T("How much do you want to offer?"), I18n.T("Patronage Amount"), painting_metadata.credit_value + 1, account.account_balance, painting_metadata.credit_value);
        if (!Dm.truthy(offer_amount) || QDELETED(user) || QDELETED(this) || loc is not obj_structure_sign_painting || !Dm.truthy(await user.can_perform_action(loc as atom, FORBID_TELEKINESIS_REACH)))
            return;
        if (sniped_amount != painting_metadata.credit_value)
            return;
        if (!Dm.truthy(account.adjust_money(-Dm.num(offer_amount), I18n.T("Painting: Patron of []", painting_metadata.title))))
        {
            to_chat(user, span_warning(I18n.T("Transaction failure. Please try again.")));
            return;
        }

        datum_bank_account service_account = SSeconomy.get_dep_account(ACCOUNT_SRV);
        service_account.adjust_money(Dm.num(offer_amount) * SERVICE_PERCENTILE_CUT);
        ///We give the curator(s) a cut (unless they're themselves the patron), as it's their job to curate and promote art among other things.
        if (Dm.truthy(SSeconomy.bank_accounts_by_job[typeof(datum_job_curator)]))
        {
            DmList curator_accounts = Dm.sub(SSeconomy.bank_accounts_by_job[typeof(datum_job_curator)], account) as DmList;
            float curators_length = length(curator_accounts);
            if (Dm.truthy(curators_length))
            {
                float curator_cut = round(Dm.num(offer_amount) * CURATOR_PERCENTILE_CUT / curators_length);
                if (Dm.truthy(curator_cut))
                {
                    foreach (datum_bank_account curator in Dm.each_cast<datum_bank_account>(curator_accounts))
                    {
                        curator.adjust_money(curator_cut, I18n.T("Painting: Patronage cut"));
                        await curator.bank_card_talk(I18n.T("Cut on patronage received, account now holds [] [].", curator.account_balance, MONEY_SYMBOL));
                    }
                }
            }
        }

        if (loc is obj_structure_sign_painting frame)
        {
            frame.remove_art_element(painting_metadata.credit_value);
            frame.add_art_element(); // tg passes offer_amount, which the proc does not take (paintings.dm:726)
        }

        painting_metadata.patron_ckey = user.ckey;
        painting_metadata.patron_name = user.real_name;
        painting_metadata.credit_value = Dm.num(offer_amount);
        last_patron = WEAKREF(user.mind) as datum_weakref;

        to_chat(user, span_notice(I18n.T("Nanotrasen Trust Foundation thanks you for your contribution. You're now an official patron of this painting.")));
        // HAS_PERSONALITY (__DEFINES/mood.dm:109: LAZYACCESS(mob.personalities, ...)), inline (rules 11.1)
        if (Dm.truthy(LAZYACCESS(user.personalities, typeof(datum_personality_creative))))
            await user.add_mood_event("creative_patronage", typeof(datum_mood_event_creative_patronage));
        if (Dm.truthy(LAZYACCESS(user.personalities, typeof(datum_personality_unimaginative))))
            await user.add_mood_event("unimaginative_patronage", typeof(datum_mood_event_unimaginative_patronage));
        DmList possible_frames = SSpersistent_paintings.get_available_frames(offer_amount);
        if (possible_frames.len <= 1) // Not much room for choices here.
            return;
        if (!Dm.eq(await tgui_alert(user, I18n.T("Do you want to change the frame appearance now? You can do so later this shift with Alt-Click as long as you're a patron."), I18n.T("Patronage Frame"), Dm.list(I18n.T("Yes"), I18n.T("No"))), I18n.T("Yes")))
            return;
        if (!Dm.truthy(await can_select_frame(user)))
            return;
        SStgui.close_uis(this); // Close the examine ui so that the radial menu doesn't end up covered by it and people don't get confused.
        await select_new_frame(user, possible_frames);
    }

    // tg: code/modules/art/paintings.dm:325
    public virtual async DmTask select_new_frame(mob user = null, DmList candidates = null)
    {
        DmList possible_frames = candidates ?? SSpersistent_paintings.get_available_frames(painting_metadata.credit_value);
        DmList radial_options = Dm.list();
        foreach (object frame_name in Dm.each(possible_frames))
            radial_options[frame_name] = image(icon, Dm.text("[]frame_[]", icon_state, frame_name));
        object result = await show_radial_menu(user, loc as atom, radial_options, radius: 60, custom_check: Dm.@new<datum_callback>(this, nameof(can_select_frame), user), tooltips: TRUE);
        if (!Dm.truthy(result))
            return;
        painting_metadata.frame_type = Dm.str(result);
        var our_frame = (obj_structure_sign_painting)loc;
        our_frame.balloon_alert(user, Dm.text("frame set to []", result));
        our_frame.update_appearance();
    }

    // tg: code/modules/art/paintings.dm:338
    public virtual async DmTask<object> can_select_frame(mob user = null)
    {
        if (loc is not obj_structure_sign_painting)
            return FALSE;
        if (!Dm.truthy(await loc.IsReachableBy(user)) || Dm.truthy(user.incapacitated))
            return FALSE;
        if (last_patron == null || !IS_WEAKREF_OF(user?.mind, last_patron))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/art/paintings.dm:347
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        var dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (Dm.truthy(icon_generated))
        {
            var generated = mutable_appearance(generated_icon) as mutable_appearance;
            generated.pixel_w = 1;
            generated.pixel_z = 1;
            dot.Add(generated);
            return dot;
        }
        if (!Dm.truthy(used))
            return dot;

        var detail = mutable_appearance(icon, Dm.text("[]wip", icon_state)) as mutable_appearance;
        detail.pixel_w = 1;
        detail.pixel_z = 1;
        dot.Add(detail);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:363
    public virtual async DmTask generate_proper_overlay()
    {
        if (Dm.truthy(icon_generated))
            return;
        string png_filename = "data/paintings/temp_painting.png";
        string image_data = get_data_string();
        string result = rustg_dmi_create_png(png_filename, Dm.text("[]", width), Dm.text("[]", height), image_data);
        if (Dm.truthy(result))
            throw Dm.CRASH(Dm.text("Error generating painting png : []", result), "code/modules/art/paintings.dm", 370);
        painting_metadata.md5 = md5(LOWER_TEXT(image_data));
        generated_icon = Dm.icon(png_filename);
        icon_generated = TRUE;
        update_appearance();
    }

    // tg: code/modules/art/paintings.dm:376
    public virtual string get_data_string()
    {
        DmList data = Dm.list();
        DmList grid = workspace.get_first_layer_pixel_data(SOUTH);
        for (int y = 1; y <= height; y++)
        {
            for (int x = 1; x <= width; x++)
                data.Add(((DmList)grid[y])[x]);
        }
        return Dm.str(data.Join(""));
    }

    //Todo make this element ?
    // tg: code/modules/art/paintings.dm:385
    public virtual object get_paint_tool_color(obj_item painting_implement = null)
    {
        if (painting_implement == null)
            return null;
        if (painting_implement is obj_item_paint_palette palette)
            return LOWER_TEXT(palette.current_color);
        if (painting_implement is obj_item_toy_crayon crayon)
            return LOWER_TEXT(crayon.paint_color);
        else if (painting_implement is obj_item_pen pen)
            return LOWER_TEXT(pen.colour);
        else if (painting_implement is obj_item_airlock_painter_decal painter)
            return LOWER_TEXT(painter.selected_custom_color);
        else if (painting_implement is obj_item_soap || painting_implement is obj_item_rag)
            return LOWER_TEXT(canvas_color);
        return null;
    }

    // tg: code/modules/art/paintings.dm:403
    public virtual DmList get_paint_tool_palette(obj_item painting_implement = null)
    {
        if (painting_implement == null)
            return Dm.list();
        if (painting_implement.GetComponent(typeof(datum_component_palette)) is not datum_component_palette palette_comp)
        {
            object implement_color = get_paint_tool_color(painting_implement);
            return Dm.truthy(implement_color) ? Dm.list(implement_color) : Dm.list();
        }
        return palette_comp.colors;
    }

    // tg: code/modules/art/paintings.dm:412
    public virtual object get_paint_tool_palette_capacity(obj_item painting_implement = null)
    {
        if (painting_implement == null)
            return null;
        if (painting_implement.GetComponent(typeof(datum_component_palette)) is not datum_component_palette palette_comp)
            return Dm.truthy(get_paint_tool_color(painting_implement)) ? 1 : 0;
        return palette_comp.max_colors;
    }

    // tg: code/modules/art/paintings.dm:420
    public virtual object can_change_paint_tool_color(obj_item painting_implement = null)
    {
        if (painting_implement == null)
            return null;
        // tg also tests istype(painting_metadata, /obj/item/paint_palette), which is never true (a painting's metadata is a /datum/painting)
        if (painting_implement is obj_item_airlock_painter_decal)
            return TRUE;
        if (painting_implement is obj_item_toy_crayon crayon)
            return crayon.can_change_colour;
        return null;
    }

    /// Generates medium description
    // tg: code/modules/art/paintings.dm:430
    public virtual object get_paint_tool_medium(obj_item painting_implement = null)
    {
        if (painting_implement == null)
            return null;
        if (painting_implement is obj_item_paint_palette)
            return "Oil on canvas";
        else if (painting_implement is obj_item_toy_crayon_spraycan)
            return "Spraycan on canvas";
        else if (painting_implement is obj_item_toy_crayon)
            return "Crayon on canvas";
        else if (painting_implement is obj_item_pen || painting_implement is obj_item_airlock_painter_decal)
            return "Ink on canvas";
        else if (painting_implement is obj_item_soap || painting_implement is obj_item_rag)
            return null; //These are just for cleaning, ignore them
        else
            return "Unknown medium";
    }

    // tg: code/modules/art/paintings.dm:446
    public virtual async DmTask<object> try_rename(mob user = null)
    {
        if (Dm.truthy(painting_metadata.loaded_from_json)) // No renaming old paintings
            return TRUE;
        object new_name = await tgui_input_text(user, "What do you want to name the painting?", "Title Your Masterpiece", max_length: MAX_NAME_LEN);
        new_name = reject_bad_name(new_name, allow_numbers: TRUE, ascii_only: FALSE, strict: TRUE, cap_after_symbols: FALSE);
        if (new_name == null)
            return FALSE;
        if (!Dm.eq(new_name, painting_metadata.title) && Dm.truthy(await user.can_perform_action(this)))
            painting_metadata.title = new_name;
        switch (Dm.str(await tgui_alert(user, "Do you want to sign it or remain anonymous?", "Sign painting?", Dm.list("Yes", "No", "Cancel"))))
        {
            case "Yes":
                return TRUE;
            case "No":
                painting_metadata.creator_name = "Anonymous";
                return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/art/paintings.dm:772
    public virtual void fill_grid_from_icon(icon I = null)
    {
        DmList grid = workspace.get_first_layer_pixel_data(SOUTH);
        float h = I.Height() + 1;
        for (int x = 1; x <= width; x++)
        {
            for (int y = 1; y <= height; y++)
                ((DmList)grid[y])[x] = I.GetPixel(x, h - y);
        }
    }
}

// tg: code/modules/art/paintings.dm:518
public partial class obj_item_canvas_thirtysix_twentyfour : obj_item_canvas
{
    // tg: code/modules/art/paintings.dm:518
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_item_scaling), 1, 0.8f));
        icon = Dm.resource("icons/obj/art/artstuff_64x64.dmi");
        icon_state = "36x24";
        return dot;
    }
}

// tg: code/modules/art/paintings.dm:538
public partial class obj_item_canvas_fortyfive_twentyseven : obj_item_canvas
{
    // tg: code/modules/art/paintings.dm:538
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_item_scaling), 1, 0.7f));
        icon = Dm.resource("icons/obj/art/artstuff_64x64.dmi");
        icon_state = "45x27";
        return dot;
    }
}

// tg: code/modules/art/paintings.dm:555
public partial class obj_structure_sign_painting : obj_structure_sign
{
    // tg: code/modules/art/paintings.dm:580
    public override object get_save_vars()
    {
        var dot = base.get_save_vars() as DmList;
        Dm.remove(ref dot, "icon"); // NAMEOF(src, icon)
        return dot;
    }

    // tg: code/modules/art/paintings.dm:583
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        SSpersistent_paintings.painting_frames.Add(this);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:587
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        SSpersistent_paintings.painting_frames.Remove(this);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:591
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (current_canvas == null && tool is obj_item_canvas canvas)
        {
            await frame_canvas(user, canvas);
            return ITEM_INTERACT_SUCCESS;
        }
        if (current_canvas != null && Dm.eq(current_canvas.painting_metadata.title, "Untitled Artwork") && tool is obj_item_pen) // initial(painting_metadata.title): the metadata's title as the canvas makes it
        {
            if (Dm.truthy(await try_rename(user)))
                SStgui.update_uis(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/art/paintings.dm:600
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        turf drop_turf = drop_location() as turf;
        if (current_canvas != null)
            await current_canvas.forceMove(drop_turf);
        var frame = Dm.@new(wallframe_type, drop_turf) as obj_item_wallframe;
        frame.update_integrity(get_integrity()); //Transfer how damaged it is.
    }

    // tg: code/modules/art/paintings.dm:606
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(persistence_id))
            dot.Add(span_notice(I18n.T("Any painting placed here will be archived at the end of the shift.")));
        if (current_canvas != null)
        {
            await current_canvas.ui_interact(user);
            dot.Add(span_notice(I18n.T("Use wirecutters to remove the painting.")));
            if (IS_WEAKREF_OF(user?.mind, current_canvas.last_patron))
                dot.Add(span_notice(I18n.T("<b>Alt-Click</b> to change select a new appearance for the frame of this painting.")));
        }
        return dot;
    }

    // tg: code/modules/art/paintings.dm:616
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.wirecutter_act(user, tool);
        if (current_canvas != null)
        {
            await current_canvas.forceMove(drop_location() as atom);
            to_chat(user, span_notice(I18n.T("You remove the painting from the frame.")));
            return TRUE;
        }
        return dot;
    }

    // tg: code/modules/art/paintings.dm:623
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == current_canvas)
        {
            if (!QDELETED(current_canvas))
                remove_art_element(current_canvas.painting_metadata.credit_value);
            current_canvas = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/art/paintings.dm:631
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (current_canvas == null || !Dm.truthy(await current_canvas.can_select_frame(user)))
            return CLICK_ACTION_BLOCKING;

        Waiting.Detach(current_canvas.select_new_frame(user));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/modules/art/paintings.dm:638
    public virtual async DmTask<object> frame_canvas(mob_living user = null, obj_item_canvas new_canvas = null)
    {
        if (!Dm.@in(new_canvas.type, accepted_canvas_types))
        {
            to_chat(user, span_warning(I18n.T("[] won't fit in this frame.", new_canvas)));
            return FALSE;
        }
        if (Dm.truthy(await user.transferItemToLoc(new_canvas, this)))
        {
            current_canvas = new_canvas;
            if (!Dm.truthy(current_canvas.finalized))
                await current_canvas.finalize(user);
            to_chat(user, span_notice(I18n.T("You frame [].", current_canvas)));
            add_art_element();
            update_appearance();
            // HAS_PERSONALITY(user, creative/unimaginative): the framers' moods (personalities are not ported)
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/art/paintings.dm:656
    public virtual async DmTask<object> try_rename(mob user = null)
    {
        if (!Dm.eq(current_canvas.painting_metadata.title, "Untitled Artwork")) // initial(current_canvas.painting_metadata.title)
            return null;
        if (!Dm.truthy(await current_canvas.try_rename(user)))
            return null;
        SStgui.update_uis(current_canvas);
        return null;
    }

    // tg: code/modules/art/paintings.dm:663
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        // Stops the frame icon_state from poking out behind the paintings. we have proper frame overlays in artstuff.dmi.
        icon = current_canvas?.generated_icon != null ? null : Dm.initial(this, (obj_structure_sign_painting p) => p.icon);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:668
    public override object update_name(float? updates = null)
    {
        name = current_canvas != null ? Dm.text("painting - []", current_canvas.painting_metadata.title) : Dm.initial(this, (obj_structure_sign_painting p) => p.name) as string;
        return base.update_name(updates);
    }

    // tg: code/modules/art/paintings.dm:672
    public override object update_desc(float? updates = null)
    {
        desc = current_canvas != null ? Dm.str(desc_with_canvas) : Dm.initial(this, (obj_structure_sign_painting p) => p.desc) as string;
        return base.update_desc(updates);
    }

    // tg: code/modules/art/paintings.dm:676
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        var dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (current_canvas?.generated_icon == null)
            return dot;

        var painting = mutable_appearance(current_canvas.generated_icon) as mutable_appearance;
        painting.pixel_w = current_canvas.framed_offset_x;
        painting.pixel_z = current_canvas.framed_offset_y;
        dot.Add(painting);
        object frame_type = current_canvas.painting_metadata.frame_type;
        dot.Add(mutable_appearance(current_canvas.icon, Dm.text("[]frame_[]", current_canvas.icon_state, frame_type))); //add the frame
        return dot;
    }

    /**
     * Loads a painting from SSpersistence. Called globally by said subsystem when it inits
     *
     * Deleting paintings leaves their json, so this proc will remove the json and try again if it finds one of those.
     */
    // tg: code/modules/art/paintings.dm:693
    public virtual object load_persistent()
    {
        if (!Dm.truthy(persistence_id))
            return FALSE;
        DmList valid_paintings = SSpersistent_paintings.get_paintings_with_tag(persistence_id);
        if (!Dm.truthy(Dm.length(valid_paintings)))
            return FALSE; //aborts loading anything this category has no usable paintings
        var painting = pick(valid_paintings) as datum_painting;
        string png = Dm.text("data/paintings/images/[].png", painting.md5);
        icon I = Dm.icon(png);
        obj_item_canvas new_canvas = null;
        float w = I.Width();
        float h = I.Height();
        foreach (Type T in Dm.each_cast<Type>(typesof(typeof(obj_item_canvas))))
        {
            if (Dm.eq(Dm.initial(T, (obj_item_canvas c) => c.width), w) && Dm.eq(Dm.initial(T, (obj_item_canvas c) => c.height), h))
            {
                if (!Dm.@in(T, accepted_canvas_types))
                    throw Dm.CRASH(Dm.text("Found painting with canvas size not compatible with this frame. Canvas type: []", T), "code/modules/art/paintings.dm", 709);
                new_canvas = Dm.@new(T, this) as obj_item_canvas;
                break;
            }
        }
        if (new_canvas == null)
            throw Dm.CRASH("Found painting size with no matching canvas type", "code/modules/art/paintings.dm", 713);
        new_canvas.painting_metadata = painting;
        new_canvas.fill_grid_from_icon(I);
        new_canvas.generated_icon = I;
        new_canvas.icon_generated = TRUE;
        new_canvas.finalized = TRUE;
        new_canvas.name = Dm.text("painting - []", painting.title);
        current_canvas = new_canvas;
        add_art_element();
        current_canvas.update_appearance();
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/art/paintings.dm:726
    public virtual void add_art_element()
    {
        float artistic_value = get_art_value(current_canvas.painting_metadata.credit_value);
        if (artistic_value != 0)
            Waiting.NoSleep(AddElement(typeof(datum_element_art), artistic_value));
    }

    // tg: code/modules/art/paintings.dm:731
    public virtual void remove_art_element(object patronage = null)
    {
        float artistic_value = get_art_value(patronage);
        if (artistic_value != 0)
            RemoveElement(typeof(datum_element_art), artistic_value);
    }

    // tg: code/modules/art/paintings.dm:736
    public virtual float get_art_value(object patronage = null)
    {
        float value = Dm.num(patronage);
        if (value >= PATRONAGE_SUPERB_FRAME)
            return GREAT_ART;
        if (value >= PATRONAGE_EXCELLENT_FRAME)
            return GOOD_ART;
        if (value >= PATRONAGE_NICE_FRAME)
            return OK_ART;
        return 0;
    }

    // tg: code/modules/art/paintings.dm:746
    public virtual void save_persistent()
    {
        if (!Dm.truthy(persistence_id) || current_canvas == null || Dm.truthy(current_canvas.no_save) || Dm.truthy(current_canvas.painting_metadata.loaded_from_json))
            return;
        if (!Dm.eq(SANITIZE_FILENAME(persistence_id), persistence_id))
        {
            stack_trace(Dm.text("Invalid persistence_id - []", persistence_id), "code/modules/art/paintings.dm", 750);
            return;
        }
        string data = current_canvas.get_data_string();
        string md5 = Dm.md5(LOWER_TEXT(data));
        foreach (datum_painting entry in Dm.each<datum_painting>(SSpersistent_paintings.paintings))
        {
            if (Dm.eq(entry.md5, md5)) // No duplicates
                return;
        }
        current_canvas.painting_metadata.md5 = md5;
        if (!Dm.truthy(current_canvas.painting_metadata.tags))
            current_canvas.painting_metadata.tags = Dm.list(persistence_id);
        else
        {
            DmList tags = current_canvas.painting_metadata.tags;
            Dm.combine(ref tags, persistence_id);
            current_canvas.painting_metadata.tags = tags;
        }
        string png_directory = "data/paintings/images/";
        string png_path = png_directory + Dm.text("[].png", md5);
        string result = rustg_dmi_create_png(png_path, Dm.text("[]", current_canvas.width), Dm.text("[]", current_canvas.height), data);
        if (Dm.truthy(result))
            throw Dm.CRASH(Dm.text("Error saving persistent painting: []", result), "code/modules/art/paintings.dm", 769);
        SSpersistent_paintings.paintings.Add(current_canvas.painting_metadata);
    }
}

// tg: code/modules/art/paintings.dm:779
public partial class obj_item_wallframe_painting_large : obj_item_wallframe_painting
{
    // tg: code/modules/art/paintings.dm:788
    public override async DmTask<object> try_build(atom support = null, mob user = null)
    {
        object dot = await base.try_build(support, user);
        if (!Dm.truthy(dot))
            return dot;
        var on_wall = support as turf;
        object our_dir = get_dir(user, on_wall);
        float check_dir = Dm.truthy(Dm.bitand(our_dir, Dm.bitor(EAST, WEST))) ? NORTH : EAST;
        if (get_step(on_wall, check_dir) is not turf_closed_wall second_wall || !Dm.truthy(await second_wall.IsReachableBy(user)))
        {
            to_chat(user, span_warning(I18n.T("You need a reachable wall to the [] of this one to mount this frame!", check_dir == EAST ? "right" : "left")));
            return FALSE;
        }
        if (Dm.truthy(check_wall_item(second_wall, our_dir, wall_external)))
        {
            to_chat(user, span_warning(I18n.T("There's already an item on the wall to the [] of this one!", check_dir == EAST ? "right" : "left")));
            return FALSE;
        }
        return dot;
    }

    // tg: code/modules/art/paintings.dm:802
    public override void after_attach(obj attached_to = null)
    {
        base.after_attach(attached_to);
        var our_frame = (obj_structure_sign_painting_large)attached_to;
        our_frame.finalize_size();
    }
}

// tg: code/modules/art/paintings.dm:807
public partial class obj_structure_sign_painting_large : obj_structure_sign_painting
{
    // tg: code/modules/art/paintings.dm:816
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // Necessary so that the painting is framed correctly by the frame overlay when flipped.
        ADD_KEEP_TOGETHER(this, INNATE_TRAIT);
        if (Dm.truthy(mapload))
            finalize_size();
        return base.Initialize(mapload, __extra);
    }

    /**
     * This frame is visually put between two wall turfs and it has an icon that's bigger than 32px, and because
     * of the way it's designed, the pixel_shift variable from the wallframe item won't do.
     * Also we want higher bounds so it actually covers an extra wall turf, so that it can count toward check_wall_item calls for
     * that wall turf.
     */
    // tg: code/modules/art/paintings.dm:829
    public virtual void finalize_size()
    {
        switch ((int)Dm.num(dir)) // the dirs, as ints
        {
            case (int)SOUTH:
                pixel_y = -32;
                bound_width = 64;
                break;
            case (int)NORTH:
                bound_width = 64;
                break;
            case (int)WEST:
                // Totally intended so that the frame sprite doesn't spill behind the wall and get partly covered by the darkness plane.
                // Ditto for the ones below.
                pixel_x = -29;
                bound_height = 64;
                break;
            case (int)EAST:
                bound_height = 64;
                break;
        }
    }

    // tg: code/modules/art/paintings.dm:844
    public override object get_turfs_to_mount_on()
    {
        return !Dm.truthy(pixel_x) && !Dm.truthy(pixel_y) ? Dm.list(get_step(this, Dm.num(dir))) : base.get_turfs_to_mount_on();
    }

    // tg: code/modules/art/paintings.dm:847
    public override async DmTask<object> frame_canvas(mob_living user = null, obj_item_canvas new_canvas = null)
    {
        object dot = await base.frame_canvas(user, new_canvas);
        if (Dm.truthy(dot))
            set_painting_offsets();
        return dot;
    }

    // tg: code/modules/art/paintings.dm:852
    public override object load_persistent()
    {
        object dot = base.load_persistent();
        if (Dm.truthy(dot))
            set_painting_offsets();
        return dot;
    }

    // tg: code/modules/art/paintings.dm:857
    public virtual void set_painting_offsets()
    {
        switch ((int)Dm.num(dir)) // the dirs, as ints
        {
            case (int)EAST:
                transform = (transform as matrix).Turn(90);
                pixel_x += 29;
                pixel_y += 29;
                break;
            case (int)WEST:
                transform = (transform as matrix).Turn(-90);
                break;
            case (int)NORTH:
                pixel_y += 29;
                break;
        }
    }

    // tg: code/modules/art/paintings.dm:868
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == current_canvas)
        {
            switch ((int)Dm.num(dir)) // the dirs, as ints
            {
                case (int)EAST:
                    transform = (transform as matrix).Turn(-90);
                    pixel_x -= 29;
                    pixel_y -= 29;
                    break;
                case (int)WEST:
                    transform = (transform as matrix).Turn(90);
                    break;
                case (int)NORTH:
                    pixel_y -= 29;
                    break;
            }
        }
        return await base.Exited(gone, direction);
    }
}

/// Simple painting utility.
// tg: code/modules/art/paintings.dm:916
public partial class obj_item_paint_palette : obj_item
{
    // tg: code/modules/art/paintings.dm:913  #define AVAILABLE_PALETTE_SPACE 14 (file-local, #undef at :940)
    private const int AVAILABLE_PALETTE_SPACE = 14; // Enough to fill two radial menu pages

    // tg: code/modules/art/paintings.dm:928
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_palette), AVAILABLE_PALETTE_SPACE, current_color));
        return dot;
    }

    // tg: code/modules/art/paintings.dm:932
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await pick_painting_tool_color(user, current_color);
        return dot;
    }

    // tg: code/modules/art/paintings.dm:936
    public override void set_painting_tool_color(object chosen_color = null)
    {
        base.set_painting_tool_color(chosen_color);
        current_color = Dm.str(chosen_color);
    }
}
