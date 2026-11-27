// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmo_controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gizmo controllers, which make a gizmo's interfaces (its wires, and for the voice gizmo its voice) and hand
// each one its puzzle. The var blocks are generated (D111).

/// The master datum that handles every bit of gizmo code
/// Just so you will understand the full insanity of this:
/// object > gizmo_controller > gizmo_puzzle + gizmo_interface > gizmode > gizpulse > callbacks
// tg: code/modules/research/gizmo/gizmo_controller.dm:4
public partial class datum_gizmo_controller : datum
{
    /// Generate interfaces for interacting with the gizmo
    // tg: code/modules/research/gizmo/gizmo_controller.dm:11
    public virtual async DmTask generate_interfaces(atom_movable holder = null)
    {
        foreach ((object interface_define, object interface_type) in Dm.pairs(interfaces))
        {
            DmList callbacks = Dm.list();
            datum_gizmo_interface interface_instance = Dm.@new(interface_type as Type, this) as datum_gizmo_interface;
            await interface_instance.generate_interface(holder, null); // tg passes its empty callbacks list to the unused pulse_callback
            Dm.append(ref instances, interface_instance);

            object sw__1 = interface_define;
            if (Dm.eq(sw__1, GIZMO_INTERFACE_WIRES))
                holder.set_wires(Dm.@new<datum_wires_gizmo>(holder, interface_instance.puzzle_value));
            else if (Dm.eq(sw__1, GIZMO_INTERFACE_VOICE))
                await holder.AddComponent(typeof(datum_component_gizmo_voice), interface_instance.puzzle_value);
        }
    }
}
