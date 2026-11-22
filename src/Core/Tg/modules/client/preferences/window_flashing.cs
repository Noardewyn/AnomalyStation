// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/window_flashing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

/// Enables flashing the window in your task tray for important events
// tg: code/modules/client/preferences/window_flashing.dm:2  (its vars only; the toggle's procs are the parent's). Silicons SI2:
// window_flash reads it (the ghosts' notifications), so it joins the preference entries (DV180)
[DmPath("/datum/preference/toggle/window_flashing")]
public partial class datum_preference_toggle_window_flashing : datum_preference_toggle
{
}
