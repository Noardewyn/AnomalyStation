// Engine ("BYOND builtin") part of tg's /obj (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md section 2).
// Not translated from a tg file: BYOND declares /obj; tg's own vars and procs are in game/objects/objs.cs and the other
// mirror files.
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath].
[DmPath("/obj")]
public partial class obj : atom_movable
{
    /// <summary>
    /// BYOND's default of /obj: layer 3 (OBJ_LAYER; BYOND ref <c>/atom/var/layer</c>, OpenDream
    /// DMStandard/Types/Atoms/Obj.dm). tg's overrides of /obj follow in <see cref="dm_tg_vars"/> (rules 10.2, "Builtin
    /// types").
    /// </summary>
    public obj()
    {
        layer = OBJ_LAYER;
        dm_tg_vars();
    }

    // tg's overrides of /obj's defaults, implemented in game/objects/objs.cs (rules 10.2, "Builtin types").
    private partial void dm_tg_vars();
}
