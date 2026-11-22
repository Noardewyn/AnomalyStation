// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/cult/constructs/proteon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C1).

// tg: code/modules/mob/living/basic/cult/constructs/proteon.dm:18
public partial class mob_living_basic_construct_proteon_hostile : mob_living_basic_construct_proteon
{
    // tg: code/modules/mob/living/basic/cult/constructs/proteon.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        datum_callback retaliate_callback = Dm.@new<datum_callback>(this, nameof(ai_retaliate_behaviour));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), retaliate_callback));
        return dot;
    }

    /// Set a timer to clear our retaliate list
    // tg: code/modules/mob/living/basic/cult/constructs/proteon.dm:30
    public virtual void ai_retaliate_behaviour(mob_living attacker = null)
    {
        if (attacker == null)
            return;
        float random_timer = rand(2 * SECONDS, 4 * SECONDS); //for unpredictability
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_retaliate_list)), random_timer, file: "code/modules/mob/living/basic/cult/constructs/proteon.dm", line: 34);
    }

    // tg: code/modules/mob/living/basic/cult/constructs/proteon.dm:36
    public virtual void clear_retaliate_list()
    {
        datum_ai_controller controller = ai_controller as datum_ai_controller;
        if (!Dm.truthy(controller.blackboard_key_exists(BB_BASIC_MOB_RETALIATE_LIST)))
            return;
        controller.clear_blackboard_key(BB_BASIC_MOB_RETALIATE_LIST);
    }
}
