// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/food/food_trash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps S1 (D291): the whole file (a wrapper, a chip bag or a can left behind when food is eaten).
//
// GAP(types): tg's `var/atom/trash` (:6) holds a typepath (the trash's type); the generator types it `atom`, so it is declared
// here as the `Type` it is, with the type's constructor.

// If an item has the food_trash element it will drop an item when it is consumed.
// tg: code/datums/elements/food/food_trash.dm:2
public partial class datum_element_food_trash : datum_element
{
    /// The type of trash that is spawned by this element
    // tg: code/datums/elements/food/food_trash.dm:6  (hand-written: see the GAP(types) above)
    public Type trash;

    // tg: code/datums/elements/food/food_trash.dm:2  (the type's overrides, hand-written with the var above)
    public datum_element_food_trash()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }

    // tg: code/datums/elements/food/food_trash.dm:12
    [DmParams("target", "trash", "flags", "generate_trash_proc")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        Type trash = Dm.arg(__extra, 0) as Type;
        object flags = Dm.arg(__extra, 1);
        object generate_trash_proc = Dm.arg(__extra, 2);
        object dot = await base.Attach(target, Dm.forward(__extra, trash, flags, generate_trash_proc));
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        this.trash = trash;
        this.flags = flags;
        RegisterSignal(target, COMSIG_FOOD_CONSUMED, nameof(generate_trash));
        if (!Dm.truthy(generate_trash_procpath) && Dm.truthy(generate_trash_proc))
            generate_trash_procpath = generate_trash_proc;
        if (Dm.truthy(Dm.bitand(flags, FOOD_TRASH_OPENABLE)))
            RegisterSignal(target, COMSIG_ITEM_ATTACK_SELF, nameof(open_trash));
        if (Dm.truthy(Dm.bitand(flags, FOOD_TRASH_POPABLE)))
            RegisterSignal(target, COMSIG_FOOD_CROSSED, nameof(food_crossed));
        RegisterSignals(target, Dm.list(
            COMSIG_ITEM_ON_GRIND,
            COMSIG_ITEM_ON_JUICE,
            COMSIG_ITEM_USED_AS_INGREDIENT,
            COMSIG_ITEM_ON_COMPOSTED,
            COMSIG_ITEM_SOLD_TO_CUSTOMER,
            COMSIG_MOVABLE_SPLAT
        ), nameof(generate_trash));
        return dot;
    }

    // tg: code/datums/elements/food/food_trash.dm:35
    [DmParams("target")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(
            COMSIG_FOOD_CONSUMED,
            COMSIG_ITEM_ATTACK_SELF,
            COMSIG_FOOD_CROSSED,
            COMSIG_ITEM_ON_GRIND,
            COMSIG_ITEM_ON_JUICE,
            COMSIG_ITEM_USED_AS_INGREDIENT,
            COMSIG_ITEM_ON_COMPOSTED,
            COMSIG_ITEM_SOLD_TO_CUSTOMER,
            COMSIG_MOVABLE_SPLAT
        ));
        return dot;
    }

    // tg: code/datums/elements/food/food_trash.dm:49
    [SignalHandler]
    public virtual void generate_trash(datum source = null, mob_living eater = null, mob_living feeder = null)
    {
        ///cringy signal_handler shouldnt be needed if you dont want to return but oh well
        Waiting.Detach(async_generate_trash(source));
    }

    // tg: code/datums/elements/food/food_trash.dm:55
    public virtual async DmTask async_generate_trash(datum source = null)
    {
        atom edible_object = (atom)source;
        obj_item trash_item = null;

        if (source is obj_item_food_grown && ispath(trash, typeof(obj_item_food)))
        {
            obj_item_food_grown plant = source as obj_item_food_grown;
            trash_item = (ispath(trash, typeof(obj_item_food_grown)) ? Dm.@new(trash, edible_object.drop_location(), plant.seed) : Dm.@new(trash, edible_object.drop_location())) as obj_item;
            (trash_item.reagents as datum_reagents)?.set_all_reagents_purity(Dm.num(((obj_item_seeds)plant.seed).get_reagent_purity()));
        }
        else
            trash_item = Dm.truthy(generate_trash_procpath)
                ? await Dm.Call(source, (string)generate_trash_procpath) as obj_item
                : Dm.@new(trash, edible_object.drop_location()) as obj_item;

        if (isliving(edible_object.loc))
        {
            mob_living food_holding_mob = (mob_living)edible_object.loc;
            await food_holding_mob.dropItemToGround((obj_item)edible_object);
            await food_holding_mob.put_in_hands(trash_item);
        }

        Detach(source);
    }

    // tg: code/datums/elements/food/food_trash.dm:74
    [SignalHandler]
    public virtual void food_crossed(datum source = null, mob crosser = null, object bitecount = null)
    {
        if (!isliving(crosser) || Dm.truthy(bitecount)) // can't pop opened chips
            return;
        mob_living popper = (mob_living)crosser;
        if (Dm.num(popper.mob_size) < MOB_SIZE_HUMAN)
            return;

        playsound((atom)source, Dm.resource("sound/effects/chipbagpop.ogg"), 100);

        popper.visible_message(span_danger(I18n.T("[] steps on \\the [], popping the bag!", popper, source)), span_danger(I18n.T("You step on \\the [], popping the bag!", source)), span_danger(I18n.T("You hear a sharp crack!")), COMBAT_MESSAGE_RANGE);
        Waiting.Detach(async_generate_trash(source));
        qdel(source);
    }

    // tg: code/datums/elements/food/food_trash.dm:89
    [SignalHandler]
    public virtual void open_trash(datum source = null, mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You open \\the [], revealing \\a [].", source, Dm.initial(trash, (atom p) => p.name))));

        Waiting.Detach(async_generate_trash(source));
        qdel(source);
    }
}
