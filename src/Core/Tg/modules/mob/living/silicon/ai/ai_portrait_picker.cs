// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/ai/ai_portrait_picker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4d (D326): the whole file. The window's pictures (tg's /datum/asset/simple/portraits) are the paintings' PNGs, which the
// client gets as runtime resources (protocol 79). The var block is generated.

/**
 * ## Portrait picker!!
 *
 * It's a tgui window that lets you look through all the portraits, and choose one as your AI.
 * very similar to centcom_podlauncher in terms of how this is coded, so i kept a lot of comments from it
 */
// tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:7
public partial class datum_portrait_picker : datum
{
    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:18
    [DmParams("user")]
    public override async DmTask New(params object[] __extra)
    {
        object user = Dm.arg(__extra, 0);
        await base.New(__extra);
        if (user is client user_client) //user can either be a client or a mob due to byondcode(tm)
            holder = user_client; //if its a client, assign it to holder
        else
        {
            var user_mob = user as mob;
            holder = user_mob?.client; //if its a mob, assign the mob's client to holder
        }
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:26
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "PortraitPicker");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:32
    public override void ui_close(mob user = null)
    {
        qdel(this);
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:35
    public override object ui_state(mob user = null)
    {
        return GLOB.conscious_state;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:38
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/simple/portraits)): the paintings' pictures; the port's asset transport is the client's (DV44),
        // and the pictures are runtime resources the window names by md5 (portrait_assets)
        portrait_assets(user);
        await DmTask.CompletedTask;
        return Dm.list();
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:43
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["paintings"] = matching_paintings ?? SSpersistent_paintings.painting_ui_data(filter: PAINTINGS_FILTER_AI_PORTRAIT);
        data["search_string"] = search_string;
        data["search_mode"] = search_mode == PAINTINGS_FILTER_SEARCH_TITLE ? "Title" : "Author";
        await DmTask.CompletedTask;
        return data;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:50
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (!isAI(holder.mob))
        {
            qdel(this);
            return dot;
        }
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
                search_mode = search_mode == PAINTINGS_FILTER_SEARCH_TITLE ? PAINTINGS_FILTER_SEARCH_CREATOR : PAINTINGS_FILTER_SEARCH_TITLE;
                generate_matching_paintings_list();
                dot = TRUE;
                break;
            case "select":
            {
                //var/list/tab2key = list(TAB_LIBRARY = "library", TAB_SECURE = "library_secure", TAB_PRIVATE = "library_private")
                //var/folder = tab2key[params["tab"]]
                //var/list/current_list = SSpersistent_paintings.paintings[folder]
                var chosen_portrait = Dm.locate_ref(@params["selected"]) as datum_painting;
                if (chosen_portrait == null || !Dm.@in(chosen_portrait, SSpersistent_paintings.paintings))
                    return dot;
                string png = Dm.text("data/paintings/images/[].png", chosen_portrait.md5);
                icon portrait_icon = Dm.icon(png);
                var ai = (mob_living_silicon_ai)holder.mob;
                float w = portrait_icon.Width();
                float h = portrait_icon.Height();

                var MA = mutable_appearance(portrait_icon) as mutable_appearance;
                MA.appearance_flags = Dm.bitor(RESET_COLOR, KEEP_APART);
                MA.layer = FLOAT_LAYER;

                if (w == 23 || h == 23)
                {
                    to_chat(ai, span_notice(I18n.T("Small note: 23x23 Portraits are accepted, but they do not fit perfectly inside the display frame.")));
                    MA.pixel_w = 5;
                    MA.pixel_z = 5;
                }
                else if (w == 24 || h == 24)
                {
                    to_chat(ai, span_notice(I18n.T("Portrait Accepted. Enjoy!")));
                    MA.pixel_w = 4;
                    MA.pixel_z = 4;
                }
                else
                {
                    to_chat(ai, span_warning(I18n.T("Sorry, only 23x23 and 24x24 Portraits are accepted.")));
                    return dot;
                }

                ai.portrait_appearance = MA;

                ai.display_icon_override = "ai-portrait"; //for AI cards

                ai.update_appearance();
                break;
            }
        }
        return dot;
    }

    // tg: code/modules/mob/living/silicon/ai/ai_portrait_picker.dm:101
    public virtual void generate_matching_paintings_list()
    {
        matching_paintings = null;
        if (!Dm.truthy(search_string))
            return;
        matching_paintings = SSpersistent_paintings.painting_ui_data(filter: Dm.bitor(PAINTINGS_FILTER_AI_PORTRAIT, search_mode), search_text: search_string);
    }
}

public static partial class GlobalProcs
{
    // tg's /datum/asset/simple/portraits (asset_list_items.dm): every archived painting's PNG as an asset the window shows. Here each is
    // taken into the world's resource cache (icon(file)), which the replicator sends the window's viewer once (protocol 79).
    public static void portrait_assets(mob user = null)
    {
        foreach (datum_painting painting in Dm.each<datum_painting>(SSpersistent_paintings?.paintings))
            Dm.icon(Dm.text("data/paintings/images/[].png", painting.md5));
    }
}
