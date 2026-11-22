namespace Anomaly.Core.Motion;

// Owner: the types and their meaning are the M2 contracts step's (frozen, docs/ai/specs/m2-ownership.md); M2 P4 fills the
// bodies of MotionStep and MotionPredictor and writes docs/ai/specs/locomotion.md.
//
// Units and conventions, used by every type of this assembly:
// - Positions are in TILES on one map level, BYOND's tile coordinates: the CENTRE of the tile with BYOND x (y) is exactly
//   x (y), so a tile x covers [x - 0.5, x + 0.5). The client's TileSpace (godot/scripts/Shared/TileSpace.cs) uses the same
//   fractional coordinates (a tile is 2 m, D33). East is +x, north is +y.
// - The state stores positions QUANTIZED to 1/256 tile (MotionMath.SubunitsPerTile) as integers: the subunit q is the
//   position p times 256, so the tile centre x is q = 256 * x. The controller computes in floats and quantizes after every
//   step, so every replay starts from the same bits (M2 scope 2d).
// - Time is in TICKS of the server world (20 per second, tg's world.fps; 1 tick = 0.5 ds). Speeds are tiles per tick.
// - Yaw is a ushort: 65536 is a full turn, 0 faces north (+y) and the angle grows CLOCKWISE seen from above (16384 east,
//   32768 south, 49152 west), a compass bearing.
// - Directions are BYOND's: NORTH 1, SOUTH 2, EAST 4, WEST 8 (MotionDir).
//
// Determinism rule (M2 scope 2d, risk 4): the controller uses only + - * / and sqrt on floats (no Math.Sin, Cos, Atan2,
// Pow, no FMA, no double), integer arithmetic on the quantized state, and quantizes after each step. Same machine: a replay
// is bit-identical to the server's step. Across x64 and arm64 only these operations are guaranteed equal; D64 covers the
// rest (the server corrects).

/// <summary>BYOND's direction bits, as the controller's events report them.</summary>
public static class MotionDir
{
    public const byte None = 0;
    public const byte North = 1;
    public const byte South = 2;
    public const byte East = 4;
    public const byte West = 8;
}

/// <summary>
/// The body's posture as tg holds it (<c>/mob/living/var/body_position</c>: <c>STANDING_UP</c> or <c>LYING_DOWN</c>),
/// mirrored into the state by the locomotion bridge every tick. A lying capsule takes no part in capsule collisions,
/// neither as an obstacle nor as a mover (D51, D105; tg's <c>on_lying_down</c> adds <c>TRAIT_UNDENSE</c>).
/// </summary>
public enum Posture : byte
{
    /// <summary>tg <c>STANDING_UP</c>.</summary>
    Standing = 0,

    /// <summary>tg <c>LYING_DOWN</c>.</summary>
    Lying = 1,
}

/// <summary>
/// Which stepping rule drives the capsule this tick (the driver slot of scope 2a: "Grounded and Prone in M2, Thrown
/// (tile-driven) in M4a, ZeroG (6DoF, crossings still call Move) in M4c"). The bridge chooses it from tg's state.
/// </summary>
public enum MotionDriver : byte
{
    /// <summary>Walking or running on a floor: the wish vector at <see cref="MotionParams.TilesPerTick"/>, collision with solid tiles and standing capsules.</summary>
    Grounded = 0,

    /// <summary>Lying (crawling): as Grounded at tg's crawl speed, but without capsule collisions (D51).</summary>
    Prone = 1,

    /// <summary>Reserved for M4a: thrown by tg's <c>throw_at</c>, moved by tg's tile steps; the controller only follows <c>loc</c>.</summary>
    Thrown = 2,

    /// <summary>Zero gravity (DV69): the wish moves it as Grounded while tg's gate lets it (a push-off, a jetpack), else its drift (<see cref="MotionParams.DriftX"/>); crossings still call tg's <c>Move</c>.</summary>
    ZeroG = 3,
}

/// <summary>Flags of a <see cref="MotionInput"/> frame. None are defined in M2; the bits are reserved (the codec refuses unknown bits).</summary>
[Flags]
public enum MotionInputFlags : byte
{
    None = 0,
}

/// <summary>
/// One client input frame: what the player wants for one server tick (scope 2b). The client turns its view yaw and the
/// held movement keys into a world-space wish vector, so the server needs no trigonometry. Sent on the unreliable Input
/// channel, every message repeating the last 4 frames (Anomaly.Protocol); the server's jitter buffer applies one frame per
/// tick, in seq order.
/// </summary>
/// <param name="Seq">1 for the first frame of a connection, one more per frame (one per client tick); the server echoes the last one it applied (<c>lastInputSeq</c>), which is what the predictor reconciles against.</param>
/// <param name="MoveX">The east component of the wish vector, -127 to 127 (127 is full speed east). The vector's length is clamped to 127 by the controller.</param>
/// <param name="MoveY">The north component of the wish vector, -127 to 127.</param>
/// <param name="Yaw">Where the player looks (a compass bearing, see the file header): the server derives tg's <c>dir</c> from it (4 directions with M1's 5 degree hysteresis) and replicates it for billboard frames.</param>
/// <param name="Flags">Reserved, <see cref="MotionInputFlags.None"/>.</param>
public readonly record struct MotionInput(uint Seq, sbyte MoveX, sbyte MoveY, ushort Yaw, MotionInputFlags Flags)
{
    /// <summary>The largest length of the wish vector (full speed).</summary>
    public const int MaxMove = 127;

    /// <summary>A frame that wants nothing (no movement, the yaw kept by the caller).</summary>
    public static MotionInput Idle(uint seq, ushort yaw) => new(seq, 0, 0, yaw, MotionInputFlags.None);
}

/// <summary>
/// A slide: a displacement tg made (a shove, <c>PushAM</c>'s follow, any cardinal <c>Move</c> the bridge did not start)
/// that the capsule plays over tg's glide time instead of jumping (D105: "a shove ... moves the target tg's full tile, slid
/// over its glide time"). While <see cref="TicksLeft"/> is above 0 the controller moves the capsule toward the target by an
/// equal share each tick and ignores the wish vector; a zero slide is none.
/// </summary>
/// <param name="TargetX">The target x in 1/256 tile (normally a tile centre).</param>
/// <param name="TargetY">The target y in 1/256 tile.</param>
/// <param name="TicksLeft">Ticks until the capsule is at the target.</param>
public readonly record struct Slide(int TargetX, int TargetY, ushort TicksLeft)
{
    /// <summary>True while a slide runs.</summary>
    public bool Active => TicksLeft > 0;
}

/// <summary>
/// The state of one capsule: everything the controller needs from one tick to the next, quantized, a plain value. The
/// server keeps one per capsule mob (the locomotion bridge); a client predicts its own (D103) and the server sends the
/// authoritative one after <c>lastInputSeq</c> in the session's private block (D40: only to its owner).
/// <para>
/// <b>Invariant</b> (the bridge keeps it, scope 2a): at the end of every tick the tile of the capsule's centre
/// (<see cref="TileX"/>, <see cref="TileY"/>, <see cref="Z"/>) equals the mob's tg <c>loc</c>.
/// </para>
/// </summary>
public struct MotionState
{
    /// <summary>The centre's x in 1/256 tile (the tile centre x is 256 * x).</summary>
    public int X;

    /// <summary>The centre's y in 1/256 tile.</summary>
    public int Y;

    /// <summary>The map level (BYOND z, from 1).</summary>
    public int Z;

    /// <summary>Where the body faces (a compass bearing, 65536 per turn): the last input's yaw.</summary>
    public ushort Yaw;

    /// <summary>tg's body position, mirrored by the bridge.</summary>
    public Posture Posture;

    /// <summary>The stepping rule of this tick.</summary>
    public MotionDriver Driver;

    /// <summary>A tg-made displacement in progress (<see cref="Slide.Active"/>).</summary>
    public Slide Slide;

    /// <summary>
    /// Ticks until the next attempt to step into a blocked face the player presses into (D105: "pressing into a blocked
    /// face calls Move at tg's move delay, so Bump works"); 0 means the next press attempts at once.
    /// </summary>
    public short PressTimer;

    /// <summary>
    /// DV263 (protocol version 66): the walking capsule's velocity east in 1/65536 tile a tick (<see cref="MotionMath.VelocityPerTile"/>),
    /// which <see cref="MoveFeel"/>'s ramp carries from tick to tick; 0 while no ramp is on.
    /// </summary>
    public int VelX;

    /// <summary>DV263: the velocity north in 1/65536 tile a tick.</summary>
    public int VelY;

    /// <summary>The BYOND tile x of the centre: <c>floor((X + 128) / 256)</c>.</summary>
    public readonly int TileX => MotionMath.TileOf(X);

    /// <summary>The BYOND tile y of the centre.</summary>
    public readonly int TileY => MotionMath.TileOf(Y);

    /// <summary>DV263: drops the velocity (tg moved the capsule, it cannot move, it left the floor).</summary>
    public void Stop()
    {
        VelX = 0;
        VelY = 0;
    }

    /// <summary>A state standing still at the centre of the tile (x, y, z).</summary>
    public static MotionState AtTile(int x, int y, int z, ushort yaw = 0) => new()
    {
        X = x * MotionMath.SubunitsPerTile,
        Y = y * MotionMath.SubunitsPerTile,
        Z = z,
        Yaw = yaw,
    };
}

/// <summary>
/// What tg allows the capsule this tick, read by the bridge from tg every tick and sent to the owner in the private block
/// (the predictor uses the newest it has for every frame it replays).
/// </summary>
/// <param name="TilesPerTick">Speed: tg's <c>cached_multiplicative_slowdown</c> (deciseconds per tile) turned into tiles per tick, <c>0.5 / slowdown</c>, times the server's <c>MOVE_SPEED_SCALE</c> (D106). Running 1.5 ds per tile is 1/3 tile per tick (6.67 tiles/s, 13.3 m/s).</param>
/// <param name="Radius">The capsule radius in tiles (0.35 m at 2 m tiles is 0.175, scope 2c; <see cref="MotionMath.DefaultRadius"/>).</param>
/// <param name="CanMove">tg's movement gate this tick (the bridge's <c>capsule_move_gate</c>: DEAD, <c>MOBILITY_MOVE</c>, buckled, <c>Process_Spacemove</c>, ... in tg's order, DV15). False: the wish vector is ignored (a slide still plays).</param>
/// <param name="MoveDelayTicks">tg's move delay in ticks, the cadence of the attempts into a blocked face (<see cref="MotionState.PressTimer"/>): <c>max(1, floor(slowdown / tick_lag))</c> (waiting-model.md section 8's delay rule).</param>
/// <param name="NoClip">The dev noclip (the owner's play test, 2026-10-09): tg's <c>incorporeal_move</c> is <c>INCORPOREAL_MOVE_BASIC</c>, so only the map's edge stops the capsule and its crossings are tg's <c>forceMove</c> (<c>Process_Incorpmove</c>), DV29.</param>
/// <param name="DriftX">DV69: a weightless capsule's drift east, subunits a tick: tg's drift (its <c>drift_handler</c>'s angle, a tile per loop delay), what moves it while no key moves it (protocol version 28).</param>
/// <param name="DriftY">DV69: the drift north, subunits a tick.</param>
/// <param name="OnClimbable">D239: the mob has tg's <c>TRAIT_ON_CLIMBABLE</c> (it stands on a table or a crate it climbed), so the tiles solid only through <c>climb_walkable</c> structures do not stop it (<see cref="ICollisionView.IsClimbWalkable"/>; protocol version 37).</param>
/// <param name="GravityMask">Protocol version 48: the 7 x 7 tiles around (<see cref="GravityX"/>, <see cref="GravityY"/>) on which the mob would be weightless, bit <c>(dy + 3) * 7 + dx + 3</c>; 0 when they are all alike. The predictor switches the driver and the speed where the capsule crosses into the other kind (<see cref="MotionPredictor.InGravityOf"/>).</param>
/// <param name="GravityX">The tile the mask is centred on (the mob's tile when the parameters were made).</param>
/// <param name="GravityY">The tile the mask is centred on, north.</param>
/// <param name="OtherTilesPerTick">The speed on the tiles of the other kind than the centre (tg's movespeed modifiers that apply only <c>FLOATING</c> or only not: the jetpack's full speed).</param>
/// <param name="IntentTilesPerTick">Protocol version 49: the speed after a walk/run toggle (tg's <c>config_walk_run</c> modifier swapped for the other one), 0 when unknown; the predictor steps the frames from a toggle on with it (<see cref="MotionPredictor.ToggleIntentAt"/>).</param>
/// <param name="IntentMoveDelayTicks">Protocol version 49: the move delay after a walk/run toggle.</param>
/// <param name="OtherCanMove">Protocol version 50, with a <see cref="GravityMask"/>: whether tg's gate lets the mob steer on a weightless tile with nothing to push off (a lit jetpack's stabilizer, being pulled); the predictor stops the wish on such a tile without it.</param>
/// <param name="InertiaTilesPerTick">Protocol version 50, with a <see cref="GravityMask"/>: the speed of a drift tg starts from rest (a <c>drift_handler</c> of 1 newton: <c>DEFAULT_INERTIA_SPEED</c> times the mob's inertia multipliers), which the predictor gives a step off a floor into space.</param>
/// <param name="UnblockTicks">Protocol version 50, with <see cref="CanMove"/> false: the first of the next ticks (1 the next one) on which tg's drift input block (<c>drift_handler</c>'s <c>block_inputs_until</c> after a drift glides to a halt on a floor) no longer holds the mob and the gate lets the held wish move; 0 when that block is not what stops it.</param>
/// <param name="ImpulseSpeed">Protocol version 51 (DV177), without a drift: the speed (subunits a tick, as the drift's) of the drift a push would start from rest where it surely drifts whatever its direction (no stabilizers, nothing to push off or a lit jetpack's <c>TRAIT_NOGRAV_ALWAYS_DRIFT</c>), which the predictor gives its own push; 0 elsewhere.</param>
/// <param name="Feel">DV263 (protocol version 66): the walking capsule's speed ramp and its slower backward and sideways speeds; the default is tg's (none).</param>
/// <param name="Belt">DV347 (protocol version 69): the running conveyor belts around the mob that carry it; the default is none.</param>
/// <param name="Wind">DV357 (protocol version 72): the space wind around the mob that blows it, a <see cref="BeltField"/> of each tile's wind direction and this mob's push speed; the default is none.</param>
/// <param name="PullX">DV357 (protocol version 72): the tile a gravity pull (a singularity, a gravitational anomaly, a delaminating supermatter) draws the mob toward.</param>
/// <param name="PullY">The pull's tile, north.</param>
/// <param name="PullSpeed">The pull's speed, subunits a tick (0: none).</param>
/// <param name="HaltsAtHandhold">Protocol version 89, with a drift: tg's drift halts where the mob can grab something (<c>after_move</c>'s <c>Process_Spacemove</c>: a player's mob without a lit jetpack's <c>TRAIT_NOGRAV_ALWAYS_DRIFT</c> or DV412's blow), so the predictor halts it once a handhold is within <see cref="MotionMath.HandReach"/> (DV534).</param>
/// <param name="SteersByHandhold">Protocol version 89 (DV534): the weightless mob steers only by something to push off (no jetpack's stabilizer, no pull, no
/// <c>TRAIT_NOGRAV_ALWAYS_DRIFT</c>), so the predictor lets the wish move it only while a handhold is within <see cref="MotionMath.HandReach"/>, and a step
/// that leaves the last one behind starts the drift of <see cref="ImpulseSpeed"/>.</param>
/// <param name="PassesFlaps">DV360 (protocol version 73): tg's plastic flaps (<c>CanAllowThrough</c>) let this mover through standing: <c>PASSFLAPS</c> or <c>PASSGLASS</c>, a tiny or ventcrawling mob, a mulebot's passenger. Without it only a lying capsule enters a flaps tile (<see cref="ICollisionView.IsFlaps"/>).</param>
public readonly record struct MotionParams(float TilesPerTick, float Radius, bool CanMove, short MoveDelayTicks, bool NoClip = false, short DriftX = 0, short DriftY = 0, bool OnClimbable = false,
    ulong GravityMask = 0, int GravityX = 0, int GravityY = 0, float OtherTilesPerTick = 0f, float IntentTilesPerTick = 0f, short IntentMoveDelayTicks = 0,
    bool OtherCanMove = false, float InertiaTilesPerTick = 0f, short UnblockTicks = 0, short ImpulseSpeed = 0, MoveFeel Feel = default, BeltField Belt = default,
    BeltField Wind = default, int PullX = 0, int PullY = 0, short PullSpeed = 0, bool PassesFlaps = false, bool HaltsAtHandhold = false, bool SteersByHandhold = false)
{
    /// <summary>The bits of a full <see cref="GravityMask"/>: 7 x 7 tiles.</summary>
    public const ulong GravityMaskAll = (1UL << 49) - 1;

    /// <summary>tg's running speed at <c>MOVE_SPEED_SCALE</c> 1: <c>RUN_DELAY</c> 1.5 ds per tile, 3 ticks of move delay.</summary>
    public static MotionParams DefaultRun => new(MotionMath.TilesPerTickFor(1.5f, 1f), MotionMath.DefaultRadius, true, 3);
}

/// <summary>
/// DEVIATION(DV347): the running conveyor belts of the 9 x 9 tiles around (<see cref="X"/>, <see cref="Y"/>) that tg's convey component would
/// carry the mob along: each tile's direction as a 3-bit code (0 none, 1 north, 2 south, 3 east, 4 west), tile <c>(dy + 4) * 9 + dx + 4</c>,
/// 21 tiles a word in <see cref="W0"/> to <see cref="W3"/>; <see cref="Speed"/> the belts' speed in subunits a tick (tg's a tile per
/// <c>speed</c> seconds times <c>MOVE_SPEED_SCALE</c>), 0 for none. A walking capsule on such a tile moves with the belt as well (<see cref="MotionStep"/>), on the server
/// and in its client's prediction alike; four tiles either way cover a prediction several ticks ahead at a run along a belt.
/// </summary>
public readonly record struct BeltField(ulong W0, ulong W1, ulong W2, ulong W3, int X, int Y, short Speed)
{
    /// <summary>The tiles either way of the centre.</summary>
    public const int Reach = 4;

    /// <summary>The side of the field in tiles.</summary>
    public const int Side = 2 * Reach + 1;

    /// <summary>The tiles a word holds.</summary>
    public const int TilesPerWord = 21;

    /// <summary>The bits the last word may hold (its tiles 63 to 80).</summary>
    public const ulong LastWordMask = (1UL << ((Side * Side - 3 * TilesPerWord) * 3)) - 1;

    /// <summary>True when some belt runs in the field.</summary>
    public bool Active => Speed > 0;

    /// <summary>The direction the belt of tile (<paramref name="tileX"/>, <paramref name="tileY"/>) carries things (a <see cref="MotionDir"/>), none outside the field.</summary>
    public byte DirAt(int tileX, int tileY)
    {
        int dx = tileX - X, dy = tileY - Y;
        if (!Active || Math.Abs(dx) > Reach || Math.Abs(dy) > Reach)
            return MotionDir.None;
        return CodeAt((dy + Reach) * Side + dx + Reach) switch
        {
            1 => MotionDir.North,
            2 => MotionDir.South,
            3 => MotionDir.East,
            4 => MotionDir.West,
            _ => MotionDir.None,
        };
    }

    /// <summary>The 3-bit code of tile <paramref name="i"/>.</summary>
    public int CodeAt(int i) => (int)((Word(i / TilesPerWord) >> (i % TilesPerWord * 3)) & 7);

    /// <summary>The field with tile (<paramref name="dx"/>, <paramref name="dy"/>) from the centre carrying <paramref name="dir"/> (a cardinal <see cref="MotionDir"/>).</summary>
    public BeltField With(int dx, int dy, byte dir)
    {
        ulong code = dir switch { MotionDir.North => 1, MotionDir.South => 2, MotionDir.East => 3, MotionDir.West => 4, _ => 0 };
        int i = (dy + Reach) * Side + dx + Reach, shift = i % TilesPerWord * 3;
        ulong word = (Word(i / TilesPerWord) & ~(7UL << shift)) | (code << shift);
        return (i / TilesPerWord) switch
        {
            0 => this with { W0 = word },
            1 => this with { W1 = word },
            2 => this with { W2 = word },
            _ => this with { W3 = word },
        };
    }

    private ulong Word(int n) => n switch { 0 => W0, 1 => W1, 2 => W2, _ => W3 };
}

/// <summary>
/// DEVIATION(DV263): how a walking capsule (<see cref="MotionDriver.Grounded"/>, <see cref="MotionDriver.Prone"/>) gathers and loses speed and how
/// much slower it goes backward and sideways (the owner, 2026-11-03: tg's speed starts and stops at once and is the same in every direction).
/// The server sends it in the parameters and the controller applies it, so a prediction replays it bit for bit. The default (all zero) is tg's.
/// </summary>
/// <param name="AccelTicks">Ticks from rest to full speed (0: at once).</param>
/// <param name="DecelTicks">Ticks from full speed to rest, also the braking of a reversal (0: at once).</param>
/// <param name="BackSlow">The part of the speed lost moving straight back from where the view faces (0.3: 70 % of the speed), 0 to <see cref="MaxSlow"/>.</param>
/// <param name="StrafeSlow">The part lost moving straight sideways; a direction between gets the ellipse between them.</param>
public readonly record struct MoveFeel(short AccelTicks, short DecelTicks, float BackSlow, float StrafeSlow)
{
    /// <summary>The longest ramp, ticks.</summary>
    public const short MaxTicks = 40;

    /// <summary>The largest slow-down of a direction.</summary>
    public const float MaxSlow = 0.9f;

    /// <summary>The server's default (D314): full speed in 0.2 s, a stop in 0.15 s, 49 % backward, 63 % sideways (the owner, 2026-11-19: 30 % less than 70 and 90).</summary>
    public static MoveFeel Default => new(4, 3, 0.51f, 0.37f);

    /// <summary>Whether the speed ramps (the state carries a velocity).</summary>
    public bool Ramps => AccelTicks > 0 || DecelTicks > 0;

    /// <summary>Whether a direction is slower than another.</summary>
    public bool Directional => BackSlow > 0f || StrafeSlow > 0f;

    /// <summary>Whether every field is within its range (the codec and the setters check it).</summary>
    public bool IsValid => AccelTicks >= 0 && AccelTicks <= MaxTicks && DecelTicks >= 0 && DecelTicks <= MaxTicks
        && BackSlow >= 0f && BackSlow <= MaxSlow && StrafeSlow >= 0f && StrafeSlow <= MaxSlow;
}

/// <summary>Constants and the quantization rule.</summary>
public static class MotionMath
{
    /// <summary>Subunits per tile: the state is quantized to 1/256 tile (scope 2d).</summary>
    public const int SubunitsPerTile = 256;

    /// <summary>DEVIATION(DV534): how far a weightless capsule's hand reaches, from its centre: half a tile (1 m at 2 m tiles).</summary>
    public const int HandReach = SubunitsPerTile / 2;

    /// <summary>
    /// DV534: whether tile (<paramref name="tileX"/>, <paramref name="tileY"/>) is within <see cref="HandReach"/> of the centre
    /// (<paramref name="x"/>, <paramref name="y"/>, subunits): the distance from the centre to the tile's square, so the tile the centre
    /// is on is always in reach and a wall beside a tile's centre just is.
    /// </summary>
    public static bool InHandReach(int x, int y, int tileX, int tileY)
    {
        long dx = Math.Max(0, Math.Abs(x - tileX * SubunitsPerTile) - SubunitsPerTile / 2);
        long dy = Math.Max(0, Math.Abs(y - tileY * SubunitsPerTile) - SubunitsPerTile / 2);
        return dx * dx + dy * dy <= (long)HandReach * HandReach;
    }

    /// <summary>DV263: a velocity's unit, 1/65536 tile a tick (a subunit is 256 of them), fine enough that a ramp's steps keep their speed.</summary>
    public const int VelocityPerTile = 65536;

    /// <summary>
    /// DV263: where a compass bearing faces, (east, north), from <c>+ - *</c> only (the determinism rule: no <c>Math.Sin</c>): the quadrant
    /// from the bearing's top bits, then Taylor series of the angle within it (an error under 1e-5).
    /// </summary>
    public static (float East, float North) Facing(ushort yaw)
    {
        int quadrant = yaw >> 14;
        float a = (yaw & 0x3FFF) * (MathF.PI / 2f / 16384f);
        float a2 = a * a;
        float sin = a * (1f - a2 / 6f * (1f - a2 / 20f * (1f - a2 / 42f * (1f - a2 / 72f))));
        float cos = 1f - a2 / 2f * (1f - a2 / 12f * (1f - a2 / 30f * (1f - a2 / 56f * (1f - a2 / 90f))));
        // compass: 0 faces north, a quarter turn east; within a quadrant the bearing turns clockwise from its start
        return quadrant switch
        {
            0 => (sin, cos),
            1 => (cos, -sin),
            2 => (-sin, -cos),
            _ => (-cos, sin),
        };
    }

    /// <summary>The server's ticks per second (tg's <c>world.fps</c>).</summary>
    public const int TicksPerSecond = 20;

    /// <summary>A tick in deciseconds (tg's <c>world.tick_lag</c>).</summary>
    public const float TickDeciseconds = 0.5f;

    /// <summary>The capsule radius in tiles: 0.35 m at 2 m per tile (scope 2c "same check", D33).</summary>
    public const float DefaultRadius = 0.175f;

    /// <summary>DEVIATION(DV358): an exosuit's capsule radius in tiles (1.2 m across at 2 m tiles): a bulk that still walks through a one-tile door.</summary>
    public const float MechRadius = 0.3f;

    /// <summary>The tile of a quantized coordinate: <c>floor((q + 128) / 256)</c> (a tile centre is a multiple of 256; a tile covers [256x - 128, 256x + 128)).</summary>
    public static int TileOf(int q) => (q + SubunitsPerTile / 2) >> 8;

    /// <summary>The quantization rule: a float position in tiles to the nearest 1/256 tile, halves up (<c>floor(p * 256 + 0.5)</c>).</summary>
    public static int Quantize(float tiles) => (int)MathF.Floor(tiles * SubunitsPerTile + 0.5f);

    /// <summary>A quantized coordinate in tiles.</summary>
    public static float ToTiles(int q) => q / (float)SubunitsPerTile;

    /// <summary>
    /// tg's slowdown (deciseconds per tile) and the server's speed scale as tiles per tick: <c>tick_lag / slowdown * scale</c>
    /// (D106). A slowdown below one tick counts as one tick (at most a tile per tick at scale 1).
    /// </summary>
    public static float TilesPerTickFor(float slowdownDeciseconds, float speedScale)
        => TickDeciseconds / MathF.Max(slowdownDeciseconds, TickDeciseconds) * speedScale;
}
