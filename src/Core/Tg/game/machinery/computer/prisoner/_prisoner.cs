// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/computer/prisoner/_prisoner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps LC1 (D333's station order: security and command consoles; lavaland.md L3's labour camp): the whole file. The prisoner
// consoles' base: the prisoner ID slot (insert by hand or from the window, eject by alt-click or the window). The var block is
// generated.

using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/game/machinery/computer/prisoner/_prisoner.dm:1
public partial class obj_machinery_computer_prisoner : obj_machinery_computer
{
    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:8
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (contained_id != null)
            await contained_id.forceMove(drop_location() as atom);
        return null;
    }

    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:11
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref contained_id);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:15
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone != null && ReferenceEquals(gone, contained_id))
            contained_id = null;
        return dot;
    }

    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:20
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (contained_id != null)
            Dm.append(ref dot, span_notice(I18n.T("<b>Alt-click</b> to eject the ID card.")));
        return dot;
    }

    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:25
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await id_eject(user);
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Called when the prison teleporter computer attempts to insert a new ID card, that will be assigned to the prisoner if successful.
     * user: The mob user of the computer.
     * new_id: The ID that we're trying to insert.
     */
    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:34
    public virtual async DmTask id_insert(mob user, object new_id = null)
    {
        if (new_id == null)
            new_id = user.get_active_held_item();
        if (new_id is not obj_item_card_id_advanced_prisoner prisoner_id)
            return;
        if (contained_id != null)
        {
            balloon_alert(user, I18n.T("no empty slot!"));
            return;
        }
        if (!Dm.truthy(await user.transferItemToLoc(prisoner_id, this)))
            return;
        contained_id = prisoner_id;
        balloon_alert_to_viewers(I18n.T("id inserted"));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);
    }

    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:47
    public virtual async DmTask id_eject(mob user)
    {
        if (contained_id == null)
        {
            balloon_alert(user, I18n.T("no id!"));
            return;
        }

        if (!issilicon(user) && Dm.truthy(await Adjacent(user)))
            await user.put_in_hands(contained_id);
        else
            await contained_id.forceMove(drop_location() as atom);

        balloon_alert_to_viewers(I18n.T("id ejected"));
        playsound(this, Dm.resource("sound/machines/terminal/terminal_insert_disc.ogg"), 50, FALSE);
    }

    // tg: code/game/machinery/computer/prisoner/_prisoner.dm:60
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_card_id_advanced_prisoner)
            return NONE;
        await id_insert(user, tool);
        return ITEM_INTERACT_SUCCESS;
    }
}
