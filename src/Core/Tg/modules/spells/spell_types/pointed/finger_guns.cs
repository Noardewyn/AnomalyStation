// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/pointed/finger_guns.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the mime's finger guns, three mimed bullets that need empty hands. The var block is generated.

// tg: code/modules/spells/spell_types/pointed/finger_guns.dm:1
public partial class datum_action_cooldown_spell_pointed_projectile_finger_guns : datum_action_cooldown_spell_pointed_projectile
{
    // tg: code/modules/spells/spell_types/pointed/finger_guns.dm:29
    public override async DmTask<object> try_invoke(mob_living invoker = null, float? feedback = null)
    {
        feedback ??= TRUE;
        if (Dm.eq(invocation_type, INVOCATION_EMOTE))
        {
            if (!ishuman(invoker))
                return FALSE;

            mob_living_carbon_human human_invoker = (mob_living_carbon_human)invoker;
            if (Dm.truthy(human_invoker.incapacitated))
            {
                if (Dm.truthy(feedback))
                    to_chat(human_invoker, span_warning(I18n.T("You can't properly point your fingers while incapacitated.")));
                return FALSE;
            }
            if (Dm.truthy(human_invoker.get_active_held_item()))
            {
                if (Dm.truthy(feedback))
                    to_chat(human_invoker, span_warning(I18n.T("You can't properly fire your finger guns with something in your hand.")));
                return FALSE;
            }
        }

        return await base.try_invoke(invoker, feedback);
    }
}
