// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/moth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the whole file: the mothman's Character Setup features (antennae, body markings, wings). A /datum/universal_icon is the
// port's /icon (universal_icon.cs), as in the lizard's file; the static icons of tg are made each call (an /icon is a short list of operations).

// tg: code/modules/client/preferences/species_features/moth.dm:1
public partial class datum_preference_choiced_species_feature_moth_antennae : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/moth.dm:9
    public override object icon_for(object value = null)
    {
        icon moth_head = Dm.icon(Dm.resource("icons/mob/human/species/moth/bodyparts.dmi"), "moth_head");
        moth_head.Blend(Dm.icon(Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon), Dm.text("[]_l", Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon_state))), ICON_OVERLAY);
        moth_head.Blend(Dm.icon(Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon), Dm.text("[]_r", Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon_state))), ICON_OVERLAY);

        datum_sprite_accessory antennae = get_accessory_for_value(value) as datum_sprite_accessory;

        icon icon_with_antennae = Dm.icon(moth_head);
        icon_with_antennae.Blend(Dm.icon(antennae.icon, Dm.text("m_moth_antennae_[]_FRONT", antennae.icon_state)), ICON_OVERLAY);
        icon_with_antennae.Scale(64, 64);
        icon_with_antennae.Crop(15, 64 - 31, 15 + 31, 64);

        return icon_with_antennae;
    }
}

// tg: code/modules/client/preferences/species_features/moth.dm:27
public partial class datum_preference_choiced_species_feature_moth_markings : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/moth.dm:34
    public override object icon_for(object value = null)
    {
        Type[] body_parts =
        {
            typeof(obj_item_bodypart_head_moth),
            typeof(obj_item_bodypart_chest_moth),
            typeof(obj_item_bodypart_arm_left_moth),
            typeof(obj_item_bodypart_arm_right_moth),
        };

        icon moth_body = Dm.icon(Dm.resource("icons/blanks/32x32.dmi"), "nothing");

        foreach (Type body_part in body_parts)
            moth_body.Blend(Dm.icon(Dm.resource("icons/mob/human/species/moth/bodyparts.dmi"), Dm.initial(body_part, (obj_item_bodypart p) => p.icon_state)), ICON_OVERLAY);

        moth_body.Blend(Dm.icon(Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon), Dm.text("[]_l", Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon_state))), ICON_OVERLAY);
        moth_body.Blend(Dm.icon(Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon), Dm.text("[]_r", Dm.initial(typeof(obj_item_organ_eyes_moth), (obj_item_organ_eyes p) => p.eye_icon_state))), ICON_OVERLAY);

        datum_sprite_accessory markings = get_accessory_for_value(value) as datum_sprite_accessory;
        icon icon_with_markings = Dm.icon(moth_body);

        if (!Dm.eq(value, SPRITE_ACCESSORY_NONE))
        {
            foreach (Type body_part in body_parts)
            {
                icon body_part_icon = Dm.icon(markings.icon, Dm.text("[]_[]", markings.icon_state, Dm.initial(body_part, (obj_item_bodypart p) => p.body_zone)));
                body_part_icon.Crop(1, 1, 32, 32);
                icon_with_markings.Blend(body_part_icon, ICON_OVERLAY);
            }
        }

        icon_with_markings.Blend(Dm.icon(Dm.resource("icons/mob/human/species/moth/moth_wings.dmi"), "m_moth_wings_plain_FRONT"), ICON_OVERLAY);
        icon_with_markings.Blend(Dm.icon(Dm.resource("icons/mob/human/species/moth/moth_antennae.dmi"), "m_moth_antennae_plain_FRONT"), ICON_OVERLAY);

        // Zoom in on the top of the head and the chest
        icon_with_markings.Scale(64, 64);
        icon_with_markings.Crop(15, 64 - 31, 15 + 31, 64);

        return icon_with_markings;
    }
}

// tg: code/modules/client/preferences/species_features/moth.dm:71
public partial class datum_preference_choiced_species_feature_moth_wings : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/moth.dm:78
    public override object icon_for(object value = null)
    {
        datum_sprite_accessory moth_wings = get_accessory_for_value(value) as datum_sprite_accessory;
        return Dm.icon(moth_wings.icon, Dm.text("m_moth_wings_[]_BEHIND", moth_wings.icon_state));
    }
}
