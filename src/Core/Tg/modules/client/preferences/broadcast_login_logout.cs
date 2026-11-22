// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/broadcast_login_logout.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

/// Whether or not to announce when the player logs in or out.
// tg: code/modules/client/preferences/broadcast_login_logout.dm:2  (its vars only; the toggle's procs are the parent's)
[DmPath("/datum/preference/toggle/broadcast_login_logout")]
public partial class datum_preference_toggle_broadcast_login_logout : datum_preference_toggle
{
}
