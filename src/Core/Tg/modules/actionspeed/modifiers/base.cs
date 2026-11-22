// Translated from tgstation (snapshot 1d99bd7f): code/modules/actionspeed/modifiers/base.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (W4.0 contracts, rules 10.2: a type with its var override); /mob/proc/initialize_actionspeed adds it.
// Package D owns the file in wave 4.

// tg: code/modules/actionspeed/modifiers/base.dm:1
[DmPath("/datum/actionspeed_modifier/base")]
public partial class datum_actionspeed_modifier_base : datum_actionspeed_modifier
{
    // tg: code/modules/actionspeed/modifiers/base.dm:2
    public datum_actionspeed_modifier_base()
    {
        variable = TRUE;
    }
}
