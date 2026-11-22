// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/_species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Written by the M2 contracts step (docs/ai/specs/m2-ownership.md) and completed by M2's P2 (carbon, human, body, breath) and the later
// packages; gaps SP1 (D338) translated the rest for the roundstart species (the appearance's randomizers, the preview's and the
// species page's procs, the perks, the body markings). Not translated: dump_harddel_info (SSgarbage's hard deletes, DV7) and the
// random negative mutation of handle_radiation (genetics).

/**
 * # species datum
 *
 * Datum that handles different species in the game.
 *
 * This datum handles species in the game, such as lizardpeople, mothmen, zombies, skeletons, etc.
 * It is used in [carbon humans][mob/living/carbon/human] to determine various things about them, like their food preferences, if they have biological genders, their damage resistances, and more.
 *
 */
// tg: code/modules/mob/living/carbon/human/_species.dm:18
// The var block (:19-178) was written by the M2 contracts step (rules 10.2): the declarations and the object-creating
// defaults (dm_init_vars).
[DmPath("/datum/species")]
public partial class datum_species : datum
{
    // tg: code/modules/mob/living/carbon/human/_species.dm:64, :73, :75, :98, :135, :178  the object-creating defaults
    // (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        bodypart_overrides = Dm.list(
            Dm.kv(BODY_ZONE_L_ARM, typeof(obj_item_bodypart_arm_left)),
            Dm.kv(BODY_ZONE_R_ARM, typeof(obj_item_bodypart_arm_right)),
            Dm.kv(BODY_ZONE_HEAD, typeof(obj_item_bodypart_head)),
            Dm.kv(BODY_ZONE_L_LEG, typeof(obj_item_bodypart_leg_left)),
            Dm.kv(BODY_ZONE_R_LEG, typeof(obj_item_bodypart_leg_right)),
            Dm.kv(BODY_ZONE_CHEST, typeof(obj_item_bodypart_chest)));
        survival_box_overrides = Dm.list();
        mutant_organs = Dm.list();
        body_markings = Dm.list();
        inherent_traits = Dm.list();
        outfit_override_registry = Dm.list();
    }

    ///If the game needs to manually check your race to do something not included in a proc here, it will use this.
    // tg: code/modules/mob/living/carbon/human/_species.dm:20
    public object id;
    ///This is used for children, it will determine their default limb ID for use of examine. See [/mob/living/carbon/human/proc/examine].
    // tg: code/modules/mob/living/carbon/human/_species.dm:22
    public object examine_limb_id;
    ///This is the fluff name. They are displayed on health analyzers and in the character setup menu. Leave them generic for other servers to customize.
    // tg: code/modules/mob/living/carbon/human/_species.dm:24  (a /datum var, not the builtin /atom name)
    public object name;
    /**
     * The formatting of the name of the species in plural context. Defaults to "[name]\s" if unset.
     *  Ex "[Plasmamen] are weak", "[Mothmen] are strong", "[Lizardpeople] don't like", "[Golems] hate"
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:29
    public object plural_form;

    /// Whether or not the race has sexual characteristics (biological genders). At the moment this is only FALSE for skeletons and shadows
    // tg: code/modules/mob/living/carbon/human/_species.dm:32
    public float sexes = TRUE;

    ///The maximum number of bodyparts this species can have.
    // tg: code/modules/mob/living/carbon/human/_species.dm:35
    public float max_bodypart_count = 6;
    /// This allows races to have specific hair colors.
    /// If null, it uses the mob's hair/facial hair colors.
    /// If USE_MUTANT_COLOR, it uses the mob's mutant_color.
    /// If USE_FIXED_MUTANT_COLOR, it uses fixedmutcolor
    // tg: code/modules/mob/living/carbon/human/_species.dm:40
    public object hair_color_mode;
    ///The alpha used by the hair. 255 is completely solid, 0 is invisible.
    // tg: code/modules/mob/living/carbon/human/_species.dm:42
    public float hair_alpha = 255;
    ///The alpha used by the facial hair. 255 is completely solid, 0 is invisible.
    // tg: code/modules/mob/living/carbon/human/_species.dm:44
    public float facial_hair_alpha = 255;

    ///Never, Optional, or Forced digi legs?
    // tg: code/modules/mob/living/carbon/human/_species.dm:47
    public float digitigrade_customization = DIGITIGRADE_NEVER;
    ///If your race uses a non standard bloodtype (A+, O-, AB-, etc). For example, lizards have L type blood.
    ///Reagent that your species bleeds, and what chemical can be used to recover lost blood depend on this
    // tg: code/modules/mob/living/carbon/human/_species.dm:50  var/datum/blood_type/exotic_bloodtype (holds typepaths:
    // V3, the species subtypes assign /datum/blood_type/... paths, e.g. lizardpeople.dm:33)
    public Type exotic_bloodtype;
    ///The rate at which blood is passively drained by having the blood deficiency quirk. Some races such as slimepeople can regen their blood at different rates so this is to account for that
    // tg: code/modules/mob/living/carbon/human/_species.dm:52
    public float blood_deficiency_drain_rate = BLOOD_REGEN_FACTOR + BLOOD_DEFICIENCY_MODIFIER; // slightly above the regen rate so it slowly drains instead of regenerates.
    ///What the species drops when gibbed by a gibber machine.
    // tg: code/modules/mob/living/carbon/human/_species.dm:54
    public Type meat = typeof(obj_item_food_meat_slab_human);
    ///What skin the species drops when gibbed by a gibber machine.
    // tg: code/modules/mob/living/carbon/human/_species.dm:56  (holds typepaths: V3, humans.dm:7)
    public Type skinned_type;
    ///flags for inventory slots the race can't equip stuff to. Golems cannot wear jumpsuits, for example.
    // tg: code/modules/mob/living/carbon/human/_species.dm:58
    public object no_equip_flags;
    /// What languages this species can understand and say.
    /// Use a [language holder datum][/datum/language_holder] typepath in this var.
    /// Should never be null.
    // tg: code/modules/mob/living/carbon/human/_species.dm:62  var/datum/language_holder/species_language_holder (holds
    // typepaths: V3)
    public Type species_language_holder = typeof(datum_language_holder_human_basic);
    ///The bodyparts this species uses. assoc of bodypart string - bodypart type. Make sure all the fucking entries are in or I'll skin you alive.
    // tg: code/modules/mob/living/carbon/human/_species.dm:64  (= list(BODY_ZONE_L_ARM = ..., ...): dm_init_vars)
    public DmList bodypart_overrides;
    ///A list to override what gets put in a species' survival box. keys are defined in code\__DEFINES\survival_box.dm
    // tg: code/modules/mob/living/carbon/human/_species.dm:73
    public DmList survival_box_overrides;
    ///Internal organs that are unique to this race, like a tail or other cosmetic organs. list(typepath of organ 1, typepath of organ 2 = "Round").
    // tg: code/modules/mob/living/carbon/human/_species.dm:75
    public DmList mutant_organs;
    ///Replaces default brain with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:77  var/obj/item/organ/brain/mutantbrain (holds typepaths: V3)
    public Type mutantbrain = typeof(obj_item_organ_brain);
    ///Replaces default heart with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:79  (V3)
    public Type mutantheart = typeof(obj_item_organ_heart);
    ///Replaces default lungs with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:81  (V3)
    public Type mutantlungs = typeof(obj_item_organ_lungs);
    /// Smoker lungs for the quirk, overriden by certain species
    // tg: code/modules/mob/living/carbon/human/_species.dm:83  (V3)
    public Type smoker_lungs = typeof(obj_item_organ_lungs_smoker_lungs);
    ///Replaces default eyes with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:85  (V3)
    public Type mutanteyes = typeof(obj_item_organ_eyes);
    ///Replaces default ears with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:87  (V3)
    public Type mutantears = typeof(obj_item_organ_ears);
    ///Replaces default tongue with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:89  (V3)
    public Type mutanttongue = typeof(obj_item_organ_tongue);
    ///Replaces default liver with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:91  (V3)
    public Type mutantliver = typeof(obj_item_organ_liver);
    ///Replaces default stomach with a different organ
    // tg: code/modules/mob/living/carbon/human/_species.dm:93  (V3)
    public Type mutantstomach = typeof(obj_item_organ_stomach);
    ///Replaces default appendix with a different organ.
    // tg: code/modules/mob/living/carbon/human/_species.dm:95  (V3)
    public Type mutantappendix = typeof(obj_item_organ_appendix);

    /// Store body marking defines. See mobs.dm for bitflags
    // tg: code/modules/mob/living/carbon/human/_species.dm:98
    public DmList body_markings;

    /// Flat modifier on all damage taken via [apply_damage][/mob/living/proc/apply_damage] (so being punched, shot, etc.)
    /// IE: 10 = 10% less damage taken.
    // tg: code/modules/mob/living/carbon/human/_species.dm:102
    public float damage_modifier = 0;
    ///multiplier for damage from cold temperature
    // tg: code/modules/mob/living/carbon/human/_species.dm:104
    public float coldmod = 1;
    ///multiplier for damage from hot temperature
    // tg: code/modules/mob/living/carbon/human/_species.dm:106
    public float heatmod = 1;
    ///multiplier for stun durations
    // tg: code/modules/mob/living/carbon/human/_species.dm:108
    public float stunmod = 1;
    ///multiplier for money paid at payday
    // tg: code/modules/mob/living/carbon/human/_species.dm:110
    public float payday_modifier = 1.0f;
    ///Base electrocution coefficient.  Basically a multiplier for damage from electrocutions.
    // tg: code/modules/mob/living/carbon/human/_species.dm:112
    public float? siemens_coeff = 1;
    ///To use MUTCOLOR with a fixed color that's independent of the mcolor feature in DNA.
    // tg: code/modules/mob/living/carbon/human/_species.dm:114
    public string fixed_mut_color = "";
    ///Special mutation that can be found in the genepool exclusively in this species. Dont leave empty or changing species will be a headache
    // tg: code/modules/mob/living/carbon/human/_species.dm:116  var/inert_mutation = /datum/mutation/dwarfism
    // GAP: the default is withheld (null). Declaring /datum/mutation/dwarfism (datums/mutations/body.dm:116) gives
    // /datum/mutation its first subtype, and SSatoms.setupGenetics then calls generate_gene_sequence
    // (dna.dm:225, Dm.NotTranslated) at boot. The default returns with that type: = typeof(datum_mutation_dwarfism).
    public Type inert_mutation;
    ///Special sound for grabbing
    // tg: code/modules/mob/living/carbon/human/_species.dm:118
    public object grab_sound;
    /// A path to an outfit that is important for species life e.g. plasmaman outfit
    // tg: code/modules/mob/living/carbon/human/_species.dm:120  var/datum/outfit/outfit_important_for_life (holds
    // typepaths: V3, plasmamen.dm:31)
    public Type outfit_important_for_life;

    /// The natural temperature for a body
    // tg: code/modules/mob/living/carbon/human/_species.dm:123
    public float bodytemp_normal = BODYTEMP_NORMAL;
    /// Minimum amount of kelvin moved toward normal body temperature per tick.
    // tg: code/modules/mob/living/carbon/human/_species.dm:125
    public float bodytemp_autorecovery_min = BODYTEMP_AUTORECOVERY_MINIMUM;
    /// The body temperature limit the body can take before it starts taking damage from heat.
    // tg: code/modules/mob/living/carbon/human/_species.dm:127
    public float bodytemp_heat_damage_limit = BODYTEMP_HEAT_DAMAGE_LIMIT;
    /// The body temperature limit the body can take before it starts taking damage from cold.
    // tg: code/modules/mob/living/carbon/human/_species.dm:129
    public float bodytemp_cold_damage_limit = BODYTEMP_COLD_DAMAGE_LIMIT;

    /// The icon_state of the fire overlay added when sufficently ablaze and standing. see onfire.dmi
    // tg: code/modules/mob/living/carbon/human/_species.dm:132
    public string fire_overlay = "human";

    /// Generic traits tied to having the species.
    // tg: code/modules/mob/living/carbon/human/_species.dm:135
    public DmList inherent_traits;
    /// List of biotypes the mob belongs to. Used by diseases.
    // tg: code/modules/mob/living/carbon/human/_species.dm:137
    public float inherent_biotypes = Dm.bitor(MOB_ORGANIC, MOB_HUMANOID);
    ///List of factions the mob gain upon gaining this species.
    // tg: code/modules/mob/living/carbon/human/_species.dm:139
    public DmList inherent_factions;

    ///What anim to use for gibbing
    // tg: code/modules/mob/living/carbon/human/_species.dm:142
    public string gib_anim = "gibbed-h";

    ///Bitflag that controls what in game ways something can select this species as a spawnable source, such as magic mirrors. See [mob defines][code/__DEFINES/mobs.dm] for possible sources.
    // tg: code/modules/mob/living/carbon/human/_species.dm:145
    public float changesource_flags = NONE;

    ///Unique cookie given by admins through prayers
    // tg: code/modules/mob/living/carbon/human/_species.dm:148
    public Type species_cookie = typeof(obj_item_food_cookie);

    /// List of family heirlooms this species can get with the family heirloom quirk. List of types.
    // tg: code/modules/mob/living/carbon/human/_species.dm:151
    public DmList family_heirlooms;

    ///List of results you get from knife-butchering. null means you cant butcher it. Associated by resulting type - value of amount
    // tg: code/modules/mob/living/carbon/human/_species.dm:154
    public DmList knife_butcher_results;

    /// Should we preload this species's organs?
    // tg: code/modules/mob/living/carbon/human/_species.dm:157
    public float preload = TRUE;

    /// Should the character preview dummy load all organs? Required for i.e organ_set_bonus visual effects
    // tg: code/modules/mob/living/carbon/human/_species.dm:160
    public float character_preview_load_all_organs = FALSE;

    /// Do we try to prevent reset_perspective() from working? Useful for Dullahans to stop perspective changes when they're looking through their head.
    // tg: code/modules/mob/living/carbon/human/_species.dm:163
    public float prevent_perspective_change = FALSE;

    ///Was the species changed from its original type at the start of the round?
    // tg: code/modules/mob/living/carbon/human/_species.dm:166
    public float roundstart_changed = FALSE;

    /// This supresses the "dosen't appear to be himself" examine text for if the mob is run by an AI controller. Should be used on any NPC human subtypes. Monkeys are the prime example.
    // tg: code/modules/mob/living/carbon/human/_species.dm:169
    public float ai_controlled_species = FALSE;

    /**
     * Was on_species_gain ever actually called?
     * Species code is really odd...
     **/
    // tg: code/modules/mob/living/carbon/human/_species.dm:175
    public float properly_gained = FALSE;

    ///A list containing outfits that will be overridden in the species_equip_outfit proc. [Key = Typepath passed in] [Value = Typepath of outfit you want to equip for this specific species instead].
    // tg: code/modules/mob/living/carbon/human/_species.dm:178
    public DmList outfit_override_registry;
}

// tg: code/modules/mob/living/carbon/human/_species.dm:185 (interleaved types, rules 1.9)
public partial class datum_species : datum
{
    // tg: code/modules/mob/living/carbon/human/_species.dm:185
    public override async DmTask New(params object[] __extra)
    {
        if (!Dm.truthy(plural_form))
            plural_form = Dm.text("[]\\s", name);
        if (!Dm.truthy(examine_limb_id))
            examine_limb_id = id;
        // Carbons determine bodypart order by this list, so we need to make sure it's sorted properly
        sortTim(bodypart_overrides, "/proc/" + nameof(GlobalProcs.cmp_bodypart_by_body_part_asc), associative: TRUE);

        await base.New(__extra);
    }
}

// tg: code/modules/mob/living/carbon/human/_species.dm:2 (M10 slice F: the character's selectable species)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/mob/living/carbon/human/_species.dm:2  GLOBAL_LIST_EMPTY(roundstart_races)
    public DmList roundstart_races;

    // tg: code/modules/mob/living/carbon/human/_species.dm:2  (GLOBAL_MANAGED(roundstart_races, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalroundstart_races()
    {
        roundstart_races = Dm.list();
        Dm.append(ref gvars_datum_init_order, "roundstart_races");
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:7  GLOBAL_LIST_EMPTY(features_by_species)
    public DmList features_by_species;

    // tg: code/modules/mob/living/carbon/human/_species.dm:7  (GLOBAL_MANAGED(features_by_species, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfeatures_by_species()
    {
        features_by_species = Dm.list();
        Dm.append(ref gvars_datum_init_order, "features_by_species");
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:4  GLOBAL_LIST_EMPTY(uncommon_roundstart_languages)
    public DmList uncommon_roundstart_languages;

    // tg: code/modules/mob/living/carbon/human/_species.dm:4  (GLOBAL_MANAGED(uncommon_roundstart_languages, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaluncommon_roundstart_languages()
    {
        uncommon_roundstart_languages = Dm.list();
        Dm.append(ref gvars_datum_init_order, "uncommon_roundstart_languages");
    }
}

// tg: code/modules/mob/living/carbon/human/_species.dm:196 (global procs)
public static partial class GlobalProcs
{
    // tg: code/modules/mob/living/carbon/human/_species.dm:196
    public static DmList get_selectable_species()
    {
        if (!Dm.truthy(GLOB.roundstart_races.len))
            GLOB.roundstart_races = generate_selectable_species_and_languages();

        return GLOB.roundstart_races;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:210
    public static DmList generate_selectable_species_and_languages()
    {
        DmList selectable_species = Dm.list();

        foreach (object species_type in Dm.each(subtypesof(typeof(datum_species))))
        {
            datum_species species = GLOB.species_prototypes[species_type] as datum_species;
            if (species != null && Dm.truthy(species.check_roundstart_eligible()))
            {
                Dm.append(ref selectable_species, species.id);
                datum_language_holder temp_holder = GLOB.prototype_language_holders[species.species_language_holder] as datum_language_holder;
                foreach (object spoken_language in Dm.each(temp_holder.understood_languages))
                    Dm.combine(ref GLOB.uncommon_roundstart_languages, spoken_language);
            }
        }

        Dm.remove(ref GLOB.uncommon_roundstart_languages, typeof(datum_language_common));
        if (!Dm.truthy(selectable_species.len))
            Dm.append(ref selectable_species, SPECIES_HUMAN);

        return selectable_species;
    }
}

// tg: code/modules/mob/living/carbon/human/_species.dm:233
public partial class datum_species : datum
{
    // tg: code/modules/mob/living/carbon/human/_species.dm:233
    public virtual object check_roundstart_eligible()
    {
        if (Dm.@in(id, config.Get(typeof(datum_config_entry_keyed_list_roundstart_races))))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:245
    public virtual void copy_properties_from(datum_species old_species = null, object pref_load = null, object regenerate_icons = null)
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:251
    public virtual object get_mutant_organ_type_for_slot(object slot = null)
    {
        object sw__1 = slot;
        if (Dm.eq(sw__1, ORGAN_SLOT_BRAIN))
            return mutantbrain;
        else if (Dm.eq(sw__1, ORGAN_SLOT_HEART))
            return mutantheart;
        else if (Dm.eq(sw__1, ORGAN_SLOT_LUNGS))
            return mutantlungs;
        else if (Dm.eq(sw__1, ORGAN_SLOT_APPENDIX))
            return mutantappendix;
        else if (Dm.eq(sw__1, ORGAN_SLOT_EYES))
            return mutanteyes;
        else if (Dm.eq(sw__1, ORGAN_SLOT_EARS))
            return mutantears;
        else if (Dm.eq(sw__1, ORGAN_SLOT_TONGUE))
            return mutanttongue;
        else if (Dm.eq(sw__1, ORGAN_SLOT_LIVER))
            return mutantliver;
        else if (Dm.eq(sw__1, ORGAN_SLOT_STOMACH))
            return mutantstomach;
        else
        {
            // Non-standard organs we might have
            foreach (Type extra_organ in Dm.each_cast<Type>(mutant_organs))
            {
                if (Dm.eq(Dm.initial(extra_organ, (obj_item_organ p) => p.slot), slot))
                    return extra_organ;
            }
        }
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:291
    public virtual async DmTask regenerate_organs(mob_living_carbon organ_holder = null, datum_species old_species = null, float? replace_current = null, DmList excluded_zones = null, float? visual_only = null, float? replace_missing = null)
    {
        replace_current ??= TRUE;
        visual_only ??= FALSE;
        replace_missing ??= TRUE;
        foreach (object slot in Dm.each(get_all_slots()))
        {
            obj_item_organ existing_organ = organ_holder.get_organ_slot(slot) as obj_item_organ;
            object new_organ = get_mutant_organ_type_for_slot(slot);
            object old_organ_type = old_species?.get_mutant_organ_type_for_slot(slot);

            if (existing_organ != null && !Dm.truthy(existing_organ.get_replaceability(new_organ, old_organ_type, old_species, replace_current)))
                continue;
            // if we have an extra organ that before changing that the species didnt have, remove it
            else if (!Dm.truthy(new_organ))
            {
                if (existing_organ != null)
                {
                    await existing_organ.Remove(organ_holder);
                    qdel(existing_organ);
                }
                continue;
            }

            if (Dm.truthy(visual_only) && (!Dm.truthy(Dm.initial(new_organ as Type, (obj_item_organ p) => p.bodypart_overlay)) && !Dm.truthy(Dm.initial(new_organ as Type, (obj_item_organ p) => p.visual))))
                continue;

            object used_neworgan = FALSE;
            // new_organ = SSwardrobe.provide_type(new_organ): GAP(lead): SSwardrobe (controllers/subsystem/wardrobe.dm) is not
            // declared; the wardrobe is a pre-built stock of common types, and with an empty stock provide_type returns
            // new type_to_provide() (wardrobe.dm), which is what this does.
            new_organ = await Dm.new_async(new_organ as Type);
            object should_have = (Dm.truthy(((obj_item_organ)new_organ).get_availability(this, organ_holder)) && Dm.truthy(should_visual_organ_apply_to(new_organ as obj_item_organ, organ_holder))) ? TRUE : FALSE;

            // Check for an existing organ, and if there is one check to see if we should remove it
            float health_pct = 1;
            bool remove_existing = existing_organ != null && !Dm.@in(existing_organ.zone, excluded_zones) && !Dm.truthy(Dm.bitand(existing_organ.organ_flags, ORGAN_UNREMOVABLE));
            if (remove_existing)
            {
                health_pct = (existing_organ.maxHealth - Dm.num(existing_organ.damage)) / existing_organ.maxHealth;
                if (Dm.eq(slot, ORGAN_SLOT_BRAIN))
                {
                    obj_item_organ_brain existing_brain = existing_organ as obj_item_organ_brain;
                    await existing_brain.before_organ_replacement(new_organ as obj_item_organ);
                    await existing_brain.Remove(organ_holder, special: TRUE, movement_flags: NO_ID_TRANSFER);
                }
                else
                {
                    await existing_organ.before_organ_replacement(new_organ as obj_item_organ);
                    await existing_organ.Remove(organ_holder, special: TRUE);
                }

                QDEL_NULL(ref existing_organ);
            }
            if (existing_organ == null && Dm.truthy(should_have) && !Dm.@in(((obj_item_organ)new_organ).zone, excluded_zones) && Dm.truthy(organ_holder.get_bodypart(deprecise_zone(((obj_item_organ)new_organ).zone) as string)) && (Dm.truthy(replace_missing) || remove_existing))
            {
                used_neworgan = TRUE;
                await ((obj_item_organ)new_organ).set_organ_damage(((obj_item_organ)new_organ).maxHealth * (1 - health_pct));
                await ((obj_item_organ)new_organ).Insert(organ_holder, special: TRUE, movement_flags: DELETE_IF_REPLACED);
            }

            if (!Dm.truthy(used_neworgan))
                QDEL_NULL(ref new_organ);
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:335
    public virtual async DmTask worn_items_fit_body_check(mob_living_carbon wearer = null)
    {
        foreach (obj_item equipped_item in Dm.each<obj_item>(wearer.get_equipped_items(INCLUDE_POCKETS)))
        {
            object equipped_item_slot = wearer.get_slot_by_item(equipped_item);
            if (!Dm.truthy(await equipped_item.mob_can_equip(wearer, equipped_item_slot, bypass_equip_delay_self: TRUE, ignore_equipped: TRUE)))
                await wearer.dropItemToGround(equipped_item, force: TRUE);
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:341
    public virtual async DmTask update_no_equip_flags(mob_living_carbon wearer = null, object new_flags = null)
    {
        no_equip_flags = new_flags;
        wearer.hud_used?.update_locked_slots();
        await worn_items_fit_body_check(wearer);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:349
    public virtual void normalize_blood(mob_living_carbon_human blood_possessing_human = null)
    {
        blood_possessing_human.set_blood_volume(min(Dm.num(blood_possessing_human.get_blood_volume()), BLOOD_VOLUME_NORMAL));
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:364
    [ShouldCallParent]
    public virtual async DmTask on_species_gain(mob_living_carbon_human human_who_gained_species = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        regenerate_icons ??= TRUE;
        replace_missing ??= TRUE;

        Dm.combine(ref human_who_gained_species.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS); //Don't call update_body_parts() for every single bodypart overlay added.

        // Drop the items the new species can't wear
        Dm.combine(ref human_who_gained_species.mob_biotypes, inherent_biotypes);
        human_who_gained_species.butcher_results = knife_butcher_results?.Copy();
        // MODIFY_PHYSIOLOGY(human_who_gained_species, PHYS_COEFF_ELEC_CONDUCTIVITY, siemens_coeff) (__DEFINES/living.dm:53, a statement macro that assigns the target's
        // var: inline, rules 11.1 (a))
        LAZYINITLIST(ref human_who_gained_species.physiology);
        if (human_who_gained_species.physiology[PHYS_COEFF_ELEC_CONDUCTIVITY] == null)
            human_who_gained_species.physiology[PHYS_COEFF_ELEC_CONDUCTIVITY] = DEFAULT_PHYSIOLOGY_VAL;
        human_who_gained_species.physiology[PHYS_COEFF_ELEC_CONDUCTIVITY] = Dm.mul(human_who_gained_species.physiology[PHYS_COEFF_ELEC_CONDUCTIVITY], siemens_coeff);
        if (round(Dm.num(human_who_gained_species.physiology[PHYS_COEFF_ELEC_CONDUCTIVITY]), 0.001f) == DEFAULT_PHYSIOLOGY_VAL)
            LAZYREMOVE(ref human_who_gained_species.physiology, PHYS_COEFF_ELEC_CONDUCTIVITY);
        // MODIFY_PHYSIOLOGY(human_who_gained_species, PHYS_COEFF_HEAT, heatmod) (__DEFINES/living.dm:53, a statement macro that assigns the target's
        // var: inline, rules 11.1 (a))
        LAZYINITLIST(ref human_who_gained_species.physiology);
        if (human_who_gained_species.physiology[PHYS_COEFF_HEAT] == null)
            human_who_gained_species.physiology[PHYS_COEFF_HEAT] = DEFAULT_PHYSIOLOGY_VAL;
        human_who_gained_species.physiology[PHYS_COEFF_HEAT] = Dm.mul(human_who_gained_species.physiology[PHYS_COEFF_HEAT], heatmod);
        if (round(Dm.num(human_who_gained_species.physiology[PHYS_COEFF_HEAT]), 0.001f) == DEFAULT_PHYSIOLOGY_VAL)
            LAZYREMOVE(ref human_who_gained_species.physiology, PHYS_COEFF_HEAT);
        // MODIFY_PHYSIOLOGY(human_who_gained_species, PHYS_COEFF_COLD, coldmod) (__DEFINES/living.dm:53, a statement macro that assigns the target's
        // var: inline, rules 11.1 (a))
        LAZYINITLIST(ref human_who_gained_species.physiology);
        if (human_who_gained_species.physiology[PHYS_COEFF_COLD] == null)
            human_who_gained_species.physiology[PHYS_COEFF_COLD] = DEFAULT_PHYSIOLOGY_VAL;
        human_who_gained_species.physiology[PHYS_COEFF_COLD] = Dm.mul(human_who_gained_species.physiology[PHYS_COEFF_COLD], coldmod);
        if (round(Dm.num(human_who_gained_species.physiology[PHYS_COEFF_COLD]), 0.001f) == DEFAULT_PHYSIOLOGY_VAL)
            LAZYREMOVE(ref human_who_gained_species.physiology, PHYS_COEFF_COLD);

        //update body zones to match what they are supposed to have
        atom_movable_screen_healthdoll doll = human_who_gained_species.hud_used?.screen_objects[HUD_MOB_HEALTHDOLL] as atom_movable_screen_healthdoll;
        if (doll != null)
            doll.update_body_zones();

        if (old_species.type != type)
            await replace_body(human_who_gained_species, this, old_species);

        if (!Dm.truthy(((human_who_gained_species.get_bloodtype() as datum_blood_type)?.is_species_universal))) // Clown blood is forever.
        {
            //Assigns exotic blood type if the species has one
            if (exotic_bloodtype != null && !Dm.eq((human_who_gained_species.get_bloodtype() as datum_blood_type)?.id, exotic_bloodtype))
                human_who_gained_species.set_blood_type(exotic_bloodtype);
            //Otherwise, check if the previous species had an exotic bloodtype and we do not have one and assign a random blood type
            else if (old_species.exotic_bloodtype != null && exotic_bloodtype == null)
                human_who_gained_species.set_blood_type(random_human_blood_type());
        }

        if (Dm.truthy(character_preview_load_all_organs))
            human_who_gained_species.visual_only_organs = FALSE;

        await regenerate_organs(human_who_gained_species, old_species, replace_current: FALSE, visual_only: human_who_gained_species.visual_only_organs, replace_missing: replace_missing);
        // Update locked slots AFTER all organ and body stuff is handled
        human_who_gained_species.hud_used?.update_locked_slots();
        // Drop the items the new species can't wear
        Waiting.Detach(worn_items_fit_body_check(human_who_gained_species));

        //Resets blood if it is excessively high so they don't gib
        normalize_blood(human_who_gained_species);

        await add_body_markings(human_who_gained_species);

        if (Dm.truthy(length(inherent_traits)))
            human_who_gained_species.add_traits(inherent_traits, SPECIES_TRAIT);

        if (Dm.truthy(inherent_factions))
        {
            foreach (object i in Dm.each(inherent_factions))
                human_who_gained_species.add_faction(i);
        }

        // All languages associated with this language holder are added with source [LANGUAGE_SPECIES]
        // rather than source [LANGUAGE_ATOM], so we can track what to remove if our species changes again
        datum_language_holder gaining_holder = GLOB.prototype_language_holders[species_language_holder] as datum_language_holder;
        foreach (object language in Dm.each(gaining_holder.understood_languages))
            human_who_gained_species.grant_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_SPECIES);
        foreach (object language in Dm.each(gaining_holder.spoken_languages))
            human_who_gained_species.grant_language(language, SPOKEN_LANGUAGE, LANGUAGE_SPECIES);
        foreach (object language in Dm.each(gaining_holder.blocked_speaking))
            human_who_gained_species.add_blocked_language(language, SPOKEN_LANGUAGE, LANGUAGE_SPECIES);
        foreach (object language in Dm.each(gaining_holder.blocked_understanding))
            human_who_gained_species.add_blocked_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_SPECIES);
        if (Dm.truthy(regenerate_icons))
        {
            await human_who_gained_species.regenerate_icons();
        }

        // SEND_SIGNAL(human_who_gained_species, COMSIG_SPECIES_GAIN, src, old_species) (rules 11.1 (e))
        if (Dm.truthy(human_who_gained_species._listen_lookup?[COMSIG_SPECIES_GAIN]))
            human_who_gained_species._SendSignal(COMSIG_SPECIES_GAIN, Dm.list(human_who_gained_species, this, old_species));

        properly_gained = TRUE;

        Dm.mask(ref human_who_gained_species.living_flags, Dm.bitnot(STOP_OVERLAY_UPDATE_BODY_PARTS));

        //we don't allow it to update during species transition, so update it now
        (human_who_gained_species.hud_used?.screen_objects?[HUD_MOB_HEALTHDOLL] as atom)?.update_appearance();
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:446
    public virtual async DmTask on_species_loss(mob_living_carbon_human human = null, datum_species new_species = null, object pref_load = null, params object[] __extra)
    {
        // M10 slice F: the character's species preference sets the species again (set_species, pref_load), so the old one is lost
        Dm.combine(ref human.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS); //Don't call update_body_parts() for every single bodypart overlay removed.
        human.butcher_results = null;
        human.mob_biotypes = Dm.bitand(human.mob_biotypes, Dm.bitnot(inherent_biotypes));
        MODIFY_PHYSIOLOGY(human, PHYS_COEFF_ELEC_CONDUCTIVITY, 1 / siemens_coeff);
        MODIFY_PHYSIOLOGY(human, PHYS_COEFF_HEAT, 1 / heatmod);
        MODIFY_PHYSIOLOGY(human, PHYS_COEFF_COLD, 1 / coldmod);
        foreach (object trait in Dm.each(inherent_traits))
            REMOVE_TRAIT(human, trait, SPECIES_TRAIT);

        //If their inert mutation is not the same, swap it out
        if (!Dm.eq(inert_mutation, new_species.inert_mutation) && Dm.truthy(LAZYLEN(human.dna.mutation_index)) && Dm.@in(inert_mutation, human.dna.mutation_index))
        {
            await human.dna.remove_mutation(inert_mutation, MUTATION_SOURCE_ACTIVATED);
            //keep it at the right spot, so we can't have people taking shortcuts
            float location = human.dna.mutation_index.Find(inert_mutation);
            human.dna.mutation_index[location] = new_species.inert_mutation;
            human.dna.default_mutation_genes[location] = human.dna.mutation_index[location];
            human.dna.mutation_index[new_species.inert_mutation] = create_sequence(new_species.inert_mutation);
            human.dna.default_mutation_genes[new_species.inert_mutation] = human.dna.mutation_index[new_species.inert_mutation];
        }

        if (Dm.truthy(inherent_factions))
        {
            foreach (object i in Dm.each(inherent_factions))
                human.remove_faction(i);
        }

        clear_tail_moodlets(human);

        await remove_body_markings(human);

        // Removes all languages previously associated with [LANGUAGE_SPECIES], gaining our new species will add new ones back
        datum_language_holder losing_holder = GLOB.prototype_language_holders[species_language_holder] as datum_language_holder;
        foreach (object language in Dm.each(losing_holder.understood_languages))
            human.remove_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_SPECIES);
        foreach (object language in Dm.each(losing_holder.spoken_languages))
            human.remove_language(language, SPOKEN_LANGUAGE, LANGUAGE_SPECIES);
        foreach (object language in Dm.each(losing_holder.blocked_speaking))
            human.remove_blocked_language(language, SPOKEN_LANGUAGE, LANGUAGE_SPECIES);
        foreach (object language in Dm.each(losing_holder.blocked_understanding))
            human.remove_blocked_language(language, UNDERSTOOD_LANGUAGE, LANGUAGE_SPECIES);

        // SEND_SIGNAL(human, COMSIG_SPECIES_LOSS, src) (rules 11.1 (e), a statement)
        if (Dm.truthy(human._listen_lookup?[COMSIG_SPECIES_LOSS]))
            human._SendSignal(COMSIG_SPECIES_LOSS, Dm.list(human, this));

        human.living_flags = Dm.bitand(human.living_flags, Dm.bitnot(STOP_OVERLAY_UPDATE_BODY_PARTS));
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:492
    public virtual void randomize_main_appearance_element(mob_living_carbon_human human_mob = null)
    {
        human_mob.set_hairstyle(random_hairstyle(human_mob.gender), update: FALSE);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:497
    public virtual void randomize_active_underwear_only(mob_living_carbon_human human_mob = null)
    {
        human_mob.undershirt = random_undershirt(human_mob.gender) as string;
        human_mob.underwear = random_underwear(human_mob.gender) as string;
        human_mob.socks = random_socks() as string;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:503
    public virtual async DmTask randomize_active_underwear(mob_living_carbon_human human_mob = null)
    {
        randomize_active_underwear_only(human_mob);
        await human_mob.update_body();
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:507
    public virtual async DmTask randomize_active_features(mob_living_carbon_human human_mob = null)
    {
        DmList new_features = randomize_features() as DmList;
        foreach (object feature_key in Dm.each(new_features))
            human_mob.dna.features[feature_key] = new_features[feature_key];
        await human_mob.updateappearance(mutcolor_update: TRUE);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:520  var/static/list/organs_to_randomize (a proc-level static, rules 10.6)
    private static ref DmList randomize_features__organs_to_randomize => ref TgState.Current.datum_species__randomize_features__organs_to_randomize;

    // tg: code/modules/mob/living/carbon/human/_species.dm:516
    [ShouldCallParent]
    public virtual object randomize_features()
    {
        DmList new_features = Dm.list();
        foreach (Type organ_path in Dm.each_cast<Type>(get_organs()))
        {
            if (!Dm.truthy(Dm.initial(organ_path, (obj_item_organ p) => p.bodypart_overlay)))
                continue;
            object overlay_path = Dm.initial(organ_path, (obj_item_organ p) => p.bodypart_overlay);
            datum_bodypart_overlay_mutant sample_overlay = randomize_features__organs_to_randomize[overlay_path] as datum_bodypart_overlay_mutant;
            if (sample_overlay == null)
            {
                sample_overlay = Dm.@new(overlay_path as Type) as datum_bodypart_overlay_mutant;
                randomize_features__organs_to_randomize[overlay_path] = sample_overlay;
            }

            new_features[Dm.text("[]", sample_overlay.feature_key)] = (sample_overlay.get_random_appearance() as datum_sprite_accessory).name;
        }

        return new_features;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:534
    public virtual async DmTask<object> can_equip(obj_item I = null, object slot = null, object disable_warning = null, mob_living_carbon_human H = null, float? bypass_equip_delay_self = null, float? ignore_equipped = null, float? indirect_action = null)
    {
        bypass_equip_delay_self ??= FALSE;
        ignore_equipped ??= FALSE;
        indirect_action ??= FALSE;
        if (Dm.truthy(Dm.bitand(no_equip_flags, slot)))
        {
            if (!Dm.truthy(I.species_exception) || !Dm.truthy(is_type_in_list(this, I.species_exception)))
                return FALSE;
        }

        // if there's an item in the slot we want, fail
        if (!Dm.truthy(ignore_equipped))
        {
            if (Dm.truthy(H.get_item_by_slot(slot)))
                return FALSE;
        }

        // this check prevents us from equipping something to a slot it doesn't support, WITH the exceptions of storage slots (pockets, suit storage, and backpacks)
        // we don't require having those slots defined in the item's slot_flags, so we'll rely on their own checks further down
        if (!Dm.truthy(Dm.bitand(I.slot_flags, slot)))
        {
            float excused = FALSE;
            // Anything that's small or smaller can fit into a pocket by default
            if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_RPOCKET, ITEM_SLOT_LPOCKET))) && I.w_class <= POCKET_WEIGHT_CLASS)
                excused = TRUE;
            else if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_SUITSTORE, ITEM_SLOT_HANDS))))
                excused = TRUE;
            if (!Dm.truthy(excused))
                return FALSE;
        }

        object sw__1 = slot;
        if (Dm.eq(sw__1, ITEM_SLOT_HANDS))
        {
            if (!Dm.truthy(Dm.bitand(H.mobility_flags, MOBILITY_PICKUP)))
                return FALSE;
            if (Dm.truthy(H.get_empty_held_indexes()))
                return TRUE;
            return FALSE;
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_MASK))
        {
            if (!Dm.truthy(H.get_bodypart(BODY_ZONE_HEAD)))
                return FALSE;
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_NECK))
            return TRUE;
        else if (Dm.eq(sw__1, ITEM_SLOT_BACK))
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        else if (Dm.eq(sw__1, ITEM_SLOT_OCLOTHING))
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        else if (Dm.eq(sw__1, ITEM_SLOT_GLOVES))
        {
            if (H.num_hands == 0)
                return FALSE;
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_FEET))
        {
            if (H.num_legs < 2)
                return FALSE;
            if (Dm.truthy(Dm.bitand(H.bodytype, BODYTYPE_DIGITIGRADE)) && !Dm.truthy(Dm.bitand(I.item_flags, IGNORE_DIGITIGRADE)))
            {
                if (!Dm.truthy(Dm.bitand(I.supports_variations_flags, DIGITIGRADE_VARIATIONS)))
                {
                    if (!Dm.truthy(disable_warning))
                        to_chat(H, span_warning(I18n.T("The footwear around here isn't compatible with your feet!")));
                    return FALSE;
                }
            }
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_BELT))
        {
            obj_item_bodypart O = H.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
            if (!Dm.truthy(H.w_uniform) && !HAS_TRAIT(H, TRAIT_NO_JUMPSUIT) && (O == null || Dm.truthy(IS_ORGANIC_LIMB(O))))
            {
                if (!Dm.truthy(disable_warning))
                    to_chat(H, span_warning(I18n.T("You need a jumpsuit before you can attach this []!", I.name)));
                return FALSE;
            }
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_EYES))
        {
            if (!Dm.truthy(H.get_bodypart(BODY_ZONE_HEAD)))
                return FALSE;
            obj_item_organ_eyes eyes = H.get_organ_slot(ORGAN_SLOT_EYES) as obj_item_organ_eyes;
            if (Dm.truthy(eyes?.no_glasses))
                return FALSE;
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_HEAD))
        {
            if (!Dm.truthy(H.get_bodypart(BODY_ZONE_HEAD)))
                return FALSE;
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_EARS))
        {
            if (!Dm.truthy(H.get_bodypart(BODY_ZONE_HEAD)))
                return FALSE;
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_ICLOTHING))
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        else if (Dm.eq(sw__1, ITEM_SLOT_ID))
        {
            obj_item_bodypart O = H.get_bodypart(BODY_ZONE_CHEST) as obj_item_bodypart;
            if (!Dm.truthy(H.w_uniform) && !HAS_TRAIT(H, TRAIT_NO_JUMPSUIT) && (O == null || Dm.truthy(IS_ORGANIC_LIMB(O))))
            {
                if (!Dm.truthy(disable_warning))
                    to_chat(H, span_warning(I18n.T("You need a jumpsuit before you can attach this []!", I.name)));
                return FALSE;
            }
            return await equip_delay_self_check(I, H, bypass_equip_delay_self);
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_LPOCKET))
        {
            if (HAS_TRAIT(I, TRAIT_NODROP)) //Pockets aren't visible, so you can't move TRAIT_NODROP items into them.
                return FALSE;
            if (H.l_store != null && H.l_store != I) // no pocket swaps at all
                return FALSE;

            obj_item_bodypart O = H.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart;

            if (!Dm.truthy(H.w_uniform) && !HAS_TRAIT(H, TRAIT_NO_JUMPSUIT) && (O == null || Dm.truthy(IS_ORGANIC_LIMB(O))))
            {
                if (!Dm.truthy(disable_warning))
                    to_chat(H, span_warning(I18n.T("You need a jumpsuit before you can attach this []!", I.name)));
                return FALSE;
            }
            return TRUE;
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_RPOCKET))
        {
            if (HAS_TRAIT(I, TRAIT_NODROP))
                return FALSE;
            if (H.r_store != null && H.r_store != I)
                return FALSE;

            obj_item_bodypart O = H.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart;

            if (!Dm.truthy(H.w_uniform) && !HAS_TRAIT(H, TRAIT_NO_JUMPSUIT) && (O == null || Dm.truthy(IS_ORGANIC_LIMB(O))))
            {
                if (!Dm.truthy(disable_warning))
                    to_chat(H, span_warning(I18n.T("You need a jumpsuit before you can attach this []!", I.name)));
                return FALSE;
            }
            return TRUE;
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_SUITSTORE))
        {
            // tg: :643 (M4c slice 2: the tank on the space suit)
            if (HAS_TRAIT(I, TRAIT_NODROP))
                return FALSE;
            obj_item_clothing_suit suit = H.get_item_by_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing_suit;
            if (!(suit is obj_item_clothing_suit))
            {
                if (!Dm.truthy(disable_warning))
                    to_chat(H, span_warning(I18n.T("You need a suit before you can attach this []!", I.name)));
                return FALSE;
            }
            bool any_suit_storage = (Dm.truthy(is_type_in_typecache(I, GLOB.any_suit_storage)) || I.w_class == WEIGHT_CLASS_TINY);
            if (any_suit_storage)
                return TRUE;
            if (I.w_class > WEIGHT_CLASS_BULKY)
            {
                if (!Dm.truthy(disable_warning))
                    to_chat(H, span_warning(I18n.T("\\The [] is too big to attach!", I))); //should be src?
                return FALSE;
            }
            if (Dm.truthy(is_type_in_list(I, suit.allowed)))
                return TRUE;
            return FALSE;
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_HANDCUFFED))
        {
            if (!istype(I, typeof(obj_item_restraints_handcuffs)))
                return FALSE;
            if (H.num_hands < 2)
                return FALSE;
            return TRUE;
        }
        else if (Dm.eq(sw__1, ITEM_SLOT_LEGCUFFED))
        {
            if (!istype(I, typeof(obj_item_restraints_legcuffs)))
                return FALSE;
            if (H.num_legs < 2)
                return FALSE;
            return TRUE;
        }
        return FALSE; //Unsupported slot
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:675
    public virtual async DmTask<object> equip_delay_self_check(obj_item I = null, mob_living_carbon_human H = null, object bypass_equip_delay_self = null)
    {
        if (!Dm.truthy(I.equip_delay_self) || Dm.truthy(bypass_equip_delay_self))
            return TRUE;
        H.visible_message(span_notice(I18n.T("[] start putting on []...", H, I)), span_notice(I18n.T("You start putting on []...", I)));
        return await do_after(H, I.equip_delay_self, target: H);
    }

    /// Equips the necessary species-relevant gear before putting on the rest of the uniform.
    // tg: code/modules/mob/living/carbon/human/_species.dm:683
    public virtual async DmTask pre_equip_species_outfit(datum_job job = null, mob_living_carbon_human equipping = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        return;
    }

    /// Equips species-relevant gear after putting on an outfit.
    // tg: code/modules/mob/living/carbon/human/_species.dm:687
    public virtual async DmTask post_equip_species_outfit(mob_living_carbon_human equipping = null, float? visuals_only = null)
    {
        visuals_only ??= FALSE;
        return;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:693
    // syndicate X1a (D316): a contractor's stripped victim keeps what their species needs to live
    public virtual async DmTask give_important_for_life(mob_living_carbon_human human_to_equip = null)
    {
        if (outfit_important_for_life == null)
            return;

        await human_to_equip.equipOutfit(outfit_important_for_life);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:707
    public virtual async DmTask handle_radiation(mob_living_carbon_human source = null, object time_since_irradiated = null, object seconds_per_tick = null)
    {
        // M7 radiation slice: translated (the irradiated component's process calls it)
        float time = Dm.num(time_since_irradiated);
        if (time > RAD_MOB_KNOCKDOWN && SPT_PROB(RAD_MOB_KNOCKDOWN_PROB, seconds_per_tick))
        {
            if (!Dm.truthy(source.IsParalyzed()))
            {
                await source.emote("collapse");
            }
            await source.Paralyze(RAD_MOB_KNOCKDOWN_AMOUNT);
            to_chat(source, span_danger(I18n.T("You feel weak.")));
        }

        if (time > RAD_MOB_VOMIT && SPT_PROB(RAD_MOB_VOMIT_PROB, seconds_per_tick))
            await source.vomit(VOMIT_CATEGORY_BLOOD, lost_nutrition: 10);

        if (time > RAD_MOB_MUTATE && SPT_PROB(RAD_MOB_MUTATE_PROB, seconds_per_tick))
        {
            // to_chat(source, span_danger("You mutate!")); source.easy_random_mutate(NEGATIVE + MINOR_NEGATIVE); source.emote("gasp");
            // source.domutcheck(): a random negative mutation (dna.dm's mutations are Dm.NotTranslated)
            Dm.Deferred("code/modules/mob/living/carbon/human/_species.dm:718", "handle_radiation: the random negative mutation (easy_random_mutate, domutcheck: genetics, M8; the gasp emote, M10+)");
        }

        if (time > RAD_MOB_HAIRLOSS && SPT_PROB(RAD_MOB_HAIRLOSS_PROB, seconds_per_tick))
        {
            obj_item_bodypart_head head = source.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
            if (!(source.hairstyle == "Bald") && Dm.truthy(Dm.bitand(head?.head_flags, Dm.bitor(HEAD_HAIR, HEAD_FACIAL_HAIR))))
            {
                to_chat(source, span_danger(I18n.T("Your hair starts to fall out in clumps...")));
                _addtimer(Dm.@new<datum_callback>(this, nameof(go_bald), source), 5 * SECONDS, file: "code/modules/mob/living/carbon/human/_species.dm", line: 727);
            }
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:735
    public virtual void go_bald(mob_living_carbon_human target = null)
    {
        if (QDELETED(target)) //may be called from a timer
            return;
        target.set_facial_hairstyle("Shaved", update: FALSE);
        target.set_hairstyle("Bald"); //This calls update_body_parts()
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:745
    public virtual async DmTask<object> help(mob_living_carbon_human user = null, mob_living_carbon_human target = null, datum_martial_art attacker_style = null)
    {
        // SEND_SIGNAL(target, COMSIG_CARBON_PRE_HELP, user, attacker_style) (rules 11.1 (e), a value)
        if (Dm.truthy(Dm.bitand((!Dm.truthy(target._listen_lookup?[COMSIG_CARBON_PRE_HELP]) ? NONE : target._SendSignal(COMSIG_CARBON_PRE_HELP, Dm.list(target, user, attacker_style))), COMPONENT_BLOCK_HELP_ACT)))
            return TRUE;

        // if lying down and dead (or faking death) or not stable: cpr
        // if standing up or stable (and not faking death): shake/hug

        if (target.body_position == LYING_DOWN && (IS_DEAD_OR_FAKING(target) || target.stat != STABLE))
        {
            await user.do_cpr(target);
            return TRUE;
        }

        await target.help_shake_act(user);
        if (target != user)
            log_combat(user, target, "shaken");
        return TRUE;
    }

    ///This proc handles punching damage. IMPORTANT: Our owner is the TARGET and not the USER in this proc. For whatever reason...
    // tg: code/modules/mob/living/carbon/human/_species.dm:762
    public virtual async DmTask<object> harm(mob_living_carbon_human user = null, mob_living_carbon_human target = null, datum_martial_art attacker_style = null)
    {
        if (HAS_TRAIT(user, TRAIT_PACIFISM) && !Dm.truthy(attacker_style?.pacifist_style))
        {
            to_chat(user, span_warning(I18n.T("You don't want to harm []!", target)));
            return FALSE;
        }

        obj_item_organ_brain brain = user.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
        obj_item_bodypart attacking_bodypart = (attacker_style?.get_attacking_limb(user, target) is var or__1 && Dm.truthy(or__1) ? or__1
            : brain?.get_attacking_limb(target) is var or__2 && Dm.truthy(or__2) ? or__2 : user.get_active_hand()) as obj_item_bodypart;

        float atk_verb_index = rand(1, length(attacking_bodypart.unarmed_attack_verbs));
        object atk_verb = attacking_bodypart.unarmed_attack_verbs[atk_verb_index];
        object atk_verb_continuous = Dm.text("[]s", atk_verb);
        if (length(attacking_bodypart.unarmed_attack_verbs_continuous) >= atk_verb_index) // Just in case
            atk_verb_continuous = attacking_bodypart.unarmed_attack_verbs_continuous[atk_verb_index];

        string atk_effect = attacking_bodypart.unarmed_attack_effect;

        if (atk_effect == ATTACK_EFFECT_BITE)
        {
            if (Dm.truthy(user.is_mouth_covered(ITEM_SLOT_MASK)))
            {
                if (Dm.truthy(user.get_active_hand())) //In the event we can't bite, emergency swap to see if we can attack with a hand.
                {
                    attacking_bodypart = user.get_active_hand() as obj_item_bodypart;
                    atk_verb_index = rand(1, length(attacking_bodypart.unarmed_attack_verbs));
                    atk_verb = attacking_bodypart.unarmed_attack_verbs[atk_verb_index];
                    atk_verb_continuous = Dm.text("[]s", atk_verb);
                    if (length(attacking_bodypart.unarmed_attack_verbs_continuous) >= atk_verb_index) // Just in case
                        atk_verb_continuous = attacking_bodypart.unarmed_attack_verbs_continuous[atk_verb_index];
                    atk_effect = attacking_bodypart.unarmed_attack_effect;
                }
                else //Nothing? Okay. Fail.
                {
                    user.balloon_alert(user, I18n.T("can't attack!"));
                    return FALSE;
                }
            }
        }

        await user.do_attack_animation(target, atk_effect);

        //has our target been shoved recently? If so, they're staggered and we get an easy hit.
        object staggered = target.has_status_effect(typeof(datum_status_effect_staggered));

        //Someone in a grapple is much more vulnerable to being harmed by punches.
        bool grappled = target.pulledby != null && target.pulledby.grab_state >= GRAB_AGGRESSIVE;

        // Our lower and upper unarmed damage values. Damage is rolled between these two values.
        float lower_unarmed_damage = attacking_bodypart.unarmed_damage_low;
        float upper_unarmed_damage = attacking_bodypart.unarmed_damage_high;

        // The presence of TRAIT_STRENGTH increases our upper unarmed damage. This is a damage cap increase.
        upper_unarmed_damage += HAS_TRAIT(user, TRAIT_STRENGTH) ? 2 : 0;

        // Out athletics skill is used to set our potential base damage roll. It won't increase our potential damage roll, but will make our unarmed attack more consistent.
        // For a normal human arm, this would cap at 10, and for a normal human leg, this would go up to 14.
        lower_unarmed_damage = Dm.num(Dm.min(Dm.add(lower_unarmed_damage, (user.mind?.get_skill_level(typeof(datum_skill_athletics)) is var or__3 && Dm.truthy(or__3) ? or__3 : 0)), upper_unarmed_damage));

        // The actual damage roll. May still be augmented by further factors.
        float damage = rand(lower_unarmed_damage, upper_unarmed_damage);
        // Limb accuracy is used to determine miss probabilities (higher the value, the less likely you are to miss), armor penetration (if entitled) and the possible result from a stagger combo hit.
        float limb_accuracy = attacking_bodypart.unarmed_effectiveness;
        // Limb sharpness determines the type of wounds this unarmed strike could possibly roll. By default, most limbs are blunt and have no sharpness.
        float limb_sharpness = attacking_bodypart.unarmed_sharpness;

        if (grappled)
        {
            float pummel_bonus = attacking_bodypart.unarmed_pummeling_bonus;
            damage = MathF.Floor(damage * pummel_bonus);
            limb_accuracy = MathF.Floor(limb_accuracy * pummel_bonus);
        }

        //Get our puncher's combined brute and burn damage.
        float puncher_brute_and_burn = Dm.num(user.get_fire_loss()) + Dm.num(user.get_brute_loss());

        //Get our targets combined brute and burn damage.
        float target_brute_and_burn = Dm.num(target.get_fire_loss()) + Dm.num(target.get_brute_loss());

        // In a brawl, drunkenness can make you swing more wildly and with more force, and thus catch your opponent off guard, but it could also totally throw you off if you're too intoxicated
        // But god is it going to make you sick moving too much while drunk
        float user_drunkenness = Dm.num(user.get_drunk_amount());

        if (Dm.truthy(user_drunkenness))
        {
            if (HAS_TRAIT(user, TRAIT_DRUNKEN_BRAWLER)) // Drunken brawlers only need to be intoxicated, doesn't matter how much
            {
                limb_accuracy += Dm.clamp(puncher_brute_and_burn / 2, 10, 200);
                damage += damage * Dm.clamp(puncher_brute_and_burn / 100, 0.3f, 2); //Basically a multiplier of how much extra damage you get based on how low your health is overall. A floor of about a 30%.
                object drunken_martial_descriptor = pick("Drunken", "Intoxicated", "Tipsy", "Inebriated", "Delirious", "Day-Drinker's", "Firegut", "Blackout");
                atk_verb = Dm.text("[] []", drunken_martial_descriptor, capitalize(atk_verb));
                atk_verb_continuous = Dm.text("[] []", drunken_martial_descriptor, capitalize(atk_verb_continuous));
            }
            else if (user_drunkenness >= 60)
            {
                limb_accuracy = -limb_accuracy; // good luck landing a punch now, you drunk fuck
                user.adjust_disgust(5);
            }
            else if (user_drunkenness >= 30)
            {
                limb_accuracy *= 1.2f;
                user.adjust_disgust(2);
            }
        }

        // Select a zone to hit, blacklisting the part we're attacking with if we're attacking ourselves.
        object hit_zone = target.get_random_valid_zone(user.zone_selected, blacklisted_parts: user == target ? Dm.list(attacking_bodypart.body_zone) : null);
        obj_item_bodypart affecting = target.get_bodypart(Dm.str(hit_zone)) as obj_item_bodypart;

        float miss_chance = 100;//calculate the odds that a punch misses entirely. considers stamina and brute damage of the puncher. punches miss by default to prevent weird cases
        if (Dm.truthy(lower_unarmed_damage))
        {
            if ((target.body_position == LYING_DOWN) || HAS_TRAIT(user, TRAIT_PERFECT_ATTACKER) || Dm.truthy(staggered) || Dm.truthy(user_drunkenness) && HAS_TRAIT(user, TRAIT_DRUNKEN_BRAWLER)) //kicks and attacks against staggered targets never miss (provided your species deals more than 0 damage). Drunken brawlers while drunk also don't miss
                miss_chance = 0;
            else
                miss_chance = Dm.clamp(UNARMED_MISS_CHANCE_BASE - limb_accuracy + (puncher_brute_and_burn / 2), 0, UNARMED_MISS_CHANCE_MAX); //Limb miss chance + various damage. capped at 80 so there is at least a chance to land a hit.
        }

        if (!Dm.truthy(damage) || affecting == null || prob(miss_chance))//future-proofing for species that have 0 damage/weird cases where no zone is targeted
        {
            playsound(target.loc, attacking_bodypart.unarmed_miss_sound, 25, TRUE, -1); // tg: code/modules/mob/living/carbon/human/_species.dm:861
            target.visible_message(span_danger(I18n.T("[]'s [] misses []!", user, atk_verb, target)),
                            span_danger(I18n.T("You avoid []'s []!", user, atk_verb)), span_hear(I18n.T("You hear a swoosh!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] misses []!", atk_verb, target)));
            log_combat(user, target, "attempted to punch");
            return FALSE;
        }

        float armor_block = Dm.num(target.run_armor_check(affecting, MELEE));

        // In a brawl, drunkenness is a boon if you're a bit drunk but not too much. Else you're easier to hit.
        // But, generally, getting hit while drunk is probably a good way to start throwing up
        float target_drunkenness = Dm.num(target.get_drunk_amount());

        if (Dm.truthy(target_drunkenness))
        {
            if (HAS_TRAIT(target, TRAIT_DRUNKEN_BRAWLER)) // Drunken brawlers only need to be intoxicated, doesn't matter how much
                armor_block += 20;
            else if (target_drunkenness >= 60)
            {
                armor_block *= 0.5f;
                target.adjust_disgust(5);
            }
            else if (target_drunkenness >= 30)
            {
                armor_block += 10;
                target.adjust_disgust(2);
            }
        }

        if (grappled && Dm.truthy(attacking_bodypart.grappled_attack_verb))
        {
            atk_verb = attacking_bodypart.grappled_attack_verb;
            atk_verb_continuous = attacking_bodypart.grappled_attack_verb_continuous;
        }

        target.visible_message(span_danger(I18n.T("[] [] []!", user, atk_verb_continuous, target)),
                        span_userdanger(I18n.T("[] [] you!", user, atk_verb_continuous)), span_hear(I18n.T("You hear a sickening sound of flesh hitting flesh!")), COMBAT_MESSAGE_RANGE, user);
        to_chat(user, span_danger(I18n.T("You [] []!", atk_verb, target)));

        target.lastattacker = user.real_name;
        target.lastattackerckey = user.ckey;

        if (Dm.truthy(user.limb_destroyer))
            await target.dismembering_strike(user, affecting.body_zone);

        float attack_direction = get_dir(user, target);
        string attack_type = attacking_bodypart.attack_type;
        bool kicking = atk_effect == ATTACK_EFFECT_KICK;
        float final_armor_block = armor_block;

        // tg: code/modules/mob/living/carbon/human/_species.dm:905
        object smack_sound = attacking_bodypart.unarmed_attack_sound;
        bool smack_attack = (atk_effect == ATTACK_EFFECT_KICK || atk_effect == ATTACK_EFFECT_PUNCH || atk_effect == ATTACK_EFFECT_SMASH);
        if (smack_attack && attack_type == BRUTE && Dm.truthy(Dm.bitand(affecting.bodytype, BODYTYPE_ROBOTIC)))
            smack_sound = Dm.resource("sound/effects/bang.ogg");

        playsound(target, smack_sound, atk_effect == ATTACK_EFFECT_SMASH ? 33 : 25, TRUE, -1); // tg: code/modules/mob/living/carbon/human/_species.dm:910

        if (kicking || grappled) //kicks and punches when grappling bypass armor slightly.
        {
            if (damage >= 12 || (damage >= 9 && prob(66)))
                target.force_say();
            log_combat(user, target, grappled ? "grapple punched" : "kicked");
            final_armor_block -= limb_accuracy;
            await target.apply_damage(damage, attack_type, affecting, final_armor_block, attack_direction: attack_direction, sharpness: limb_sharpness);
        }
        else // Normal attacks do not gain the benefit of armor penetration.
        {
            await target.apply_damage(damage, attack_type, affecting, armor_block, attack_direction: attack_direction, sharpness: limb_sharpness);
            if (damage >= 12 || (damage >= 9 && prob(66)))
                target.force_say();
            log_combat(user, target, "punched");
        }

        if (Dm.truthy(target._listen_lookup?[COMSIG_HUMAN_GOT_PUNCHED]))
            target._SendSignal(COMSIG_HUMAN_GOT_PUNCHED, Dm.list(target, user, damage, attack_type, atk_effect, affecting, final_armor_block, limb_sharpness));
        if (Dm.truthy(user._listen_lookup?[COMSIG_HUMAN_PUNCHED]))
            user._SendSignal(COMSIG_HUMAN_PUNCHED, Dm.list(user, target, damage, attack_type, atk_effect, affecting, final_armor_block, limb_sharpness));

        // If our target is staggered and has sustained enough damage, we can apply a randomly determined status effect to inflict when we punch them.
        // The effects are based on the punching effectiveness of our attacker. Some effects are not reachable by the average human, and require augmentation to reach or being a species with a heavy punch effectiveness.
        // Or they're just drunk enough.
        if (HAS_TRAIT(target, TRAIT_BRAWLING_KNOCKDOWN_BLOCKED) || target.stat == DEAD) //If our target is dead or has specailized armor, there is no way to inflict these effects.
            return null;

        // If our target is staggered, the target's armor, minus our limb effectiveness sets the minimum necessary amount of damage sustained to cause an effect. We clamp the value for sanity reasons.
        float effective_armor = Dm.max(armor_block, UNARMED_COMBO_HIT_HEALTH_BASE) - limb_accuracy;
        if (Dm.truthy(staggered) && target_brute_and_burn >= Dm.clamp(effective_armor, 0, 200))
            await stagger_combo(user, target, Dm.str(atk_verb), limb_accuracy, armor_block);
        return null;
    }

    /// Handles the stagger combo effect of our punch. Follows the same logic as the above proc, target is our owner, user is our attacker.
    // tg: code/modules/mob/living/carbon/human/_species.dm:939
    public virtual async DmTask stagger_combo(mob_living_carbon_human user = null, mob_living_carbon_human target = null, string atk_verb = null, float? limb_accuracy = null, float? armor_block = null)
    {
        atk_verb ??= "hit";
        limb_accuracy ??= 0;
        armor_block ??= 0;
        // Randomly determines the effects of our punch. Limb accuracy is a bonus, armor block is a defense, attacker athletics provides a minor to significant bonus.
        float roll_them_bones = rand(-20, 20) + limb_accuracy.Value - armor_block.Value + Dm.num((Dm.div(user.mind?.get_skill_modifier(typeof(datum_skill_athletics), SKILL_RANDS_MODIFIER), 2) is var or__1 && Dm.truthy(or__1) ? or__1 : 0));

        if (roll_them_bones <= 0) //Mostly a gimmie, this one just keeps them staggered briefly
        {
            await target.adjust_staggered_up_to(1 * SECONDS, 10 * SECONDS);
            target.visible_message(span_warning(I18n.T("[]'s [] briefly winds []!", user, atk_verb, target)),
                span_warning(I18n.T("You are briefly winded by []'s []!", user, atk_verb)), span_hear(I18n.T("You hear a thud!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] briefly winds []!", atk_verb, target)));
        }
        else if (roll_them_bones >= 1 && roll_them_bones <= 10)
        {
            await target.adjust_eye_blur_up_to(5 * SECONDS, 10 * SECONDS);
            target.visible_message(span_warning(I18n.T("[]'s [] hits [] so hard, their eyes water! Ouch!", user, atk_verb, target)),
                span_warning(I18n.T("You are hit viciously by []'s [], and your eyes begin to water!", user, atk_verb)), span_hear(I18n.T("You hear a thud!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] causes [] to tear up!", atk_verb, target)));
        }
        else if (roll_them_bones >= 11 && roll_them_bones <= 30)
        {
            await target.adjust_dizzy_up_to(5 * SECONDS, 10 * SECONDS);
            await target.adjust_eye_blur_up_to(5 * SECONDS, 10 * SECONDS);
            await target.adjust_confusion_up_to(5 * SECONDS, 10 * SECONDS);
            target.visible_message(span_warning(I18n.T("[]'s [] hits [] so hard, they are sent reeling in agony! Damn!", user, atk_verb, target)),
                span_warning(I18n.T("You are hit viciously by []'s [], and everything becomes a dizzying blur!", user, atk_verb)), span_hear(I18n.T("You hear a thud!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] causes [] to go stumbling about in a confuzed daze!", atk_verb, target)));
        }
        else if (roll_them_bones >= 31 && roll_them_bones <= 40)
        {
            await target.adjust_dizzy_up_to(5 * SECONDS, 10 * SECONDS);
            await target.adjust_confusion_up_to(5 * SECONDS, 10 * SECONDS);
            await target.adjust_temp_blindness_up_to(5 * SECONDS, 10 * SECONDS);
            target.visible_message(span_warning(I18n.T("[]'s [] hits [] so hard, they are sent reeling blindly in agony! Goddamn!", user, atk_verb, target)),
                span_warning(I18n.T("You are hit viciously by []'s [], and everything becomes a dizzying, blinding blur!", user, atk_verb)), span_hear(I18n.T("You hear a thud!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] causes [] to go stumbling about in a confuzed, blind daze!", atk_verb, target)));
        }
        else if (roll_them_bones >= 41 && roll_them_bones <= 45)
        {
            await target.apply_effect(4 * SECONDS, EFFECT_KNOCKDOWN, armor_block);
            target.visible_message(span_warning(I18n.T("[]'s [] hits [] so hard, you knock them off their feet! Holy shit!", user, atk_verb, target)),
                span_warning(I18n.T("You are hit viciously by []'s [] and sent toppling head over heels!", user, atk_verb)), span_hear(I18n.T("You hear a sickening thud!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] lands, and you send [] sailing off their feet!", atk_verb, target)));
        }
        else if (roll_them_bones >= 46)
        {
            await target.apply_effect(4 * SECONDS, EFFECT_KNOCKDOWN, armor_block);
            obj_item_bodypart affecting = target.get_bodypart(Dm.str(target.get_random_valid_zone(user.zone_selected))) as obj_item_bodypart;
            await target.apply_damage(5, BRUTE, affecting, armor_block, wound_bonus: limb_accuracy * 2); //Mostly for the crunchy wounding effect than actually doing damage
            target.visible_message(span_warning(I18n.T("[]'s [] hits [] so hard, you hit them off their feet with a loud crunch! Fucking hell!", user, atk_verb, target)),
                span_warning(I18n.T("You are hit viciously by []'s [], and suddenly feel an overwhelming pain as you topple head over heels!", user, atk_verb)), span_hear(I18n.T("You hear a sickening crack and a loud thud!")), COMBAT_MESSAGE_RANGE, user);
            to_chat(user, span_warning(I18n.T("Your [] lands, and [] is sent crashing to the floor with the immense force! Good god!", atk_verb, target)));
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:987
    public virtual async DmTask<object> disarm(mob_living_carbon_human user = null, mob_living_carbon_human target = null, datum_martial_art attacker_style = null)
    {
        if (user.body_position != STANDING_UP)
            return FALSE;
        if (user == target)
            return FALSE;
        // DEVIATION(DV17): two capsule mobs may share a turf (D105, DV16), so tg's same-turf refusal (it stands here as well as in
        // can_disarm) does not apply to them: the shove goes on to /mob/living/proc/disarm, whose direction comes from the
        // capsule centres.
        if (user.loc == target.loc && !(LocomotionSeam.IsRegistered(user) && LocomotionSeam.IsRegistered(target)))
            return FALSE;
        await user.disarm(target);
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:996
    public virtual async DmTask spec_attack_hand(mob_living_carbon_human owner = null, mob_living_carbon_human target = null, datum_martial_art attacker_style = null, object modifiers = null)
    {
        if (!(owner is mob_living_carbon_human))
            return;
        // CHECK_DNA_AND_SPECIES(owner) and CHECK_DNA_AND_SPECIES(target) (__DEFINES/DNA.dm:3, a statement macro that returns
        // from the caller: inline, rules 11.1 (c))
        if (!Dm.truthy(owner.dna?.species))
            return;
        if (!Dm.truthy(target.dna?.species))
            return;

        if (!(owner is mob_living_carbon_human)) //sanity check for drones.
            return;
        if (owner.mind != null)
            attacker_style = LAZYACCESS(owner.martial_arts, 1) as datum_martial_art; // GET_ACTIVE_MARTIAL_ART(owner) (__DEFINES/combat.dm:353), inline
        if ((owner != target) && Dm.truthy(await target.check_block(owner, 0, owner.name, attack_type: UNARMED_ATTACK)))
        {
            log_combat(owner, target, "attempted to touch");
            target.visible_message(span_warning(I18n.T("[] attempts to touch []!", owner, target)),
                            span_danger(I18n.T("[] attempts to touch you!", owner)), span_hear(I18n.T("You hear a swoosh!")), COMBAT_MESSAGE_RANGE, owner);
            to_chat(owner, span_warning(I18n.T("You attempt to touch []!", target)));
            return;
        }

        // SEND_SIGNAL(owner, COMSIG_MOB_ATTACK_HAND, owner, target, attacker_style) (rules 11.1 (e), a statement)
        if (Dm.truthy(owner._listen_lookup?[COMSIG_MOB_ATTACK_HAND]))
            owner._SendSignal(COMSIG_MOB_ATTACK_HAND, Dm.list(owner, owner, target, attacker_style));

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            await disarm(owner, target, attacker_style);
            return; // dont attack after
        }
        if (Dm.truthy(owner.combat_mode))
            await harm(owner, target, attacker_style);
        else
            await help(owner, target, attacker_style);
    }

    /**
     * Environment handler for species
     *
     * vars:
     * * environment (required) The environment gas mix
     * * humi (required)(type: /mob/living/carbon/human) The mob we will target
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1034
    public virtual async DmTask handle_environment(mob_living_carbon_human humi = null, datum_gas_mixture environment = null, object seconds_per_tick = null)
    {
        await handle_environment_pressure(humi, environment, seconds_per_tick);
        await handle_gas_interaction(humi, environment, seconds_per_tick);
    }

    /**
     * Body temperature handler for species
     *
     * These procs manage body temp, bamage, and alerts
     * Some of these will still fire when not alive to balance body temp to the room temp.
     * vars:
     * * humi (required)(type: /mob/living/carbon/human) The mob we will target
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1046
    public virtual async DmTask handle_body_temperature(mob_living_carbon_human humi = null, object seconds_per_tick = null)
    {
        // When in a cryo unit we suspend all natural body regulation
        if (istype(humi.loc, typeof(obj_machinery_cryo_cell)))
            return;

        // Only stabilise core temp when alive and not in statis
        if (humi.stat != DEAD && !HAS_TRAIT(humi, TRAIT_STASIS) && !HAS_TRAIT(humi, TRAIT_COLD_BLOODED))
            body_temperature_core(humi, seconds_per_tick);

        // These do run in statis
        await body_temperature_skin(humi, seconds_per_tick);
        await body_temperature_alerts(humi);

        // Do not cause more damage in statis
        if (!HAS_TRAIT(humi, TRAIT_STASIS))
            await body_temperature_damage(humi, seconds_per_tick);
    }

    /**
     * Used to stabilize the core temperature back to normal on living mobs
     *
     * The metabolisim heats up the core of the mob trying to keep it at the normal body temp
     * vars:
     * * humi (required) The mob we will stabilize
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1070
    public virtual void body_temperature_core(mob_living_carbon_human humi = null, object seconds_per_tick = null)
    {
        float natural_change = get_temp_change_amount(Dm.num(humi.get_body_temp_normal()) - humi.coretemperature, BODYTEMP_CORE_CHANGE_RATE * Dm.num(seconds_per_tick));
        humi.adjust_coretemperature(humi.metabolism_efficiency * natural_change);
    }

    /**
     * Used to normalize the skin temperature on living mobs
     *
     * The core temp effects the skin, then the enviroment effects the skin, then we refect that back to the core.
     * This happens even when dead so bodies revert to room temp over time.
     * vars:
     * * humi (required) The mob we will targeting
     * - seconds_per_tick: The amount of time that is considered as elapsing
     * - times_fired: The number of times SSmobs has fired
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1084
    public virtual async DmTask body_temperature_skin(mob_living_carbon_human humi = null, object seconds_per_tick = null)
    {
        // change the core based on the skin temp
        float skin_core_diff = humi.bodytemperature - humi.coretemperature;
        // change rate of slightly below area to skin change rate and still having a solid curve
        float skin_core_change = get_temp_change_amount(skin_core_diff, BODYTEMP_SKIN_CORE_CHANGE_RATE * Dm.num(seconds_per_tick));

        humi.adjust_coretemperature(skin_core_change);

        // get the enviroment details of where the mob is standing
        datum_gas_mixture environment = humi.loc == null ? null : await humi.loc.return_air() as datum_gas_mixture;
        if (environment == null) // if there is no environment (nullspace) drop out here.
            return;

        // Get the temperature of the environment for area
        float area_temp = Dm.num(await humi.get_temperature(environment));

        // Get the insulation value based on the area's temp
        float thermal_protection = Dm.num(humi.get_insulation_protection(area_temp));

        // Changes to the skin temperature based on the area
        float area_skin_diff = area_temp - humi.bodytemperature;
        if (!Dm.truthy(humi.on_fire) || area_skin_diff > 0)
        {
            // change rate near full speed as area temp has large impact on the surface
            float area_skin_change = get_temp_change_amount(area_skin_diff, BODYTEMP_AREA_SKIN_CHANGE_RATE * Dm.num(seconds_per_tick));

            // We need to apply the thermal protection of the clothing when applying area to surface change
            // If the core bodytemp goes over the normal body temp you are overheating and becom sweaty
            // This will cause the insulation value of any clothing to reduced in effect (70% normal rating)
            // we add 10 degree over normal body temp before triggering as thick insulation raises body temp
            if (Dm.num(humi.get_body_temp_normal(apply_change: FALSE)) + 10 < humi.coretemperature)
            {
                // we are overheating and sweaty insulation is not as good reducing thermal protection
                area_skin_change = (1 - (thermal_protection * 0.7f)) * area_skin_change;
            }
            else
                area_skin_change = (1 - thermal_protection) * area_skin_change;

            humi.adjust_bodytemperature(area_skin_change);
        }

        // Core to skin temp transfer, when not on fire
        if (!Dm.truthy(humi.on_fire))
        {
            // Get the changes to the skin from the core temp
            float core_skin_diff = humi.coretemperature - humi.bodytemperature;
            // change rate to reflect temp back to the skin at a slightly higher rate than skin to core
            float core_skin_change = (1 + thermal_protection) * get_temp_change_amount(core_skin_diff, BODYTEMP_CORE_SKIN_CHANGE_RATE * Dm.num(seconds_per_tick));

            // We do not want to over shoot after using protection
            if (core_skin_diff > 0)
                core_skin_change = min(core_skin_change, core_skin_diff);
            else
                core_skin_change = max(core_skin_change, core_skin_diff);

            humi.adjust_bodytemperature(core_skin_change);
        }
    }

    /**
     * Used to set alerts and debuffs based on body temperature
     * vars:
     * * humi (required) The mob we will targeting
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1143
    public virtual async DmTask body_temperature_alerts(mob_living_carbon_human humi = null)
    {
        float old_bodytemp = humi.old_bodytemperature;
        float bodytemp = humi.bodytemperature;
        // Body temperature is too hot, and we do not have resist traits
        if (bodytemp > bodytemp_heat_damage_limit && !HAS_TRAIT(humi, TRAIT_RESISTHEAT))
        {
            // Clear cold mood and apply hot mood
            humi.clear_mood_event("cold");
            await humi.add_mood_event("hot", typeof(datum_mood_event_hot));

            //Remove any slowdown from the cold.
            humi.remove_movespeed_modifier(typeof(datum_movespeed_modifier_cold));
            // display alerts based on how hot it is
            // Can't be a switch due to http://www.byond.com/forum/post/2750423
            if (bodytemp >= bodytemp_heat_damage_limit && bodytemp <= BODYTEMP_HEAT_WARNING_2)
                humi.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), 1);
            else if (bodytemp >= BODYTEMP_HEAT_WARNING_2 && bodytemp <= BODYTEMP_HEAT_WARNING_3)
                humi.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), 2);
            else
                humi.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_hot), 3);
        }
        // Body temperature is too cold, and we do not have resist traits
        else if (bodytemp < bodytemp_cold_damage_limit && !HAS_TRAIT(humi, TRAIT_RESISTCOLD) && !Dm.truthy(humi.has_status_effect(typeof(datum_status_effect_inebriated))))
        {
            // clear any hot moods and apply cold mood
            humi.clear_mood_event("hot");
            await humi.add_mood_event("cold", typeof(datum_mood_event_cold));
            // Apply cold slow down
            await humi.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_cold), multiplicative_slowdown: ((bodytemp_cold_damage_limit - humi.bodytemperature) / COLD_SLOWDOWN_FACTOR));
            // Display alerts based how cold it is
            // Can't be a switch due to http://www.byond.com/forum/post/2750423
            if (bodytemp >= BODYTEMP_COLD_WARNING_2 && bodytemp <= bodytemp_cold_damage_limit)
                humi.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_cold), 1);
            else if (bodytemp >= BODYTEMP_COLD_WARNING_3 && bodytemp <= BODYTEMP_COLD_WARNING_2)
                humi.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_cold), 2);
            else
                humi.throw_alert(ALERT_TEMPERATURE, typeof(atom_movable_screen_alert_cold), 3);
        }
        // We are not to hot or cold, remove status and moods
        // Optimization here, we check these things based off the old temperature to avoid unneeded work
        // We're not perfect about this, because it'd just add more work to the base case, and resistances are rare
        else if (old_bodytemp > bodytemp_heat_damage_limit || old_bodytemp < bodytemp_cold_damage_limit)
        {
            humi.clear_alert(ALERT_TEMPERATURE);
            humi.remove_movespeed_modifier(typeof(datum_movespeed_modifier_cold));
            humi.clear_mood_event("cold");
            humi.clear_mood_event("hot");
        }

        // Store the old bodytemp for future checking
        humi.old_bodytemperature = bodytemp;
    }

    /**
     * Used to apply wounds and damage based on core/body temp
     * vars:
     * * humi (required) The mob we will targeting
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1196
    public virtual async DmTask body_temperature_damage(mob_living_carbon_human humi = null, object seconds_per_tick = null)
    {
        //If the body temp is above the wound limit start adding exposure stacks
        if (humi.bodytemperature > BODYTEMP_HEAT_WOUND_LIMIT)
            humi.heat_exposure_stacks = min(humi.heat_exposure_stacks + (0.5f * Dm.num(seconds_per_tick)), 40);
        else //When below the wound limit, reduce the exposure stacks fast.
            humi.heat_exposure_stacks = max(humi.heat_exposure_stacks - (2 * Dm.num(seconds_per_tick)), 0);

        //when exposure stacks are greater then 10 + rand20 try to apply wounds and reset stacks
        if (humi.heat_exposure_stacks > (10 + rand(0, 20)))
        {
            await apply_burn_wounds(humi, seconds_per_tick);
            humi.heat_exposure_stacks = 0;
        }

        // Body temperature is too hot, and we do not have resist traits
        // Apply some burn damage to the body
        if (humi.coretemperature > bodytemp_heat_damage_limit && !HAS_TRAIT(humi, TRAIT_RESISTHEAT))
        {
            float firemodifier = humi.fire_stacks / 50;
            if (!Dm.truthy(humi.on_fire)) // We are not on fire, reduce the modifier
                firemodifier = min(firemodifier, 0);

            // this can go below 5 at log 2.5
            float burn_damage = max(log(2 - firemodifier, (humi.coretemperature - Dm.num(humi.get_body_temp_normal(apply_change: FALSE)))) - 5, 0);

            // Apply species and physiology modifiers to heat damage
            burn_damage = burn_damage * Dm.num(GET_PHYSIOLOGY(humi, PHYS_COEFF_HEAT)) * 0.5f * Dm.num(seconds_per_tick);

            // 40% for level 3 damage on humans to scream in pain
            if (!IS_UNCONSCIOUS(humi) && ((prob(burn_damage) ? 1 : 0) * 10) / 4 != 0)
            {
                Waiting.Detach(humi.emote("scream")); // INVOKE_ASYNC
            }

            // Apply the damage to all body parts
            await humi.apply_damage(burn_damage, BURN, spread_damage: TRUE, wound_clothing: FALSE);
        }

        // For cold damage, we cap at the threshold if you're dead
        if (Dm.num(humi.get_fire_loss()) >= abs(humi.dead_threshold) && humi.stat == DEAD)
            return;

        // Apply some burn / brute damage to the body (Dependent if the person is hulk or not)
        bool is_hulk = HAS_TRAIT(humi, TRAIT_HULK);

        float cold_damage_limit = bodytemp_cold_damage_limit + (is_hulk ? BODYTEMP_HULK_COLD_DAMAGE_LIMIT_MODIFIER : 0);

        if (humi.coretemperature < cold_damage_limit && !HAS_TRAIT(humi, TRAIT_RESISTCOLD))
        {
            string damage_type = is_hulk ? BRUTE : BURN; // Why?
            float damage_mod = Dm.num(GET_PHYSIOLOGY(humi, PHYS_COEFF_COLD));
            // Can't be a switch due to http://www.byond.com/forum/post/2750423
            if (humi.coretemperature >= 201 && humi.coretemperature <= cold_damage_limit)
                await humi.apply_damage(COLD_DAMAGE_LEVEL_1 * damage_mod * Dm.num(seconds_per_tick), damage_type, wound_clothing: FALSE);
            else if (humi.coretemperature >= 120 && humi.coretemperature <= 200)
                await humi.apply_damage(COLD_DAMAGE_LEVEL_2 * damage_mod * Dm.num(seconds_per_tick), damage_type, wound_clothing: FALSE);
            else
                await humi.apply_damage(COLD_DAMAGE_LEVEL_3 * damage_mod * Dm.num(seconds_per_tick), damage_type, wound_clothing: FALSE);
        }
    }

    /**
     * Used to apply burn wounds on random limbs
     *
     * This is called from body_temperature_damage when exposure to extream heat adds up and causes a wound.
     * The wounds will increase in severity as the temperature increases.
     * vars:
     * * humi (required) The mob we will targeting
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1257
    public virtual async DmTask apply_burn_wounds(mob_living_carbon_human humi = null, object seconds_per_tick = null)
    {
        // If we are resistant to heat exit
        if (HAS_TRAIT(humi, TRAIT_RESISTHEAT))
            return;

        // If our body temp is to low for a wound exit
        if (humi.bodytemperature < BODYTEMP_HEAT_WOUND_LIMIT)
            return;

        // Lets pick a random body part and check for an existing burn
        obj_item_bodypart bodypart = pick(humi.get_bodyparts() as DmList) as obj_item_bodypart;
        datum_wound existing_burn = null;
        foreach (datum_wound iterated_wound in Dm.each_cast<datum_wound>(bodypart.wounds))
        {
            datum_wound_pregen_data pregen_data = (datum_wound_pregen_data)iterated_wound.get_pregen_data();
            if (Dm.@in(pregen_data.wound_series, GLOB.wounding_types_to_series[WOUND_BURN]))
            {
                existing_burn = iterated_wound;
                break;
            }
        }
        // If we have an existing burn try to upgrade it
        object severity = null;
        if (existing_burn != null)
        {
            switch (existing_burn.severity)
            {
                case WOUND_SEVERITY_MODERATE:
                    if (humi.bodytemperature > BODYTEMP_HEAT_WOUND_LIMIT + 400) // 800k
                        severity = WOUND_SEVERITY_SEVERE;
                    break;
                case WOUND_SEVERITY_SEVERE:
                    if (humi.bodytemperature > BODYTEMP_HEAT_WOUND_LIMIT + 2800) // 3200k
                        severity = WOUND_SEVERITY_CRITICAL;
                    break;
            }
        }
        else // If we have no burn apply the lowest level burn
            severity = WOUND_SEVERITY_MODERATE;

        await humi.cause_wound_of_type_and_severity(WOUND_BURN, bodypart, severity, wound_source: "hot temperatures");

        // always take some burn damage
        float burn_damage = HEAT_DAMAGE_LEVEL_1;
        if (humi.bodytemperature > BODYTEMP_HEAT_WOUND_LIMIT + 400)
            burn_damage = HEAT_DAMAGE_LEVEL_2;
        if (humi.bodytemperature > BODYTEMP_HEAT_WOUND_LIMIT + 2800)
            burn_damage = HEAT_DAMAGE_LEVEL_3;

        await humi.apply_damage(burn_damage * Dm.num(seconds_per_tick), BURN, bodypart, wound_clothing: FALSE);
    }

    /// Handle the air pressure of the environment
    // tg: code/modules/mob/living/carbon/human/_species.dm:1299
    public virtual async DmTask handle_environment_pressure(mob_living_carbon_human H = null, datum_gas_mixture environment = null, object seconds_per_tick = null)
    {
        float pressure = Dm.num(environment.return_pressure());
        float adjusted_pressure = Dm.num(H.calculate_affecting_pressure(pressure));

        // Set alerts and apply damage based on the amount of pressure
        // switch(adjusted_pressure): overlapping ranges, so an if/else chain where the first match wins (rules 6.7)
        // Very high pressure, show an alert and take damage
        if (adjusted_pressure >= HAZARD_HIGH_PRESSURE && adjusted_pressure <= INFINITY)
        {
            if (HAS_TRAIT(H, TRAIT_RESISTHIGHPRESSURE))
                H.clear_alert(ALERT_PRESSURE);
            else
            {
                float pressure_damage = min(((adjusted_pressure / HAZARD_HIGH_PRESSURE) - 1) * PRESSURE_DAMAGE_COEFFICIENT, MAX_HIGH_PRESSURE_DAMAGE) * Dm.num(seconds_per_tick);
                pressure_damage *= Dm.num(GET_PHYSIOLOGY(H, BRUTE)) * Dm.num(GET_PHYSIOLOGY(H, PHYS_COEFF_PRESSURE));
                await H.adjust_brute_loss(pressure_damage, required_bodytype: BODYTYPE_ORGANIC);
                H.throw_alert(ALERT_PRESSURE, typeof(atom_movable_screen_alert_highpressure), 2);
            }
        }
        // High pressure, show an alert
        else if (adjusted_pressure >= WARNING_HIGH_PRESSURE && adjusted_pressure <= HAZARD_HIGH_PRESSURE)
            H.throw_alert(ALERT_PRESSURE, typeof(atom_movable_screen_alert_highpressure), 1);
        // No pressure issues here clear pressure alerts
        else if (adjusted_pressure >= WARNING_LOW_PRESSURE && adjusted_pressure <= WARNING_HIGH_PRESSURE)
        {
            H.clear_alert(ALERT_PRESSURE);
            H.seconds_in_low_pressure = 0;
        }
        // Low pressure here, show an alert
        else if (adjusted_pressure >= HAZARD_LOW_PRESSURE && adjusted_pressure <= WARNING_LOW_PRESSURE)
        {
            // We have low pressure resit trait, clear alerts
            if (HAS_TRAIT(H, TRAIT_RESISTLOWPRESSURE))
                H.clear_alert(ALERT_PRESSURE);
            else
                H.throw_alert(ALERT_PRESSURE, typeof(atom_movable_screen_alert_lowpressure), 1);
            H.seconds_in_low_pressure = 0;
        }
        // Very low pressure, show an alert and take damage
        else
        {
            // We have low pressure resit trait, clear alerts
            if (HAS_TRAIT(H, TRAIT_RESISTLOWPRESSURE))
                H.clear_alert(ALERT_PRESSURE);
            else
            {
                float pressure_damage = min(round(1 + (H.seconds_in_low_pressure / (80 * SECONDS)), 0.05f) * BASE_LOW_PRESSURE_DAMAGE, MAX_LOW_PRESSURE_DAMAGE) * Dm.num(seconds_per_tick);
                pressure_damage *= Dm.num(GET_PHYSIOLOGY(H, BRUTE)) * Dm.num(GET_PHYSIOLOGY(H, PHYS_COEFF_PRESSURE));
                await H.adjust_brute_loss(pressure_damage, required_bodytype: BODYTYPE_ORGANIC);
                H.throw_alert(ALERT_PRESSURE, typeof(atom_movable_screen_alert_lowpressure), 2);
            }
            H.seconds_in_low_pressure += Dm.num(seconds_per_tick);
        }
    }

    /**
     *	Handles exposure to the skin of various gases.
     */
    // tg: code/modules/mob/living/carbon/human/_species.dm:1348
    public virtual async DmTask handle_gas_interaction(mob_living_carbon_human human = null, datum_gas_mixture environment = null, object seconds_per_tick = null)
    {
        /// Some non-clothing items may end up in these slots, e.g. flowers worn on the head, so we should consider clothing_flags as potentially nonexistant as a var.
        /// Otherwise we will get a very spammy runtime.
        float suit_flags = Dm.num((human?.wear_suit as obj_item_clothing)?.clothing_flags);
        float head_flags = Dm.num((human?.head as obj_item_clothing)?.clothing_flags);

        if (Dm.truthy(Dm.bitand(suit_flags, STOPSPRESSUREDAMAGE)) && Dm.truthy(Dm.bitand(head_flags, STOPSPRESSUREDAMAGE)))
            return;

        float antinoblium_moles = Dm.num(environment.moles[typeof(datum_gas_antinoblium)]);
        if (antinoblium_moles >= MOLES_GAS_VISIBLE && SPT_PROB(1, antinoblium_moles * Dm.num(seconds_per_tick)))
        {
            SSradiation?.irradiate(human); // rules 10.6: tg's Master always creates SSradiation; a harness world may lack it
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1365
    public virtual object negates_gravity(mob_living_carbon_human H = null)
    {
        if (Dm.truthy(Dm.bitand(H.movement_type, FLYING)))
            return TRUE;
        return FALSE;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1379
    public virtual void clear_tail_moodlets(mob_living_carbon_human former_tail_owner = null)
    {
        former_tail_owner.clear_mood_event("tail_lost");
        former_tail_owner.clear_mood_event("tail_balance_lost");
        former_tail_owner.clear_mood_event("tail_regained");
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1386
    public virtual object get_features()
    {
        object cached_features = GLOB.features_by_species[type];
        if (cached_features != null)
            return cached_features;

        DmList features = Dm.list();
        DmList mut_organs = get_organs() as DmList;

        foreach (object preference_type in Dm.each(GLOB.preference_entries))
        {
            datum_preference preference = GLOB.preference_entries[preference_type] as datum_preference;
            if ((preference.relevant_inherent_trait != null && Dm.@in(preference.relevant_inherent_trait, inherent_traits))
                || (preference.relevant_organ != null && Dm.@in(preference.relevant_organ, mut_organs))
                || (Dm.truthy(preference.relevant_head_flag) && Dm.truthy(check_head_flags(Dm.num(preference.relevant_head_flag))))
                || (preference.relevant_body_markings != null && Dm.@in(preference.relevant_body_markings, body_markings)))
                Dm.append(ref features, preference.savefile_key);
        }

        GLOB.features_by_species[type] = features;

        return features;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1410
    public virtual async DmTask prepare_human_for_preview(mob_living_carbon_human human = null)
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1414
    public virtual object get_organs(float? include_brain = null)
    {
        include_brain ??= TRUE;
        DmList mut_organs = Dm.list();
        Dm.append(ref mut_organs, mutant_organs);
        if (Dm.truthy(include_brain))
            Dm.append(ref mut_organs, mutantbrain);
        Dm.append(ref mut_organs, mutantheart);
        Dm.append(ref mut_organs, mutantlungs);
        Dm.append(ref mut_organs, mutanteyes);
        Dm.append(ref mut_organs, mutantears);
        Dm.append(ref mut_organs, mutanttongue);
        Dm.append(ref mut_organs, mutantliver);
        Dm.append(ref mut_organs, mutantstomach);
        Dm.append(ref mut_organs, mutantappendix);
        list_clear_nulls(mut_organs);
        return mut_organs;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1430
    public virtual object get_types_to_preload()
    {
        return get_organs(FALSE);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1444
    public virtual async DmTask on_owner_login(mob_living_carbon_human owner = null)
    {
        return;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1453
    public virtual object get_physical_attributes()
    {
        return I18n.T("An unremarkable species.");
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1462
    public virtual object get_species_description()
    {
        stack_trace(Dm.text("Species [] ([]) did not have a description set, and is a selectable roundstart race! Override get_species_description.", name, type), "code/modules/mob/living/carbon/human/_species.dm", 1465);
        return I18n.T("No species description set, file a bug report!");
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1475
    public virtual object get_species_lore()
    {
        stack_trace(Dm.text("Species [] ([]) did not have lore set, and is a selectable roundstart race! Override get_species_lore.", name, type), "code/modules/mob/living/carbon/human/_species.dm", 1479);
        return Dm.list(I18n.T("No species lore set, file a bug report!"));
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1488
    // M5 slice 8: the crafting window's diet (cooking mode).
    public virtual object get_species_diet()
    {
        if (Dm.@in(TRAIT_NOHUNGER, inherent_traits) || mutanttongue == null)
            return null;

        // var/static/list/food_flags = FOOD_FLAGS: a cache of the macro's constant list (read only), so the macro is used directly
        DmList food_flags = FOOD_FLAGS;
        Type fake_tongue = mutanttongue;

        return Dm.list(
            Dm.kv("liked_food", bitfield_to_list(Dm.num(Dm.initial(fake_tongue, (obj_item_organ_tongue t) => t.liked_foodtypes)), food_flags)),
            Dm.kv("disliked_food", bitfield_to_list(Dm.num(Dm.initial(fake_tongue, (obj_item_organ_tongue t) => t.disliked_foodtypes)), food_flags)),
            Dm.kv("toxic_food", bitfield_to_list(Dm.num(Dm.initial(fake_tongue, (obj_item_organ_tongue t) => t.toxic_foodtypes)), food_flags))
        );
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1519
    public virtual object get_species_perks()
    {
        DmList species_perks = Dm.list();

        // Let us get every perk we can conceive of in one big list.
        // The order these are called (kind of) matters.
        // Species unique perks first, as they're more important than genetic perks,
        // and language perk last, as it comes at the end of the perks list
        Dm.append(ref species_perks, create_pref_unique_perks());
        Dm.append(ref species_perks, create_pref_blood_perks());
        Dm.append(ref species_perks, create_pref_damage_perks());
        Dm.append(ref species_perks, create_pref_temperature_perks());
        Dm.append(ref species_perks, create_pref_traits_perks());
        Dm.append(ref species_perks, create_pref_biotypes_perks());
        Dm.append(ref species_perks, create_pref_organs_perks());
        Dm.append(ref species_perks, create_pref_language_perk());

        // Some overrides may return `null`, prevent those from jamming up the list.
        list_clear_nulls(species_perks);

        // Now let's sort them out for cleanliness and sanity
        DmList perks_to_return = Dm.list(
            Dm.kv(SPECIES_POSITIVE_PERK, Dm.list()),
            Dm.kv(SPECIES_NEUTRAL_PERK, Dm.list()),
            Dm.kv(SPECIES_NEGATIVE_PERK, Dm.list()));

        foreach (DmList perk in Dm.each_cast<DmList>(species_perks))
        {
            object perk_type = perk[SPECIES_PERK_TYPE];
            // If we find a perk that isn't postiive, negative, or neutral,
            // it's a bad entry - don't add it to our list. Throw a stack trace and skip it instead.
            if (perks_to_return[perk_type] == null)
            {
                stack_trace(Dm.text("Invalid species perk ([]) found for species []. The type should be positive, negative, or neutral. (Got: [])", perk[SPECIES_PERK_NAME], name, perk_type), "code/modules/mob/living/carbon/human/_species.dm", 1550);
                continue;
            }

            DmList sorted = perks_to_return[perk_type] as DmList;
            Dm.append(ref sorted, Dm.list(perk));
        }

        return perks_to_return;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1563
    public virtual object create_pref_unique_perks()
    {
        return null;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1572
    public virtual object create_pref_damage_perks()
    {
        // We use the chest to figure out brute and burn mod perks
        Type fake_chest = bodypart_overrides[BODY_ZONE_CHEST] as Type;

        DmList to_add = Dm.list();

        // Brute related
        if (Dm.initial(fake_chest, (obj_item_bodypart p) => p.brute_modifier) > 1)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "band-aid"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Brutal Weakness")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are weak to brute damage.", plural_form)))));

        if (Dm.initial(fake_chest, (obj_item_bodypart p) => p.brute_modifier) < 1)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "shield-alt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Brutal Resilience")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are resilient to brute damage.", plural_form)))));

        // Burn related
        if (Dm.initial(fake_chest, (obj_item_bodypart p) => p.burn_modifier) > 1)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "burn"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Burn Weakness")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are weak to burn damage.", plural_form)))));

        if (Dm.initial(fake_chest, (obj_item_bodypart p) => p.burn_modifier) < 1)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "shield-alt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Burn Resilience")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are resilient to burn damage.", plural_form)))));

        // Shock damage
        if (siemens_coeff > 1)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "bolt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Shock Vulnerability")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are vulnerable to being shocked.", plural_form)))));

        if (siemens_coeff < 1)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "shield-alt"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Shock Resilience")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are resilient to being shocked.", plural_form)))));

        if (Dm.truthy(Dm.bitand(inherent_biotypes, Dm.bitor(MOB_ROBOTIC, MOB_MINERAL))))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, FA_ICON_HAMMER),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Tough Frame")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are more resistant to slashing and stabbing, but more vulnerable to impacts.", plural_form)))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1644
    public virtual object create_pref_temperature_perks()
    {
        DmList to_add = Dm.list();

        // Hot temperature tolerance
        if (heatmod > 1 || bodytemp_heat_damage_limit < BODYTEMP_HEAT_DAMAGE_LIMIT)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "temperature-high"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Heat Vulnerability")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are vulnerable to high temperatures.", plural_form)))));

        if (heatmod < 1 || bodytemp_heat_damage_limit > BODYTEMP_HEAT_DAMAGE_LIMIT)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "thermometer-empty"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Heat Resilience")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are resilient to hotter environments.", plural_form)))));

        // Cold temperature tolerance
        if (coldmod > 1 || bodytemp_cold_damage_limit > BODYTEMP_COLD_DAMAGE_LIMIT)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "temperature-low"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Cold Vulnerability")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are vulnerable to cold temperatures.", plural_form)))));

        if (coldmod < 1 || bodytemp_cold_damage_limit < BODYTEMP_COLD_DAMAGE_LIMIT)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "thermometer-empty"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Cold Resilience")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are resilient to colder environments.", plural_form)))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1688
    public virtual object create_pref_blood_perks()
    {
        DmList to_add = Dm.list();
        datum_blood_type blood_type = exotic_bloodtype != null ? get_blood_type(exotic_bloodtype) as datum_blood_type : null;

        // TRAIT_NOBLOOD takes priority by default
        if (Dm.@in(TRAIT_NOBLOOD, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "tint-slash"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Bloodletted")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] do not have blood.", plural_form)))));

        // Otherwise, check if their exotic blood is a valid typepath
        else if (ispath(blood_type?.reagent_type))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "tint"),
                Dm.kv(SPECIES_PERK_NAME, Dm.initial(blood_type.reagent_type, (datum_reagent r) => r.name)),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] blood is [], which can make receiving medical treatment harder.", name, Dm.initial(blood_type.reagent_type, (datum_reagent r) => r.name))))));

        // Otherwise otherwise, see if they have an exotic bloodtype set
        else if (exotic_bloodtype != null)
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "tint"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Exotic Blood")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] have \"[]\" type blood, which can make receiving medical treatment harder.", plural_form, exotic_bloodtype)))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1726
    public virtual object create_pref_traits_perks()
    {
        DmList to_add = Dm.list();

        if (Dm.@in(TRAIT_LIMBATTACHMENT, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "user-plus"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Limbs Easily Reattached")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] limbs are easily readded, and as such do not require surgery to restore. Simply pick it up and pop it back in, champ!", plural_form)))));

        if (Dm.@in(TRAIT_EASYDISMEMBER, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "user-times"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Limbs Easily Dismembered")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] limbs are not secured well, and as such they are easily dismembered.", plural_form)))));

        if (Dm.@in(TRAIT_EASILY_WOUNDED, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "user-times"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Easily Wounded")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] skin is very weak and fragile. They are much easier to apply serious wounds to.", plural_form)))));

        if (Dm.@in(TRAIT_TOXINLOVER, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "syringe"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Toxins Lover")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Toxins damage dealt to [] are reversed - healing toxins will instead cause harm, and causing toxins will instead cause healing. Be careful around purging chemicals!", plural_form)))));

        if (Dm.@in(TRAIT_GENELESS, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "dna"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("No Genes")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] have no genes, making genetic scrambling a useless weapon, but also locking them out from getting genetic powers.", plural_form)))));

        if (Dm.@in(TRAIT_NOBREATH, inherent_traits))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "wind"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("No Respiration")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] have no need to breathe!", plural_form)))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1786
    public virtual object create_pref_biotypes_perks()
    {
        DmList to_add = Dm.list();

        if (Dm.truthy(Dm.bitand(inherent_biotypes, MOB_UNDEAD)))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "skull"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Undead")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] are of the undead! The undead do not have the need to eat or breathe, and most viruses will not be able to infect a walking corpse. Their worries mostly stop at remaining in one piece, really.", plural_form)))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1806
    public virtual object create_pref_organs_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, create_pref_liver_perks());
        Dm.append(ref to_add, create_pref_lung_perks());

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1816
    public virtual object create_pref_liver_perks()
    {
        if (mutantliver == null || Dm.@in(TRAIT_LIVERLESS_METABOLISM, inherent_traits))
            return null;

        DmList to_add = Dm.list();

        float alcohol_tolerance = Dm.initial(mutantliver, (obj_item_organ_liver p) => p.alcohol_tolerance);
        Type base_liver = typeof(obj_item_organ_liver);
        float tolerance_difference = alcohol_tolerance - Dm.initial(base_liver, (obj_item_organ_liver p) => p.alcohol_tolerance);

        if (tolerance_difference != 0)
        {
            bool difference_positive = tolerance_difference > 0;
            string more_or_less = difference_positive ? I18n.T("more") : I18n.T("less");
            string perk_type = difference_positive ? SPECIES_NEGATIVE_PERK : SPECIES_POSITIVE_PERK;
            string perk_name = difference_positive ? I18n.T("Alcohol Weakness") : I18n.T("Alcohol Tolerance"); // "Alcohol " + ...
            float percent_difference = (alcohol_tolerance / Dm.initial(base_liver, (obj_item_organ_liver p) => p.alcohol_tolerance)) * 100;

            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, perk_type),
                Dm.kv(SPECIES_PERK_ICON, "wine-glass"),
                Dm.kv(SPECIES_PERK_NAME, perk_name),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] livers are [] susceptable to alcohol than human livers, by about []%.", name, more_or_less, percent_difference)))));
        }

        float tox_shrugging = Dm.initial(mutantliver, (obj_item_organ_liver p) => p.toxTolerance);
        float shrugging_difference = tox_shrugging - Dm.initial(base_liver, (obj_item_organ_liver p) => p.toxTolerance);
        if (shrugging_difference != 0)
        {
            bool difference_positive = shrugging_difference > 0;
            string more_or_less = difference_positive ? I18n.T("more") : I18n.T("less");
            string perk_type = difference_positive ? SPECIES_POSITIVE_PERK : SPECIES_NEGATIVE_PERK;
            string perk_name = difference_positive ? I18n.T("Toxin Resistant Liver") : I18n.T("Toxin Vulnerable Liver"); // ("Toxin " + ...) + " Liver"

            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, perk_type),
                Dm.kv(SPECIES_PERK_ICON, "biohazard"),
                Dm.kv(SPECIES_PERK_NAME, perk_name),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] livers are capable of rapidly shrugging off []u of toxins, which is [] than humans.", name, tox_shrugging, more_or_less)))));
        }

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1859
    public virtual object create_pref_lung_perks()
    {
        if (mutantlungs == null || Dm.@in(TRAIT_NOBREATH, inherent_traits))
            return null;

        DmList to_add = Dm.list();

        object breath_id = get_breath_type();
        if (Dm.truthy(breath_id) && !Dm.eq(breath_id, GAS_O2))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "wind"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("[] Breathing", capitalize(breath_id))),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] must breathe [] to survive. You receive a tank when you arrive.", plural_form, breath_id)))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1884
    public virtual object create_pref_language_perk()
    {
        // Grab galactic common as a path, for comparisons
        Type common_language = typeof(datum_language_common);

        // Now let's find all the languages they can speak that aren't common
        DmList bonus_languages = Dm.list();
        datum_language_holder basic_holder = GLOB.prototype_language_holders[species_language_holder] as datum_language_holder;
        foreach (Type language_type in Dm.each_cast<Type>(basic_holder.spoken_languages))
        {
            if (ispath(language_type, common_language))
                continue;
            Dm.append(ref bonus_languages, Dm.initial(language_type, (datum_language p) => p.name));
        }

        if (!Dm.truthy(length(bonus_languages)))
            return null; // You're boring

        DmList to_add = Dm.list();
        if (Dm.@in(common_language, basic_holder.spoken_languages))
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "comment"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Native Speaker")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Alongside [], [] gain the ability to speak [].", Dm.initial(common_language, (datum_language p) => p.name), plural_form, english_list(bonus_languages))))));

        else
            Dm.append(ref to_add, Dm.list(Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "comment"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Foreign Speaker")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("[] may not speak [], but they can speak [].", plural_form, Dm.initial(common_language, (datum_language p) => p.name), english_list(bonus_languages))))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1920
    public virtual async DmTask replace_body(mob_living_carbon target = null, datum_species new_species = null, datum_species old_species = null)
    {
        new_species ??= target.dna.species as datum_species; //If no new species is provided, assume its src.
        //Note for future: Potentionally add a new C.dna.species() to build a template species for more accurate limb replacement

        DmList new_bodypart_overrides = new_species.bodypart_overrides.Copy();
        if ((new_species.digitigrade_customization == DIGITIGRADE_OPTIONAL && Dm.eq(target.dna.features[FEATURE_LEGS], DIGITIGRADE_LEGS)) || new_species.digitigrade_customization == DIGITIGRADE_FORCED)
        {
            new_bodypart_overrides[BODY_ZONE_R_LEG] = typeof(obj_item_bodypart_leg_right_digitigrade);
            new_bodypart_overrides[BODY_ZONE_L_LEG] = typeof(obj_item_bodypart_leg_left_digitigrade);
        }

        foreach (obj_item_bodypart old_part in Dm.each_cast<obj_item_bodypart>((target.get_bodyparts() as DmList)?.Copy()))
        {
            if (Dm.truthy(Dm.bitand(old_part.change_exempt_flags, BP_BLOCK_CHANGE_SPECIES)) || Dm.truthy(Dm.bitand(old_part.bodypart_flags, BODYPART_IMPLANTED)))
                continue;
            // update all our existing limbs
            object path = new_bodypart_overrides?[old_part.body_zone];
            if (Dm.truthy(path))
            {
                obj_item_bodypart new_part = await Dm.new_async(path as Type) as obj_item_bodypart;
                await new_part.replace_limb(target);
                await new_part.update_limb(is_creating: TRUE);
                new_part.set_initial_damage(old_part.brute_dam, old_part.burn_dam);
            }
            qdel(old_part);
        }

        DmList old_bodypart_overrides = old_species.bodypart_overrides.Copy();
        if (Dm.num(length(new_bodypart_overrides)) <= Dm.num(length(old_bodypart_overrides)))
            return; // we updated all the limbs the character deserves
        // but there are cases where the old species naturally lacked limbs
        foreach (object zone in Dm.each(new_bodypart_overrides))
        {
            // find a limb that the character doesn't have because of their old species
            if (Dm.truthy(new_bodypart_overrides.Find(zone)) && Dm.truthy(old_bodypart_overrides.Find(zone)))
                continue;
            object path = new_bodypart_overrides[zone];
            if (!Dm.truthy(path))
                return;

            obj_item_bodypart new_part = await Dm.new_async(path as Type) as obj_item_bodypart;
            await new_part.replace_limb(target);
            await new_part.update_limb(is_creating: TRUE);
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1959
    public virtual async DmTask create_fresh_body(mob_living_carbon target = null)
    {
        DmList override_limbs = Dm.list();
        foreach (object bodypart in Dm.each(bodypart_overrides))
            Dm.append(ref override_limbs, bodypart_overrides[bodypart]);
        await target.create_bodyparts(override_limbs);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1971
    // (the ghosts, 2026-10-16: a ghost takes the body's hair when the species' head has it, observer.dm:116)
    public virtual object check_head_flags(float? check_flags = null)
    {
        check_flags ??= NONE;
        Type fake_head = bodypart_overrides[BODY_ZONE_HEAD] as Type;
        return Dm.bitand(Dm.initial(fake_head, (obj_item_bodypart_head p) => p.head_flags), check_flags);
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:1989
    // Gaps P3b: the infused pod hair's colour (a mutant overlay's inherit_color)
    public virtual object get_fixed_hair_color(mob_living_carbon for_mob = null)
    {
        if (for_mob == null)
            throw Dm.CRASH("assertion failed: !isnull(for_mob)", "code/modules/mob/living/carbon/human/_species.dm", 1990);
        if (Dm.eq(hair_color_mode, USE_MUTANT_COLOR))
            return for_mob.dna.features[FEATURE_MUTANT_COLOR];
        if (Dm.eq(hair_color_mode, USE_FIXED_MUTANT_COLOR))
            return fixed_mut_color;

        return null;
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:2000
    public virtual async DmTask add_body_markings(mob_living_carbon_human hooman = null)
    {
        foreach (object markings_type in Dm.each(body_markings)) //loop through possible species markings
        {
            datum_bodypart_overlay_simple_body_marking markings = Dm.@new((Type)markings_type) as datum_bodypart_overlay_simple_body_marking; // made to die... mostly because we cant use initial on lists but its convenient and organized
            object accessory_name = hooman.dna.features[markings.dna_feature_key] is var or__1 && Dm.truthy(or__1) ? or__1 : body_markings[markings_type]; //get the accessory name from dna
            foreach (Type part in Dm.each_cast<Type>(markings.applies_to)) //check through our limbs
            {
                obj_item_bodypart people_part = hooman.get_bodypart(Dm.initial(part, (obj_item_bodypart p) => p.body_zone) as string) as obj_item_bodypart; // and see if we have a compatible marking for that limb
                if (people_part == null)
                    continue;

                datum_bodypart_overlay_simple_body_marking overlay = Dm.@new((Type)markings_type) as datum_bodypart_overlay_simple_body_marking;
                overlay.set_appearance(accessory_name, hooman.dna.features[FEATURE_MUTANT_COLOR]);
                await people_part.add_bodypart_overlay(overlay);
            }

            qdel(markings);
        }
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:2016
    public virtual async DmTask remove_body_markings(mob_living_carbon_human hooman = null)
    {
        bool needs_update = false;
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(hooman.get_bodyparts()))
        {
            foreach (datum_bodypart_overlay_simple_body_marking marking in Dm.each<datum_bodypart_overlay_simple_body_marking>(part.bodypart_overlays?.Copy()))
            {
                await part.remove_bodypart_overlay(marking, update: FALSE);
                needs_update = true;
            }
        }

        if (needs_update && !Dm.truthy(Dm.bitand(hooman.living_flags, STOP_OVERLAY_UPDATE_BODY_PARTS)))
            await hooman.update_body_parts();
    }

    // tg: code/modules/mob/living/carbon/human/_species.dm:2030
    public virtual object get_breath_type()
    {
        if (mutantlungs == null)
            return null;

        if (Dm.num(Dm.initial(mutantlungs, (obj_item_organ_lungs p) => p.safe_plasma_min)) > 0)
            return GAS_PLASMA;

        if (Dm.num(Dm.initial(mutantlungs, (obj_item_organ_lungs p) => p.safe_oxygen_min)) > 0)
            return GAS_O2;

        if (Dm.num(Dm.initial(mutantlungs, (obj_item_organ_lungs p) => p.safe_nitro_min)) > 0)
            return GAS_N2;

        stack_trace(Dm.text("Unsupported breath type for species with []", mutantlungs), "code/modules/mob/living/carbon/human/_species.dm", 2043);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/human/_species.dm:520  var/static/list/organs_to_randomize = list()
    internal DmList datum_species__randomize_features__organs_to_randomize = Dm.list();
}
