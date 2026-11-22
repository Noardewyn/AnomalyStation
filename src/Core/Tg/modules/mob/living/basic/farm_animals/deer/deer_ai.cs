// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Munches grass with a happy little emote.
// tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:18
public partial class datum_bt_node_ai_behavior_hunt_target_deer_graze : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:22
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.manual_emote(pick("eats the grass!", "munches down the grass!", "chews on the grass!"));
        return null;
    }
}

/// Splashes happily in the water.
// tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:28
public partial class datum_bt_node_ai_behavior_hunt_target_deer_drink : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:32
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.manual_emote(pick("drinks the water!", "dances in the water!", "splashes around happily!"));
        return null;
    }
}

/// Claims a tree as home territory.
// tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:38
public partial class datum_bt_node_ai_behavior_hunt_target_deer_mark : datum_bt_node_ai_behavior_hunt_target
{
    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:42
    public override async DmTask<object> target_caught(mob_living hunter = null, atom hunted = null)
    {
        hunter.manual_emote(Dm.text("marks [] with its hooves!", hunted));
        (hunter.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_DEER_TREEHOME, hunted);
        return null;
    }
}

/// Rests by its home tree, then won't feel the need to rest again for a while.
// tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:49
public partial class datum_bt_node_ai_behavior_deer_rest : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:58
    public override async DmTask<object> setup(datum_ai_controller controller = null)
    {
        object dot = await base.setup(controller);
        timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(finish_action), controller, TRUE), rest_duration, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm", line: 60);
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:62
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        if (Dm.truthy(sleeping))
            return AI_BEHAVIOR_DELAY;
        (controller.pawn as mob_living).manual_emote(pick("rests its legs...", "yawns and naps...", "curls up and rests..."));
        sleeping = TRUE;
        return AI_BEHAVIOR_DELAY;
    }

    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:71
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        deltimer(timerid);
        timerid = null;
        sleeping = FALSE;
        controller.set_blackboard_key(BB_DEER_NEXT_REST_TIMER, world.time + rand(minimum_time, maximum_time));
    }
}

/// Plays with another deer, spinning around them.
// tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:81
public partial class datum_bt_node_ai_behavior_deer_play : datum_bt_node_ai_behavior
{
    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:85
    public override object perform(object seconds_per_tick = null, datum_ai_controller controller = null)
    {
        atom friend = controller.blackboard[friend_key] as atom;
        if (QDELETED(friend))
            return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_FAILED);
        mob_living living_pawn = controller.pawn as mob_living;
        living_pawn.manual_emote(Dm.text("[] []!", pick("plays with", "dances with", "celebrates with"), friend));
        living_pawn.spin(4f, 1f);
        return Dm.bitor(AI_BEHAVIOR_DELAY, AI_BEHAVIOR_SUCCEEDED);
    }

    // tg: code/modules/mob/living/basic/farm_animals/deer/deer_ai.dm:95
    public override async DmTask finish_action(datum_ai_controller controller = null, object succeeded = null)
    {
        await base.finish_action(controller, succeeded);
        controller.clear_blackboard_key(friend_key);
    }
}
