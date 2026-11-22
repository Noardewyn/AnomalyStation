// Translated from tgstation (snapshot 1d99bd7f): code/modules/forensics/forensics_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (blood in the world): the whole file, the atoms' forensics procs (fingerprints, hidden prints, fibres, blood
// DNA and its colour) and their overrides of add_blood_DNA (objects, the blood decals, items, gloves, turfs, closed turfs,
// uniforms, humans, the living). The macros of __HELPERS/forensics.dm (GET_ATOM_BLOOD_DNA and its kin) and add_mob_blood
// (__HELPERS/atoms.dm:431) are the static helpers at the end. Diseases on the blood (add_diseases) are M8's.

// tg: code/modules/forensics/forensics_helpers.dm:2 (interleaved types, rules 1.9)
public partial class atom
{
    /// Adds a list of fingerprints to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:2
    public virtual object add_fingerprint_list(DmList fingerprints_to_add = null) //ASSOC LIST FINGERPRINT = FINGERPRINT
    {
        if (QDELETED(this))
            return null;
        if ((fingerprints_to_add == null))
            return null;
        if (Dm.truthy(forensics))
            forensics.inherit_new(fingerprints: fingerprints_to_add);
        else
            forensics = Dm.@new<datum_forensics>(this, Dm.named("fingerprints", fingerprints_to_add));
        return TRUE;
    }

    /// Adds a single fingerprint to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:14
    public virtual object add_fingerprint(mob suspect = null, object ignoregloves = null) //Set ignoregloves to add prints irrespective of the mob having gloves on.
    {
        ignoregloves ??= FALSE;
        if (QDELETED(this))
            return null;
        if ((forensics == null))
            forensics = Dm.@new<datum_forensics>(this);
        forensics.add_fingerprint(suspect, ignoregloves);
        return TRUE;
    }

    /// Add a list of fibers to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:23
    public virtual object add_fiber_list(DmList fibers_to_add = null) //ASSOC LIST FIBERTEXT = FIBERTEXT
    {
        if (QDELETED(this))
            return null;
        if ((fibers_to_add == null))
            return null;
        if (Dm.truthy(forensics))
            forensics.inherit_new(fibers: fibers_to_add);
        else
            forensics = Dm.@new<datum_forensics>(this, Dm.named("fibers", fibers_to_add));
        return TRUE;
    }

    /// Adds a single fiber to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:35
    public virtual async DmTask<object> add_fibers(mob_living_carbon_human suspect = null)
    {
        if (QDELETED(this))
            return null;
        float old = 0;
        if (Dm.truthy(suspect.gloves) && isclothing(suspect.gloves))
        {
            obj_item_clothing_gloves suspect_gloves = suspect.gloves as obj_item_clothing_gloves;
            old = GET_ATOM_BLOOD_DNA_LENGTH(suspect_gloves);
            if (suspect_gloves.transfer_blood > 1) //bloodied gloves transfer blood to touched objects
            {
                if (Dm.truthy(await add_blood_DNA(GET_ATOM_BLOOD_DNA(suspect_gloves))) && GET_ATOM_BLOOD_DNA_LENGTH(suspect_gloves) > old) //only reduces the bloodiness of our gloves if the item wasn't already bloody
                    suspect_gloves.transfer_blood -= 1;
            }
        }
        else if (suspect.blood_in_hands > 1)
        {
            old = GET_ATOM_BLOOD_DNA_LENGTH(suspect);
            if (Dm.truthy(await add_blood_DNA(GET_ATOM_BLOOD_DNA(suspect))) && GET_ATOM_BLOOD_DNA_LENGTH(suspect) > old)
                suspect.blood_in_hands -= 1;
        }
        if ((forensics == null))
            forensics = Dm.@new<datum_forensics>(this);
        forensics.add_fibers(suspect);
        return TRUE;
    }

    /// Adds a list of hiddenprints to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:55
    public virtual object add_hiddenprint_list(DmList hiddenprints_to_add = null) //NOTE: THIS IS FOR ADMINISTRATION FINGERPRINTS, YOU MUST CUSTOM SET THIS TO INCLUDE CKEY/REAL NAMES! CHECK FORENSICS.DM
    {
        if (QDELETED(this))
            return null;
        if ((hiddenprints_to_add == null))
            return null;
        if (Dm.truthy(forensics))
            forensics.inherit_new(hiddenprints: hiddenprints_to_add);
        else
            forensics = Dm.@new<datum_forensics>(this, Dm.named("hiddenprints", hiddenprints_to_add));
        return TRUE;
    }

    /// Adds a single hiddenprint to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:67
    public virtual object add_hiddenprint(mob suspect = null)
    {
        if (QDELETED(this))
            return null;
        if ((forensics == null))
            forensics = Dm.@new<datum_forensics>(this);
        forensics.add_hiddenprint(suspect);
        return TRUE;
    }

    /// Fetch current blood color
    // tg: code/modules/forensics/forensics_helpers.dm:76
    public virtual object get_blood_dna_color()
    {
        if (Dm.truthy(cached_blood_color))
            return cached_blood_color;

        DmList blood_DNA = GET_ATOM_BLOOD_DECALS(this);
        if (!Dm.truthy(Dm.length(blood_DNA)))
            return null;

        cached_blood_color = Waiting.NoSleep(get_color_from_blood_list(blood_DNA));
        return cached_blood_color;
    }

    /// Check if we have any emissive blood on us
    /// is_worn - When TRUE, we're fetching the value for mob overlays, in which case we bypass the cache
    // tg: code/modules/forensics/forensics_helpers.dm:89
    public virtual object get_blood_emissive_alpha(object is_worn = null)
    {
        is_worn ??= FALSE;
        if (Dm.truthy(cached_blood_emissive) && !Dm.truthy(is_worn))
            return cached_blood_emissive;

        DmList blood_DNA = GET_ATOM_BLOOD_DECALS(this);
        if (!Dm.truthy(Dm.length(blood_DNA)))
            return 0;

        object blood_alpha = 0;
        foreach (object blood_key in Dm.each(blood_DNA))
        {
            datum_blood_type blood_type = blood_DNA[blood_key] as datum_blood_type;
            blood_alpha = Dm.add(blood_alpha, blood_type.get_emissive_alpha(this, Dm.num(is_worn)));
        }

        blood_alpha = Dm.div(blood_alpha, Dm.length(blood_DNA));
        if (!Dm.truthy(is_worn))
            cached_blood_emissive = blood_alpha;
        return blood_alpha;
    }

    /// Adds blood dna to the atom
    // tg: code/modules/forensics/forensics_helpers.dm:108
    public virtual async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null) //ASSOC LIST DNA = BLOODTYPE
    {
        return FALSE;
    }

    /*
     * Transfer all the fingerprints and hidden prints from [src] to [transfer_to].
     */
    // tg: code/modules/forensics/forensics_helpers.dm:252
    public virtual void transfer_fingerprints_to(atom transfer_to = null)
    {
        transfer_to.add_fingerprint_list(GET_ATOM_FINGERPRINTS(this));
        transfer_to.add_hiddenprint_list(GET_ATOM_HIDDENPRINTS(this));
        transfer_to.fingerprintslast = fingerprintslast;
    }

    /*
     * Transfer all the fibers from [src] to [transfer_to].
     */
    // tg: code/modules/forensics/forensics_helpers.dm:260
    public virtual void transfer_fibers_to(atom transfer_to = null)
    {
        transfer_to.add_fiber_list(GET_ATOM_FIBRES(this));
    }

    /// add_mob_blood(from_who) (__HELPERS/atoms.dm:431): add_blood_DNA(from_who.get_blood_dna_list(), from_who.get_static_viruses())
    public async DmTask<object> add_mob_blood(mob_living from_who)
    {
        return await add_blood_DNA(from_who.get_blood_dna_list() as DmList, await from_who.get_static_viruses() as DmList);
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:111
public partial class obj : atom_movable
{
    // tg: code/modules/forensics/forensics_helpers.dm:111
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        if (QDELETED(this))
            return null;
        object dot = await base.add_blood_DNA(blood_DNA_to_add, diseases);
        if ((blood_DNA_to_add == null) || !Dm.truthy(Dm.length(blood_DNA_to_add)))
            return dot;
        foreach (object blood_key in Dm.each(blood_DNA_to_add))
        {
            if ((blood_DNA_to_add[blood_key] == null))
                throw Dm.CRASH(Dm.text("add_blood_DNA on [] ([]) has been passed bad blood_DNA_to_add ([] - [] key-value pair)!", this, type, blood_key, blood_DNA_to_add[blood_key]), "code/modules/forensics/forensics_helpers.dm", 121);
        }
        cached_blood_color = null;
        cached_blood_emissive = null;
        if (Dm.truthy(forensics))
            forensics.inherit_new(blood_DNA: blood_DNA_to_add);
        else
            forensics = Dm.@new<datum_forensics>(this, Dm.named("blood_DNA", blood_DNA_to_add));
        return TRUE;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:130
public partial class obj_effect_decal_cleanable_blood : obj_effect_decal_cleanable
{
    // tg: code/modules/forensics/forensics_helpers.dm:130
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        float first_dna = GET_ATOM_BLOOD_DNA_LENGTH(this);
        if (!Dm.truthy(await base.add_blood_DNA(blood_DNA_to_add, diseases)))
            return FALSE;
        if (Dm.truthy(dried))
            return TRUE;
        // Imperfect, ends up with some blood types being double-set-up, but harmless (for now)
        foreach (object blood_key in Dm.each(blood_DNA_to_add))
        {
            datum_blood_type blood_type = blood_DNA_to_add[blood_key] as datum_blood_type;
            // We shouldn't ever get no valid blood types with BLOOD_COVER_TURFS and first_dna == 0 here so we're safe
            if (Dm.truthy(Dm.bitand(blood_type.blood_flags, BLOOD_COVER_TURFS)))
                await blood_type.set_up_blood(this, first_dna == 0 ? TRUE : FALSE);
        }
        await add_diseases(diseases);
        update_appearance();
        await add_atom_colour(get_blood_dna_color(), FIXED_COLOUR_PRIORITY);
        return TRUE;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:147
public partial class obj_item : obj
{
    // tg: code/modules/forensics/forensics_helpers.dm:147
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        if (Dm.truthy(Dm.bitand(item_flags, NO_BLOOD_ON_ITEM)))
            return FALSE;
        return await base.add_blood_DNA(blood_DNA_to_add, diseases);
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:152
public partial class obj_item_clothing_gloves : obj_item_clothing
{
    // tg: code/modules/forensics/forensics_helpers.dm:152
    public override async DmTask<object> add_blood_DNA(DmList blood_dna = null, DmList diseases = null)
    {
        object dot = await base.add_blood_DNA(blood_dna, diseases);
        if (Dm.truthy(dot) && Dm.truthy(has_blood_flag(blood_dna, BLOOD_COVER_ITEMS)))
            transfer_blood = min(transfer_blood, rand(2, 4));
        return dot;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:157
public partial class turf : atom
{
    // tg: code/modules/forensics/forensics_helpers.dm:157
    public override async DmTask<object> add_blood_DNA(DmList blood_dna = null, DmList diseases = null)
    {
        obj_effect_decal_cleanable_blood_splatter blood_splatter = Dm.locate<obj_effect_decal_cleanable_blood_splatter>(this);
        object blood_flags = has_blood_flag(blood_dna, Dm.bitor(Dm.bitor(BLOOD_COVER_TURFS, BLOOD_ADD_DNA), BLOOD_TRANSFER_VIRAL_DATA));
        if (blood_splatter == null)
        {
            if (Dm.truthy(Dm.bitand(blood_flags, BLOOD_COVER_TURFS)))
                blood_splatter = Dm.@new<obj_effect_decal_cleanable_blood_splatter>(this, diseases, blood_dna);
        }
        else
        {
            if (Dm.truthy(Dm.bitand(blood_flags, BLOOD_ADD_DNA)))
                await blood_splatter.add_blood_DNA(blood_dna);
            if (Dm.truthy(Dm.bitand(blood_flags, BLOOD_TRANSFER_VIRAL_DATA)))
                await blood_splatter.add_diseases(diseases);
        }
        return !QDELETED(blood_splatter) ? blood_splatter : null;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:171
public partial class turf_closed : turf
{
    // tg: code/modules/forensics/forensics_helpers.dm:171
    public override async DmTask<object> add_blood_DNA(DmList blood_dna = null, DmList diseases = null)
    {
        return FALSE;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:174
public partial class obj_item_clothing_under : obj_item_clothing
{
    // tg: code/modules/forensics/forensics_helpers.dm:174
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        object dot = await base.add_blood_DNA(blood_DNA_to_add, diseases);
        if (!Dm.truthy(dot))
            return dot;
        foreach (obj_item_clothing_accessory thing_accessory in Dm.each_cast<obj_item_clothing_accessory>(attached_accessories))
        {
            if (prob(66))
                continue;
            await thing_accessory.add_blood_DNA(blood_DNA_to_add);
        }
        return dot;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:183
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/forensics/forensics_helpers.dm:183
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        return await add_blood_DNA_to_items(blood_DNA_to_add, diseases: diseases);
    }

    /// Adds blood DNA to certain slots the mob is wearing
    // tg: code/modules/forensics/forensics_helpers.dm:187
    public virtual async DmTask<object> add_blood_DNA_to_items(DmList blood_DNA_to_add = null, object target_flags = null, DmList diseases = null)
    {
        target_flags ??= Dm.bitor(Dm.bitor(Dm.bitor(Dm.bitor(ITEM_SLOT_ICLOTHING, ITEM_SLOT_OCLOTHING), ITEM_SLOT_GLOVES), ITEM_SLOT_HEAD), ITEM_SLOT_MASK);
        if (QDELING(this))
            return FALSE;

        if (!Dm.truthy(Dm.length(blood_DNA_to_add)))
            return FALSE;

        // Don't messy up our jumpsuit if we're got a coat
        if (Dm.truthy(Dm.bitand(target_flags, ITEM_SLOT_OCLOTHING)) && wear_suit is obj_item suit && Dm.truthy(Dm.bitand(suit.body_parts_covered, CHEST)))
            target_flags = Dm.bitand(target_flags, Dm.bitnot(ITEM_SLOT_ICLOTHING));

        bool dirty_hands = Dm.truthy(Dm.bitand(target_flags, Dm.bitor(ITEM_SLOT_GLOVES, ITEM_SLOT_HANDS)));
        bool dirty_feet = Dm.truthy(Dm.bitand(target_flags, ITEM_SLOT_FEET));
        object slots_to_bloody = Dm.bitand(target_flags, Dm.bitnot(hidden_slots_to_inventory_slots(covered_slots)));
        DmList all_worn = get_equipped_items() as DmList;
        foreach (obj_item thing in Dm.each_cast<obj_item>(all_worn))
        {
            if (Dm.truthy(Dm.bitand(thing.slot_flags, slots_to_bloody)))
                await thing.add_blood_DNA(blood_DNA_to_add, diseases);
            if (Dm.truthy(Dm.bitand(thing.body_parts_covered, HANDS)))
                dirty_hands = false;
            if (Dm.truthy(Dm.bitand(thing.body_parts_covered, FEET)))
                dirty_feet = false;
        }

        if (Dm.truthy(Dm.bitand(slots_to_bloody, ITEM_SLOT_HANDS)))
        {
            foreach (obj_item thing in Dm.each<obj_item>(held_items))
                await thing.add_blood_DNA(blood_DNA_to_add);
        }

        cached_blood_color = null;
        cached_blood_emissive = null;
        if (!Dm.truthy(has_blood_flag(blood_DNA_to_add, BLOOD_COVER_MOBS)))
        {
            await update_clothing(slots_to_bloody);
            return null;
        }

        if (dirty_hands || dirty_feet || !Dm.truthy(Dm.length(all_worn)))
        {
            if ((forensics == null))
                forensics = Dm.@new<datum_forensics>(this);
            forensics.inherit_new(blood_DNA: blood_DNA_to_add);
        }

        if (dirty_hands)
            blood_in_hands = min(blood_in_hands, rand(2, 4));

        if (dirty_feet)
            await AddComponent(typeof(datum_component_bloodysoles_feet), blood_DNA_to_add); // Add blood to our feet

        await update_clothing(slots_to_bloody);
        return TRUE;
    }
}

// tg: code/modules/forensics/forensics_helpers.dm:240
public partial class mob_living : mob
{
    // tg: code/modules/forensics/forensics_helpers.dm:240
    public override async DmTask<object> add_blood_DNA(DmList blood_DNA_to_add = null, DmList diseases = null)
    {
        if (QDELING(this))
            return FALSE;
        if (!Dm.truthy(Dm.length(blood_DNA_to_add)))
            return FALSE;
        if ((forensics == null))
            forensics = Dm.@new<datum_forensics>(this);
        cached_blood_color = null;
        cached_blood_emissive = null;
        forensics.inherit_new(blood_DNA: blood_DNA_to_add);
        return TRUE;
    }
}

public static partial class GlobalProcs
{
    /// Returns the fingerprints on this atom
    // tg: code/__HELPERS/forensics.dm:2  #define GET_ATOM_FINGERPRINTS(atom) atom.forensics?.fingerprints
    public static DmList GET_ATOM_FINGERPRINTS(atom a) => a.forensics?.fingerprints;

    /// Returns the hidden prints on this atom
    // tg: code/__HELPERS/forensics.dm:4  #define GET_ATOM_HIDDENPRINTS(atom) atom.forensics?.hiddenprints
    public static DmList GET_ATOM_HIDDENPRINTS(atom a) => a.forensics?.hiddenprints;

    /// Returns the blood dna on this atom
    // tg: code/__HELPERS/forensics.dm:6  #define GET_ATOM_BLOOD_DNA(atom) atom.forensics?.blood_DNA
    public static DmList GET_ATOM_BLOOD_DNA(atom a) => a.forensics?.blood_DNA;

    /// Returns visible blood dna on this atom
    // tg: code/__HELPERS/forensics.dm:8  #define GET_ATOM_BLOOD_DECALS(atom) atom.forensics?.get_visible_blood()
    public static DmList GET_ATOM_BLOOD_DECALS(atom a) => a.forensics?.get_visible_blood();

    /// Returns the fibers on this atom
    // tg: code/__HELPERS/forensics.dm:10  #define GET_ATOM_FIBRES(atom) atom.forensics?.fibers
    public static DmList GET_ATOM_FIBRES(atom a) => a.forensics?.fibers;

    /// Returns the number of unique blood dna sources on this atom
    // tg: code/__HELPERS/forensics.dm:12  #define GET_ATOM_BLOOD_DNA_LENGTH(atom) ((atom.forensics == null) ? 0 : length(atom.forensics.blood_DNA))
    public static float GET_ATOM_BLOOD_DNA_LENGTH(atom a) => a.forensics == null ? 0 : Dm.length(a.forensics.blood_DNA);

    /// Returns the number of *visible* blood dna on this atom
    // tg: code/__HELPERS/forensics.dm:14  #define GET_ATOM_BLOOD_DECAL_LENGTH(atom) ((atom.forensics == null) ? 0 : length(atom.forensics.get_visible_blood()))
    public static float GET_ATOM_BLOOD_DECAL_LENGTH(atom a) => a.forensics == null ? 0 : Dm.length(a.forensics.get_visible_blood());
}
