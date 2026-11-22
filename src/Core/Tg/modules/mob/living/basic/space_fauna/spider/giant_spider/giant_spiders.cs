// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The giant spider's base and Sergeant Araneus (gaps package C2), the hunter and the nurse (gaps E3, D327: the Spider Clan gift's shuttle
// loan); the other castes have the antagonists' abilities and stay inert.

/**
 * # Giant Spider
 *
 * A mob which can be created by dynamic event, botany, or xenobiology.
 * The basic type is the guard, which is slow but sturdy and outputs good damage.
 * All spiders can produce webbing.
 */
// tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:9
public partial class mob_living_basic_spider_giant : mob_living_basic_spider
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        grant_actions_by_list(innate_actions);
        return dot;
    }
}

/**
 * ### Hunter Spider
 * A subtype of the giant spider which is faster, has toxin injection, but less health and damage.
 * This spider is only slightly slower than a human.
 */
// tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:93
public partial class mob_living_basic_spider_giant_hunter : mob_living_basic_spider_giant
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:109
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_web_walker), typeof(datum_movespeed_modifier_fast_web)));
        return dot;
    }
}

/**
 * ### Nurse Spider
 *
 * A subtype of the giant spider which specializes in support skills.
 * Nurses can place down webbing in a quarter of the time that other species can and can wrap other spiders' wounds, healing them.
 * Note that it cannot heal itself.
 */
// tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:149
public partial class mob_living_basic_spider_giant_nurse : mob_living_basic_spider_giant
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:168
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_MEDICAL_HUD, INNATE_TRAIT);

        Waiting.NoSleep(AddComponent(typeof(datum_component_healing_touch),
            Dm.named("heal_brute", 10),
            Dm.named("heal_burn", 10),
            Dm.named("heal_time", 2.5f * SECONDS),
            Dm.named("interaction_key", DOAFTER_SOURCE_SPIDER),
            Dm.named("valid_targets_typecache", typecacheof(Dm.list(typeof(mob_living_basic_spider_giant)))),
            Dm.named("action_text", "%SOURCE% begins wrapping the wounds of %TARGET%."),
            Dm.named("complete_text", "%SOURCE% wraps the wounds of %TARGET%.")
        ));

        Waiting.NoSleep(AddElement(typeof(datum_element_web_walker), typeof(datum_movespeed_modifier_average_web)));
        return dot;
    }
}

/**
 * ### Sergeant Araneus
 *
 * This friendly arachnid hangs out in the HoS office on some space stations. Better trained than an average officer and does not attack except in self-defence.
 */
// tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:583
public partial class mob_living_basic_spider_giant_sgt_araneus : mob_living_basic_spider_giant
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/giant_spider/giant_spiders.dm:594
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_pet_bonus), "chitter"));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        return dot;
    }
}
