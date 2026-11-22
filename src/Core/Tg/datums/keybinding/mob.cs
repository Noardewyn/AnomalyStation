// Translated from tgstation (snapshot 1d99bd7f): code/datums/keybinding/mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the M4a bindings' procs (swap_hands, select_hand, activate_inhand, drop_item: M4a P2) and stop_pulling (pulling,
// 2026-10-15); drop_item_specific, prevent_movement, target and look up/down come with their mechanics (M9, M6, multi-z). The var
// blocks are generated (types-converter.md 3.2).

// tg: code/datums/keybinding/mob.dm:12  (pulling, 2026-10-15)
public partial class datum_keybinding_mob_stop_pulling : datum_keybinding_mob
{
    // tg: code/datums/keybinding/mob.dm:12
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob M = user.mob;
        if (!Dm.truthy(M.pulling))
            to_chat(user, span_notice(I18n.T("You are not pulling anything.")));
        else
            await M.stop_pulling();
        return TRUE;
    }
}

// tg: code/datums/keybinding/mob.dm:30
public partial class datum_keybinding_mob_swap_hands : datum_keybinding_mob
{
    // tg: code/datums/keybinding/mob.dm:30
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob M = user.mob;
        M.swap_hand();
        return TRUE;
    }
}

// tg: code/datums/keybinding/mob.dm:55
public partial class datum_keybinding_mob_select_hand : datum_keybinding_mob
{
    // tg: code/datums/keybinding/mob.dm:55
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob user_mob = user.mob;
        float active_hand_set = ceil(user_mob.active_hand_index / 2) - 1; //offset
        float desired_hand_index = Dm.num(hand_index) + (2 * active_hand_set);
        user_mob.swap_hand(desired_hand_index);
        return TRUE;
    }
}

// tg: code/datums/keybinding/mob.dm:75
public partial class datum_keybinding_mob_activate_inhand : datum_keybinding_mob
{
    // tg: code/datums/keybinding/mob.dm:75
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob M = user.mob;
        M.mode__proc();
        return TRUE;
    }
}

// tg: code/datums/keybinding/mob.dm:90
public partial class datum_keybinding_mob_drop_item : datum_keybinding_mob
{
    // tg: code/datums/keybinding/mob.dm:90
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        if (iscyborg(user.mob)) //cyborgs can't drop items
            return FALSE;
        mob user_mob = user.mob;
        obj_item item_dropped = user_mob.get_active_held_item() as obj_item;
        if (!Dm.truthy(item_dropped))
        {
            to_chat(user, span_warning(I18n.T("You have nothing to drop in your hand!")));
            return TRUE;
        }
        await user.mob.dropItemToGround(item_dropped);
        return TRUE;
    }
}
