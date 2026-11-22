// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/monkeys.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5); the var overrides are generated.

// tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:4
public partial class datum_species_monkey : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:42
    public override async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(human_who_gained_species, old_species, pref_load, regenerate_icons, replace_missing);
        if (Dm.truthy(pref_load))
            ADD_TRAIT(human_who_gained_species, TRAIT_BORN_MONKEY, INNATE_TRAIT); // Not a species trait, you cannot escape your genetic destiny
        Dm.Deferred("code/modules/mob/living/carbon/human/species_types/monkeys.dm:46", "on_species_gain: dna.add_mutation(/datum/mutation/race, MUTATION_SOURCE_ACTIVATED): the monkey mutation (genetics, P3)");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:48
    public override async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(human, new_species, pref_load, __extra);
        Dm.Deferred("code/modules/mob/living/carbon/human/species_types/monkeys.dm:50", "on_species_loss: dna.remove_mutation(/datum/mutation/race, MUTATION_SOURCE_ACTIVATED): the monkey mutation (genetics, P3)");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:52
    public override object check_roundstart_eligible()
    {
        // STOP ADDING MONKEY SUBTYPES YOU HEATHEN
        // ok we killed monkey subtypes but we're keeping this in cause we can't trust you fuckers
        if (Dm.truthy(check_holidays(MONKEYDAY)) && Dm.eq(id, SPECIES_MONKEY))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:59
    public override object get_physical_attributes()
    {
        return "Monkeys are slippery, can crawl into vents, and are more dextrous than humans.. but only when stealing things. Natural monkeys cannot operate machinery or most tools with their paws, but unusually clever monkeys or those that were once something else can.";
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:63
    public override object get_species_description()
    {
        return "Monkeys are a type of primate that exist between humans and animals on the evolutionary chain. Every year, on Monkey Day, Nanotrasen shows their respect for the little guys by allowing them to roam the station freely.";
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:67
    public override object get_species_lore()
    {
        return Dm.list(
            "Monkeys are commonly used as test subjects on board Space Station Thirteen. But what if... for one day... the Monkeys were allowed to be the scientists? What experiments would they come up it? Would they (stereotypically) be related to bananas somehow? There's only one way to find out.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:75
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "spider"),
                Dm.kv(SPECIES_PERK_NAME, "Vent Crawling"),
                Dm.kv(SPECIES_PERK_DESC, "Monkeys can crawl through the vent and scrubber networks while wearing no clothing. Stay out of the kitchen!")),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "paw"),
                Dm.kv(SPECIES_PERK_NAME, "Primal Primate"),
                Dm.kv(SPECIES_PERK_DESC, "Monkeys are primitive humans, and can't do most things a human can do. Computers are impossible, complex machines are right out, and most clothes don't fit your smaller form."))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:97
    public override object create_pref_language_perk()
    {
        DmList to_add = Dm.list();
        // Holding these variables so we can grab the exact names for our perk.
        object common_language = Dm.initial(typeof(datum_language_common), "name");
        object monkey_language = Dm.initial(typeof(datum_language_monkey), "name");

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "comment"),
            Dm.kv(SPECIES_PERK_NAME, "Primitive Tongue"),
            Dm.kv(SPECIES_PERK_DESC, Dm.text("You may be able to understand [], but you can't speak it. You can only speak [].", common_language, monkey_language)))));

        return to_add;
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:114
public partial class obj_item_organ_brain_primate : obj_item_organ_brain //Ook Ook
{
    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:121
    public override object get_replaceability(object new_organ_type = null, object expected_organ_type = null, datum_species old_species = null, float? replace_current = null)
    {
        replace_current ??= TRUE;
        ///Real monkeys retain their ape brains when humanized (further species change can override it). If old_species is null,
        if (HAS_TRAIT(owner, TRAIT_BORN_MONKEY) && (old_species == null || old_species is datum_species_monkey))
            return FALSE;
        return base.get_replaceability(new_organ_type, expected_organ_type, old_species, replace_current);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:147
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        RegisterSignal(organ_owner, COMSIG_LIVING_MOB_BUMPED, nameof(on_mob_bump));
        await organ_owner.AddElement(typeof(datum_element_human_biter));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:152
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        mob_living_carbon primate = organ_owner as mob_living_carbon;
        UnregisterSignal(primate, COMSIG_LIVING_MOB_BUMPED);
        primate.RemoveElement(typeof(datum_element_human_biter));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:157
    public virtual void on_mob_bump(mob source = null, mob_living crossing_mob = null)
    {
        if (!Dm.truthy(tripping) || !Dm.truthy(crossing_mob.combat_mode))
            return;
        if (crossing_mob.mob_size < MOB_SIZE_HUMAN)
            return;
        if (ishuman(crossing_mob))
        {
            mob_living_carbon_human crossing_humie = crossing_mob as mob_living_carbon_human;
            if (crossing_humie.mob_height <= HUMAN_HEIGHT_SHORTEST)
                return;
        }
        Waiting.Detach(crossing_mob.knockOver(owner));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:169
    public override object get_attacking_limb(mob_living_carbon_human target = null)
    {
        if (!HAS_TRAIT(owner, TRAIT_ADVANCEDTOOLUSER) || HAS_TRAIT(owner, TRAIT_FERAL_BITER))
            return owner.get_bodypart(BODY_ZONE_HEAD);
        return base.get_attacking_limb(target);
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:128
public partial class datum_action_item_action_organ_action_toggle_trip : datum_action_item_action_organ_action
{
    // tg: code/modules/mob/living/carbon/human/species_types/monkeys.dm:135
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_organ_brain_primate monkey_brain = target as obj_item_organ_brain_primate;
        if (Dm.truthy(monkey_brain.tripping))
        {
            monkey_brain.tripping = FALSE;
            background_icon_state = "bg_default";
            to_chat(monkey_brain.owner, span_notice("You will now avoid stumbling while colliding with people who are in combat mode."));
        }
        else
        {
            monkey_brain.tripping = TRUE;
            background_icon_state = "bg_default_on";
            to_chat(monkey_brain.owner, span_notice("You will now stumble while colliding with people who are in combat mode."));
        }
        await build_all_button_icons();
        return TRUE;
    }
}
