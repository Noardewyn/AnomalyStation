// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/wumborian_fugu/fugu_gland.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Item you use on a mob to make it bigger and stronger
// tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/fugu_gland.dm:2
public partial class obj_item_fugu_gland : obj_item
{
    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/fugu_gland.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // tg fills its static fugu_blacklist here; the check reads the type directly
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mob/living/basic/space_fauna/wumborian_fugu/fugu_gland.dm:22
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isanimal_or_basicmob(interacting_with) || interacting_with.GetType() == typeof(mob_living_basic_guardian))
            return NONE;
        mob_living animal = interacting_with as mob_living;

        if (IS_DEAD_OR_FAKING(animal))
        {
            balloon_alert(user, "it's dead!");
            return ITEM_INTERACT_BLOCKING;
        }
        if (HAS_TRAIT(animal, TRAIT_FUGU_GLANDED))
        {
            balloon_alert(user, "already large!");
            return ITEM_INTERACT_BLOCKING;
        }

        ADD_TRAIT(animal, TRAIT_FUGU_GLANDED, type);
        await animal.AddComponent(typeof(datum_component_seethrough_mob));
        animal.maxHealth = Dm.num(animal.maxHealth) * 1.5f;
        animal.health = Dm.min(Dm.num(animal.maxHealth), Dm.num(animal.health) * 1.5f);
        animal.melee_damage_lower = Dm.max(Dm.num(animal.melee_damage_lower) * 2, 10);
        animal.melee_damage_upper = Dm.max(Dm.num(animal.melee_damage_upper) * 2, 10);
        await animal.update_transform(2);
        await animal.AddElement(typeof(datum_element_wall_tearer));
        to_chat(user, span_info(Dm.text("You increase the size of [], giving [] a surge of strength!", animal, animal.p_them())));
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}
