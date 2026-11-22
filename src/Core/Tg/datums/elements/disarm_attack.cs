// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/disarm_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the screentip (gaps J4, D298): shoving with a shield.

// tg: code/datums/elements/disarm_attack.dm:2
public partial class datum_element_disarm_attack : datum_element
{
    // tg: code/datums/elements/disarm_attack.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        obj_item item = target as obj_item;
        RegisterSignal(item, COMSIG_ITEM_ATTACK_SECONDARY, nameof(secondary_attack));
        RegisterSignal(item, COMSIG_ATOM_EXAMINE, nameof(examine));
        item.item_flags = Dm.num(Dm.bitor(item.item_flags, ITEM_HAS_CONTEXTUAL_SCREENTIPS));
        // COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET -> add_item_context: the screentip (a row)
        return null;
    }

    // tg: code/datums/elements/disarm_attack.dm:15
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ITEM_ATTACK_SECONDARY));
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/disarm_attack.dm:26
    [SignalHandler]
    public virtual object secondary_attack(obj_item source = null, mob_living victim = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(user.can_disarm(victim)) || !Dm.truthy(can_disarm_attack(source, victim, user)))
            return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
        if (Dm.truthy(Waiting.NoSleep(victim.check_block(source, 0, Dm.text("\\the []", source), MELEE_ATTACK, 0))))
            return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
        Waiting.Detach(user.disarm(victim, source));
        user.changeNext_move(Dm.num(Dm.truthy(source.secondary_attack_speed) ? source.secondary_attack_speed : source.attack_speed));
        return COMPONENT_SECONDARY_CANCEL_ATTACK_CHAIN;
    }

    ///check if the item conditions for the disarm action are met.
    // tg: code/datums/elements/disarm_attack.dm:37
    public virtual object can_disarm_attack(obj_item source = null, mob_living victim = null, mob_living user = null, object message = null)
    {
        message ??= TRUE;
        if (Dm.truthy(source._listen_lookup?[COMSIG_ITEM_CAN_DISARM_ATTACK]) && Dm.truthy(Dm.bitand(source._SendSignal(COMSIG_ITEM_CAN_DISARM_ATTACK, Dm.list(source, victim, user, message)), COMPONENT_BLOCK_ITEM_DISARM_ATTACK)))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/elements/disarm_attack.dm:42
    [SignalHandler]
    public virtual void examine(obj_item source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.truthy(can_disarm_attack(source, user as mob_living, user as mob_living, FALSE)))
            examine_list.Add(span_notice(I18n.T("You can use it to <b>shove</b> people with <b>right-click</b>.")));
    }
}
