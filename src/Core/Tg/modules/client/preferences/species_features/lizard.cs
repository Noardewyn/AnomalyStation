// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/lizard.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: the lizard's Character Setup features. A /datum/universal_icon is the port's /icon (universal_icon.cs):
// the choices' pictures reach the client as /icon recipes (DV44), the side shots with the recipe's direction pick.

public static partial class GlobalProcs
{
    // tg: code/modules/client/preferences/species_features/lizard.dm:1
    public static object generate_lizard_side_shot(datum_sprite_accessory sprite_accessory = null, object key = null, float? include_snout = null)
    {
        include_snout ??= TRUE;
        // var/static/datum/universal_icon/lizard and lizard_with_snout: made each call (an /icon is a short list of operations)
        icon lizard = Dm.icon(Dm.resource("icons/mob/human/species/lizard/bodyparts.dmi"), "lizard_head", EAST);
        icon eyes = Dm.icon(Dm.initial(typeof(obj_item_organ_eyes_lizard), (obj_item_organ_eyes p) => p.eye_icon), Dm.text("[]_l", Dm.initial(typeof(obj_item_organ_eyes_lizard), (obj_item_organ_eyes p) => p.eye_icon_state)), EAST);
        eyes.Blend(COLOR_GRAY, ICON_MULTIPLY);
        lizard.Blend(eyes, ICON_OVERLAY);

        icon lizard_with_snout = Dm.icon(lizard);
        lizard_with_snout.Blend(Dm.icon(Dm.resource("icons/mob/human/species/lizard/lizard_misc.dmi"), "m_snout_round_ADJ", EAST), ICON_OVERLAY);

        icon final_icon = Dm.truthy(include_snout) ? Dm.icon(lizard_with_snout) : Dm.icon(lizard);

        if (sprite_accessory != null && !Dm.eq(sprite_accessory.icon_state, SPRITE_ACCESSORY_NONE))
        {
            icon accessory_icon_base = Dm.icon(sprite_accessory.icon, Dm.text("m_[]_[]_ADJ", key, sprite_accessory.icon_state), EAST);
            if (Dm.truthy(icon_exists(sprite_accessory.icon, Dm.text("m_[]_[]_FRONT", key, sprite_accessory.icon_state))))
            {
                icon accessory_icon_front = Dm.icon(sprite_accessory.icon, Dm.text("m_[]_[]_FRONT", key, sprite_accessory.icon_state), EAST);
                accessory_icon_base.Blend(accessory_icon_front, ICON_OVERLAY);
            }
            final_icon.Blend(accessory_icon_base, ICON_OVERLAY);
        }

        final_icon.Crop(11, 20, 23, 32);
        final_icon.Scale(32, 32);
        final_icon.Blend(COLOR_VIBRANT_LIME, ICON_MULTIPLY);

        return final_icon;
    }
}

// tg: code/modules/client/preferences/species_features/lizard.dm:30
public partial class datum_preference_choiced_species_feature_lizard_body_markings : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/lizard.dm:37
    public override object icon_for(object value = null)
    {
        datum_sprite_accessory sprite_accessory = get_accessory_for_value(value) as datum_sprite_accessory;

        icon final_icon = Dm.icon(Dm.resource("icons/mob/human/species/lizard/bodyparts.dmi"), "lizard_chest_m");

        if (!Dm.eq(sprite_accessory.icon_state, SPRITE_ACCESSORY_NONE))
        {
            icon body_markings_icon = Dm.icon(
                sprite_accessory.icon,
                Dm.text("male_[]_chest", sprite_accessory.icon_state));

            final_icon.Blend(body_markings_icon, ICON_OVERLAY);
        }

        final_icon.Blend(COLOR_VIBRANT_LIME, ICON_MULTIPLY);
        final_icon.Crop(10, 8, 22, 23);
        final_icon.Scale(26, 32);
        final_icon.Crop(-2, 1, 29, 32);

        return final_icon;
    }
}

// tg: code/modules/client/preferences/species_features/lizard.dm:58
public partial class datum_preference_choiced_species_feature_lizard_frills : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/lizard.dm:65
    public override object icon_for(object value = null)
    {
        return generate_lizard_side_shot(get_accessory_for_value(value) as datum_sprite_accessory, "frills");
    }
}

// tg: code/modules/client/preferences/species_features/lizard.dm:69
public partial class datum_preference_choiced_species_feature_lizard_horns : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/lizard.dm:76
    public override object icon_for(object value = null)
    {
        return generate_lizard_side_shot(get_accessory_for_value(value) as datum_sprite_accessory, "horns");
    }
}

// tg: code/modules/client/preferences/species_features/lizard.dm:80
public partial class datum_preference_choiced_lizard_legs : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/lizard.dm:84
    public override DmList init_possible_values()
    {
        return Dm.list(NORMAL_LEGS, DIGITIGRADE_LEGS);
    }

    // tg: code/modules/client/preferences/species_features/lizard.dm:87
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.dna.features[FEATURE_LEGS] = value;
        // Hack to update the dummy in the preference menu
        // (Because digi legs are ONLY handled on species change)
        if (!isdummy(target) || ((datum_species)target.dna.species).digitigrade_customization == DIGITIGRADE_NEVER)
            return;

        DmList correct_legs = Dm.bitand(((datum_species)target.dna.species).bodypart_overrides.Copy(), Dm.list(BODY_ZONE_R_LEG, BODY_ZONE_L_LEG)) as DmList;

        if (Dm.eq(value, DIGITIGRADE_LEGS))
        {
            correct_legs[BODY_ZONE_R_LEG] = typeof(obj_item_bodypart_leg_right_digitigrade);
            correct_legs[BODY_ZONE_L_LEG] = typeof(obj_item_bodypart_leg_left_digitigrade);
        }

        foreach (obj_item_bodypart old_part in Dm.each_cast<obj_item_bodypart>(target.get_bodyparts()))
        {
            if (Dm.truthy(Dm.bitand(old_part.change_exempt_flags, BP_BLOCK_CHANGE_SPECIES)))
                continue;

            Type path = correct_legs[old_part.body_zone] as Type;
            if (path == null)
                continue;
            obj_item_bodypart new_part = Dm.@new(path) as obj_item_bodypart;
            Waiting.NoSleep(new_part.replace_limb(target));
            Waiting.NoSleep(new_part.update_limb(is_creating: TRUE));
            qdel(old_part);
        }
    }

    // tg: code/modules/client/preferences/species_features/lizard.dm:112
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;
        Type species_type = preferences.read_preference(typeof(datum_preference_choiced_species)) as Type;
        return Dm.initial(species_type, (datum_species p) => p.digitigrade_customization) == DIGITIGRADE_OPTIONAL ? TRUE : FALSE;
    }
}

// tg: code/modules/client/preferences/species_features/lizard.dm:119
public partial class datum_preference_choiced_species_feature_lizard_snout : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/lizard.dm:126
    public override object icon_for(object value = null)
    {
        return generate_lizard_side_shot(get_accessory_for_value(value) as datum_sprite_accessory, "snout", include_snout: FALSE);
    }
}

// tg: code/modules/client/preferences/species_features/lizard.dm:130 and :136, var-only: declared here so that they are written types
// (DV180: GLOB.preference_entries holds the translated preferences)
public partial class datum_preference_choiced_species_feature_lizard_spines : datum_preference_choiced_species_feature { }

public partial class datum_preference_choiced_species_feature_lizard_tail : datum_preference_choiced_species_feature { }
