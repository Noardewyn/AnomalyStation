// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/honkerblast.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the monkey bomb banana bunch's blast).

public static partial class GlobalProcs
{
    ///Unleashes a honkerblast similar to the honkmech weapon, but with more granular control.
    // tg: code/__HELPERS/honkerblast.dm:2
    public static async DmTask honkerblast(atom origin = null, float? light_range = null, float? medium_range = null, float? heavy_range = null)
    {
        float light = light_range ?? 1, medium = medium_range ?? 0, heavy = heavy_range ?? 0;
        turf origin_turf = get_turf(origin);
        DmList lightly_honked = Dm.list();
        DmList properly_honked = Dm.list();
        DmList severely_honked = Dm.list();

        playsound(origin_turf, Dm.resource("sound/items/airhorn/airhorn.ogg"), 100, TRUE);

        foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(hearers(Dm.max(light, medium, heavy), origin_turf)))
        {
            if (HAS_TRAIT(victim, TRAIT_DEAF))
                continue;
            float distance = get_dist(origin_turf, victim.loc);
            if (distance <= heavy)
                Dm.append(ref severely_honked, victim);
            else if (distance <= medium)
                Dm.append(ref properly_honked, victim);
            else if (distance <= light)
                Dm.append(ref lightly_honked, victim);
        }

        foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(severely_honked))
        {
            await victim.Unconscious(40);
            await victim.Stun(100);
            await victim.adjust_stutter(30 * SECONDS);
            await victim.set_jitter_if_lower(1000 * SECONDS);
            await victim.sound_damage(10, 30 * SECONDS);
            to_chat(victim, "<font color='red' size='8'>HONK</font>");
            obj_item_clothing_shoes victim_shoes = victim.get_item_by_slot(ITEM_SLOT_FEET) as obj_item_clothing_shoes;
            if (victim_shoes == null || Dm.eq(victim_shoes.fastening_type, SHOES_SLIPON))
                continue;
            victim_shoes.adjust_laces(SHOES_KNOTTED);
        }

        foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(properly_honked))
        {
            await victim.Paralyze(20);
            await victim.Stun(50);
            await victim.set_jitter_if_lower(500 * SECONDS);
            await victim.sound_damage(7, 20 * SECONDS);
            to_chat(victim, "<font color='red' size='5'>HONK</font>");
        }

        foreach (mob_living_carbon victim in Dm.each<mob_living_carbon>(lightly_honked))
        {
            await victim.Knockdown(20);
            await victim.set_jitter_if_lower(200 * SECONDS);
            await victim.sound_damage(4, 10 * SECONDS);
            to_chat(victim, "<font color='red' size='2'>HONK</font>");
        }
    }
}
