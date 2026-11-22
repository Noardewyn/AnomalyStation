// Translated from tgstation (snapshot 1d99bd7f): code/modules/forensics/_forensics.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 1 (blood in the world; owner 2026-10-16: the whole forensics datum): the whole file. The fingerprints, the hidden
// prints, the blood DNA (which colours tg's blood decals) and the clothing fibres of an atom. The var block is generated (D111).

/**
 * Forensics datum
 *
 * Placed onto atoms, and contains:
 * * List of fingerprints on the atom
 * * List of hidden prints (used for admins)
 * * List of blood on the atom
 * * List of clothing fibers on the atom
 */
// tg: code/modules/forensics/_forensics.dm:10
[DmPath("/datum/forensics")]
public partial class datum_forensics : datum
{
    // tg: code/modules/forensics/_forensics.dm:42
    [DmParams("parent", "fingerprints", "hiddenprints", "blood_DNA", "fibers")]
    public override async DmTask New(params object[] __extra)
    {
        atom parent = Dm.arg<atom>(__extra, 0);
        DmList fingerprints = Dm.arg<DmList>(__extra, 1);
        DmList hiddenprints = Dm.arg<DmList>(__extra, 2);
        DmList blood_DNA = Dm.arg<DmList>(__extra, 3);
        DmList fibers = Dm.arg<DmList>(__extra, 4);
        if (!isatom(parent))
        {
            stack_trace("We tried adding a forensics datum to something that isnt an atom. What the hell are you doing?", "code/modules/forensics/_forensics.dm", 44);
            qdel(this);
            return;
        }

        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(clean_act));

        this.parent = parent;
        this.fingerprints = fingerprints;
        this.hiddenprints = hiddenprints;
        this.blood_DNA = blood_DNA;
        this.fibers = fibers;
        check_blood();
    }

    /// Merges the given lists into the preexisting values
    // tg: code/modules/forensics/_forensics.dm:57
    public virtual void inherit_new(DmList fingerprints = null, DmList hiddenprints = null, DmList blood_DNA = null, DmList fibers = null) //Use of | and |= being different here is INTENTIONAL.
    {
        if (Dm.truthy(fingerprints))
            this.fingerprints = LAZY_LISTS_OR(this.fingerprints, fingerprints) as DmList;
        if (Dm.truthy(hiddenprints))
            this.hiddenprints = LAZY_LISTS_OR(this.hiddenprints, hiddenprints) as DmList;
        if (Dm.truthy(blood_DNA))
            this.blood_DNA = LAZY_LISTS_OR(this.blood_DNA, blood_DNA) as DmList;
        if (Dm.truthy(fibers))
            this.fibers = LAZY_LISTS_OR(this.fibers, fibers) as DmList;
        check_blood();
    }

    // tg: code/modules/forensics/_forensics.dm:68
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT);
        parent = null;
        return base.Destroy(force);
    }

    /// Empties the fingerprints list
    // tg: code/modules/forensics/_forensics.dm:74
    public virtual object wipe_fingerprints()
    {
        if ((fingerprints == null))
            return NONE;

        fingerprints = null;
        return COMPONENT_CLEANED;
    }

    /// Empties the blood_DNA list
    // tg: code/modules/forensics/_forensics.dm:82
    public virtual object wipe_blood_DNA()
    {
        if ((blood_DNA == null))
            return NONE;

        blood_DNA = null;
        return COMPONENT_CLEANED;
    }

    /// Empties the fibers list
    // tg: code/modules/forensics/_forensics.dm:90
    public virtual object wipe_fibers()
    {
        if ((fibers == null))
            return NONE;

        fibers = null;
        return COMPONENT_CLEANED;
    }

    /// Handles cleaning up the various forensic types
    // tg: code/modules/forensics/_forensics.dm:98  (SIGNAL_HANDLER is dropped, rules 7.9)
    public virtual object clean_act(datum source = null, object clean_types = null)
    {
        object dot = NONE;
        if (Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_FINGERPRINTS)))
            dot = Dm.bitor(dot, wipe_fingerprints());
        if (Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
            dot = Dm.bitor(dot, wipe_blood_DNA());
        if (Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_FIBERS)))
            dot = Dm.bitor(dot, wipe_fibers());
        return dot;
    }

    /// Adds the given list into fingerprints
    // tg: code/modules/forensics/_forensics.dm:110
    public virtual object add_fingerprint_list(DmList fingerprints = null)
    {
        if (!Dm.truthy(Dm.length(fingerprints)))
            return null;
        LAZYINITLIST(ref this.fingerprints);
        foreach (object fingerprint in Dm.each(fingerprints)) //We use an associative list, make sure we don't just merge a non-associative list into ours.
            this.fingerprints[fingerprint] = fingerprint;
        return TRUE;
    }

    /// Adds a single fingerprint
    // tg: code/modules/forensics/_forensics.dm:119
    public virtual object add_fingerprint(mob suspect = null, object ignoregloves = null)
    {
        ignoregloves ??= FALSE;
        if (!isliving(suspect))
        {
            if (!iseyemob(suspect))
                return null;
            if (isaicamera(suspect))
            {
                mob_eye_camera_ai ai_camera = (mob_eye_camera_ai)suspect;
                if (!Dm.truthy(ai_camera.ai))
                    return null;
                suspect = ai_camera.ai as mob;
            }
        }
        add_hiddenprint(suspect);
        if (ishuman(suspect))
        {
            mob_living_carbon_human human_suspect = (mob_living_carbon_human)suspect;
            add_fibers(human_suspect);
            obj_item gloves = human_suspect.gloves as obj_item;
            if (Dm.truthy(gloves)) //Check if the gloves (if any) hide fingerprints
            {
                if (!Dm.truthy(Dm.bitand(gloves.body_parts_covered, HANDS)) || HAS_TRAIT(gloves, TRAIT_FINGERPRINT_PASSTHROUGH) || HAS_TRAIT(human_suspect, TRAIT_FINGERPRINT_PASSTHROUGH))
                    ignoregloves = TRUE;
                if (!Dm.truthy(ignoregloves))
                {
                    ((obj_item)human_suspect.gloves).add_fingerprint(human_suspect, ignoregloves: TRUE); //ignoregloves = TRUE to avoid infinite loop.
                    return null;
                }
            }
            // DEVIATION(DV83): an inert human (D112: Meta's monkeys until C5, whose Initialize never made their DNA) has no identity to print
            if (human_suspect.dna == null && InertSeam.IsInert(human_suspect))
                return TRUE;
            string full_print = md5(human_suspect.dna.unique_identity);
            LAZYSET(ref fingerprints, full_print, full_print);
        }
        return TRUE;
    }

    /// Adds the given list into fibers
    // tg: code/modules/forensics/_forensics.dm:145
    public virtual object add_fiber_list(DmList fibers = null)
    {
        if (!Dm.truthy(Dm.length(fibers)))
            return null;
        LAZYINITLIST(ref this.fibers);
        foreach (object fiber in Dm.each(fibers)) //We use an associative list, make sure we don't just merge a non-associative list into ours.
            this.fibers[fiber] = fiber;
        return TRUE;
    }

    // ITEM_FIBER_MULTIPLIER 1.2 and NON_ITEM_FIBER_MULTIPLIER 1 (_forensics.dm:154-155), file-local defines

    /// Adds a single fiber
    // tg: code/modules/forensics/_forensics.dm:158
    public virtual object add_fibers(mob_living_carbon_human suspect = null)
    {
        string fibertext;
        float item_multiplier = isitem(parent) ? 1.2f : 1f;
        obj_item wear_suit = suspect.wear_suit as obj_item;
        obj_item w_uniform = suspect.w_uniform as obj_item;
        obj_item gloves = suspect.gloves as obj_item;
        if (Dm.truthy(wear_suit))
        {
            fibertext = Dm.text("Material from \\a [].", wear_suit);
            if (prob(10 * item_multiplier) && !Dm.truthy(LAZYACCESS(fibers, fibertext)))
                LAZYSET(ref fibers, fibertext, fibertext);
            if (!Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, CHEST)))
            {
                if (Dm.truthy(w_uniform))
                {
                    fibertext = Dm.text("Fibers from \\a [].", w_uniform);
                    if (prob(12 * item_multiplier) && !Dm.truthy(LAZYACCESS(fibers, fibertext))) //Wearing a suit means less of the uniform exposed.
                        LAZYSET(ref fibers, fibertext, fibertext);
                }
            }
            if (!Dm.truthy(Dm.bitand(wear_suit.body_parts_covered, HANDS)))
            {
                if (Dm.truthy(gloves))
                {
                    fibertext = Dm.text("Material from a pair of [].", gloves.name);
                    if (prob(20 * item_multiplier) && !Dm.truthy(LAZYACCESS(fibers, fibertext)))
                        LAZYSET(ref fibers, fibertext, fibertext);
                }
            }
        }
        else if (Dm.truthy(w_uniform))
        {
            fibertext = Dm.text("Fibers from \\a [].", w_uniform);
            if (prob(15 * item_multiplier) && !Dm.truthy(LAZYACCESS(fibers, fibertext)))
                LAZYSET(ref fibers, fibertext, fibertext);
            if (Dm.truthy(gloves))
            {
                fibertext = Dm.text("Material from a pair of [].", gloves.name);
                if (prob(20 * item_multiplier) && !Dm.truthy(LAZYACCESS(fibers, fibertext)))
                    LAZYSET(ref fibers, fibertext, fibertext);
            }
        }
        else if (Dm.truthy(gloves))
        {
            fibertext = Dm.text("Material from a pair of [].", gloves.name);
            if (prob(20 * item_multiplier) && !Dm.truthy(LAZYACCESS(fibers, fibertext)))
                LAZYSET(ref fibers, fibertext, fibertext);
        }
        return TRUE;
    }

    /// Adds the given list into hiddenprints
    // tg: code/modules/forensics/_forensics.dm:193
    public virtual object add_hiddenprint_list(DmList hiddenprints = null) //list(ckey = text)
    {
        if (!Dm.truthy(Dm.length(hiddenprints)))
            return null;
        LAZYINITLIST(ref this.hiddenprints);
        foreach (object hidden_print in Dm.each(hiddenprints)) //We use an associative list, make sure we don't just merge a non-associative list into ours.
            this.hiddenprints[hidden_print] = hiddenprints[hidden_print];
        return TRUE;
    }

    /// Adds a single hiddenprint
    // tg: code/modules/forensics/_forensics.dm:202
    public virtual object add_hiddenprint(mob suspect = null)
    {
        if (!isliving(suspect))
        {
            if (!iseyemob(suspect))
                return null;
            if (isaicamera(suspect))
            {
                mob_eye_camera_ai ai_camera = (mob_eye_camera_ai)suspect;
                if (!Dm.truthy(ai_camera.ai))
                    return null;
                suspect = ai_camera.ai as mob;
            }
        }
        if (!Dm.truthy(suspect.key))
            return null;
        string has_gloves = "";
        if (ishuman(suspect))
        {
            mob_living_carbon_human human_suspect = (mob_living_carbon_human)suspect;
            if (Dm.truthy(human_suspect.gloves))
                has_gloves = "(gloves)";
        }
        object current_time = round_timestamp();
        if (!Dm.truthy(LAZYACCESS(hiddenprints, suspect.key)))
            LAZYSET(ref hiddenprints, suspect.key, Dm.text("First: \\[[]\\] \"[]\"[]. Ckey: []", current_time, suspect.real_name, has_gloves, suspect.ckey));
        else
        {
            object last_stamp_pos = Dm.findtext(LAZYACCESS(hiddenprints, suspect.key), "\nLast: ");
            if (Dm.truthy(last_stamp_pos))
                LAZYSET(ref hiddenprints, suspect.key, Dm.copytext(hiddenprints[suspect.key] as string, 1, Dm.num(last_stamp_pos)));
            hiddenprints[suspect.key] = Dm.add(hiddenprints[suspect.key], Dm.text("\nLast: \\[[]\\] \"[]\"[]. Ckey: []", current_time, suspect.real_name, has_gloves, suspect.ckey)); //made sure to be existing by if(!LAZYACCESS);else
        }
        parent.fingerprintslast = suspect.ckey;
        return TRUE;
    }

    /// Adds the given list into blood_DNA
    // tg: code/modules/forensics/_forensics.dm:230
    public virtual object add_blood_DNA(DmList blood_DNA = null)
    {
        if (!Dm.truthy(Dm.length(blood_DNA)))
            return null;
        LAZYINITLIST(ref this.blood_DNA);
        foreach (object gene in Dm.each(blood_DNA))
            this.blood_DNA[gene] = blood_DNA[gene];
        check_blood();
        return TRUE;
    }

    /// Updates the blood displayed on parent
    // tg: code/modules/forensics/_forensics.dm:240
    public virtual void check_blood()
    {
        if (!isitem(parent) || isorgan(parent)) // organs don't spawn with blood decals by default
            return;
        object blood_color = parent.get_blood_dna_color();
        if (Dm.truthy(blood_color))
            Waiting.NoSleep(parent.AddElement(typeof(datum_element_decal_blood), Dm.named("_color", blood_color), Dm.named("_emissive", parent.get_blood_emissive_alpha())));
    }

    /// Returns how many blood datums on us fit our parent's expose flags
    // tg: code/modules/forensics/_forensics.dm:248
    public virtual DmList get_visible_blood()
    {
        object expose_flag = null;
        if (isturf(parent) || parent is obj_effect_decal)
            expose_flag = BLOOD_COVER_TURFS;
        else if (ismob(parent))
            expose_flag = BLOOD_COVER_MOBS;
        else if (isitem(parent))
            expose_flag = BLOOD_COVER_ITEMS;

        if (!Dm.truthy(expose_flag))
            return null;

        DmList visible_blood = Dm.list();
        foreach (object blood_key in Dm.each(blood_DNA))
        {
            datum_blood_type blood_type = blood_DNA[blood_key] as datum_blood_type;
            if (blood_type == null)
            {
                blood_type = get_blood_type(blood_DNA[blood_key]) as datum_blood_type;
                if (blood_type == null)
                    continue;
            }
            if (Dm.truthy(Dm.bitand(blood_type.blood_flags, expose_flag)))
                visible_blood[blood_key] = blood_type;
        }

        return visible_blood;
    }
}
