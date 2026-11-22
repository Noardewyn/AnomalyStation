// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/verbs/typing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M9 slice A): the indicator procs of the say window. The command bar spy (initialize_commandbar_spy,
// handle_commandbar_typing: BYOND's input bar typing a say verb) has no counterpart: the port's client has no BYOND command bar.

public partial class client : datum
{
    /** Sets the mob as "thinking" - with indicator and the TRAIT_THINKING_IN_CHARACTER trait */
    // tg: code/modules/client/verbs/typing.dm:32
    public virtual object start_thinking()
    {
        if (!Dm.truthy(typing_indicators))
            return FALSE;
        /// Special exemptions
        if (HAS_MIND_TRAIT(mob, TRAIT_HIDE_THINKING_INDICATOR))
            return FALSE;
        ADD_TRAIT(mob, TRAIT_THINKING_IN_CHARACTER, CURRENTLY_TYPING_TRAIT);
        mob.create_thinking_indicator();
        return null;
    }

    /** Removes typing/thinking indicators and flags the mob as not thinking */
    // tg: code/modules/client/verbs/typing.dm:42
    public virtual object stop_thinking()
    {
        mob?.remove_all_indicators();
        return null;
    }

    /**
     * Handles the user typing. After a brief period of inactivity,
     * signals the client mob to revert to the "thinking" icon.
     */
    // tg: code/modules/client/verbs/typing.dm:49
    public virtual object start_typing()
    {
        mob client_mob = mob;
        client_mob.remove_thinking_indicator();
        if (!Dm.truthy(typing_indicators) || !HAS_TRAIT(client_mob, TRAIT_THINKING_IN_CHARACTER))
            return FALSE;
        client_mob.create_typing_indicator();
        _addtimer(Dm.@new<datum_callback>(this, nameof(stop_typing)), 5 * SECONDS, Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), file: "code/modules/client/verbs/typing.dm", line: 55);
        return null;
    }

    /**
     * Callback to remove the typing indicator after a brief period of inactivity.
     * If the user was typing IC, the thinking indicator is shown.
     */
    // tg: code/modules/client/verbs/typing.dm:61
    public virtual object stop_typing()
    {
        if (mob == null)
            return FALSE;
        mob client_mob = mob;
        client_mob.remove_typing_indicator();
        if (!Dm.truthy(typing_indicators) || !HAS_TRAIT(client_mob, TRAIT_THINKING_IN_CHARACTER))
            return FALSE;
        client_mob.create_thinking_indicator();
        return null;
    }
}
