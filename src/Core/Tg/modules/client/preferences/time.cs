// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/time.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

/// Whether the clocks a player examines show the station's time on a twelve-hour dial
// tg: code/modules/client/preferences/time.dm:1  (its vars only; the toggle's procs are the parent's). Gaps ST3: the wall clock
// reads it (signs_interactive.dm:10), so it joins the preference entries (DV180)
[DmPath("/datum/preference/toggle/twelve_hour")]
public partial class datum_preference_toggle_twelve_hour : datum_preference_toggle
{
}
