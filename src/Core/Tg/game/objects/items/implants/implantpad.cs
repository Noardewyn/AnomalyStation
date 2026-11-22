// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implantpad.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The implant pad: an implant case put in (its "implantpad-1" picture), its implant's information and
// lore read in tg's ImplantPad window (the client's Interfaces/ImplantPad.cs), the case ejected by the window or Alt-click, and a
// deathrattle implant's group saved, set to the saved one or started anew. The var block is generated (D111).

// tg: code/game/objects/items/implants/implantpad.dm:1
public partial class obj_item_implantpad : obj_item
{
    // tg: code/game/objects/items/implants/implantpad.dm:21
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]-[]", base_icon_state, Dm.num(inserted_case != null));
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/implants/implantpad.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(inserted_case))
        {
            Dm.append(ref dot, span_info(I18n.T("It is currently empty.")));
            return dot;
        }

        if (Dm.truthy(await Adjacent(user)))
            Dm.append(ref dot, span_info(I18n.T("It contains \\a [].", inserted_case)));
        else
            Dm.append(ref dot, span_warning(I18n.T("There seems to be something inside it, but you can't quite tell what from here...")));
        Dm.append(ref dot, span_info(I18n.T("Alt-click to remove [].", inserted_case)));
        return dot;
    }

    // tg: code/game/objects/items/implants/implantpad.dm:37
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == inserted_case)
        {
            inserted_case = null;
            update_appearance(UPDATE_ICON);
        }
        return dot;
    }

    // tg: code/game/objects/items/implants/implantpad.dm:43
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(inserted_case) || tool is not obj_item_implantcase)
            return NONE;
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        user.balloon_alert(user, I18n.T("case inserted"));
        inserted_case = (obj_item_implantcase)tool;
        await update_static_data_for_all_viewers();
        update_appearance(UPDATE_ICON);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/implants/implantpad.dm:54
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await remove_implant(user);
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/implants/implantpad.dm:58
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "ImplantPad", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/implants/implantpad.dm:64
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["saved_deathrattle_group"] = Dm.truthy(saved_deathrattle_group) ? saved_deathrattle_group.name : null;
        data["current_deathrattle_group"] = null;
        data["has_case"] = Dm.num(Dm.truthy(inserted_case));
        if (!Dm.truthy(inserted_case))
            return data;
        data["has_implant"] = Dm.num(Dm.truthy(inserted_case.imp));
        if (Dm.truthy(inserted_case.imp))
        {
            data["case_information"] = inserted_case.imp.get_data();
            data["case_lore"] = inserted_case.imp.get_lore();
            if (inserted_case.imp is obj_item_implant_deathrattle inserted_deathrattle)
            {
                if (Dm.truthy(inserted_deathrattle.current_group))
                    data["current_deathrattle_group"] = inserted_deathrattle.current_group;
            }
        }
        return data;
    }

    // tg: code/game/objects/items/implants/implantpad.dm:81
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        mob user = usr;
        if (Dm.eq(action, "eject_implant"))
        {
            await remove_implant(user);
            return dot;
        }
        if (Dm.eq(action, "save_deathrattle_group"))
            await save_deathrattle_group(user);
        if (Dm.eq(action, "set_deathrattle_group"))
            await set_deathrattle_group(user);
        if (Dm.eq(action, "init_deathrattle_group"))
            await init_deathrattle_group(user);
        return dot;
    }

    ///Removes the implant from the pad and puts it in the user's hands if possible.
    // tg: code/game/objects/items/implants/implantpad.dm:95
    public virtual async DmTask<object> remove_implant(mob user = null)
    {
        if (!Dm.truthy(inserted_case))
        {
            user.balloon_alert(user, I18n.T("no case inside!"));
            return FALSE;
        }
        add_fingerprint(user);
        inserted_case.add_fingerprint(user);
        await user.put_in_hands(inserted_case);
        user.balloon_alert(user, I18n.T("case removed"));
        update_appearance(UPDATE_ICON);
        await update_static_data_for_all_viewers();
        return TRUE;
    }

    /// Saves the currently inserted implant's deathrattle group.
    // tg: code/game/objects/items/implants/implantpad.dm:108
    public virtual async DmTask<object> save_deathrattle_group(mob user = null)
    {
        if (!Dm.truthy(inserted_case))
        {
            user.balloon_alert(user, I18n.T("no case inside!"));
            return FALSE;
        }
        if (inserted_case.imp is not obj_item_implant_deathrattle)
        {
            user.balloon_alert(user, I18n.T("incompatible implant!"));
            return FALSE;
        }
        obj_item_implant_deathrattle inserted_implant = (obj_item_implant_deathrattle)inserted_case.imp;
        datum_deathrattle_group current_group = inserted_implant.current_group;
        if (!Dm.truthy(current_group))
        {
            user.balloon_alert(user, I18n.T("no active group!"));
            return FALSE;
        }
        saved_deathrattle_group = current_group;
        user.balloon_alert(user, I18n.T("saved group []", current_group.name));
        await update_static_data_for_all_viewers();
        return TRUE;
    }

    /// Sets the currently inserted implant's deathrattle group to saved.
    // tg: code/game/objects/items/implants/implantpad.dm:126
    public virtual async DmTask<object> set_deathrattle_group(mob user = null)
    {
        if (!Dm.truthy(inserted_case))
        {
            user.balloon_alert(user, I18n.T("no case inside!"));
            return FALSE;
        }
        if (!Dm.truthy(saved_deathrattle_group))
        {
            user.balloon_alert(user, I18n.T("no saved deathrattle group!"));
            return FALSE;
        }
        if (inserted_case.imp is not obj_item_implant_deathrattle)
        {
            user.balloon_alert(user, I18n.T("incompatible implant!"));
            return FALSE;
        }
        obj_item_implant_deathrattle inserted_implant = (obj_item_implant_deathrattle)inserted_case.imp;
        if (!istype(saved_deathrattle_group, inserted_implant.deathrattle_group_type))
        {
            user.balloon_alert(user, I18n.T("incompatible deathrattle group!"));
            return FALSE;
        }
        saved_deathrattle_group.register(inserted_implant);
        user.balloon_alert(user, I18n.T("registered to group []", saved_deathrattle_group.name));
        inserted_case.name = Dm.text("[] - []", Dm.initial(inserted_case, (obj_item_implantcase p) => p.name), saved_deathrattle_group.name);
        await update_static_data_for_all_viewers();
        return TRUE;
    }

    /// Initializes and saves a new deathrattle group, then registers the current implant to it.
    // tg: code/game/objects/items/implants/implantpad.dm:147
    public virtual async DmTask<object> init_deathrattle_group(mob user = null)
    {
        if (!Dm.truthy(inserted_case))
        {
            user.balloon_alert(user, I18n.T("no case inside!"));
            return FALSE;
        }
        if (inserted_case.imp is not obj_item_implant_deathrattle)
        {
            user.balloon_alert(user, I18n.T("incompatible implant!"));
            return FALSE;
        }
        obj_item_implant_deathrattle inserted_implant = (obj_item_implant_deathrattle)inserted_case.imp;
        if (Dm.truthy(inserted_implant.current_group))
        {
            user.balloon_alert(user, I18n.T("group already set!"));
            return FALSE;
        }
        // init and save new group
        saved_deathrattle_group = Dm.@new(inserted_implant.deathrattle_group_type) as datum_deathrattle_group;
        // register current implant
        saved_deathrattle_group.register(inserted_implant);
        user.balloon_alert(user, I18n.T("registered to new group []", saved_deathrattle_group.name));
        inserted_case.name += Dm.text(" - []", saved_deathrattle_group.name);
        await update_static_data_for_all_viewers();
        return TRUE;
    }
}
