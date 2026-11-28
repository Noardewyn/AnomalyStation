using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// ui-overhaul.md section 10, slice 3A (DV693, protocol version 100's Hotbar): the client's copy of the own hotbar, the ten cells (1 to 7 the belt,
/// 8 and 9 the pockets, 10 the ID slot) as the server last sent them, the names of the items in the own hands, and the last refused press with
/// when it came (for slice 3B's blink). Nothing is predicted: a press is sent and the server's next message says what it did.
/// </summary>
public sealed class HotbarModel
{
    private int _refusalsSeen;

    /// <summary>The own mob has the bar (a human, tg's human HUD).</summary>
    public bool Bar { get; private set; }

    /// <summary>A belt with a storage is worn (else the seven belt cells are empty).</summary>
    public bool BeltWorn { get; private set; }

    /// <summary>The ten cells, cell 1 first.</summary>
    public IReadOnlyList<HotbarCell> Cells { get; private set; } = HotbarMessage.None.Cells;

    /// <summary>The name of the item in the left hand, null when it is empty.</summary>
    public string LeftHand { get; private set; }

    /// <summary>The name of the item in the right hand, null when it is empty.</summary>
    public string RightHand { get; private set; }

    /// <summary>The cell (1 to 10) of the last refused press, 0 before the first.</summary>
    public int RefusedCell { get; private set; }

    /// <summary>When the last refusal came, in the clock of <see cref="Apply"/>; negative infinity before the first.</summary>
    public double RefusedAt { get; private set; } = double.NegativeInfinity;

    /// <summary>What cell <paramref name="cell"/> (1 to 10) shows.</summary>
    public HotbarCellState StateOf(int cell) => cell is >= 1 and <= HotbarMessage.CellCount ? Cells[cell - 1].State : HotbarCellState.Empty;

    /// <summary>
    /// The feed's state at <paramref name="now"/>: <paramref name="message"/> (the last Hotbar), and the refusals so far (<paramref name="refusals"/>,
    /// the last one's <paramref name="refusedCell"/>): a new count is a new refusal, dated now.
    /// </summary>
    public void Apply(HotbarMessage message, int refusals, int refusedCell, double now)
    {
        message ??= HotbarMessage.None;
        Bar = message.Bar;
        BeltWorn = message.BeltWorn;
        Cells = message.Cells;
        LeftHand = message.LeftHand;
        RightHand = message.RightHand;
        if (refusals != _refusalsSeen)
        {
            if (refusals > _refusalsSeen && refusedCell is >= 1 and <= HotbarMessage.CellCount)
            {
                RefusedCell = refusedCell;
                RefusedAt = now;
            }
            _refusalsSeen = refusals; // a feed that started again (a new join) counts from 0
        }
    }

    /// <summary>The cell (1 to 10) a hotbar key presses, 0 for any other action.</summary>
    public static int CellOf(PlayAction action) => action is >= PlayAction.Hotbar1 and <= PlayAction.Hotbar10 ? action - PlayAction.Hotbar1 + 1 : 0;
}
