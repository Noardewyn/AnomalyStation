// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/pod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the whole file, the podperson's hairstyle in Character Setup.

// tg: code/modules/client/preferences/species_features/pod.dm:1
public partial class datum_preference_choiced_species_feature_pod_hair : datum_preference_choiced_species_feature
{
    // tg: code/modules/client/preferences/species_features/pod.dm:9
    public override object icon_for(object value = null)
    {
        datum_sprite_accessory pod_hair = get_accessory_for_value(value) as datum_sprite_accessory;

        icon icon_with_hair = Dm.icon(Dm.resource("icons/mob/human/bodyparts_greyscale.dmi"), "pod_head_m");

        icon icon_adj = Dm.icon(pod_hair.icon, Dm.text("m_pod_hair_[]_ADJ", pod_hair.icon_state));
        icon icon_front = Dm.icon(pod_hair.icon, Dm.text("m_pod_hair_[]_FRONT", pod_hair.icon_state));
        icon_adj.Blend(icon_front, ICON_OVERLAY);
        icon_with_hair.Blend(icon_adj, ICON_OVERLAY);
        icon_with_hair.Scale(64, 64);
        icon_with_hair.Crop(15, 64 - 31, 15 + 31, 64);
        icon_with_hair.Blend(COLOR_GREEN, ICON_MULTIPLY);

        return icon_with_hair;
    }
}
