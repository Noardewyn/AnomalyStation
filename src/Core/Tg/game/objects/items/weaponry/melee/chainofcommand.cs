// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/weaponry/melee/chainofcommand.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the speed boost of datums/status_effects/buffs.dm. The captain's chain of command: a whip
// that, on the right button, hastens whom it lashes (not its wielder): tg's whip crack and the Commanded effect for seven seconds
// (a fifth faster, its alert); its suicide strangles. The liz and cat o' nine tails only change vars. The screentip of its right
// button is a context tip (D291: screentips are deferred). The var blocks are generated (D111).

// tg: code/game/objects/items/weaponry/melee/chainofcommand.dm:1
public partial class obj_item_melee_chainofcommand : obj_item_melee
{
    // tg: code/game/objects/items/weaponry/melee/chainofcommand.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_item_context();
        return dot;
    }

    // tg: code/game/objects/items/weaponry/melee/chainofcommand.dm:28
    public override object add_item_context(obj_item source = null, DmList context = null, atom target = null, mob_living user = null)
    {
        if (isliving(target))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Hasten";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return NONE;
    }

    // tg: code/game/objects/items/weaponry/melee/chainofcommand.dm:34
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is strangling []self with []! It looks like [] trying to commit suicide!", user, user.p_them(), this, user.p_theyre())));
        await DmTask.CompletedTask;
        return OXYLOSS;
    }

    // tg: code/game/objects/items/weaponry/melee/chainofcommand.dm:38
    public override async DmTask<object> attack_secondary(mob_living victim = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        object dot = await base.attack_secondary(victim, user, modifiers, attack_modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (victim == user)
        {
            to_chat(user, span_warning(I18n.T("You consider lashing yourself, but hesitate at the thought of how much it would hurt.")));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        await user.do_attack_animation(victim);
        playsound(victim, Dm.resource("sound/items/weapons/whip.ogg"), 50, TRUE, -1);
        await victim.apply_status_effect(typeof(datum_status_effect_speed_boost_commanded));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }
}
