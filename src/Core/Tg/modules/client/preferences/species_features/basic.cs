// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/basic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slices D3 and D4 (lobby.md 8): the whole file. A /datum/universal_icon is the port's /icon (universal_icon.cs).

public static partial class GlobalProcs
{
    // tg: code/modules/client/preferences/species_features/basic.dm:1
    public static icon generate_icon_with_head_accessory(datum_sprite_accessory sprite_accessory = null, object y_offset = null)
    {
        // var/static/datum/universal_icon/head_icon: made each call (an /icon is a short list of operations)
        icon head_icon = Dm.icon(Dm.resource("icons/mob/human/bodyparts_greyscale.dmi"), "human_head_m");
        head_icon.Blend(skintone2hex("caucasian1"), ICON_MULTIPLY);

        icon final_icon = Dm.icon(head_icon);
        if (sprite_accessory != null && !Dm.eq(sprite_accessory.icon_state, SPRITE_ACCESSORY_NONE))
        {
            icon head_accessory_icon = Dm.icon(sprite_accessory.icon, sprite_accessory.icon_state);
            head_accessory_icon.Blend(COLOR_DARK_BROWN, ICON_MULTIPLY);
            // head_accessory_icon.shift(NORTH, y_offset) then blend_icon(ICON_OVERLAY): the same pixels as the overlay at the offset
            final_icon.Blend(head_accessory_icon, ICON_OVERLAY, 1, 1 + (Dm.truthy(y_offset) ? Dm.num(y_offset) : 0));
        }

        final_icon.Crop(10, 19, 22, 31);
        final_icon.Scale(32, 32);

        return final_icon;
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:23
[DmPath("/datum/preference/color/eye_color")]
public partial class datum_preference_color_eye_color : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/basic.dm:29
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        float hetero = target.eye_color_heterochromatic;
        target.eye_color_left = Dm.str(value);
        if (!Dm.truthy(hetero))
            target.eye_color_right = Dm.str(value);

        if (target.get_organ_by_type(typeof(obj_item_organ_eyes)) is not obj_item_organ_eyes eyes_organ)
            return;

        if (!Dm.truthy(Dm.initial(eyes_organ, "eye_color_left")))
            eyes_organ.eye_color_left = value;

        if (Dm.truthy(hetero)) // Don't override the snowflakes please
            return;

        if (!Dm.truthy(Dm.initial(eyes_organ, "eye_color_right")))
            eyes_organ.eye_color_right = value;
        Waiting.NoSleep(eyes_organ.refresh());
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:49
    public override object create_default_value()
    {
        return random_eye_color();
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:53
[DmPath("/datum/preference/choiced/facial_hairstyle")]
public partial class datum_preference_choiced_facial_hairstyle : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/basic.dm:61
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.facial_hairstyles_list);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:64
    public override object icon_for(object value = null)
    {
        return generate_icon_with_head_accessory(SSaccessories.facial_hairstyles_list[value] as datum_sprite_accessory);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:67
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_facial_hairstyle(value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:70
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_facial_hair_shaved), (datum_sprite_accessory a) => a.name);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:73
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        object gender = preferences.read_preference(typeof(datum_preference_choiced_gender));
        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species_real = GLOB.species_prototypes[species_type] as datum_species;
        if (!Dm.truthy(gender) || species_real == null || !Dm.truthy(species_real.sexes))
            return base.create_informed_default_value(preferences);

        object picked_beard = random_facial_hairstyle(gender);
        datum_sprite_accessory beard_style = SSaccessories.facial_hairstyles_list[picked_beard] as datum_sprite_accessory;
        if (beard_style == null || !Dm.truthy(beard_style.natural_spawn) || Dm.truthy(beard_style.locked)) // Invalid, go with god(bald)
            return base.create_informed_default_value(preferences);

        return picked_beard;
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:87
    public override object compile_constant_data()
    {
        DmList data = (DmList)base.compile_constant_data();

        data[SUPPLEMENTAL_FEATURE_KEY] = Dm.initial(typeof(datum_preference_color_facial_hair_color), (datum_preference p) => p.savefile_key);

        return data;
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:95
[DmPath("/datum/preference/color/facial_hair_color")]
public partial class datum_preference_color_facial_hair_color : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/basic.dm:101
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_facial_haircolor(value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:104
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        object hair_color = preferences.read_preference(typeof(datum_preference_color_hair_color));
        return Dm.truthy(hair_color) ? hair_color : random_hair_color();
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:108
[DmPath("/datum/preference/choiced/facial_hair_gradient")]
public partial class datum_preference_choiced_facial_hair_gradient : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/basic.dm:115
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.facial_hair_gradients_list);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:118
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_facial_hair_gradient_style(new_style: value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:121
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_gradient_none), (datum_sprite_accessory a) => a.name);
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:125
[DmPath("/datum/preference/color/facial_hair_gradient")]
public partial class datum_preference_color_facial_hair_gradient : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/basic.dm:131
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_facial_hair_gradient_color(new_color: value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:134
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;
        return !Dm.eq(preferences.read_preference(typeof(datum_preference_choiced_facial_hair_gradient)), Dm.initial(typeof(datum_sprite_accessory_gradient_none), (datum_sprite_accessory a) => a.name)) ? TRUE : FALSE;
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:140
[DmPath("/datum/preference/color/hair_color")]
public partial class datum_preference_color_hair_color : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/basic.dm:146
    public override object has_relevant_feature(datum_preferences preferences = null)
    {
        return Dm.truthy(base.has_relevant_feature(preferences)) || Dm.@in(Dm.initial(typeof(datum_quirk_item_quirk_bald), (datum_quirk q) => q.name), preferences.all_quirks) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:149
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_haircolor(value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:152
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        return random_hair_color();
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:156
[DmPath("/datum/preference/choiced/hairstyle")]
public partial class datum_preference_choiced_hairstyle : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/basic.dm:164
    public override object has_relevant_feature(datum_preferences preferences = null)
    {
        return Dm.truthy(base.has_relevant_feature(preferences)) || Dm.@in(Dm.initial(typeof(datum_quirk_item_quirk_bald), (datum_quirk q) => q.name), preferences.all_quirks) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:167
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.hairstyles_list);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:170
    public override object icon_for(object value = null)
    {
        datum_sprite_accessory_hair hairstyle = SSaccessories.hairstyles_list[value] as datum_sprite_accessory_hair;
        return generate_icon_with_head_accessory(hairstyle, hairstyle?.y_offset);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:174
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_hairstyle(value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:177
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_hair_bald), (datum_sprite_accessory a) => a.name);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:180
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        object gender = preferences.read_preference(typeof(datum_preference_choiced_gender));
        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species_real = GLOB.species_prototypes[species_type] as datum_species;
        if (!Dm.truthy(gender) || species_real == null || !Dm.truthy(species_real.sexes))
            return base.create_informed_default_value(preferences);

        object picked_hair = random_hairstyle(gender);
        datum_sprite_accessory hair_style = SSaccessories.hairstyles_list[picked_hair] as datum_sprite_accessory;
        if (hair_style == null || !Dm.truthy(hair_style.natural_spawn) || Dm.truthy(hair_style.locked)) // Invalid, go with god(bald)
            return base.create_informed_default_value(preferences);

        return picked_hair;
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:194
    public override object compile_constant_data()
    {
        DmList data = (DmList)base.compile_constant_data();

        data[SUPPLEMENTAL_FEATURE_KEY] = Dm.initial(typeof(datum_preference_color_hair_color), (datum_preference p) => p.savefile_key);

        return data;
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:202
[DmPath("/datum/preference/choiced/hair_gradient")]
public partial class datum_preference_choiced_hair_gradient : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species_features/basic.dm:209
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.hair_gradients_list);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:212
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_hair_gradient_style(new_style: value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:215
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_gradient_none), (datum_sprite_accessory a) => a.name);
    }
}

// tg: code/modules/client/preferences/species_features/basic.dm:219
[DmPath("/datum/preference/color/hair_gradient")]
public partial class datum_preference_color_hair_gradient : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/basic.dm:225
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_hair_gradient_color(new_color: value, update: FALSE);
    }

    // tg: code/modules/client/preferences/species_features/basic.dm:228
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;
        return !Dm.eq(preferences.read_preference(typeof(datum_preference_choiced_hair_gradient)), Dm.initial(typeof(datum_sprite_accessory_gradient_none), (datum_sprite_accessory a) => a.name)) ? TRUE : FALSE;
    }
}
