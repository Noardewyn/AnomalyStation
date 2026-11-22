// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/udder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4). The gutlunch and raptor udders run with their hosts.

/**
 * Udder component; for farm animals to generate milk.
 *
 * Used for cows, goats, gutlunches. neat!
 */
// tg: code/datums/components/udder.dm:6
public partial class datum_component_udder : datum_component
{
    //udder_type and reagent_produced_typepath are typepaths, not reference
    // tg: code/datums/components/udder.dm:13
    [DmParams("udder_type", "on_milk_callback", "on_generate_callback", "reagent_produced_override")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object udder_type = Dm.arg(__extra, 0) ?? typeof(obj_item_udder);
        if (!isliving(parent)) //technically is possible to drop this on carbons... but you wouldn't do that to me, would you?
            return COMPONENT_INCOMPATIBLE;
        udder = Dm.@new((Type)udder_type, (object)null) as obj_item_udder;
        await udder.add_features(parent, Dm.arg(__extra, 2) as datum_callback, Dm.arg(__extra, 3));
        this.on_milk_callback = Dm.arg(__extra, 1) as datum_callback;
        return null;
    }

    // tg: code/datums/components/udder.dm:20
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));
    }

    // tg: code/datums/components/udder.dm:24
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        QDEL_NULL(ref udder);
        on_milk_callback = null;
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_ATTACKBY));
    }

    ///signal called on parent being examined
    // tg: code/datums/components/udder.dm:30
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        mob_living milked = parent as mob_living;
        if (IS_UNCONSCIOUS_OR_CRIT(milked))
            return; //come on now

        float udder_filled_percentage = Dm.num((udder.reagents as datum_reagents).total_volume) / Dm.num((udder.reagents as datum_reagents).maximum_volume) * 100;
        if (udder_filled_percentage >= 0 && udder_filled_percentage <= 10)
            Dm.append(ref examine_list, span_notice(Dm.text("[]'s [] is dry.", parent, udder.name)));
        else if (udder_filled_percentage >= 11 && udder_filled_percentage <= 99)
            Dm.append(ref examine_list, span_notice(Dm.text("[]'s [] can be milked if you have something to contain it.", parent, udder.name)));
        else if (udder_filled_percentage == 100)
            Dm.append(ref examine_list, span_notice(Dm.text("[]'s [] is round and full, and can be milked if you have something to contain it.", parent, udder.name)));
    }

    ///signal called on parent being attacked with an item
    // tg: code/datums/components/udder.dm:48
    [SignalHandler]
    public virtual object on_attackby(datum source = null, obj_item milking_tool = null, mob user = null)
    {
        mob_living milked = parent as mob_living;
        if (!IS_UNCONSCIOUS_OR_CRIT(milked) && milking_tool is obj_item_reagent_containers_cup milk_holder)
        {
            Waiting.NoSleep(udder.milk(milk_holder, user));
            if (on_milk_callback != null)
                Waiting.NoSleep(on_milk_callback.Invoke((udder.reagents as datum_reagents).total_volume, (udder.reagents as datum_reagents).maximum_volume));
            return COMPONENT_NO_AFTERATTACK;
        }
        return null;
    }
}

/**
 * # udder item
 *
 * Abstract item that is held in nullspace and manages reagents. Created by udder component.
 */
// tg: code/datums/components/udder.dm:64
public partial class obj_item_udder : obj_item
{
    // tg: code/datums/components/udder.dm:83
    public virtual async DmTask add_features(object parent = null, datum_callback callback = null, object reagent_override = null)
    {
        udder_mob = parent as mob_living;
        on_generate_callback = callback;
        create_reagents(size, REAGENT_HOLDER_ALIVE);
        if (Dm.truthy(reagent_override))
            reagent_produced_typepath = reagent_override as Type;
        await initial_conditions();
        if (require_consume_type == null)
            return;
        RegisterSignal(udder_mob, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_mob_consume));
        RegisterSignal(udder_mob, COMSIG_ATOM_ATTACKBY, nameof(on_mob_feed));
        (udder_mob.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_CHECK_HUNGRY, TRUE);
    }

    // tg: code/datums/components/udder.dm:96
    [SignalHandler]
    public virtual object on_mob_consume(datum source = null, atom feed = null)
    {
        if (!Dm.istype(feed, require_consume_type))
            return null;
        Waiting.Detach(handle_consumption(feed as atom_movable));
        return COMPONENT_HOSTILE_NO_ATTACK;
    }

    // tg: code/datums/components/udder.dm:104
    [SignalHandler]
    public virtual object on_mob_feed(datum source = null, atom used_item = null, mob_living user = null)
    {
        if (!Dm.istype(used_item, require_consume_type))
            return null;
        Waiting.Detach(handle_consumption(used_item as atom_movable, user));
        return COMPONENT_NO_AFTERATTACK;
    }

    // tg: code/datums/components/udder.dm:112
    public virtual async DmTask handle_consumption(atom_movable food = null, mob user = null)
    {
        if (Dm.locate_type(food.GetType(), this) != null)
        {
            if (user != null)
                user.balloon_alert(user, "already full!");
            return;
        }
        playsound(udder_mob.loc, Dm.resource("sound/items/eatfood.ogg"), 50, TRUE);
        udder_mob.visible_message(span_notice(Dm.text("[] gobbles up []!", udder_mob, food)), span_notice(Dm.text("You gobble up []!", food)));
        atom_movable final_food = food;
        if (isstack(food)) //if stack, only consume 1
        {
            obj_item_stack food_stack = food as obj_item_stack;
            final_food = await food_stack.split_stack(1) as atom_movable;
        }
        await final_food.forceMove(this);
    }

    // tg: code/datums/components/udder.dm:125
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (!Dm.istype(arrived, require_consume_type))
            return await base.Entered(arrived, old_loc, old_locs);

        (udder_mob.ai_controller as datum_ai_controller)?.set_blackboard_key(hunger_key, FALSE);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), arrived), require_consume_timer, file: "code/datums/components/udder.dm", line: 130);
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/datums/components/udder.dm:133
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (!Dm.istype(gone, require_consume_type))
            return dot;
        (udder_mob.ai_controller as datum_ai_controller)?.set_blackboard_key(hunger_key, TRUE);
        return dot;
    }

    // tg: code/datums/components/udder.dm:140
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        STOP_PROCESSING(SSobj, this);
        udder_mob = null;
        on_generate_callback = null;
        return dot;
    }

    // tg: code/datums/components/udder.dm:146
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (udder_mob.stat != DEAD)
            Waiting.NoSleep(generate()); //callback is on generate() itself as sometimes generate does not add new reagents, or is not called via process
        return null;
    }

    // tg: code/datums/components/udder.dm:154
    public virtual async DmTask initial_conditions()
    {
        await (reagents as datum_reagents).add_reagent(reagent_produced_typepath, 20, added_purity: 1);
        START_PROCESSING(SSobj, this);
    }

    // tg: code/datums/components/udder.dm:161
    public virtual async DmTask<object> generate()
    {
        if (require_consume_type != null && Dm.locate_type(require_consume_type, this) == null)
            return FALSE;
        if (!prob(production_probability))
            return FALSE;
        await (reagents as datum_reagents).add_reagent(reagent_produced_typepath, rand(5, 10), added_purity: 1);
        if (on_generate_callback != null)
            await on_generate_callback.Invoke((reagents as datum_reagents).total_volume, (reagents as datum_reagents).maximum_volume);
        return TRUE;
    }

    // tg: code/datums/components/udder.dm:178
    public virtual async DmTask milk(obj_item_reagent_containers_cup milk_holder = null, mob user = null)
    {
        if (Dm.num((milk_holder.reagents as datum_reagents).total_volume) >= Dm.num(milk_holder.volume))
        {
            to_chat(user, span_warning(Dm.text("[] is full.", milk_holder)));
            return;
        }
        object transferred = await (reagents as datum_reagents).trans_to(milk_holder, rand(5, 10));
        if (Dm.truthy(transferred))
            user.visible_message(span_notice(Dm.text("[] milks [] using \\the [].", user, udder_mob, milk_holder)), span_notice(Dm.text("You milk [] using \\the [].", udder_mob, milk_holder)));
        else
            to_chat(user, span_warning("The udder is dry. Wait a bit longer..."));
    }
}

// tg: code/datums/components/udder.dm:192
public partial class obj_item_udder_gutlunch : obj_item_udder
{
    // tg: code/datums/components/udder.dm:197
    public override async DmTask<object> generate()
    {
        object dot = await base.generate();
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.locate_type(typeof(obj_item_stack_ore_gold), this) != null)
            await (reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_cream), rand(2, 5), added_purity: 1);
        if (Dm.locate_type(typeof(obj_item_stack_ore_bluespace_crystal), this) != null)
            await (reagents as datum_reagents).add_reagent(typeof(datum_reagent_medicine_salglu_solution), rand(2, 5));
        if (on_generate_callback != null)
            await on_generate_callback.Invoke((reagents as datum_reagents).total_volume, (reagents as datum_reagents).maximum_volume);
        return dot;
    }
}

// tg: code/datums/components/udder.dm:208
public partial class obj_item_udder_raptor : obj_item_udder
{
    // tg: code/datums/components/udder.dm:211
    public override async DmTask<object> generate()
    {
        if (!prob(production_probability))
            return FALSE;
        object happiness_percentage = (udder_mob.ai_controller as datum_ai_controller)?.blackboard[BB_BASIC_HAPPINESS];
        if (prob(Dm.num(happiness_percentage)))
            await (reagents as datum_reagents).add_reagent(typeof(datum_reagent_consumable_cream), 5, added_purity: 1);
        float minimum_bound = Dm.num(happiness_percentage) > 0.6f ? 10 : 5;
        float upper_bound = minimum_bound + 5;
        await (reagents as datum_reagents).add_reagent(reagent_produced_typepath, rand(minimum_bound, upper_bound), added_purity: 1);
        return null;
    }
}
