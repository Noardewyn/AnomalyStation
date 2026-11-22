// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/skill_reward.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file: the skill capes, the golden boxing gloves and the fishing hat are worn only by a legend of their
// skill (the veteran's cloak by a player of five thousand hours). The var blocks are generated (D111).

///An element that forbids mobs without a required skill level from equipping the item.
// tg: code/datums/elements/skill_reward.dm:2
public partial class datum_element_skill_reward : datum_element
{
    // tg: code/datums/elements/skill_reward.dm:8
    [DmParams("target", "associated_skill")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object associated_skill = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        this.associated_skill = associated_skill;
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        RegisterSignal(target, COMSIG_ITEM_POST_EQUIPPED, nameof(drop_if_unworthy));
        return dot;
    }

    // tg: code/datums/elements/skill_reward.dm:18
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("You notice a powerful aura about this item, suggesting that only the truly experienced may wield it.")));
    }

    // tg: code/datums/elements/skill_reward.dm:22
    [SignalHandler]
    public virtual object on_attack_hand(datum source = null, mob_living user = null, DmList modifiers = null)
    {
        if (!Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)) && !Dm.truthy(check_equippable(user))) //Allows other players to drag it around at least.
        {
            to_chat(user, span_warning(I18n.T("You feel completely and utterly unworthy to even touch \\the [].", source)));
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        }
        return NONE;
    }

    ///We check if the item can be equipped, otherwise we drop it.
    // tg: code/datums/elements/skill_reward.dm:30
    [SignalHandler]
    public virtual object drop_if_unworthy(datum source = null, mob_living user = null)
    {
        if (Dm.truthy(check_equippable(user)) || !Dm.@in(source, user.get_equipped_items(Dm.bitor(INCLUDE_POCKETS, INCLUDE_ACCESSORIES))))
            return NONE;
        to_chat(user, span_warning(I18n.T("You feel completely and utterly unworthy to even touch \\the [].", source)));
        Waiting.NoSleep(user.dropItemToGround(source as obj_item, TRUE));
        return COMPONENT_EQUIPPED_FAILED;
    }

    // tg: code/datums/elements/skill_reward.dm:38
    public virtual object check_equippable(mob_living user = null)
    {
        return Dm.num(Dm.ge(user.mind?.get_skill_level(associated_skill), SKILL_LEVEL_LEGENDARY));
    }
}

/**
 * Welp, the code is pretty much the same, except for one tiny detail, I suppose it's ok to make a subtype of this element.
 * That tiny detail is that we don't check for skills, but if the player has played for thousands of hours.
 */
// tg: code/datums/elements/skill_reward.dm:45
public partial class datum_element_skill_reward_veteran : datum_element_skill_reward
{
    // tg: code/datums/elements/skill_reward.dm:48
    public override object check_equippable(mob_living user = null)
    {
        return user.client?.is_veteran();
    }
}
