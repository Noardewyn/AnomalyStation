namespace Anomaly.Protocol;

// Version 37 (M8z, D243): what the M8z slices add. Slice C (climbing, D239): a MapGrid cell's climb-walkable bit, and the own capsule's
// OnClimbable flag in the private block's motion (Codec.V2.cs, the packed byte's bit 128), so the client predicts a climber walking from
// table to table as the server moves him.
// Slice B (the strip menu, D238): the radial's Strip is a hint whose action is
// ContextHintAction.DropOntoSelf (MessagesV36.cs, value 3): choosing it sends a Drop of the hinted mob onto the own mob, which is tg's
// drag of a mob onto oneself and opens its strip window (a long drag, never a quick drag as a click). The hint's wire layout is
// version 36's; only the action byte's range grows (0..3).

public static partial class CellFlags
{
    /// <summary>
    /// Version 37 (D239): the cell is solid only through tg's <c>climb_walkable</c> structures (a table, a closed crate:
    /// <c>Anomaly.Core.Tg.LocomotionSeam.IsClimbWalkable</c>): a capsule with <c>TRAIT_ON_CLIMBABLE</c> walks through it, and a standing
    /// mob on it is drawn on the structure's top.
    /// </summary>
    public const byte ClimbWalkable = 64;
}
