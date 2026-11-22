// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/snails/snail_ability.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/snails/snail_ability.dm:1
public partial class datum_action_cooldown_mob_cooldown_shell_retreat : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/snails/snail_ability.dm:9
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        if (HAS_TRAIT(owner, TRAIT_SHELL_RETREATED))
            unretreat_from_shell();
        else
            retreat_into_shell();
        return dot;
    }

    // tg: code/modules/mob/living/basic/snails/snail_ability.dm:13
    [SignalHandler]
    public virtual void unretreat_from_shell()
    {
        owner.visible_message(
            span_danger(Dm.text("[] slowly pops its head out of its shell!", owner)),
            span_userdanger("You pop your head out of your shell.")
        );
        REMOVE_TRAIT(owner, TRAIT_SHELL_RETREATED, REF(this));
        UnregisterSignal(owner, Dm.list(COMSIG_MOVABLE_ATTEMPTED_MOVE, COMSIG_LIVING_DEATH));
        owner.update_appearance(UPDATE_ICON_STATE);
    }

    // tg: code/modules/mob/living/basic/snails/snail_ability.dm:22
    public virtual void retreat_into_shell()
    {
        owner.visible_message(
            span_danger(Dm.text("[] quickly escapes into its shell!", owner)),
            span_userdanger("You hide in your shell..")
        );
        RegisterSignals(owner, Dm.list(COMSIG_LIVING_DEATH, COMSIG_MOVABLE_ATTEMPTED_MOVE), nameof(unretreat_from_shell));
        ADD_TRAIT(owner, TRAIT_SHELL_RETREATED, REF(this));
        owner.update_appearance(UPDATE_ICON_STATE);
    }
}
