using System.Linq;
using Anomaly.Core.Motion;

namespace Anomaly.Protocol;

// Version 43 (M10 slice E, D38 stage 1): the own access list, and the client's guesses laid over the server's state. The replica keeps
// the server's state as it arrived; a guess changes only what the client's readers see (Screen, TryGet, Entities, IsSolid, the door
// state) until the guesser clears it, and every change of the view raises the same events a message would.

/// <summary>Version 43: a door the client guessed is opening (<see cref="ReplicaWorld.GuessDoorOpening"/>).</summary>
public readonly record struct DoorGuess(long Door, int X, int Y, int Z, uint PassableFrom);

public sealed partial class ReplicaWorld
{
    /// <summary>The first index of the client's own (local) private table entries: never reached by the server's tables (<see cref="Limits"/>).</summary>
    public const uint LocalBase = 1u << 30;

    /// <summary>The icon state a guessed opening airlock shows (tg's <c>AIRLOCK_OPENING</c>, airlock.dm update_icon_state).</summary>
    public const string DoorOpeningState = "opening";

    private readonly List<string> _localStrings = new();
    private readonly Dictionary<string, uint> _localStringIndex = new();
    private readonly List<AppearanceEntry> _localAppearances = new();
    private readonly Dictionary<long, ScreenEntry?> _screenGuess = new();
    private Dictionary<long, ScreenEntry> _screenView;
    private readonly HashSet<long> _hidden = new();
    private readonly Dictionary<long, DoorGuess> _doorGuess = new();
    private readonly Dictionary<int, uint> _passableGuess = new();

    /// <summary>Version 43: the own mob's access (tg's <c>get_access()</c>), the server's last word; empty for none.</summary>
    public IReadOnlyList<string> OwnAccess { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// The seq of the frame the own capsule's predictor steps (the collision view's <c>SteppingFrame</c>), the newest frame otherwise: a cell
    /// guessed passable from a frame on is passable only for that frame and later ones.
    /// </summary>
    public uint GuessFrame { get; set; }

    /// <summary>Whether any guess is laid over the server's state.</summary>
    public bool HasGuesses => _screenGuess.Count > 0 || _hidden.Count > 0 || _doorGuess.Count > 0 || _passableGuess.Count > 0 || _solidGuess.Count > 0 || _placeGuess.Count > 0;

    // ---- the client's own table entries ----

    /// <summary>A string of the client's own (a guessed icon state), as a private reference past <see cref="LocalBase"/>.</summary>
    public TableRef LocalString(string value)
    {
        if (!_localStringIndex.TryGetValue(value, out uint index))
        {
            _localStrings.Add(value);
            index = LocalBase + (uint)_localStrings.Count;
            _localStringIndex[value] = index;
        }
        return new TableRef(index, true);
    }

    /// <summary>An appearance of the client's own (a guessed HUD picture), as a private reference past <see cref="LocalBase"/>.</summary>
    public TableRef LocalAppearance(AppearanceEntry entry)
    {
        _localAppearances.Add(entry ?? throw new ArgumentNullException(nameof(entry)));
        return new TableRef(LocalBase + (uint)_localAppearances.Count, true);
    }

    private bool TryGetLocal<T>(List<T> table, TableRef reference, out T value)
    {
        value = default;
        if (!reference.Private || reference.Index <= LocalBase)
            return false;
        uint i = reference.Index - LocalBase;
        if (i > table.Count)
            return false;
        value = table[(int)i - 1];
        return true;
    }

    // ---- the guessed screen ----

    /// <summary>Shows <paramref name="entry"/> in place of the server's screen entry of its id (or as a new one) until <see cref="ClearScreenGuess"/>.</summary>
    public void GuessScreen(in ScreenEntry entry)
    {
        _screenGuess[entry.Id] = entry;
        RebuildScreenView();
        _screenChangedHandlers?.Invoke(entry);
    }

    /// <summary>Hides the server's screen entry <paramref name="id"/> until <see cref="ClearScreenGuess"/>.</summary>
    public void GuessScreenRemoved(long id)
    {
        _screenGuess[id] = null;
        RebuildScreenView();
        _screenRemovedHandlers?.Invoke(id);
    }

    /// <summary>Drops the guess on screen entry <paramref name="id"/>: the server's entry (or its absence) shows again.</summary>
    public void ClearScreenGuess(long id)
    {
        if (!_screenGuess.Remove(id))
            return;
        RebuildScreenView();
        if (_screen.TryGetValue(id, out ScreenEntry entry))
            _screenChangedHandlers?.Invoke(entry);
        else
            _screenRemovedHandlers?.Invoke(id);
    }

    private void RebuildScreenView()
    {
        if (_screenGuess.Count == 0)
        {
            _screenView = null;
            return;
        }
        _screenView = new Dictionary<long, ScreenEntry>(_screen);
        foreach (KeyValuePair<long, ScreenEntry?> guess in _screenGuess)
        {
            if (guess.Value.HasValue)
                _screenView[guess.Key] = guess.Value.Value;
            else
                _screenView.Remove(guess.Key);
        }
    }

    // ---- hidden entities (an item guessed picked up) ----

    /// <summary>Takes entity <paramref name="id"/> out of the client's view (raising <see cref="Despawned"/>) until <see cref="ClearHidden"/> or its real despawn.</summary>
    public void GuessHidden(long id)
    {
        if (!_byId.TryGetValue(id, out ReplicaEntity entity) || !_hidden.Add(id))
            return;
        Despawned?.Invoke(entity);
    }

    /// <summary>Puts a hidden entity back (raising <see cref="Spawned"/>) when the server still has it.</summary>
    public void ClearHidden(long id)
    {
        if (!_hidden.Remove(id))
            return;
        if (_byId.TryGetValue(id, out ReplicaEntity entity))
            Spawned?.Invoke(entity);
    }

    /// <summary>Whether the client's view hides entity <paramref name="id"/>.</summary>
    public bool IsHidden(long id) => _hidden.Contains(id);

    // ---- guessed doors ----

    /// <summary>
    /// Shows airlock <paramref name="door"/> opening (<see cref="GuessedDoorState"/>) and makes its cell (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>) passable for the frames from <paramref name="passableFrom"/> on (<see cref="IsSolid"/>), until cleared.
    /// </summary>
    public void GuessDoorOpening(long door, int x, int y, int z, uint passableFrom)
    {
        if (_map == null || IsOutsideMap(x, y, z))
            return;
        _doorGuess[door] = new DoorGuess(door, x, y, z, passableFrom);
        _passableGuess[CellIndex(x, y, z)] = passableFrom;
    }

    /// <summary>The guessed icon state of a door (<see cref="DoorOpeningState"/>), or null when none is guessed.</summary>
    public string GuessedDoorState(long door) => _doorGuess.ContainsKey(door) ? DoorOpeningState : null;

    /// <summary>The door guess of <paramref name="door"/>, if any.</summary>
    public bool TryGetDoorGuess(long door, out DoorGuess guess) => _doorGuess.TryGetValue(door, out guess);

    /// <summary>Drops the door's picture guess; its cell stays passable as guessed when <paramref name="keepPassable"/> (until <see cref="ClearPassable"/>).</summary>
    public void ClearDoorGuess(long door, bool keepPassable)
    {
        if (!_doorGuess.Remove(door, out DoorGuess guess) || keepPassable)
            return;
        ClearPassable(guess.X, guess.Y, guess.Z);
    }

    /// <summary>Drops the passable guess of a cell.</summary>
    public void ClearPassable(int x, int y, int z)
    {
        if (_map != null && !IsOutsideMap(x, y, z))
            _passableGuess.Remove(CellIndex(x, y, z));
    }

    /// <summary>Whether the server's state (without the guesses) has the cell blocking capsules.</summary>
    public bool IsSolidServer(int x, int y, int z)
    {
        if (_map == null || x < 1 || y < 1 || z < 1 || x > _map.Width || y > _map.Height || z > _map.Levels)
            return true;
        int i = CellIndex(x, y, z);
        return _solid != null && (_solid[i >> 3] & (1 << (i & 7))) != 0;
    }

    // ---- guessed pushes ----

    /// <summary>
    /// A thing the own capsule pushed (tg's PushAM) from cell (<paramref name="fromX"/>, <paramref name="fromY"/>) to (<paramref name="toX"/>,
    /// <paramref name="toY"/>) on level <paramref name="z"/>: for the frames from <paramref name="from"/> on the first cell is passable and the
    /// second solid (<see cref="IsSolid"/>), until <see cref="ClearPushGuess"/>.
    /// </summary>
    public void GuessPushed(int fromX, int fromY, int toX, int toY, int z, uint from)
    {
        if (_map == null || IsOutsideMap(fromX, fromY, z) || IsOutsideMap(toX, toY, z))
            return;
        _passableGuess[CellIndex(fromX, fromY, z)] = from;
        _solidGuess[CellIndex(toX, toY, z)] = from;
    }

    /// <summary>Drops a push guess of <see cref="GuessPushed"/> (each cell's only when a later guess has not replaced it).</summary>
    public void ClearPushGuess(int fromX, int fromY, int toX, int toY, int z, uint from)
    {
        if (_map == null || IsOutsideMap(fromX, fromY, z) || IsOutsideMap(toX, toY, z))
            return;
        int a = CellIndex(fromX, fromY, z), b = CellIndex(toX, toY, z);
        if (_passableGuess.TryGetValue(a, out uint pa) && pa == from)
            _passableGuess.Remove(a);
        if (_solidGuess.TryGetValue(b, out uint sb) && sb == from)
            _solidGuess.Remove(b);
    }

    private readonly Dictionary<int, uint> _solidGuess = new();
    private readonly Dictionary<long, (int X, int Y, int Z, float GlideSize)> _placeGuess = new();

    /// <summary>
    /// Version 56: shows entity <paramref name="id"/> on the cell (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), gliding there at
    /// <paramref name="glideSize"/> (<see cref="ReplicaEntity.State"/>; <see cref="ReplicaEntity.ServerState"/> keeps the server's), until
    /// <see cref="ClearPlaceGuess"/>: a thing the own capsule pushed.
    /// </summary>
    public void GuessPlace(long id, int x, int y, int z, float glideSize) => _placeGuess[id] = (x, y, z, glideSize);

    /// <summary>Drops the place guess of entity <paramref name="id"/>: the server's place shows again.</summary>
    public void ClearPlaceGuess(long id) => _placeGuess.Remove(id);

    internal bool HasPlaceGuesses => _placeGuess.Count > 0;

    internal EntityState Guessed(in EntityState state)
        => _placeGuess.TryGetValue(state.Id, out var at) && (at.X, at.Y, at.Z) != (state.X, state.Y, state.Z)
            ? state with { X = at.X, Y = at.Y, Z = at.Z, PosX = at.X * MotionMath.SubunitsPerTile, PosY = at.Y * MotionMath.SubunitsPerTile, GlideSize = at.GlideSize }
            : state;

    // The newest guess that holds at GuessFrame wins: a cell a pushed thing left and then entered again (or entered then left) is what the later push made it.
    private bool? Guessed(int cell)
    {
        uint pass = _passableGuess.Count > 0 && _passableGuess.TryGetValue(cell, out uint p) && GuessFrame >= p ? p : 0;
        uint solid = _solidGuess.Count > 0 && _solidGuess.TryGetValue(cell, out uint s) && GuessFrame >= s ? s : 0;
        if (pass == 0 && solid == 0)
            return null;
        return solid > pass;
    }

    // A Snapshot (a resynchronisation) drops every guess: the hidden entities it still has come back, the guessed screen entries show the server's.
    private void ResetGuesses()
    {
        if (!HasGuesses)
            return;
        long[] hidden = _hidden.ToArray();
        long[] screen = _screenGuess.Keys.ToArray();
        _hidden.Clear();
        _doorGuess.Clear();
        _passableGuess.Clear();
        _solidGuess.Clear();
        _placeGuess.Clear();
        foreach (long id in hidden)
            if (_byId.TryGetValue(id, out ReplicaEntity entity))
                Spawned?.Invoke(entity);
        foreach (long id in screen)
            ClearScreenGuess(id);
    }
}
