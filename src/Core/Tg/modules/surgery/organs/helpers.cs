// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/surgery/organs/helpers.dm:7
public partial class mob : atom_movable
{
    /**
     * Get the organ object from the mob matching the passed in typepath
     *
     * Arguments:
     * * typepath The typepath of the organ to get
     */
    // tg: code/modules/surgery/organs/helpers.dm:7
    public virtual object get_organ_by_type(object typepath = null)
    {
        return null;
    }

    /**
     * Get organ objects by zone
     *
     * This will return a list of all the organs that are relevant to the zone that is passedin
     *
     * Arguments:
     * * zone [a BODY_ZONE_X define](https://github.com/tgstation/tgstation/blob/master/code/__DEFINES/combat.dm#L187-L200)
     */
    // tg: code/modules/surgery/organs/helpers.dm:20
    public virtual object get_organs_for_zone(object zone = null, params object[] __extra)
    {
        return null;
    }

    /**
     * Returns a list of all organs in specified slot
     *
     * Arguments:
     * * slot Slot to get the organs from
     */
    // tg: code/modules/surgery/organs/helpers.dm:29
    public virtual object get_organ_slot(object slot = null)
    {
        return null;
    }
}

// tg: code/modules/surgery/organs/helpers.dm:32 (interleaved types, rules 1.9)
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/surgery/organs/helpers.dm:32
    public override object get_organ_by_type(object typepath = null)
    {
        return Dm.locate_type(typepath as Type, organs);
    }

    // tg: code/modules/surgery/organs/helpers.dm:35
    [DmParams("zone", "include_children")]
    public override object get_organs_for_zone(object zone = null, params object[] __extra)
    {
        float? include_children = Dm.arg<float?>(__extra, 0);
        include_children ??= FALSE;
        object valid_organs = Dm.list();
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(organs))
        {
            if (Dm.eq(zone, organ.zone))
                Dm.append(ref valid_organs, organ);
            else if (Dm.truthy(include_children) && Dm.eq(zone, deprecise_zone(organ.zone)))
                Dm.append(ref valid_organs, organ);
        }
        return valid_organs;
    }

    // tg: code/modules/surgery/organs/helpers.dm:44
    public override object get_organ_slot(object slot = null)
    {
        object dot = null;
        dot = organs_slot[slot];
        return dot;
    }
}

// tg: code/modules/surgery/organs/helpers.dm:52 (interleaved types, rules 1.9)
public partial class mob_living_carbon_human : mob_living_carbon
{
    /**
     * Returns a list of all missing organs this species should have
     *
     * list [key] is the ORGAN_SLOT missing an organ, list value is the text name of the slot organ
     */
    // tg: code/modules/surgery/organs/helpers.dm:52
    public virtual object get_missing_organs(float? include_appendix = null)
    {
        include_appendix ??= FALSE;
        DmList missing_organs = Dm.list();
        datum_species species = dna.species as datum_species;

        if (!Dm.truthy(get_organ_slot(ORGAN_SLOT_BRAIN)))
            missing_organs[ORGAN_SLOT_BRAIN] = "Brain";
        if (Dm.truthy(needs_heart()) && !Dm.truthy(get_organ_slot(ORGAN_SLOT_HEART)))
            missing_organs[ORGAN_SLOT_HEART] = "Heart";
        if (!HAS_TRAIT_FROM(this, TRAIT_NOBREATH, SPECIES_TRAIT) && species.mutantlungs != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_LUNGS)))
            missing_organs[ORGAN_SLOT_LUNGS] = "Lungs";
        if (!HAS_TRAIT_FROM(this, TRAIT_LIVERLESS_METABOLISM, SPECIES_TRAIT) && species.mutantliver != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_LIVER)))
            missing_organs[ORGAN_SLOT_LIVER] = "Liver";
        if (!HAS_TRAIT_FROM(this, TRAIT_NOHUNGER, SPECIES_TRAIT) && species.mutantstomach != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_STOMACH)))
            missing_organs[ORGAN_SLOT_STOMACH] = "Stomach";
        if (species.mutanttongue != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_TONGUE)))
            missing_organs[ORGAN_SLOT_TONGUE] = "Tongue";
        if (species.mutantears != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_EARS)))
            missing_organs[ORGAN_SLOT_EARS] = "Ears";
        if (species.mutantears != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_EYES)))
            missing_organs[ORGAN_SLOT_EYES] = "Eyes";
        if (Dm.truthy(include_appendix) && species.mutantappendix != null && !Dm.truthy(get_organ_slot(ORGAN_SLOT_APPENDIX)))
            missing_organs[ORGAN_SLOT_APPENDIX] = "Appendix";

        return missing_organs;
    }
}
