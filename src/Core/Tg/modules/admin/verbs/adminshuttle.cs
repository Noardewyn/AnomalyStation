// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/adminshuttle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with the shuttles (later milestones). Only the type /obj/docking_port/mobile (its [DmPath]: adminshuttle.dm is the earliest .dme file that declares it, rules 2.3), which /atom/proc/connect_to_shuttle names (wave 3, the atoms package),
// declared with the slot signatures of docs/ai/specs/generated/slots.tsv; bodies are Dm.NotTranslated until then.

// tg: code/modules/admin/verbs/adminshuttle.dm:135
[DmPath("/obj/docking_port/mobile")]
public partial class obj_docking_port_mobile : obj_docking_port
{
}
