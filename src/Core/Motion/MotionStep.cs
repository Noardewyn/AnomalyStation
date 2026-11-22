namespace Anomaly.Core.Motion;

// Owner: the contract (types, signatures, the meaning in these comments) is the M2 contracts step's and frozen; the bodies
// are M2 P4's (docs/ai/specs/m2-ownership.md). Units and the determinism rule: MotionTypes.cs.

/// <summary>The faces of a tile that block movement across them (reserved for M3's <c>ON_BORDER</c> objects: windows, railings, windoors).</summary>
[Flags]
public enum TileEdges : byte
{
    /// <summary>No blocked face (every tile in M2).</summary>
    None = 0,
    North = 1,
    South = 2,
    East = 4,
    West = 8,
}

/// <summary>Another capsule the controller collides with: a standing capsule mob near the mover.</summary>
/// <param name="Id">Its mob's world id (<c>dm_id</c>); the controller orders contacts by it.</param>
/// <param name="X">Its centre's x in 1/256 tile.</param>
/// <param name="Y">Its centre's y in 1/256 tile.</param>
/// <param name="Radius">Its radius in tiles.</param>
public readonly record struct CapsuleObstacle(long Id, int X, int Y, float Radius);

/// <summary>
/// What the controller collides with (scope 2a, 2d): tile solidity per level, the blocked faces of a tile (M3), and the
/// standing capsules near the mover. On the server the bridge implements it over the tg world (solidity: the predicate of
/// <c>Anomaly.Core.Tg.LocomotionSeam.IsSolid</c>, the same one <c>MapGrid</c> v2's solidity bits come from); on a
/// client over the replica (the MapGrid's solidity bits, the <c>SolidChanged</c> records, and the other capsules at their
/// newest received position). A view only reads; the controller never changes it.
/// </summary>
public interface ICollisionView
{
    /// <summary>True when the tile (x, y) of level z blocks capsules (a dense turf or a dense non-mob movable on it; scope 2a). Outside the map: true.</summary>
    bool IsSolid(int x, int y, int z);

    /// <summary>
    /// True when the tile (x, y) of level z is outside the map: the one wall of a <see cref="MotionParams.NoClip"/> capsule
    /// (the dev noclip, tg's <c>INCORPOREAL_MOVE_BASIC</c>). A view that cannot tell answers false.
    /// </summary>
    bool IsOutsideMap(int x, int y, int z) => false;

    /// <summary>
    /// D239 (protocol version 37): true when the tile (x, y) of level z is solid only through tg's <c>climb_walkable</c> structures
    /// (tables, closed crates): a <see cref="MotionParams.OnClimbable"/> capsule walks through it, as tg's <c>can_allow_through</c>
    /// lets a mover with <c>TRAIT_ON_CLIMBABLE</c> pass them. A view that cannot tell answers false.
    /// </summary>
    bool IsClimbWalkable(int x, int y, int z) => false;

    /// <summary>
    /// DV360 (protocol version 73): true when tg's airtight plastic flaps (<c>require_resting</c>) stand on the tile (x, y) of level z: a
    /// standing capsule without <see cref="MotionParams.PassesFlaps"/> does not enter it, as their <c>CanAllowThrough</c> refuses it. A view
    /// that cannot tell answers false.
    /// </summary>
    bool IsFlaps(int x, int y, int z) => false;

    /// <summary>DV534: whether a weightless capsule may hold on to the tile (x, y) of level z (tg's <c>get_spacemove_backup</c>): a solid cell, or a lattice where the view knows them.</summary>
    bool IsHandhold(int x, int y, int z) => IsSolid(x, y, z);

    /// <summary>
    /// The blocked faces of the tile (x, y, z): from M3 the faces of the dense <c>ON_BORDER_1</c> objects on it by their
    /// <c>dir</c> (the server: <c>Anomaly.Core.Tg.LocomotionSeam.FacesOf</c>; a client: MapGrid version 3's cell flags and their
    /// changes). <see cref="TileEdges.None"/> in M2. Outside the map: <see cref="TileEdges.None"/> (the tile itself is solid).
    /// </summary>
    TileEdges EdgesOf(int x, int y, int z);

    /// <summary>
    /// M3: whether movement from the tile (x, y, z) across its face <paramref name="dir"/> (one cardinal
    /// <see cref="MotionDir"/>) into the neighbour is blocked by a face, seen from either side: the tile's own face toward
    /// <paramref name="dir"/>, or the neighbour's opposite face (<see cref="TileFaces.IsFaceBlocked"/>, the one definition:
    /// the controller's sweeps, presses and crossings test exactly this). The tiles' solidity is not part of it. A view does
    /// not override it, so the server and the predictor decide a face the same way from the same <see cref="EdgesOf"/>.
    /// </summary>
    bool IsFaceBlocked(int x, int y, int z, byte dir) => TileFaces.IsFaceBlocked(this, x, y, z, dir);

    /// <summary>
    /// Adds to <paramref name="into"/> the standing capsules (not lying: D51) of level <paramref name="z"/> whose centres lie
    /// within <paramref name="range"/> subunits of (<paramref name="x"/>, <paramref name="y"/>) on both axes, except the one
    /// with id <paramref name="exclude"/> (the mover), in increasing id order (determinism).
    /// </summary>
    void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into);

    /// <summary>
    /// M10 slice E: the seq of the frame the predictor steps next (<see cref="MotionPredictor"/> sets it before each step and leaves the
    /// newest after a push or a replay). A client's view whose answers depend on it (an airlock guessed open, passable from a frame on)
    /// reads it; every other view ignores it.
    /// </summary>
    uint SteppingFrame { set { } }

    /// <summary>
    /// D325, DV370: the client's guess of the levels tg's code moves a grounded mob whose centre crosses from the tile (x, y, z) across its
    /// face <paramref name="dir"/>: +1 off the top of tg's stairs going that way (stairs.dm on_exit_stairs), minus the levels it falls onto
    /// open space (openspace.dm Entered, zFall), 0 for none. Only the predictor asks it: on the server tg's own Move chain moves the mob and
    /// the capsule follows. A view that cannot tell answers 0.
    /// </summary>
    int LevelShift(int x, int y, int z, byte dir) => 0;

    /// <summary>
    /// D325: whether tg's elevator platform (/obj/structure/transport/linear) or a ladder stands on the tile: the car's ride moves its riders
    /// a level (transport_module.dm group_move) on the server's timer, a ladder its climber at the end of a do_after (ladders.dm travel), which
    /// no input foretells, so the predictor takes either as a carry. A view that cannot tell answers false.
    /// </summary>
    bool OnLift(int x, int y, int z) => false;

    /// <summary>
    /// DEVIATION(DV403): the whole tiles a tram moves a capsule standing on tile (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>)
    /// before the frame being stepped (<see cref="SteppingFrame"/>): the server's tram steps in its world phase, before the capsules. None by default.
    /// </summary>
    (int Dx, int Dy) TramCarry(int x, int y, int z) => (0, 0);

    /// <summary>DV403: the tiles a capsule on that tile is drawn moved by <paramref name="phase"/> of the way through the next tick: its tram's even progress.</summary>
    (float Dx, float Dy) TramDrawShift(int x, int y, int z, float phase) => (0f, 0f);

    /// <summary>
    /// DV397: whether a ladder (tg's /obj/structure/ladder) stands on the tile: its rails are fixed obstacles across the tile's north side
    /// (<see cref="LadderBody"/>), the rest of the tile stays open. A view that cannot tell answers false.
    /// </summary>
    bool LadderAt(int x, int y, int z) => false;
}

/// <summary>
/// DEVIATION(DV397): a ladder's body in the capsule controller. tg's ladder is not dense (a mob walks onto its tile); in 3D it stands against the
/// north side of its tile (the models' rails, whatever its dir), and walking through it looked broken (the owner, 2026-11-11): its rails are three
/// small fixed circles along that side, too close together for a capsule to pass between, so only the ladder blocks and the rest of the tile is free.
/// </summary>
public static class LadderBody
{
    /// <summary>How far north of the tile's centre the rails' line runs (subunits: the models' rails at 0.86 m of a 2 m tile).</summary>
    public const int Line = 110;

    /// <summary>The east and west circles' distance from the tile's centre line (subunits: the stiles at 0.47 m).</summary>
    public const int HalfSpan = 60;

    /// <summary>A circle's radius in tiles (8 subunits, 6 cm: a stile's half thickness).</summary>
    public const float RailRadius = 8f / MotionMath.SubunitsPerTile;

    /// <summary>The obstacles' id: none of the world's (no contact is raised for them).</summary>
    public const long Id = -1;

    /// <summary>
    /// tg's icon state of a ladder linked to nothing (ladders.dm update_icon_state: "ladder00"), the only kind a single-level station has: a ladder
    /// lying on the floor, drawn flat as tg's picture (the owner, 2026-11-14) and with no rails to block a walker.
    /// </summary>
    public const string LyingState = "ladder00";

    /// <summary>
    /// The spot before the ladder of tile (x, y) a capsule of <paramref name="radiusSubunits"/> stands at touching its rails from the south: where a climber
    /// arrives on the ladder's other level, and where the client draws a climber over the climb.
    /// </summary>
    public static (int X, int Y) Front(int x, int y, int radiusSubunits)
        => (x * MotionMath.SubunitsPerTile, y * MotionMath.SubunitsPerTile + Line - (int)MathF.Round(RailRadius * MotionMath.SubunitsPerTile) - radiusSubunits);

    /// <summary>Adds the rails of the ladders on the tiles around tile (x, y) of level z (the mover's tile and its eight neighbours), in a fixed order.</summary>
    public static void Gather(ICollisionView view, int x, int y, int z, List<CapsuleObstacle> into)
    {
        for (int ty = y - 1; ty <= y + 1; ty++)
        {
            for (int tx = x - 1; tx <= x + 1; tx++)
            {
                if (!view.LadderAt(tx, ty, z))
                    continue;
                int cx = tx * MotionMath.SubunitsPerTile, cy = ty * MotionMath.SubunitsPerTile + Line;
                into.Add(new CapsuleObstacle(Id, cx - HalfSpan, cy, RailRadius));
                into.Add(new CapsuleObstacle(Id, cx, cy, RailRadius));
                into.Add(new CapsuleObstacle(Id, cx + HalfSpan, cy, RailRadius));
            }
        }
    }
}

/// <summary>What happened in one <see cref="MotionStep.Step"/>, in order.</summary>
public enum MotionEventKind : byte
{
    /// <summary>The centre crossed from tile (FromX, FromY) to the neighbour (ToX, ToY) in <see cref="MotionEvent.Dir"/> (one cardinal border; an exact corner gives the north/south crossing first, then east/west, as tg's diagonal <c>Move</c> splits, scope 2a). The bridge calls tg's <c>Move</c> with <c>PASSMOB</c> (DV16); a refused Move clamps the capsule back (<see cref="MotionStep.ClampToTile"/>).</summary>
    Crossed = 1,

    /// <summary>The mover pressed into a blocked face of its tile toward <see cref="MotionEvent.Dir"/> (its wish within 45 degrees of the face's normal) and the press timer ran out: the bridge attempts tg's <c>Move</c> into that tile, so <c>Bump</c> fires (D105). The timer is reset to <see cref="MotionParams.MoveDelayTicks"/>.</summary>
    Pressing = 2,

    /// <summary>The mover touched the standing capsule <see cref="MotionEvent.OtherId"/> while moving toward it: the bridge runs <c>MobBump</c>'s signals only (DV16; no swap, no walk-push, D105), at most once per move delay per pair.</summary>
    Contact = 3,
}

/// <summary>One event of a step (<see cref="MotionEventKind"/> says which fields mean something).</summary>
public readonly record struct MotionEvent(MotionEventKind Kind, int FromX, int FromY, int ToX, int ToY, byte Dir, long OtherId);

/// <summary>A reusable buffer of a step's events (allocation-free in a steady state). The caller clears it before a step.</summary>
public sealed class MotionEvents
{
    private MotionEvent[] _items = new MotionEvent[8];

    /// <summary>The events recorded.</summary>
    public int Count { get; private set; }

    /// <summary>The event at <paramref name="index"/> (0 to <see cref="Count"/> - 1).</summary>
    public MotionEvent this[int index] => index < Count ? _items[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>DV69: the step moved a weightless capsule by its drift (not by the wish): its crossings are tg's drift loop's moves.</summary>
    public bool Drifting { get; set; }

    /// <summary>Drops every event.</summary>
    public void Clear()
    {
        Count = 0;
        Drifting = false;
    }

    /// <summary>Records an event (the controller; tests).</summary>
    public void Add(in MotionEvent e)
    {
        if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        _items[Count++] = e;
    }
}

/// <summary>
/// The kinematic capsule controller (scope 2a B1, 2b; D105): a pure function of the state, one input frame, the parameters
/// and the collision view. tg moves at a constant speed; DEVIATION(DV263): a walking capsule's <see cref="MotionParams.Feel"/> ramps its
/// speed (the velocity is part of the state, so a prediction replays it) and slows it backward and sideways.
/// <para>
/// How it collides (docs/ai/specs/locomotion.md section 2): against tiles the capsule is the square of half-side
/// <see cref="MotionParams.Radius"/> around its centre (a tile is a square, so axis-separated sweeps against the grid are
/// exact in integers); against other capsules it is the circle of that radius. All collision work is done on the
/// quantized integer position (1/256 tile), so walls and capsules are hit at the same subunit on every machine; floats
/// (only <c>+ - * /</c> and <c>sqrt</c>) appear only where the wish vector is scaled and where a push-out direction is
/// normalized, and their results are quantized at once.
/// </para>
/// </summary>
public static class MotionStep
{
    /// <summary>
    /// One tick of one capsule. In order:
    /// <list type="number">
    /// <item><description>a running <see cref="MotionState.Slide"/> moves the capsule a share toward its target and ends the step (the input is ignored);</description></item>
    /// <item><description>otherwise, when <see cref="MotionParams.CanMove"/>, the wish vector (<see cref="MotionInput.MoveX"/>, <see cref="MotionInput.MoveY"/>, length clamped to 127) scaled to <see cref="MotionParams.TilesPerTick"/> is swept axis by axis against solid tiles and blocked faces, sliding along walls; then the capsule is pushed out of the standing capsules it overlaps (in id order; <see cref="MotionDriver.Prone"/> skips capsules);</description></item>
    /// <item><description>the yaw becomes the input's; the position is quantized (<see cref="MotionMath.Quantize"/>).</description></item>
    /// </list>
    /// Every tile border the centre crossed is a <see cref="MotionEventKind.Crossed"/> event in crossing order; a press into
    /// a blocked face whose timer ran out a <see cref="MotionEventKind.Pressing"/>; a touch of a standing capsule a
    /// <see cref="MotionEventKind.Contact"/>. <paramref name="events"/> may be null (the predictor needs none). Same
    /// inputs, same result, bit for bit (the determinism rule of MotionTypes.cs).
    /// <para>
    /// The details the bodies fix (docs/ai/specs/locomotion.md section 2): the yaw is taken from the input in every step, a
    /// slide included (looking is not moving); a slide raises no event (tg already moved the mob, the capsule only plays
    /// it); the sweep goes north/south first, then east/west (tg's diagonal step order); the push-out from capsules runs
    /// whenever the capsule may move, with or without a wish, and a push-out that cannot end clear of every capsule (a
    /// wall behind) leaves the capsule where it started when it started clear; the events come as the crossings in path
    /// order (an exact corner north/south first), then the presses (north/south first), then the contacts (in id order).
    /// <see cref="MotionDriver.Thrown"/> and <see cref="MotionDriver.ZeroG"/> (reserved, M4) move nothing yet. This
    /// overload excludes no capsule itself: the view leaves the mover out (a client's replica view), or the caller uses
    /// the overload with the mover's id (the server's view of every registered capsule).
    /// </para>
    /// </summary>
    public static void Step(ref MotionState state, in MotionInput input, in MotionParams parameters, ICollisionView view, MotionEvents events)
        => Step(ref state, in input, in parameters, view, events, 0);

    /// <summary>
    /// <see cref="Step(ref MotionState, in MotionInput, in MotionParams, ICollisionView, MotionEvents)"/> for the capsule of
    /// mob <paramref name="selfId"/>, which <see cref="ICollisionView.GatherCapsules"/> is asked to leave out (0: none; the
    /// server's bridge passes the mob's <c>dm_id</c>).
    /// </summary>
    public static void Step(ref MotionState state, in MotionInput input, in MotionParams parameters, ICollisionView view, MotionEvents events, long selfId)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (state.PressTimer > 0)
            state.PressTimer--;
        state.Yaw = input.Yaw;

        // DV263: only a walking capsule with a ramp keeps a velocity; a slide, a throw, zero gravity and a stop drop it.
        bool walking = state.Driver == MotionDriver.Grounded || state.Driver == MotionDriver.Prone;
        bool ramped = walking && parameters.Feel.Ramps && parameters.CanMove && !state.Slide.Active;
        if (!ramped)
            state.Stop();
        if (state.Slide.Active)
        {
            AdvanceSlide(ref state);
            return;
        }
        if (state.Driver == MotionDriver.Thrown)
            return; // Thrown (M4a) is reserved: tg's tile steps move those mobs, the bridge follows loc

        // The dev noclip (tg's INCORPOREAL_MOVE_BASIC): nothing but the map's edge stops the capsule; no capsules, no faces.
        if (parameters.NoClip)
            view = new NoClipView(view);
        else if (parameters.OnClimbable)
            view = new ClimberView(view); // DEVIATION(DV153): a climber walks over the climb_walkable tiles (D239)
        if (!parameters.NoClip && !parameters.PassesFlaps && state.Posture != Posture.Lying)
            view = new FlapsView(view); // DEVIATION(DV360): the flaps stop a standing capsule at their tile, before tg's refused Move
        int radius = RadiusSubunits(parameters.Radius);
        int x0 = state.X;
        int y0 = state.Y;
        int z = state.Z;

        // The wish vector: its length clamped to MotionInput.MaxMove, scaled to the speed, quantized.
        int wishX = 0;
        int wishY = 0;
        if (ramped)
            Ramp(ref state, in input, in parameters, out wishX, out wishY);
        else if (parameters.CanMove)
            Wish(input.MoveX, input.MoveY, input.Yaw, walking ? parameters.Feel : default, parameters.TilesPerTick, out wishX, out wishY);
        // DV69: a weightless capsule that no key moves goes on at tg's drift.
        bool drifting = false;
        if (state.Driver == MotionDriver.ZeroG && wishX == 0 && wishY == 0 && (parameters.DriftX != 0 || parameters.DriftY != 0))
        {
            wishX = parameters.DriftX;
            wishY = parameters.DriftY;
            drifting = true;
            if (events != null)
                events.Drifting = true;
        }
        // DV347: a running belt under the centre carries a walking capsule along it, keys or not.
        bool belted = walking && Belt(in state, in input, in parameters, ref wishX, ref wishY);
        // DV357: the wind and a gravity pull carry it too; its own wish is added, so it can lean against a weaker one.
        belted |= walking && Blow(in state, in parameters, ref wishX, ref wishY);

        // The standing capsules near the whole path (a prone capsule takes no part, D51), gathered once.
        List<CapsuleObstacle> others = null;
        int pieces = 1;
        if ((parameters.CanMove || drifting || belted) && (state.Driver == MotionDriver.Grounded || state.Driver == MotionDriver.ZeroG))
        {
            others = new List<CapsuleObstacle>();
            int reach = Math.Abs(wishX) + Math.Abs(wishY) + 2 * radius + MaxOtherRadiusSubunits + 2;
            view.GatherCapsules(x0, y0, z, reach, selfId, others);
            LadderBody.Gather(view, MotionMath.TileOf(x0), MotionMath.TileOf(y0), z, others); // DV397: a ladder's rails
            pieces = PiecesOf(wishX, wishY, others, radius);
        }

        int x = x0;
        int y = y0;
        // A press into a blocked face needs the wish within 45 degrees of the face's normal (an exact diagonal into a
        // corner presses both faces, as tg's diagonal step bumps on both).
        byte pressed = MotionDir.None;
        // A drift and a ramped capsule press the way they move (DV263: a capsule still coasting after its keys went up presses ahead).
        bool byMotion = drifting || ramped || belted;
        bool alongY = byMotion ? Math.Abs(wishY) >= Math.Abs(wishX) : Math.Abs((int)input.MoveY) >= Math.Abs((int)input.MoveX);
        bool alongX = byMotion ? Math.Abs(wishX) >= Math.Abs(wishY) : Math.Abs((int)input.MoveX) >= Math.Abs((int)input.MoveY);
        List<long> contacts = null;
        // One piece, the whole step, unless it is as long as two bodies' touch distance (see PiecesOf). Each piece: the
        // sweep against the tiles, north/south first; the contacts (a touch while moving toward a capsule); the push-out.
        for (int piece = 1; piece <= pieces; piece++)
        {
            int pieceX = pieces == 1 ? wishX : wishX * piece / pieces - wishX * (piece - 1) / pieces;
            int pieceY = pieces == 1 ? wishY : wishY * piece / pieces - wishY * (piece - 1) / pieces;
            int pieceX0 = x;
            int pieceY0 = y;
            if (pieceY != 0)
            {
                y = SweepY(x, y, z, radius, pieceY, view, out bool blocked);
                byte dir = pieceY > 0 ? MotionDir.North : MotionDir.South;
                if (blocked && alongY && FacesBlocked(view, x, y, z, dir, radius))
                    pressed |= dir;
            }
            if (pieceX != 0)
            {
                x = SweepX(x, y, z, radius, pieceX, view, out bool blocked);
                byte dir = pieceX > 0 ? MotionDir.East : MotionDir.West;
                if (blocked && alongX && FacesBlocked(view, x, y, z, dir, radius))
                    pressed |= dir;
            }

            // Capsules: the contacts, then the push-out.
            if (others != null && others.Count > 0)
            {
                if (events != null && (pieceX != 0 || pieceY != 0))
                {
                    foreach (CapsuleObstacle other in others)
                    {
                        long touch = radius + RadiusSubunits(other.Radius) + 1;
                        long ox = x - other.X;
                        long oy = y - other.Y;
                        long toward = (long)pieceX * (other.X - pieceX0) + (long)pieceY * (other.Y - pieceY0);
                        if (other.Id != LadderBody.Id && ox * ox + oy * oy < touch * touch && toward > 0 && (contacts == null || !contacts.Contains(other.Id)))
                            (contacts ??= new List<long>()).Add(other.Id);
                    }
                }
                PushOut(ref x, ref y, pieceX0, pieceY0, z, radius, pieceX, pieceY, others, view);
            }
        }
        contacts?.Sort(); // id order, whatever the piece that found it

        // DV263: a capsule that wanted to move and did not (a wall or a body ahead) has stopped; a slide along a wall keeps its speed.
        // DV347: not a rider a belt pins to a wall: stopped each tick, its own speed would never grow past the belt's and it could not walk off.
        if (ramped && !belted && x == x0 && y == y0 && (wishX != 0 || wishY != 0))
            state.Stop();
        state.X = x;
        state.Y = y;

        if (events != null)
            AddCrossings(x0, y0, x, y, z, view, events);
        // A face found blocked during the sweeps is pressed only if it still blocks the tile the centre ends in: the north
        // sweep runs first, so a capsule that slides along a wall corner into the next tile found the old tile's face
        // blocked while the face of the tile it now stands in is open (tg's Move there would succeed: the mob would jump
        // ahead of its capsule). M2 integration.
        pressed = StillPressed(view, x, y, z, pressed, radius);
        AddPresses(ref state, pressed, parameters.MoveDelayTicks, events);
        if (contacts != null)
        {
            foreach (long id in contacts)
                events.Add(new MotionEvent(MotionEventKind.Contact, 0, 0, 0, 0, MotionDir.None, id));
        }
    }

    /// <summary>
    /// DEVIATION(DV535): whether a step whose press pushed what it pressed (tg's PushAM) is taken again through the freed tile
    /// (<see cref="StepAgain"/>), so the press costs the walker no time: a walking capsule whose step (its events from <paramref name="first"/>)
    /// crossed no border, as the server's bridge and the predictor both decide.
    /// </summary>
    public static bool WalksOnAfterPush(in MotionState state, MotionEvents events, int first)
    {
        if (state.Driver != MotionDriver.Grounded || events == null || events.Drifting)
            return false;
        for (int i = first; i < events.Count; i++)
            if (events[i].Kind == MotionEventKind.Crossed)
                return false;
        return true;
    }

    /// <summary>
    /// DV535: the step from <paramref name="before"/> taken again, its press already made: the press timer the first step set is kept (no
    /// second press), the rest as <see cref="Step(ref MotionState, in MotionInput, in MotionParams, ICollisionView, MotionEvents, long)"/>
    /// through the view that now has the pushed tile open; its events are added to <paramref name="events"/>.
    /// </summary>
    public static void StepAgain(ref MotionState state, in MotionState before, in MotionInput input, in MotionParams parameters, ICollisionView view, MotionEvents events, long selfId = 0)
    {
        short timer = state.PressTimer;
        state = before;
        state.PressTimer = (short)(timer + 1); // the step counts it down first
        Step(ref state, in input, in parameters, view, events, selfId);
    }

    /// <summary>
    /// Puts the capsule back inside the tile (x, y): the bridge's answer to a refused <c>Move</c> (a stale grid, a
    /// <c>PRE_MOVE</c> veto), so the invariant holds (the centre's tile equals <c>loc</c>). The centre moves to the nearest
    /// point of the tile, kept off its border by the radius where the tile allows it. A running slide ends.
    /// </summary>
    public static void ClampToTile(ref MotionState state, int x, int y, float radius)
    {
        int r = Math.Min(RadiusSubunits(radius), Half - 1);
        int cx = x * MotionMath.SubunitsPerTile;
        int cy = y * MotionMath.SubunitsPerTile;
        state.X = Math.Clamp(state.X, cx - Half + r, cx + Half - 1 - r);
        state.Y = Math.Clamp(state.Y, cy - Half + r, cy + Half - 1 - r);
        state.Slide = default;
        state.Stop(); // DV263
    }

    /// <summary>
    /// The displacement a tg-made move of the mob starts (scope 2a "moved by others"): a neighbouring tile on the same level
    /// starts a <see cref="Slide"/> from where the centre is to the new tile's centre over <paramref name="glideTicks"/>
    /// ticks (tg's glide time for that move: a shove slides the full tile, D105). A neighbour is a cardinal one or a
    /// diagonal one: tg's diagonal <c>Move</c> (a diagonal shove under DV17) is two cardinal steps in one call, which the
    /// bridge sees as one displacement. Anything else (a teleport, <c>forceMove</c> into a farther tile, another level, a
    /// glide time of 0) snaps the centre to the new tile's centre with no slide; the same tile changes nothing.
    /// </summary>
    public static void Displace(ref MotionState state, int toX, int toY, int toZ, ushort glideTicks)
        => Displace(ref state, toX, toY, toZ, glideTicks, 1);

    /// <summary>
    /// <see cref="Displace(ref MotionState, int, int, int, ushort)"/> that also slides a move of up to <paramref name="reach"/> tiles
    /// (Chebyshev, counted from the tile the capsule is at or sliding to), over <paramref name="glideTicks"/> per tile. M4c slice 7: a
    /// weightless capsule (<see cref="MotionDriver.ZeroG"/>) can be moved twice in one tick by tg (a client/Move step and the drift's first
    /// step, a drift that steps on the tick it starts), which BYOND glides from where the mob is drawn; the bridge passes 2 for it.
    /// </summary>
    public static void Displace(ref MotionState state, int toX, int toY, int toZ, ushort glideTicks, int reach)
    {
        int tx = toX * MotionMath.SubunitsPerTile;
        int ty = toY * MotionMath.SubunitsPerTile;
        int fromX = state.Slide.Active ? MotionMath.TileOf(state.Slide.TargetX) : state.TileX;
        int fromY = state.Slide.Active ? MotionMath.TileOf(state.Slide.TargetY) : state.TileY;
        if (toZ == state.Z && toX == fromX && toY == fromY)
            return;
        int tiles = Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY));
        // DEVIATION(DV370): a move to another level onto the tile under, over or beside the capsule (tg's stairs, a fall, a ladder) keeps
        // where on the tile it stands, brought into the new tile, and its speed: the climb up the stairs' flight goes on where the level
        // above begins, a fall drops straight down, and the client's guess of them (ICollisionView.LevelShift) matches
        if (toZ != state.Z && tiles <= 1)
        {
            state.X = Math.Clamp(state.X, tx - Half, tx + Half - 1);
            state.Y = Math.Clamp(state.Y, ty - Half, ty + Half - 1);
            state.Z = toZ;
            state.Slide = default;
            return;
        }
        state.Stop(); // DV263: tg moved the mob; its own speed starts over
        bool near = toZ == state.Z && tiles <= Math.Max(1, reach);
        if (near && glideTicks > 0)
        {
            state.Slide = new Slide(tx, ty, (ushort)Math.Min(ushort.MaxValue, glideTicks * tiles));
            return;
        }
        state.X = tx;
        state.Y = ty;
        state.Z = toZ;
        state.Slide = default;
    }

    /// <summary>
    /// DEVIATION(DV403): a tram's step carries the capsule by whole tiles, keeping where on the tile it stands, its speed and a slide it is in
    /// (moved with it): the server's bridge when tg's group_move moves the mob, the own predictor on the same tick (<see cref="TramRide.CarriesAt"/>).
    /// </summary>
    public static void Carry(ref MotionState state, int dx, int dy)
    {
        state.X += dx * MotionMath.SubunitsPerTile;
        state.Y += dy * MotionMath.SubunitsPerTile;
        if (state.Slide.Active)
            state.Slide = state.Slide with { TargetX = state.Slide.TargetX + dx * MotionMath.SubunitsPerTile, TargetY = state.Slide.TargetY + dy * MotionMath.SubunitsPerTile };
    }

    // ---- internals ----

    // Half a tile in subunits: a tile x covers [256x - Half, 256x + Half).
    private const int Half = MotionMath.SubunitsPerTile / 2;

    // The largest radius of another capsule the gather range allows for (a tile; capsules are 0.175 in M2).
    private const int MaxOtherRadiusSubunits = MotionMath.SubunitsPerTile;

    /// <summary>A radius in tiles as whole subunits (0.175 tile is 45), at least 0.</summary>
    internal static int RadiusSubunits(float radius) => Math.Max(0, MotionMath.Quantize(radius));

    // DEVIATION(DV347): tg's convey loop moves what lies on a running belt a tile per its delay; a capsule rides it instead at that speed,
    // added to its own wish. With no key held the belt also draws the centre toward its middle line at up to half its speed, so a corner
    // turns the rider onto the next belt's line (a key held across a belt is not held back). True when a belt carries it this tick.
    private static bool Belt(in MotionState state, in MotionInput input, in MotionParams parameters, ref int wishX, ref int wishY)
    {
        byte dir = parameters.Belt.DirAt(state.TileX, state.TileY);
        if (dir == MotionDir.None)
            return false;
        int speed = parameters.Belt.Speed;
        int pull = input.MoveX == 0 && input.MoveY == 0 ? speed / 2 : 0;
        int toLineX = Math.Clamp(state.TileX * MotionMath.SubunitsPerTile - state.X, -pull, pull);
        int toLineY = Math.Clamp(state.TileY * MotionMath.SubunitsPerTile - state.Y, -pull, pull);
        switch (dir)
        {
            case MotionDir.North:
                wishY += speed;
                wishX += toLineX;
                break;
            case MotionDir.South:
                wishY -= speed;
                wishX += toLineX;
                break;
            case MotionDir.East:
                wishX += speed;
                wishY += toLineY;
                break;
            case MotionDir.West:
                wishX -= speed;
                wishY += toLineY;
                break;
            default:
                return false;
        }
        return true;
    }

    // DEVIATION(DV357): tg's space wind and gravity pulls step a mob a tile now and then (experience_pressure_difference's step, step_towards);
    // a walking capsule is carried continuously instead: along the wind of the tile under the centre, and toward the pull's tile centre.
    // True when either moves it this tick.
    private static bool Blow(in MotionState state, in MotionParams parameters, ref int wishX, ref int wishY)
    {
        bool blown = false;
        if (parameters.Wind.Active)
        {
            int speed = parameters.Wind.Speed;
            switch (parameters.Wind.DirAt(state.TileX, state.TileY))
            {
                case MotionDir.North: wishY += speed; blown = true; break;
                case MotionDir.South: wishY -= speed; blown = true; break;
                case MotionDir.East: wishX += speed; blown = true; break;
                case MotionDir.West: wishX -= speed; blown = true; break;
            }
        }
        if (parameters.PullSpeed > 0)
        {
            long dx = (long)parameters.PullX * MotionMath.SubunitsPerTile - state.X;
            long dy = (long)parameters.PullY * MotionMath.SubunitsPerTile - state.Y;
            long distance = (long)Math.Sqrt(dx * dx + dy * dy); // whole subunits: the same on every machine
            if (distance > 0)
            {
                long speed = Math.Min(parameters.PullSpeed, distance);
                wishX += (int)(dx * speed / distance);
                wishY += (int)(dy * speed / distance);
                blown = true;
            }
        }
        return blown;
    }

    private static void AdvanceSlide(ref MotionState state)
    {
        Slide slide = state.Slide;
        int left = slide.TicksLeft;
        state.X += (slide.TargetX - state.X) / left; // an equal share of what is left; the last tick lands on the target
        state.Y += (slide.TargetY - state.Y) / left;
        left--;
        state.Slide = left > 0 ? new Slide(slide.TargetX, slide.TargetY, (ushort)left) : default;
        if (left == 0)
        {
            state.X = slide.TargetX;
            state.Y = slide.TargetY;
        }
    }

    // The wish vector in subunits for this tick: (moveX, moveY) with its length clamped to MaxMove, slowed by the feel's direction
    // (DV263), times tilesPerTick / MaxMove.
    internal static void Wish(sbyte moveX, sbyte moveY, ushort yaw, in MoveFeel feel, float tilesPerTick, out int wishX, out int wishY)
    {
        wishX = 0;
        wishY = 0;
        if (!WishTiles(moveX, moveY, yaw, in feel, tilesPerTick, out float tx, out float ty))
            return;
        wishX = MotionMath.Quantize(tx);
        wishY = MotionMath.Quantize(ty);
    }

    // The wish in tiles a tick as floats; false when there is none.
    private static bool WishTiles(sbyte moveX, sbyte moveY, ushort yaw, in MoveFeel feel, float tilesPerTick, out float tx, out float ty)
    {
        tx = 0f;
        ty = 0f;
        if ((moveX == 0 && moveY == 0) || !(tilesPerTick > 0f))
            return false;
        float mx = moveX;
        float my = moveY;
        float lengthSquared = mx * mx + my * my;
        const float max = MotionInput.MaxMove;
        if (lengthSquared > max * max)
        {
            float length = MathF.Sqrt(lengthSquared);
            mx = mx * max / length;
            my = my * max / length;
        }
        if (feel.Directional)
        {
            // DEVIATION(DV263): the part along the view's back and the part to its side are slowed (tg's speed is the same every way).
            (float fx, float fy) = MotionMath.Facing(yaw);
            float along = mx * fx + my * fy;
            float side = mx * fy - my * fx; // the right of (fx, fy) is (fy, -fx)
            if (along < 0f)
                along *= 1f - feel.BackSlow;
            side *= 1f - feel.StrafeSlow;
            mx = along * fx + side * fy;
            my = along * fy - side * fx;
        }
        float scale = tilesPerTick / max;
        tx = mx * scale;
        ty = my * scale;
        return true;
    }

    // DV263: one tick of the walking capsule's velocity toward the wish, then the step it moves (subunits). Integers on the state, floats
    // only through + - * / sqrt and quantized at once (the determinism rule). Within a quarter turn the velocity takes the wish's
    // direction at once and only its speed ramps (a turn of the view while running does not drift); a wish more than a quarter turn
    // away, or none, brakes the velocity toward it at the decel rate.
    private static void Ramp(ref MotionState state, in MotionInput input, in MotionParams parameters, out int stepX, out int stepY)
    {
        int tvx = 0, tvy = 0;
        if (WishTiles(input.MoveX, input.MoveY, input.Yaw, parameters.Feel, parameters.TilesPerTick, out float tx, out float ty))
        {
            tvx = QuantizeVelocity(tx);
            tvy = QuantizeVelocity(ty);
        }
        long full = Math.Max(1, QuantizeVelocity(parameters.TilesPerTick));
        // rounded up, so full speed is reached (and lost) in exactly the feel's ticks
        long accel = parameters.Feel.AccelTicks > 0 ? (full + parameters.Feel.AccelTicks - 1) / parameters.Feel.AccelTicks : Instant;
        long decel = parameters.Feel.DecelTicks > 0 ? (full + parameters.Feel.DecelTicks - 1) / parameters.Feel.DecelTicks : Instant;
        long vx = state.VelX, vy = state.VelY;
        long dot = vx * tvx + vy * tvy;
        if ((tvx != 0 || tvy != 0) && (vx != 0 || vy != 0) && dot >= 0)
        {
            float speed = MathF.Sqrt((float)(vx * vx + vy * vy));
            float target = MathF.Sqrt((float)((long)tvx * tvx + (long)tvy * tvy));
            float next = speed < target ? MathF.Min(target, speed + accel) : MathF.Max(target, speed - decel);
            if (next == target)
            {
                vx = tvx;
                vy = tvy;
            }
            else
            {
                vx = RoundVelocity(tvx * next / target);
                vy = RoundVelocity(tvy * next / target);
            }
        }
        else
        {
            long rate = (tvx == 0 && tvy == 0) || dot < 0 ? decel : accel;
            long dx = tvx - vx, dy = tvy - vy;
            long d2 = dx * dx + dy * dy;
            if (d2 <= rate * rate)
            {
                vx = tvx;
                vy = tvy;
            }
            else
            {
                float d = MathF.Sqrt(d2);
                vx += RoundVelocity(dx * (rate / d));
                vy += RoundVelocity(dy * (rate / d));
            }
        }
        state.VelX = (int)vx;
        state.VelY = (int)vy;
        // a subunit is 256 velocity units: round halves up, as Quantize does
        stepX = (int)((vx + 128) >> 8);
        stepY = (int)((vy + 128) >> 8);
    }

    // Tiles a tick as velocity units (1/65536 tile a tick), halves up.
    private static int QuantizeVelocity(float tilesPerTick) => RoundVelocity(tilesPerTick * MotionMath.VelocityPerTile);

    private static int RoundVelocity(float units) => (int)MathF.Floor(units + 0.5f);

    // A ramp of 0 ticks: more than any change of velocity in a tick (its square still fits a long).
    private const long Instant = 1L << 30;

    internal static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    internal static int CeilDiv(int a, int b) => -FloorDiv(-a, b);

    // The tiles whose interior an open interval (centre - r, centre + r) overlaps on one axis: [first, last].
    private static void Span(int centre, int r, out int first, out int last)
    {
        first = FloorDiv(centre - r - Half, MotionMath.SubunitsPerTile) + 1;
        last = CeilDiv(centre + r + Half, MotionMath.SubunitsPerTile) - 1;
    }

    // Moves y by dy against the tiles: the box's leading face may reach a blocked line but not pass it. A line
    // y = 256k + 128 is the north face of row k and the south face of row k + 1.
    private static int SweepY(int x, int y, int z, int r, int dy, ICollisionView view, out bool blocked)
    {
        blocked = false;
        Span(x, r, out int c0, out int c1);
        if (dy > 0)
        {
            int edge = y + r;
            int target = edge + dy;
            for (int k = CeilDiv(edge - Half, MotionMath.SubunitsPerTile); ; k++)
            {
                int line = k * MotionMath.SubunitsPerTile + Half;
                if (line >= target) break;
                for (int c = c0; c <= c1; c++)
                {
                    if (view.IsSolid(c, k + 1, z) || (view.EdgesOf(c, k, z) & TileEdges.North) != 0 || (view.EdgesOf(c, k + 1, z) & TileEdges.South) != 0
                        || c < c1 && SplitsRow(view, c, k + 1, z))
                    {
                        blocked = true;
                        return line - r;
                    }
                }
            }
        }
        else
        {
            int edge = y - r;
            int target = edge + dy;
            for (int k = FloorDiv(edge + Half, MotionMath.SubunitsPerTile); ; k--)
            {
                int line = k * MotionMath.SubunitsPerTile - Half;
                if (line <= target) break;
                for (int c = c0; c <= c1; c++)
                {
                    if (view.IsSolid(c, k - 1, z) || (view.EdgesOf(c, k, z) & TileEdges.South) != 0 || (view.EdgesOf(c, k - 1, z) & TileEdges.North) != 0
                        || c < c1 && SplitsRow(view, c, k - 1, z))
                    {
                        blocked = true;
                        return line + r;
                    }
                }
            }
        }
        return y + dy;
    }

    // The same for x: a line x = 256k + 128 is the east face of column k and the west face of column k + 1.
    private static int SweepX(int x, int y, int z, int r, int dx, ICollisionView view, out bool blocked)
    {
        blocked = false;
        Span(y, r, out int r0, out int r1);
        if (dx > 0)
        {
            int edge = x + r;
            int target = edge + dx;
            for (int k = CeilDiv(edge - Half, MotionMath.SubunitsPerTile); ; k++)
            {
                int line = k * MotionMath.SubunitsPerTile + Half;
                if (line >= target) break;
                for (int row = r0; row <= r1; row++)
                {
                    if (view.IsSolid(k + 1, row, z) || (view.EdgesOf(k, row, z) & TileEdges.East) != 0 || (view.EdgesOf(k + 1, row, z) & TileEdges.West) != 0
                        || row < r1 && SplitsColumn(view, k + 1, row, z))
                    {
                        blocked = true;
                        return line - r;
                    }
                }
            }
        }
        else
        {
            int edge = x - r;
            int target = edge + dx;
            for (int k = FloorDiv(edge + Half, MotionMath.SubunitsPerTile); ; k--)
            {
                int line = k * MotionMath.SubunitsPerTile - Half;
                if (line <= target) break;
                for (int row = r0; row <= r1; row++)
                {
                    if (view.IsSolid(k - 1, row, z) || (view.EdgesOf(k, row, z) & TileEdges.West) != 0 || (view.EdgesOf(k - 1, row, z) & TileEdges.East) != 0
                        || row < r1 && SplitsColumn(view, k - 1, row, z))
                    {
                        blocked = true;
                        return line + r;
                    }
                }
            }
        }
        return x + dx;
    }

    // Whether the line between columns c and c + 1 is blocked in row: the box that straddles it may not move into that row (the owner's report,
    // 2026-11-10: a walker overhanging the next column stepped along a railing's line into the row it guards, past its end; tg's Move refused it).
    private static bool SplitsRow(ICollisionView view, int c, int row, int z)
        => (view.EdgesOf(c, row, z) & TileEdges.East) != 0 || (view.EdgesOf(c + 1, row, z) & TileEdges.West) != 0;

    // The same for the line between rows row and row + 1 in column.
    private static bool SplitsColumn(ICollisionView view, int column, int row, int z)
        => (view.EdgesOf(column, row, z) & TileEdges.North) != 0 || (view.EdgesOf(column, row + 1, z) & TileEdges.South) != 0;

    // Whether the capsule, stopped by a sweep toward dir, rests against a blocked face of the tile its centre is in: the
    // box touches that face and the tile across it is solid or the face itself is blocked (a block by a diagonal
    // neighbour, the box overlapping the next row, is not a press: the tile ahead is open).
    private static bool FacesBlocked(ICollisionView view, int x, int y, int z, byte dir, int r)
    {
        int tx = MotionMath.TileOf(x);
        int ty = MotionMath.TileOf(y);
        int cx = tx * MotionMath.SubunitsPerTile;
        int cy = ty * MotionMath.SubunitsPerTile;
        switch (dir)
        {
            case MotionDir.North:
                return y + r == cy + Half && (view.IsSolid(tx, ty + 1, z) || (view.EdgesOf(tx, ty, z) & TileEdges.North) != 0 || (view.EdgesOf(tx, ty + 1, z) & TileEdges.South) != 0);
            case MotionDir.South:
                return y - r == cy - Half && (view.IsSolid(tx, ty - 1, z) || (view.EdgesOf(tx, ty, z) & TileEdges.South) != 0 || (view.EdgesOf(tx, ty - 1, z) & TileEdges.North) != 0);
            case MotionDir.East:
                return x + r == cx + Half && (view.IsSolid(tx + 1, ty, z) || (view.EdgesOf(tx, ty, z) & TileEdges.East) != 0 || (view.EdgesOf(tx + 1, ty, z) & TileEdges.West) != 0);
            default:
                return x - r == cx - Half && (view.IsSolid(tx - 1, ty, z) || (view.EdgesOf(tx, ty, z) & TileEdges.West) != 0 || (view.EdgesOf(tx - 1, ty, z) & TileEdges.East) != 0);
        }
    }

    // The pressed faces that still block the tile of the final position.
    private static byte StillPressed(ICollisionView view, int x, int y, int z, byte pressed, int r)
    {
        byte still = MotionDir.None;
        foreach (byte dir in PressOrder)
        {
            if ((pressed & dir) != 0 && FacesBlocked(view, x, y, z, dir, r))
                still |= dir;
        }
        return still;
    }

    // One timer serves every face: a press whose timer ran out raises an event per pressed face (north/south first) and
    // resets the timer to tg's move delay. Without an event buffer (the predictor) the timer runs all the same.
    private static void AddPresses(ref MotionState state, byte pressed, short moveDelayTicks, MotionEvents events)
    {
        if (pressed == MotionDir.None || state.PressTimer > 0)
            return;
        if (events != null)
        {
            int tx = state.TileX;
            int ty = state.TileY;
            foreach (byte dir in PressOrder)
            {
                if ((pressed & dir) == 0) continue;
                (int nx, int ny) = Neighbour(tx, ty, dir);
                events.Add(new MotionEvent(MotionEventKind.Pressing, tx, ty, nx, ny, dir, 0));
            }
        }
        state.PressTimer = Math.Max((short)1, moveDelayTicks);
    }

    private static ReadOnlySpan<byte> PressOrder => new byte[] { MotionDir.North, MotionDir.South, MotionDir.East, MotionDir.West };

    private static (int X, int Y) Neighbour(int x, int y, byte dir) => dir switch
    {
        MotionDir.North => (x, y + 1),
        MotionDir.South => (x, y - 1),
        MotionDir.East => (x + 1, y),
        _ => (x - 1, y),
    };

    // How many pieces a step is cut into. The push-out puts a mover that overlaps a capsule on the circle of touch around the
    // capsule's centre, in the direction of its end position from the centre. A mover that starts clear (at least the touch
    // distance away) and steps less than that distance cannot end beyond the centre's line, so the push-out keeps it on its
    // side of the body: the usual speeds (a run is 85 subunits, the touch distance 90) are one piece. A longer step (a slowdown
    // under the run delay, MOVE_SPEED_SCALE above 1, D106) could land past the centre and be pushed out on the far side, through
    // the body (D51): it is cut into equal pieces, each shorter than the shortest touch distance of the capsules near the path,
    // and every piece is swept and pushed out in turn. Whole subunits, a few pieces at most (a tile a tick at scale 4 is six).
    private static int PiecesOf(int wishX, int wishY, List<CapsuleObstacle> others, int radius)
    {
        if (others.Count == 0 || (wishX == 0 && wishY == 0))
            return 1;
        long touch = long.MaxValue;
        foreach (CapsuleObstacle other in others)
            touch = Math.Min(touch, radius + RadiusSubunits(other.Radius));
        long length2 = (long)wishX * wishX + (long)wishY * wishY;
        if (touch < 2 || length2 < touch * touch)
            return 1;
        long manhattan = Math.Abs((long)wishX) + Math.Abs((long)wishY); // at least the length: each piece's length is under the touch distance
        return (int)Math.Min(MaxPieces, (manhattan + touch - 2) / (touch - 1));
    }

    private const int MaxPieces = 64;

    // Pushes (x, y) out of every capsule it overlaps, in id order, a few passes; each push is swept against the tiles. When
    // the result still overlaps a capsule and the start did not, the start is kept (no motion into a body).
    private static void PushOut(ref int x, ref int y, int x0, int y0, int z, int r, int wishX, int wishY, List<CapsuleObstacle> others, ICollisionView view)
    {
        bool startClear = Clear(x0, y0, r, others);
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            foreach (CapsuleObstacle other in others)
            {
                long min = r + RadiusSubunits(other.Radius);
                long dx = x - other.X;
                long dy = y - other.Y;
                long d2 = dx * dx + dy * dy;
                if (d2 >= min * min)
                    continue;
                float nx;
                float ny;
                if (d2 == 0)
                {
                    // Coincident centres: back out along the wish, else north (deterministic).
                    if (wishX == 0 && wishY == 0) { nx = 0f; ny = 1f; }
                    else
                    {
                        float wl = MathF.Sqrt((float)wishX * wishX + (float)wishY * wishY);
                        nx = -wishX / wl;
                        ny = -wishY / wl;
                    }
                }
                else
                {
                    float d = MathF.Sqrt(d2);
                    nx = dx / d;
                    ny = dy / d;
                }
                int px = other.X + AwayFromZero(nx * min);
                int py = other.Y + AwayFromZero(ny * min);
                // Rounding error: step outward until clear of this capsule.
                for (int guard = 0; guard < 4; guard++)
                {
                    long qx = px - other.X;
                    long qy = py - other.Y;
                    if (qx * qx + qy * qy >= min * min) break;
                    px += Math.Sign(qx == 0 && qy == 0 ? 0 : qx);
                    py += Math.Sign(qx == 0 && qy == 0 ? 1 : qy);
                }
                int sy = py - y;
                int sx = px - x;
                if (sy != 0) y = SweepY(x, y, z, r, sy, view, out _);
                if (sx != 0) x = SweepX(x, y, z, r, sx, view, out _);
                moved = true;
            }
            if (!moved)
                return;
        }
        if (startClear && !Clear(x, y, r, others))
        {
            x = x0;
            y = y0;
        }
    }

    private static bool Clear(int x, int y, int r, List<CapsuleObstacle> others)
    {
        foreach (CapsuleObstacle other in others)
        {
            long min = r + RadiusSubunits(other.Radius);
            long dx = x - other.X;
            long dy = y - other.Y;
            if (dx * dx + dy * dy < min * min) return false;
        }
        return true;
    }

    private static int AwayFromZero(float v) => v >= 0f ? (int)MathF.Ceiling(v) : -(int)MathF.Ceiling(-v);

    // The cardinal border crossings of the centre's path from (x0, y0) to (x1, y1), in path order (an exact corner gives
    // north/south first). When the straight path's order would step through a solid tile or a blocked face (it cut a wall's
    // corner that the sweep went around), the sweep's own order is used: north/south first, then east/west, else the reverse.
    private static void AddCrossings(int x0, int y0, int x1, int y1, int z, ICollisionView view, MotionEvents events)
    {
        int tx0 = MotionMath.TileOf(x0);
        int ty0 = MotionMath.TileOf(y0);
        int tx1 = MotionMath.TileOf(x1);
        int ty1 = MotionMath.TileOf(y1);
        int nx = Math.Abs(tx1 - tx0);
        int ny = Math.Abs(ty1 - ty0);
        if (nx == 0 && ny == 0)
            return;
        Span<bool> order = nx + ny <= 16 ? stackalloc bool[nx + ny] : new bool[nx + ny]; // true: a north/south crossing
        PathOrder(x0, y0, x1, y1, tx0, ty0, tx1, ty1, order);
        if (!Walkable(tx0, ty0, tx1, ty1, z, order, view))
        {
            for (int i = 0; i < order.Length; i++) order[i] = i < ny;
            if (!Walkable(tx0, ty0, tx1, ty1, z, order, view))
            {
                for (int i = 0; i < order.Length; i++) order[i] = i >= nx;
                if (!Walkable(tx0, ty0, tx1, ty1, z, order, view))
                    PathOrder(x0, y0, x1, y1, tx0, ty0, tx1, ty1, order);
            }
        }
        int cx = tx0;
        int cy = ty0;
        int stepX = Math.Sign(tx1 - tx0);
        int stepY = Math.Sign(ty1 - ty0);
        foreach (bool northSouth in order)
        {
            int fx = cx;
            int fy = cy;
            byte dir;
            if (northSouth)
            {
                cy += stepY;
                dir = stepY > 0 ? MotionDir.North : MotionDir.South;
            }
            else
            {
                cx += stepX;
                dir = stepX > 0 ? MotionDir.East : MotionDir.West;
            }
            events.Add(new MotionEvent(MotionEventKind.Crossed, fx, fy, cx, cy, dir, 0));
        }
    }

    // Merges the x and y border crossings of the straight segment by their position along it, exactly: a crossing's
    // parameter is num / den in doubled subunits (a westward or southward crossing happens just past the line, +1).
    private static void PathOrder(int x0, int y0, int x1, int y1, int tx0, int ty0, int tx1, int ty1, Span<bool> order)
    {
        int nx = Math.Abs(tx1 - tx0);
        int ny = Math.Abs(ty1 - ty0);
        long denX = 2L * Math.Abs((long)x1 - x0);
        long denY = 2L * Math.Abs((long)y1 - y0);
        int ix = 0;
        int iy = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (ix == nx) { order[i] = true; iy++; continue; }
            if (iy == ny) { order[i] = false; ix++; continue; }
            long numX = CrossingNumerator(x0, tx0, Math.Sign(tx1 - tx0), ix);
            long numY = CrossingNumerator(y0, ty0, Math.Sign(ty1 - ty0), iy);
            // numY / denY <= numX / denX: the y crossing comes first (ties go north/south first).
            bool yFirst = numY * denX <= numX * denY;
            order[i] = yFirst;
            if (yFirst) iy++; else ix++;
        }
    }

    // The doubled distance from p0 along the axis to the i-th border crossed from tile t0 in direction step.
    private static long CrossingNumerator(int p0, int t0, int step, int i)
    {
        if (step > 0)
        {
            long line = (long)(t0 + i) * MotionMath.SubunitsPerTile + Half;
            return 2L * (line - p0);
        }
        long west = (long)(t0 - i) * MotionMath.SubunitsPerTile - Half;
        return 2L * (p0 - west) + 1;
    }

    // Whether stepping through the tiles in this order meets no solid tile and no blocked face before the last one.
    private static bool Walkable(int tx0, int ty0, int tx1, int ty1, int z, ReadOnlySpan<bool> order, ICollisionView view)
    {
        int cx = tx0;
        int cy = ty0;
        int stepX = Math.Sign(tx1 - tx0);
        int stepY = Math.Sign(ty1 - ty0);
        for (int i = 0; i < order.Length; i++)
        {
            int fx = cx;
            int fy = cy;
            TileEdges leaving;
            TileEdges entering;
            if (order[i])
            {
                cy += stepY;
                leaving = stepY > 0 ? TileEdges.North : TileEdges.South;
                entering = stepY > 0 ? TileEdges.South : TileEdges.North;
            }
            else
            {
                cx += stepX;
                leaving = stepX > 0 ? TileEdges.East : TileEdges.West;
                entering = stepX > 0 ? TileEdges.West : TileEdges.East;
            }
            if ((view.EdgesOf(fx, fy, z) & leaving) != 0 || (view.EdgesOf(cx, cy, z) & entering) != 0)
                return false;
            if (i < order.Length - 1 && view.IsSolid(cx, cy, z))
                return false;
        }
        return true;
    }
}

/// <summary>
/// The view of a <see cref="MotionParams.NoClip"/> capsule: only the tiles outside the map are solid (<see
/// cref="ICollisionView.IsOutsideMap"/> of the real view); no blocked faces, no standing capsules.
/// </summary>
internal sealed class NoClipView : ICollisionView
{
    private readonly ICollisionView _inner;

    public NoClipView(ICollisionView inner) => _inner = inner;

    public bool IsSolid(int x, int y, int z) => _inner.IsOutsideMap(x, y, z);

    public bool IsOutsideMap(int x, int y, int z) => _inner.IsOutsideMap(x, y, z);

    public TileEdges EdgesOf(int x, int y, int z) => TileEdges.None;

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into)
    {
    }
}

/// <summary>
/// D239: the view of a <see cref="MotionParams.OnClimbable"/> capsule (tg's <c>TRAIT_ON_CLIMBABLE</c>): a tile solid only through
/// <c>climb_walkable</c> structures (<see cref="ICollisionView.IsClimbWalkable"/>) is open; everything else is the real view's.
/// </summary>
internal sealed class ClimberView : ICollisionView
{
    private readonly ICollisionView _inner;

    public ClimberView(ICollisionView inner) => _inner = inner;

    public bool IsSolid(int x, int y, int z) => _inner.IsSolid(x, y, z) && !_inner.IsClimbWalkable(x, y, z);

    public bool IsOutsideMap(int x, int y, int z) => _inner.IsOutsideMap(x, y, z);

    public bool IsClimbWalkable(int x, int y, int z) => _inner.IsClimbWalkable(x, y, z);

    public bool IsFlaps(int x, int y, int z) => _inner.IsFlaps(x, y, z);

    public TileEdges EdgesOf(int x, int y, int z) => _inner.EdgesOf(x, y, z);

    public bool LadderAt(int x, int y, int z) => _inner.LadderAt(x, y, z);

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) => _inner.GatherCapsules(x, y, z, range, exclude, into);
}

/// <summary>
/// DV360: the view of a standing capsule that tg's plastic flaps refuse (no <see cref="MotionParams.PassesFlaps"/>): a flaps tile
/// (<see cref="ICollisionView.IsFlaps"/>) is solid; everything else is the real view's. Leaving a flaps tile is free, as in tg.
/// </summary>
internal sealed class FlapsView : ICollisionView
{
    private readonly ICollisionView _inner;

    public FlapsView(ICollisionView inner) => _inner = inner;

    public bool IsSolid(int x, int y, int z) => _inner.IsSolid(x, y, z) || _inner.IsFlaps(x, y, z);

    public bool IsOutsideMap(int x, int y, int z) => _inner.IsOutsideMap(x, y, z);

    public bool IsClimbWalkable(int x, int y, int z) => _inner.IsClimbWalkable(x, y, z);

    public bool IsFlaps(int x, int y, int z) => _inner.IsFlaps(x, y, z);

    public TileEdges EdgesOf(int x, int y, int z) => _inner.EdgesOf(x, y, z);

    public bool LadderAt(int x, int y, int z) => _inner.LadderAt(x, y, z);

    public void GatherCapsules(int x, int y, int z, int range, long exclude, List<CapsuleObstacle> into) => _inner.GatherCapsules(x, y, z, range, exclude, into);
}
