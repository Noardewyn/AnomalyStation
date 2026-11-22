using System.Linq;
using Anomaly.Client.Shared.World;
using Anomaly.Core.Motion;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Owner: M10 slice E (D38 stage 1, multiplayer.md section 8). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// The client's guesses of its own hands, doors and pushes: what an action or a bump will show, laid over the replica at once
/// (<see cref="ReplicaWorld.GuessScreen"/>, <see cref="ReplicaWorld.GuessHidden"/>, <see cref="ReplicaWorld.GuessDoorOpening"/>) and
/// dropped when the server's answer is in the replica: a hand or inventory guess when the server has settled its action
/// (<see cref="OwnState.ActionsDone"/>), a door guess when it has applied the bump's frame or the click's action, then kept passable for the
/// own capsule until the replica's cell turns passable. Single-threaded: the frame loop calls everything. DV179: a port's addition, tg's
/// client guesses nothing; sounds are not guessed.
/// </summary>
public sealed class OwnGuesses
{
    // tg's ITEM_SLOT_* bits (__DEFINES/inventory.dm) that human/can_equip ties to another slot
    private const uint SlotOClothing = 1u << 0;
    private const uint SlotIClothing = 1u << 1;
    private const uint SlotId = 1u << 8;
    private const uint SlotBelt = 1u << 9;
    private const uint SlotSuitStore = 1u << 14;
    private const uint SlotPockets = (1u << 15) | (1u << 16);

    /// <summary>Frames from an airlock's opening to its cell turning passable: airlock.dm open's sleeps to <c>AIRLOCK_OPENING_PASSABLE</c> (0.5 s).</summary>
    public const uint PassableFrames = 10;

    /// <summary>tg's bump-open cooldown in frames: door.dm Bumped ignores a bump within 0.3 s of the mob's last one.</summary>
    public const uint BumpCooldownFrames = 7;

    /// <summary>Frames after which a guess the server never settled is dropped (a lost or refused action of a kind not counted).</summary>
    public const uint TimeoutFrames = 100;

    private readonly ReplicaWorld _replica;
    private readonly Func<(int X, int Y, int Z)> _ownTile;
    private readonly List<Guess> _pending = new();
    private uint _actions;
    private uint _lastBump;
    private bool _bumped;

    private sealed class Guess
    {
        public uint Action; // the action's number (1 based), 0 for a bump
        public uint Frame; // the frame the guess belongs to
        public readonly List<long> Screen = new();
        public long Hidden;
        public long Door;
        public bool Accepted; // a door the server opened: only its passable cell is kept
        public (int X, int Y, int Z) Cell;
    }

    /// <param name="replica">The replica the guesses are laid over.</param>
    /// <param name="ownTile">The own capsule's tile now (the prediction's).</param>
    public OwnGuesses(ReplicaWorld replica, Func<(int X, int Y, int Z)> ownTile)
    {
        _replica = replica ?? throw new ArgumentNullException(nameof(replica));
        _ownTile = ownTile ?? throw new ArgumentNullException(nameof(ownTile));
    }

    /// <summary>Guesses not yet settled.</summary>
    public int Pending => _pending.Count;

    /// <summary>Guesses made so far, by what they were (for tests and the dev overlay).</summary>
    public int Made { get; private set; }

    /// <summary>Guesses the server refused (a door that stayed shut, a hand or slot the server's state did not take).</summary>
    public int Refused { get; private set; }

    /// <summary>The connection started over: the action count with it.</summary>
    public void Reset()
    {
        _pending.Clear();
        foreach (PushGuess g in _pushes)
            _replica.ClearPlaceGuess(g.Thing);
        _pushes.Clear();
        _actions = 0;
        _bumped = false;
    }

    /// <summary>
    /// The action about to be sent (every action counts, so the n-th sent is the server's n-th): its visible outcome is guessed when it is
    /// one of stage 1's.
    /// </summary>
    public void OnAction(in ActionMessage action)
    {
        uint number = ++_actions;
        var guess = new Guess { Action = number, Frame = action.FrameSeq };
        switch (action.Kind)
        {
            case ActionKind.Keybinding when !action.KeyUp && action.Keybinding == KeybindingCode.SwapHands:
                Swap(guess, 0);
                break;
            case ActionKind.Keybinding when !action.KeyUp && action.Keybinding == KeybindingCode.DropItem:
                Drop(guess);
                break;
            case ActionKind.Keybinding when !action.KeyUp && action.Keybinding == KeybindingCode.ToggleThrowMode:
                Throw(guess, null);
                break;
            case ActionKind.Keybinding when action.Keybinding == KeybindingCode.HoldThrowMode:
                Throw(guess, !action.KeyUp);
                break;
            case ActionKind.Click when action.Button == ClickButton.Left && action.Modifiers == ClickModifiers.None:
                Click(guess, action);
                break;
            case ActionKind.Throw: // version 81 (D328): the held thing leaves the hand at once
            case ActionKind.PutDown: // version 82 (D329): so does a thing put down
                Drop(guess);
                break;
        }
        Keep(guess);
    }

    /// <summary>The newest frame's capsule pressed into a blocked face (<see cref="OwnPrediction.Pressed"/>): a bumped airlock that opens for the player is guessed opening.</summary>
    public void OnPressed(MotionEvent press, uint frame)
    {
        (int x, int y, int z) = _ownTile();
        ReplicaEntity door = DoorAt(press.ToX, press.ToY, z);
        if (door == null)
            return;
        // door.dm Bumped: the mob's last bump is set before the access check, so any bump on a door restarts the cooldown
        bool cooling = _bumped && frame - _lastBump < BumpCooldownFrames;
        _bumped = true;
        _lastBump = frame;
        if (cooling || !Opens(door.State.Door, press.FromX, press.FromY, press.ToX, press.ToY))
            return;
        var guess = new Guess { Frame = frame };
        OpenDoor(guess, door, frame);
        Keep(guess);
    }

    // ---- pushes ----

    // A thing guessed pushed from one cell to the next by the press of frame Frame (cells guessed from Frame + 1).
    private readonly record struct PushGuess(long Thing, int FromX, int FromY, int ToX, int ToY, int Z, uint Frame, ushort GlideTicks);

    private readonly List<PushGuess> _pushes = new();

    /// <summary>Pushes guessed and not yet answered by the server.</summary>
    public int PendingPushes => _pushes.Count;

    /// <summary>
    /// <see cref="MotionPredictor.Pushes"/>: the press of frame <paramref name="frame"/> on level <paramref name="z"/> pushes a thing tg's
    /// PushAM moves (living.dm:385: <c>AM.Move(get_step(AM.loc, dir_to_target), dir_to_target, glide_size)</c>, then the mob after it): a thing
    /// the server flags <see cref="EntityFlags.Pushable"/> in the pressed cell, the cell beyond it open. Then the pressed cell is guessed passable
    /// and the one beyond solid from the next frame, and the thing is shown gliding there at the mob's glide (<see cref="ReplicaWorld.GuessPlace"/>),
    /// until the server's state of that frame arrives. Called again for the same frame when the predictor replays it.
    /// </summary>
    public bool OnPush(in MotionEvent press, int z, uint frame, ushort glideTicks)
    {
        foreach (PushGuess g in _pushes)
            if (g.Frame == frame)
                return g.FromX == press.ToX && g.FromY == press.ToY && g.Z == z;
        long thing = PushedBy(in press, z, frame);
        if (thing == 0)
            return false;
        (int fx, int fy) = (2 * press.ToX - press.FromX, 2 * press.ToY - press.FromY);
        _pushes.Add(new PushGuess(thing, press.ToX, press.ToY, fx, fy, z, frame, glideTicks));
        _replica.GuessPushed(press.ToX, press.ToY, fx, fy, z, frame + 1);
        ShowPlace(thing);
        return true;
    }

    /// <summary>Whether <see cref="OnPush"/> would guess the press of frame <paramref name="frame"/> pushing, guessing nothing (the drawn tick in progress).</summary>
    public bool WouldPush(in MotionEvent press, int z, uint frame) => PushedBy(in press, z, frame) != 0;

    // The thing the press pushes (0: none): a Pushable thing in the pressed cell, the cell beyond it open and no mob there.
    private long PushedBy(in MotionEvent press, int z, uint frame)
    {
        if ((_replica.FacesOf(press.FromX, press.FromY, z) & (TileEdges)press.Dir) != 0 || !_replica.IsSolid(press.ToX, press.ToY, z))
            return 0;
        (int dx, int dy) = (press.ToX - press.FromX, press.ToY - press.FromY);
        int fx = press.ToX + dx, fy = press.ToY + dy;
        var opposite = (TileEdges)(press.Dir switch { MotionDir.North => MotionDir.South, MotionDir.South => MotionDir.North, MotionDir.East => MotionDir.West, _ => MotionDir.East });
        if ((_replica.FacesOf(press.ToX, press.ToY, z) & (TileEdges)press.Dir) != 0 || (_replica.FacesOf(fx, fy, z) & opposite) != 0 || _replica.IsSolid(fx, fy, z))
            return 0;
        long thing = 0;
        foreach (ReplicaEntity e in _replica.Entities)
        {
            if (e.HasPos && e.State.X == fx && e.State.Y == fy && e.State.Z == z)
                return 0; // a mob beyond: the pushed thing's Move bumps it
            if (thing == 0 && Pushable(e) && CellAt(e, frame) == (press.ToX, press.ToY, z))
                thing = e.Id;
        }
        return thing;
    }

    // The thing drawn where its newest push guess moved it, else where the server has it.
    private void ShowPlace(long thing)
    {
        PushGuess? newest = null;
        foreach (PushGuess g in _pushes)
            if (g.Thing == thing && (newest == null || g.Frame > newest.Value.Frame))
                newest = g;
        if (newest is PushGuess n)
            _replica.GuessPlace(thing, n.ToX, n.ToY, n.Z, EntityGlide.TilePixels / Math.Max((ushort)1, n.GlideTicks));
        else
            _replica.ClearPlaceGuess(thing);
    }

    // Where a thing is for frame <paramref name="frame"/>: the cell the last earlier push guess moved it to, else the replica's.
    private (int X, int Y, int Z) CellAt(ReplicaEntity e, uint frame)
    {
        EntityState server = e.ServerState;
        (int X, int Y, int Z) cell = (server.X, server.Y, server.Z);
        uint newest = 0;
        foreach (PushGuess g in _pushes)
        {
            if (g.Thing == e.Id && g.Frame < frame && g.Frame >= newest)
            {
                newest = g.Frame;
                cell = (g.ToX, g.ToY, g.Z);
            }
        }
        return cell;
    }

    private static bool Pushable(ReplicaEntity e) => (e.ServerState.Flags & EntityFlags.Pushable) != 0;

    /// <summary>A private block arrived: the guesses the server has answered are dropped, so the replica's state shows.</summary>
    public void OnOwnState(OwnState own)
    {
        for (int i = _pushes.Count - 1; i >= 0; i--)
        {
            PushGuess g = _pushes[i];
            if (own.AckedSeq < g.Frame)
                continue;
            _replica.ClearPushGuess(g.FromX, g.FromY, g.ToX, g.ToY, g.Z, g.Frame + 1);
            _pushes.RemoveAt(i);
            ShowPlace(g.Thing);
        }
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            Guess g = _pending[i];
            bool due = g.Action != 0 ? own.ActionsDone >= g.Action : own.AckedSeq >= g.Frame;
            bool stale = own.AckedSeq > g.Frame + TimeoutFrames;
            if (g.Door != 0)
            {
                if (!g.Accepted && (due || stale))
                {
                    g.Accepted = !stale && DoorAccepted(g.Door, g.Cell);
                    _replica.ClearDoorGuess(g.Door, keepPassable: g.Accepted);
                    if (!g.Accepted)
                    {
                        Refused++;
                        _pending.RemoveAt(i);
                    }
                    continue;
                }
                if (g.Accepted && (stale || !_replica.IsSolidServer(g.Cell.X, g.Cell.Y, g.Cell.Z)))
                {
                    _replica.ClearPassable(g.Cell.X, g.Cell.Y, g.Cell.Z);
                    _pending.RemoveAt(i);
                }
                continue;
            }
            if (!due && !stale)
                continue;
            foreach (long id in g.Screen)
                _replica.ClearScreenGuess(id);
            if (g.Hidden != 0)
            {
                if (_replica.IsHidden(g.Hidden))
                    Refused++; // the server kept the item where it was (its despawn ends the hiding)
                _replica.ClearHidden(g.Hidden);
            }
            _pending.RemoveAt(i);
        }
    }

    private void Keep(Guess guess)
    {
        if (guess.Screen.Count == 0 && guess.Hidden == 0 && guess.Door == 0)
            return;
        Made++;
        _pending.Add(guess);
    }

    // ---- the hands ----

    // tg's hand boxes (screen/inventory/hand, by held_index) and which one is active (its "lhandactive" or "rhandactive" overlay).
    private List<ScreenEntry> Hands(out int active, out TableRef activeOverlay)
    {
        var hands = new List<ScreenEntry>();
        foreach (ScreenEntry e in _replica.Screen.Values)
            if (e.HandIndex > 0)
                hands.Add(e);
        hands.Sort((a, b) => a.HandIndex.CompareTo(b.HandIndex));
        active = -1;
        activeOverlay = TableRef.None;
        for (int i = 0; i < hands.Count && active < 0; i++)
        {
            if (!_replica.TryGetAppearance(hands[i].Appearance, out AppearanceEntry app) || app.OverlayCount == 0)
                continue;
            foreach (TableRef o in app.Overlays)
            {
                if (_replica.TryGetAppearance(o, out AppearanceEntry oa) && (_replica.ResolveString(oa.IconState) ?? string.Empty).EndsWith("handactive", StringComparison.Ordinal))
                {
                    active = i;
                    activeOverlay = o;
                    break;
                }
            }
        }
        return hands;
    }

    // The item a box holds: tg puts it in the box's vis_contents (inventory_slot.dm update_slot_icon_contents).
    private static bool ItemIn(in ScreenEntry box, out ScreenChild item)
    {
        if (box.VisChildren != null)
        {
            foreach (ScreenChild c in box.VisChildren.Items)
            {
                if (c.IsItem)
                {
                    item = c;
                    return true;
                }
            }
        }
        item = default;
        return false;
    }

    // The box whose vis_contents hold the item <paramref name="id"/>.
    private bool BoxOf(long id, out ScreenEntry box)
    {
        foreach (ScreenEntry e in _replica.Screen.Values)
        {
            if (e.VisChildren != null && e.VisChildren.Contains(id))
            {
                box = e;
                return true;
            }
        }
        box = default;
        return false;
    }

    // The box with the item put in: its vis child, and its picture flattened over the box's as the server draws it.
    private ScreenEntry Holding(in ScreenEntry box, in ScreenChild item)
    {
        AppearanceEntry look = _replica.TryGetAppearance(box.Appearance, out AppearanceEntry app) ? app.WithOverlays(_ => true) : new AppearanceEntry();
        look.Overlays.Add(item.Appearance);
        ScreenChild[] children = box.VisChildren?.Items ?? Array.Empty<ScreenChild>();
        return box with { Appearance = _replica.LocalAppearance(look), VisChildren = new ScreenChildren { Items = children.Append(item).ToArray() } };
    }

    // The box with the item taken out.
    private ScreenEntry Emptied(in ScreenEntry box, ScreenChild item)
    {
        AppearanceEntry look = _replica.TryGetAppearance(box.Appearance, out AppearanceEntry app) ? app.WithOverlays(o => !o.Equals(item.Appearance)) : new AppearanceEntry();
        ScreenChild[] left = box.VisChildren.Items.Where(c => c.Id != item.Id).ToArray();
        return box with { Appearance = _replica.LocalAppearance(look), VisChildren = left.Length == 0 ? null : new ScreenChildren { Items = left } };
    }

    // tg's swap_hand: the active overlay moves to the next hand (or to held_index).
    private void Swap(Guess guess, byte toIndex)
    {
        List<ScreenEntry> hands = Hands(out int active, out TableRef overlay);
        if (active < 0 || hands.Count < 2)
            return;
        int to = toIndex == 0 ? (active + 1) % hands.Count : hands.FindIndex(h => h.HandIndex == toIndex);
        if (to < 0 || to == active || !_replica.TryGetAppearance(hands[active].Appearance, out AppearanceEntry from)
            || !_replica.TryGetAppearance(hands[to].Appearance, out AppearanceEntry target) || !_replica.TryGetAppearance(overlay, out AppearanceEntry mark))
            return;
        AppearanceEntry left = from.WithOverlays(o => !o.Equals(overlay));
        AppearanceEntry right = target.WithOverlays(_ => true);
        AppearanceEntry newMark = mark.WithOverlays(_ => true);
        newMark.IconState = _replica.LocalString(hands[to].HandIndex % 2 == 1 ? "lhandactive" : "rhandactive"); // IS_LEFT_INDEX
        right.Overlays.Add(_replica.LocalAppearance(newMark));
        GuessEntry(guess, hands[active] with { Appearance = _replica.LocalAppearance(left) });
        GuessEntry(guess, hands[to] with { Appearance = _replica.LocalAppearance(right) });
    }

    // dropItemToGround of the active hand's item: it leaves the hand (where it lands is the server's word).
    private void Drop(Guess guess)
    {
        List<ScreenEntry> hands = Hands(out int active, out _);
        if (active >= 0 && ItemIn(hands[active], out ScreenChild item))
            GuessEntry(guess, Emptied(hands[active], item));
    }

    // toggle_throw_mode / hold_throw_mode: the throw button's act_throw_on or act_throw.
    private void Throw(Guess guess, bool? on)
    {
        foreach (ScreenEntry e in _replica.Screen.Values)
        {
            if (!_replica.TryGetAppearance(e.Appearance, out AppearanceEntry app))
                continue;
            string state = _replica.ResolveString(app.IconState);
            if (state != "act_throw_on" && state != "act_throw")
                continue;
            bool next = on ?? state == "act_throw";
            if (next == (state == "act_throw_on"))
                return;
            AppearanceEntry copy = app.WithOverlays(_ => true);
            copy.IconState = _replica.LocalString(next ? "act_throw_on" : "act_throw"); // living_item_handling.dm throw_mode_on, throw_mode_off
            GuessEntry(guess, e with { Appearance = _replica.LocalAppearance(copy) });
            return;
        }
    }

    private void Click(Guess guess, in ActionMessage action)
    {
        if (_replica.Own.HandsBlocked)
            return; // click.dm ClickOn: an incapacitated or hand-blocked mob's click reaches nothing (the owner, 2026-11-18: a stunned pick-up flashed into the hand)
        if (action.EffectiveTargetKind == TargetKind.Screen)
        {
            List<ScreenEntry> hands = Hands(out int active, out _);
            if (active < 0)
                return;
            if (!_replica.Screen.TryGetValue(action.Target, out ScreenEntry clicked))
            {
                // an item in a box (BYOND sends the click on its pixels to the vis child): the item's attack_hand moves it to the empty active hand
                if (BoxOf(action.Target, out ScreenEntry box) && box.Id != hands[active].Id && ItemIn(box, out ScreenChild item) && item.Id == action.Target)
                    TakeToHand(guess, box, item, hands[active]);
                return;
            }
            if (clicked.HandIndex > 0)
            {
                // screen/inventory/hand Click: another hand's box swaps to it
                if (clicked.HandIndex != hands[active].HandIndex)
                    Swap(guess, clicked.HandIndex);
                return;
            }
            if (clicked.SlotId == 0)
                return;
            // screen/inventory Click: a worn item takes the click; an empty slot is attack_ui, equipping the active hand's item
            if (ItemIn(clicked, out ScreenChild worn))
                TakeToHand(guess, clicked, worn, hands[active]);
            else if (ItemIn(hands[active], out ScreenChild held) && CanWear(held, clicked.SlotId))
            {
                GuessEntry(guess, Emptied(hands[active], held));
                GuessEntry(guess, Holding(clicked, held));
            }
            return;
        }
        if (action.EffectiveTargetKind != TargetKind.Entity || !_replica.TryGet(action.Target, out ReplicaEntity target) || !NextToMe(target))
            return;
        if (target.State.Door != null)
        {
            // door.dm attack_hand -> try_to_activate_door with an empty hand
            List<ScreenEntry> hands = Hands(out int active, out _);
            (int x, int y, _) = _ownTile();
            if (active >= 0 && !ItemIn(hands[active], out _) && Opens(target.State.Door, x, y, target.State.X, target.State.Y))
                OpenDoor(guess, target, action.FrameSeq);
            return;
        }
        // an anchored item on the wall (an intercom) is not picked up: its attack_hand opens it (the owner's play test 2026-11-03: it flashed into the hand)
        if (target.TypePath?.StartsWith("/obj/item/", StringComparison.Ordinal) == true && !PropKinds.IsWallMountType(target.TypePath))
            PickUp(guess, target);
    }

    // item attack_hand -> attempt_pickup: from the world into the empty active hand.
    private void PickUp(Guess guess, ReplicaEntity item)
    {
        List<ScreenEntry> hands = Hands(out int active, out _);
        if (active < 0 || ItemIn(hands[active], out _) || item.State.Appearance.IsNone)
            return;
        _replica.GuessHidden(item.Id);
        guess.Hidden = item.Id;
        GuessEntry(guess, Holding(hands[active], new ScreenChild(item.Id, item.State.Appearance) { IsItem = true }));
    }

    // item attack_hand on an item the mob wears or holds in another hand: temporarilyRemoveItemFromInventory, put_in_active_hand.
    private void TakeToHand(Guess guess, in ScreenEntry box, in ScreenChild item, in ScreenEntry activeHand)
    {
        if (box.Id == activeHand.Id || ItemIn(activeHand, out _) || item.HasStorage) // a storage's attack_hand opens it (storage.dm on_attack_hand)
            return;
        GuessEntry(guess, Emptied(box, item));
        GuessEntry(guess, Holding(activeHand, item));
    }

    // The item's slot_flags take the slot, it has no equip delay, the slot is free (the caller), and human/can_equip's ties hold:
    // the belt, the ID and the pockets need a jumpsuit, the suit storage a suit.
    private bool CanWear(in ScreenChild item, uint slot)
    {
        if (item.EquipDelay || (item.ItemSlots & slot) == 0)
            return false;
        if ((slot & (SlotBelt | SlotId | SlotPockets)) != 0 && !Wears(SlotIClothing))
            return false;
        return (slot & SlotSuitStore) == 0 || Wears(SlotOClothing);
    }

    // Whether the slot holds an item; a slot whose box is not on screen (tg's collapsed inventory hides the clothing's boxes) is taken as worn.
    private bool Wears(uint slot)
    {
        foreach (ScreenEntry box in _replica.Screen.Values)
            if (box.SlotId == slot)
                return ItemIn(box, out _);
        return true;
    }

    private void GuessEntry(Guess guess, in ScreenEntry entry)
    {
        _replica.GuessScreen(entry);
        if (!guess.Screen.Contains(entry.Id))
            guess.Screen.Add(entry.Id);
    }

    // ---- the doors ----

    private ReplicaEntity DoorAt(int x, int y, int z)
    {
        foreach (ReplicaEntity e in _replica.Entities)
            if (e.State.Door != null && e.State.X == x && e.State.Y == y && e.State.Z == z)
                return e;
        return null;
    }

    private void OpenDoor(Guess guess, ReplicaEntity door, uint frame)
    {
        EntityState s = door.State;
        _replica.GuessDoorOpening(door.Id, s.X, s.Y, s.Z, frame + PassableFrames);
        guess.Door = door.Id;
        guess.Cell = (s.X, s.Y, s.Z);
    }

    // door.dm try_to_activate_door: openable now, and allowed (access.dm check_access_list, or emergency) or the unrestricted side.
    private bool Opens(DoorState door, int fromX, int fromY, int doorX, int doorY)
    {
        if (door == null || (door.Flags & DoorFlags.Openable) == 0)
            return false;
        if ((door.Flags & DoorFlags.Emergency) != 0 || HasAccess(door, _replica.OwnAccess))
            return true;
        int side = (fromY > doorY ? 1 : fromY < doorY ? 2 : 0) | (fromX > doorX ? 4 : fromX < doorX ? 8 : 0); // get_dir(door, opener)
        return (side & door.UnresSides) != 0;
    }

    /// <summary>tg's <c>check_access_list</c>: every one of <c>req_access</c>, and one of <c>req_one_access</c> when it has any.</summary>
    public static bool HasAccess(DoorState door, IReadOnlyList<string> access)
    {
        if (door.ReqAll.Length == 0 && door.ReqAny.Length == 0)
            return true;
        if (access == null || access.Count == 0)
            return false;
        foreach (string req in door.ReqAll)
            if (!access.Contains(req))
                return false;
        if (door.ReqAny.Length == 0)
            return true;
        foreach (string req in door.ReqAny)
            if (access.Contains(req))
                return true;
        return false;
    }

    // The server opened it: its picture is opening or open, or its cell already lets capsules through.
    private bool DoorAccepted(long id, (int X, int Y, int Z) cell)
    {
        if (!_replica.IsSolidServer(cell.X, cell.Y, cell.Z))
            return true;
        if (!_replica.TryGet(id, out ReplicaEntity door) || !_replica.TryGetAppearance(door.State.Appearance, out AppearanceEntry app))
            return false;
        string state = _replica.ResolveString(app.IconState);
        return DoorAnimation.PhaseOf(state) == DoorPhase.Opening || DoorAnimation.OwnSolid(state) == false;
    }

    private bool NextToMe(ReplicaEntity e)
    {
        (int x, int y, int z) = _ownTile();
        return e.State.Z == z && Math.Abs(e.State.X - x) <= 1 && Math.Abs(e.State.Y - y) <= 1;
    }
}
