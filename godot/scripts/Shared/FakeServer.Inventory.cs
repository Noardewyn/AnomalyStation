using System.Globalization;
using System.Linq;
using Anomaly.Core.Motion;
using Anomaly.Net;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M4a P7 (client interaction). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The FakeServer's version 4 (docs/ai/specs/interaction.md 8, FakeServerOptions.Inventory): tg's hands, slots and a toolbox, with the screen
// entries the real server sends for the same state (tg's screen_locs, the HUD group byte), the Thrown flag, the private progress-bar image
// and the ContextHints of a hover. It is a script, not tg: the clicks it understands are the ones the doc comment of the option lists.

public sealed partial class FakeServer
{
    private const long ThingIdBase = 400000;
    private const long HandLeftId = 2001, HandRightId = 2002, SwapId = 2003, DropId = 2004, ThrowId = 2005;
    private const long SlotIdBase = 2010, StorageCellsId = 2040, StorageCloseId = 2048, ProgressImageId = 3001;
    private const string HudIcon = "icons/hud/screen_midnight.dmi";
    private const string ToolsIcon = "icons/obj/tools.dmi";
    private const string ProgressIcon = "icons/effects/progressbar.dmi";
    private const int WeldTicks = 40;

    // tg's slot icon states, screen_locs and slot_ids (_onclick/hud/human.dm, __DEFINES/hud.dm, __DEFINES/inventory.dm), the order the human HUD
    // builds them in; Toggled: in HUD_GROUP_TOGGLEABLE_INVENTORY (the worn slots the inventory toggle shows), else in HUD_GROUP_STATIC (the bottom row).
    private static readonly (string State, string Name, string Loc, uint SlotId, bool Toggled)[] SlotDefs =
    {
        ("uniform", "uniform", "WEST:6,SOUTH+1:7", 1u << 1, true),
        ("suit", "suit", "WEST+1:8,SOUTH+1:7", 1u << 0, true),
        ("id", "id", "CENTER-4:12,SOUTH:5", 1u << 8, false),
        ("mask", "mask", "WEST+1:8,SOUTH+2:9", 1u << 5, true),
        ("neck", "neck", "WEST:6,SOUTH+2:9", 1u << 12, true),
        ("back", "back", "CENTER-2:14,SOUTH:5", 1u << 10, false),
        ("pocket", "left pocket", "CENTER+1:18,SOUTH:5", 1u << 15, false),
        ("pocket", "right pocket", "CENTER+2:20,SOUTH:5", 1u << 16, false),
        ("suit_storage", "suit storage", "CENTER-5:10,SOUTH:5", 1u << 14, false),
        ("gloves", "gloves", "WEST+2:10,SOUTH+1:7", 1u << 2, true),
        ("glasses", "eyes", "WEST:6,SOUTH+3:11", 1u << 3, true),
        ("ears", "ears", "WEST+2:10,SOUTH+2:9", 1u << 4, true),
        ("head", "head", "WEST+1:8,SOUTH+3:11", 1u << 6, true),
        ("shoes", "shoes", "WEST+1:8,SOUTH:5", 1u << 7, true),
        ("belt", "belt", "CENTER-3:14,SOUTH:5", 1u << 9, false),
    };

    /// <summary>tg's inventory toggle (screen/human/toggle at ui_inventory), which only the human HUD has.</summary>
    public const long InventoryToggleId = 2006;

    private const int BeltSlot = 14;

    private enum Where : byte { Floor, Hand, Slot, Storage }

    private sealed class Thing
    {
        public long Id;
        public string Name;
        public string TypePath;
        public int TypeIndex;
        public string Icon, State;
        public bool IsStorage, IsDoor, IsWelder, Lit, Welded;
        public readonly List<Thing> Contents = new();
        public Where Where;
        public int X, Y, Z;
        public int PixelX, PixelY;
        public long LastMoveTick;
        public bool Thrown;
        public List<(int X, int Y)> Flight;
        public bool SentOnFloor;
        public EntityState Sent;
        public Thing Parent;
    }

    private readonly List<Thing> _things = new();
    private readonly Thing[] _hands = new Thing[2];
    private readonly Thing[] _slotItems = new Thing[15];
    private int _activeHand;
    private bool _throwMode, _throwHeld;
    private Thing _openStorage;
    private readonly List<byte[]> _outbox = new();
    private readonly List<ImageEntry> _imagesAdded = new(), _imagesChanged = new();
    private readonly List<long> _imagesRemoved = new();
    private Thing _weldTarget;
    private int _weldTicks;
    private int _weldPercentShown = -1;
    private readonly Dictionary<string, TableRef> _thingApps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TableRef> _invApps = new(StringComparer.Ordinal);
    private bool _imageLive;

    /// <summary>Whether the version-4 inventory script is on.</summary>
    private bool InventoryOn => _options.Inventory;

    /// <summary>The thing with this world id (the tests ask where an item is).</summary>
    private Thing ThingById(long id) => _things.Find(t => t.Id == id);

    /// <summary>The world ids of the toolbox and its tools, the welder and the airlock (for the tests and the shot scripts).</summary>
    public long ToolboxId => InventoryOn ? _things.Find(t => t.TypePath.Contains("toolbox", StringComparison.Ordinal))?.Id ?? 0 : 0;

    private long _looseWelderId;

    /// <summary>The loose welding tool lying beside the toolbox (the toolbox holds another).</summary>
    public long WelderId => InventoryOn ? _looseWelderId : 0;

    public long AirlockId => InventoryOn ? _things.Find(t => t.IsDoor)?.Id ?? 0 : 0;

    /// <summary>The id of the item in a hand (0: left, 1: right), 0 when empty.</summary>
    public long HeldId(int hand) => InventoryOn && hand is 0 or 1 ? _hands[hand]?.Id ?? 0 : 0;

    /// <summary>The active hand (0: left, 1: right).</summary>
    public int ActiveHand => _activeHand;

    /// <summary>True while throw mode is on.</summary>
    public bool ThrowMode => _throwMode;

    /// <summary>The id of the open storage's item, 0 when none is open.</summary>
    public long OpenStorageId => _openStorage?.Id ?? 0;

    /// <summary>Whether the airlock is welded shut.</summary>
    public bool AirlockWelded => _things.Find(t => t.IsDoor)?.Welded ?? false;

    /// <summary>The tile an item lies on (0, 0 when it is not on a floor).</summary>
    public (int X, int Y) TileOfThing(long id) => ThingById(id) is { Where: Where.Floor } t ? (t.X, t.Y) : (0, 0);

    /// <summary>True while the item is in flight.</summary>
    public bool IsThrown(long id) => ThingById(id)?.Thrown ?? false;

    private int TypeOf(string path)
    {
        int index = _types.IndexOf(path);
        if (index >= 0)
            return index;
        _types.Add(path);
        return _types.Count - 1;
    }

    private void InitInventory()
    {
        Mob player = _mobs[0];
        var cells = NearbyOpenCells(player, 8);
        int next = 0;
        Thing Make(string name, string path, string icon, string state, Action<Thing> tweak = null)
        {
            Thing t = new() { Id = ThingIdBase + _things.Count, Name = name, TypePath = path, TypeIndex = TypeOf(path), Icon = icon, State = state };
            tweak?.Invoke(t);
            _things.Add(t);
            return t;
        }
        Thing toolbox = Make("mechanical toolbox", "/obj/item/storage/toolbox/mechanical", "icons/obj/storage/toolbox.dmi", "toolbox_default", t => t.IsStorage = true);
        string[] tools = { "screwdriver", "wrench", "welding tool", "crowbar", "wirecutters" };
        string[] toolPaths = { "/obj/item/screwdriver", "/obj/item/wrench", "/obj/item/weldingtool", "/obj/item/crowbar", "/obj/item/wirecutters" };
        string[] toolStates = { "screwdriver", "wrench", "welder", "crowbar", "cutters" };
        for (int i = 0; i < tools.Length; i++)
        {
            int index = i;
            Thing tool = Make(tools[i], toolPaths[i], ToolsIcon, toolStates[i], t => t.IsWelder = index == 2);
            tool.Where = Where.Storage;
            tool.Parent = toolbox;
            toolbox.Contents.Add(tool);
        }
        (int X, int Y) at = cells[next++ % cells.Count];
        Place(toolbox, at);
        // A loose welder beside it, to light without opening the box, and the airlock on the next cell.
        Thing welder = Make("welding tool", "/obj/item/weldingtool", ToolsIcon, "welder", t => t.IsWelder = true);
        Place(welder, cells[next++ % cells.Count]);
        _looseWelderId = welder.Id;
        Thing airlock = Make("public airlock", "/obj/machinery/door/airlock/public", "icons/obj/doors/airlocks/station/public.dmi", "closed", t => t.IsDoor = true);
        Place(airlock, cells[next++ % cells.Count]);
        foreach (Thing t in _things)
        {
            if (t.Where == Where.Floor)
            {
                t.Sent = WireOf(t);
                t.SentOnFloor = true;
            }
        }
    }

    private void Place(Thing t, (int X, int Y) at)
    {
        t.Where = Where.Floor;
        t.X = at.X;
        t.Y = at.Y;
        t.Z = _mobs[0].Motion.Z;
        t.Parent = null;
    }

    // Open cells around the player (rings of growing radius), nearest first, away from the player's own tile.
    private List<(int X, int Y)> NearbyOpenCells(Mob player, int count)
    {
        var cells = new List<(int X, int Y)>();
        int px = player.Motion.TileX, py = player.Motion.TileY, z = player.Motion.Z;
        for (int r = 1; r <= 12 && cells.Count < count; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || dy < 0 && r > 2)
                        continue; // the cells in front (north) of the spawn first
                    int x = px + dx, y = py + dy;
                    if (x < 1 || y < 1 || x > _width || y > _height || _view.IsSolid(x, y, z) || cells.Contains((x, y)))
                        continue;
                    cells.Add((x, y));
                }
            }
        }
        if (cells.Count == 0)
            cells.Add((px, py));
        return cells;
    }

    // ---- the entities ----

    private TableRef PubResource(string path)
    {
        int index = _pubResources.IndexOf(path);
        if (index < 0)
        {
            _pubResources.Add(path);
            index = _pubResources.Count - 1;
        }
        return TableRef.Public((uint)(index + 1));
    }

    private TableRef PubString(string text)
    {
        int index = _pubStrings.IndexOf(text);
        if (index < 0)
        {
            _pubStrings.Add(text);
            index = _pubStrings.Count - 1;
        }
        return TableRef.Public((uint)(index + 1));
    }

    private TableRef AppearanceOf(Thing t)
    {
        string state = t.IsWelder && t.Lit ? "welder-on" : t.State;
        string key = string.Join('|', t.Icon, state, t.PixelX.ToString(CultureInfo.InvariantCulture), t.PixelY.ToString(CultureInfo.InvariantCulture), t.Welded ? "w" : "");
        if (_thingApps.TryGetValue(key, out TableRef known))
            return known;
        var entry = new AppearanceEntry { Icon = PubResource(t.Icon), IconState = PubString(state), Layer = t.IsDoor ? 3.5f : 3f, PixelX = (short)t.PixelX, PixelY = (short)t.PixelY };
        if (t.Welded)
            entry.Overlays.Add(AddPublic(new AppearanceEntry { Icon = PubResource("icons/obj/doors/airlocks/station/overlays.dmi"), IconState = PubString("welded"), Layer = -1f }));
        TableRef made = AddPublic(entry);
        _thingApps[key] = made;
        return made;
    }

    private EntityState WireOf(Thing t) => new(t.Id, t.TypeIndex, t.X, t.Y, t.Z, ByondDirSouth, t.Name, 0f, t.LastMoveTick)
    {
        HasPos = false,
        PosX = t.X * MotionMath.SubunitsPerTile,
        PosY = t.Y * MotionMath.SubunitsPerTile,
        Appearance = AppearanceOf(t),
        Flags = t.Thrown ? EntityFlags.Thrown : EntityFlags.None,
    };

    private const byte ByondDirSouth = 2;

    // The inventory's part of a Delta: things that appeared on the floor, left it, or changed there.
    private void InventoryDelta(Delta delta)
    {
        foreach (Thing t in _things.OrderBy(t => t.Id))
        {
            bool onFloor = t.Where == Where.Floor;
            if (onFloor && !t.SentOnFloor)
            {
                t.Sent = WireOf(t);
                delta.Spawned.Add(t.Sent);
            }
            else if (!onFloor && t.SentOnFloor)
                delta.Despawned.Add(t.Id);
            else if (onFloor)
            {
                EntityState now = WireOf(t);
                if (!now.Equals(t.Sent))
                {
                    delta.Changed.Add(Diff(t.Sent, now));
                    t.Sent = now;
                }
            }
            t.SentOnFloor = onFloor;
        }
    }

    private void InventorySnapshot(Snapshot snapshot)
    {
        foreach (Thing t in _things.Where(t => t.Where == Where.Floor).OrderBy(t => t.Id))
        {
            t.Sent = WireOf(t);
            t.SentOnFloor = true;
            snapshot.Entities.Add(t.Sent);
        }
    }

    // ---- the private block's images ----

    private void InventoryOwnImages(OwnBlock block, bool snapshot)
    {
        if (snapshot)
        {
            _imagesAdded.Clear();
            _imagesChanged.Clear();
            _imagesRemoved.Clear();
            if (_imageLive && _weldTarget != null)
                block.ImagesAdded.Add(new ImageEntry(ProgressImageId, ProgressApp(_weldPercentShown), _weldTarget.Id) { Holder = _weldTarget.Id, HolderName = _weldTarget.Name });
            return;
        }
        block.ImagesAdded.AddRange(_imagesAdded);
        block.ImagesChanged.AddRange(_imagesChanged);
        block.ImagesRemoved.AddRange(_imagesRemoved);
        _imagesAdded.Clear();
        _imagesChanged.Clear();
        _imagesRemoved.Clear();
    }

    private TableRef ProgressApp(int percent)
        => PrivateLeaf(ProgressIcon, "prog_bar_" + Math.Max(0, percent).ToString(CultureInfo.InvariantCulture), 255, 21f);

    private TableRef PrivateLeaf(string icon, string state, byte alpha, float layer) => Leaf(icon, state, alpha, layer);

    // ---- the HUD ----

    private void BuildInventoryHud()
    {
        // tg's held_index: odd is a left hand (IS_LEFT_INDEX)
        _screen[HandLeftId] = new ScreenEntry(HandLeftId, HandApp(0), "CENTER+-1:16,SOUTH+0:5") { Group = HudGroup.Static, VisChildren = ShownChild(_hands[0]), HandIndex = 1 };
        _screen[HandRightId] = new ScreenEntry(HandRightId, HandApp(1), "CENTER+0:16,SOUTH+0:5") { Group = HudGroup.Static, VisChildren = ShownChild(_hands[1]), HandIndex = 2 };
        _screen[InventoryToggleId] = new ScreenEntry(InventoryToggleId, Leaf(HudIcon, "toggle", 255, 20f), "WEST:6,SOUTH:5") { Group = HudGroup.Static };
        _screen[DropId] = new ScreenEntry(DropId, Leaf(HudIcon, "act_drop", 255, 20f), "CENTER-1:16,SOUTH+1:5") { Group = HudGroup.Static };
        _screen[SwapId] = new ScreenEntry(SwapId, Leaf(HudIcon, "act_swap", 255, 20f), "CENTER:16,SOUTH+1:5") { Group = HudGroup.Static };
        _screen[ThrowId] = new ScreenEntry(ThrowId, Leaf(HudIcon, _throwMode ? "act_throw_on" : "act_throw", 255, 20f), "EAST-1:28,SOUTH+1:24") { Group = HudGroup.Hotkeys };
        for (int i = 0; i < SlotDefs.Length; i++)
            _screen[SlotIdBase + i] = new ScreenEntry(SlotIdBase + i, SlotApp(i), SlotDefs[i].Loc)
            {
                Group = SlotDefs[i].Toggled ? HudGroup.ToggleableInventory : HudGroup.Static, VisChildren = ShownChild(_slotItems[i]), SlotId = SlotDefs[i].SlotId,
            };
        // An open storage: tg's storage_interface (cells, corners, row joins, up and close buttons) and the items, at orient_storage's screen_locs.
        for (long id = StorageCellsId; id <= StorageCloseId; id++)
            _screen.Remove(id);
        foreach (long id in _storageItemIds)
            _screen.Remove(id);
        _storageItemIds.Clear();
        if (_openStorage != null)
        {
            Thing box = _openStorage;
            int count = box.Contents.Count;
            int columns = 7;
            int rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns) + (count % columns == 0 && count < 7 ? 1 : 0));
            int startPx = 4 * 32 + 16, startPy = 2 * 32 + 16;
            int endPx = startPx + (columns - 1) * 32, endPy = startPy + (rows - 1) * 32;
            string Span(int x0, int y0, int x1, int y1)
            {
                int tx0 = Round(x0 / 32.0), ty0 = Round(y0 / 32.0), tx1 = Round(x1 / 32.0), ty1 = Round(y1 / 32.0);
                return $"{tx0}:{x0 - tx0 * 32},{ty0}:{y0 - ty0 * 32} to {tx1}:{x1 - tx1 * 32},{ty1}:{y1 - ty1 * 32}";
            }
            AddStorage(StorageCellsId, "storage_cell", Span(startPx, startPy, endPx, endPy));
            string left = Span(Math.Min(startPx + 32, endPx), startPy, endPx, endPy), right = Span(startPx, startPy, Math.Max(startPx, endPx - 32), endPy);
            AddStorage(StorageCellsId + 1, "storage_corner_topleft", left);
            AddStorage(StorageCellsId + 2, "storage_corner_topright", right);
            AddStorage(StorageCellsId + 3, "storage_corner_bottomleft", left);
            AddStorage(StorageCellsId + 4, "storage_corner_bottomright", right);
            AddStorage(StorageCellsId + 5, "storage_rowjoin_left", Span(startPx, startPy + 27, startPx, startPy + 27 + Math.Max(0, rows - 2) * 32));
            AddStorage(StorageCellsId + 6, "storage_rowjoin_right", Span(endPx, startPy + 27, endPx, startPy + 27 + Math.Max(0, rows - 2) * 32));
            AddStorage(StorageCellsId + 7, "storage_up", $"{4 + columns}:{16 + 8},2:16");
            AddStorage(StorageCloseId, "storage_close", $"{4 + columns}:{16 - 5},2:16");
            int cx = 4, cy = 2;
            foreach (Thing item in box.Contents)
            {
                _screen[item.Id] = new ScreenEntry(item.Id, AppearanceOf(item), $"{cx}:16,{cy}:16") { Group = HudGroup.Storage };
                _storageItemIds.Add(item.Id);
                cx++;
                if (cx - 4 < columns)
                    continue;
                cx = 4;
                cy++;
            }
        }
    }

    private readonly List<long> _storageItemIds = new();

    private void AddStorage(long id, string state, string loc)
        => _screen[id] = new ScreenEntry(id, Leaf(HudIcon, state, 255, 22f), loc) { Group = HudGroup.Storage };

    // DM's round(): halves go up.
    private static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

    // Version 12: tg draws the item in a hand or a slot through the slot's vis_contents (inventory_slot.dm:55), so the entry lists it as its
    // clickable child, with the appearance of the overlay it is flattened into (HandApp, SlotApp).
    private ScreenChildren ShownChild(Thing item)
        => item == null ? null : new ScreenChildren { Items = new[] { new ScreenChild(item.Id, Leaf(item.Icon, ShownState(item), 255, 20f)) } };

    private static string ShownState(Thing item) => item.IsWelder && item.Lit ? "welder-on" : item.State;

    private TableRef HandApp(int hand)
    {
        Thing held = _hands[hand];
        bool active = _activeHand == hand;
        string key = $"hand|{hand}|{(active ? 1 : 0)}|{(held == null ? "" : held.Icon + "#" + (held.IsWelder && held.Lit ? "welder-on" : held.State))}";
        if (_invApps.TryGetValue(key, out TableRef known))
            return known;
        var root = new AppearanceEntry { Icon = PrivateResource(HudIcon), IconState = PrivateString(hand == 0 ? "hand_l" : "hand_r"), Layer = 19f };
        if (held != null)
            root.Overlays.Add(Leaf(held.Icon, held.IsWelder && held.Lit ? "welder-on" : held.State, 255, 20f));
        if (active)
            root.Overlays.Add(Leaf(HudIcon, hand == 0 ? "lhandactive" : "rhandactive", 255, 20.1f));
        TableRef made = PrivateAppearance(root);
        _invApps[key] = made;
        return made;
    }

    private TableRef SlotApp(int slot)
    {
        Thing worn = _slotItems[slot];
        string key = $"slot|{slot}|{(worn == null ? "" : worn.Icon + "#" + worn.State)}";
        if (_invApps.TryGetValue(key, out TableRef known))
            return known;
        var root = new AppearanceEntry { Icon = PrivateResource(HudIcon), IconState = PrivateString(SlotDefs[slot].State), Layer = 19f };
        if (worn != null)
            root.Overlays.Add(Leaf(worn.Icon, worn.State, 255, 20f));
        TableRef made = PrivateAppearance(root);
        _invApps[key] = made;
        return made;
    }

    // ---- what the player does ----

    private bool Reaches(Mob player, int x, int y, int z) => z == player.Motion.Z && Math.Abs(x - player.Motion.TileX) <= 1 && Math.Abs(y - player.Motion.TileY) <= 1;

    private void Balloon(string text) => _outbox.Add(Codec.Encode(new ServerText("balloon", text)));

    private bool InventoryClick(Mob player, ActionMessage a, List<string> replies)
    {
        TargetKind kind = a.EffectiveTargetKind;
        switch (kind)
        {
            case TargetKind.Screen:
                ScreenClick(a.Target, a.Button, replies);
                return true;
            case TargetKind.Entity:
                Thing thing = ThingById(a.Target);
                if (thing == null || thing.Where != Where.Floor)
                    return false;
                EntityClick(player, thing, a, replies);
                return true;
            case TargetKind.Turf:
                TurfClick(player, a.TurfX, a.TurfY, a.TurfZ, replies);
                return true;
        }
        return false;
    }

    private void EntityClick(Mob player, Thing thing, ActionMessage a, List<string> replies)
    {
        if (!Reaches(player, thing.X, thing.Y, thing.Z))
        {
            Balloon("Too far away!");
            return;
        }
        if (a.Button == ClickButton.Left && (a.Modifiers & ClickModifiers.Shift) != 0)
        {
            replies.Add($"This is a {thing.Name}.");
            return;
        }
        Thing inHand = _hands[_activeHand];
        if (thing.IsDoor)
        {
            if (a.Button == ClickButton.Right && inHand != null && inHand.IsWelder && inHand.Lit && !thing.Welded && _weldTarget == null)
            {
                _weldTarget = thing;
                _weldTicks = 0;
                _weldPercentShown = 0;
                _imageLive = true;
                _imagesAdded.Add(new ImageEntry(ProgressImageId, ProgressApp(0), thing.Id) { Holder = thing.Id, HolderName = thing.Name });
                replies.Add("You begin welding the airlock shut.");
            }
            else if (a.Button == ClickButton.Left)
                replies.Add(thing.Welded ? "The airlock is welded shut." : "The airlock whirs, but nothing opens in this room.");
            return;
        }
        if (a.Button == ClickButton.Right && thing.IsStorage)
        {
            OpenStorage(thing, replies);
            return;
        }
        if (a.Button == ClickButton.Left)
        {
            if (inHand != null)
            {
                replies.Add("Your hand is full.");
                return;
            }
            TakeToHand(thing, replies);
        }
    }

    private void TakeToHand(Thing thing, List<string> replies)
    {
        int hand = _hands[_activeHand] == null ? _activeHand : (_hands[1 - _activeHand] == null ? 1 - _activeHand : -1);
        if (hand < 0)
        {
            replies.Add("Your hands are full.");
            return;
        }
        if (thing.Parent != null)
            thing.Parent.Contents.Remove(thing);
        thing.Parent = null;
        thing.Where = Where.Hand;
        _hands[hand] = thing;
        if (_openStorage != null && _openStorage.Contents.Count == 0 && _openStorage == thing)
            _openStorage = null;
        replies.Add($"You pick up the {thing.Name}.");
    }

    private void OpenStorage(Thing box, List<string> replies)
    {
        _openStorage = box;
        replies.Add($"You open the {box.Name}.");
    }

    private void ScreenClick(long id, ClickButton button, List<string> replies)
    {
        if (ShownItemClick(id, replies))
            return;
        if (id == HandLeftId || id == HandRightId)
        {
            int hand = id == HandLeftId ? 0 : 1;
            if (_activeHand != hand)
            {
                _activeHand = hand;
                return;
            }
            Thing held = _hands[hand];
            if (held == null)
                return;
            if (button == ClickButton.Right || held.IsStorage)
            {
                if (held.IsStorage)
                    OpenStorage(held, replies);
            }
            else
                UseInHand(replies);
            return;
        }
        if (id == SwapId)
        {
            _activeHand = 1 - _activeHand;
            return;
        }
        if (id == DropId)
        {
            DropActive(replies);
            return;
        }
        if (id == ThrowId)
        {
            ToggleThrowMode(replies);
            return;
        }
        if (id >= SlotIdBase && id < SlotIdBase + SlotDefs.Length)
        {
            int slot = (int)(id - SlotIdBase);
            Thing inHand = _hands[_activeHand];
            if (inHand != null && _slotItems[slot] == null)
            {
                _hands[_activeHand] = null;
                inHand.Where = Where.Slot;
                _slotItems[slot] = inHand;
                replies.Add($"You put the {inHand.Name} on your {SlotDefs[slot].Name}.");
            }
            else if (inHand == null && _slotItems[slot] != null)
            {
                Thing worn = _slotItems[slot];
                _slotItems[slot] = null;
                worn.Where = Where.Hand;
                _hands[_activeHand] = worn;
                replies.Add($"You take the {worn.Name} off your {SlotDefs[slot].Name}.");
            }
            return;
        }
        if (_openStorage == null)
            return;
        if (id == StorageCloseId)
        {
            _openStorage = null;
            return;
        }
        if (id == StorageCellsId)
        {
            Thing inHand = _hands[_activeHand];
            if (inHand != null && inHand != _openStorage && _openStorage.Contents.Count < 7)
            {
                _hands[_activeHand] = null;
                inHand.Where = Where.Storage;
                inHand.Parent = _openStorage;
                _openStorage.Contents.Add(inHand);
                replies.Add($"You put the {inHand.Name} in the {_openStorage.Name}.");
            }
            return;
        }
        Thing item = _openStorage.Contents.Find(t => t.Id == id);
        if (item != null)
        {
            if (_hands[_activeHand] != null)
                replies.Add("Your hand is full.");
            else
                TakeToHand(item, replies);
        }
    }

    // Version 12: a click on the item in a hand or a slot is the item's own Click (BYOND's vis_contents rule), tg's ClickOn on an item in
    // DirectAccess: the active hand's own item gets attack_self; an empty active hand's UnarmedAttack is attack_hand (a toolbox opens,
    // datum/storage on_attack; another item goes to the active hand); a full one's is attackby (its item goes into a toolbox).
    private bool ShownItemClick(long id, List<string> replies)
    {
        int hand = _hands[0]?.Id == id ? 0 : _hands[1]?.Id == id ? 1 : -1;
        int slot = hand >= 0 ? -1 : Array.FindIndex(_slotItems, t => t != null && t.Id == id);
        if (hand < 0 && slot < 0)
            return false;
        Thing item = hand >= 0 ? _hands[hand] : _slotItems[slot];
        if (hand == _activeHand)
        {
            UseInHand(replies);
            return true;
        }
        Thing inHand = _hands[_activeHand];
        if (inHand == null)
        {
            if (item.IsStorage)
            {
                OpenStorage(item, replies);
                return true;
            }
            if (hand >= 0)
                _hands[hand] = null;
            else
                _slotItems[slot] = null;
            item.Where = Where.Hand;
            _hands[_activeHand] = item;
            replies.Add($"You pick up the {item.Name}.");
            return true;
        }
        if (item.IsStorage && item.Contents.Count < 7)
        {
            _hands[_activeHand] = null;
            inHand.Where = Where.Storage;
            inHand.Parent = item;
            item.Contents.Add(inHand);
            replies.Add($"You put the {inHand.Name} in the {item.Name}.");
        }
        return true;
    }

    private void ThrowAt(Mob player, int x, int y, int z, List<string> replies)
    {
        bool mode = _throwMode;
        _throwMode = true;
        TurfClick(player, x, y, z, replies);
        _throwMode = mode && _hands[_activeHand] != null;
    }

    private void TurfClick(Mob player, int x, int y, int z, List<string> replies)
    {
        Thing held = _hands[_activeHand];
        if (!_throwMode || held == null)
            return;
        Throw(player, held, x, y);
        _throwMode = false;
        replies.Add($"You throw the {held.Name}.");
    }

    private void Throw(Mob player, Thing item, int tx, int ty)
    {
        _hands[_activeHand] = null;
        item.Where = Where.Floor;
        item.X = player.Motion.TileX;
        item.Y = player.Motion.TileY;
        item.Z = player.Motion.Z;
        item.PixelX = item.PixelY = 0;
        int dx = tx - item.X, dy = ty - item.Y;
        int steps = Math.Min(7, Math.Max(Math.Abs(dx), Math.Abs(dy)));
        item.Flight = new List<(int X, int Y)>();
        for (int i = 1; i <= steps; i++)
        {
            int x = item.X + (int)Math.Round(dx * (i / (double)Math.Max(Math.Abs(dx), Math.Abs(dy)))), y = item.Y + (int)Math.Round(dy * (i / (double)Math.Max(Math.Abs(dx), Math.Abs(dy))));
            if (_view.IsSolid(x, y, item.Z))
                break;
            item.Flight.Add((x, y));
        }
        item.Thrown = item.Flight.Count > 0;
    }

    private void DropActive(List<string> replies)
    {
        Thing held = _hands[_activeHand];
        if (held == null)
            return;
        Mob player = _mobs[0];
        _hands[_activeHand] = null;
        held.Where = Where.Floor;
        held.X = player.Motion.TileX;
        held.Y = player.Motion.TileY;
        held.Z = player.Motion.Z;
        held.PixelX = _random.Next(-6, 7); // tg's dropItemToGround offsets, rand(-6, 6)
        held.PixelY = _random.Next(-6, 7);
        held.LastMoveTick = _tick;
        replies.Add($"You drop the {held.Name}.");
    }

    private void UseInHand(List<string> replies)
    {
        Thing held = _hands[_activeHand];
        if (held == null)
            return;
        if (held.IsWelder)
        {
            held.Lit = !held.Lit;
            replies.Add(held.Lit ? "You light the welding tool." : "You turn off the welding tool.");
        }
        else if (held.IsStorage)
            OpenStorage(held, replies);
    }

    private void ToggleThrowMode(List<string> replies)
    {
        _throwMode = !_throwMode;
        replies.Add(_throwMode ? "You are now getting ready to throw things." : "You have stopped getting ready to throw.");
    }

    private void InventoryKeybinding(ActionMessage a, List<string> replies)
    {
        switch (a.Keybinding)
        {
            case KeybindingCode.SwapHands when !a.KeyUp:
                _activeHand = 1 - _activeHand;
                break;
            case KeybindingCode.SelectLeftHand when !a.KeyUp:
                _activeHand = 0;
                break;
            case KeybindingCode.SelectRightHand when !a.KeyUp:
                _activeHand = 1;
                break;
            case KeybindingCode.ActivateInhand when !a.KeyUp:
                UseInHand(replies);
                break;
            case KeybindingCode.DropItem when !a.KeyUp:
                DropActive(replies);
                break;
            case KeybindingCode.ToggleThrowMode when !a.KeyUp:
                ToggleThrowMode(replies);
                break;
            case KeybindingCode.HoldThrowMode:
                if (a.KeyUp == _throwHeld)
                {
                    _throwHeld = !a.KeyUp;
                    _throwMode = _throwHeld;
                }
                break;
            case KeybindingCode.QuickEquip when !a.KeyUp:
                Thing held = _hands[_activeHand];
                if (held != null && _slotItems[BeltSlot] == null)
                {
                    _hands[_activeHand] = null;
                    held.Where = Where.Slot;
                    _slotItems[BeltSlot] = held;
                    replies.Add($"You put the {held.Name} on your belt.");
                }
                break;
        }
    }

    private void InventoryHover(ActionMessage a)
    {
        TargetKind kind = a.EffectiveTargetKind;
        var message = new ContextHintsMessage { TargetKind = kind, Target = kind is TargetKind.Entity or TargetKind.Screen ? a.Target : 0, TurfX = a.TurfX, TurfY = a.TurfY, TurfZ = a.TurfZ };
        Thing held = _hands[_activeHand];
        if (kind == TargetKind.Entity)
        {
            Thing thing = ThingById(a.Target);
            if (thing != null && thing.Where == Where.Floor)
            {
                message.Name = thing.Name;
                if (thing.IsDoor)
                {
                    message.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, thing.Welded ? "Welded" : "Open"));
                    if (held != null && held.IsWelder && held.Lit && !thing.Welded)
                        message.Hints.Add(new ContextHint(ClickButton.Right, ClickModifiers.None, "Weld shut"));
                }
                else
                {
                    message.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Pick up"));
                    if (thing.IsStorage)
                        message.Hints.Add(new ContextHint(ClickButton.Right, ClickModifiers.None, "Open"));
                }
                _outbox.Add(Codec.Encode(message));
                return;
            }
            Mob mob = Find(a.Target);
            if (mob != null && !mob.IsStatic)
            {
                message.Name = mob.Name;
                message.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Help"));
                message.Hints.Add(new ContextHint(ClickButton.Right, ClickModifiers.None, "Shove"));
                _outbox.Add(Codec.Encode(message));
            }
            return;
        }
        if (kind == TargetKind.Turf)
        {
            message.Name = "floor";
            if (_throwMode && held != null)
                message.Hints.Add(new ContextHint(ClickButton.Left, ClickModifiers.None, "Throw"));
            _outbox.Add(Codec.Encode(message));
        }
    }

    // ---- the tick ----

    private void StepInventory()
    {
        foreach (Thing t in _things)
        {
            if (t.Where == Where.Floor && t.Flight != null)
            {
                if (t.Flight.Count > 0 && _tick % Math.Max(1, _options.ThrowTicksPerTile) == 0)
                {
                    (int x, int y) = t.Flight[0];
                    t.Flight.RemoveAt(0);
                    t.X = x;
                    t.Y = y;
                    t.LastMoveTick = _tick;
                }
                if (t.Flight.Count == 0)
                {
                    t.Flight = null;
                    t.Thrown = false;
                }
            }
        }
        if (_weldTarget != null)
        {
            _weldTicks++;
            int percent = Math.Min(100, Round(_weldTicks / (double)WeldTicks * 20.0) * 5);
            if (_weldTicks >= WeldTicks)
            {
                _weldTarget.Welded = true;
                _imagesRemoved.Add(ProgressImageId);
                _imageLive = false;
                _weldTarget = null;
                _outbox.Add(Codec.Encode(new ServerText(null, "You weld the airlock shut.")));
            }
            else if (percent != _weldPercentShown)
            {
                _weldPercentShown = percent;
                _imagesChanged.Add(new ImageEntry(ProgressImageId, ProgressApp(percent), _weldTarget.Id) { Holder = _weldTarget.Id, HolderName = _weldTarget.Name });
            }
        }
    }

    private void FlushOutbox(Action<Channel, byte[]> emit)
    {
        foreach (byte[] message in _outbox)
            emit(Channel.Control, message);
        _outbox.Clear();
    }
}
