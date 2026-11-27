using System.Linq;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The FakeServer's belt hotbar (ui-overhaul.md section 10, slice 3A, DV693; on with FakeServerOptions.Inventory): the ten cells of the real server
// (1 to 7 a worn belt's contents, 8 and 9 the pockets, 10 the ID slot) and the five rules of a press, in the fake's simple model (a belt holds seven
// items that are no storage; a pocket takes any item that is no storage; the ID slot takes an ID card). It sends the Hotbar message the real server
// sends for the same state. The dev command "toolbelt" puts on a toolbelt of tg's seven tools, a pen in the left pocket and an ID card in the ID slot
// (those that are empty), for the client's tests and slice 3B's screenshots. It is a script, not tg: no do_after, no sounds, no stun.

public sealed partial class FakeServer
{
    private const int HotbarBeltCapacity = 7;
    private const string BeltPath = "/obj/item/storage/belt/utility/full", PenPath = "/obj/item/pen", CardPath = "/obj/item/card/id";

    // the fake's SlotDefs indices of the left pocket, the right pocket and the ID slot (cells 8, 9, 10)
    private static readonly int[] HotbarSlotIndices = { 6, 7, 2 };

    // tg's seven tools of /obj/item/storage/belt/utility/full (belt.dm:79), with their tg icons
    private static readonly (string Name, string Path, string Icon, string State)[] BeltTools =
    {
        ("screwdriver", "/obj/item/screwdriver", ToolsIcon, "screwdriver"),
        ("wrench", "/obj/item/wrench", ToolsIcon, "wrench"),
        ("welding tool", "/obj/item/weldingtool", ToolsIcon, "welder"),
        ("crowbar", "/obj/item/crowbar", ToolsIcon, "crowbar"),
        ("wirecutters", "/obj/item/wirecutters", ToolsIcon, "cutters"),
        ("multitool", "/obj/item/multitool", "icons/obj/devices/tool.dmi", "multitool"),
        ("cable coil", "/obj/item/stack/cable_coil", "icons/obj/stack_objects.dmi", "coil"),
    };

    private readonly Thing[] _hotbarCells = new Thing[HotbarMessage.CellCount];
    private readonly bool[] _hotbarTaken = new bool[HotbarMessage.CellCount];
    private Thing _hotbarBelt;
    private HotbarMessage _hotbarSent;
    private byte _hotbarRefused;

    /// <summary>The id of the item of hotbar cell <paramref name="cell"/> (1 to 10), in its container or out, 0 for an empty cell (the tests).</summary>
    public long HotbarItemId(int cell) => InventoryOn && cell is >= 1 and <= HotbarMessage.CellCount ? _hotbarCells[cell - 1]?.Id ?? 0 : 0;

    /// <summary>The ids of the worn belt's contents in their order, empty without a belt (the tests).</summary>
    public IReadOnlyList<long> BeltContentIds => _hotbarBelt == null ? Array.Empty<long>() : _hotbarBelt.Contents.Select(t => t.Id).ToList();

    // The kit's types exist from the join on: a tool dropped later is an entity of a type the client knows (a Delta adds no types).
    private void InitHotbar()
    {
        foreach (var tool in BeltTools)
            TypeOf(tool.Path);
        TypeOf(BeltPath);
        TypeOf(PenPath);
        TypeOf(CardPath);
    }

    private Thing NewThing(string name, string path, string icon, string state)
    {
        Thing t = new() { Id = ThingIdBase + _things.Count, Name = name, TypePath = path, TypeIndex = TypeOf(path), Icon = icon, State = state, Where = Where.Slot };
        _things.Add(t);
        return t;
    }

    // The dev command "toolbelt": the kit into the empty belt, left pocket and ID slots.
    private void GiveToolbelt(List<string> replies)
    {
        if (!InventoryOn)
        {
            replies.Add("The fake server's inventory is off (--fake-inventory).");
            return;
        }
        if (_slotItems[BeltSlot] == null)
        {
            Thing belt = NewThing("toolbelt", BeltPath, "icons/obj/clothing/belts.dmi", "utility");
            belt.IsStorage = true;
            foreach (var tool in BeltTools)
            {
                Thing t = NewThing(tool.Name, tool.Path, tool.Icon, tool.State);
                t.IsWelder = tool.Path == "/obj/item/weldingtool";
                t.Where = Where.Storage;
                t.Parent = belt;
                belt.Contents.Add(t);
            }
            _slotItems[BeltSlot] = belt;
        }
        if (_slotItems[HotbarSlotIndices[0]] == null)
            _slotItems[HotbarSlotIndices[0]] = NewThing("pen", PenPath, "icons/obj/service/bureaucracy.dmi", "pen");
        if (_slotItems[HotbarSlotIndices[2]] == null)
            _slotItems[HotbarSlotIndices[2]] = NewThing("identification card", CardPath, "icons/obj/card.dmi", "card_grey");
        replies.Add("You put on a toolbelt of seven tools; a pen and an ID card where there was room.");
    }

    // The real server's rules (BeltHotbar.Reconcile): a new belt empties the seven and its contents take the free cells in order; an item keeps its
    // cell in its container or, taken from the bar, in a hand; a slot's item is its cell's.
    private void HotbarReconcile()
    {
        Thing worn = _slotItems[BeltSlot];
        Thing belt = worn is { IsStorage: true } ? worn : null;
        if (belt != _hotbarBelt)
        {
            for (int i = 0; i < HotbarMessage.BeltCells; i++)
                HotbarClear(i);
            _hotbarBelt = belt;
        }
        for (int i = 0; i < HotbarMessage.BeltCells; i++)
        {
            Thing item = _hotbarCells[i];
            if (item == null)
                continue;
            if (item.Parent == belt && belt != null)
                _hotbarTaken[i] = false;
            else if (!_hotbarTaken[i] || !IsHeld(item))
                HotbarClear(i);
        }
        if (belt != null)
        {
            foreach (Thing item in belt.Contents)
            {
                if (Array.IndexOf(_hotbarCells, item, 0, HotbarMessage.BeltCells) >= 0)
                    continue;
                int free = Array.IndexOf(_hotbarCells, null, 0, HotbarMessage.BeltCells);
                if (free < 0)
                    break;
                _hotbarCells[free] = item;
                _hotbarTaken[free] = false;
            }
        }
        for (int i = HotbarMessage.BeltCells; i < HotbarMessage.CellCount; i++)
        {
            Thing slotted = _slotItems[HotbarSlotIndices[i - HotbarMessage.BeltCells]];
            if (slotted != null)
            {
                _hotbarCells[i] = slotted;
                _hotbarTaken[i] = false;
            }
            else if (_hotbarCells[i] != null && (!_hotbarTaken[i] || !IsHeld(_hotbarCells[i])))
                HotbarClear(i);
        }
    }

    private void HotbarClear(int i)
    {
        _hotbarCells[i] = null;
        _hotbarTaken[i] = false;
    }

    private bool IsHeld(Thing item) => _hands[0] == item || _hands[1] == item;

    private HotbarCellState HotbarStateOf(int i)
    {
        Thing item = _hotbarCells[i];
        if (item == null)
            return HotbarCellState.Empty;
        bool inside = i < HotbarMessage.BeltCells ? item.Parent == _hotbarBelt && _hotbarBelt != null : _slotItems[HotbarSlotIndices[i - HotbarMessage.BeltCells]] == item;
        return inside ? HotbarCellState.Holding : HotbarCellState.Out;
    }

    // The five rules of a press (BeltHotbar.Apply), refused when none applies or the move is not allowed.
    private void HotbarPress(int cell)
    {
        if (cell is < 1 or > HotbarMessage.CellCount)
            return;
        HotbarReconcile();
        int i = cell - 1;
        Thing held = _hands[_activeHand];
        HotbarCellState state = HotbarStateOf(i);
        bool done;
        if (state == HotbarCellState.Out)
            done = HotbarInsert(i, _hotbarCells[i]);
        else if (state == HotbarCellState.Holding && held == null)
            done = HotbarTakeOut(i);
        else if (state == HotbarCellState.Holding && Enumerable.Range(0, HotbarMessage.CellCount).FirstOrDefault(m => m != i && _hotbarCells[m] == held && HotbarStateOf(m) == HotbarCellState.Out, -1) is int from and >= 0)
            done = HotbarInsert(from, held) && (HotbarTakeOut(i) || true);
        else if (state == HotbarCellState.Empty && held != null)
            done = HotbarInsert(i, held);
        else
            done = false;
        if (!done)
            _hotbarRefused = (byte)cell;
    }

    private bool HotbarInsert(int i, Thing item)
    {
        if (item == null || item.IsStorage)
            return false;
        if (i < HotbarMessage.BeltCells)
        {
            if (_hotbarBelt == null || _hotbarBelt.Contents.Count >= HotbarBeltCapacity)
                return false;
            LeaveHands(item);
            item.Where = Where.Storage;
            item.Parent = _hotbarBelt;
            _hotbarBelt.Contents.Add(item);
        }
        else
        {
            int slot = HotbarSlotIndices[i - HotbarMessage.BeltCells];
            if (_slotItems[slot] != null || (i == HotbarMessage.CellCount - 1 && item.TypePath != CardPath))
                return false;
            LeaveHands(item);
            item.Where = Where.Slot;
            _slotItems[slot] = item;
        }
        for (int c = 0; c < HotbarMessage.CellCount; c++)
        {
            if (_hotbarCells[c] == item)
                HotbarClear(c);
        }
        _hotbarCells[i] = item;
        return true;
    }

    private bool HotbarTakeOut(int i)
    {
        Thing item = _hotbarCells[i];
        if (item == null || _hands[_activeHand] != null)
            return false;
        if (i < HotbarMessage.BeltCells)
            _hotbarBelt.Contents.Remove(item);
        else
            _slotItems[HotbarSlotIndices[i - HotbarMessage.BeltCells]] = null;
        item.Parent = null;
        item.Where = Where.Hand;
        _hands[_activeHand] = item;
        _hotbarTaken[i] = true;
        return true;
    }

    private void LeaveHands(Thing item)
    {
        for (int h = 0; h < 2; h++)
        {
            if (_hands[h] == item)
                _hands[h] = null;
        }
    }

    // After the tick's commands, before its Delta: the Hotbar message when it changed or a press was refused (its pictures' rows go out in that
    // Delta, the message right after it, as the real server's).
    private void BuildHotbar()
    {
        HotbarReconcile();
        var cells = new HotbarCell[HotbarMessage.CellCount];
        for (int i = 0; i < cells.Length; i++)
        {
            HotbarCellState state = HotbarStateOf(i);
            Thing item = _hotbarCells[i];
            cells[i] = state == HotbarCellState.Empty ? HotbarCell.Empty : new HotbarCell(state, Leaf(item.Icon, ShownState(item), 255, 20f), item.Id);
        }
        var now = new HotbarMessage
        {
            Bar = true,
            BeltWorn = _hotbarBelt != null,
            Cells = cells,
            LeftHand = _hands[0]?.Name,
            RightHand = _hands[1]?.Name,
        };
        byte refused = _hotbarRefused;
        _hotbarRefused = 0;
        if (refused == 0 && now.Equals(_hotbarSent))
            return;
        _hotbarSent = now;
        _outbox.Add(Codec.Encode(refused == 0 ? now : new HotbarMessage
        {
            Bar = true, BeltWorn = now.BeltWorn, Cells = cells, LeftHand = now.LeftHand, RightHand = now.RightHand, RefusedCell = refused,
        }));
    }
}
