// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/goose/goose.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2; the deadchat control since gaps E1a).

/// A mob that gets mad at people at random and tries to eat nearby objects
// tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:6
public partial class mob_living_basic_goose : mob_living_basic
{
    /// The most random things a goose can have inside of it
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:2
    protected const int GOOSE_SATIATED = 50;

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:34
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_content_barfer)));
        Waiting.NoSleep(AddElement(typeof(datum_element_basic_eating), null, null, null, null, conserve_food, null, string_list(Dm.list(typeof(obj_item)))));

        RegisterSignal(this, COMSIG_MOB_PRE_EAT, nameof(on_tried_gobbling));
        RegisterSignal(this, COMSIG_MOB_ATE, nameof(on_gobbled));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:43
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Dm.truthy(gibbed) && Dm.truthy(length(contents)))
        {
            if (drop_location() is turf drop_turf)
            {
                playsound(drop_turf, Dm.resource("sound/effects/splat.ogg"), 50, TRUE);
                await drop_turf.add_vomit_floor(this);
            }
        }
        return await base.death(gibbed, __extra);
    }

    /// Called when we try to eat something
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:52
    [SignalHandler]
    public virtual object on_tried_gobbling(datum source = null, obj_item potential_food = null)
    {
        if (Dm.truthy((ai_controller as datum_ai_controller)?.blackboard[BB_GOOSE_PANICKED]))
            return COMSIG_MOB_CANCEL_EAT;
        if (Dm.truthy(Waiting.NoSleep(potential_food.has_material_type(typeof(datum_material_plastic)))) || IS_EDIBLE(potential_food))
            return NONE; // Geese only eat FOOD or PLASTIC
        return COMSIG_MOB_CANCEL_EAT;
    }

    /// Called when we've eaten something
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:61
    [SignalHandler]
    public virtual object on_gobbled(atom source = null, obj_item food = null, mob feeder = null)
    {
        // The eaten item isn't always destroyed (see conserve_food), so make sure the AI
        // forgets about it instead of trying to eat the same swallowed item forever.
        (ai_controller as datum_ai_controller)?.clear_blackboard_key(BB_TARGET_FOOD);
        if (!Dm.truthy(Waiting.NoSleep(food.has_material_type(typeof(datum_material_plastic)))))
            return NONE;

        visible_message(span_boldwarning(Dm.text("[] is choking on \\the []!", this, food)));
        Waiting.NoSleep(food.forceMove(this));
        choke(food);

        return COMSIG_MOB_TERMINATE_EAT;
    }

    /// Start choking on something we just ate
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:76
    public virtual void choke(obj_item not_food_after_all = null)
    {
        Waiting.NoSleep(apply_status_effect(typeof(datum_status_effect_goose_choking)));
    }
}

/// A less grumpy but much grosser variant of the goose, who will decorate the halls in their own special way
// tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:81
public partial class mob_living_basic_goose_vomit : mob_living_basic_goose
{
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:94
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        vomit_action = Dm.@new<datum_action_cooldown_mob_cooldown_goose_vomit>(this);
        Waiting.NoSleep(vomit_action.Grant(this));
        RegisterSignal(this, COMSIG_MOB_ABILITY_STARTED, nameof(on_started_vomiting));

        // 5% chance every round to have anarchy mode deadchat control on birdboat.
        if (!prob(5))
            return dot;
        desc = Dm.text("[] It's waddling more than usual. It seems to be possessed.", initial(this, "desc"));
        Waiting.NoSleep(deadchat_plays());
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:107
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        qdel(vomit_action);
        vomit_action = null;
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:111
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        object vomit_chance = (ai_controller as datum_ai_controller)?.blackboard[BB_GOOSE_VOMIT_CHANCE];
        if (prob(vomit_chance == null ? 0 : Dm.num(vomit_chance)))
            await vomit();
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:117
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice("Somehow, it still looks hungry."));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:121
    public override object on_gobbled(atom source = null, obj_item food = null, mob feeder = null)
    {
        if (length(contents) > GOOSE_SATIATED)
        {
            if (eat_fail_feedback_cooldown <= world.time) // COOLDOWN_FINISHED, inline (rules 11.1)
            {
                if (feeder != null)
                    visible_message(span_notice(Dm.text("[] looks too full to eat \\the []!", this, food)));
                eat_fail_feedback_cooldown = world.time + 5 * SECONDS;
            }
            return COMSIG_MOB_TERMINATE_EAT;
        }

        object dot = base.on_gobbled(source, food, feeder);
        if (Dm.eq(dot, COMSIG_MOB_TERMINATE_EAT))
            return dot; // It's plastic, which we already swallowed whole. If it's not plastic we already filtered it for edible

        // This also increases my vomit chance, but we atomised this field to the inside of a component and I need to read it
        datum_component_edible edible = food.GetComponent(typeof(datum_component_edible)) as datum_component_edible;
        if (edible != null && Dm.truthy(Dm.bitand(edible.foodtypes, GROSS)))
        {
            (ai_controller as datum_ai_controller)?.add_blackboard_key(BB_GOOSE_VOMIT_CHANCE, 3);
            if (vomit_action != null)
                vomit_action.extra_duration += 0.2f * SECONDS;
        }
        else
            (ai_controller as datum_ai_controller)?.add_blackboard_key(BB_GOOSE_VOMIT_CHANCE, 1);
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:141
    public override void choke(obj_item not_food_after_all = null)
    {
        if (prob(75))
        {
            base.choke(not_food_after_all);
            return;
        }
        visible_message(span_warning(Dm.text("[] is gagging on \\the []!", this, not_food_after_all)));
        manual_emote("gags!");
        _addtimer(Dm.@new<datum_callback>(this, nameof(vomit)), 5 * SECONDS, file: "code/modules/mob/living/basic/farm_animals/goose/goose.dm", line: 147);
    }

    /// Start making a mess
    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:149
    public virtual async DmTask vomit()
    {
        if (vomit_action != null)
            await vomit_action.Trigger(null, null, this);
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:152
    [SignalHandler]
    public virtual void on_started_vomiting(mob_living owner = null, datum_action_cooldown activated = null)
    {
        if (activated != vomit_action)
            return;
        remove_status_effect(typeof(datum_status_effect_goose_choking)); // We're going to cough it out
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:158
    public virtual void stop_deadchat_plays()
    {
        (ai_controller as datum_ai_controller)?.clear_blackboard_key(BB_DISABLE_IDLE);
    }

    // tg: code/modules/mob/living/basic/farm_animals/goose/goose.dm:161
    public override async DmTask<object> deadchat_plays(float? mode = null, float? cooldown = null)
    {
        mode ??= ANARCHY_MODE;
        cooldown ??= 12 * SECONDS;
        DmList goose_inputs = Dm.list(
            Dm.kv("vomit", Dm.@new<datum_callback>(this, nameof(vomit))),
            Dm.kv("honk", Dm.@new<datum_callback>(this, nameof(say), "HONK!!!")),
            Dm.kv("spin", Dm.@new<datum_callback>(this, nameof(emote), "spin")));

        object dot = await AddComponent(typeof(datum_component_deadchat_control_cardinal_movement), mode, goose_inputs, cooldown, Dm.@new<datum_callback>(this, nameof(stop_deadchat_plays)));

        if (Dm.eq(dot, COMPONENT_INCOMPATIBLE))
            return dot;

        // Stop automated movement, retain the other behaviour so you can lead the horse to plastic and have it drink.
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_DISABLE_IDLE, TRUE);
        return dot;
    }
}
