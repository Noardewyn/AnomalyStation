// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/swapper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The quantum spin inverter: two linked by using one on the other (their "-linked" picture), one
// activated buzzes both and two and a half seconds later swaps the two holders' places (do_teleport on the quantum channel, with tg's
// two swap sounds); thirty seconds to recharge; the link broken by Alt-click. The var block is generated (D111).

// tg: code/game/objects/items/devices/swapper.dm:1
public partial class obj_item_swapper : obj_item
{
    // tg: code/game/objects/items/devices/swapper.dm:20
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(linked_swapper))
        {
            linked_swapper.linked_swapper = null; //*inception music*
            linked_swapper.update_appearance();
            linked_swapper = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/devices/swapper.dm:27
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("swapper[]", Dm.truthy(linked_swapper) ? "-linked" : null);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/items/devices/swapper.dm:31
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_swapper)
            return NONE;
        obj_item_swapper other_swapper = (obj_item_swapper)tool;
        if (Dm.truthy(other_swapper.linked_swapper))
        {
            to_chat(user, span_warning(I18n.T("[] is already linked. Break the current link to establish a new one.", other_swapper)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(linked_swapper))
        {
            to_chat(user, span_warning(I18n.T("[] is already linked. Break the current link to establish a new one.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You establish a quantum link between the two devices.")));
        linked_swapper = other_swapper;
        other_swapper.linked_swapper = this;
        update_appearance();
        linked_swapper.update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/swapper.dm:48
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (world.time < next_use)
        {
            to_chat(user, span_warning(I18n.T("[] is still recharging.", this)));
            return null;
        }
        if (QDELETED(linked_swapper))
        {
            to_chat(user, span_warning(I18n.T("[] is not linked with another swapper.", this)));
            return null;
        }
        playsound(this, Dm.resource("sound/items/weapons/flash.ogg"), 25, TRUE);
        to_chat(user, span_notice(I18n.T("You activate [].", this)));
        playsound(linked_swapper, Dm.resource("sound/items/weapons/flash.ogg"), 25, TRUE);
        if (ismob(linked_swapper.loc))
        {
            mob holder = (mob)linked_swapper.loc;
            to_chat(holder, span_notice(I18n.T("[] starts buzzing.", linked_swapper)));
        }
        next_use = world.time + cooldown; //only the one used goes on cooldown
        _addtimer(Dm.@new<datum_callback>(this, nameof(swap), user), 2.5f * SECONDS, file: "code/game/objects/items/devices/swapper.dm", line: 62);
        return null;
    }

    // tg: code/game/objects/items/devices/swapper.dm:64
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (world.time < next_use)
            Dm.append(ref dot, span_warning(I18n.T("Time left to recharge: [].", DisplayTimeText(next_use - world.time))));
        if (Dm.truthy(linked_swapper))
            Dm.append(ref dot, span_notice(I18n.T("<b>Linked.</b> Alt-Click to break the quantum link.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("<b>Not Linked.</b> Use on another quantum spin inverter to establish a quantum link.")));
        return dot;
    }

    // tg: code/game/objects/items/devices/swapper.dm:73
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        to_chat(user, span_notice(I18n.T("You break the current quantum link.")));
        if (!QDELETED(linked_swapper))
        {
            linked_swapper.linked_swapper = null;
            linked_swapper.update_appearance();
            linked_swapper = null;
        }
        update_appearance();
        return CLICK_ACTION_SUCCESS;
    }

    /**
     * Swaps two atoms following the activation of a swapper item.
     * If a mob is holding a swapper, it will carry the mob as-per the rules of do_teleport().
     */
    // tg: code/game/objects/items/devices/swapper.dm:86
    public virtual async DmTask swap(mob user = null)
    {
        if (QDELETED(linked_swapper) || linked_swapper.loc == null || world.time < linked_swapper.cooldown)
            return;

        atom_movable container_A = get_loose_container(this);
        atom_movable container_B = get_loose_container(linked_swapper);
        object target_A = container_A.drop_location();
        object target_B = container_B.drop_location();

        playsound(target_A as atom, Dm.resource("sound/effects/swapper/swap_a.ogg"), 30, TRUE);
        playsound(target_B as atom, Dm.resource("sound/effects/swapper/swap_b.ogg"), 30, TRUE);
        if (Dm.truthy(await do_teleport(container_A, target_B as atom, channel: TELEPORT_CHANNEL_QUANTUM)))
        {
            await do_teleport(container_B, target_A as atom, channel: TELEPORT_CHANNEL_QUANTUM);
            if (ismob(container_B))
            {
                mob swapped_mob = (mob)container_B;
                to_chat(swapped_mob, span_warning(I18n.T("[] activates, and you find yourself somewhere else.", linked_swapper)));
            }
        }
    }
}
