using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tg;

// Protocol version 17 (drag and drop; docs/ai/specs/interaction.md section 8, protocol.md section 26): BYOND's side of a mouse drop.
// BYOND calls the client's MouseDrag while the mouse moves with a button held (src_object the thing the press was on, over_object
// the thing under the mouse) and the client's MouseDrop when the button is released; the defaults call the same procs on
// src_object (OpenDream's DMStandard Client.dm; MouseDrop__d0, MouseDrag__d0). tg overrides both client procs
// (code/_onclick/drag_drop.dm) for its bookkeeping and /atom/MouseDrop for the drop's handlers.

public static partial class ClientSeam
{
    /// <summary>
    /// The player dragged <paramref name="dragged"/> and released it over <paramref name="over"/> (both validated by the host as a
    /// click's target is: something the player sees, its own HUD, or an item shown in a hand or slot): tg's <c>/client/MouseDrag</c>
    /// with the drag's start params, then tg's <c>/client/MouseDrop</c> with the release's, with <c>usr</c> the client's mob, BYOND's
    /// locations (a map object's turf, the turf itself, null for a screen object as in <see cref="Click"/>) and the map's control.
    /// Detached, as BYOND starts a verb (a drop's handler may wait: a closet stuffed with a mob, a storage dumped onto a table).
    /// <para>
    /// DEVIATION(DV45): BYOND calls MouseDrag on every move of the drag, the first of which sets tg's <c>drag_start</c> and
    /// <c>drag_details</c>; the client sends only the drop, so tg's MouseDrag runs once, here, and <c>drag_start</c> is then dated
    /// back by the drag's length the client measured (<paramref name="dragDeciseconds"/>), so that tg's quick-drag-as-a-click rule
    /// (<c>client/is_drag_clickable</c>: shorter than 0.1 s) sees the drag's real length.
    /// </para>
    /// </summary>
    public static void Drop(client player, atom dragged, atom over, string dragParams, string dropParams, int dragDeciseconds)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null)
            return;
        if (dragged == null || dragged.gc_destroyed != null || over == null || over.gc_destroyed != null)
            return; // BYOND sends no drag of an object that is gone
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = player.mob;
            Waiting.Detach(DragAndDrop(player, dragged, over, LocationOf(player, dragged), LocationOf(player, over), dragParams, dropParams, dragDeciseconds));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    private static async DmTask DragAndDrop(client player, atom dragged, atom over, atom srcLocation, atom overLocation, string dragParams, string dropParams, int dragDeciseconds)
    {
        await player.MouseDrag(dragged, over, srcLocation, overLocation, Defines.SKIN_MAPWINDOW_MAP, Defines.SKIN_MAPWINDOW_MAP, dragParams);
        // DEVIATION(DV155): dated back no further than just after the world's start: drag_start 0 means "no drag" to tg, and a drag longer than the
        // world's age (the radial's Climb or Buckle in a round's first seconds) would else stay a quick drag, a click
        float started = Dm.world.time - Math.Max(0, dragDeciseconds);
        if (started > 0 || dragDeciseconds > 1)
            player.drag_start = Math.Max(started, 0.01f);
        await player.MouseDrop(dragged, over, srcLocation, overLocation, Defines.SKIN_MAPWINDOW_MAP, Defines.SKIN_MAPWINDOW_MAP, dropParams);
    }

    /// <summary>
    /// Version 17: BYOND's params of a MouseDrag or MouseDrop: <see cref="ClickParams(string, bool, bool, bool, int, int, string)"/>'s keys (the
    /// icon pixel of the object under the mouse, the button, the modifiers, <c>button</c>) with <c>drag</c> (tg's <c>DRAG</c>: the button that
    /// drags, BYOND ref "mouse control": "drag" is set in MouseDrag and MouseDrop) before the screen object's <c>screen-loc</c>.
    /// </summary>
    public static string DropParams(string button, bool shift, bool ctrl, bool alt, int iconX, int iconY, string screenLoc)
    {
        string text = ClickParams(button, shift, ctrl, alt, iconX, iconY) + ";" + Defines.DRAG + "=" + button;
        if (!string.IsNullOrEmpty(screenLoc) && screenLoc.AsSpan().IndexOfAny(';', '&', '=') < 0)
            text += ";" + Defines.SCREEN_LOC + "=" + screenLoc;
        return text;
    }
}
