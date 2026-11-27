// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/clown/clown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the clowns of Clown Planet (the bitrunning clown planet's denizens): the base clown with its waddle, honk, retaliation
// call to the nearby clowns and banana ripening, the living lube, the honkling and the honkmunculus with their venom, the flesh
// clown, and the var-only clown hulks, chlown, destroyer and mutant; the longface, the banana glutton with its eating, regurgitation and
// riding, and the banana clown with its rustle and exquisite bunch, the ones cytology grows. The var blocks are generated.

// tg: code/modules/mob/living/basic/clown/clown.dm:1
public partial class mob_living_basic_clown : mob_living_basic
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:43
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NAIVE, INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), Dm.named("footstep_type", FOOTSTEP_MOB_SHOE)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ai_retaliate_advanced), Dm.@new<datum_callback>(this, nameof(retaliate_callback))));
        ((datum_ai_controller)ai_controller).set_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, emotes);
        //im not putting dynamic humans or whatever its called here because this is the base path of nonhuman clownstrosities
        if (Dm.truthy(waddles))
            Waiting.NoSleep(AddElementTrait(TRAIT_WADDLING, INNATE_TRAIT, typeof(datum_element_waddling)));
        if (Dm.truthy(length(loot)))
        {
            loot = string_list(loot) as DmList;
            Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), loot));
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:56
    public virtual void retaliate_callback(mob_living attacker = null)
    {
        if (attacker is not mob_living)
            return;
        foreach (mob_living_basic_clown harbringer in Dm.each<mob_living_basic_clown>(oview(this, 7)))
            ((datum_ai_controller)harbringer.ai_controller).set_blackboard_key_assoc_lazylist(BB_BASIC_MOB_RETALIATE_LIST, attacker, world.time);
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:62
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        ignore_cooldown ??= FALSE;
        if (target is not obj_item_food_grown_banana_bunch)
            return await base.melee_attack(target, modifiers, ignore_cooldown);
        obj_item_food_grown_banana_bunch unripe_bunch = target as obj_item_food_grown_banana_bunch;
        unripe_bunch.start_ripening();
        log_combat(this, target, "explosively ripened");
        return null;
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:69
    public override object get_bloodtype()
    {
        if (Dm.truthy(check_holidays(APRIL_FOOLS)))
            return get_blood_type(typeof(datum_blood_type_clown));
        return base.get_bloodtype();
    }
}

// tg: code/modules/mob/living/basic/clown/clown.dm:74
public partial class mob_living_basic_clown_lube : mob_living_basic_clown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:97
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_lube_walking)));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/clown/clown.dm:101
public partial class mob_living_basic_clown_honkling : mob_living_basic_clown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:118
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object injection_range = TgState.Current.mob_living_basic_clown_honkling__Initialize__injection_range;
        if (!Dm.truthy(injection_range))
            injection_range = TgState.Current.mob_living_basic_clown_honkling__Initialize__injection_range = string_numbers_list(Dm.list(1, 5));
        Waiting.NoSleep(AddElement(
            typeof(datum_element_venomous),
            typeof(datum_reagent_consumable_laughter),
            injection_range,
            Dm.named("injection_flags", Dm.bitor(INJECT_CHECK_PENETRATE_THICK, INJECT_CHECK_IGNORE_SPECIES))
        ));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/clown/clown.dm:130
public partial class mob_living_basic_clown_fleshclown : mob_living_basic_clown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:169
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        return dot;
    }
}

// tg: code/modules/mob/living/basic/clown/clown.dm:274
public partial class mob_living_basic_clown_clownhulk_honkmunculus : mob_living_basic_clown_clownhulk
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:300
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object injection_range = TgState.Current.mob_living_basic_clown_clownhulk_honkmunculus__Initialize__injection_range;
        if (!Dm.truthy(injection_range))
            injection_range = TgState.Current.mob_living_basic_clown_clownhulk_honkmunculus__Initialize__injection_range = string_numbers_list(Dm.list(1, 5));
        Waiting.NoSleep(AddElement(
            typeof(datum_element_venomous),
            typeof(datum_reagent_peaceborg_confuse),
            injection_range,
            Dm.named("injection_flags", Dm.bitor(INJECT_CHECK_PENETRATE_THICK, INJECT_CHECK_IGNORE_SPECIES))
        )); // I don't really know what a clown is using to inject people but let's assume it doesn't need to penetrate at all
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:120  the honkling's Initialize's var/static/list/injection_range
    internal object mob_living_basic_clown_honkling__Initialize__injection_range;
    // tg: code/modules/mob/living/basic/clown/clown.dm:302  the honkmunculus' Initialize's var/static/list/injection_range
    internal object mob_living_basic_clown_clownhulk_honkmunculus__Initialize__injection_range;
}

// tg: code/modules/mob/living/basic/clown/clown.dm:173
public partial class mob_living_basic_clown_longface : mob_living_basic_clown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:204
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_LONGFACE, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/clown/clown.dm:380
public partial class mob_living_basic_clown_mutant_glutton : mob_living_basic_clown_mutant
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:415  var/static/list/food_types (rules 10.6)
    private static ref DmList Initialize__food_types => ref TgState.Current.mob_living_basic_clown_mutant_glutton__Initialize__food_types;
    // tg: code/modules/mob/living/basic/clown/clown.dm:452  var/static/funny_items (rules 10.6)
    private static ref DmList eat_atom__funny_items => ref TgState.Current.mob_living_basic_clown_mutant_glutton__eat_atom__funny_items;

    // tg: code/modules/mob/living/basic/clown/clown.dm:412
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(Dm.@new<datum_action_cooldown_regurgitate>(this).Grant(this)); // GRANT_ACTION
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_GLUTTON, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        Waiting.NoSleep(AddComponent(typeof(datum_component_tameable), Dm.named("food_types", Initialize__food_types), Dm.named("tame_chance", 30), Dm.named("bonus_tame_chance", 0)));
        Waiting.NoSleep(AddElement(typeof(datum_element_damage_threshold), 10)); //lots of fat to cushion blows.
        return dot;
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:423
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(check_edible(tool)))
            return NONE;
        await eat_atom(tool);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:429
    public override async DmTask<object> resolve_unarmed_attack(atom attack_target = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(check_edible(attack_target as atom_movable)))
            return await base.resolve_unarmed_attack(attack_target, modifiers, __extra);
        await eat_atom(attack_target as atom_movable);
        return null;
    }

    ///Returns whether or not the supplied movable atom is edible.
    // tg: code/modules/mob/living/basic/clown/clown.dm:435
    public virtual object check_edible(atom_movable potential_food = null)
    {
        if (isliving(potential_food))
        {
            mob_living living_morsel = (mob_living)potential_food;
            if (living_morsel.mob_size > MOB_SIZE_SMALL)
                return FALSE;
            else
                return TRUE;
        }

        if (potential_food != null && IS_EDIBLE(potential_food))
        {
            if (prank_pouch.len >= 8)
            {
                to_chat(this, span_warning(I18n.T("Your prank pouch is filled to the brim! You don't think you can swallow any more morsels right now.")));
                return FALSE;
            }
            return TRUE;
        }
        return null;
    }

    ///This proc eats the atom, certain funny items are stored directly in the prank pouch while bananas grant a heal based on their potency and the peels are retained in the pouch.
    // tg: code/modules/mob/living/basic/clown/clown.dm:450
    public virtual async DmTask eat_atom(atom_movable eaten_atom = null)
    {
        visible_message(span_warning(I18n.T("[] eats []!", this, eaten_atom)), span_notice(I18n.T("You eat [].", eaten_atom)));
        if (Dm.truthy(is_type_in_list(eaten_atom, eat_atom__funny_items)))
        {
            await eaten_atom.forceMove(this);
            Dm.append(ref prank_pouch, eaten_atom);
        }
        else
        {
            //Encourage gluttony RP by giving a litle bonus for each obj eaten.
            foods_eaten++;
            if (!Dm.truthy(foods_eaten % 5) && foods_eaten <= 100)
            {
                balloon_alert(this, I18n.T("weight gained!"));
                maxHealth += 10;
                health += 10;
            }
            if (eaten_atom is obj_item_food_grown_banana)
            {
                obj_item_food_grown_banana banana_morsel = (obj_item_food_grown_banana)eaten_atom;
                await adjust_brute_loss(-(Dm.num((banana_morsel.seed as obj_item_seeds)?.potency) / 100) * maxHealth * 0.2f);
                Dm.append(ref prank_pouch, await banana_morsel.generate_trash(this));
            }
            else
                await adjust_brute_loss(-maxHealth * 0.1f);
            qdel(eaten_atom);
        }

        playsound(loc, "sound/items/eatfood.ogg", rand(30, 50), TRUE);
        flick("glutton_mouth", this);
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:482
    public override async DmTask tamed(mob_living tamer = null, obj_item food = null, params object[] __extra)
    {
        await base.tamed(tamer, food, __extra);
        buckle_lying = 0;
        await AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_glutton));
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:487
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        Dm.remove(ref prank_pouch, gone);
        return dot;
    }
}

///This ability will let you fire one random item from your pouch,
// tg: code/modules/mob/living/basic/clown/clown.dm:492
public partial class datum_action_cooldown_regurgitate : datum_action_cooldown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:502
    public override async DmTask<object> set_click_ability(mob on_who = null)
    {
        object dot = await base.set_click_ability(on_who);
        if (!Dm.truthy(dot))
            return dot;

        to_chat(on_who, span_notice(I18n.T("Your throat muscles tense up. <B>Left-click to regurgitate a funny morsel!</B>")));
        on_who.icon_state = "glutton_tongue";
        on_who.update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:511
    public override async DmTask<object> unset_click_ability(mob on_who = null, float? refund_cooldown = null)
    {
        refund_cooldown ??= TRUE;
        object dot = await base.unset_click_ability(on_who, refund_cooldown);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(refund_cooldown))
            to_chat(on_who, span_notice(I18n.T("Your throat muscles relax.")));
        on_who.icon_state = Dm.str(Dm.initial(on_who, (atom a) => a.icon_state));
        on_who.update_appearance(UPDATE_ICON);
        return dot;
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:521
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;

        // Hardcoded to only work with gluttons. Come back next year
        return Dm.num(owner is mob_living_basic_clown_mutant_glutton);
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:529
    public override async DmTask<object> Activate(atom spit_at = null)
    {
        StartCooldown(cooldown_time / 4);

        mob_living_basic_clown_mutant_glutton pouch_owner = owner as mob_living_basic_clown_mutant_glutton;
        if (!Dm.truthy(length(pouch_owner.prank_pouch)))
        {
            pouch_owner.icon_state = Dm.str(Dm.initial(pouch_owner, (atom a) => a.icon_state));
            to_chat(pouch_owner, span_notice(I18n.T("Your prank pouch is empty.")));
            return TRUE;
        }

        obj_item projected_morsel = pick(pouch_owner.prank_pouch) as obj_item;
        await projected_morsel.forceMove(pouch_owner.loc);
        await projected_morsel.throw_at(spit_at, 8, 2, pouch_owner);
        flick("glutton_mouth", pouch_owner);
        playsound(pouch_owner, "sound/misc/soggy.ogg", 75);

        StartCooldown();
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/clown/clown.dm:547
public partial class mob_living_basic_clown_banana : mob_living_basic_clown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:575  var/static/list/innate_actions (rules 10.6)
    private static ref DmList Initialize__innate_actions => ref TgState.Current.mob_living_basic_clown_banana__Initialize__innate_actions;

    // tg: code/modules/mob/living/basic/clown/clown.dm:573
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_CLOWNANA, CELL_VIRUS_TABLE_GENERIC_MOB, 1, 5));
        grant_actions_by_list(Initialize__innate_actions);
        return dot;
    }
}

///drops peels around the mob when activated
// tg: code/modules/mob/living/basic/clown/clown.dm:584
public partial class datum_action_cooldown_rustle : datum_action_cooldown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:597
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        DmList reachable_turfs = Dm.list();
        foreach (turf adjacent_turf in Dm.each<turf>(RANGE_TURFS(1, owner.loc as atom)))
        {
            if (adjacent_turf == owner.loc || !Dm.truthy(await adjacent_turf.IsReachableBy(owner)) || !isopenturf(adjacent_turf))
                continue;
            Dm.append(ref reachable_turfs, adjacent_turf);
        }

        float peels_to_spawn = Dm.min(peel_amount, reachable_turfs.len);
        for (float i = 1; i <= peels_to_spawn; i++)
            Dm.@new(banana_type, pick_n_take(reachable_turfs));
        playsound(owner, "sound/mobs/non-humanoids/clown/clownana_rustle.ogg", 60);
        animate(owner, Dm.named("time", 0.1f * SECONDS), Dm.named("pixel_w", 6), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)), Dm.named("flags", Dm.bitor(ANIMATION_PARALLEL, ANIMATION_RELATIVE)));
        animate(Dm.named("time", 0.2f * SECONDS), Dm.named("pixel_w", -8), Dm.named("easing", CUBIC_EASING), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("time", 0.1f * SECONDS), Dm.named("pixel_w", 2), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)), Dm.named("flags", ANIMATION_RELATIVE));
        StartCooldown();
        return dot;
    }
}

///spawns a plumb bunch of bananas imbued with mystical power.
// tg: code/modules/mob/living/basic/clown/clown.dm:615
public partial class datum_action_cooldown_exquisite_bunch : datum_action_cooldown
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:626
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        if (Dm.truthy(activating))
            return null;
        if (!Dm.truthy(await IsAvailable(feedback: TRUE)))
            return null;
        atom bunch_turf = get_step(owner.loc as atom, owner.dir);
        if (bunch_turf == null)
            return null;
        if (!Dm.truthy(await bunch_turf.IsReachableBy(owner)) || !isopenturf(bunch_turf))
        {
            owner.balloon_alert(owner, I18n.T("can't do that here!"));
            return null;
        }
        activating = TRUE;
        if (!Dm.truthy(await do_after(owner, 1 * SECONDS)))
        {
            activating = FALSE;
            return null;
        }
        playsound(owner, "sound/mobs/non-humanoids/clown/hehe.ogg", 100);
        if (!Dm.truthy(await do_after(owner, 1 * SECONDS)))
        {
            activating = FALSE;
            return null;
        }
        activating = FALSE;
        return await base.Trigger(clicker, trigger_flags, __extra);
    }

    // tg: code/modules/mob/living/basic/clown/clown.dm:648
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        Dm.@new<obj_item_food_grown_banana_bunch>(get_step(owner.loc as atom, owner.dir));
        playsound(owner, "sound/items/bikehorn.ogg", 60);
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.playsound), owner, Dm.resource("sound/mobs/non-humanoids/clown/hohoho.ogg"), 100, 1), 1 * SECONDS, file: "code/modules/mob/living/basic/clown/clown.dm", line: 652);
        StartCooldown();
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:575  /mob/living/basic/clown/banana/Initialize's var/static/list/innate_actions
    internal DmList mob_living_basic_clown_banana__Initialize__innate_actions = Dm.list(
        typeof(datum_action_cooldown_exquisite_bunch),
        typeof(datum_action_cooldown_rustle)
    );
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/clown/clown.dm:415  the glutton's Initialize's var/static/list/food_types
    internal DmList mob_living_basic_clown_mutant_glutton__Initialize__food_types = Dm.list(
        typeof(obj_item_food_cheesiehonkers),
        typeof(obj_item_food_cornchips)
    );
    // tg: code/modules/mob/living/basic/clown/clown.dm:452  the glutton's eat_atom's var/static/funny_items
    internal DmList mob_living_basic_clown_mutant_glutton__eat_atom__funny_items = Dm.list(
        typeof(obj_item_food_pie_cream),
        typeof(obj_item_food_grown_tomato),
        typeof(obj_item_food_meatclown),
        typeof(obj_item_food_ink_sac)
    );
}
