// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmo_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the handheld gizmo and the hard-mode code-crack gizmo the tutorial gizmo dispenses: a random look, a controller
// that makes its wires, opened with a wire tool. The var blocks are generated (D111).
//
// GAP(types): tg's `var/datum/gizmo_controller/controller` (:12) is generated as a Type (rules 3.2 V3: its default is a path that
// tg replaces by the controller on Initialize); the controller the gizmo holds is controller_value (the same split as
// /obj/machinery/var/circuit).

/// A handheld gizmo, with some different activation modes
// tg: code/modules/research/gizmo/gizmo_items.dm:2
public partial class obj_item_gizmo : obj_item
{
    /// GAP(types): tg's `controller` (:12) once Initialize made it: reference to the gizmo master controller that handles all the
    /// other gizmo stuff
    public datum_gizmo_controller controller_value;

    // tg: code/modules/research/gizmo/gizmo_items.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(icon_states))
        {
            base_icon_state = Dm.str(pick(icon_states));
            icon_state = Dm.str(base_icon_state);
        }

        controller_value = Dm.@new(controller, this) as datum_gizmo_controller;
        Waiting.NoSleep(controller_value.generate_interfaces(this));
        return dot;
    }

    // tg: code/modules/research/gizmo/gizmo_items.dm:24
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);

        if (Dm.truthy(is_wire_tool(tool)))
        {
            await attempt_wire_interaction(user);
            return ITEM_INTERACT_SUCCESS;
        }
        return NONE;
    }
}
