// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// Parrot behavior that perches them on their current perch target.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:8
public partial class datum_bt_node_ai_behavior_perch_on_target : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:10
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom target = controller.blackboard[target_key] as atom;
        if (QDELETED(target))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        mob_living_basic_parrot living_pawn = controller.pawn as mob_living_basic_parrot;

        if (ishuman(target) && !Dm.truthy(check_human_conditions(target as mob_living)))
            return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_FAILED;

        Waiting.Detach(living_pawn.start_perching(target));
        return AI_BEHAVIOR_DELAY | AI_BEHAVIOR_SUCCEEDED;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:23
    public virtual object check_human_conditions(mob_living living_human = null)
    {
        if (living_human.stat == DEAD || LAZYLEN(living_human.buckled_mobs) >= Dm.num(living_human.max_buckled_mobs))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:28
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(target_key);
    }
}

/// Variant for the ghost parrot, which can haunt any living human.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:35
public partial class datum_bt_node_ai_behavior_perch_on_target_haunt : datum_bt_node_ai_behavior_perch_on_target
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:35
    public override object check_human_conditions(mob_living living_human = null)
    {
        return living_human.stat != DEAD ? TRUE : FALSE;
    }
}

/// Parrot idle wander; sits much more still while perched.
// tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:41
public partial class datum_bt_node_ai_behavior_idle_random_walk_parrot : datum_bt_node_ai_behavior_idle_random_walk
{
    // tg: code/modules/mob/living/basic/pets/parrot/parrot_ai/parrot_perching.dm:43
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        walk_chance = HAS_TRAIT(controller.pawn, TRAIT_PARROT_PERCHED) ? walk_chance_when_perched : Dm.num(initial(this, "walk_chance"));
        return base.perform(seconds_per_tick, controller);
    }
}
