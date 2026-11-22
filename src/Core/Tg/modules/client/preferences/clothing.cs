// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slices D3 and D4 (lobby.md 8): the whole file. A /datum/universal_icon is the port's /icon (universal_icon.cs); the static
// base pictures are made each call (an /icon is a short list of operations).

public static partial class GlobalProcs
{
    // tg: code/modules/client/preferences/clothing.dm:1
    public static icon generate_underwear_icon(datum_sprite_accessory accessory = null, icon base_icon = null, object color = null)
    {
        icon final_icon = Dm.icon(base_icon);

        if (accessory != null)
        {
            icon accessory_icon = Dm.icon(Dm.resource("icons/mob/clothing/underwear.dmi"), accessory.icon_state);
            if (Dm.truthy(color) && !Dm.truthy(accessory.use_static))
                accessory_icon.Blend(color, ICON_MULTIPLY);
            final_icon.Blend(accessory_icon, ICON_OVERLAY);
        }

        final_icon.Crop(10, 1, 22, 13);
        final_icon.Scale(32, 32);

        return final_icon;
    }

    // The bodyparts_greyscale.dmi states overlaid in order on icons/blanks/32x32.dmi's "nothing" (or on the first state, with no blank).
    internal static icon preference_body(bool blank, params string[] states)
    {
        icon body = blank ? Dm.icon(Dm.resource("icons/blanks/32x32.dmi"), "nothing") : Dm.icon(Dm.resource("icons/mob/human/bodyparts_greyscale.dmi"), states[0]);
        foreach (string state in blank ? states : states[1..])
            body.Blend(Dm.icon(Dm.resource("icons/mob/human/bodyparts_greyscale.dmi"), state), ICON_OVERLAY);
        return body;
    }
}

/// Backpack preference
// tg: code/modules/client/preferences/clothing.dm:17
[DmPath("/datum/preference/choiced/backpack")]
public partial class datum_preference_choiced_backpack : datum_preference_choiced
{
    // tg: code/modules/client/preferences/clothing.dm:23
    public override DmList init_possible_values()
    {
        return Dm.list(
            GBACKPACK,
            GSATCHEL,
            LSATCHEL,
            GDUFFELBAG,
            GMESSENGER,
            DBACKPACK,
            DSATCHEL,
            DDUFFELBAG,
            DMESSENGER);
    }

    // tg: code/modules/client/preferences/clothing.dm:36
    public override object create_default_value()
    {
        return DBACKPACK;
    }

    // tg: code/modules/client/preferences/clothing.dm:39
    public override object icon_for(object value = null)
    {
        switch (value)
        {
            case GBACKPACK:
                return typeof(obj_item_storage_backpack);
            case GSATCHEL:
                return typeof(obj_item_storage_backpack_satchel);
            case LSATCHEL:
                return typeof(obj_item_storage_backpack_satchel_leather);
            case GDUFFELBAG:
                return typeof(obj_item_storage_backpack_duffelbag);
            case GMESSENGER:
                return typeof(obj_item_storage_backpack_messenger);

            // In a perfect world, these would be your department's backpack.
            // However, this doesn't factor in assistants, or no high slot, and would
            // also increase the spritesheet size a lot.
            // I play medical doctor, and so medical doctor you get.
            case DBACKPACK:
                return typeof(obj_item_storage_backpack_medic);
            case DSATCHEL:
                return typeof(obj_item_storage_backpack_satchel_med);
            case DDUFFELBAG:
                return typeof(obj_item_storage_backpack_duffelbag_med);
            case DMESSENGER:
                return typeof(obj_item_storage_backpack_messenger_med);
        }
        return null;
    }

    // tg: code/modules/client/preferences/clothing.dm:65
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.backpack = Dm.str(value);
    }
}

/// Jumpsuit preference
// tg: code/modules/client/preferences/clothing.dm:70
[DmPath("/datum/preference/choiced/jumpsuit")]
public partial class datum_preference_choiced_jumpsuit : datum_preference_choiced
{
    // tg: code/modules/client/preferences/clothing.dm:77
    public override DmList init_possible_values()
    {
        return Dm.list(
            PREF_SUIT,
            PREF_SKIRT);
    }

    // tg: code/modules/client/preferences/clothing.dm:83
    public override object create_default_value()
    {
        return PREF_SUIT;
    }

    // tg: code/modules/client/preferences/clothing.dm:86
    public override object icon_for(object value = null)
    {
        switch (value)
        {
            case PREF_SUIT:
                return typeof(obj_item_clothing_under_color_grey);
            case PREF_SKIRT:
                return typeof(obj_item_clothing_under_color_jumpskirt_grey);
        }
        return null;
    }

    // tg: code/modules/client/preferences/clothing.dm:93
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.jumpsuit_style = Dm.str(value);
    }
}

/// Socks preference
// tg: code/modules/client/preferences/clothing.dm:98
[DmPath("/datum/preference/choiced/socks")]
public partial class datum_preference_choiced_socks : datum_preference_choiced
{
    // tg: code/modules/client/preferences/clothing.dm:105
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.socks_list);
    }

    // tg: code/modules/client/preferences/clothing.dm:108
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_clothing_socks_nude), (datum_sprite_accessory a) => a.name);
    }

    // tg: code/modules/client/preferences/clothing.dm:111
    public override object icon_for(object value = null)
    {
        icon lower_half = preference_body(true, "human_r_leg", "human_l_leg");
        return generate_underwear_icon(SSaccessories.socks_list[value] as datum_sprite_accessory, lower_half);
    }

    // tg: code/modules/client/preferences/clothing.dm:121
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.socks = Dm.str(value);
    }
}

/// Undershirt preference
// tg: code/modules/client/preferences/clothing.dm:126
[DmPath("/datum/preference/choiced/undershirt")]
public partial class datum_preference_choiced_undershirt : datum_preference_choiced
{
    // tg: code/modules/client/preferences/clothing.dm:134
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.undershirt_list);
    }

    // tg: code/modules/client/preferences/clothing.dm:137
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_clothing_undershirt_nude), (datum_sprite_accessory a) => a.name);
    }

    // tg: code/modules/client/preferences/clothing.dm:140
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        object gender = preferences.read_preference(typeof(datum_preference_choiced_gender));
        if (Dm.eq(gender, MALE))
            return Dm.initial(typeof(datum_sprite_accessory_clothing_undershirt_nude), (datum_sprite_accessory a) => a.name);
        if (Dm.eq(gender, FEMALE))
            return Dm.initial(typeof(datum_sprite_accessory_clothing_undershirt_sports_bra), (datum_sprite_accessory a) => a.name);

        return base.create_informed_default_value(preferences);
    }

    // tg: code/modules/client/preferences/clothing.dm:149
    public override object icon_for(object value = null)
    {
        icon body = preference_body(false, "human_r_leg", "human_l_leg", "human_r_arm", "human_l_arm", "human_r_hand", "human_l_hand", "human_chest_m");

        icon icon_with_undershirt = Dm.icon(body);

        if (!Dm.eq(value, "Nude"))
        {
            datum_sprite_accessory accessory = SSaccessories.undershirt_list[value] as datum_sprite_accessory;
            icon_with_undershirt.Blend(Dm.icon(Dm.resource("icons/mob/clothing/underwear.dmi"), accessory.icon_state), ICON_OVERLAY);
        }

        icon_with_undershirt.Crop(9, 9, 23, 23);
        icon_with_undershirt.Scale(32, 32);
        return icon_with_undershirt;
    }

    // tg: code/modules/client/preferences/clothing.dm:170
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.undershirt = Dm.str(value);
    }
}

/// Underwear preference
// tg: code/modules/client/preferences/clothing.dm:175
[DmPath("/datum/preference/choiced/underwear")]
public partial class datum_preference_choiced_underwear : datum_preference_choiced
{
    // tg: code/modules/client/preferences/clothing.dm:182
    public override DmList init_possible_values()
    {
        return (DmList)assoc_to_keys_features(SSaccessories.underwear_list);
    }

    // tg: code/modules/client/preferences/clothing.dm:185
    public override object create_default_value()
    {
        return Dm.initial(typeof(datum_sprite_accessory_clothing_underwear_male_hearts), (datum_sprite_accessory a) => a.name);
    }

    // tg: code/modules/client/preferences/clothing.dm:188
    public override object icon_for(object value = null)
    {
        icon lower_half = preference_body(true, "human_chest_m", "human_r_leg", "human_l_leg");
        return generate_underwear_icon(SSaccessories.underwear_list[value] as datum_sprite_accessory, lower_half, COLOR_ALMOST_BLACK);
    }

    // tg: code/modules/client/preferences/clothing.dm:199
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.underwear = Dm.str(value);
    }

    // tg: code/modules/client/preferences/clothing.dm:202
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species = GLOB.species_prototypes[species_type] as datum_species;
        return !Dm.@in(TRAIT_NO_UNDERWEAR, species.inherent_traits) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/clothing.dm:210
    public override object compile_constant_data()
    {
        DmList data = (DmList)base.compile_constant_data();

        data[SUPPLEMENTAL_FEATURE_KEY] = "underwear_color";

        return data;
    }
}
