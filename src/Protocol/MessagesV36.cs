namespace Anomaly.Protocol;

// Version 36 (M8y slice D, D230, docs/ai/specs/protocol.md section 45): a context hint names what its radial entry does. tg's screentip
// entries are clicks; the port's Buckle (a drop of oneself onto the target, tg's drag) and Stop pulling (tg's stop-pulling keybinding)
// are not, so the hint carries its action.

/// <summary>Version 36: what choosing a <see cref="ContextHint"/> in the radial sends.</summary>
public enum ContextHintAction : byte
{
    /// <summary>Its click on the target (<see cref="ContextHint.Button"/> with <see cref="ContextHint.Modifiers"/>): every tg entry.</summary>
    Click = 0,

    /// <summary>A Drop of the own mob onto the target (tg's drag of oneself onto a chair or a bed: <c>user_buckle_mob</c>).</summary>
    DropSelf = 1,

    /// <summary>tg's stop-pulling keybinding (<see cref="KeybindingCode.StopPulling"/>).</summary>
    StopPulling = 2,

    /// <summary>
    /// Version 37 (D238, MessagesV37.cs): a Drop of the hinted target onto the own mob (tg's drag of a mob onto oneself: the strip menu).
    /// </summary>
    DropOntoSelf = 3,

    /// <summary>
    /// Version 86 (DV503): a port action of the radial that is no click (the table's Flip): choosing it sends a <see cref="ActionKind.ContextPick"/>
    /// of the target with the hint's label.
    /// </summary>
    Extra = 4,

    /// <summary>The highest value.</summary>
    Last = Extra,
}
