// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/ghost.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the spirit (a legless floating human) and the ghost subtype, who can toggle passthrough to phase
// through everything at the price of dropping all that cannot phase. The var blocks are generated (D111).

///Spirit mob that lacks legs but still roams the station as part of the unliving.
// tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:3
public partial class datum_species_spirit : datum_species
{
    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:44
    public override object check_roundstart_eligible()
    {
        if (Dm.truthy(check_holidays(HALLOWEEN)) && Dm.truthy(halloween_exclusive))
            return TRUE;
        return base.check_roundstart_eligible();
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:49
    public override object get_physical_attributes()
    {
        return I18n.T("Spirits are the spiritual remains of long-passed entities. They lack legs, can fly, but still eat, breathe, hear and see.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:52
    public override object get_species_description()
    {
        return I18n.T("Spirits are spirits of long-dead creatures whom, for one reason or another, still roam around.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:55
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Spirits are the non-physical remains that linger onto their mortal coil. They still need their protein and organs to keep themselves \"alive\", which leads to many of them still believing they are still part of the living, whether or not they are is a very open-ended debate between philosophers."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:63
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "body"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Leg-less")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Ghosts lack legs and float, preventing you from falling into holes in the ground.")))));

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "shoe-prints"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("No Feet")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("You lack feet, therefore the ability to wear any shoes!")))));

        return to_add;
    }
}

/**
 * Ghost subtype
 * This is the type of ghost that can actually phase through walls,
 * exclusive to magic mirrors & admins, as roundstart-ability to phase anywhere
 * is not something that is generally fun to play against.
 */
// tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:89
public partial class datum_species_spirit_ghost : datum_species_spirit
{
    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:118
    public override object get_physical_attributes()
    {
        return I18n.T("Ghosts are the spiritual remains of long-passed entities. They lack legs, can fly, can choose at will to become incorporeal, but still eat, breathe, hear and see.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:122
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Ghosts are one of the spookiest creatures known in the galaxy. While they still need their protein to sustain themselves, they are able to control their own bodies, going through walls and getting rid of all their posessions at will. Most knowledge known about them is kept secret by Nanotrasen's top Chaplains, who are keen to keep it private."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:131
    public override async DmTask on_species_gain(mob_living_carbon_human new_ghost = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(new_ghost, old_species, pref_load, regenerate_icons, replace_missing);
        passthrough_ability = Dm.@new<datum_action_innate_toggle_passthrough>(this) as datum_action_innate_toggle_passthrough;
        await passthrough_ability.Grant(new_ghost);
        foreach (datum_atom_hud_alternate_appearance_basic_blessed_aware blessed_hud in Dm.each<datum_atom_hud_alternate_appearance_basic_blessed_aware>(GLOB.active_alternate_appearances))
            blessed_hud.check_hud(new_ghost);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:138
    public override async DmTask on_species_loss(mob_living_carbon_human former_ghost = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        await base.on_species_loss(former_ghost, new_species, pref_load, __extra);
        QDEL_NULL(ref passthrough_ability);
        //this has to be called after parent so inherent traits is cleared before we update our HUDs
        foreach (datum_atom_hud_alternate_appearance_basic_blessed_aware blessed_hud in Dm.each<datum_atom_hud_alternate_appearance_basic_blessed_aware>(GLOB.active_alternate_appearances))
            blessed_hud.check_hud(former_ghost);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:145
    public override object create_pref_unique_perks()
    {
        DmList to_add = base.create_pref_unique_perks() as DmList;

        Dm.append(ref to_add, Dm.list(Dm.list(
            Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
            Dm.kv(SPECIES_PERK_ICON, "ghost"),
            Dm.kv(SPECIES_PERK_NAME, I18n.T("Incorporeal")),
            Dm.kv(SPECIES_PERK_DESC, I18n.T("Ghost are able to control their body to the extent where you can willingly make yourself able to phase through anything, including your own equipment.")))));

        return to_add;
    }
}

/**
 * Passthrough ability
 *
 * Ghost innate ability that allows them to enter ghost mode,
 * which gives them the phasing ability, but makes them drop everything
 * and completely unable to wear anything.
 */
// tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:166
public partial class datum_action_innate_toggle_passthrough : datum_action_innate
{
    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:171
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        RegisterSignal(grant_to, COMSIG_CARBON_POST_ATTACH_LIMB, nameof(on_new_limb));
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:175
    public override async DmTask Remove(mob remove_from = null)
    {
        await swap_mode(force_off: TRUE);
        UnregisterSignal(remove_from, COMSIG_CARBON_POST_ATTACH_LIMB);
        await base.Remove(remove_from);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:180
    public override async DmTask<object> Activate()
    {
        object dot = await base.Activate();
        if (!iscarbon(owner))
            return dot;
        await swap_mode();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:186
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (!isliving(owner))
            return FALSE;
        mob_living living_owner = (mob_living)owner;
        if (Dm.truthy(living_owner.has_reagent(typeof(datum_reagent_water_holywater))))
            return FALSE;
        //technically you can trap a ghost by blessing them as theyre phasing,
        //but they can still be dragged out.
        turf owner_turf = get_turf(owner);
        if (HAS_TRAIT(owner_turf, TRAIT_TURF_BLESSED))
            return FALSE;
        obj_item_bodypart_chest their_chest = living_owner.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart_chest;
        if (their_chest == null || !Dm.truthy(Dm.bitand(their_chest.bodytype, BODYTYPE_GHOST)))
            return FALSE;
        return await base.IsAvailable(feedback);
    }

    ///Called when the owner of this action gets a new limb, if it isn't a ghost-limb
    ///the action will turn itself off, and you'll lose said limb if you try using this action again.
    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:204
    [SignalHandler]
    public virtual void on_new_limb(mob source = null, obj_item_bodypart new_part = null, object special = null)
    {
        if (!iscarbon(owner))
            return;
        if (!Dm.truthy(Dm.bitand(new_part.bodytype, BODYTYPE_GHOST)))
            Waiting.NoSleep(swap_mode(force_off: TRUE));
    }

    ///Swaps the mode, allowing us to phase through stuff but drops everything. Optional 'force_off' arg to prevent being able to turn it on.
    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:212
    public virtual async DmTask swap_mode(object force_off = null)
    {
        //we can only turn off, early return if we're trying to turn it on instead.
        if (Dm.truthy(force_off) && Dm.truthy(HAS_TRAIT_FROM(owner, TRAIT_NO_FLOATING_ANIM, SPECIES_TRAIT)))
            return;

        mob_living_carbon carbon_owner = (mob_living_carbon)owner;
        datum_species carbon_species = (datum_species)carbon_owner.dna.species;

        //drop any limbs & organs that can't phase, now that you're phasing.
        foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(carbon_owner.bodyparts.Copy()))
        {
            if (!Dm.truthy(Dm.bitand(bodypart.bodytype, BODYTYPE_GHOST)))
                await bodypart.drop_limb(special: FALSE, dismembered: FALSE, move_to_floor: TRUE);
        }
        foreach (obj_item_organ organ in Dm.each<obj_item_organ>(carbon_owner.organs.Copy()))
        {
            if (!Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_GHOST)))
                await organ.Remove(carbon_owner, special: FALSE);
        }

        if (Dm.truthy(HAS_TRAIT_FROM(carbon_owner, TRAIT_NO_FLOATING_ANIM, SPECIES_TRAIT)))
        {
            REMOVE_TRAIT(carbon_owner, TRAIT_NO_FLOATING_ANIM, SPECIES_TRAIT);
            carbon_owner.add_traits(Dm.list(
                TRAIT_MOVE_PHASING,
                TRAIT_PIERCEIMMUNE,
                TRAIT_INVISIBLE_TO_CAMERA,
                TRAIT_HANDS_BLOCKED, //MOBILITY_USE | MOBILITY_PICKUP | MOBILITY_STORAGE
                TRAIT_PULL_BLOCKED, //MOBILITY_PULL
                TRAIT_UI_BLOCKED //MOBILITY_UI
                ), SPECIES_TRAIT);
            await carbon_species.update_no_equip_flags(carbon_owner, ALL);
            RegisterSignal(carbon_owner, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, nameof(attempt_move));
        }
        else
        {
            ADD_TRAIT(carbon_owner, TRAIT_NO_FLOATING_ANIM, SPECIES_TRAIT);
            carbon_owner.remove_traits(Dm.list(
                TRAIT_MOVE_PHASING,
                TRAIT_PIERCEIMMUNE,
                TRAIT_INVISIBLE_TO_CAMERA,
                TRAIT_HANDS_BLOCKED,
                TRAIT_PULL_BLOCKED,
                TRAIT_UI_BLOCKED
                ), SPECIES_TRAIT);
            await carbon_species.update_no_equip_flags(carbon_owner, Dm.initial(carbon_species, (datum_species s) => s.no_equip_flags));
            UnregisterSignal(carbon_owner, COMSIG_MOB_CLIENT_PRE_LIVING_MOVE);
        }
    }

    ///Called when attempting to move to a new tile while the action is active, returns to cancel moving.
    // tg: code/modules/mob/living/carbon/human/species_types/ghost.dm:254
    [SignalHandler]
    public virtual object attempt_move(mob source = null, atom new_loc = null, object direct = null)
    {
        if (new_loc != null && HAS_TRAIT(new_loc, TRAIT_TURF_BLESSED))
        {
            to_chat(source, span_warning(I18n.T("Holy energies block your path!")));
            return COMSIG_MOB_CLIENT_BLOCK_PRE_LIVING_MOVE;
        }
        return null;
    }
}
