// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/alien/maid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the xenomorph maid, who cleans what she walks over and polishes what she attacks. The barmaid (:36-62, the escape
// shuttle's, with its area-based godmode) waits for that component (not-translated.md). The var blocks are generated.

// tg: code/modules/mob/living/basic/alien/maid.dm:1
public partial class mob_living_basic_alien_maid : mob_living_basic_alien
{
    // tg: code/modules/mob/living/basic/alien/maid.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_cleaning)));
        return dot;
    }

    ///Handles the maid attacking other players, cancelling the attack to clean up instead.
    // tg: code/modules/mob/living/basic/alien/maid.dm:20
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        await target.wash(CLEAN_SCRUB);
        if (target is obj_effect_decal_cleanable)
            visible_message(span_notice(I18n.T("[] cleans up \\the [].", this, target)));
        else
            visible_message(span_notice(I18n.T("[] polishes \\the [].", this, target)));
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }
}
