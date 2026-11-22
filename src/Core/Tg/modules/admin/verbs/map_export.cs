// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/map_export.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed with the shuttles (later milestones). Only the type /obj/docking_port (its [DmPath]: map_export.dm is the earliest .dme file that declares it, rules 2.3), the parent of the docking ports that /atom/proc/connect_to_shuttle names (wave 3, the atoms package),
// declared with the slot signatures of docs/ai/specs/generated/slots.tsv; bodies are Dm.NotTranslated until then.

// tg: code/modules/admin/verbs/map_export.dm:124
[DmPath("/obj/docking_port")]
public partial class obj_docking_port : obj
{
}

// tg: code/modules/admin/verbs/map_export.dm:79
public partial class atom : datum
{
    // tg: code/modules/admin/verbs/map_export.dm:79
    public virtual object get_save_vars()
    {
        object dot = Dm.list();
        Dm.append(ref dot, nameof(color));
        Dm.append(ref dot, nameof(dir));
        Dm.append(ref dot, nameof(icon));
        Dm.append(ref dot, nameof(icon_state));
        Dm.append(ref dot, nameof(name));
        Dm.append(ref dot, nameof(pixel_x));
        Dm.append(ref dot, nameof(pixel_y));
        Dm.append(ref dot, nameof(density));
        Dm.append(ref dot, nameof(opacity));

        if (Dm.truthy(uses_integrity))
        {
            if (!Dm.eq(atom_integrity, max_integrity)) // Only save if atom_integrity differs from max_integrity to avoid redundant saving
                Dm.append(ref dot, nameof(atom_integrity));
            Dm.append(ref dot, nameof(max_integrity));
            Dm.append(ref dot, nameof(integrity_failure));
            Dm.append(ref dot, nameof(damage_deflection));
            Dm.append(ref dot, nameof(resistance_flags));
        }

        return dot;
    }
}
