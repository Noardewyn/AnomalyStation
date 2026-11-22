using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owners (docs/ai/specs/m2-ownership.md): the contract (these signatures and the meaning in the comments) is the M2
// contracts step's and frozen; M2 P4 implements it in this folder (the locomotion phase, the gate, the crossings, the
// displacement, the contact signals: DV15, DV16, DV17) and writes docs/ai/specs/locomotion.md; M2 P6 wires it into the
// server host; M2 P5's MapGrid version 2 uses IsSolid. Host code, not tg code: it stands where BYOND's client/Move stepping
// stood (D105).

/// <summary>
/// The locomotion seam (D105, scope 2a, 2b): the bridge between the continuous capsules of <c>Anomaly.Core.Motion</c> and
/// tg's tile world. A capsule mob (a session's living mob) is moved by the <b>locomotion phase</b>, a world phase that runs
/// right after the host's command phase each tick. For each registered mob, in <c>dm_id</c> order, it takes the frame the
/// command phase set for the tick (<see cref="SetFrame"/>; the last one repeats when none came), runs tg's movement gate
/// (<c>client/capsule_move_gate</c>: <c>client/Move</c>'s checks in tg's order without <c>move_delay</c>, DV15), reads the
/// parameters from tg (<c>cached_multiplicative_slowdown</c> times the speed scale, D106; the radius; tg's move delay),
/// steps the capsule (<see cref="MotionStep.Step"/>), and applies the events as tg calls: a crossing is tg's real
/// <c>Move(newTurf, dir, 0, update_dir = FALSE)</c> with <c>PASSMOB</c> for that call (DV16), a refused one clamps the
/// capsule back; a press into a blocked face is <c>Move</c> at tg's move delay (so <c>Bump</c> works); a contact runs
/// <c>MobBump</c>'s signals only (DV16, D105). Then <c>client/Move</c>'s tail (<c>COMSIG_MOB_CLIENT_MOVED</c>, ...), and
/// <c>dir</c> follows the yaw (4 directions, M1's 5 degree hysteresis; <c>setDir</c> only on a change).
/// <para>
/// A <c>loc</c> change tg makes (a shove, <c>forceMove</c>, a teleport, <c>PushAM</c>'s follow) is seen through the moved
/// hint and displaces the capsule (<see cref="MotionStep.Displace"/>: a cardinal neighbour slides over tg's glide time,
/// anything else snaps). <b>Invariant:</b> after the phase, every registered capsule's centre is on its mob's <c>loc</c>.
/// </para>
/// <para>
/// The seam's state (the registered mobs, their <see cref="MotionState"/>, the frames, the speed scale) is world state: per
/// world (a Kernel extension, no static state), changed only inside <c>World.Step</c>, and hashed by a contributor that
/// <see cref="Install"/> registers, so a replay of the recorded frames gives the live run's hash.
/// </para>
/// </summary>
public static partial class LocomotionSeam
{
    /// <summary>The name of the locomotion world phase.</summary>
    public const string PhaseName = "locomotion";

    /// <summary>
    /// The name of the second, short phase <see cref="Install"/> adds right after the locomotion phase: it runs no tg code
    /// and only makes every capsule follow its mob's <c>loc</c> again, so that a <c>loc</c> change made by the zero-delay work
    /// the locomotion phase issued (the drain after a phase) still leaves the invariant true at the end of the tick.
    /// </summary>
    public const string FollowPhaseName = "locomotion follow";

    /// <summary>The largest speed scale <see cref="SetSpeedScale"/> takes.</summary>
    public const float MaxSpeedScale = 4f;

    /// <summary>
    /// Adds the locomotion phase to <paramref name="world"/> (after the phases already registered: the host calls it right
    /// after adding its command phase) and its state hash contributor. Once per world, before the first tick that drives a
    /// capsule; a second call throws.
    /// </summary>
    public static void Install(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Seam seam = Seam.Of(world);
        if (seam.Installed)
            throw new InvalidOperationException("LocomotionSeam.Install: the locomotion phase is already installed in this world.");
        seam.Installed = true;
        world.AddPhase(PhaseName, RunPhase);
        world.AddPhase(FollowPhaseName, RunFollowPhase);
        world.AddPhase(ShotRewind.PhaseName, static w => ShotRewind.Record(w, Seam.Of(w).Capsules)); // DV527: after every capsule followed its mob
        world.AddStateHasher(nameof(LocomotionSeam), static (World w, ref StateHasher h) => Seam.Of(w).Hash(ref h));
    }

    /// <summary>Whether <see cref="Install"/> ran on <paramref name="world"/>.</summary>
    public static bool IsInstalled(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Seam.Of(world).Installed;
    }

    /// <summary>
    /// From now on the bridge drives <paramref name="mob"/> (inside the world: the command phase of the join, after the
    /// client attached). Its capsule starts standing at its tile's centre, facing its <c>dir</c>. tg's own key path
    /// (<c>keyLoop</c>, <c>client/Move</c>) is never fed movement for it (DV14 stays for the other mobs).
    /// </summary>
    public static void Register(mob_living mob)
    {
        ArgumentNullException.ThrowIfNull(mob);
        World world = World.Current;
        Seam seam = Seam.Of(world);
        if (!seam.Installed)
            throw new InvalidOperationException("LocomotionSeam.Register: install the locomotion phase first (LocomotionSeam.Install).");
        if (QDELETED(mob) || seam.Capsules.ContainsKey(mob.dm_id))
            return;
        seam.Capsules.Add(mob.dm_id, Capsule.Create(mob, seam.SpeedScale));
    }

    /// <summary>The bridge stops driving <paramref name="mob"/> (the session left, the mob died and was ghosted in M10, the mob was deleted); its state is dropped.</summary>
    public static void Unregister(mob mob)
    {
        ArgumentNullException.ThrowIfNull(mob);
        Seam seam = Seam.Of(World.Current);
        seam.Capsules.Remove(mob.dm_id);
        seam.Ghosts.Remove(mob.dm_id);
    }

    /// <summary>Whether the bridge drives <paramref name="mob"/>.</summary>
    public static bool IsRegistered(mob mob)
    {
        if (mob == null) return false;
        World world = World.CurrentOrNull;
        if (world == null)
            return false;
        Seam seam = Seam.Of(world);
        return seam.Capsules.TryGetValue(mob.dm_id, out Capsule c) && ReferenceEquals(c.Mob, mob)
            || seam.Ghosts.TryGetValue(mob.dm_id, out GhostCapsule g) && ReferenceEquals(g.Mob, mob);
    }

    /// <summary>
    /// Whether the player walks <paramref name="mob"/>'s capsule (a held wish the gate lets move): what tg reads from <c>client.move_delay</c>
    /// being ahead (client/Move's steps push it; a capsule's do not, DV15), as the wind's resistance does (living.dm:1115).
    /// </summary>
    public static bool Steers(mob mob)
    {
        World world = World.CurrentOrNull;
        return mob != null && world != null && Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c) && ReferenceEquals(c.Mob, mob)
            && c.Framed && WishDir(c.Frame.MoveX, c.Frame.MoveY) != MotionDir.None && c.NextParams.CanMove;
    }

    /// <summary>
    /// The frame for this tick's locomotion phase (inside the world, in the command phase: the host's Motion command,
    /// the frame the session's jitter buffer gave). A mob without a frame this tick repeats its last one.
    /// </summary>
    public static void SetFrame(mob mob, in MotionInput frame)
    {
        ArgumentNullException.ThrowIfNull(mob);
        Seam seam = Seam.Of(World.Current);
        if (seam.Capsules.TryGetValue(mob.dm_id, out Capsule c) && ReferenceEquals(c.Mob, mob))
        {
            c.Frame = frame;
            c.Framed = true;
        }
        else if (seam.Ghosts.TryGetValue(mob.dm_id, out GhostCapsule g) && ReferenceEquals(g.Mob, mob))
        {
            g.Frame = frame;
            g.Framed = true;
        }
    }

    /// <summary>
    /// The capsule state of the mob <paramref name="mobId"/> after the last step, and the parameters for the next tick (for
    /// the session's private block). Read-only, outside the world after <c>World.Step</c> (like the replicator), no ambient
    /// world needed; false when the mob is not registered.
    /// </summary>
    public static bool TryGetState(World world, long mobId, out MotionState state, out MotionParams parameters)
    {
        ArgumentNullException.ThrowIfNull(world);
        Seam seam = Seam.Of(world);
        if (seam.Mechs.TryGetValue(mobId, out MechCapsule m))
        {
            state = m.State;
            parameters = m.NextParams;
            return true;
        }
        if (seam.Capsules.TryGetValue(mobId, out Capsule c))
        {
            // DV358: an occupant's body is its exosuit: the client predicts the mech's capsule (only its driver moves it)
            if (c.Mob.dm_loc_raw is obj_vehicle_sealed_mecha mech && seam.Mechs.TryGetValue(mech.dm_id, out m))
            {
                state = m.State;
                parameters = ReferenceEquals(m.Driver, c.Mob) ? m.NextParams : m.NextParams with { CanMove = false };
                return true;
            }
            state = c.State;
            parameters = c.NextParams;
            return true;
        }
        if (seam.Ghosts.TryGetValue(mobId, out GhostCapsule g))
        {
            state = g.State;
            parameters = g.NextParams;
            return true;
        }
        if (seam.Eyes.TryGetValue(mobId, out EyeCapsule e)) // DV423: an AI eye's continuous place, for the replicator
        {
            state = e.State;
            parameters = default;
            return true;
        }
        state = default;
        parameters = default;
        return false;
    }

    /// <summary>The speed scale of <paramref name="world"/> (D106, <c>MOVE_SPEED_SCALE</c>, default 1).</summary>
    public static float SpeedScale(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Seam.Of(world).SpeedScale;
    }

    /// <summary>Sets the speed scale (the host at boot from <c>ServerConfig.MoveSpeedScale</c>, the dev command <c>speedscale</c>); a positive number up to 4.</summary>
    public static void SetSpeedScale(World world, float scale)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!(scale > 0f) || scale > MaxSpeedScale)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "MOVE_SPEED_SCALE is a positive number up to 4.");
        Seam seam = Seam.Of(world);
        seam.SpeedScale = scale;
        foreach (Capsule c in seam.Capsules.Values)
            c.NextParams = c.NextParams with { TilesPerTick = MotionMath.TilesPerTickFor(c.Slowdown, c.Scaled(scale)) };
    }

    /// <summary>DV263: the walking capsules' movement feel of <paramref name="world"/> (none until the host sets it).</summary>
    public static MoveFeel Feel(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Seam.Of(world).Feel;
    }

    /// <summary>DV263: sets the movement feel (the host at boot from <c>ServerConfig.MoveFeel</c>, the dev command <c>movefeel</c>).</summary>
    public static void SetFeel(World world, MoveFeel feel)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!feel.IsValid)
            throw new ArgumentOutOfRangeException(nameof(feel), feel, "A ramp is 0 to " + MoveFeel.MaxTicks + " ticks, a slow-down 0 to " + MoveFeel.MaxSlow + ".");
        Seam seam = Seam.Of(world);
        seam.Feel = feel;
        foreach (Capsule c in seam.Capsules.Values)
            c.NextParams = c.NextParams with { Feel = feel };
    }

    /// <summary>Sets one capsule's own speed multiplier over the world's scale (the dev command <c>speed</c>); false when <paramref name="mob"/> has no capsule.</summary>
    public static bool SetOwnSpeed(mob_living mob, float multiplier)
    {
        ArgumentNullException.ThrowIfNull(mob);
        if (!(multiplier > 0f) || multiplier > MaxSpeedScale)
            throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "A capsule's own speed is a positive number up to 4.");
        Seam seam = Seam.Of(World.Current);
        if (!seam.Capsules.TryGetValue(mob.dm_id, out Capsule c))
            return false;
        c.OwnSpeed = multiplier;
        c.NextParams = c.NextParams with { TilesPerTick = MotionMath.TilesPerTickFor(c.Slowdown, c.Scaled(seam.SpeedScale)) };
        return true;
    }

    /// <summary>
    /// The solidity predicate (scope 2a "Collision", 2d): whether the tile (x, y, z) blocks capsules, a dense turf or a
    /// dense non-mob movable on it (edge objects come in M3). The controller's collision view and MapGrid version 2's
    /// solidity bits (M2 P5) both use exactly this, so the client predicts against what the server collides with.
    /// Read-only: usable inside the world and outside it after <c>World.Step</c> (no pending contents edit applied, as for
    /// the replicator). Outside the map: true.
    /// </summary>
    public static bool IsSolid(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null)
            return true; // outside the map
        if (Dm.truthy(cell.density))
            return true;
        foreach (atom thing in cell.dm_held)
        {
            if (thing is mob or obj_vehicle_sealed_mecha || thing is not atom_movable movable)
                continue; // mobs (D105) and exosuits (DV358) are capsules, not solid tiles
            if (Dm.truthy(movable.density) && !BlocksFace(movable))
                return true; // a dense obj; an ON_BORDER_1 one blocks a face, not the tile (TileEdges, M3)
        }
        return false;
    }

    /// <summary>
    /// Whether a dense <paramref name="movable"/> blocks the face of its dir, not its tile: an <c>ON_BORDER_1</c> object, or a flipped table, whose
    /// <c>CanAllowThrough</c> and <c>on_exit</c> (tables_racks.dm:215, :236) refuse only a crossing of its dir's face as a border object's do (the
    /// owner, 2026-11-17: walking past one mispredicted, the prediction saw a solid tile).
    /// </summary>
    public static bool BlocksFace(atom_movable movable)
        => Dm.truthy(Dm.bitand(movable.flags_1, ON_BORDER_1)) || movable is obj_structure_table table && Dm.truthy(table.is_flipped);

    /// <summary>
    /// D239 (MapGrid's <c>CellFlags.ClimbWalkable</c>, protocol version 37): whether the tile (x, y, z) is solid only through tg's
    /// <c>climb_walkable</c> structures: its turf is not dense, and every dense non-mob movable that makes it <see cref="IsSolid"/> has the
    /// element (tg's <c>can_allow_through</c> lets a mover with <c>TRAIT_ON_CLIMBABLE</c> pass it). A capsule with the trait
    /// (<see cref="MotionParams.OnClimbable"/>) walks through such a tile; the controller's view and the client's prediction both use
    /// exactly this. Read-only and raw like <see cref="IsSolid"/>; outside the map: false.
    /// </summary>
    public static bool IsClimbWalkable(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null || Dm.truthy(cell.density))
            return false;
        bool any = false;
        foreach (atom thing in cell.dm_held)
        {
            if (thing is mob or obj_vehicle_sealed_mecha || thing is not atom_movable movable)
                continue;
            if (!Dm.truthy(movable.density) || BlocksFace(movable))
                continue;
            if (ListenerOf<datum_element_climb_walkable>(movable, COMSIG_ATOM_TRIED_PASS) == null)
                return false;
            any = true;
        }
        return any;
    }

    /// <summary>
    /// DV360 (MapGrid's <c>CellFlags.Flaps</c>, protocol version 73): whether tg's plastic flaps that refuse a standing mob
    /// (<c>require_resting</c>, plasticflaps.dm:203 <c>CanAllowThrough</c>) stand on the tile (x, y, z). The flaps are not dense, so the tile is
    /// not <see cref="IsSolid"/>; a standing capsule without <see cref="MotionParams.PassesFlaps"/> treats it as solid (the controller's view
    /// and the client's prediction alike), and tg's <c>CanAllowThrough</c> stays the truth for the crossing <c>Move</c>. Read-only and raw
    /// like <see cref="IsSolid"/>; outside the map: false.
    /// </summary>
    public static bool IsFlaps(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null)
            return false;
        foreach (atom thing in cell.dm_held)
        {
            if (thing is obj_structure_plasticflaps flaps && Dm.truthy(flaps.require_resting))
                return true;
        }
        return false;
    }

    /// <summary>DV397: whether a ladder stands on the tile (x, y, z): its rails block capsules (<see cref="LadderBody"/>); one linked to nothing lies flat and blocks nothing. Outside the map: false.</summary>
    public static bool LadderAt(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null)
            return false;
        foreach (atom thing in cell.dm_held)
        {
            if (thing is obj_structure_ladder ladder && ladder.icon_state != LadderBody.LyingState)
                return true;
        }
        return false;
    }

    /// <summary>
    /// DV360: whether tg's plastic flaps (plasticflaps.dm:203 <c>CanAllowThrough</c>) let <paramref name="mob"/> through while it stands:
    /// <c>PASSFLAPS</c>, <c>PASSGLASS</c> (tg rolls 60% in the Move; a refused one is a correction), a mulebot's passenger, a tiny mob
    /// or a ventcrawler. The lying posture is the capsule's own (<see cref="MotionState.Posture"/>), so the prediction follows a rest at once.
    /// </summary>
    internal static bool PassesFlaps(mob_living mob)
    {
        if (Dm.truthy(Dm.bitand(mob.pass_flags, PASSFLAPS)) || Dm.truthy(Dm.bitand(mob.pass_flags, PASSGLASS)))
            return true;
        if (mob.buckled is mob_living_basic_bot_mulebot)
            return true;
        return Dm.eq(mob.mob_size, MOB_SIZE_TINY) || HAS_TRAIT(mob, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(mob, TRAIT_VENTCRAWLER_NUDE);
    }

    /// <summary>The listener of type <typeparamref name="T"/> that <paramref name="target"/>'s <paramref name="signal"/> reaches (an element attached to it: tg keeps no other record of one), or null. Read-only.</summary>
    internal static T ListenerOf<T>(datum target, object signal) where T : datum
    {
        object listeners = target._listen_lookup?[signal];
        if (listeners is T one)
            return one;
        if (listeners is DmList list)
        {
            for (int i = 1; i <= list.Count; i++)
            {
                if (list[i] is T listener)
                    return listener;
            }
        }
        return null;
    }

    /// <summary>
    /// M3 (locomotion.md section 4, "an <c>ON_BORDER_1</c> object blocks a face, not the tile"): the blocked faces of the
    /// tile (x, y, z), the union of <see cref="TileFaces.OfDir"/>(<c>dir</c>) of every dense movable on the turf that is not a
    /// mob and has <c>ON_BORDER_1</c> in <c>flags_1</c> (directional windows, windoors, railings, plastic flaps' frames as
    /// tg maps them). The controller's view (<see cref="CollisionView"/>'s <c>EdgesOf</c>) and MapGrid version 3's face bits
    /// (M3 P5) both use exactly this, as <see cref="IsSolid"/> for the tiles. Read-only and raw like <see cref="IsSolid"/>;
    /// outside the map: <see cref="TileEdges.None"/>. tg's own <c>CanAllowThrough</c>/<c>on_exit</c> of a border object stay
    /// the truth for the crossing <c>Move</c> (a refused Move clamps the capsule, as for a stale solidity bit).
    /// M3 P5 implements it and switches the server view's <c>EdgesOf</c> to it.
    /// </summary>
    public static TileEdges FacesOf(World world, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(world);
        turf cell = MapView.TurfAt(world, x, y, z);
        if (cell == null)
            return TileEdges.None;
        TileEdges faces = TileEdges.None;
        foreach (atom thing in cell.dm_held)
        {
            if (thing is mob || thing is not atom_movable movable)
                continue;
            if (Dm.truthy(movable.density) && BlocksFace(movable))
                faces |= TileFaces.OfDir((int)movable.dir); // an ON_BORDER_1 object blocks the faces it covers, its dir
        }
        return faces;
    }

    /// <summary>
    /// M3: whether movement from (x, y, z) across its face toward the cardinal <paramref name="dir"/> is blocked by a face
    /// (<see cref="TileFaces.IsFaceBlocked"/> over <see cref="CollisionView"/>): the predicate the bots' route planner and
    /// the tests share with the controller.
    /// </summary>
    public static bool IsFaceBlocked(World world, int x, int y, int z, byte dir) => TileFaces.IsFaceBlocked(CollisionView(world), x, y, z, dir);

    /// <summary>The server's collision view over <paramref name="world"/> for the controller (the phase's own; tests): <see cref="IsSolid"/> and the registered standing capsules.</summary>
    public static ICollisionView CollisionView(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Seam.Of(world).View;
    }
}
