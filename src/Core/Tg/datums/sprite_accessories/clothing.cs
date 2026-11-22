// Translated from tgstation (snapshot 1d99bd7f): code/datums/sprite_accessories/clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice D1 (lobby.md 8): the procs; the vars and the 135 accessories are generated. Gaps SP1: the digitigrade underwear.

// tg: code/datums/sprite_accessories/clothing.dm:1
[DmPath("/datum/sprite_accessory/clothing")]
public partial class datum_sprite_accessory_clothing : datum_sprite_accessory
{
    /// Override to return a different icon state given a bodytype or physique
    // tg: code/datums/sprite_accessories/clothing.dm:17
    public virtual object get_icon_state(object physique = null, object bodyshape = null)
    {
        return icon_state;
    }

    /**
     * Generate an appearance from this clothing datum
     *
     * * color - if this is NOT a statically colored clothing article and NOT gags, uses this color.
     * * physique - physique of the wearer (male or female)
     * * bodyshape - bodyshape of the wearer (humanoid, digitigrade, etc)
     */
    // tg: code/datums/sprite_accessories/clothing.dm:27
    public virtual object make_appearance(string color = null, string physique = null, float? bodyshape = null)
    {
        color ??= COLOR_WHITE;
        physique ??= MALE;
        bodyshape ??= BODYSHAPE_HUMANOID;
        bool use_digi = Dm.truthy(digi_icon_state) && Dm.truthy(Dm.bitand(bodyshape, BODYSHAPE_DIGITIGRADE));
        object icon_state_to_use = get_icon_state(physique, bodyshape);
        // The cached icon ops (:39-52) are server-side icons the port does not render (D104): a GAGS icon is the name the client
        // composes (DV19), so it needs no icon() copy and no cache. DEVIATION(DV-F1a): no female cut (wear_female_version, :45),
        // the shape the jumpsuits keep too.
        object icon_file_path = greyscale_config != null ? SSgreyscale.GetColoredIconByType(greyscale_config, greyscale_colors) : icon;
        mutable_appearance result;
        if (use_digi)
        {
            // gaps SP1: a digitigrade lizard's underwear, its legs masked off and the digitigrade legs drawn in (an /icon recipe the
            // client composes, universal_icon.cs)
            icon created = Dm.icon(icon_file_path, icon_state_to_use);
            icon replacement = Dm.icon(SSgreyscale.GetColoredIconByType(typeof(datum_greyscale_config_digitigrade_underwear), greyscale_colors), digi_icon_state);
            created = apply_icon_mask(created, LEGS_MASK, replacement) as icon;
            result = (mutable_appearance)mutable_appearance(created);
        }
        else
            result = (mutable_appearance)mutable_appearance(icon_file_path, icon_state_to_use as string);

        result.layer = -Dm.num(layer);
        result.color = Dm.truthy(use_static) ? null : color;

        return result;
    }
}

// tg: code/datums/sprite_accessories/clothing.dm:73
[DmPath("/datum/sprite_accessory/clothing/underwear/nude")]
public partial class datum_sprite_accessory_clothing_underwear_nude : datum_sprite_accessory_clothing_underwear
{
    // tg: code/datums/sprite_accessories/clothing.dm:78 (make_appearance(mob/living/carbon/human/for_who): the slot keeps the base's names)
    public override object make_appearance(string color = null, string physique = null, float? bodyshape = null)
    {
        return null;
    }
}

// tg: code/datums/sprite_accessories/clothing.dm:267
[DmPath("/datum/sprite_accessory/clothing/undershirt/nude")]
public partial class datum_sprite_accessory_clothing_undershirt_nude : datum_sprite_accessory_clothing_undershirt
{
    // tg: code/datums/sprite_accessories/clothing.dm:272
    public override object make_appearance(string color = null, string physique = null, float? bodyshape = null)
    {
        return null;
    }
}

// tg: code/datums/sprite_accessories/clothing.dm:556
[DmPath("/datum/sprite_accessory/clothing/socks/nude")]
public partial class datum_sprite_accessory_clothing_socks_nude : datum_sprite_accessory_clothing_socks
{
    // tg: code/datums/sprite_accessories/clothing.dm:560
    public override object make_appearance(string color = null, string physique = null, float? bodyshape = null)
    {
        return null;
    }
}
