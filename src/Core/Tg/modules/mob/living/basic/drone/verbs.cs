// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/verbs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file, the drone's two command verbs (GAME_VERB: the body procs and the verbs, as the ghosts' verbs).

public partial class mob_living_basic_drone : mob_living_basic
{
    /**
     * Echoes drone laws to the user
     *
     * See [/mob/living/basic/drone/var/laws]
     */
    // tg: code/modules/mob/living/basic/drone/verbs.dm:6  GAME_VERB(/mob/living/basic/drone, check_laws, "Check Laws", "Drone"): the body
    public virtual void __gvb_check_laws(DmList structured_args = null)
    {
        to_chat(this, "<b>Drone Laws</b>");
        to_chat(this, laws);
    }

    // tg: code/modules/mob/living/basic/drone/verbs.dm:6  the verb (called as a proc by Login): a call of the body after SSverbs.invoke_verb's check
    public virtual void check_laws(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        __gvb_check_laws(Dm.list());
    }

    /**
     * Creates an alert to drones in the same network
     *
     * Prompts user for alert level of:
     * * Low
     * * Medium
     * * High
     * * Critical
     *
     * Attaches area name to message
     */
    // tg: code/modules/mob/living/basic/drone/verbs.dm:24  GAME_VERB(/mob/living/basic/drone, drone_ping, "Drone ping", "Drone"): the body
    public virtual async DmTask __gvb_drone_ping(DmList structured_args = null)
    {
        object alert_s = await tgui_input_list(this, I18n.T("Alert severity level"), I18n.T("Drone ping"), Dm.list("Low", "Medium", "High", "Critical")); // input(...) as null|anything in list(...)

        var A = get_area(loc) as area;

        if (Dm.truthy(alert_s) && A != null && stat != DEAD)
        {
            string msg = span_big(I18n.T("DRONE PING: []: [] priority alert in []!", name, alert_s, A.name));
            alert_drones(msg);
        }
    }

    // tg: code/modules/mob/living/basic/drone/verbs.dm:24  the verb: a detached call of the body after SSverbs.invoke_verb's check (the body sleeps in the prompt)
    public virtual void drone_ping(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_drone_ping(Dm.list()));
    }
}
