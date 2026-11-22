// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/blood_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the blood type datum's procs and the human blood types translated (M2 P1), oil (M9 slice D: the map's motor oil
// puddles hold its DNA); the other blood types of the file
// (clown, animal, ...: other species, M9) come with their species (M8 slice C: universal, lizard, ethereal,
// slime, water, snail; gaps X5: xeno), and the blood decal procs with the
// decals (M3, M6).

// tg: code/modules/mob/living/blood_types.dm:1
[DmPath("/datum/blood_type")]
public partial class datum_blood_type : datum
{
    // tg: code/modules/mob/living/blood_types.dm:13  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list();
    }

    /// Name of the blood type.
    // tg: code/modules/mob/living/blood_types.dm:3
    public string name = "?";
    /// A description of the blood type.
    // tg: code/modules/mob/living/blood_types.dm:5
    public object desc;
    /// Unique identifier for the blood type in the global list of singletons. Typically this is just the name, but some blood types might have the same name (e.g. evil blood)
    // tg: code/modules/mob/living/blood_types.dm:7
    public object id;
    /// What DNA string does this bloodtype have by default, if not set by a mob?
    // tg: code/modules/mob/living/blood_types.dm:9
    public string dna_string = "Unknown DNA";
    /// Shown color of the blood type.
    // tg: code/modules/mob/living/blood_types.dm:11
    public string color = BLOOD_COLOR_RED;
    /// Blood types that are safe to use with people that have this blood type (for blood transfusions)
    // tg: code/modules/mob/living/blood_types.dm:13 (list(): dm_init_vars above)
    public DmList compatible_types;
    /// What reagent is represented by this blood type?
    // tg: code/modules/mob/living/blood_types.dm:15  var/datum/reagent/reagent_type (Type: a typepath default, rules 3.2 V3)
    public Type reagent_type = typeof(datum_reagent_blood);
    /// What chem is used to restore this blood type (outside of itself, of course)?
    // tg: code/modules/mob/living/blood_types.dm:17  var/datum/reagent/restoration_chem (Type: a typepath default, rules 3.2 V3)
    public Type restoration_chem = typeof(datum_reagent_iron);
    /// If this blood type is meant to persist across species changes
    // tg: code/modules/mob/living/blood_types.dm:19
    public object is_species_universal;
    /// Splash and expose behaviors for this blood type's reagent, to prevent water-blood covered items, as well as information transfer flags
    // tg: code/modules/mob/living/blood_types.dm:21
    public float blood_flags = Dm.bitor(Dm.bitor(BLOOD_ADD_DNA, BLOOD_COVER_ALL), BLOOD_TRANSFER_VIRAL_DATA);
}

// tg: code/modules/mob/living/blood_types.dm:23
public partial class datum_blood_type : datum
{
    // tg: code/modules/mob/living/blood_types.dm:23
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        Dm.combine(ref compatible_types, type_key());
        if (!Dm.truthy(id))
            id = name;
        if (!Dm.truthy(desc))
            desc = Dm.initial(reagent_type, (datum_reagent p) => p.description); // tg: :27  desc ||= reagent_type::description
    }

    // tg: code/modules/mob/living/blood_types.dm:29
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(force))
        {
            stack_trace("qdel called on blood type singleton! (use FORCE if necessary)", "code/modules/mob/living/blood_types.dm", 31);
            return QDEL_HINT_LETMELIVE;
        }

        return base.Destroy(force);
    }

    /**
     * Key used to identify this blood type in compatible_types
     *
     * Allows for more complex or dynamically generated blood types
     */
    // tg: code/modules/mob/living/blood_types.dm:41
    public virtual object type_key()
    {
        return type;
    }

    /// Name of the reagent we use for blood
    // tg: code/modules/mob/living/blood_types.dm:45
    public virtual object get_blood_name()
    {
        return capitalize(LOWER_TEXT(Dm.initial(reagent_type, (datum_reagent p) => p.name)));
    }

    /// Type string of this bloodtype. Used to prevent "Oil type: Oil" scenarios
    // tg: code/modules/mob/living/blood_types.dm:49
    public virtual object get_type()
    {
        if (reagent_type != typeof(datum_reagent_blood))
            return null;
        return name;
    }

    /// Default stuff included in this blood type's blood data
    /// Return a list of key/value pairs for blood data, or null if no additional default data
    // tg: code/modules/mob/living/blood_types.dm:56
    public virtual object get_default_blood_data()
    {
        return Dm.list(
            Dm.kv(BLOOD_DATA_TYPE, this),
            Dm.kv(BLOOD_DATA_DNA, dna_string)
        );
    }

    /// Returns blood color or color matrix
    /// Useful when you want to have a blood color with values out of normal hex bounds for that acidic look
    /// set dynamic to TRUE to redo the matrix each time (e.g. for clown blood dynamically shifting each time)
    // tg: code/modules/mob/living/blood_types.dm:65
    public virtual async DmTask<object> get_color(float? dynamic = null)
    {
        dynamic ??= FALSE;
        return color;
    }

    /// Returns blood color for mob damage overlays
    // tg: code/modules/mob/living/blood_types.dm:69
    public virtual async DmTask<object> get_damage_color(mob_living_carbon victim = null)
    {
        return await get_color();
    }

    /// Returns blood color for wound bleeding overlays
    // tg: code/modules/mob/living/blood_types.dm:73
    public virtual async DmTask<object> get_wound_color(mob_living_carbon victim = null)
    {
        return await get_color();
    }

    /// Returns emissive value for an atom
    /// is_worn - Emissive is being fetched for a mob overlay and not the item itself
    // tg: code/modules/mob/living/blood_types.dm:78
    public virtual object get_emissive_alpha(atom source = null, float? is_worn = null)
    {
        is_worn ??= FALSE;
        return 0;
    }

    /**
     * Used to handle any unique facets of blood spawned of this blood type
     *
     * You don't need to worry about updating the icon of the decal,
     * it will be handled automatically after setup is finished
     *
     * Arguments
     * * blood - the blood being set up
     * * new_splat - whether this is a newly instantiated blood decal, or an existing one this blood is being added to
     */
    // tg: code/modules/mob/living/blood_types.dm:91
    public virtual async DmTask set_up_blood(obj_effect_decal_cleanable_blood blood = null, float? new_splat = null)
    {
        new_splat ??= FALSE;
        if (Dm.truthy(new_splat) && !Dm.truthy(blood.decal_reagent))
            blood.decal_reagent = reagent_type;
        else if (Dm.truthy(blood.reagents) && Dm.truthy(blood.bloodiness)) // If reagents don't exist yet, we'll be added via lazyloading
        {
            // blood.reagents.add_reagent(reagent_type, round(blood.bloodiness / (GET_ATOM_BLOOD_DNA_LENGTH(blood) - 1) *
            // BLOOD_TO_UNITS_MULTIPLIER, CHEMICAL_VOLUME_ROUNDING)): the holder's add_reagent is not in the port (reagents, M8)
            Dm.Deferred("code/modules/mob/living/blood_types.dm:94", "set_up_blood: the new blood's reagent in the decal's holder (reagents, M8)");
        }
    }

    /**
     * Helper proc to make a blood splatter from the passed mob of this type
     *
     * Arguments
     * * bleeding - the mob bleeding the blood, note we assume this blood type is that mob's blood
     * * blood_turf - the turf to spawn the blood on
     * * drip - whether to spawn a drip or a splatter
     */
    // tg: code/modules/mob/living/blood_types.dm:106
    public virtual async DmTask<object> make_blood_splatter(mob_living bleeding = null, turf blood_turf = null, float? drip = null)
    {
        drip ??= FALSE;
        if (!Dm.truthy(Dm.bitand(blood_flags, BLOOD_COVER_TURFS)))
            return null;

        if (isgroundlessturf(blood_turf))
            blood_turf = GET_TURF_BELOW(blood_turf) as turf;

        if (blood_turf == null || isclosedturf(blood_turf))
            return null;

        DmList temp_blood_DNA = null;
        if (Dm.truthy(drip))
        {
            float new_blood = Dm.initial(typeof(obj_effect_decal_cleanable_blood_drip), (obj_effect_decal_cleanable_blood_drip p) => p.bloodiness);
            // Only a certain number of drips (or one large splatter) can be on a given turf.
            obj_effect_decal_cleanable_blood_drip drop = Dm.locate<obj_effect_decal_cleanable_blood_drip>(blood_turf);
            if (drop == null)
            {
                obj_effect_decal_cleanable_blood existing = Dm.locate<obj_effect_decal_cleanable_blood>(blood_turf);
                if (!QDELETED(existing) && !Dm.truthy(existing.dried))
                {
                    await existing.add_mob_blood(bleeding);
                    await existing.adjust_bloodiness(new_blood);
                    return existing;
                }

                drop = Dm.@new<obj_effect_decal_cleanable_blood_drip>(blood_turf, await bleeding.get_static_viruses(), bleeding.get_blood_dna_list());
                if (!QDELETED(drop))
                    Dm.remove(drop.random_icon_states, drop.icon_state);
                return drop;
            }

            if (Dm.truthy(Dm.length(drop.random_icon_states)))
            {
                // Handle adding a single drip to the base atom
                // Makes use of viscontents so every drip can dry at an individual rate (with an individual color)
                obj_effect_decal_cleanable_blood_drip new_drop = Dm.@new<obj_effect_decal_cleanable_blood_drip>(drop, null, bleeding.get_blood_dna_list());
                new_drop.bloodiness = 0;
                new_drop.icon_state = pick_n_take(drop.random_icon_states) as string;
                new_drop.color = color;
                new_drop.vis_flags = Dm.bitor(new_drop.vis_flags, Dm.bitor(Dm.bitor(VIS_INHERIT_LAYER, VIS_INHERIT_PLANE), VIS_INHERIT_ID));
                new_drop.appearance_flags = Dm.bitor(new_drop.appearance_flags, RESET_COLOR);
                drop.gender = PLURAL;
                drop.base_name = "drips of";
                Dm.append(drop.vis_contents, new_drop);
                // Handle adding blood to the base atom
                await drop.adjust_bloodiness(new_blood);
                await drop.add_mob_blood(bleeding);
                await drop.add_diseases(await bleeding.get_static_viruses() as DmList);
                return drop;
            }

            temp_blood_DNA = GET_ATOM_BLOOD_DNA(drop); // We transfer the dna from the drip to the splatter
            qdel(drop); // The drip is replaced by a bigger splatter
        }

        // Find a blood decal or create a new one.
        obj_effect_decal_cleanable_blood splatter = Dm.locate<obj_effect_decal_cleanable_blood>(blood_turf);
        if (splatter == null || Dm.truthy(splatter.dried))
        {
            splatter = Dm.@new<obj_effect_decal_cleanable_blood>(blood_turf, await bleeding.get_static_viruses(), bleeding.get_blood_dna_list());
            if (QDELETED(splatter)) //Give it up
                return null;
        }
        else
        {
            await splatter.adjust_bloodiness(BLOOD_AMOUNT_PER_DECAL);
            await splatter.add_diseases(await bleeding.get_static_viruses() as DmList);
            await splatter.add_mob_blood(bleeding); // Give blood info to the blood decal
        }

        if (Dm.truthy(LAZYLEN(temp_blood_DNA)))
            await splatter.add_blood_DNA(temp_blood_DNA);
        return splatter;
    }
}

// Human blood type, for organizational purposes mainly
// tg: code/modules/mob/living/blood_types.dm:170
[DmPath("/datum/blood_type/human")]
public partial class datum_blood_type_human : datum_blood_type
{
    // tg: code/modules/mob/living/blood_types.dm:171-173  the var overrides of the type block (rules 10.2)
    public datum_blood_type_human()
    {
        desc = "Blood cells suspended in plasma, the most abundant of which being the hemoglobin-containing red blood cells.";
        dna_string = "Human DNA";
        abstract_type = typeof(datum_blood_type_human);
    }
}

// tg: code/modules/mob/living/blood_types.dm:175
[DmPath("/datum/blood_type/human/a_minus")]
public partial class datum_blood_type_human_a_minus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:176  the var overrides of the type block (rules 10.2); compatible_types (:177) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_a_minus()
    {
        name = "A-";
    }

    // tg: code/modules/mob/living/blood_types.dm:177-180  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_a_minus),
            typeof(datum_blood_type_human_o_minus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:182
[DmPath("/datum/blood_type/human/a_plus")]
public partial class datum_blood_type_human_a_plus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:183  the var overrides of the type block (rules 10.2); compatible_types (:184) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_a_plus()
    {
        name = "A+";
    }

    // tg: code/modules/mob/living/blood_types.dm:184-189  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_a_minus),
            typeof(datum_blood_type_human_a_plus),
            typeof(datum_blood_type_human_o_minus),
            typeof(datum_blood_type_human_o_plus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:191
[DmPath("/datum/blood_type/human/b_minus")]
public partial class datum_blood_type_human_b_minus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:192  the var overrides of the type block (rules 10.2); compatible_types (:193) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_b_minus()
    {
        name = "B-";
    }

    // tg: code/modules/mob/living/blood_types.dm:193-196  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_b_minus),
            typeof(datum_blood_type_human_o_minus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:198
[DmPath("/datum/blood_type/human/b_plus")]
public partial class datum_blood_type_human_b_plus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:199  the var overrides of the type block (rules 10.2); compatible_types (:200) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_b_plus()
    {
        name = "B+";
    }

    // tg: code/modules/mob/living/blood_types.dm:200-205  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_b_minus),
            typeof(datum_blood_type_human_b_plus),
            typeof(datum_blood_type_human_o_minus),
            typeof(datum_blood_type_human_o_plus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:207
[DmPath("/datum/blood_type/human/ab_minus")]
public partial class datum_blood_type_human_ab_minus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:208  the var overrides of the type block (rules 10.2); compatible_types (:209) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_ab_minus()
    {
        name = "AB-";
    }

    // tg: code/modules/mob/living/blood_types.dm:209-214  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_a_minus),
            typeof(datum_blood_type_human_b_minus),
            typeof(datum_blood_type_human_ab_minus),
            typeof(datum_blood_type_human_o_minus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:216
[DmPath("/datum/blood_type/human/ab_plus")]
public partial class datum_blood_type_human_ab_plus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:217  the var overrides of the type block (rules 10.2); compatible_types (:218) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_ab_plus()
    {
        name = "AB+";
    }

    // tg: code/modules/mob/living/blood_types.dm:218-227  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_a_minus),
            typeof(datum_blood_type_human_a_plus),
            typeof(datum_blood_type_human_b_minus),
            typeof(datum_blood_type_human_b_plus),
            typeof(datum_blood_type_human_o_minus),
            typeof(datum_blood_type_human_o_plus),
            typeof(datum_blood_type_human_ab_minus),
            typeof(datum_blood_type_human_ab_plus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:229
[DmPath("/datum/blood_type/human/o_minus")]
public partial class datum_blood_type_human_o_minus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:230  the var overrides of the type block (rules 10.2); compatible_types (:231) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_o_minus()
    {
        name = "O-";
    }

    // tg: code/modules/mob/living/blood_types.dm:231-233  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_o_minus));
    }
}

// tg: code/modules/mob/living/blood_types.dm:235
[DmPath("/datum/blood_type/human/o_plus")]
public partial class datum_blood_type_human_o_plus : datum_blood_type_human
{
    // tg: code/modules/mob/living/blood_types.dm:236  the var overrides of the type block (rules 10.2); compatible_types (:237) creates a list:
    // dm_init_vars below
    public datum_blood_type_human_o_plus()
    {
        name = "O+";
    }

    // tg: code/modules/mob/living/blood_types.dm:237-240  the object-creating override (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        compatible_types = Dm.list(
            typeof(datum_blood_type_human_o_minus),
            typeof(datum_blood_type_human_o_plus));
    }
}

// M8 slice C (D192): the universal blood and the blood types of the blood freezer's and the blood packs' species
// (lizard, ethereal, slime, water, snail), so GLOB.blood_types has them (init_blood_types takes the written types).

// tg: code/modules/mob/living/blood_types.dm:243
public partial class datum_blood_type_universal : datum_blood_type
{
    // tg: code/modules/mob/living/blood_types.dm:247
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        compatible_types = subtypesof(typeof(datum_blood_type)) as DmList;
    }
}

// tg: code/modules/mob/living/blood_types.dm:260  (no procs of its own: its var block is generated)
public partial class datum_blood_type_lizard : datum_blood_type
{
}

// tg: code/modules/mob/living/blood_types.dm:267
public partial class datum_blood_type_ethereal : datum_blood_type
{
    // tg: code/modules/mob/living/blood_types.dm:273
    public override object get_default_blood_data()
    {
        DmList dot = base.get_default_blood_data() as DmList;
        // Unenriched by default to prevent Ethereals from going crazy with syringe guns
        dot[BLOOD_DATA_ENRICHED_ETHEREAL] = FALSE;
        return dot;
    }

    // tg: code/modules/mob/living/blood_types.dm:278
    public override object get_emissive_alpha(atom source = null, float? is_worn = null)
    {
        is_worn ??= FALSE;
        return Dm.truthy(is_worn) ? 102 : 125;
    }

    // tg: code/modules/mob/living/blood_types.dm:281
    public override async DmTask set_up_blood(obj_effect_decal_cleanable_blood blood = null, float? new_splat = null)
    {
        new_splat ??= FALSE;
        await base.set_up_blood(blood, new_splat);
        blood.emissive_alpha = max(Dm.num(blood.emissive_alpha), Dm.truthy(new_splat) ? 125 : 63);
        if (Dm.truthy(new_splat))
            return;
        blood.can_dry = FALSE;
    }
}

// tg: code/modules/mob/living/blood_types.dm:320  (gaps X5: the xenomorphs' acid blood)
public partial class datum_blood_type_xeno : datum_blood_type
{
    // tg: code/modules/mob/living/blood_types.dm:329
    public override object get_blood_name()
    {
        return I18n.T("Acid"); // "pool of sulphuric acid" is a bit too lengthy of a name
    }

    // tg: code/modules/mob/living/blood_types.dm:332
    public override async DmTask set_up_blood(obj_effect_decal_cleanable_blood blood = null, float? new_splat = null)
    {
        new_splat ??= FALSE;
        await base.set_up_blood(blood, new_splat);
        if (!Dm.truthy(new_splat))
            return;
        blood.can_dry = FALSE;
        // Replace only the default description
        if (blood.desc == Dm.initial(typeof(obj_effect_decal_cleanable_blood), (atom p) => p.desc))
            blood.desc = I18n.T("It's green and acidic. It looks like... <i>blood?</i>");
    }
}

/// Slimeperson blood, aka 'toxin' blood type
// tg: code/modules/mob/living/blood_types.dm:362
public partial class datum_blood_type_slime : datum_blood_type
{
    // Ensures that lighter slimefolk look half-decent when wounded and bleeding
    // tg: code/modules/mob/living/blood_types.dm:370
    public override async DmTask<object> get_wound_color(mob_living_carbon victim = null)
    {
        object mutant_color = (victim.dna?.features as DmList)?[FEATURE_MUTANT_COLOR];
        return Dm.truthy(mutant_color) ? mutant_color : await get_color();
    }

    // tg: code/modules/mob/living/blood_types.dm:373
    public override async DmTask<object> get_damage_color(mob_living_carbon victim = null)
    {
        object mutant_color = (victim.dna?.features as DmList)?[FEATURE_MUTANT_COLOR];
        return Dm.truthy(mutant_color) ? mutant_color : await get_color();
    }
}

/// Podpeople blood
// tg: code/modules/mob/living/blood_types.dm:377
public partial class datum_blood_type_water : datum_blood_type
{
    // Prevents awkward grey wounds on the mob while keeping bleed overlays looking like water leaking from a balloon
    // tg: code/modules/mob/living/blood_types.dm:386
    public override async DmTask<object> get_damage_color(mob_living_carbon victim = null)
    {
        return COLOR_LIME;
    }
}

// tg: code/modules/mob/living/blood_types.dm:288
public partial class datum_blood_type_oil : datum_blood_type
{
    // tg: code/modules/mob/living/blood_types.dm:296
    public override async DmTask set_up_blood(obj_effect_decal_cleanable_blood blood = null, float? new_splat = null)
    {
        await base.set_up_blood(blood, new_splat);
        if (!Dm.truthy(new_splat))
            return;

        // Oil blood will never dry and can be ignited with fire or anything sufficiently hot
        blood.can_dry = FALSE;
        blood.dry_prefix = null;
        blood.dry_desc = null;

        // Always force our decals to have our reagent, we don't want liquid gibs from oily guts
        blood.decal_reagent = reagent_type;

        // Oily guts are not converted to robotic ones, so you can still have your biomechanical abominations >X)
        if (!(blood is obj_effect_decal_cleanable_blood_gibs))
        {
            // blood.AddElement(/datum/element/easy_ignite): the easy ignite element comes with its other users (fire, a later milestone)
            Dm.Deferred("code/modules/mob/living/blood_types.dm:311", "set_up_blood: AddElement(/datum/element/easy_ignite), the oil that a flame lights (the easy ignite element)");
        }

        // Replace only the default description
        if (Dm.eq(blood.desc, Dm.initial(typeof(obj_effect_decal_cleanable_blood), (obj_effect_decal_cleanable_blood p) => p.desc)))
            blood.desc = Dm.initial(typeof(obj_effect_decal_cleanable_blood_oil), (obj_effect_decal_cleanable_blood_oil p) => p.desc);
    }
}

/// Snail blood
// tg: code/modules/mob/living/blood_types.dm:390
public partial class datum_blood_type_snail : datum_blood_type
{
    // tg: code/modules/mob/living/blood_types.dm:396
    public override async DmTask set_up_blood(obj_effect_decal_cleanable_blood blood = null, float? new_splat = null)
    {
        await base.set_up_blood(blood, new_splat);
        if (Dm.num(blood.bloodiness) < BLOOD_AMOUNT_PER_DECAL)
            return;
        float slip_amt = Dm.truthy(new_splat) ? 4 * SECONDS : 1 * SECONDS;
        float slip_flags = Dm.truthy(new_splat) ? Dm.num(Dm.bitor(NO_SLIP_WHEN_WALKING, SLIDE)) : NO_SLIP_WHEN_WALKING;
        await blood.AddComponent(typeof(datum_component_slippery), slip_amt, slip_flags);
    }
}

// tg: code/modules/mob/living/blood_types.dm:317  (a written class so GLOB.blood_types holds it: the default blood of a blood walk, gaps package C3)
public partial class datum_blood_type_meat : datum_blood_type
{
}
