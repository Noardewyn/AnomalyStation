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
// clown, and the var-only clown hulks, chlown, destroyer and mutant. The longface and the banana clown (their cell lines, the
// swabbing of xenobiology) and the glutton (its regurgitation, eating and riding) wait (not-translated.md). The var blocks are
// generated.

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
