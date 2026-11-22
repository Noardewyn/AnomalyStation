namespace Anomaly.Protocol;

// Protocol version 89 (DV179; docs/ai/specs/protocol.md section 86). In the private block's version 43 flags byte:
// - bit 6 (64): the own mob's hands are blocked (no bytes of its own): tg's ClickOn stops a click before it reaches what was clicked
//   (incapacitated: stunned, paralyzed, asleep, in crit; or TRAIT_HANDS_BLOCKED: cuffed, holding nothing it can use), so the client
//   guesses no pick-up, no item taken to a hand and no door opened by a click.

public sealed partial class OwnBlock
{
    /// <summary>Version 89: whether tg's ClickOn refuses the own mob's clicks now (incapacitated or <c>TRAIT_HANDS_BLOCKED</c>).</summary>
    public bool HandsBlocked { get; set; }
}
