// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/portrait_printer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file but the download (an image file, /datum/computer_file/image: the NanoPaint program's image files
// are not ported). The window's pictures are the paintings' PNGs as runtime resources (protocol 79). The var block is generated.

/**
 * ## the art gallery viewer/printer!
 *
 * Program that lets the curator (or anyone really) browse all of the portraits in the database
 * Stationary consoles can also print them out as they please as long as they've enough paper
 */
// tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:7
public partial class datum_computer_file_program_portrait_printer : datum_computer_file_program
{
    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:27
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        var dot = await base.ui_static_data(user) as DmList ?? Dm.list();
        dot["is_console"] = Dm.bitand(computer.hardware_flag, PROGRAM_CONSOLE);
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:31
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["paintings"] = matching_paintings ?? SSpersistent_paintings.painting_ui_data();
        data["search_string"] = search_string;
        data["search_mode"] = Dm.eq(search_mode, PAINTINGS_FILTER_SEARCH_TITLE) ? "Title" : "Author";
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:38
    public override async DmTask<object> ui_assets(mob user = null)
    {
        portrait_assets(user); // list(get_asset_datum(/datum/asset/simple/portraits)), as the AI's portrait picker
        await DmTask.CompletedTask;
        return Dm.list();
    }

    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:43
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        switch (Dm.str(action))
        {
            case "search":
                if (!Dm.eq(search_string, @params["to_search"]))
                {
                    search_string = @params["to_search"];
                    generate_matching_paintings_list();
                }
                dot = TRUE;
                break;
            case "change_search_mode":
                search_mode = Dm.eq(search_mode, PAINTINGS_FILTER_SEARCH_TITLE) ? PAINTINGS_FILTER_SEARCH_CREATOR : PAINTINGS_FILTER_SEARCH_TITLE;
                generate_matching_paintings_list();
                dot = TRUE;
                break;
            case "print":
                await print_painting(@params["selected"]);
                break;
            case "download":
                download_painting(@params["selected"]);
                break;
        }
        return dot;
    }

    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:60
    public virtual void generate_matching_paintings_list()
    {
        matching_paintings = null;
        if (!Dm.truthy(search_string))
            return;
        matching_paintings = SSpersistent_paintings.painting_ui_data(filter: Dm.num(search_mode), search_text: search_string);
    }

    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:66
    public virtual async DmTask print_painting(object selected_painting = null)
    {
        if (!Dm.truthy(Dm.bitand(computer.hardware_flag, PROGRAM_CONSOLE)))
            return;
        if (Dm.num(computer.stored_paper) < CANVAS_PAPER_COST)
        {
            to_chat(usr, span_notice(I18n.T("Printing error: Your printer needs at least [] paper to print a canvas.", CANVAS_PAPER_COST)));
            return;
        }

        //canvas printing!
        var chosen_portrait = Dm.locate_ref(selected_painting) as datum_painting; // locate(selected_painting) in SSpersistent_paintings.paintings
        if (chosen_portrait != null && !Dm.@in(chosen_portrait, SSpersistent_paintings.paintings))
            chosen_portrait = null;

        obj_item_canvas new_canvas = chosen_portrait?.spawn_canvas(get_turf(computer.physical));
        if (new_canvas == null)
        {
            to_chat(usr, span_notice(I18n.T("Printing error: An unknown error has occurred.")));
            return;
        }
        computer.stored_paper = Dm.num(computer.stored_paper) - CANVAS_PAPER_COST;
        to_chat(usr, span_notice(I18n.T("You have printed [] onto a new canvas.", chosen_portrait.title)));
        playsound(computer.physical, Dm.resource("sound/machines/printer.ogg"), 100, TRUE);
    }

    // tg: code/modules/modular_computers/file_system/programs/portrait_printer.dm:83
    public virtual void download_painting(object selected_painting = null)
    {
        // the painting as an image file (/datum/computer_file/image, store_file): the NanoPaint program's image files are not ported
        Dm.Deferred("code/modules/modular_computers/file_system/programs/portrait_printer.dm:83", "download_painting: a painting as an image file (the NanoPaint image files, not ported)");
    }
}
