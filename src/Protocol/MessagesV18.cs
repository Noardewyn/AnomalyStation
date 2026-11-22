namespace Anomaly.Protocol;

// Protocol version 18 (HUD maptext, D142; docs/ai/specs/protocol.md section 27). tg draws text on its screen objects with BYOND's
// maptext: the space suit display's charge percentage on hover, the action buttons' cooldown counters and keybinds, the item
// count of a storage's stacks, the stack amount in a hand. The text is a small HTML subset (tg's MAPTEXT macros), drawn in a
// box of maptext_width x maptext_height pixels at maptext_x, maptext_y from the icon's lower left corner.
//
// The appearance entry gains the text (a string table reference) and its box (AppearanceEntry.Maptext, MaptextWidth, MaptextHeight,
// MaptextX, MaptextY; presence bit 15). Only an entry of a session's private table carries it: the public tables hold the world's
// appearances, and the world's maptext (runechat, floating text over atoms) is M9. The string cap of a table string rises from 256 to
// 1024 bytes (Limits.MaxTableStringBytes): tg's MAPTEXT_TINY_UNICODE macro alone is 130 bytes.
//
// There is no new message or action kind: the last action kind is still ActionKinds.Last (MessagesV17.cs).
