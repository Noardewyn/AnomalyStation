// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/accessibility.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the double-click toggle (examine reads it), the stair indicator toggle (D325: the stairs read it); the flash, screen-shake and recoil preferences keep DV161.

/// When toggled, removes some double-click reliant actions.
// tg: code/modules/client/preferences/accessibility.dm:16
public partial class datum_preference_toggle_remove_double_click : datum_preference_toggle
{
}

/// When toggled, enables staircase indicators
// tg: code/modules/client/preferences/accessibility.dm:33 (D325: the stairs read it)
public partial class datum_preference_toggle_stair_indicator : datum_preference_toggle
{
    // tg: code/modules/client/preferences/accessibility.dm:39
    public override void apply_to_client_updated(client client = null, object value = null)
    {
        if (Dm.truthy(value) || !GlobalProcs.isliving(client.mob)) // only hide, showing is more trouble than it's worth
            return;

        object climber_ref = GlobalProcs.WEAKREF(client.mob);
        foreach (obj_structure_stairs stair in Dm.each<obj_structure_stairs>(GLOB.stairs))
            stair.clear_climber_image(climber_ref as datum_weakref);
    }
}
