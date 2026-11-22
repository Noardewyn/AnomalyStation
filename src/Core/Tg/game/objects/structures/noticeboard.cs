// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/noticeboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but on_object_saved (the admin map export: a live-partial row), gaps S3 (D298). The var blocks are generated.

// tg: code/game/objects/structures/noticeboard.dm:3
public partial class obj_structure_noticeboard : obj_structure
{
    // tg: code/game/objects/structures/noticeboard.dm:1  #define MAX_NOTICES 8 (file-local)
    private const int MAX_NOTICES = 8;

    // tg: code/game/objects/structures/noticeboard.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (!Dm.truthy(mapload))
            return dot;

        foreach (obj_item_paper paper in Dm.each<obj_item_paper>(loc))
        {
            if (notices >= MAX_NOTICES)
                break;

            Waiting.NoSleep(paper.forceMove(this));
            notices++;
        }
        update_appearance(UPDATE_ICON);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    //attaching papers!!
    // tg: code/game/objects/structures/noticeboard.dm:42
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_paper)) && !istype(tool, typeof(obj_item_photo)))
            return NONE;

        if (!Dm.truthy(await allowed__proc(user)))
        {
            to_chat(user, span_warning(I18n.T("You are not authorized to add notices!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (notices >= MAX_NOTICES)
        {
            to_chat(user, span_warning(I18n.T("The notice board is full!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        notices++;
        update_appearance(UPDATE_ICON);
        to_chat(user, span_notice(I18n.T("You pin the [] to the noticeboard.", tool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/noticeboard.dm:63
    public override object ui_state(mob user = null)
    {
        return GLOB.physical_state;
    }

    // tg: code/game/objects/structures/noticeboard.dm:66
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "NoticeBoard", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/noticeboard.dm:72
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["allowed"] = await allowed__proc(user);
        data["items"] = Dm.list();
        foreach (obj_item content in Dm.each<obj_item>(contents))
        {
            DmList content_data = Dm.list(
                Dm.kv("name", content.name),
                Dm.kv("ref", REF(content)));
            Dm.append((DmList)data["items"], Dm.list(content_data));
        }
        return data;
    }

    // tg: code/game/objects/structures/noticeboard.dm:84
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        obj_item item = Dm.locate_ref(@params["ref"]) is obj_item found && Dm.@in(found, contents) ? found : null; // locate(params["ref"]) in contents
        if (item == null || item.loc != this)
            return dot;

        mob user = usr;

        switch (action)
        {
            case "examine":
                if (istype(item, typeof(obj_item_paper)))
                    await item.ui_interact(user);
                else
                    user.examinate(item);
                return TRUE;
            case "remove":
                if (!Dm.truthy(await allowed__proc(user)))
                    return dot;
                await remove_item(item, user);
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/noticeboard.dm:108
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (notices != 0)
            Dm.append(ref dot, Dm.text("notices_[]", notices));
        return dot;
    }

    /**
     * Removes an item from the notice board
     *
     * Arguments:
     * * item - The item that is to be removed
     * * user - The mob that is trying to get the item removed, if there is one
     */
    // tg: code/game/objects/structures/noticeboard.dm:120
    public virtual async DmTask remove_item(obj_item item = null, mob user = null)
    {
        await item.forceMove(drop_location() as atom);
        if (user != null)
        {
            await user.put_in_hands(item);
            balloon_alert(user, I18n.T("removed from board"));
        }
        notices--;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/noticeboard.dm:128
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
            Dm.@new<obj_item_stack_sheet_mineral_wood>(loc);
        else
            Dm.@new<obj_item_wallframe_noticeboard>(loc);
        foreach (obj_item content in Dm.each<obj_item>(contents))
            await remove_item(content);
    }
}
