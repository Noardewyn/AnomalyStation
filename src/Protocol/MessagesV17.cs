namespace Anomaly.Protocol;

// Protocol version 17 (drag and drop: BYOND's MouseDrop reaches the server; docs/ai/specs/protocol.md section 26). BYOND calls the
// client's MouseDrag while the mouse moves with a button held over a thing and MouseDrop when the button is released over another;
// tg's /client/MouseDrop (code/_onclick/drag_drop.dm) calls the dragged atom's MouseDrop, which runs the drop's handlers (a worn
// backpack dragged onto a hand slot comes off into that hand, a storage dragged onto its wearer opens, onto a table empties out,
// an item dragged between storage cells reorders them). The client starts a drag when the button is held and the pointer moves
// past a few pixels (a press and release without that is the old Click) and sends one Drop action on the release
// (ActionKind.Drop, MessagesV2.cs): the dragged thing, what it was released over, the button and modifiers, tg's icon pixel of the
// thing under the pointer and how long the drag lasted.

/// <summary>The last action kind the codec takes (<see cref="ActionKind.MouseUp"/> since version 34, MessagesV34.cs).</summary>
public static class ActionKinds
{
    /// <summary>The highest <see cref="ActionKind"/> on the wire.</summary>
    public const ActionKind Last = ActionKind.InventoryOpen; // version 100 (also 29: InventoryMove, 28: HotbarPress; 97: Loaded, 86: ContextPick, 82: PutDown, 81: Throw, 78: PopupClose, 70: MouseWheel)
}
