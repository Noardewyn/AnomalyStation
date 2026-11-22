namespace Anomaly.Protocol;

// Protocol version 14 (M4c slice 3: BYOND's MouseExited reaches the server; docs/ai/specs/protocol.md section 23). BYOND calls
// MouseExited on the object the mouse leaves before MouseEntered on the one it enters, and alone when it leaves to nothing; tg's
// hover states (the space suit display's percentage, the inventory slot's item outline, the zone selector's highlighted part, the
// action button's tooltip) are set by MouseEntered and cleared by MouseExited. The server keeps the last hovered thing of each
// client and exits it when a Hover names another one; the new ActionKind.Unhover (MessagesV2.cs) says the pointer left it over
// nothing. The client also hovers its HUD entries in cursor mode (a Screen target), as the mouse over BYOND's screen does.
// The last action kind the codec takes is ActionKinds.Last (MessagesV16.cs since version 16).
