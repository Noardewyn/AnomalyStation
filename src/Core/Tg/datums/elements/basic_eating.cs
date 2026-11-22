// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/basic_eating.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/**
 * ## basic eating element!
 *
 * Small behavior for non-carbons to eat certain stuff they interact with
 */
// tg: code/datums/elements/basic_eating.dm:7
public partial class datum_element_basic_eating : datum_element
{
    // tg: code/datums/elements/basic_eating.dm:24
    [DmParams("target", "heal_amt", "damage_amount", "damage_type", "drinking", "add_to_contents", "consume_healing", "food_types")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object heal_amt = Dm.arg(__extra, 0) ?? 0f;
        object damage_amount = Dm.arg(__extra, 1) ?? 0f;
        object damage_type = Dm.arg(__extra, 2);
        object drinking = Dm.arg(__extra, 3) ?? FALSE;
        object add_to_contents = Dm.arg(__extra, 4) ?? FALSE;
        object consume_healing = Dm.arg(__extra, 5) ?? TRUE;
        DmList food_types = Dm.arg<DmList>(__extra, 6) ?? Dm.list();
        object dot = await base.Attach(target, __extra);

        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        ADD_TRAIT(target, TRAIT_MOB_EATER, REF(this));
        this.heal_amt = heal_amt;
        this.damage_amount = damage_amount;
        this.damage_type = damage_type;
        this.drinking = drinking;
        this.add_to_contents = add_to_contents;
        this.consume_healing = consume_healing;
        this.food_types = food_types;

        RegisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION, nameof(try_feed));
        RegisterSignal(target, COMSIG_LIVING_UNARMED_ATTACK, nameof(on_unarm_attack));
        return dot;
    }

    // tg: code/datums/elements/basic_eating.dm:42
    public override object Detach(datum target = null, params object[] __extra)
    {
        REMOVE_TRAIT(target, TRAIT_MOB_EATER, REF(this));

        UnregisterSignal(target, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_ATOM_ITEM_INTERACTION));
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/basic_eating.dm:51
    [SignalHandler]
    public virtual object try_feed(atom source = null, mob_living user = null, atom possible_food = null)
    {
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(is_type_in_list(possible_food, food_types)))
            return NONE;
        mob_living living_source = source as mob_living;
        if (IS_UNCONSCIOUS(living_source))
            return NONE;
        return Dm.truthy(try_eating(living_source, possible_food, user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/datums/elements/basic_eating.dm:60
    [SignalHandler]
    public virtual object on_unarm_attack(mob_living eater = null, atom target = null, object proximity = null, DmList modifiers = null)
    {
        if (!Dm.truthy(proximity))
            return NONE;

        if (Dm.truthy(try_eating(eater, target)))
            return COMPONENT_CANCEL_ATTACK_CHAIN;
        return NONE;
    }

    // tg: code/datums/elements/basic_eating.dm:69
    public virtual object try_eating(mob_living eater = null, atom target = null, mob_living feeder = null)
    {
        if (!Dm.truthy(is_type_in_list(target, food_types)))
            return FALSE;
        DmList effect_mult = Dm.list();
        if (Dm.truthy(Dm.bitand(!Dm.truthy(eater._listen_lookup?[COMSIG_MOB_PRE_EAT]) ? NONE : eater._SendSignal(COMSIG_MOB_PRE_EAT, Dm.list(eater, target, feeder, effect_mult)), COMSIG_MOB_CANCEL_EAT)))
            return FALSE;
        if (Dm.truthy(add_to_contents) && !ismovable(target))
            return FALSE;
        object eat_verb;
        if (Dm.truthy(drinking))
            eat_verb = pick("slurp", "sip", "guzzle", "drink", "quaff", "suck");
        else
            eat_verb = pick("bite", "chew", "nibble", "gnaw", "gobble", "chomp");

        Type best_match = null;
        float best_value = 0;
        foreach (object food_path in Dm.each<object>(food_types))
        {
            // Not an assoc list
            if (food_types[food_path] == null)
                break;
            if (istype(target, (Type)food_path) && (best_match == null || ispath(food_path, best_match)))
            {
                best_match = food_path as Type;
                best_value = Dm.num(food_types[food_path]);
            }
        }

        float to_heal = Dm.num(heal_amt);
        float to_damage = Dm.num(damage_amount);

        if (best_match != null)
        {
            to_heal = 0;
            to_damage = 0;
            if (best_value > 0)
                to_heal = best_value;
            else if (best_value < 0)
                to_damage = -best_value;

            foreach (object mult in Dm.each<object>(effect_mult))
            {
                to_heal *= Dm.num(mult);
                to_damage *= Dm.num(mult);
            }
        }

        if (to_heal > 0)
        {
            object healed = Waiting.NoSleep(eater.heal_overall_damage(to_heal));
            eater.visible_message(span_notice(Dm.text("[] []s [].", eater, eat_verb, target)), span_notice(Dm.text("You [] [][].", eat_verb, target, Dm.truthy(healed) ? ", restoring some health" : "")));
        }
        else if (to_damage > 0 && Dm.truthy(damage_type))
        {
            object damaged = Waiting.NoSleep(eater.apply_damage(to_damage, damage_type as string));
            eater.visible_message(span_notice(Dm.text("[] []s [][]", eater, eat_verb, target, Dm.truthy(damaged) ? Dm.text(", and seems to hurt []!", eater.p_themselves()) : ".")), span_notice(Dm.text("You [] [][].", eat_verb, target, Dm.truthy(damaged) ? ", hurting yourself in the process" : "")));
        }
        else
            eater.visible_message(span_notice(Dm.text("[] []s [].", eater, eat_verb, target)), span_notice(Dm.text("You [] [].", eat_verb, target)));

        Waiting.Detach(finish_eating(eater, target, feeder, to_heal));
        return TRUE;
    }

    // tg: code/datums/elements/basic_eating.dm:120
    public virtual async DmTask finish_eating(mob_living eater = null, atom target = null, mob_living feeder = null, object to_heal = null)
    {
        if (Dm.truthy(drinking))
            playsound(eater.loc, Dm.resource("sound/items/drink.ogg"), rand(10, 50), TRUE);
        else
            playsound(eater.loc, Dm.resource("sound/items/eatfood.ogg"), rand(10, 50), TRUE);
        atom final_target = target;
        if (Dm.truthy(Dm.bitand(!Dm.truthy(eater._listen_lookup?[COMSIG_MOB_ATE]) ? NONE : eater._SendSignal(COMSIG_MOB_ATE, Dm.list(eater, final_target, feeder)), COMSIG_MOB_TERMINATE_EAT)))
            return;
        if (isstack(target)) //if stack, only consume 1
        {
            obj_item_stack food_stack = target as obj_item_stack;
            final_target = await food_stack.split_stack(1) as atom;
        }

        bool devour = Dm.truthy(add_to_contents) && !Dm.truthy(to_heal);
        eater.log_message(Dm.text("has eaten [], []!", target, devour ? "swallowing it" : "destroying it"), LOG_ATTACK);

        if (devour)
        {
            atom_movable movable_target = final_target as atom_movable;
            await movable_target.forceMove(eater);
        }
        else
            qdel(final_target);
    }
}
