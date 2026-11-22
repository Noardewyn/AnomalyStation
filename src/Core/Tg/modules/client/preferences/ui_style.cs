// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/ui_style.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// UI style preference
// tg: code/modules/client/preferences/ui_style.dm:2
[DmPath("/datum/preference/choiced/ui_style")]
public partial class datum_preference_choiced_ui_style : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ui_style.dm:8
    public override DmList init_possible_values()
    {
        return assoc_to_keys(GLOB.available_ui_styles) as DmList;
    }

    // tg: code/modules/client/preferences/ui_style.dm:11
    public override object icon_for(object value = null)
    {
        object ui_icons = GLOB.available_ui_styles[value];

        // a /datum/universal_icon is the port's /icon (universal_icon.cs)
        icon icon = Dm.icon(ui_icons, "hand_l");
        icon.Crop(1 - ICON_SIZE_X, 1, ICON_SIZE_Y, ICON_SIZE_X);
        icon.Blend(Dm.icon(ui_icons, "hand_r"), ICON_OVERLAY);

        return icon;
    }

    // tg: code/modules/client/preferences/ui_style.dm:19
    public override object create_default_value()
    {
        return GLOB.available_ui_styles[1];
    }

    // tg: code/modules/client/preferences/ui_style.dm:22
    public override void apply_to_client(client client = null, object value = null)
    {
        if (client.mob?.hud_used != null)
            Waiting.NoSleep(client.mob.hud_used.update_ui_style(ui_style2icon(value)));
    }
}
