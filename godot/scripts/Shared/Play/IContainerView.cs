using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: the M4a contracts step (frozen interface: docs/ai/specs/m4a-ownership.md); M4a P7 implements it (the container window
// and the inventory panel, D131). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// docs/ai/specs/interaction.md section 5: the server stays 100 % tg; the windows only re-lay out tg's own screen entries.

/// <summary>
/// One cell of a native window laid out from tg's screen entries (interaction.md 5): the entry it shows, where it sits in
/// the window's grid, and its D70 label.
/// </summary>
/// <param name="EntryId">The screen entry's id: a click on the cell is a <see cref="TargetKind.Screen"/> click on it.</param>
/// <param name="Column">The cell's column, from 0 at the left, in tg's order (from tg's <c>screen_loc</c> of the entry: <c>orient_storage</c>'s layout for storage, the slot's position for the inventory).</param>
/// <param name="Row">The cell's row, from 0 at the top.</param>
/// <param name="Role">What the entry is (<see cref="ContainerCellRole"/>).</param>
/// <param name="Label">The D70 text label: the item's or the slot's name (from the replica's tables); never empty for an item.</param>
public readonly record struct ContainerCell(long EntryId, int Column, int Row, ContainerCellRole Role, string Label);

/// <summary>What a laid-out screen entry is.</summary>
public enum ContainerCellRole : byte
{
    /// <summary>A storage cell or an inventory slot (tg's <c>/atom/movable/screen/storage/cell</c>, <c>/atom/movable/screen/inventory</c>): a click inserts or equips the held item.</summary>
    Slot = 0,

    /// <summary>An item tg shows in the storage (its own appearance, <c>show_contents</c>): a click takes it into the active hand.</summary>
    Item = 1,

    /// <summary>tg's close button (<c>/atom/movable/screen/close</c>).</summary>
    Close = 2,

    /// <summary>Any other entry of the group (a storage's background).</summary>
    Other = 3,
}

/// <summary>
/// A native window over one HUD group's screen entries (D131): <see cref="HudGroup.Storage"/> for the open container window (tg's
/// toggled inventory is the Tab inventory's character panel since ui-overhaul.md slice 4A, <see cref="TabInventory"/>). Rebuilt whenever the group's entries
/// change; it never sends anything itself (the window's clicks are screen clicks on <see cref="ContainerCell.EntryId"/>).
/// </summary>
public interface IContainerView
{
    /// <summary>The HUD group the view lays out.</summary>
    byte Group { get; }

    /// <summary>Whether the group has any entry now (an open storage; the inventory toggled on).</summary>
    bool IsOpen { get; }

    /// <summary>The cells in reading order (rows top to bottom, columns left to right).</summary>
    IReadOnlyList<ContainerCell> Cells { get; }

    /// <summary>The grid's size in cells.</summary>
    int Columns { get; }

    /// <summary>The grid's size in cells.</summary>
    int Rows { get; }

    /// <summary>
    /// Lays out the group's entries (<see cref="ReplicaWorld.ScreenGroup"/>) from their tg <c>screen_loc</c>s and appearances
    /// (<paramref name="world"/>'s tables give the names and pictures). Pure; the same entries give the same cells.
    /// </summary>
    void Rebuild(IReadOnlyList<ScreenEntry> entries, ReplicaWorld world);
}
