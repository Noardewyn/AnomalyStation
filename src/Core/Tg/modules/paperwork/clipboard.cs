// Translated from tgstation (snapshot 1d99bd7f): code/modules/paperwork/clipboard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298) but the skins (setup_reskins and the /datum/atom_skin/clipboard datums: the reskin component and
// its radial menu are not ported, as the other reskinnable items'). The var blocks are generated.

/**
 * Clipboard
 */
// tg: code/modules/paperwork/clipboard.dm:21
public partial class obj_item_clipboard : obj_item
{
    // tg: code/modules/paperwork/clipboard.dm:47
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] begins putting [] head into the clip of \\the []! It looks like [] trying to commit suicide!", user, user.p_their(), this, user.p_theyre())));
        return BRUTELOSS; //The clipboard's clip is very strong. Industrial duty. Can kill a man easily.
    }

    // tg: code/modules/paperwork/clipboard.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        update_appearance();
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/paperwork/clipboard.dm:55
    public override async DmTask setup_reskins()
    {
        Dm.Deferred("code/modules/paperwork/clipboard.dm:56", "setup_reskins: AddComponent(/datum/component/reskinable_item, /datum/atom_skin/clipboard) (the reskin component and its radial menu)");
    }

    // tg: code/modules/paperwork/clipboard.dm:58
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref pen);
        return base.Destroy(force);
    }

    // tg: code/modules/paperwork/clipboard.dm:62
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!Dm.truthy(integrated_pen) && pen != null)
            Dm.append(ref dot, span_notice(I18n.T("Right-click to remove [].", pen)));
        else if (top_paper != null)
            Dm.append(ref dot, span_notice(I18n.T("Right-click to remove [].", top_paper)));
        return dot;
    }

    /// Take out the topmost paper
    // tg: code/modules/paperwork/clipboard.dm:70
    public virtual async DmTask remove_paper(obj_item_paper paper = null, mob user = null)
    {
        if (paper == null)
            return;
        await paper.forceMove(user.loc as atom);
        await user.put_in_hands(paper);
        to_chat(user, span_notice(I18n.T("You remove [] from [].", paper, this)));
    }

    // tg: code/modules/paperwork/clipboard.dm:77
    public virtual async DmTask remove_pen(mob user = null)
    {
        obj_item_pen pen = this.pen;
        await pen.forceMove(user.loc as atom);
        await user.put_in_hands(pen);
        to_chat(user, span_notice(I18n.T("You remove [] from [].", pen, this)));
    }

    // tg: code/modules/paperwork/clipboard.dm:83
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == pen)
        {
            pen = null;
            await update_icon();
            return dot;
        }

        if (gone != top_paper)
            return dot;

        UnregisterSignal(top_paper, COMSIG_ATOM_UPDATED_ICON);
        top_paper = Dm.locate<obj_item_paper>(this);
        await update_icon();
        return dot;
    }

    // tg: code/modules/paperwork/clipboard.dm:97
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        object paper_to_add = get_paper_overlay();
        if (Dm.truthy(paper_to_add))
            Dm.append(ref dot, paper_to_add);
        if (pen != null)
            Dm.append(ref dot, "clipboard_pen");
        Dm.append(ref dot, "clipboard_over");
        return dot;
    }

    // tg: code/modules/paperwork/clipboard.dm:106
    public virtual object get_paper_overlay()
    {
        if (top_paper == null)
            return null;

        mutable_appearance paper_overlay = mutable_appearance(icon, top_paper.icon_state, offset_spokesman: this, appearance_flags: KEEP_APART) as mutable_appearance;
        paper_overlay = top_paper.color_atom_overlay(paper_overlay) as mutable_appearance;
        Dm.append(paper_overlay.overlays, top_paper.overlays);
        return paper_overlay;
    }

    // tg: code/modules/paperwork/clipboard.dm:115
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            if (!Dm.truthy(integrated_pen) && pen != null)
                await remove_pen(user);
            else
                await remove_paper(top_paper, user);
            return TRUE;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }

    // tg: code/modules/paperwork/clipboard.dm:124
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_paper)))
        {
            //Add paper into the clipboard
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            if (top_paper != null)
                UnregisterSignal(top_paper, COMSIG_ATOM_UPDATED_ICON);
            RegisterSignal(tool, COMSIG_ATOM_UPDATED_ICON, nameof(on_top_paper_change));
            top_paper = (obj_item_paper)tool;
            to_chat(user, span_notice(I18n.T("You clip [] onto [].", tool, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (istype(tool, typeof(obj_item_pen)) && pen == null)
        {
            //Add a pen into the clipboard, attack (write) if there is already one
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
                return ITEM_INTERACT_BLOCKING;
            pen = (obj_item_pen)tool;
            to_chat(user, span_notice(I18n.T("You slot [] into [].", tool, this)));
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        // Always label the clipboard so all of the papers don't have to be taken out.
        if (istype(tool, typeof(obj_item_hand_labeler)))
            return await tool.interact_with_atom(this, user);

        if (top_paper != null)
        {
            await top_paper.item_interaction(user, user.get_active_held_item() as obj_item);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }

    // tg: code/modules/paperwork/clipboard.dm:159
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        add_fingerprint(usr);
        await ui_interact(user);
        return null;
    }

    // tg: code/modules/paperwork/clipboard.dm:164
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "Clipboard");
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/paperwork/clipboard.dm:170
    public override async DmTask<object> ui_data(mob user = null)
    {
        // prepare data for TGUI
        DmList data = Dm.list();
        data["pen"] = Dm.text("[]", pen);
        data["integrated_pen"] = integrated_pen;

        data["top_paper"] = Dm.text("[]", top_paper);
        data["top_paper_ref"] = Dm.text("[]", REF(top_paper));

        data["paper"] = Dm.list();
        data["paper_ref"] = Dm.list();
        foreach (obj_item_paper paper in Dm.each<obj_item_paper>(this))
        {
            if (paper == top_paper)
                continue;
            Dm.append((DmList)data["paper"], Dm.text("[]", paper));
            Dm.append((DmList)data["paper_ref"], Dm.text("[]", REF(paper)));
        }

        return data;
    }

    // locate(params["ref"]) in src: the paper of that REF() among the contents, or null
    private obj_item_paper LocatePaper(object paper_ref) => Dm.locate_ref(paper_ref) is obj_item_paper paper && paper.loc == this ? paper : null;

    // tg: code/modules/paperwork/clipboard.dm:189
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        if (IS_UNCONSCIOUS_OR_CRIT(usr) || HAS_TRAIT(usr, TRAIT_HANDS_BLOCKED))
            return dot;

        switch (action)
        {
            // Take the pen out
            case "remove_pen":
                if (pen != null)
                {
                    if (!Dm.truthy(integrated_pen))
                        await remove_pen(usr);
                    else
                        to_chat(usr, span_warning(I18n.T("You can't seem to find a way to remove []'s [].", this, pen)));
                    dot = TRUE;
                }
                break;
            // Take paper out
            case "remove_paper":
            {
                obj_item_paper paper = LocatePaper(@params["ref"]);
                if (paper != null)
                {
                    await remove_paper(paper, usr);
                    dot = TRUE;
                }
                break;
            }
            // Look at (or edit) the paper
            case "edit_paper":
            {
                obj_item_paper paper = LocatePaper(@params["ref"]);
                if (paper != null)
                {
                    await paper.ui_interact(usr);
                    await update_icon();
                    dot = TRUE;
                }
                break;
            }
            // Move paper to the top
            case "move_top_paper":
            {
                obj_item_paper paper = LocatePaper(@params["ref"]);
                if (paper != null)
                {
                    top_paper = paper;
                    to_chat(usr, span_notice(I18n.T("You move [] to the top.", paper)));
                    await update_icon();
                    dot = TRUE;
                }
                break;
            }
            // Rename the paper (it's a verb)
            case "rename_paper":
            {
                obj_item_paper paper = LocatePaper(@params["ref"]);
                if (paper != null)
                {
                    paper.rename();
                    await update_icon();
                    dot = TRUE;
                }
                break;
            }
        }
        return dot;
    }

    /**
     * This is a simple proc to handle calling update_icon() upon receiving the top paper's `COMSIG_ATOM_UPDATE_APPEARANCE`.
     */
    // tg: code/modules/paperwork/clipboard.dm:239
    public virtual void on_top_paper_change()
    {
        update_appearance();
    }

    // tg: code/modules/paperwork/clipboard.dm:243
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return Dm.num(Dm.truthy(base.IsContainedAtomAccessible(contained, user)) || contained == top_paper);
    }
}
