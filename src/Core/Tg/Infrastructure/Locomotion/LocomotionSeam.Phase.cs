using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owner: M2 P4 (docs/ai/specs/locomotion.md section 4). The locomotion phase: one step per registered capsule mob in dm_id
// order, and the application of its events as tg calls (DV15, DV16).

public static partial class LocomotionSeam
{
    // ---- the phases ----

    // Each capsule's step is its own call, detached as BYOND starts a client's Move: an isolation boundary (a runtime in
    // one mob's Move does not stop the others), run with usr the mob, as BYOND runs client/Move. A step that sleeps (no tg
    // proc on the M2 path does) finishes later; its capsule is skipped until it has.
    private static void RunPhase(World world)
    {
        Seam seam = Seam.Of(world);
        RunGhosts(world, seam);
        RunMechs(world, seam);
        if (seam.Capsules.Count == 0)
            return;
        var ids = new List<long>(seam.Capsules.Keys); // a step may register or unregister mobs
        foreach (long id in ids)
        {
            if (!seam.Capsules.TryGetValue(id, out Capsule c))
                continue;
            if (QDELETED(c.Mob))
            {
                seam.Capsules.Remove(id);
                continue;
            }
            if (c.Busy)
                continue;
            object caller_usr = world.usr;
            try
            {
                world.usr = c.Mob;
                Waiting.Detach(StepCapsule(world, seam, c));
            }
            finally
            {
                world.usr = caller_usr;
            }
        }
        RunAiEyes(world, seam); // DV423: after the AIs' gates granted their eyes' flights
    }

    // After the drain that follows the locomotion phase: every capsule follows its mob's loc (no tg code runs).
    private static void RunFollowPhase(World world)
    {
        Seam seam = Seam.Of(world);
        FollowGhosts(seam);
        FollowMechs(seam);
        FollowAiEyes(seam);
        if (seam.Capsules.Count == 0)
            return;
        var ids = new List<long>(seam.Capsules.Keys);
        foreach (long id in ids)
        {
            Capsule c = seam.Capsules[id];
            if (QDELETED(c.Mob))
            {
                seam.Capsules.Remove(id);
                continue;
            }
            if (c.Busy)
                continue;
            SyncPosture(c);
            Follow(c);
        }
    }

    // ---- one capsule ----

    private static async DmTask StepCapsule(World world, Seam seam, Capsule c)
    {
        c.Busy = true;
        try
        {
            mob_living mob = c.Mob;
            ForgetOldContacts(c, world.Tick);
            SyncPosture(c);
            Follow(c); // a loc change tg made since the last phase: a shove, forceMove, a teleport, PushAM's follow

            // DEVIATION(DV416): a thrown capsule flies the flight seam's arc; no wish moves it in the air (its client's prediction is told it cannot move)
            if (FlightSeam.Of(world).IsFlying(mob))
            {
                // DV435: a fall's landing is known ahead, so the prediction walks on from the same frame as the server
                c.NextParams = NextParamsOf(c, c.Scaled(seam.SpeedScale)) with { CanMove = false, UnblockTicks = FlightSeam.Of(world).FallLandsIn(mob) };
                return;
            }

            MotionInput frame = c.Frame;
            byte wishDir = WishDir(frame.MoveX, frame.MoveY);
            // tg's client/calculate_move_dir (bindings_atom.dm:40): the held movement keys as the client's
            // intended_direction, which keyLoop moves by and the jetpack, the mob swap and the scope read
            // (components/jetpack.dm:136, :192; living.dm:310; components/scope.dm:66). DEVIATION(DV15): the wish's BYOND
            // direction stands for the held keys; keyLoop leaves capsule mobs to the bridge (bindings_atom.cs).
            if (mob.client != null)
                mob.client.intended_direction = wishDir;
            if (!c.OnGrid)
            {
                // In a locker, a mech, a body bag (or nullspace): no capsule step; a wish is relayed as tg's client/Move does.
                // The slide of the carrier's move goes on (Follow), as the client's replay of the frame steps it.
                if (c.State.Slide.Active)
                    MotionStep.Step(ref c.State, in frame, c.NextParams, seam.View, null, c.Id);
                // DV358: an exosuit with a capsule reads its driver's frame itself (RunMechs); tg's relaymove would step it a tile
                bool driven = mob.loc is obj_vehicle_sealed_mecha mech && seam.Mechs.ContainsKey(mech.dm_id);
                if (!driven && wishDir != MotionDir.None && mob.client != null && mob.loc != null)
                    await mob.client.capsule_move_gate(get_step(mob, wishDir), (float)wishDir);
                if (!QDELETED(mob))
                {
                    Follow(c);
                    c.NextParams = NextParamsOf(c, c.Scaled(seam.SpeedScale));
                }
                return;
            }

            // Facing (scope 2b): the yaw quantized to 4 directions with 5 degree hysteresis gives dir; setDir only on a
            // change. A mob that never got a frame keeps the dir tg gave it (a dummy); one tg would not let turn (CanFace)
            // keeps its dir too.
            // DEVIATION(DV48): while the mob pulls a dense object, client/Move's pull turn (capsule_move_tail) owns dir: tg's
            // puller faces what it drags after every step and keeps that dir standing still.
            if (c.Framed && CanFace(mob, wishDir) && !PullTurnsAround(mob))
            {
                byte current = (byte)Dm.num(mob.dir);
                byte facing = MotionFacing.DirOf(frame.Yaw, current);
                if (facing != current)
                    await mob.setDir((float)facing);
            }

            // DEVIATION(DV69): zero gravity moves the capsule continuously too (the owner, 2026-10-19, amending D146): the wish as on a floor
            // while tg's gate lets it (Process_Spacemove: something to push off, a jetpack), else tg's drift (DriftOf); every crossing is still
            // tg's Move, whose Moved starts or turns the drift (newtonian_move), and a drift's crossings are its loop's moves.
            bool weightless = UsesZeroG(mob);
            if (weightless)
                c.State.Driver = MotionDriver.ZeroG;
            else if (c.State.Driver == MotionDriver.ZeroG)
            {
                c.State.Driver = MotionDriver.Grounded;
                SyncPosture(c);
            }

            // The gate (client/Move's checks, DV15) runs each tick the wish is not zero; tg's speed is read every tick.
            bool canMove;
            byte direct = MotionDir.None; // the direction client/Move's direct holds after the gate's PRE_MOVE handlers
            if (wishDir != MotionDir.None && weightless && world.Tick < c.GateAgainTick)
            {
                canMove = c.GateAnswer && PreviewCanMove(mob); // a cached grant cannot outlive its fuel or handhold
                direct = c.GateDir;
            }
            else if (wishDir != MotionDir.None)
            {
                client player = mob.client;
                if (player != null)
                    player.dm_capsule_direct = null;
                canMove = player != null && Dm.truthy(await player.capsule_move_gate(get_step(mob, wishDir), (float)wishDir));
                if (canMove && player.dm_capsule_direct != null)
                    direct = (byte)Dm.num(player.dm_capsule_direct);
                if (weightless && !QDELETED(mob))
                {
                    c.GateAgainTick = world.Tick + MoveDelayTicks(Dm.num(mob.cached_multiplicative_slowdown));
                    c.GateAnswer = canMove;
                    c.GateDir = direct != wishDir ? direct : MotionDir.None;
                }
            }
            else
            {
                canMove = PreviewCanMove(mob);
            }
            // DEVIATION(DV15): the capsule moves the way client/Move's direct points after its COMSIG_MOB_CLIENT_PRE_MOVE (a confusion's
            // scramble, through move_args): the wish vector turned to it, its length kept. tg's client/Move runs once a move delay, so the
            // direction a step takes is the one of the gate at the step's start, held for that move delay (a confusion rolls its scramble
            // once a tg step, though the gate's checks and signals still run each tick); only a mob with a PRE_MOVE handler keeps steps.
            byte moveDir = wishDir;
            if (weightless)
            {
                if (canMove && c.GateDir != MotionDir.None)
                    moveDir = c.GateDir;
            }
            else if (canMove && wishDir != MotionDir.None && Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_MOVE]))
            {
                if (world.Tick >= c.TurnAgainTick || wishDir != c.TurnWish)
                {
                    c.TurnDir = direct != MotionDir.None && direct != wishDir ? direct : MotionDir.None;
                    c.TurnWish = wishDir;
                    c.TurnAgainTick = world.Tick + MoveDelayTicks(Dm.num(mob.cached_multiplicative_slowdown));
                }
                if (c.TurnDir != MotionDir.None)
                    moveDir = c.TurnDir;
            }
            if (moveDir != wishDir)
                frame = TurnWish(frame, wishDir, moveDir);
            if (QDELETED(mob) || !ReferenceEquals(seam.Capsules.GetValueOrDefault(c.Id), c))
                return;
            SyncPosture(c);
            Follow(c); // a relay or a signal handler may have moved the mob
            // DEVIATION(DV48): tg's living/Move refreshes the pull slowdown before every tile step (living.dm:1061,
            // update_pull_movespeed, so a pull's first step is already slowed); the capsule's step is each tick, so it is
            // refreshed before each step, and a pull started standing still changes the speed before the first move.
            if (Dm.truthy(mob.pulling))
                await mob.update_pull_movespeed();
            c.Slowdown = Dm.num(mob.cached_multiplicative_slowdown);
            (short driftX, short driftY) = weightless ? DriftOf(mob) : ((short)0, (short)0);
            var parameters = Carried(new MotionParams(MotionMath.TilesPerTickFor(c.Slowdown, c.Scaled(seam.SpeedScale)), MotionMath.DefaultRadius, canMove, MoveDelayTicks(c.Slowdown), IsNoClip(mob), driftX, driftY, HAS_TRAIT(mob, TRAIT_ON_CLIMBABLE),
                Feel: seam.Feel, Belt: BeltsAround(c), PassesFlaps: PassesFlaps(mob)), c);

            var events = new MotionEvents();
            int fromX = c.State.X, fromY = c.State.Y;
            // the owner (2026-10-26): a mob tg will not let turn (unconscious, dead, buckled) keeps its replicated yaw, so the others
            // see a still body however its player looks around
            MotionInput stepFrame = c.Framed && !CanFace(mob, wishDir) ? frame with { Yaw = c.State.Yaw } : frame;
            // DV534: a step that starts with a handhold in reach may leave it behind: tg's push off (Moved's newtonian_move) follows it
            bool heldAtStart = weightless && canMove && wishDir != MotionDir.None && Waiting.NoSleep(mob.get_spacemove_backup(0, FALSE)) != null;
            MotionState before = c.State;
            c.Pushed = false;
            MotionStep.Step(ref c.State, in stepFrame, in parameters, seam.View, events, c.Id);
            int dx = c.State.X - fromX, dy = c.State.Y - fromY;
            float? inertiaAngle = dx == 0 && dy == 0 ? null : (float)((Math.Atan2(dx, dy) * 180 / Math.PI + 360) % 360);
            await ApplyEvents(world, seam, c, events, parameters, inertiaAngle);
            // DEVIATION(DV535): a press that pushed (PushAM) freed the tile within this step: the step is taken again through it, so the
            // press costs no time (the predictor takes it again on its guess of the push)
            if (c.Pushed && !QDELETED(mob) && StandsOn(mob, c.State.TileX, c.State.TileY, c.State.Z) && MotionStep.WalksOnAfterPush(in c.State, events, 0))
            {
                var again = new MotionEvents();
                MotionStep.StepAgain(ref c.State, in before, in stepFrame, in parameters, seam.View, again, c.Id);
                await ApplyEvents(world, seam, c, again, parameters, inertiaAngle);
                dx = c.State.X - fromX;
                dy = c.State.Y - fromY;
            }
            c.Pushed = false;
            if (events.Drifting && !QDELETED(mob) && UsesZeroG(mob))
                await HaltInReach(mob);
            if (!events.Drifting && !parameters.NoClip && !c.State.Slide.Active)
                Stride(seam, c, dx, dy);

            // DV177: thrust within a tile is still an impulse. Waiting for Moved would discard a short tap's
            // direction on release. Keep tg's newtonian checks/force and leave the loop's own timer alone.
            if (!QDELETED(mob) && canMove && wishDir != MotionDir.None && !events.Drifting
                && !parameters.NoClip && !c.State.Slide.Active && inertiaAngle.HasValue && UsesZeroG(mob) && (heldAtStart || PreviewCanMove(mob)))
                await mob.newtonian_move(inertiaAngle.Value, force_loop: FALSE);

            if (!QDELETED(mob))
            {
                SyncPosture(c);
                Follow(c); // the invariant: the centre's tile (a slide's target while sliding) is loc
                c.NextParams = NextParamsOf(c, c.Scaled(seam.SpeedScale), gateRefused: wishDir != MotionDir.None && !canMove);
            }
        }
        finally
        {
            c.Busy = false;
        }
    }

    // Whether this capsule mob is weightless (DV69: its capsule pushes off, flies by a jetpack and drifts): a mob on a turf without
    // gravity whose own movement depends on pushing off or a jetpack, so that newtonian drift may follow its steps. A mob tg lets move freely without gravity (TRAIT_SPACEWALK, FLYING,
    // TRAIT_FREE_FLOAT_MOVEMENT: Process_Spacemove is TRUE and newtonian_move never drifts it, mob_movement.dm:266-275), the
    // dev noclip (DV29) and a buckled mob (client/Move relays its moves) keep the capsule's continuous movement (DV15).
    internal static bool UsesZeroG(mob_living mob)
    {
        if (mob.loc is not turf || IsNoClip(mob) || Dm.truthy(mob.buckled))
            return false;
        if (HAS_TRAIT(mob, TRAIT_SPACEWALK) || Dm.truthy(Dm.bitand(mob.movement_type, FLYING)) || HAS_TRAIT(mob, TRAIT_FREE_FLOAT_MOVEMENT))
            return false;
        return !Dm.truthy(mob.has_gravity());
    }

    // The events in order, as tg calls (D105): a crossing is tg's real Move with PASSMOB for that call (DV16) and
    // update_dir FALSE (DV15), followed by client/Move's tail; a refused one clamps the capsule back into its tile and
    // ends the movement of this step; a press is a Move into the blocked tile, at tg's move delay (so Bump runs); a contact
    // runs MobBump's signal part, at most once per move delay per pair. When tg's code put the mob somewhere else the
    // remaining movement events are dropped (the capsule follows loc instead).
    private static async DmTask ApplyEvents(World world, Seam seam, Capsule c, MotionEvents events, MotionParams parameters, float? inertiaAngle)
    {
        mob_living mob = c.Mob;
        int z = c.State.Z;
        bool moving = true;
        for (int i = 0; i < events.Count; i++)
        {
            if (QDELETED(mob))
                return;
            MotionEvent e = events[i];
            switch (e.Kind)
            {
                case MotionEventKind.Crossed:
                {
                    if (!moving)
                        break;
                    if (!StandsOn(mob, e.FromX, e.FromY, z))
                    {
                        moving = false; // tg moved it elsewhere: Follow
                        break;
                    }
                    turf to = locate(e.ToX, e.ToY, z);
                    if (!(parameters.NoClip ? await IncorporealMove(mob, to) : events.Drifting ? await DriftMove(mob, to, e.Dir) : await BridgeMove(c, to, e.Dir, c.Scaled(seam.SpeedScale), inertiaAngle)))
                    {
                        moving = false;
                        if (StandsOn(mob, e.FromX, e.FromY, z))
                            MotionStep.ClampToTile(ref c.State, e.FromX, e.FromY, MotionMath.DefaultRadius);
                    }
                    break;
                }
                case MotionEventKind.Pressing:
                {
                    if (!moving || !StandsOn(mob, e.FromX, e.FromY, z))
                        break;
                    turf to = locate(e.ToX, e.ToY, z);
                    if (to == null)
                        break; // the map's edge: tg's client/Move gets no new_loc and stops (:53)
                    if (events.Drifting ? await DriftMove(mob, to, e.Dir) : await BridgeMove(c, to, e.Dir, c.Scaled(seam.SpeedScale), inertiaAngle))
                        moving = false; // the face was not blocked after all (a stale grid): the capsule follows loc
                    break;
                }
                case MotionEventKind.Contact:
                {
                    if (!seam.Capsules.TryGetValue(e.OtherId, out Capsule other) || QDELETED(other.Mob))
                        break;
                    long tick = world.Tick;
                    if (c.ContactReady.TryGetValue(other.Id, out long ready) && tick < ready)
                        break;
                    c.ContactReady[other.Id] = tick + parameters.MoveDelayTicks;
                    mob.capsule_mob_bump(other.Mob);
                    break;
                }
            }
        }
    }

    // tg's Move of the capsule mob into a neighbouring turf: PASSMOB for this call (DEVIATION(DV16): tg's own mob-swap
    // trick, living.dm:232-245, so a turf with a standing mob is entered, not bumped), update_dir FALSE (DEVIATION(DV15): dir
    // follows the view, as tg's move loops pass it), no glide override; then client/Move's tail when it moved. True when
    // the mob is on that turf after it.
    private static async DmTask<bool> BridgeMove(Capsule c, turf to, byte dir, float speedScale, float? inertiaAngle)
    {
        if (to == null)
            return false;
        mob_living mob = c.Mob;
        object old_dir = mob.dir;
        atom old_loc = mob.loc;
        // tg: code/modules/mob/mob_movement.dm:132  mob.set_glide_size(DELAY_TO_GLIDE_SIZE(glide_delay)) // set it now in case of
        // pulled objects. DEVIATION(DV48): the capsule's delay for a tile (its slowdown over the speed scale) stands for tg's
        // glide_delay, so a pulled thing glides behind the capsule at its speed (move_from_pull passes the puller's glide_size).
        mob.set_glide_size(DELAY_TO_GLIDE_SIZE(Dm.num(mob.cached_multiplicative_slowdown) / speedScale));
        bool had_passmob = Dm.truthy(Dm.bitand(mob.pass_flags, PASSMOB));
        object moved;
        Dm.combine(ref mob.pass_flags, PASSMOB);
        float? previousAngle = c.CrossingInertiaAngle;
        c.CrossingInertiaAngle = inertiaAngle;
        c.Bridging = true;
        try
        {
            moved = await mob.Move(to, (float)dir, 0, FALSE);
        }
        finally
        {
            c.Bridging = false;
            c.CrossingInertiaAngle = previousAngle;
            if (!had_passmob)
                Dm.mask(ref mob.pass_flags, Dm.bitnot(PASSMOB));
        }
        bool arrived = Dm.truthy(moved) && ReferenceEquals(mob.loc, to);
        client player = mob.client;
        if (arrived && player != null)
            await player.capsule_move_tail((float)dir, old_dir, old_loc);
        return arrived;
    }

    // DEVIATION(DV233): a capsule's footsteps follow the distance it walks, not its tile crossings: each half tile of travel is one of
    // tg's footstep Moves for the footstep element (which still sounds every other one: a step a tile, where tg's is every second tile;
    // the owner, 2026-10-31: more often, as a real stride, then 30 % less often than that, and set apart for running and walking in the
    // dev menu), so the rhythm stays even along a tile's edge, on a diagonal and when turning. The bridge's own Move plays none (IsBridging).
    internal const int DefaultStride = 183; // half a tile over 0.7: a step every 1.43 tiles

    /// <summary>The dev command <c>footstride</c>: the tiles walked per footstep stride (two strides a step), running or walking.</summary>
    public static void SetFootstride(World world, bool walk, float tiles)
    {
        if (!(tiles > 0f) || tiles > MaxFootstride)
            throw new ArgumentOutOfRangeException(nameof(tiles), tiles, "A stride is a positive number of tiles up to 4.");
        int subunits = Math.Max(1, (int)MathF.Round(tiles * MotionMath.SubunitsPerTile));
        Seam seam = Seam.Of(world);
        if (walk)
            seam.WalkStride = subunits;
        else
            seam.RunStride = subunits;
    }

    public const float MaxFootstride = 4f;

    private static void Stride(Seam seam, Capsule c, int dx, int dy)
    {
        mob_living mob = c.Mob;
        if (dx == 0 && dy == 0)
        {
            // the owner (2026-10-31): stopping lands the foot in the air (COMSIG_LIVING_CAPSULE_HALT); the next walk starts a new stride
            if (c.Striding && !QDELETED(mob) && mob.loc is turf && Dm.truthy(mob._listen_lookup?[COMSIG_LIVING_CAPSULE_HALT])) // SEND_SIGNAL: a mob without footsteps has no listener
                mob._SendSignal(COMSIG_LIVING_CAPSULE_HALT, Dm.list(mob));
            c.Striding = false;
            c.StrideTravel = 0;
            return;
        }
        c.Striding = true;
        c.StrideTravel += (int)Math.Sqrt((double)dx * dx + (double)dy * dy);
        int stride = mob.move_intent == MOVE_INTENT_WALK ? seam.WalkStride : seam.RunStride;
        if (c.StrideTravel < stride)
            return;
        c.StrideTravel %= stride;
        if (!QDELETED(mob) && mob.loc is turf)
            mob._SendSignal(COMSIG_LIVING_CAPSULE_STRIDE, Dm.list(mob, mob.loc, mob.dir, FALSE, null, TRUE));
    }

    // DV233: whether this movable's Moved is a capsule's bridge crossing (its footstep comes from Stride instead).
    public static bool IsBridging(atom_movable moving)
    {
        World world = World.CurrentOrNull;
        if (world != null && moving is obj_vehicle_sealed_mecha mech)
            return Seam.Of(world).Mechs.TryGetValue(mech.dm_id, out MechCapsule m) && m.Bridging;
        return world != null && moving is mob_living mob && Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c)
            && ReferenceEquals(c.Mob, mob) && c.Bridging;
    }

    // DV535: PushAM of a capsule's press pushed what it pressed: true when the capsule walks on into the freed tile itself (noted, so its
    // step is taken again through that tile), false when tg's own step after the push should run.
    public static bool WalksOnAfterPush(mob_living mob)
    {
        World world = World.CurrentOrNull;
        if (world == null || !Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c) || !ReferenceEquals(c.Mob, mob) || !c.Bridging)
            return false;
        c.Pushed = true;
        return true;
    }

    // DV69: tg's Moved starts drift from its tile-step direction (atoms_movable.dm:900).
    // A bridge crossing instead carries the actual continuous displacement's angle, including wall sliding.
    // Only that Move call uses it; throws, pushes, ordinary tg moves and existing drift keep their own angles.
    public static object MovementInertiaAngle(atom_movable moving, object tileDirection)
    {
        World world = World.CurrentOrNull;
        if (world != null && moving is mob_living mob && Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c)
            && ReferenceEquals(c.Mob, mob) && c.CrossingInertiaAngle is float angle)
            return angle;
        return dir2angle(tileDirection);
    }

    // DEVIATION(DV29): an incorporeal capsule's crossing (the dev noclip): tg's Process_Incorpmove's INCORPOREAL_MOVE_BASIC
    // branch (mob_movement.dm:186-190), `forceMove(get_step(L, direct))`, without its setDir (DV15: dir follows the view).
    // True when the mob is on that turf after it.
    private static async DmTask<bool> IncorporealMove(mob_living mob, turf to)
    {
        if (to == null)
            return false;
        await mob.forceMove(to);
        return ReferenceEquals(mob.loc, to);
    }

    // DEVIATION(DV69): a drifting capsule's crossing is its drift loop's move (smooth_move's move, movement_types.dm:1038, with the
    // drift handler's pre- and post-process around it, drift_handler.dm): inertia_moving while tg's Move runs (so its Moved does not
    // push again), then after_move: a failed move ends the drift (a wall), and one that leaves the mob where it can grab something
    // halts it (Process_Spacemove's continuous check). True when the mob is on that turf after it.
    private static async DmTask<bool> DriftMove(mob_living mob, turf to, byte dir)
    {
        datum_drift_handler drift = mob.drift_handler;
        if (to == null || drift == null || QDELETED(drift))
            return false;
        drift.before_move(null);
        bool arrived;
        try
        {
            object moved = await mob.Move(to, (float)dir, FALSE, FALSE);
            arrived = Dm.truthy(moved) && ReferenceEquals(mob.loc, to);
        }
        finally
        {
            mob.inertia_moving = FALSE;
        }
        // tg's after_move is the loop's own signal (drift_handler.dm:143): a loop that died in the step sends none
        if (!QDELETED(drift) && drift.drifting_loop != null)
            drift.after_move(null, arrived ? MOVELOOP_SUCCESS : MOVELOOP_FAILURE, drift.drifting_loop.delay);
        return arrived;
    }

    /// <summary>
    /// DEVIATION(DV534): whether <paramref name="candidate"/>'s tile is within the hand's reach of <paramref name="mob"/>'s capsule
    /// (<see cref="MotionMath.HandReach"/> from its centre), so tg's <c>get_spacemove_backup</c> may hold on to it; a mob without a
    /// capsule on a turf reaches its range 1, as in tg.
    /// </summary>
    public static bool InHandReach(mob mob, atom candidate)
    {
        World world = World.CurrentOrNull;
        if (world == null || mob is not mob_living living || mob.loc is not turf || get_turf(candidate) is not turf t
            || !Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c) || !ReferenceEquals(c.Mob, living) || !c.OnGrid)
            return true;
        return MotionMath.InHandReach(c.State.X, c.State.Y, (int)t.x, (int)t.y);
    }

    // DEVIATION(DV534): tg checks a drift's halt after each of its loop's moves (drift_handler.dm:153 after_move); the capsule drifts a
    // little each tick and the hand's reach is shorter than a tile, so the halt is checked each drifting tick: once a handhold comes in
    // reach, Process_Spacemove's continuous check holds the mob there and glide_to_halt ends the drift with tg's input block.
    private static async DmTask HaltInReach(mob_living mob)
    {
        datum_drift_handler drift = mob.drift_handler;
        datum_move_loop_smooth_move loop = drift?.drifting_loop;
        if (drift == null || QDELETED(drift) || loop == null || QDELETED(loop) || !Dm.truthy(Dm.bitand(loop.status, MOVELOOP_STATUS_RUNNING)))
            return;
        object dir = angle2dir(Dm.num(loop.angle));
        if (await mob.get_spacemove_backup(dir, TRUE) == null)
            return; // nothing in reach: no signals sent for it
        if (Dm.truthy(await mob.Process_Spacemove(Dm.num(dir), continuous_move: TRUE)) && !QDELETED(drift) && drift.drifting_loop != null)
            drift.glide_to_halt(loop.delay);
    }

    // DEVIATION(DV69): tg's drift of a weightless capsule mob as a velocity, subunits a tick: its drift loop's angle (BYOND's: clockwise
    // from north) at a tile per loop delay (smooth_move goes a tile a delay along its angle, a diagonal tile 1.4 delays). None without a
    // drift, or while the loop does not run.
    internal static (short X, short Y) DriftOf(mob_living mob)
    {
        datum_drift_handler drift = mob.drift_handler;
        datum_move_loop_smooth_move loop = drift?.drifting_loop;
        if (drift == null || QDELETED(drift) || loop == null || QDELETED(loop) || !(loop.delay > 0f))
            return (0, 0);
        if (!Dm.truthy(Dm.bitand(loop.status, MOVELOOP_STATUS_RUNNING)))
            return (0, 0);
        float tiles = MotionMath.TickDeciseconds / loop.delay;
        double radians = (loop.angle ?? 0f) * Math.PI / 180.0;
        int x = MotionMath.Quantize((float)(Math.Sin(radians) * tiles));
        int y = MotionMath.Quantize((float)(Math.Cos(radians) * tiles));
        return ((short)Math.Clamp(x, short.MinValue, short.MaxValue), (short)Math.Clamp(y, short.MinValue, short.MaxValue));
    }

    /// <summary>
    /// DV69: whether the locomotion bridge moves <paramref name="moving"/>'s drift itself (a registered weightless capsule mob on a turf):
    /// its drift loop then keeps its timing and checks but does not move it (smooth_move's move).
    /// </summary>
    public static bool DriftsContinuously(atom_movable moving)
    {
        if (moving is not mob_living mob)
            return false;
        World world = World.CurrentOrNull;
        return world != null && Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c) && ReferenceEquals(c.Mob, mob)
            && c.OnGrid && UsesZeroG(mob);
    }

    // DEVIATION(DV29): the capsule of a mob with tg's INCORPOREAL_MOVE_BASIC ignores everything but the map's edge, as tg's
    // incorporeal step goes through walls; tg's other incorporeal kinds (the shadow walk, the jaunt) keep the gate's refusal.
    internal static bool IsNoClip(mob_living mob) => Dm.eq(mob.incorporeal_move, INCORPOREAL_MOVE_BASIC) && !Dm.truthy(is_secret_level(mob.z));

    // ---- following tg ----

    // tg's body position into the state: a lying capsule is prone (no capsule collisions, D51). The reserved drivers are
    // left alone.
    internal static void SyncPosture(Capsule c)
    {
        bool lying = Dm.num(c.Mob.body_position) == LYING_DOWN;
        c.State.Posture = lying ? Posture.Lying : Posture.Standing;
        if (c.State.Driver == MotionDriver.Grounded || c.State.Driver == MotionDriver.Prone)
            c.State.Driver = lying ? MotionDriver.Prone : MotionDriver.Grounded;
    }

    // The capsule follows a loc change it did not make (MotionStep.Displace): a neighbouring turf slides over the mob's
    // glide time, anything else snaps; inside an object the capsule stands on the outer turf, and slides with the object
    // that carries it (a transit tube pod, a pulled locker) over that object's glide time.
    internal static void Follow(Capsule c)
    {
        mob_living mob = c.Mob;
        turf here = mob.loc as turf;
        if (here == null)
        {
            atom_movable carrier = mob;
            while (carrier.loc is atom_movable outerMovable)
                carrier = outerMovable;
            if (carrier.loc is turf outer)
                MotionStep.Displace(ref c.State, (int)outer.x, (int)outer.y, (int)outer.z, GlideTicks(carrier.glide_size));
            return;
        }
        if (World.CurrentOrNull is World flightWorld && FlightSeam.Of(flightWorld).IsFlying(mob))
            return; // DV416: the flight seam places it (PlaceInFlight)
        (int tx, int ty, int tz) = c.Tile;
        int x = (int)here.x;
        int y = (int)here.y;
        int z = (int)here.z;
        if (x == tx && y == ty && z == tz)
        {
            // DEVIATION(DV187): a buckled mob sits at its tile's centre, where tg draws it; buckle_mob's Move(loc) onto the
            // tile it already stands on does not move the capsule, so it glides there.
            int cx = x * MotionMath.SubunitsPerTile, cy = y * MotionMath.SubunitsPerTile;
            if (Dm.truthy(mob.buckled) && !c.State.Slide.Active && (c.State.X != cx || c.State.Y != cy))
                c.State.Slide = new Slide(cx, cy, Math.Max((ushort)1, GlideTicks(mob.glide_size)));
            return;
        }
        // M4c slice 7: a weightless capsule slides two tiles too (a client/Move step and a drift step in one tick, glided by BYOND
        // from where the mob is drawn); anything else keeps one.
        MotionStep.Displace(ref c.State, x, y, z, GlideTicks(mob.glide_size), c.State.Driver == MotionDriver.ZeroG ? 2 : 1);
        // DEVIATION(DV397): a climber arrives before the ladder of the other level, where the client drew the climb (LadderPull); a level change
        // onto a ladder's tile is a climb (a ladder holds what stands above it: no fall ends there)
        if (z != tz && World.CurrentOrNull is World world && LadderAt(world, x, y, z))
            (c.State.X, c.State.Y) = LadderBody.Front(x, y, MotionMath.Quantize(MotionMath.DefaultRadius));
    }

    // tg's glide time of one tile in ticks: ICON_SIZE_ALL pixels at glide_size pixels per tick (8 by default: 4 ticks).
    internal static ushort GlideTicks(float glide_size)
    {
        if (!(glide_size > 0f))
            return 0;
        float ticks = MathF.Ceiling(ICON_SIZE_ALL / glide_size);
        return (ushort)Math.Clamp((int)ticks, 1, ICON_SIZE_ALL);
    }

    private static bool StandsOn(mob_living mob, int x, int y, int z)
        => mob.loc is turf t && (int)t.x == x && (int)t.y == y && (int)t.z == z;

    private static void ForgetOldContacts(Capsule c, long tick)
    {
        if (c.ContactReady.Count == 0)
            return;
        List<long> done = null;
        foreach (KeyValuePair<long, long> pair in c.ContactReady)
        {
            if (pair.Value <= tick)
                (done ??= new List<long>()).Add(pair.Key);
        }
        if (done != null)
        {
            foreach (long id in done)
                c.ContactReady.Remove(id);
        }
    }

    // ---- parameters ----

    // The BYOND direction of a wish vector, as keyLoop would hold keys: a component counts when it is more than 22.5
    // degrees away from the other axis (tan 22.5 is 0.414).
    public static byte WishDir(sbyte moveX, sbyte moveY) => SectorDir(moveX, moveY);

    // DV15: the frame's wish vector (x east, y north) turned clockwise by the BYOND angle from one direction to another (dir2angle:
    // north 0, east 90), its length kept; the rest of the frame unchanged.
    internal static MotionInput TurnWish(in MotionInput frame, byte from, byte to)
    {
        double degrees = Dm.num(dir2angle((float)to)) - Dm.num(dir2angle((float)from));
        double r = degrees * Math.PI / 180.0;
        double cos = Math.Cos(r), sin = Math.Sin(r);
        double x = frame.MoveX * cos + frame.MoveY * sin;
        double y = -frame.MoveX * sin + frame.MoveY * cos;
        sbyte Clamp(double v) => (sbyte)Math.Clamp((int)Math.Round(v), -MotionInput.MaxMove, MotionInput.MaxMove);
        return frame with { MoveX = Clamp(x), MoveY = Clamp(y) };
    }

    // The bridge's 8-sector rule for any vector (x east, y north): the cardinal or diagonal direction whose 45-degree sector
    // holds it, the sectors bounded at 22.5 degrees from each axis (tan 22.5 is 0.414); 0 for a zero vector. WishDir and the
    // shove direction of two capsule mobs (DV17) use it.
    internal static byte SectorDir(long x, long y)
    {
        long ax = Math.Abs(x);
        long ay = Math.Abs(y);
        byte dir = MotionDir.None;
        if (ay * 1000 > ax * 414)
            dir |= y > 0 ? MotionDir.North : MotionDir.South;
        if (ax * 1000 > ay * 414)
            dir |= x > 0 ? MotionDir.East : MotionDir.West;
        return dir;
    }

    // tg's move delay in ticks: the delay rule of waiting-model.md section 8 (floor with a relative epsilon, at least 1).
    internal static short MoveDelayTicks(float slowdown)
    {
        float ticks = MathF.Floor(slowdown / MotionMath.TickDeciseconds * (1f + 1e-6f));
        return (short)Math.Clamp((int)ticks, 1, short.MaxValue);
    }

    // The gate's answer without running it (no signal is sent, nothing is rolled): what the private block tells the
    // predictor (with the gate's own refusal of the tick, NextParamsOf). The gate itself decides each tick a key is held.
    // A weightless capsule mob (UsesZeroG) moves by tg's client/Move, whose own steps push move_delay: its preview leaves that
    // test out, so CanMove says whether a held key would step once the cadence allows.
    internal static bool PreviewCanMove(mob_living mob)
    {
        client player = mob.client;
        if (player == null || !Dm.truthy(mob.loc))
            return false;
        if (world.time < player.move_delay && !UsesZeroG(mob))
            return false; // client/Move's first test (:47): tg's stops (Process_Grab, a failed resist_grab) hold the capsule
        if (HAS_TRAIT(mob, TRAIT_NO_TRANSFORM) || mob.stat == DEAD)
            return false;
        if (IsNoClip(mob))
            return true; // client/Move moves an incorporeal mob before its other checks (:70), DV29
        if (Dm.truthy(mob.incorporeal_move) || Dm.truthy(mob.remote_control))
            return false;
        if (PreviewGrabHolds(mob))
            return false;
        if (Dm.truthy(mob.buckled))
            return false;
        if (!Dm.truthy(Dm.bitand(mob.mobility_flags, MOBILITY_MOVE)))
            return false;
        if (mob.loc is not turf)
            return false;
        return PreviewSpacemove(mob);
    }

    // Process_Spacemove's answer without its signals (atoms_movable.dm:1287, mob_movement.dm:264), so that a weightless mob's
    // predictor does not walk where the gate refuses every step (the owner's play test 2026-10-09: the view jerked forward and
    // back in space). The checks with side effects or signals are left out: the push off itself (newtonian_move of a loose
    // backup, the halt signal); COMSIG_MOVABLE_SPACEMOVE is asked without being sent (PreviewSpacemoveSignal, M4c slice 6:
    // a lit jetpack lets its wearer move). M4b slice 1: tg's get_spacemove_backup is
    // translated, so a weightless mob next to something it can push off may step, as the gate lets it; the preview asks
    // without a direction (the next frame's is not known), which differs from the gate only where tg's direction rules
    // (priority directions, not pushing off what one walks into) refuse that one direction.
    internal static bool PreviewSpacemove(mob_living mob)
    {
        if (Dm.truthy(mob.has_gravity()))
            return true;
        if (PreviewSpacemoveSignal(mob))
            return true;
        if (Dm.truthy(mob.pulledby) && (mob.pulledby.pulledby != mob || Dm.truthy(mob.moving_from_pull)))
            return true;
        if (Dm.truthy(mob.throwing))
            return true;
        if (HAS_TRAIT(mob, TRAIT_SPACEWALK))
            return true;
        if (Dm.truthy(Dm.bitand(mob.movement_type, FLYING)) || HAS_TRAIT(mob, TRAIT_FREE_FLOAT_MOVEMENT))
            return true;
        if (HAS_TRAIT(mob, TRAIT_NOGRAV_ALWAYS_DRIFT))
            return false;
        return Waiting.NoSleep(mob.get_spacemove_backup(0, FALSE)) != null;
    }

    // COMSIG_MOVABLE_SPACEMOVE's answer to a held step without sending it (atoms_movable.dm:1300): the jetpack's stabilize
    // (components/jetpack.dm:140) asked through its preview, which reads the gas without turning an empty jetpack off.
    // M4c slice 6: the jetpack component is the only listener on Meta's path (the jetpack and, gaps P6, the implantable thrusters);
    // another listener answers no.
    internal static bool PreviewSpacemoveSignal(mob_living mob, bool continuous = false)
    {
        object listeners = mob._listen_lookup?[COMSIG_MOVABLE_SPACEMOVE];
        if (listeners is datum_component_jetpack single)
            return single.preview_stabilize(continuous);
        if (listeners is DmList list)
        {
            foreach (object listener in Dm.each(list))
            {
                if (listener is datum_component_jetpack jetpack && jetpack.preview_stabilize(continuous))
                    return true;
            }
        }
        return false;
    }

    // Process_Grab's answer without its effects (mob_movement.dm:152-164): true where it would stop the move. A grab the
    // mob is held in while incapacitated or restrained stops it; any other grab is resisted (resist_grab, living.dm:1183),
    // which breaks a passive one at once but rolls against an aggressive one (its effective state, here without the
    // COMSIG_MOVABLE_GRABBED_RESISTING handlers): the predictor is told no until the roll succeeds.
    internal static bool PreviewGrabHolds(mob_living mob)
    {
        mob puller = mob.pulledby;
        if (puller == null)
            return false;
        if (ReferenceEquals(puller, mob.pulling) && puller.grab_state == GRAB_PASSIVE)
            return false; // :155, a mutual passive pull is not resisted
        if (HAS_TRAIT(mob, TRAIT_INCAPACITATED) || HAS_TRAIT(mob, TRAIT_RESTRAINED))
            return true; // :157-163
        float effective_grab_state = puller.grab_state; // living.dm:1190-1227
        if (Dm.num(mob.body_position) == LYING_DOWN)
            effective_grab_state++;
        if (HAS_TRAIT(mob, TRAIT_GRABWEAKNESS))
            effective_grab_state++;
        if (Dm.truthy(mob.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))) && (Dm.num(mob.get_fire_loss()) + Dm.num(mob.get_brute_loss())) >= 40)
            effective_grab_state++;
        if (HAS_TRAIT(mob, TRAIT_GRABRESISTANCE))
            effective_grab_state--;
        if (puller is mob_living_carbon_human human_puller && human_puller.get_active_hand() is obj_item_bodypart grabbing_bodypart)
            effective_grab_state += grabbing_bodypart.unarmed_grab_state_bonus;
        return effective_grab_state >= GRAB_AGGRESSIVE;
    }

    // client/Move's pull turn (mob_movement.dm:143-144): a mob pulling a dense object that is not a mob is turned to face it
    // after each step (capsule_move_tail). DEVIATION(DV48): while that holds, the view's yaw does not turn the mob.
    internal static bool PullTurnsAround(mob_living mob)
    {
        atom_movable P = mob.pulling;
        return P != null && !ismob(P) && Dm.truthy(P.density);
    }

    // Whether the yaw may turn the mob (setDir). tg turns a player's mob two ways: client/Move's Move (the gate passes and
    // the mob steps) and face_atom on a click, which refuses a buckled mob and one unconscious or in crit
    // (_onclick/click.dm:426, IS_UNCONSCIOUS_OR_CRIT). So a dead, unconscious or critical body keeps its dir, a buckled one
    // keeps the dir its chair gives it, and a soft crit mob turns only while it crawls. A mob that steers a remote_control (a
    // camera eye) keeps its dir too: client/Move relays its keys before any step (mob_movement.dm:107).
    internal static bool CanFace(mob_living mob, byte wishDir)
    {
        if (mob.remote_control != null)
            return false;
        // DEVIATION(DV421): a leaning mob standing still keeps its back to the wall while the player looks around (tg's lean ends on a
        // turn, and the view turns a player's mob, DV15); a step (COMSIG_MOB_CLIENT_PRE_MOVE) ends the lean as in tg
        if (wishDir == MotionDir.None && HAS_TRAIT_FROM(mob, TRAIT_UNDENSE, LEANING_TRAIT))
            return false;
        if (!Dm.truthy(mob.buckled) && !IS_UNCONSCIOUS_OR_CRIT(mob))
            return true;
        return wishDir != MotionDir.None && PreviewCanMove(mob);
    }

    // The next tick's parameters. CanMove is the preview's after this tick's step, and false where the gate refused the held
    // wish this tick (a COMSIG_MOB_CLIENT_PRE_LIVING_MOVE blocker such as a slip's force_move, a direction Process_Spacemove
    // refuses, a failed grab resist): the predictor follows the server's own answer, not only the preview's.
    internal static MotionParams NextParamsOf(Capsule c, float speedScale, bool gateRefused = false)
    {
        c.Slowdown = Dm.num(c.Mob.cached_multiplicative_slowdown);
        bool canMove = !gateRefused && PreviewCanMove(c.Mob);
        (short driftX, short driftY) = c.OnGrid && UsesZeroG(c.Mob) ? DriftOf(c.Mob) : ((short)0, (short)0);
        (ulong mask, float otherSlowdown) = c.OnGrid && c.Mob.client != null ? GravityAround(c) : (0UL, 0f);
        var here = c.Mob.loc as turf;
        float? intentSlowdown = SlowdownAfterIntentToggle(c.Mob, c.Slowdown);
        return Carried(new MotionParams(MotionMath.TilesPerTickFor(c.Slowdown, speedScale), MotionMath.DefaultRadius, canMove, MoveDelayTicks(c.Slowdown), IsNoClip(c.Mob), driftX, driftY, HAS_TRAIT(c.Mob, TRAIT_ON_CLIMBABLE),
            mask, mask == 0 ? 0 : (int)here.x, mask == 0 ? 0 : (int)here.y, mask == 0 ? 0f : MotionMath.TilesPerTickFor(otherSlowdown, speedScale),
            intentSlowdown is float s ? MotionMath.TilesPerTickFor(s, speedScale) : 0f, intentSlowdown is float d ? MoveDelayTicks(d) : (short)0,
            mask != 0 && SteersWithoutBackup(c.Mob), mask == 0 ? 0f : MotionMath.TickDeciseconds / InertiaDelayFromRest(c.Mob),
            canMove ? (short)0 : DriftBlockTicks(c.Mob),
            canMove && c.OnGrid && driftX == 0 && driftY == 0 && UsesZeroG(c.Mob) && !IsNoClip(c.Mob) && PushDrifts(c.Mob)
                ? (short)Math.Clamp(MotionMath.Quantize(MotionMath.TickDeciseconds / InertiaDelayFromRest(c.Mob)), 0, short.MaxValue) : (short)0,
            Seam.Of(World.Current).Feel, BeltsAround(c), PassesFlaps: PassesFlaps(c.Mob),
            HaltsAtHandhold: (driftX != 0 || driftY != 0) && HaltsAtHandhold(c.Mob),
            SteersByHandhold: c.OnGrid && UsesZeroG(c.Mob) && !IsNoClip(c.Mob) && !SteersWithoutBackup(c.Mob) && !HAS_TRAIT(c.Mob, TRAIT_NOGRAV_ALWAYS_DRIFT)), c);
    }

    // Protocol version 89: whether tg's drift of this mob halts where it can grab something (drift_handler.dm:153 after_move's
    // Process_Spacemove, then glide_to_halt with its client): not a lit jetpack's TRAIT_NOGRAV_ALWAYS_DRIFT, nor DV412's blow.
    // shortcut: a COMSIG_MOB_ATTEMPT_HALT_SPACEMOVE blocker and a delayed loop are not looked at; the server corrects them.
    private static bool HaltsAtHandhold(mob_living mob)
        => mob.client != null && !HAS_TRAIT(mob, TRAIT_NOGRAV_ALWAYS_DRIFT) && mob.drift_handler is { dm_blown: false };

    // DEVIATION(DV347): the running belts around a capsule mob that rides them (RidesBelts), for its capsule to ride continuously on the
    // server and in its client's prediction: every belt of tg's GLOB.conveyors_by_id within the field with its movedir while it operates,
    // at the speed of the belt under the mob (else the nearest's). shortcut: one speed for the field, so a rider crossing onto a belt of
    // another speed is corrected once, and of two belts on one tile the last listed wins (tg's should_move reads the first, locate() in
    // loc); per-tile speeds and tg's order when a map mixes them.
    internal static BeltField BeltsAround(Capsule c)
    {
        if (!RidesBelts(c) || c.Mob.loc is not turf here || GLOB?.conveyors_by_id is not DmList byId)
            return default;
        int x0 = (int)here.x, y0 = (int)here.y, z = (int)here.z;
        var field = new BeltField(0, 0, 0, 0, x0, y0, 0);
        float speed = 0f;
        int nearest = int.MaxValue;
        foreach (object key in Dm.each(byId))
        {
            if (byId[key] is not DmList members)
                continue;
            foreach (obj_machinery_conveyor belt in Dm.each<obj_machinery_conveyor>(members))
            {
                if (belt.loc is not turf t || (int)t.z != z || Dm.eq(belt.operating, obj_machinery_conveyor.CONVEYOR_OFF))
                    continue;
                int dx = (int)t.x - x0, dy = (int)t.y - y0;
                byte dir = (byte)Dm.num(belt.movedir);
                if (Math.Abs(dx) > BeltField.Reach || Math.Abs(dy) > BeltField.Reach
                    || (dir != MotionDir.North && dir != MotionDir.South && dir != MotionDir.East && dir != MotionDir.West))
                    continue;
                field = field.With(dx, dy, dir);
                int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (distance < nearest)
                {
                    nearest = distance;
                    speed = belt.speed;
                }
            }
        }
        if (nearest == int.MaxValue || !(speed > 0f))
            return default;
        // tg's delay, speed * 1 SECONDS deciseconds a tile (conveyor.dm:280), as subunits a tick, times MOVE_SPEED_SCALE (D106) as a mob's own
        // speed is: running stays faster than a belt by tg's ratio
        short subunits = (short)Math.Clamp(MotionMath.Quantize(MotionMath.TickDeciseconds / (speed * 10f) * Seam.Of(World.Current).SpeedScale), 1, short.MaxValue);
        return field with { Speed = subunits };
    }

    // DV347: whether tg's convey component would carry this capsule mob (its should_move, conveyor_movement.dm:25-31: on the ground or out
    // cold, not anchored, with gravity) while its capsule walks on a grid: not weightless (DV69), buckled (what it is buckled to rides) or
    // through the dev noclip (DV29), which tg's convey loop keeps moving.
    internal static bool RidesBelts(Capsule c)
    {
        mob_living mob = c.Mob;
        if (!c.OnGrid || mob.loc is not turf || IsNoClip(mob) || Dm.truthy(mob.buckled) || UsesZeroG(mob))
            return false;
        if (Dm.truthy(Dm.bitand(mob.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)) && !IS_UNCONSCIOUS_OR_CRIT(mob))
            return false;
        return !Dm.truthy(mob.anchored) && Dm.truthy(mob.has_gravity());
    }

    /// <summary>
    /// DV347: whether the locomotion bridge rides <paramref name="moving"/> along the belts itself (a registered capsule mob that
    /// <see cref="RidesBelts"/>): its convey loop then keeps its timing and its enter and exit checks but does not move it.
    /// </summary>
    public static bool RidesBeltsContinuously(atom_movable moving)
    {
        if (moving is not mob_living mob)
            return false;
        World world = World.CurrentOrNull;
        return world != null && Seam.Of(world).Capsules.TryGetValue(mob.dm_id, out Capsule c) && ReferenceEquals(c.Mob, mob) && RidesBelts(c);
    }

    // DEVIATION(DV177): protocol version 51: whether a push from rest surely starts a drift whatever its direction, so the client may predict
    // it at InertiaDelayFromRest's speed: newtonian_move's Process_Spacemove(continuous) answers no (atoms_movable.dm:1287, mob_movement.dm:264)
    // without a backup, or with a lit jetpack's TRAIT_NOGRAV_ALWAYS_DRIFT (jetpack.dm:59). DV534: a backup is only one in the hand's reach,
    // which the client tells for itself (SteersByHandhold): a step that leaves the last one behind drifts.
    internal static bool PushDrifts(mob_living mob)
    {
        if (mob.loc is not turf || Dm.truthy(mob.has_gravity()) || PreviewSpacemoveSignal(mob, continuous: true)
            || (Dm.truthy(mob.pulledby) && (mob.pulledby.pulledby != mob || Dm.truthy(mob.moving_from_pull))) || Dm.truthy(mob.throwing)
            || HAS_TRAIT(mob, TRAIT_SPACEWALK) || Dm.truthy(mob.buckled)
            || Dm.truthy(Dm.bitand(mob.movement_type, FLYING)) || HAS_TRAIT(mob, TRAIT_FREE_FLOAT_MOVEMENT))
            return false;
        // DV534: beside a handhold the push drifts once the step leaves its reach, which the client's predictor tells (SteersByHandhold)
        return true;
    }

    // Protocol version 50: the first of the next ticks (1 the next one) whose gate tg's drift input block (drift_handler.dm:217,
    // allow_final_movement: the glide to a halt on a floor) no longer refuses, for a mob the gate's preview would let move; 0 for none.
    private static short DriftBlockTicks(mob_living mob)
    {
        datum_drift_handler drift = mob.drift_handler;
        if (drift == null || QDELETED(drift) || !Dm.truthy(drift.block_inputs_until) || !PreviewCanMove(mob))
            return 0;
        double left = (Dm.num(drift.block_inputs_until) - Dm.num(world.time)) / MotionMath.TickDeciseconds;
        return left <= 0 ? (short)0 : (short)Math.Min(Math.Ceiling(left), short.MaxValue);
    }

    // Protocol version 50: PreviewSpacemove's answers that need no gravity and nothing to push off (a lit jetpack's stabilizer, a pull, a
    // throw, spacewalking), in its order, so the predictor knows whether a step off a floor into space still steers.
    private static bool SteersWithoutBackup(mob_living mob)
        => PreviewSpacemoveSignal(mob) || (Dm.truthy(mob.pulledby) && (mob.pulledby.pulledby != mob || Dm.truthy(mob.moving_from_pull)))
           || Dm.truthy(mob.throwing) || HAS_TRAIT(mob, TRAIT_SPACEWALK);

    // Protocol version 50: tg's drift_handler/get_loop_delay (drift_handler.dm:225) for a new drift of 1 newton, the one a step off a floor starts.
    private static float InertiaDelayFromRest(mob_living mob)
        => DEFAULT_INERTIA_SPEED / ((1 - INERTIA_SPEED_COEF) + 1 * INERTIA_SPEED_COEF) * Math.Min(Dm.num(mob.inertia_move_multiplier_passive), Dm.num(mob.inertia_move_multiplier_active));

    // Protocol version 49: tg's slowdown after toggle_move_intent (update_move_intent_slowdown puts the other config_walk_run modifier under
    // the same id): the current one's slowdown taken out and the cached other one's put in; null when the mob has neither.
    private static float? SlowdownAfterIntentToggle(mob_living mob, float slowdown)
    {
        if (mob.movespeed_modification?[MOVESPEED_ID_MOB_WALK_RUN] is not datum_movespeed_modifier_config_walk_run current)
            return null;
        Type other = current is datum_movespeed_modifier_config_walk_run_walk ? typeof(datum_movespeed_modifier_config_walk_run_run) : typeof(datum_movespeed_modifier_config_walk_run_walk);
        if (GLOB.movespeed_modification_cache[other] is not datum_movespeed_modifier_config_walk_run next)
            return null;
        bool applies = Dm.truthy(Dm.bitand(current.movetypes, mob.movement_type)) && !Dm.truthy(Dm.bitand(current.blacklisted_movetypes, mob.movement_type));
        return applies ? slowdown - Dm.num(current.multiplicative_slowdown) + Dm.num(next.multiplicative_slowdown) : slowdown;
    }

    // Protocol version 48: where around the mob it would be weightless (UsesZeroG on that tile; bit (dy + 3) * 7 + dx + 3), so that its
    // client predicts the step off a floor into space and back, and its slowdown on the other kind (tg's update_movespeed with FLOATING
    // flipped, which the jetpack's full speed hangs on). Nothing (0) when the 7 x 7 tiles are all alike.
    // M10 slice J: the mask's 48 has_gravity calls were a third of the server's allocation with 100 players, so it is kept while the
    // mob stays on its tile and that tile's gravity holds, for at most GravityReuseTicks (a floor breached beside it shows in half a second).
    internal static (ulong Mask, float OtherSlowdown) GravityAround(Capsule c)
    {
        mob_living mob = c.Mob;
        if (mob.loc is not turf here || IsNoClip(mob) || Dm.truthy(mob.buckled)
            || HAS_TRAIT(mob, TRAIT_SPACEWALK) || Dm.truthy(Dm.bitand(mob.movement_type, FLYING)) || HAS_TRAIT(mob, TRAIT_FREE_FLOAT_MOVEMENT))
            return (0UL, 0f);
        bool centre = !Dm.truthy(mob.has_gravity(here));
        long tick = World.CurrentOrNull?.Tick ?? 0;
        if (!ReferenceEquals(here, c.GravityTile) || centre != c.GravityWeightless || tick < c.GravityTick || tick - c.GravityTick >= GravityReuseTicks)
        {
            c.GravityMask = GravityMask(mob, here, centre);
            c.GravityTile = here;
            c.GravityWeightless = centre;
            c.GravityTick = tick;
        }
        ulong mask = c.GravityMask;
        if (mask == 0 || mask == MotionParams.GravityMaskAll)
            return (0UL, 0f);
        object floating = centre ? Dm.bitand(mob.movement_type, ~(int)FLOATING) : Dm.bitor(mob.movement_type, FLOATING);
        return (mask, SlowdownAs(mob, floating));
    }

    private const int GravityReuseTicks = 10;

    private static ulong GravityMask(mob_living mob, turf here, bool centre)
    {
        ulong mask = 0;
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                bool weightless = dx == 0 && dy == 0 ? centre
                    : locate(here.x + dx, here.y + dy, here.z) is turf t ? !Dm.truthy(mob.has_gravity(t)) : centre;
                if (weightless)
                    mask |= 1UL << ((dy + 3) * 7 + dx + 3);
            }
        }
        return mask;
    }

    // tg's update_movespeed (_movespeed_modifier.dm:175) for another movement_type, without setting anything or sending its signal.
    private static float SlowdownAs(mob_living mob, object movementType)
    {
        float slowdown = 0;
        DmList conflicts = Dm.list();
        foreach (object key in Dm.each(mob.get_movespeed_modifiers()))
        {
            if (mob.movespeed_modification[key] is not datum_movespeed_modifier m)
                continue;
            if (!Dm.truthy(Dm.bitand(m.movetypes, movementType)) || Dm.truthy(Dm.bitand(m.blacklisted_movetypes, movementType)))
                continue;
            if (Dm.truthy(m.conflicts_with))
            {
                if (abs(Dm.num(conflicts[m.conflicts_with])) < abs(Dm.num(m.multiplicative_slowdown)))
                    conflicts[m.conflicts_with] = m.multiplicative_slowdown;
                else
                    continue;
            }
            slowdown += Dm.num(m.multiplicative_slowdown);
        }
        return slowdown;
    }
}
