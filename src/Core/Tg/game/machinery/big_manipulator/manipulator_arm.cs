// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/big_manipulator/manipulator_arm.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E16: the big manipulator's arm, whole: the claw drawn over the core (in its vis_contents) that the manipulator turns
// toward its task's tile, with a copy of the held item's look in the claw. The var blocks are generated.

/// Manipulator hand. Effect we animate to show that the manipulator is working and moving something.
// tg: code/game/machinery/big_manipulator/manipulator_arm.dm:2
public partial class obj_effect_big_manipulator_arm : obj_effect
{
    // tg: code/game/machinery/big_manipulator/manipulator_arm.dm:18
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        Dm.append(ref dot, update_item_overlay());
        return dot;
    }

    // tg: code/game/machinery/big_manipulator/manipulator_arm.dm:22
    public virtual object update_item_overlay()
    {
        if (item_in_my_claw == null)
            return icon_overlay = null;
        atom_movable item_data = item_in_my_claw.resolve() as atom_movable;
        icon_overlay = (mutable_appearance)mutable_appearance(item_data.icon, Dm.str(item_data.icon_state), item_data.layer, this, item_data.plane, item_data.alpha, item_data.appearance_flags);
        icon_overlay.color = item_data.color;
        icon_overlay.appearance = item_data.appearance;
        icon_overlay.pixel_w = 32 + Dm.num(calculate_item_offset(is_x: TRUE));
        icon_overlay.pixel_z = 32 + Dm.num(calculate_item_offset(is_x: FALSE));
        return icon_overlay;
    }

    /// Updates item that is in the claw.
    // tg: code/game/machinery/big_manipulator/manipulator_arm.dm:34
    public virtual void update_claw(object clawed_item = null)
    {
        item_in_my_claw = clawed_item as datum_weakref;
        update_appearance();
    }

    /// Calculate x and y coordinates so that the item icon appears in the claw and not somewhere in the corner.
    // tg: code/game/machinery/big_manipulator/manipulator_arm.dm:39
    public virtual object calculate_item_offset(float? is_x = null, float? pixels_to_offset = null)
    {
        is_x ??= TRUE;
        pixels_to_offset ??= 32;
        object offset = null;
        if (dir == NORTH)
            offset = Dm.truthy(is_x) ? 0 : pixels_to_offset;
        else if (dir == SOUTH)
            offset = Dm.truthy(is_x) ? 0 : -pixels_to_offset;
        else if (dir == EAST)
            offset = Dm.truthy(is_x) ? pixels_to_offset : 0;
        else if (dir == WEST)
            offset = Dm.truthy(is_x) ? -pixels_to_offset : 0;
        return offset;
    }
}
