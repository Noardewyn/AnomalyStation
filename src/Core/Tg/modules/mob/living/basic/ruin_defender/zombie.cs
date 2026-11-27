// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/ruin_defender/zombie.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the shambling corpse of the ruins and the bitrunning psyker zombies, dressed as its outfit with the zombie's look,
// which leaves bones on death, and its rotten variants. The admins' infection on hit (infection_chance, 0 by default) waits for the
// zombie infection (not-translated.md). The var blocks are generated.

/// Everyone knows what a zombie is
// tg: code/modules/mob/living/basic/ruin_defender/zombie.dm:2
public partial class mob_living_basic_zombie : mob_living_basic
{
    // tg: code/modules/mob/living/basic/ruin_defender/zombie.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        apply_dynamic_human_appearance(this, outfit, typeof(datum_species_zombie), bloody_slots: ITEM_SLOT_OCLOTHING);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_decal_remains_human)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/ruin_defender/zombie.dm:36
    public override object get_unconscious_appearance()
    {
        return get_generic_humanoid_static_appearance();
    }

    // tg: code/modules/mob/living/basic/ruin_defender/zombie.dm:39
    public override async DmTask<object> melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.melee_attack(target, modifiers, ignore_cooldown);
        if (!Dm.truthy(dot) || !Dm.truthy(infection_chance) || !ishuman(target) || !prob(infection_chance))
            return dot;
        Dm.NotTranslated("code/modules/mob/living/basic/ruin_defender/zombie.dm:43", "melee_attack: try_to_zombie_infect, the zombie infection (modules/zombie)");
        return dot;
    }
}
