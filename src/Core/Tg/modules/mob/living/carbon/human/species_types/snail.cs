// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/snail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the snailperson and its shell, the armoured backpack it cannot take off. Its slime trail
// is the lube_walking element (datums/elements/lube_walking.cs), its speed the crawling movespeed modifier. The var blocks are generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/snail.dm:1
public partial class datum_species_snail : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:30
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human human = null)
    {
        human.dna.features[FEATURE_MUTANT_COLOR] = COLOR_BEIGE;
        await human.update_body(TRUE); // update_body(is_creating = TRUE)
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:34
    public override object get_physical_attributes()
    {
        return I18n.T("Snailpeople emit a viscous, slippery ooze when crawling along the ground, which they are somewhat faster at than other species. They are almost purely made of water, making them extremely susceptible to shocks, and salt will scour them heavily.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:38
    public override object get_species_description()
    {
        return I18n.T("Snailpeople are viscous, slimy beings with a shell on their back.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:41
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Normally, Snailpeople are a result of a genetic experiment gone wrong, but they have since become recognized species in their own right, similar to the Felinid."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:47
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();
        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_RUNNING),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Turbo")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Snailpeople walk and run very slow, but crawl around very fast. While crawling, they leave a trail of slippery slime behind them.")))));
        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_SHRIMP),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Shelled")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("The back of a Snailperson is covered in an armored shell, which is effectively an unremovable backpack. Good for keeping your things from being stolen, but bad for needing to wear a MODsuit, or disguising.")))));
        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_SKULL_CROSSBONES),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Salty")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Being evolved from Snails, Snailpeople are extremely susceptible to salt, which burns them.")))));
        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, FA_ICON_HAND_FIST),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Squishy")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Snailpeople are squishy and slimy, making their punches and kicks far less effective than other species.")))));
        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:76
    public override async DmTask on_species_gain(mob_living_carbon_human new_snailperson = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(new_snailperson, old_species, pref_load, regenerate_icons, replace_missing);
        obj_item_storage_backpack bag = new_snailperson.get_item_by_slot(ITEM_SLOT_BACK) as obj_item_storage_backpack;
        if (!(bag is obj_item_storage_backpack_snail))
            await new_snailperson.equip_to_slot_or_del(Dm.@new<obj_item_storage_backpack_snail>(new_snailperson), ITEM_SLOT_BACK);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:82
    public override async DmTask on_species_loss(mob_living_carbon_human former_snailperson = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(former_snailperson, new_species, pref_load, __extra);
        obj_item_storage_backpack bag = former_snailperson.get_item_by_slot(ITEM_SLOT_BACK) as obj_item_storage_backpack;
        if (bag is obj_item_storage_backpack_snail)
        {
            await bag.emptyStorage();
            await former_snailperson.temporarilyRemoveItemFromInventory(bag, TRUE);
            qdel(bag);
        }
    }
}

// tg: code/modules/mob/living/carbon/human/species_types/snail.dm:91
public partial class obj_item_storage_backpack_snail : obj_item_storage_backpack
{
    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:112
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        await emptyStorage();
        if (!QDELETED(this))
            qdel(this);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/snail.dm:118
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, INNATE_TRAIT);
        return dot;
    }
}
