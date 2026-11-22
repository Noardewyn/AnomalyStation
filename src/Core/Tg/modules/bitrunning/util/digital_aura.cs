// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/util/digital_aura.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the red glitching shell around the virtual domain's threats (the glitch's mobs, the
// mutated mobs, the cyber police): the overlays, the alpha and the light.

// tg: code/modules/bitrunning/util/digital_aura.dm:1
public partial class atom
{
    /// Creates a digital effect around the target
    // tg: code/modules/bitrunning/util/digital_aura.dm:2
    public virtual async DmTask create_digital_aura()
    {
        DmList overlays = get_digital_overlays() as DmList;
        if (length(overlays) == 0)
            return;

        add_overlay(overlays);
        alpha = 210;
        await set_light(2, l_color: LIGHT_COLOR_BUBBLEGUM, l_on: TRUE);
        update_appearance();
    }

    /// Removes the digital effect around the target
    // tg: code/modules/bitrunning/util/digital_aura.dm:13
    public virtual async DmTask remove_digital_aura()
    {
        DmList overlays = get_digital_overlays() as DmList;
        if (length(overlays) == 0)
            return;

        cut_overlay(overlays);
        alpha = 255;
        await set_light(0, l_color: null, l_on: FALSE);
        update_appearance();
    }

    /// Returns a list of overlays to be used for the digital effect
    // tg: code/modules/bitrunning/util/digital_aura.dm:24
    public virtual object get_digital_overlays()
    {
        object base_icon = null;
        DmList dimensions = get_icon_dimensions(icon);
        if (length(dimensions) == 0)
            return null;

        switch (Dm.num(dimensions["width"]))
        {
            case 32:
                base_icon = Dm.resource("icons/effects/bitrunning.dmi");
                break;
            case 48:
                base_icon = Dm.resource("icons/effects/bitrunning_48.dmi");
                break;
            case 64:
                base_icon = Dm.resource("icons/effects/bitrunning_64.dmi");
                break;
        }

        mutable_appearance redshift = mutable_appearance(base_icon, "redshift") as mutable_appearance;
        redshift.blend_mode = BLEND_MULTIPLY;

        object glitch_effect = mutable_appearance(base_icon, "glitch", MUTATIONS_LAYER, alpha: 150);

        return Dm.list(glitch_effect, redshift);
    }
}
