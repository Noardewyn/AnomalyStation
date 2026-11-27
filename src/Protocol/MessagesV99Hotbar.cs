namespace Anomaly.Protocol;

// Protocol version 99 also (ui-overhaul.md section 10, package 3, slice 3A, DV689; docs/ai/specs/protocol.md section 97; the codec: Codec.V99Hotbar.cs).
// - Hotbar (server to client, State channel, after the tick's Delta, when it changed or a press was refused): the own human's ten hotbar cells
//   (1 to 7 the worn belt's contents, 8 and 9 the left and right pocket, 10 the ID slot), each empty, holding its item or out (its item in a hand,
//   taken from the cell), with the item's picture as the HUD draws a slot's item; whether a belt is worn; the names of the items in the own hands.
// - HotbarPress (client to server, an Action of kind 28): a press of one cell (ActionMessage.HotbarCell, MessagesV2.cs).

/// <summary>Version 99: what a hotbar cell shows.</summary>
public enum HotbarCellState : byte
{
    /// <summary>No item in the cell and none out from it.</summary>
    Empty = 0,

    /// <summary>The cell's item is in the belt, the pocket or the ID slot.</summary>
    Holding = 1,

    /// <summary>The cell's item is in one of the player's hands, taken from this cell (it goes back here).</summary>
    Out = 2,
}

/// <summary>Version 99: one hotbar cell: its state and its item's picture (the item drawn alone, as a slot's <see cref="ScreenChild"/>; none for an empty cell, or an item that shows nothing).</summary>
public readonly record struct HotbarCell(HotbarCellState State, TableRef Appearance)
{
    /// <summary>An empty cell.</summary>
    public static readonly HotbarCell Empty = new(HotbarCellState.Empty, TableRef.None);
}

/// <summary>
/// Version 99: the own hotbar (DV689). Only a human has one (<see cref="Bar"/>, tg's human HUD); every other mob's message is <see cref="None"/>.
/// <see cref="RefusedCell"/> is an event, not state: the press of that cell (1 to 10) did nothing this tick (0: none).
/// </summary>
public sealed class HotbarMessage : IEquatable<HotbarMessage>
{
    /// <summary>The cells: 1 to 7 the belt, 8 and 9 the pockets, 10 the ID slot.</summary>
    public const int CellCount = 10;

    /// <summary>The belt's cells.</summary>
    public const int BeltCells = 7;

    /// <summary>No bar (a mob without the human HUD) and nothing refused: what a client starts from.</summary>
    public static readonly HotbarMessage None = new();

    private readonly HotbarCell[] _cells = new HotbarCell[CellCount];

    /// <summary>The own mob is a human: it has the bar (else every cell is empty, no belt, no names).</summary>
    public bool Bar { get; init; }

    /// <summary>A belt with a storage is worn (else the seven belt cells are empty).</summary>
    public bool BeltWorn { get; init; }

    /// <summary>The ten cells, cell 1 first.</summary>
    public IReadOnlyList<HotbarCell> Cells
    {
        get => _cells;
        init
        {
            if (value == null || value.Count != CellCount)
                throw new ArgumentException("a hotbar has " + CellCount + " cells", nameof(value));
            for (int i = 0; i < CellCount; i++)
                _cells[i] = value[i];
        }
    }

    /// <summary>The name of the item in the left hand (tg's held index 1), null when it is empty.</summary>
    public string LeftHand { get; init; }

    /// <summary>The name of the item in the right hand, null when it is empty.</summary>
    public string RightHand { get; init; }

    /// <summary>The cell (1 to 10) whose press was refused this tick, 0 for none.</summary>
    public byte RefusedCell { get; init; }

    /// <summary>The same state with no refusal (what the server compares to decide whether the state changed).</summary>
    public HotbarMessage WithoutRefusal() => RefusedCell == 0 ? this : new HotbarMessage
    {
        Bar = Bar, BeltWorn = BeltWorn, Cells = _cells, LeftHand = LeftHand, RightHand = RightHand,
    };

    public bool Equals(HotbarMessage other)
    {
        if (other is null || Bar != other.Bar || BeltWorn != other.BeltWorn || RefusedCell != other.RefusedCell
            || !string.Equals(LeftHand, other.LeftHand, StringComparison.Ordinal) || !string.Equals(RightHand, other.RightHand, StringComparison.Ordinal))
            return false;
        for (int i = 0; i < CellCount; i++)
        {
            if (_cells[i] != other._cells[i])
                return false;
        }
        return true;
    }

    public override bool Equals(object obj) => Equals(obj as HotbarMessage);

    public override int GetHashCode() => HashCode.Combine(Bar, BeltWorn, RefusedCell, _cells[0], _cells[CellCount - 1], LeftHand, RightHand);

    public override string ToString()
        => $"Hotbar(bar {Bar}, belt {BeltWorn}, [{string.Join(", ", Array.ConvertAll(_cells, c => c.State == HotbarCellState.Empty ? "-" : c.State + ":" + c.Appearance))}], left {LeftHand}, right {RightHand}, refused {RefusedCell})";
}
