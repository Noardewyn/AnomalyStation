// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/folders.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentips (add_context, add_item_context: live-partial rows, D264), gaps S3 (D298). The var blocks are
// generated; the type-level static typecache is in the TgState part below.

// tg: code/modules/paperwork/folders.dm:1
public partial class obj_item_folder : obj_item
{
    /// A typecache of the objects that can be inserted into a folder
    // tg: code/modules/paperwork/folders.dm:14  var/static/list/folder_insertables (rules 10.6)
    public static ref DmList folder_insertables => ref TgState.Current.obj_item_folder__folder_insertables;

    // tg: code/modules/paperwork/folders.dm:28
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins filing an imaginary death warrant! It looks like [] trying to commit suicide!", user, user.p_theyre())));
        return OXYLOSS;
    }

    // tg: code/modules/paperwork/folders.dm:32
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(update_icon());
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_burn_on_item_ignition)));
        register_item_context();
        register_context();
        return dot;
    }

    // tg: code/modules/paperwork/folders.dm:39
    public override object Destroy(float? force = null)
    {
        foreach (obj important_thing in Dm.each<obj>(contents))
        {
            if (!Dm.truthy(Dm.bitand(important_thing.resistance_flags, INDESTRUCTIBLE)))
                continue;
            Waiting.NoSleep(important_thing.forceMove(drop_location() as atom)); //don't destroy round critical content such as objective documents.
        }
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/folders.dm:46
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(length(contents)) && !Dm.truthy(contents_hidden))
            Dm.append(ref dot, span_notice(I18n.T("<b>Right-click</b> to remove [].", contents[1])));
        return dot;
    }

    // tg: code/modules/paperwork/folders.dm:72
    public virtual async DmTask remove_item(obj_item Item = null, mob user = null)
    {
        if (Item != null)
        {
            await Item.forceMove(user.loc as atom);
            await user.put_in_hands(Item);
            to_chat(user, span_notice(I18n.T("You remove [] from [].", Item, this)));
            await update_icon();
        }
    }

    // tg: code/modules/paperwork/folders.dm:79
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(length(contents)) && Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await remove_item(contents[1] as obj_item, user);
            return TRUE;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/paperwork/folders.dm:85
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(length(contents)))
        {
            object to_add = get_paper_overlay();
            if (Dm.truthy(to_add))
                Dm.append(ref dot, to_add);
        }
        return dot;
    }

    // tg: code/modules/paperwork/folders.dm:92
    public virtual object get_paper_overlay()
    {
        mutable_appearance paper_overlay = mutable_appearance(icon, paper_overlay_state, offset_spokesman: this, appearance_flags: KEEP_APART) as mutable_appearance;
        paper_overlay = ((atom)contents[1]).color_atom_overlay(paper_overlay) as mutable_appearance;
        return paper_overlay;
    }

    // tg: code/modules/paperwork/folders.dm:97
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(is_type_in_typecache(tool, folder_insertables)))
            return await interact_with_insertables(tool, user);
        if (Dm.eq(tool.tool_behaviour, TOOL_KNIFE) || Dm.eq(tool.tool_behaviour, TOOL_WIRECUTTER))
            return await sharp_thing_act(user, tool);
        return NONE;
    }

    // tg: code/modules/paperwork/folders.dm:104
    public override async DmTask<object> nameformat(object input = null, mob_living user = null)
    {
        playsound(this, SFX_WRITING_PEN, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE, SOUND_FALLOFF_EXPONENT + 3, ignore_walls: FALSE);
        return I18n.T("folder[]", Dm.truthy(input) ? I18n.T(" - '[]'", input) : null);
    }

    // tg: code/modules/paperwork/folders.dm:109
    public virtual async DmTask<object> sharp_thing_act(mob user = null, obj_item sharp_tool = null)
    {
        if (Dm.truthy(length(contents)))
        {
            balloon_alert(user, I18n.T("empty [] first!", this));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert(user, I18n.T("cut apart"));
        qdel(this);
        await user.put_in_hands(Dm.@new<obj_item_stack_sheet_cardboard>());
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/folders.dm:119
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(is_type_in_typecache(interacting_with, folder_insertables)))
            return await interact_with_insertables(interacting_with as obj_item, user);
        return null;
    }

    // tg: code/modules/paperwork/folders.dm:123
    public virtual async DmTask<object> interact_with_insertables(obj_item interacting_with = null, mob_living user = null)
    {
        if (interacting_with.loc == user)
        {
            if (!Dm.truthy(await user.transferItemToLoc(interacting_with, this, silent: TRUE)))
                return ITEM_INTERACT_BLOCKING;
        }
        else
        {
            interacting_with.do_pickup_animation(this);
            await interacting_with.forceMove(this);
        }

        if (Dm.truthy(interacting_with.pickup_sound))
            playsound(this, interacting_with.pickup_sound, PICKUP_SOUND_VOLUME, interacting_with.sound_vary, ignore_walls: FALSE);

        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/paperwork/folders.dm:137
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        add_fingerprint(usr);
        await ui_interact(user);
        return null;
    }

    // tg: code/modules/paperwork/folders.dm:142
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Folder");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/paperwork/folders.dm:148
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        if (istype(this, typeof(obj_item_folder_syndicate)))
            data["theme"] = "syndicate";
        data["bg_color"] = Dm.text("[]", bg_color);
        data["folder_name"] = Dm.text("[]", name);

        data["contents"] = Dm.list();
        data["contents_ref"] = Dm.list();
        foreach (object Content in Dm.each(this))
        {
            Dm.append((DmList)data["contents"], Dm.text("[]", Content));
            Dm.append((DmList)data["contents_ref"], Dm.text("[]", REF(Content)));
        }

        return data;
    }

    // tg: code/modules/paperwork/folders.dm:163
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (IS_UNCONSCIOUS_OR_CRIT(usr) || HAS_TRAIT(usr, TRAIT_HANDS_BLOCKED))
            return dot;

        // locate(params["ref"]) in src
        obj_item Item = Dm.locate_ref(@params["ref"]) is obj_item found && found.loc == this ? found : null;
        switch (action)
        {
            // Take item out
            case "remove":
                await remove_item(Item, usr);
                dot = TRUE;
                break;
            // Inspect the item
            case "examine":
                if (Item != null)
                {
                    usr.examinate(Item);
                    dot = TRUE;
                }
                break;
        }
        return dot;
    }

    // tg: code/modules/paperwork/folders.dm:183
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/paperwork/folders.dm:14  /obj/item/folder's var/static/list/folder_insertables
    internal DmList obj_item_folder__folder_insertables = typecacheof(Dm.list(
        typeof(obj_item_paper),
        typeof(obj_item_photo),
        typeof(obj_item_documents),
        typeof(obj_item_paperwork))) as DmList;
}
