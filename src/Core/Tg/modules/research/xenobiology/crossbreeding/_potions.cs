// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_potions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the crossbreeds' potions (the extract cloner, pacification, love, pressurization, lavaproofing, slime revival
// and the omnistabilizer; the maximizer is a var-only subtype of the enhancer). The var blocks are generated (D111).

/*
Slimecrossing Potions
	Potions added by the slimecrossing system.
	Collected here for clarity.
*/

//Extract cloner - Charged Grey
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:9
public partial class obj_item_slimepotion_extract_cloner : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:13
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (interacting_with is obj_item_slimecross)
        {
            to_chat(user, span_warning(I18n.T("[] is too complex for the potion to clone!", interacting_with)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!(interacting_with is obj_item_slime_extract))
            return ITEM_INTERACT_BLOCKING;
        obj_item_slime_extract S = (obj_item_slime_extract)interacting_with;
        if (Dm.truthy(S.recurring))
        {
            to_chat(user, span_warning(I18n.T("[] is too complex for the potion to clone!", interacting_with)));
            return ITEM_INTERACT_BLOCKING;
        }
        Type path = (Type)S.type;
        obj_item_slime_extract C = Dm.@new(path, get_turf(interacting_with)) as obj_item_slime_extract;
        C.extract_uses = S.extract_uses;
        to_chat(user, span_notice(I18n.T("You pour the potion onto [], and the fluid solidifies into a copy of it!", interacting_with)));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

//Peace potion - Charged Light Pink
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:35
public partial class obj_item_slimepotion_peacepotion : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:39
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        mob_living peace_target = interacting_with as mob_living;
        if (!isliving(peace_target) || peace_target.stat == DEAD)
        {
            to_chat(user, span_warning(I18n.T("[] only works on the living.", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (ismegafauna(peace_target))
        {
            to_chat(user, span_warning(I18n.T("[] does not work on beings of pure evil!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (peace_target != user)
            peace_target.visible_message(span_danger(I18n.T("[] starts to feed [] []!", user, peace_target, this)),
                span_userdanger(I18n.T("[] starts to feed you []!", user, this)));
        else
            peace_target.visible_message(span_danger(I18n.T("[] starts to drink []!", user, this)),
                span_danger(I18n.T("You start to drink []!", this)));

        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: peace_target)))
            return ITEM_INTERACT_BLOCKING;
        if (peace_target != user)
            to_chat(user, span_notice(I18n.T("You feed [] []!", peace_target, this)));
        else
            to_chat(user, span_warning(I18n.T("You drink []!", this)));
        if (isanimal_or_basicmob(peace_target))
            ADD_TRAIT(peace_target, TRAIT_PACIFISM, MAGIC_TRAIT);
        else if (iscarbon(peace_target))
        {
            mob_living_carbon peaceful_carbon = (mob_living_carbon)peace_target;
            await peaceful_carbon.gain_trauma(typeof(datum_brain_trauma_severe_pacifism), TRAUMA_RESILIENCE_SURGERY);
        }
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

//Love potion - Charged Pink
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:73
public partial class obj_item_slimepotion_lovepotion : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:77
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        mob_living love_target = interacting_with as mob_living;
        if (!isliving(love_target) || love_target.stat == DEAD)
        {
            to_chat(user, span_warning(I18n.T("The love potion only works on living things, sicko!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (ismegafauna(love_target))
        {
            to_chat(user, span_warning(I18n.T("The love potion does not work on beings of pure evil!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (user == love_target)
        {
            to_chat(user, span_warning(I18n.T("You can't drink the love potion. What are you, a narcissist?")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.truthy(love_target.has_status_effect(typeof(datum_status_effect_in_love))))
        {
            to_chat(user, span_warning(I18n.T("[] is already lovestruck!", love_target)));
            return ITEM_INTERACT_BLOCKING;
        }

        love_target.visible_message(span_danger(I18n.T("[] starts to feed [] a love potion!", user, love_target)),
            span_userdanger(I18n.T("[] starts to feed you a love potion!", user)));

        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: love_target)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You feed [] the love potion!", love_target)));
        to_chat(love_target, span_notice(I18n.T("You develop feelings for [], and anyone [] like[].", user, user.p_they(), user.p_s())));
        love_target.add_ally(user);
        await love_target.apply_status_effect(typeof(datum_status_effect_in_love), user);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

//Pressure potion - Charged Dark Blue
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:109
public partial class obj_item_slimepotion_spaceproof : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:114
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (uses <= 0)
        {
            qdel(this);
            return ITEM_INTERACT_BLOCKING;
        }
        obj_item_clothing clothing = interacting_with as obj_item_clothing;
        if (!(clothing is obj_item_clothing))
        {
            to_chat(user, span_warning(I18n.T("The potion can only be used on clothing!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (clothing is obj_item_clothing_suit_space)
        {
            to_chat(user, span_warning(I18n.T("The [] is already pressure-resistant!", interacting_with)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.eq(clothing.min_cold_protection_temperature, SPACE_SUIT_MIN_TEMP_PROTECT) && Dm.truthy(Dm.bitand(clothing.clothing_flags, STOPSPRESSUREDAMAGE)))
        {
            to_chat(user, span_warning(I18n.T("The [] is already pressure-resistant!", interacting_with)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You slather the blue gunk over the [], making it airtight.", clothing)));
        clothing.name = Dm.text("pressure-resistant []", clothing.name);
        await clothing.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
        await clothing.add_atom_colour(color_transition_filter(COLOR_NAVY, SATURATION_OVERRIDE), FIXED_COLOUR_PRIORITY);
        clothing.min_cold_protection_temperature = SPACE_SUIT_MIN_TEMP_PROTECT;
        clothing.cold_protection = clothing.body_parts_covered;
        clothing.clothing_flags = Dm.bitor(clothing.clothing_flags, STOPSPRESSUREDAMAGE);
        uses--;
        if (uses <= 0)
            qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

//Enhancer potion - Charged Cerulean
// /obj/item/slimepotion/enhancer/max (tg _potions.dm:144) is var-only.

//Lavaproofing potion - Charged Red
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:151
public partial class obj_item_slimepotion_lavaproof : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:157
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        if (uses <= 0)
        {
            qdel(this);
            return ITEM_INTERACT_BLOCKING;
        }
        if (!isitem(interacting_with))
        {
            to_chat(user, span_warning(I18n.T("You can't coat this with lavaproofing fluid!")));
            return ITEM_INTERACT_BLOCKING;
        }

        obj_item clothing = (obj_item)interacting_with;
        to_chat(user, span_notice(I18n.T("You slather the red gunk over the [], making it lavaproof.", clothing)));
        clothing.name = Dm.text("lavaproof []", clothing.name);
        await clothing.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
        await clothing.add_atom_colour(color_transition_filter(COLOR_MAROON, SATURATION_OVERRIDE), FIXED_COLOUR_PRIORITY);
        clothing.resistance_flags = Dm.bitor(clothing.resistance_flags, LAVA_PROOF);
        if (isclothing(clothing))
        {
            obj_item_clothing clothing_real = (obj_item_clothing)clothing;
            clothing_real.clothing_flags = Dm.bitor(clothing_real.clothing_flags, LAVAPROTECT);
            clothing_real.resistance_flags = Dm.bitor(clothing_real.resistance_flags, FIRE_PROOF);
        }
        uses--;
        if (uses <= 0)
            qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

//Revival potion - Charged Grey
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:185
public partial class obj_item_slimepotion_slime_reviver : obj_item_slimepotion
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:189
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(dot, ITEM_INTERACT_ANY_BLOCKER)))
            return dot;
        mob_living_basic_slime revive_target = interacting_with as mob_living_basic_slime;
        if (!isslime(revive_target))
        {
            to_chat(user, span_warning(I18n.T("The potion only works on slimes!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (revive_target.stat != DEAD)
        {
            to_chat(user, span_warning(I18n.T("The slime is still alive!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.num(revive_target.maxHealth) <= 0)
        {
            to_chat(user, span_warning(I18n.T("The slime is too unstable to return!")));
            return ITEM_INTERACT_BLOCKING;
        }
        await user.do_attack_animation(interacting_with);
        await revive_target.revive(HEAL_ALL);
        revive_target.visible_message(span_notice(I18n.T("[] is filled with renewed vigor and blinks awake!", revive_target)));
        revive_target.maxHealth -= 10; //Revival isn't healthy.
        revive_target.health -= 10;
        await revive_target.regenerate_icons();
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

//Stabilizer potion - Charged Blue
// tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:214
public partial class obj_item_slimepotion_slime_chargedstabilizer : obj_item_slimepotion_slime
{
    // tg: code/modules/research/xenobiology/crossbreeding/_potions.dm:218
    public override async DmTask<object> interact_with_slime(mob_living_basic_slime interacting_slime = null, mob_living user = null, DmList modifiers = null)
    {
        await DmTask.CompletedTask;
        if (IS_UNCONSCIOUS_OR_CRIT(interacting_slime))
        {
            to_chat(user, span_warning(I18n.T("The slime is dead!")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (Dm.num(interacting_slime.mutation_chance) == 0)
        {
            to_chat(user, span_warning(I18n.T("The slime already has no chance of mutating!")));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You feed the slime the omnistabilizer. It will not mutate this cycle!")));
        interacting_slime.mutation_chance = 0;
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}
