// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/inspector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file. The N-spect scanner scans an item (or what an item holds) for contraband with a bit of its power
// cell's charge: tg's uplink error buzz and "contraband detected!", or the ping and "clear"; its cell cover is pried open and shut
// with a crowbar, the cell put in and taken out with a ctrl-click, and a scanner with scans_correctly off answers the other way. At
// the snapshot the file has no inspection report and no clown's version. The cell holds the typepath until Initialize makes it, so
// the types converter types it object (Generator.Members.cs' ObjectHoldingVars). The var block is generated (D111).

// tg: code/game/objects/items/inspector.dm:9
public partial class obj_item_inspector : obj_item
{
    // tg: code/game/objects/items/inspector.dm:2  #define ENERGY_TO_SPEAK (0.001 * STANDARD_CELL_CHARGE): nothing in the file reads it
    // (interact_with_atom writes the number out), so it has no C# member.

    // tg: code/game/objects/items/inspector.dm:35
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (ispath(cell))
            cell = Dm.@new((Type)cell, this);
        register_context();
        register_item_context();
        return dot;
    }

    // Clean up the cell on destroy
    // tg: code/game/objects/items/inspector.dm:43
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == cell)
            cell = null;
        return dot;
    }

    // support for items that interact with the cell
    // tg: code/game/objects/items/inspector.dm:49
    public override object get_cell(atom_movable @interface = null, mob user = null)
    {
        return cell;
    }

    // tg: code/game/objects/items/inspector.dm:52
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        cell_cover_open = Dm.not(cell_cover_open);
        balloon_alert(user, I18n.T("[] cell cover", Dm.truthy(cell_cover_open) ? I18n.T("opened") : I18n.T("closed")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/inspector.dm:57
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(cell_cover_open) && tool is obj_item_stock_parts_power_store_cell)
        {
            if (Dm.truthy(cell))
            {
                to_chat(user, span_warning(I18n.T("[] already has a cell installed.", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                cell = tool;
                to_chat(user, span_notice(I18n.T("You successfully install \\the [] into [].", cell, this)));
                return ITEM_INTERACT_SUCCESS;
            }
            return ITEM_INTERACT_BLOCKING;
        }
        return NONE;
    }

    // tg: code/game/objects/items/inspector.dm:69
    public override async DmTask<object> item_ctrl_click(mob user = null)
    {
        if (!Dm.truthy(cell_cover_open) || !Dm.truthy(cell))
            return CLICK_ACTION_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] removes \\the [] from []!", user, cell, this)),
            span_notice(I18n.T("You remove [].", cell)));
        ((atom)cell).add_fingerprint(user);
        await user.put_in_hands((obj_item)cell);
        cell = null;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/items/inspector.dm:79
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("Use on an item to scan if it contains, or is, contraband.")));
        if (!Dm.truthy(cell_cover_open))
        {
            Dm.append(ref dot, span_notice(I18n.T("Its cell cover is closed. It looks like it could be <strong>pried</strong> out, but doing so would require an appropriate tool.")));
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("Its cell cover is open, exposing the cell slot. It looks like it could be <strong>pried</strong> in, but doing so would require an appropriate tool.")));
        if (!Dm.truthy(cell))
            Dm.append(ref dot, span_notice(I18n.T("The slot for a cell is empty.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("\\The [] is firmly in place. Ctrl-click with an empty hand to remove it.", cell)));
        return dot;
    }

    // tg: code/game/objects/items/inspector.dm:91
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(await user.Adjacent(interacting_with)))
            return ITEM_INTERACT_BLOCKING;
        if (Dm.truthy(cell_cover_open))
        {
            balloon_alert(user, I18n.T("close cover first!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(cell) || !Dm.truthy(await ((obj_item_stock_parts_power_store)cell).use(0.001f * STANDARD_CELL_CHARGE)))
        {
            balloon_alert(user, I18n.T("check cell!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (iscarbon(interacting_with)) // Prevents scanning people
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(contraband_scan(interacting_with, user)))
        {
            playsound(this, Dm.resource("sound/machines/uplink/uplinkerror.ogg"), 40);
            balloon_alert(user, I18n.T("contraband detected!"));
            return ITEM_INTERACT_SUCCESS;
        }

        playsound(this, Dm.resource("sound/machines/ping.ogg"), 20);
        balloon_alert(user, I18n.T("clear"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/inspector.dm:113
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        float update_context = FALSE;
        if (Dm.truthy(cell_cover_open) && Dm.truthy(cell))
        {
            context[SCREENTIP_CONTEXT_CTRL_LMB] = "Remove cell";
            update_context = TRUE;
        }

        if (Dm.truthy(cell_cover_open) && !Dm.truthy(cell) && held_item is obj_item_stock_parts_power_store_cell)
        {
            context[SCREENTIP_CONTEXT_LMB] = "Install cell";
            update_context = TRUE;
        }

        if (Dm.eq(held_item?.tool_behaviour, TOOL_CROWBAR))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.text("[] battery panel", Dm.truthy(cell_cover_open) ? "close" : "open");
            update_context = TRUE;
        }

        if (Dm.truthy(update_context))
            return CONTEXTUAL_SCREENTIP_SET;
        return NONE;
    }

    // tg: code/game/objects/items/inspector.dm:131
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (Dm.truthy(cell_cover_open) || !Dm.truthy(cell))
            return NONE;
        if (isitem(target))
        {
            context[SCREENTIP_CONTEXT_LMB] = "Contraband Scan";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    /**
     * Scans the carbon or item for contraband.
     *
     * Arguments:
     * - scanned - what or who is scanned?
     * - user - who is performing the scanning?
     */
    // tg: code/game/objects/items/inspector.dm:146
    public virtual object contraband_scan(object scanned = null, object user = null)
    {
        if (iscarbon(scanned))
        {
            mob_living_carbon scanned_carbon = scanned as mob_living_carbon;
            foreach (obj_item content in Dm.each<obj_item>(scanned_carbon.get_all_contents_skipping_traits(TRAIT_CONTRABAND_BLOCKER)))
            {
                object contraband_content = content.is_contraband();
                if ((Dm.truthy(contraband_content) && Dm.truthy(scans_correctly)) || (!Dm.truthy(contraband_content) && !Dm.truthy(scans_correctly)))
                    return TRUE;
            }
        }

        if (isitem(scanned))
        {
            obj_item contraband_item = scanned as obj_item;
            object contraband_status = contraband_item.is_contraband();
            if ((Dm.truthy(contraband_status) && Dm.truthy(scans_correctly)) || (!Dm.truthy(contraband_status) && !Dm.truthy(scans_correctly)))
                return TRUE;
        }

        return FALSE;
    }
}
