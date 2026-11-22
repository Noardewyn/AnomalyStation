// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/screentips.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/screentips.dm:1
[DmPath("/datum/preference/choiced/enable_screentips")]
public partial class datum_preference_choiced_enable_screentips : datum_preference_choiced
{
    // tg: code/modules/client/preferences/screentips.dm:6
    public override DmList init_possible_values()
    {
        return Dm.list(SCREENTIP_PREFERENCE_ENABLED, SCREENTIP_PREFERENCE_CONTEXT_ONLY, SCREENTIP_PREFERENCE_DISABLED);
    }

    // tg: code/modules/client/preferences/screentips.dm:9
    public override object create_default_value()
    {
        return SCREENTIP_PREFERENCE_ENABLED;
    }

    // tg: code/modules/client/preferences/screentips.dm:12
    public override void apply_to_client(client client = null, object value = null)
    {
        if (client.mob?.hud_used != null)
            client.mob.hud_used.screentips_enabled = value as string;
    }

    // tg: code/modules/client/preferences/screentips.dm:15
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        // Migrate old always disabled screentips to context only.
        // Screentips were always meant to have context, though were initially merged without it.
        // This accepts that those users found screentips distracting, but gives a second chance now that
        // they provide a more obvious helping hand.
        // If they are still too distracting, there's nothing stopping them from disabling it again for good.
        if (Dm.eq(input, FALSE))
            return base.deserialize(SCREENTIP_PREFERENCE_CONTEXT_ONLY, preferences);

        if (Dm.eq(input, TRUE))
            return base.deserialize(SCREENTIP_PREFERENCE_ENABLED, preferences);

        return base.deserialize(input, preferences);
    }
}

// tg: code/modules/client/preferences/screentips.dm:29
[DmPath("/datum/preference/color/screentip_color")]
public partial class datum_preference_color_screentip_color : datum_preference_color
{
    // tg: code/modules/client/preferences/screentips.dm:34
    public override void apply_to_client(client client = null, object value = null)
    {
        if (client.mob?.hud_used != null)
            client.mob.hud_used.screentip_color = value;
    }

    // tg: code/modules/client/preferences/screentips.dm:37
    public override object create_default_value()
    {
        return LIGHT_COLOR_FAINT_BLUE;
    }
}

// tg: code/modules/client/preferences/screentips.dm:40
[DmPath("/datum/preference/toggle/screentip_images")]
public partial class datum_preference_toggle_screentip_images : datum_preference_toggle
{
    // tg: code/modules/client/preferences/screentips.dm:45
    public override void apply_to_client(client client = null, object value = null)
    {
        if (client.mob?.hud_used != null)
            client.mob.hud_used.screentip_images = Dm.num(value);
    }
}
