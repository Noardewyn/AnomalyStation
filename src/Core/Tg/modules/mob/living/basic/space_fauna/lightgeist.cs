// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/lightgeist.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/lightgeist.dm:8
public partial class mob_living_basic_lightgeist : mob_living_basic
{
    // tg: code/modules/mob/living/basic/space_fauna/lightgeist.dm:50
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_VENTCRAWLER_ALWAYS, TRAIT_MEDICAL_HUD, TRAIT_EMOTEMUTE), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_healing_touch),
            Dm.named("heal_brute", melee_damage_upper),
            Dm.named("heal_burn", melee_damage_upper),
            Dm.named("heal_time", 0f),
            Dm.named("valid_targets_typecache", typecacheof(Dm.list(typeof(mob_living)))),
            Dm.named("action_text", "%SOURCE% begins mending the wounds of %TARGET%"),
            Dm.named("complete_text", "%TARGET%'s wounds mend together.")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/lightgeist.dm:64
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot) && isliving(target))
            add_ally(target); // Anyone we heal will treat us as a friend
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/lightgeist.dm:69
    public virtual async DmTask<object> ghost()
    {
        // shortcut: /mob/living/ghost (verb) is not on the base here: the player lightgeist's "die on ghosting" waits for ghost roles
        Dm.Deferred("code/modules/mob/living/basic/space_fauna/lightgeist.dm:69", "ghost: a player lightgeist dies when it ghosts (ghost roles)");
        return null;
    }
}

/// Attack only mobs who have damage that we can heal
// tg: code/modules/mob/living/basic/space_fauna/lightgeist.dm:84
public partial class datum_targeting_strategy_lightgeist : datum_targeting_strategy
{
    // tg: code/modules/mob/living/basic/space_fauna/lightgeist.dm:90
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target_ = null, object vision_range = null, datum_ai_controller controller = null)
    {
        if (target_ is not mob_living target || target.stat == DEAD)
            return FALSE;
        if (!Dm.truthy(Dm.bitand(heal_biotypes, target.mob_biotypes)))
            return FALSE;
        if (target is not mob_living_carbon carbon_target)
            return Dm.num(target.get_brute_loss()) > 0 || Dm.num(target.get_fire_loss()) > 0 ? TRUE : FALSE;
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(carbon_target.get_bodyparts()))
        {
            if (!Dm.truthy(part.brute_dam) && !Dm.truthy(part.burn_dam))
                continue;
            if (!Dm.truthy(Dm.bitand(part.bodytype, required_bodytype)))
                continue;
            return TRUE;
        }
        return FALSE;
    }
}
