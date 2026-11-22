// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/pets/dog/dog_subtypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

//Less exciting dog breeds

// tg: code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:4
public partial class mob_living_basic_pet_dog_pug : mob_living_basic_pet_dog
{
    // tg: code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:17
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_PUG, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        return dot;
    }
}

// tg: code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:78
public partial class mob_living_basic_pet_dog_breaddog : mob_living_basic_pet_dog
{
    // tg: code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:99
    public override async DmTask on_craft_completion(DmList components = null, datum_crafting_recipe current_recipe = null, atom crafter = null)
    {
        await base.on_craft_completion(components, current_recipe, crafter);
        Dm.Deferred("code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:102", "on_craft_completion: the crafted bread dog's brain and its player (no recipe makes it; brainmobs into a basic mob, sentience)");
    }

    // tg: code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:115
    public override object Life(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSMOBS_DT();
        object dot = base.Life(seconds_per_tick, __extra);
        if (!Dm.truthy(dot)) //dead or deleted
            return dot;
        if (IS_UNCONSCIOUS_OR_CRIT(this)) // consciousness check
            return dot;

        if (Dm.num(health) < Dm.num(maxHealth))
            Waiting.NoSleep(adjust_brute_loss(-4 * Dm.num(seconds_per_tick))); //Fast life regen

        foreach (mob_living_carbon humanoid_entities in Dm.each<mob_living_carbon>(view(3, this))) //Mood aura which stay as long you do not wear Sanallite as hat or carry(I will try to make it work with hat someday(obviously weaker than normal one))
            Waiting.NoSleep(humanoid_entities.add_mood_event("kobun", typeof(datum_mood_event_kobun)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/pets/dog/dog_subtypes.dm:128
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (user is mob_living living_user && Dm.truthy(living_user.combat_mode) && user.reagents != null && !Dm.truthy(stat))
        {
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), 0.4f);
            await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_vitamin), 0.4f);
        }
        return dot;
    }
}
