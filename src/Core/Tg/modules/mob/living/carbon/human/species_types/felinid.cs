// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/felinid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: the felinid and purrbation. The felinid is the human body with the cat's ears and tail drawn from tg's
// sprites (their bodypart overlays, hair coloured).

//Subtype of human
// tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:2
public partial class datum_species_human_felinid : datum_species_human
{
    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:27
    public override async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        regenerate_icons ??= TRUE;
        replace_missing ??= TRUE;
        if (!Dm.truthy(pref_load)) //Hah! They got forcefully purrbation'd. Force default felinid parts on them if they have no mutant parts in those areas!
        {
            if (Dm.eq(human_who_gained_species.dna.features[FEATURE_TAIL_CAT], SPRITE_ACCESSORY_NONE))
                human_who_gained_species.dna.features[FEATURE_TAIL_CAT] = get_consistent_feature_entry(SSaccessories.feature_list[FEATURE_TAIL_CAT] as DmList);
            if (Dm.eq(human_who_gained_species.dna.features[FEATURE_EARS], SPRITE_ACCESSORY_NONE))
                human_who_gained_species.dna.features[FEATURE_EARS] = get_consistent_feature_entry(SSaccessories.feature_list[FEATURE_EARS] as DmList);
        }

        // Swapping out feline ears for normal ol' human ears if they have invisible cat ears.
        if (Dm.eq(human_who_gained_species.dna.features[FEATURE_EARS], SPRITE_ACCESSORY_NONE))
            mutantears = typeof(obj_item_organ_ears);
        await base.on_species_gain(human_who_gained_species, old_species, pref_load, regenerate_icons, replace_missing);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:120
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human human_for_preview = null)
    {
        human_for_preview.set_haircolor("#ffcccc", update: FALSE); // pink
        human_for_preview.set_hairstyle("Hime Cut", update: TRUE);

        if (human_for_preview.get_organ_by_type(typeof(obj_item_organ_ears_cat)) is obj_item_organ_ears_cat cat_ears)
        {
            cat_ears.color = human_for_preview.hair_color;
            human_for_preview.update_hair();
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:129
    public override object get_physical_attributes()
    {
        return I18n.T("Felinids are very similar to humans in almost all respects, with their biggest differences being the ability to lick their wounds, and an increased sensitivity to noise, which is often detrimental. They are also rather fond of eating oranges.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:133
    public override object get_species_description()
    {
        return I18n.T("Felinids are one of the many types of bespoke genetic modifications to come of humanity's mastery of genetic science, and are also one of the most common. Meow?");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:138
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Bio-engineering at its felinest, Felinids are the peak example of humanity's mastery of genetic code. One of many \"Animalid\" variants, Felinids are the most popular and common, as well as one of the biggest points of contention in genetic-modification."),

            I18n.T("Body modders were eager to splice human and feline DNA in search of the holy trifecta: ears, eyes, and tail. These traits were in high demand, with the corresponding side effects of vocal and neurochemical changes being seen as a minor inconvenience."),

            I18n.T("Sadly for the Felinids, they were not minor inconveniences. Shunned as subhuman and monstrous by many, Felinids (and other Animalids) sought their greener pastures out in the colonies, cloistering in communities of their own kind. As a result, outer Human space has a high Animalid population."));
    }

    // Felinids are subtypes of humans.
    // This shouldn't call parent or we'll get a buncha human related perks (though it doesn't have a reason to).
    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:154
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "grin-tongue"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Grooming")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Felinids can lick wounds to reduce bleeding."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, FA_ICON_PERSON_FALLING),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Catlike Grace")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Felinids have catlike instincts allowing them to land upright on their feet.  Instead of being knocked down from falling, you only receive a short slowdown. However, they do not have catlike legs, and the fall will deal additional damage."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "assistive-listening-systems"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Sensitive Hearing")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Felinids are more sensitive to loud sounds, such as flashbangs."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "shower"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Hydrophobia")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Felinids don't like getting soaked with water."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, FA_ICON_ANGRY),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("'Fight or Flight' Defense Response")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Felinids who become mentally unstable (and deprived of food) exhibit an extreme 'fight or flight' response against aggressors. They sometimes bite people. Violently.")))));
        return to_add;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:39 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:39
    public static async DmTask mass_purrbation()
    {
        foreach (object mob in Dm.each(GLOB.human_list))
        {
            await purrbation_apply(mob as mob_living_carbon_human);
            CHECK_TICK();
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:44
    public static async DmTask mass_remove_purrbation()
    {
        foreach (object mob in Dm.each(GLOB.human_list))
        {
            await purrbation_remove(mob as mob_living_carbon_human);
            CHECK_TICK();
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:49
    public static async DmTask<object> purrbation_toggle(mob_living_carbon_human target_human = null, float? silent = null)
    {
        silent ??= FALSE;
        object dot = null;
        if (!ishuman(target_human))
            return dot;
        if (target_human.get_organ_slot(ORGAN_SLOT_EARS) is not obj_item_organ_ears_cat)
        {
            await purrbation_apply(target_human, silent: silent);
            dot = TRUE;
        }
        else
        {
            await purrbation_remove(target_human, silent: silent);
            dot = FALSE;
        }
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:59
    public static async DmTask purrbation_apply(mob_living_carbon_human soon_to_be_felinid = null, float? silent = null)
    {
        silent ??= FALSE;
        if (!ishuman(soon_to_be_felinid) || isfelinid(soon_to_be_felinid))
            return;
        if (ishumanbasic(soon_to_be_felinid))
        {
            soon_to_be_felinid.set_species(typeof(datum_species_human_felinid));
            datum_species_human_felinid cat_species = (datum_species_human_felinid)soon_to_be_felinid.dna.species;
            cat_species.original_felinid = FALSE;
        }
        else
        {
            // This removes the spines if they exist
            if (soon_to_be_felinid.get_organ_slot(ORGAN_SLOT_EXTERNAL_SPINES) is obj_item_organ_spines current_spines)
            {
                await current_spines.Remove(soon_to_be_felinid, special: TRUE);
                qdel(current_spines);
            }

            // Without this line the tails would be invisible. This is because cat tail and ears default to None.
            // Humans get converted directly to felinids, and the key is handled in on_species_gain.
            // Now when we get mob.dna.features[feature_key], it returns None, which is why the tail is invisible.
            // stored_feature_id is only set once (the first time an organ is inserted), so this should be safe.
            obj_item_organ_ears_cat kitty_ears = Dm.@new<obj_item_organ_ears_cat>();
            await kitty_ears.Insert(soon_to_be_felinid, special: TRUE, movement_flags: DELETE_IF_REPLACED);
            if (Dm.truthy(should_visual_organ_apply_to(typeof(obj_item_organ_tail_cat), soon_to_be_felinid))) //only give them a tail if they actually have sprites for it / are a compatible subspecies.
            {
                obj_item_organ_tail_cat kitty_tail = Dm.@new<obj_item_organ_tail_cat>();
                await kitty_tail.Insert(soon_to_be_felinid, special: TRUE, movement_flags: DELETE_IF_REPLACED);
            }
        }

        if (!Dm.truthy(silent))
        {
            to_chat(soon_to_be_felinid, span_boldnotice(I18n.T("Something is nya~t right.")));
            playsound(get_turf(soon_to_be_felinid), Dm.resource("sound/effects/meow1.ogg"), 50, TRUE, -1);
        }
    }

    // tg: code/modules/mob/living/carbon/human/species_types/felinid.dm:87
    public static async DmTask purrbation_remove(mob_living_carbon_human purrbated_human = null, float? silent = null)
    {
        silent ??= FALSE;
        if (isfelinid(purrbated_human))
        {
            datum_species_human_felinid cat_species = (datum_species_human_felinid)purrbated_human.dna.species;
            if (Dm.truthy(cat_species.original_felinid))
                return; // Don't display the to_chat message
            purrbated_human.set_species(typeof(datum_species_human));
        }
        else if (ishuman(purrbated_human) && !ishumanbasic(purrbated_human))
        {
            datum_species target_species = (datum_species)purrbated_human.dna.species;

            // From the previous check we know they're not a felinid, therefore removing cat ears and tail is safe
            obj_item_organ_tail old_tail = purrbated_human.get_organ_slot(ORGAN_SLOT_EXTERNAL_TAIL) as obj_item_organ_tail;
            if (old_tail is obj_item_organ_tail_cat)
            {
                await old_tail.Remove(purrbated_human, special: TRUE);
                qdel(old_tail);
                // Locate does not work on assoc lists, so we do it by hand
                foreach (object external_organ in Dm.each(target_species.mutant_organs))
                {
                    if (!Dm.truthy(should_visual_organ_apply_to(external_organ, purrbated_human)))
                        continue;
                    if (ispath(external_organ, typeof(obj_item_organ_tail)))
                    {
                        obj_item_organ_tail new_tail = Dm.@new((Type)external_organ) as obj_item_organ_tail;
                        await new_tail.Insert(purrbated_human, special: TRUE, movement_flags: DELETE_IF_REPLACED);
                    }
                    // Don't forget the spines we removed earlier
                    else if (ispath(external_organ, typeof(obj_item_organ_spines)))
                    {
                        obj_item_organ_spines new_spines = Dm.@new((Type)external_organ) as obj_item_organ_spines;
                        await new_spines.Insert(purrbated_human, special: TRUE, movement_flags: DELETE_IF_REPLACED);
                    }
                }
            }

            if (purrbated_human.get_organ_slot(ORGAN_SLOT_EARS) is obj_item_organ_ears_cat)
            {
                obj_item_organ new_ears = Dm.@new(target_species.mutantears) as obj_item_organ;
                await new_ears.Insert(purrbated_human, special: TRUE, movement_flags: DELETE_IF_REPLACED);
            }
        }
        if (!Dm.truthy(silent))
            to_chat(purrbated_human, span_boldnotice(I18n.T("You are no longer a cat.")));
    }
}
