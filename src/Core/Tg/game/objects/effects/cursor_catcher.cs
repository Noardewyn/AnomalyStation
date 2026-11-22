// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/cursor_catcher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M7x slice D, DV98): the scope's tracker. It links to its mob and follows its moves and the view's size. tg's cursor tracking
// (MouseEntered and MouseMove store the pointer's pixel in the 2D view, calculate_params turns it into given_x, given_y and given_turf, and the
// scope pans the client's view to it) has no first-person counterpart: the camera ray is the aim, so given_turf stays null and the scope
// leaves the shot where the crosshair is. The var blocks are generated (D111).

/// An effect which tracks the cursor's location on the screen
// tg: code/game/objects/effects/cursor_catcher.dm:3
public partial class atom_movable_screen_fullscreen_cursor_catcher : atom_movable_screen_fullscreen
{
    /// Links this up with a mob
    // tg: code/game/objects/effects/cursor_catcher.dm:21
    public virtual void assign_to_mob(mob owner = null, params object[] __extra)
    {
        this.owner = owner;
        view_list = getviewsize(owner.client.view);
        RegisterSignal(owner, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(owner, COMSIG_VIEWDATA_UPDATE, nameof(on_viewdata_update));
        // calculate_params(): the cursor's pixel (DV98, see the header)
    }

    /// Update when the mob we're assigned to has moved
    // tg: code/game/objects/effects/cursor_catcher.dm:28
    [SignalHandler]
    public virtual void on_move(atom source = null, atom oldloc = null, object dir = null, object forced = null)
    {
        if (given_turf == null)
            return;
        float x_offset = source.loc.x - oldloc.x;
        float y_offset = source.loc.y - oldloc.y;
        given_turf = locate(given_turf.x + x_offset, given_turf.y + y_offset, given_turf.z);
    }

    /// Update when our screen size changes
    // tg: code/game/objects/effects/cursor_catcher.dm:36
    [SignalHandler]
    public virtual void on_viewdata_update(datum source = null, object view = null)
    {
        view_list = getviewsize(view);
    }
}

// tg: code/datums/components/scope.dm:239
public partial class atom_movable_screen_fullscreen_cursor_catcher_scope : atom_movable_screen_fullscreen_cursor_catcher
{
    // tg: code/datums/components/scope.dm:244
    public override void assign_to_mob(mob new_owner = null, params object[] __extra)
    {
        range_modifier = Dm.num(Dm.arg(__extra, 0));
        base.assign_to_mob(new_owner);
    }
}
