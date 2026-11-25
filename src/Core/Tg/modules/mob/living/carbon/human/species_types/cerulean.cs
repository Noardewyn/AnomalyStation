// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/species_types/cerulean.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2c (D349): the whole file: the cerulean species, its oversized fish tail (the three variants), the tail's bodypart overlays and the
// abyss texture. The cerulean's wheelchair (the job's spawn) waits on the rideable vehicles (docs/ai/specs/live-partial.txt). The var blocks
// are generated (D111).

// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:1
public partial class datum_species_human_cerulean : datum_species_human
{
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:30
    public override object get_physical_attributes()
    {
        return I18n.T("Ceruleans struggle with the pull of gravity yet are otherwise similar to humans. They are slightly more durable, and adore Martian culinary culture, though not much else...");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:34
    public override object get_species_description()
    {
        return I18n.T("Ceruleans are descendants of humans and vastly altered \"Animalids\" who originated from a populace of fanatic body modders devoted to perfecting biomechanics. Not much of these origins remain, par for their insatiable drive to endlessly chase scientific advancement.");
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:39
    public override object get_species_lore()
    {
        return Dm.list(
            I18n.T("Ceruleans are a scarcely common \"Animalid\" variant, sought mostly for their labor in vacuum environments. They are descendant of radically spliced humans, of which a society inhabits two tidal locked hycean worlds: planets Marina and Moryana. Largely, the Cerulean society still reside there today."),

            I18n.T("Most of the scientific development of early Cerulean kind, directly post migration from humans, are left in obscurity or destroyed. In part due to the self-centered nature of the problems they were designed to solve, like the \"hydro-vaporizer\", a device which respirates gills in dry atmosphere. Or, their arrogant and inexorable attitude in favor for their inventions and the pursuit for improvement."),

            I18n.T("There is one category of technology the Ceruleans have managed to stand out with: they are pioneers of gravity manipulation technology. Not many decades ago, Ceruleans of planet Marina, known as the Marinians, built grav-gen megastructures onto their sea beds to cast its planet's ocean currents upward past atmosphere,\tbuilding an aquatic bridge flowing towards Moryana into a connection between the two worlds."),

            I18n.T("Outwardly, this was done to show-boat their planetary technology as well as their unity portraying to be two kindred planets, but to the Moryan it was an obvious attempt of the mightier celestial body to seize control over a new, peculiar society. Taking shape deep in the perpetually lightless region behind Moryana, known around space as Moryana's \"Abyss\" region."));
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:58
    public override object create_pref_unique_perks()
    {
        DmList to_add = Dm.list();

        Dm.append(ref to_add, Dm.list(
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "fish-fins"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Flip-Flap")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ceruleans are hard to keep ahold of, becoming slippery and tough to grab if soaked in water."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_POSITIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "shield"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Scaled Up")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ceruleans have slightly higher damage and pressure resistance."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEUTRAL_PERK),
                Dm.kv(SPECIES_PERK_ICON, "dragon"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Little Space Dragon")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ceruleans and their dexterity are tied to gravity, turning lethally agile if its taken out the question."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "wheelchair-move"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("No Leg to Stand On")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ceruleans do not have legs, requiring a brutal surgery to opt for a pair."))),
            Dm.list(
                Dm.kv(SPECIES_PERK_TYPE, SPECIES_NEGATIVE_PERK),
                Dm.kv(SPECIES_PERK_ICON, "shrimp"),
                Dm.kv(SPECIES_PERK_NAME, I18n.T("Fried Shrimp")),
                Dm.kv(SPECIES_PERK_DESC, I18n.T("Ceruleans gain more damage from burn damage and heat sources.")))));

        return to_add;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:96
    public override async DmTask prepare_human_for_preview(mob_living_carbon_human preview_human = null)
    {
        preview_human.set_haircolor("#C2DFED", update: FALSE);
        preview_human.set_hairstyle(Dm.initial(typeof(datum_sprite_accessory_hair_highponytail), (datum_sprite_accessory p) => p.name), update: TRUE);
        string frills_name = Dm.initial(typeof(datum_sprite_accessory_frills_aquatic), (datum_sprite_accessory p) => p.name) as string;
        preview_human.dna.features[FEATURE_FRILLS] = frills_name;
        (preview_human.dna.species as datum_species).mutant_organs[typeof(obj_item_organ_frills)] = frills_name;
        await regenerate_organs(preview_human, excluded_zones: GLOB.leg_zones);
        await preview_human.update_body(TRUE); // update_body(is_creating = TRUE)
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:104
    public override object get_features()
    {
        DmList features = base.get_features() as DmList;
        LAZYOR(ref features, Dm.initial(typeof(datum_preference_toggle_cerulean_frills), (datum_preference p) => p.savefile_key));
        LAZYOR(ref features, Dm.initial(typeof(datum_preference_color_fish_tail_color), (datum_preference p) => p.savefile_key));
        return features;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:110
    public override object randomize_features()
    {
        DmList features = base.randomize_features() as DmList;
        LAZYSET(ref features, FEATURE_TAIL_FISH_COLOR, pick(Dm.sub(GLOB.carp_colors, COLOR_CARP_SILVER) as DmList));
        return features;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:115
    public override async DmTask on_species_gain(mob_living_carbon_human cerulean = null, datum_species old_species = null, object pref_load = null, float? regenerate_icons = null, float? replace_missing = null)
    {
        await base.on_species_gain(cerulean, old_species, pref_load, regenerate_icons, replace_missing);
        obj_item_bodypart_chest cerulean_chest = cerulean.get_bodypart() as obj_item_bodypart_chest;
        if (cerulean_chest != null)
            cerulean_chest.wing_types = Dm.list(typeof(obj_item_organ_wings_dragon));
        if (Dm.truthy(cerulean.has_gravity()))
            await cerulean.set_resting(TRUE, silent: TRUE, instant: TRUE);
        //apply a free wet stack to prevent the choking screen alert to appear for a second on mob creation
        await cerulean.apply_status_effect(typeof(datum_status_effect_fire_handler_wet_stacks), 1, FALSE);
    }

    /// good guy nanotrasen provides a wheelchair to their employees
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:126
    public override async DmTask pre_equip_species_outfit(datum_job job = null, mob_living_carbon_human cerulean = null, float? visuals_only = null)
    {
        if (Dm.truthy(visuals_only))
            return;
        if (!istype(job, typeof(datum_job)))
            return;
        // cerulean.put_in_wheelchair(): the wheelchair waits on the rideable vehicles (live-partial.txt, D349)
        Dm.Deferred("code/modules/mob/living/carbon/human/species_types/cerulean.dm:131", "pre_equip_species_outfit: put_in_wheelchair, the wheelchair is not translated");
    }

    /// gives a 'necessary for life' device to Ceruleans with gills
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:134
    public override async DmTask post_equip_species_outfit(mob_living_carbon_human cerulean = null, float? visuals_only = null)
    {
        if (Dm.truthy(visuals_only))
            return;
        obj_item_organ_lungs cerulean_lungs = cerulean.get_organ_slot(ORGAN_SLOT_LUNGS) as obj_item_organ_lungs;
        if (Dm.truthy(cerulean_lungs.breathe_always[typeof(datum_gas_oxygen)]))//early return for ie amphibious lungs. siren shimai have this
            return;
        await cerulean.equip_in_one_of_slots(
            await SSwardrobe.provide_type(typeof(obj_item_vaporizer_with_cell), cerulean) as obj_item,
            Dm.list(LOCATION_RPOCKET, LOCATION_LPOCKET, LOCATION_HANDS, LOCATION_BACKPACK),
            qdel_on_fail: FALSE,
            indirect_action: TRUE);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:147
    public override object get_breath_type()
    {
        return GAS_WATER_VAPOR; //overwrite this proc because safe_water_min is on a lungs subtype. remind me to revive #91453
    }
}

/// The inverse multiplyer indicating how much blood compared to default_blood_volume() needs to exist for a clean detachment surgery
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:151
public partial class obj_item_organ_tail_fish_cerulean
{
    private const float CLEAN_CUT_MULT = 0.5f;
}

/*
 * the main driver of the species and the source of the strongest species perks
 * allowing free movement in any atmosphere if zero g
 */
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:157
public partial class obj_item_organ_tail_fish_cerulean : obj_item_organ_tail_fish
{
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:172
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        await get_your_sealegs(organ_owner, special);
        return dot;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:176
    public override async DmTask on_mob_remove(object organ_owner__arg = null, float? special = null, object movement_flags = null)
    {
        mob_living_carbon organ_owner = organ_owner__arg as mob_living_carbon;
        await base.on_mob_remove(organ_owner__arg, special, movement_flags);
        UnregisterSignal(organ_owner, Dm.list(COMSIG_CARBON_GAIN_ORGAN, COMSIG_CARBON_LOSE_ORGAN));
        if (Dm.truthy(special))
            return;
        string limb_name = Dm.text("\\improper []", bodypart_owner.name);
        string tail_name = Dm.text("\\improper []", name);
        await organ_owner.apply_damage(rand(35, 45), def_zone: BODY_ZONE_CHEST, wound_bonus: CANT_WOUND);
        if (!HAS_TRAIT(organ_owner, TRAIT_ANALGESIA))
        {
            await organ_owner.emote("scream"); //owowowowowow
            await organ_owner.set_jitter_if_lower(1 * SECONDS);
            shake_camera(organ_owner, 1 * SECONDS, 2);
            to_chat(organ_owner, span_userdanger(I18n.T("You wince and scream as your [] is grotesquely torn from your []!", tail_name, limb_name)));
        }
        if (!Dm.truthy(splatter_check(organ_owner)))
            // if you have no blood or are missing half of it, its a clean cut
            return;
        organ_owner.blood_volume -= organ_owner.blood_volume / 3;
        await organ_owner.add_splatter_floor(get_turf(this));
        await organ_owner.spray_blood(REVERSE_DIR(organ_owner.dir), 2);
        organ_owner.visible_message(span_danger(I18n.T("[] detaches from []'s [], spilling out liters of []!", this, organ_owner, limb_name, LOWER_TEXT((organ_owner.get_bloodtype() as datum_blood_type)?.get_blood_name()))));
        REMOVE_TRAIT(this, TRAIT_NODROP, ORGAN_INSIDE_BODY_TRAIT); //do this early so we can fling it
        if (QDELETED(this))
            return; // DEVIATION(DV593): a tail deleted off a living cerulean is not flung (tg's throw_at CRASHes on a qdeleted thing)
        await throw_at(get_edge_target_turf(organ_owner, REVERSE_DIR(organ_owner.dir)) as atom); //ok so it doesnt actually fling it it just spins but idk hwo to fling
        playsound(this, Dm.resource("sound/effects/cartoon_sfx/cartoon_splat.ogg"), rand(50, 75), TRUE);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:200
    public override async DmTask on_surgical_removal(mob_living user = null, obj_item_bodypart limb = null, obj_item tool = null)
    {
        await base.on_surgical_removal(user, limb, tool);
        if (!Dm.truthy(splatter_check(limb.owner)))
            return;
        shake_camera(user, 1 * SECONDS, 2); //you shake too hehe
        await user.set_jitter_if_lower(1 * SECONDS);
    }

    // limit restyle to our tail's unique pool
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:208
    public override object get_valid_restyles()
    {
        return (bodypart_overlay as datum_bodypart_overlay_mutant).get_global_feature_list();
    }

    /// check if we have (enough) blood for an (un)clean cut
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:212
    public virtual object splatter_check(mob_living_carbon organ_owner = null)
    {
        if (organ_owner == null)
            return FALSE;
        return organ_owner.blood_volume != 0 && !HAS_TRAIT(organ_owner, TRAIT_NOBLOOD) && organ_owner.blood_volume >= organ_owner.default_blood_volume * CLEAN_CUT_MULT ? TRUE : FALSE;
    }

    /// if legs are present remove them silently if special = true, not so silently else
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:218
    public virtual async DmTask get_your_sealegs(mob_living_carbon organ_owner = null, float? special = null)
    {
        DmList legs = Dm.list(organ_owner.get_bodypart(BODY_ZONE_R_LEG), organ_owner.get_bodypart(BODY_ZONE_L_LEG));
        foreach (obj_item_bodypart_leg leg in Dm.each_cast<obj_item_bodypart_leg>(legs))
        {
            if (Dm.truthy(special) || QDELING(organ_owner))
            {
                if (leg != null)
                    await leg.drop_limb(TRUE, FALSE, FALSE);
            }
            else if (leg != null)
                await leg.dismember();
        }
    }
}

/// the bodypart overlay for cerulean fish tails!
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:226
public partial class datum_bodypart_overlay_mutant_tail_fish_cerulean : datum_bodypart_overlay_mutant_tail_fish
{
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:236
    public override object can_draw_on_bodypart(obj_item_bodypart bodypart_owner = null, mob_living_carbon organ_owner = null)
    {
        return bodypart_owner != null && organ_owner != null ? TRUE : base.can_draw_on_bodypart(bodypart_owner, organ_owner);
    }

    // simpler than parent. we don't care about locked/natural_spawn. all the accessories in our pool are locked
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:240
    public override object get_random_appearance()
    {
        return fetch_sprite_datum_from_name(pick(get_global_feature_list() as DmList));
    }

    // make our own little feature list by copying the global and removing the normal fish tails
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:244
    public override object get_global_feature_list()
    {
        // tg keeps the global list in a `var/static`; the port copies it each time (a static would outlive its world)
        DmList feature_list = (SSaccessories.feature_list[feature_key] as DmList).Copy();
        foreach (object accessory in Dm.each(feature_list.Copy()))
        {
            datum_sprite_accessory accessory_datum = feature_list[accessory] as datum_sprite_accessory;
            if (!istype(accessory_datum, typeof(datum_sprite_accessory_tails_fish_cerulean)))
                Dm.remove(ref feature_list, accessory);
            if (Dm.@in(accessory_datum.type, locked_sprite_datums))
                Dm.remove(ref feature_list, accessory);
        }
        return feature_list;
    }
}

/*
 * same as parent, but with a pretty skeleton texture
 */
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:260
public partial class obj_item_organ_tail_fish_cerulean_abyss : obj_item_organ_tail_fish_cerulean
{
}

/// apply an emissive for the skeleton
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:266
public partial class datum_bodypart_overlay_mutant_tail_fish_cerulean_abyss : datum_bodypart_overlay_mutant_tail_fish_cerulean
{
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:266
    public override async DmTask<object> get_overlay(obj_item_bodypart limb = null, object layer_index = null, object layer_real = null)
    {
        DmList created_overlays = await base.get_overlay(limb, layer_index, layer_real) as DmList;
        Dm.append(ref created_overlays, mutable_appearance(sprite_datum.icon, "abyss_skeleton", offset_spokesman: limb, alpha: 105, layer: Dm.num(layer_real)));
        // tg: :269  the skeleton's emissive_appearance: DEVIATION(DV12), emissives are not ported
        return created_overlays;
    }

    /// now the texture
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:273
    public override async DmTask added_to_limb(obj_item_bodypart limb = null)
    {
        await limb.add_bodypart_texture(typeof(datum_bodypart_texture_cerulean_abyss), FALSE);
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:276
    public override async DmTask removed_from_limb(obj_item_bodypart limb = null)
    {
        await limb.remove_bodypart_texture(typeof(datum_bodypart_texture_cerulean_abyss), FALSE);
    }
}

/// apply the bodypart texture (mask) to the limb and its visual organ aka fish tail
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:279
public partial class datum_bodypart_texture_cerulean_abyss : datum_bodypart_texture
{
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:280
    public override void modify_bodypart_appearance(image appearance = null, object overlay_flags = null)
    {
        icon new_appearance = Dm.icon(appearance.icon);
        new_appearance.Blend(Dm.icon(Dm.initial(typeof(datum_sprite_accessory_tails_fish_cerulean), (datum_sprite_accessory p) => p.icon), "abyss_mask"), ICON_SUBTRACT);
        appearance.icon = new_appearance;
    }

    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:285
    public override object can_texture_bodypart(obj_item_bodypart bodypart_owner = null)
    {
        return TRUE;
    }
}

/*
 * same as parent, but for skeletons
 */
// tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:291
public partial class obj_item_organ_tail_fish_cerulean_skeletal : obj_item_organ_tail_fish_cerulean
{
    // tg: code/modules/mob/living/carbon/human/species_types/cerulean.dm:307
    public override object splatter_check(mob_living_carbon organ_owner = null)
    {
        return FALSE; //no blood in this one
    }
}
