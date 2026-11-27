using System.Text;
using Anomaly.Core.DmShim;
using static Anomaly.Core.Tg.Defines;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// Owner: M1 P4 (replication), M2 P5 (the version-2 fields), M3 P5 (the version-3 field Light and the entity threshold), M4a P6 (the version-4 field Flags). docs/ai/specs/replication.md section 2.

/// <summary>One whitelisted field (<see cref="NetField"/>) and where the replicator gets it.</summary>
/// <param name="Field">The field (one bit).</param>
/// <param name="Source">Where its value comes from.</param>
/// <param name="Hint">The engine hint that makes an entity a candidate when the field changes, or null when only the sweep finds a change.</param>
public readonly record struct FieldSource(NetField Field, string Source, string Hint);

/// <summary>
/// The replication whitelist (D40, scope 2d "M1 minimum"): exactly the fields of <see cref="NetField"/>, each with the one
/// place the replicator reads it. Nothing else of a tg object is read for the wire: <c>real_name</c>, <c>key</c>,
/// <c>ckey</c>, <c>desc</c>, health and every other tg var stay on the server (a test checks that the table and the wire
/// field set are both <see cref="NetField.All"/>).
/// </summary>
public static class ReplicatedFields
{
    /// <summary>
    /// The entity predicate: a registered <c>/atom/movable</c> whose raw <c>loc</c> is a turf within the protocol's map range
    /// (x and y 1 to <see cref="Limits.MaxMapSize"/>, z 1 to <see cref="Limits.MaxMapLevels"/>) and whose <c>invisibility</c>
    /// is at most <see cref="MaxLayerInvisibility"/> (version 22; version 3 used <see cref="MaxEntityInvisibility"/>, version 2 tg's
    /// <c>INVISIBILITY_ABSTRACT</c>); one above <see cref="MaxEntityInvisibility"/> is in the observer layer (<see cref="LayerOf"/>). A
    /// movable in nullspace or inside another atom has no coordinates: it is not an entity (despawned on the wire).
    /// </summary>
    public const string EntityRule = EntityRuleV22;

    /// <summary>
    /// M3 (D115, DV24; the contract of the M3 contracts step, M3 P5 switches <see cref="IsEntity"/> to it): the largest
    /// <c>invisibility</c> of an entity, tg's <c>SEE_INVISIBLE_LIVING</c> (25), what every M3 viewer (a living mob) sees.
    /// Hidden pipes, cables and disposals under tiles (<c>undertile</c>: <c>INVISIBILITY_MAXIMUM</c>, 100), landmarks and
    /// mapping helpers (<c>INVISIBILITY_ABSTRACT</c>) are not entities. M10's per-viewer <c>see_invisible</c> replaces it.
    /// </summary>
    public const int MaxEntityInvisibility = Defines.SEE_INVISIBLE_LIVING;

    /// <summary>
    /// Version 22 (the ghosts, D151): the largest <c>invisibility</c> of an entity of the observer layer, tg's
    /// <c>SEE_INVISIBLE_OBSERVER</c> (60): a ghost (<c>INVISIBILITY_OBSERVER</c>, 60) is replicated to the sessions whose mob's
    /// <c>see_invisible</c> is at least its invisibility, in their private blocks.
    /// </summary>
    public const int MaxLayerInvisibility = Defines.SEE_INVISIBLE_OBSERVER;

    /// <summary>Version 22: an entity's layer, 0 for the shared body (invisibility up to <see cref="MaxEntityInvisibility"/>), 1 for the observer layer.</summary>
    public static byte LayerOf(atom_movable movable) => movable.invisibility > MaxEntityInvisibility ? (byte)1 : (byte)0;

    /// <summary>The entity predicate of protocol version 22 (the ghosts, D151): the observer layer joins the entities.</summary>
    public const string EntityRuleV22 = "a registered /atom/movable whose loc is a turf within the protocol's map range and whose invisibility is at most SEE_INVISIBLE_OBSERVER (60); one above SEE_INVISIBLE_LIVING (25) is an observer-layer entity, sent only to the sessions whose mob's see_invisible reaches it (D151)";

    /// <summary>The entity predicate of protocol version 3 (M3 P5 makes it <see cref="EntityRule"/>).</summary>
    public const string EntityRuleV3 = "a registered /atom/movable whose loc is a turf within the protocol's map range and whose invisibility is at most SEE_INVISIBLE_LIVING (25; D115, until M10)";

    /// <summary>The table, in <see cref="NetField"/> bit order.</summary>
    public static IReadOnlyList<FieldSource> All { get; } = new FieldSource[]
    {
        new(NetField.Id, "datum.dm_id (object-model.md section 3)", "created: the registry watermark; deleted: ObjectRegistry.Unregistered"),
        new(NetField.Type, "DmTypes.PathOf(the object's runtime type, or the type an IShownAsType names: DV376), interned in the run's append-only type table", "created (a DM object never changes type)"),
        new(NetField.X, "the x of the turf that is the movable's raw loc", "moved (every engine change of loc)"),
        new(NetField.Y, "the y of the turf that is the movable's raw loc", "moved"),
        new(NetField.Z, "the z of the turf that is the movable's raw loc", "moved"),
        new(NetField.Dir, "atom.dir (an engine property)", "appearance changed (Dir)"),
        new(NetField.Name, "atom.name, tg's visible name: /atom/proc/get_visible_name returns it for /mob and /obj, and tg's humans keep it equal to their get_visible_name (update_visible_name); never real_name", "appearance changed (Name; an engine property since M2)"),
        new(NetField.GlideSize, "atom_movable.glide_size (an engine property)", "appearance changed (Glide)"),
        new(NetField.LastMoveTick, "the replicator's own observation: the tick in which X, Y or Z last changed, or the entity appeared", "moved"),
        new(NetField.OwnMob, "per connection, never in an entity record: the own entity id of the Welcome, Snapshot and Delta headers, which the host gives", null),
        new(NetField.Pos, "the capsule mob's MotionState x and y (LocomotionSeam, M2 P4) in 1/256 tile; the tile centre once an entity has been a capsule and is not now", "polled every tick for every mob that is or was a capsule"),
        new(NetField.Yaw, "the high byte of the capsule's MotionState.Yaw (a buckled mob's dir as a yaw, DV126)", "polled with Pos"),
        new(NetField.Posture, "mob_living.body_position, lying_angle and stat != CONSCIOUS (PostureBits)", "appearance changed (tg's update_transform and update_appearance), polled for capsule mobs"),
        new(NetField.Appearance, "the atom's builtin appearance vars and its overlays and underlays (IDmAppearance), interned in the run's public appearance table", "appearance changed (every aspect but Dir, Glide and Name)"),
        new(NetField.Light, "the atom's light_range, light_power, light_color, light_on, light_angle, light_dir, light_height, the visual offset and light_system (tg's light vars, read raw), none while range or power is 0; or the brightest light it carries (an overlay light shown on it, a COMPLEX_LIGHT in its contents; DV61)", "light changed (tg's update_light() and an overlay light's show and hide as IAppearanceSink.LightChanged, on the atom and its loc), the sweep, a visibility hint, creation"),
        new(NetField.Flags, "atom_movable.throwing != null (tg's throwing datum is set: EntityFlags.Thrown); a dense, unanchored obj whose move_resist a mob's MOVE_FORCE_DEFAULT pushes (EntityFlags.Pushable, version 56), read raw", "moved (SSthrowing moves the item each tick), appearance changed (a closet's density follows its door), the sweep (an anchored var set raw), polled every tick while a flag but Pushable is set (a throw can end without a move)"),
        new(NetField.Flick, "BYOND flick(state, atom): the atom's appearance with the flicked icon state (or icon) in its place, interned in the public table when the entity is next read, and that tick", "flick (IAppearanceSink.Flick)"),
        new(NetField.Animation, "BYOND animate(): the atom's running chains (the steps' time, delay, easing, flags, loop and the pixel_x, pixel_y, pixel_z, pixel_w, alpha, color and transform they name, as tg gave them) and the vars' values before the call (AnimationRecorder); not tg's attack lunge (Flags) nor the lying turn (Posture)", "animate (IAppearanceSink.AnimateStep), polled when the chain ends (sent as none)"),
        new(NetField.VisHost, "tg's flick_overlay_view: an /atom/movable/flick_visual (or a gun's firing effect, M7x; a visual helper /atom/movable/visual, M8x; a unique vis overlay, M8y) in the vis_contents of a turf or an entity (the replicator's own index of the vis_contents writes): the host entity's dm_id, 0 for a turf; the visual stands at its host's turf", "appearance changed (VisContents) of the host, polled every tick while the visual lives"),
        new(NetField.Door, "M10 slice E: an airlock's req_access, req_one_access, unres_sides, emergency and whether a bump would open it now (the vars of door.dm and airlock.dm open's checks); none for any other entity", "read with the entity (a door's state change updates its icon, which hints it)"),
        new(NetField.Ride, "DV371: a level change tg makes after a wait, announced when the wait starts: the port's dm_ride_dz, dm_ride_end and dm_ride_ticks of a movable (LevelRides: an elevator's car and riders in linear_controller.move_after_delay, a ladder's climber over its do_after); none for any other", "polled every tick while LevelRides holds the movable"),
        new(NetField.Flight, "DV416: the port's dm_flight (FlightSeam): a thrown thing's ballistic arc, a fall, a slide the air pushes, the point it rests at; none off a turf or once tg moved it itself", "polled while FlightSeam announces it (a change, every tick while it moves)"),
        new(NetField.Tram, "DV403: a tram's trip, announced when it starts: the port's dm_tram_ride of the tram's platform (TramRides: dispatch_transport, a step off the schedule, the stops); none for any other movable", "polled every tick while TramRides holds the platform"),
        new(NetField.Shot, "D177: a shot's 3D line, the port's vars of tg's /obj/projectile (dm_origin_x, dm_origin_y, dm_origin_h, dm_slope and tg's angle) and of the effects a shot leaves (/obj/effect: its tracer, muzzle and impact); none for any other entity", "read with the entity (a projectile is read every tick it moves; the line changes on a turn in flight, set_angle)"),
    };

    /// <summary>The union of <see cref="All"/>'s fields: <see cref="NetField.All"/> (version 10).</summary>
    public static NetField Union
    {
        get
        {
            NetField union = NetField.None;
            foreach (FieldSource source in All) union |= source.Field;
            return union;
        }
    }

    /// <summary>
    /// The changeable fields no engine hint covers: only the sweep finds their changes. None in M1's sense (<c>name</c> is
    /// hinted since M2); an in-place edit of an <c>overlays</c> list raises no hint yet (see replication.md 5.3), which only
    /// the sweep finds, but that is not a field of its own.
    /// </summary>
    public static NetField SweepOnly
    {
        get
        {
            NetField fields = NetField.None;
            foreach (FieldSource source in All)
                if (source.Hint == null && (source.Field & NetField.Changeable) != 0) fields |= source.Field;
            return fields;
        }
    }

    // ---- the readers (the tests' oracle reads the world its own way) ----

    /// <summary>
    /// Whether <paramref name="obj"/> is an entity (<see cref="EntityRule"/>), and its turf. Reads raw: called outside the
    /// world (no ambient <c>World</c>), so the engine getters apply no pending DM contents edit and nothing runs.
    /// </summary>
    // The owner (2026-10-15): a mapping helper or a spawner is never seen in tg (it deletes itself in its Initialize or LateInitialize,
    // after it has done its work or spawned its loot). One whose work is not translated yet (mail sorting, request consoles, the
    // costume and trash spawners) stays on the map here, so it is not an entity either.
    private static bool IsMapTimeOnly(atom_movable m) => m is obj_effect_mapping_helpers or obj_effect_spawner;

    /// <param name="mapMarkers">A map editor's world (<see cref="ReplicatorOptions.MapMarkers"/>): a spawner or a mapping helper that stands on the map is an entity too.</param>
    public static bool IsEntity(object obj, out atom_movable movable, out turf on, bool mapMarkers = false)
    {
        // DEVIATION(DV24): tg replicates what the viewer's see_invisible shows. The shared body carries what every viewer (a
        // living mob, 25) sees; version 22 adds the observer layer up to a ghost's sight (60), sent per viewer (LayerOf).
        if (obj is atom_movable m && m.loc is turf t && m.invisibility <= MaxLayerInvisibility && (mapMarkers || !IsMapTimeOnly(m))
            && t.x >= 1 && t.x <= Limits.MaxMapSize && t.y >= 1 && t.y <= Limits.MaxMapSize && t.z >= 1 && t.z <= Limits.MaxMapLevels)
        {
            movable = m;
            on = t;
            return true;
        }
        movable = null;
        on = null;
        return false;
    }

    /// <summary>
    /// Version 10: whether <paramref name="obj"/> is a short-lived visual (tg's <c>/atom/movable/flick_visual</c>, made by
    /// <c>flick_overlay_view</c>, or a gun's firing effect, M7x) drawn on <paramref name="host"/>: a registered visual in nullspace whose invisibility is at most
    /// <see cref="MaxEntityInvisibility"/>, held by the <c>vis_contents</c> of <paramref name="host"/>, which is a turf in the map
    /// range or an entity. Its turf is the host's (<paramref name="on"/>), and <paramref name="hostId"/> the host entity's id, 0
    /// for a turf. Reads raw, as <see cref="IsEntity"/>.
    /// </summary>
    public static bool IsVisual(object obj, atom host, out atom_movable visual, out turf on, out long hostId)
        => IsVisualOf(obj, host, out visual, out on, out hostId);

    /// <summary>
    /// A visual that sits inside its host and joins its <c>vis_contents</c>: a gun's firing effect (D176), and the smart SMG's lock reticle on the
    /// mob its bullets home on (DV96).
    /// </summary>
    public static bool IsInsideVisual(atom_movable v) => v is obj_effect_temp_visual_dir_setting_firing_effect or obj_effect_temp_visual_smartgun_target;

    private static bool IsVisualOf(object obj, atom host, out atom_movable visual, out turf on, out long hostId)
    {
        visual = null;
        on = null;
        hostId = 0;
        // M8x slice C (D210): tg's visual helpers (/atom/movable/visual: the cryo cell's occupant, cryo.dm:14) live in their holder's vis_contents for
        // as long as it does; they are visuals too, which the clients draw as part of the holder's picture.
        // A gun's firing effect (tg's temp_visual/dir_setting/firing_effect) is such a visual too, but it sits inside its host (the shooter, whose
        // vis_contents it joins in its Initialize), not in nullspace: the clients draw the muzzle flash from it (D176).
        if (obj is not atom_movable v || v.invisibility > MaxEntityInvisibility || host == null || host.gc_destroyed != null
            || !(IsHeldVisual(v) && v.loc == null || IsInsideVisual(v) && ReferenceEquals(v.loc, host)
                || IsInnerVisual(v) && Within(v, host)))
            return false;
        if (!Holds(host.vis_contents, v))
            return false;
        if (host is turf t)
        {
            if (!(t.x >= 1 && t.x <= Limits.MaxMapSize && t.y >= 1 && t.y <= Limits.MaxMapSize && t.z >= 1 && t.z <= Limits.MaxMapLevels))
                return false;
            on = t;
        }
        else if (IsEntity(host, out atom_movable hostMovable, out turf hostTurf))
        {
            on = hostTurf;
            hostId = hostMovable.dm_id;
        }
        else
        {
            return false;
        }
        visual = v;
        return true;
    }

    /// <summary>
    /// A visual of nullspace that a holder's <c>vis_contents</c> keeps: tg's flick visual, its visual helper (<c>/atom/movable/visual</c>, M8x), or a
    /// unique vis overlay (<c>SSvis_overlays.add_vis_overlay(..., unique = TRUE)</c>: the stasis bed's mattress, whose alpha tg animates; M8y). A shared
    /// vis overlay (a holosign's picture, one object in many holders) stays flattened into each holder's appearance.
    /// </summary>
    // M10x: tg's eyelid effects (the blink, _eyes.dm:72) sit in the eye organ, inside the head inside the human whose vis_contents hold them.
    private static bool Within(atom_movable v, atom host)
    {
        for (atom at = v.loc; at != null; at = (at as atom_movable)?.loc)
            if (ReferenceEquals(at, host)) return true;
        return false;
    }

    /// <summary>
    /// A visual drawn in its holder's picture that is not in nullspace (the guns' firing effects are drawn on their own): tg's eyelid effect,
    /// and the big manipulator's arm (E16: the claw in the manipulator's vis_contents, which tg turns with animate() on its transform).
    /// </summary>
    public static bool IsInnerVisual(atom_movable v) => v is obj_effect_abstract_eyelid_effect or obj_effect_big_manipulator_arm;

    /// <remarks>An aquarium's visual of a fish or a prop (tg's <c>/obj/effect/aquarium</c>, made in nullspace by the content's
    /// <c>aquarium_content</c> component and kept in the aquarium's <c>vis_contents</c>) is one too: the fish's swimming is its own
    /// <c>animate()</c> chain.</remarks>
    public static bool IsHeldVisual(atom_movable v)
        => v is atom_movable_flick_visual or atom_movable_visual or obj_effect_aquarium || v is obj_effect_overlay_vis { cache_expiration: -1 };

    // Whether the list holds the item (raw: no DM code runs).
    private static bool Holds(DmList list, object item)
    {
        if (list == null) return false;
        int count = list.Count;
        for (int i = 1; i <= count; i++)
            if (ReferenceEquals(list[(float)i], item)) return true;
        return false;
    }

    /// <summary>
    /// The light source of an entity as the wire has it (docs/ai/specs/light-grid.md section 1). Its own light (<see cref="OwnLight"/>),
    /// unless it carries a brighter one: the overlay lights tg shows on it (an <c>overlay_lighting</c> component whose
    /// <c>current_holder</c> it is, in tg's <c>affected_dynamic_lights</c>: a flashlight in a hand, on a belt or on the floor) and
    /// tg's top atom of a <c>COMPLEX_LIGHT</c> in its contents (<c>update_light</c>: a desk lamp in a hand). The wire has one light
    /// per entity: the one with the largest range times power wins (DV61). An overlay light's directional cast is its offset
    /// (<see cref="OverlayLight"/>).
    /// </summary>
    public static LightState WireLight(atom_movable a)
    {
        LightState own = OwnLight(a);
        if (a.light_system != COMPLEX_LIGHT && Shown(a) is datum_component_overlay_lighting self)
            own = OverlayLight(a, self, a);
        LightState best = Live(own) ? own : LightState.None;
        DmList shown = a.affected_dynamic_lights;
        if (shown != null)
        {
            int count = shown.Count;
            for (int i = 1; i <= count; i++)
            {
                if (shown[(float)i] is datum_component_overlay_lighting light && light.parent is atom_movable source && source != a)
                    best = Brighter(best, OverlayLight(source, light, a));
            }
        }
        DmList contents = a.contents;
        if (contents != null)
        {
            int count = contents.Count;
            for (int i = 1; i <= count; i++)
            {
                if (contents[(float)i] is atom_movable held && held.light_system == COMPLEX_LIGHT)
                    best = Brighter(best, OwnLight(held) with { OffsetX = 0f, OffsetY = 0f });
            }
        }
        return best.IsNone ? own : best;
    }

    /// <summary>
    /// An atom's own light vars read raw, <see cref="LightState.None"/> while <c>light_range</c> or <c>light_power</c> is 0 or
    /// <c>light_system</c> is not one of tg's four; values are clamped into the codec's ranges. <c>On</c> is <c>light_on</c>.
    /// The visual offset is <c>calculate_light_offset</c> without the matrix and irregular-icon terms (the client has no icon
    /// sizes of tg's): the negated pixel shift in tiles, unless <c>LIGHT_IGNORE_OFFSET</c>, plus the atom's <c>get_light_offset</c>.
    /// </summary>
    public static LightState OwnLight(atom a)
    {
        float range = a.light_range;
        float power = a.light_power;
        if (!(range > 0f) || !float.IsFinite(power) || power == 0f) return LightState.None;
        float system = a.light_system;
        if (!(system >= COMPLEX_LIGHT && system <= OVERLAY_LIGHT_BEAM)) return LightState.None;
        float angle = a.light_angle;
        float height = a.light_height;
        float ox = 0f, oy = 0f;
        if (!Dm.truthy(Dm.bitand(a.light_flags, LIGHT_IGNORE_OFFSET)))
        {
            ox = -a.pixel_x / 32f;
            oy = -a.pixel_y / 32f;
        }
        // tg's atom opinion (a wall lamp's half tile forward, light.dm:166), so the corners of its wall stay inside its cone
        var opinion = (DmList)a.get_light_offset();
        ox += Dm.num(opinion[1]);
        oy += Dm.num(opinion[2]);
        return Light(range, power, a.light_color, Dm.truthy(a.light_on), angle, a.light_dir, height, ox, oy, system);
    }

    /// <summary>
    /// The light an overlay light component shows on <paramref name="holder"/>: its parent's vars, tg's range (the component's
    /// clamped <c>range</c>), all around, and for a directional light the cast (<c>cast_directional_light</c>: the light's
    /// centre <c>directional_offset</c> pixels from the holder's, less tg's <c>-(range - 1) * 32</c> centring of the mask).
    /// </summary>
    public static LightState OverlayLight(atom_movable source, datum_component_overlay_lighting light, atom_movable holder)
    {
        LightState own = OwnLight(source);
        if (own.IsNone) return own;
        float range = light.range ?? source.light_range;
        float ox = 0f, oy = 0f;
        if (Dm.truthy(light.directional) && light.directional_offset_x is float dx && light.directional_offset_y is float dy)
        {
            float centring = (range - 1f) * 32f;
            ox = -(dx + centring) / 32f;
            oy = -(dy + centring) / 32f;
        }
        bool on = Dm.truthy(light.currently_displaying) && light.current_holder == holder;
        return Light(range, source.light_power, source.light_color, on, 360f, 0f, LIGHTING_HEIGHT, ox, oy, source.light_system);
    }

    // The overlay light component of an atom whose light it shows on itself (tg's GET_PARENT is the atom and the holder too).
    private static datum_component_overlay_lighting Shown(atom_movable a)
    {
        DmList shown = a.affected_dynamic_lights;
        if (shown == null) return null;
        int count = shown.Count;
        for (int i = 1; i <= count; i++)
            if (shown[(float)i] is datum_component_overlay_lighting light && light.parent == a)
                return light;
        return null;
    }

    private static bool Live(in LightState l) => !l.IsNone && l.On && l.Power != 0f;

    private static LightState Brighter(LightState best, LightState other)
    {
        if (!Live(other)) return best;
        if (!Live(best)) return other;
        return other.Range * Math.Abs(other.Power) > best.Range * Math.Abs(best.Power) ? other : best;
    }

    private static LightState Light(float range, float power, string color, bool on, float angle, float dir, float height, float ox, float oy, float system) =>
        new(
            MathF.Min(range, Limits.MaxLightRange),
            Math.Clamp(power, -Limits.MaxLightPower, Limits.MaxLightPower),
            CellState.ParseColor(color),
            on,
            float.IsFinite(angle) ? Math.Clamp(angle, 0f, 360f) : 360f,
            WireDir(dir),
            float.IsFinite(height) ? Math.Clamp(height, -Limits.MaxLightHeight, Limits.MaxLightHeight) : 1f,
            float.IsFinite(ox) ? Math.Clamp(ox, -Limits.MaxLightOffset, Limits.MaxLightOffset) : 0f,
            float.IsFinite(oy) ? Math.Clamp(oy, -Limits.MaxLightOffset, Limits.MaxLightOffset) : 0f,
            (byte)system);

    /// <summary><c>screen_loc</c> within <see cref="Limits.MaxScreenLocBytes"/> UTF-8 bytes (cut at a character boundary); null stays null.</summary>
    public static string WireScreenLoc(string loc)
    {
        if (loc == null || loc.Length * 3 <= Limits.MaxScreenLocBytes) return loc;
        if (Encoding.UTF8.GetByteCount(loc) <= Limits.MaxScreenLocBytes) return loc;
        int bytes = 0, i = 0;
        while (i < loc.Length)
        {
            char c = loc[i];
            int chars = 1, width;
            if (char.IsHighSurrogate(c) && i + 1 < loc.Length && char.IsLowSurrogate(loc[i + 1])) { width = 4; chars = 2; }
            else width = c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
            if (bytes + width > Limits.MaxScreenLocBytes) break;
            bytes += width;
            i += chars;
        }
        return loc.Substring(0, i);
    }

    /// <summary>
    /// The wire entity flags (version 4): <see cref="EntityFlags.Thrown"/> while tg's <c>throwing</c> datum is set; version 56:
    /// <see cref="EntityFlags.Pushable"/> for a dense obj a walking mob's PushAM moves (read raw: nothing runs).
    /// </summary>
    public static EntityFlags WireFlags(atom_movable movable)
        => (movable.throwing != null ? EntityFlags.Thrown : EntityFlags.None) | (Pushable(movable) ? EntityFlags.Pushable : EntityFlags.None);

    // living.dm PushAM's own refusals of a mob of the default move_force: an anchored thing, or one whose move_resist outweighs the force (a
    // component's COMSIG_MOVABLE_BUMP_PUSHED veto is not read: the client's guess is corrected then). Mobs are capsules: MobBump, not this.
    private static bool Pushable(atom_movable movable)
        => movable is obj && Dm.truthy(movable.density) && !Dm.truthy(movable.anchored)
            && Dm.num(movable.move_resist) * MOVE_FORCE_PUSH_RATIO <= MOVE_FORCE_DEFAULT;

    /// <summary>
    /// Version 81 (DV416): the movable's free motion (FlightSeam's dm_flight): none off a turf, or on another level than the segment's (a
    /// raw loc change the seam never saw), and then forgotten.
    /// </summary>
    public static Anomaly.Core.Motion.FlightArc WireFlight(atom_movable movable)
    {
        Anomaly.Core.Motion.FlightArc arc = movable.dm_flight;
        if (arc.IsNone)
            return arc;
        if (movable.loc is not turf t || (int)Dm.num(t.z) != arc.Z)
        {
            movable.dm_flight = Anomaly.Core.Motion.FlightArc.None;
            return Anomaly.Core.Motion.FlightArc.None;
        }
        return arc;
    }

    /// <summary>Version 77 (DV371): the movable's announced level ride (LevelRides), none when it has none.</summary>
    public static RideState WireRide(atom_movable movable)
        => movable.dm_ride_dz == 0 ? RideState.None : new RideState((sbyte)Math.Sign(movable.dm_ride_dz), movable.dm_ride_end, (ushort)Math.Clamp(movable.dm_ride_ticks, 1, ushort.MaxValue));

    /// <summary>
    /// Version 34 (D177): a shot's 3D line for the wire, from the port's vars of a projectile (tg's <c>angle</c> its heading) or of an effect a
    /// shot left; null for any other entity. Heights in 1/16 pixel, the slope in 1/4096 (clamped to the wire's range).
    /// </summary>
    /// <summary>
    /// Version 43 (M10 slice E): an airlock's access and whether a bump or a hand would open it now for a mob it lets in, read raw (door.dm
    /// Bumped and try_to_activate_door, airlock.dm bumpopen, open and try_to_force_door_open: not operating, emagged, welded, bolted or
    /// sealed; closed; ID scan on; powered; the open wire whole; openable by hand; no cycling pump or a locked elevator door); null for
    /// any other entity.
    /// </summary>
    public static DoorState WireDoor(atom_movable movable)
    {
        // DEVIATION(DV179): the requirements reach every client (a BYOND client never sees them), for the guess of the own opening
        if (movable is not obj_machinery_door_airlock door)
            return null;
        DmList cut = door.wires?.cut_wires;
        bool idScan = !(cut != null && cut.Find(WIRE_IDSCAN) > 0) && door.aiDisabledIdScanner == 0;
        bool mainOut = Dm.truthy(door.main_power_timer) || Dm.num(door.main_power_time) > 0;
        bool backupOut = Dm.truthy(door.backup_power_timer) || Dm.num(door.backup_power_time) > 0;
        bool powered = !(mainOut && backupOut) && ((int)door.machine_stat & NOPOWER) == 0;
        bool openable = !Dm.truthy(door.operating) && !Dm.truthy(door.welded) && door.locked == 0 && door.seal == null && Dm.truthy(door.density)
            && ((int)Dm.num(door.obj_flags) & EMAGGED) == 0 && door.can_open_with_hands != 0 && door.elevator_mode == 0 && door.cycle_pump == null
            && idScan && powered && !(cut != null && cut.Find(WIRE_OPEN) > 0);
        DoorFlags flags = (openable ? DoorFlags.Openable : DoorFlags.None) | (Dm.truthy(door.emergency) ? DoorFlags.Emergency : DoorFlags.None);
        return new DoorState(flags, (byte)((int)door.unres_sides & 15), AccessList(door.req_access), AccessList(door.req_one_access));
    }

    private static string[] AccessList(DmList list)
    {
        if (list == null || list.Count == 0)
            return Array.Empty<string>();
        var result = new List<string>(list.Count);
        for (int i = 1; i <= list.Count && result.Count < Limits.MaxDoorAccessEntries; i++)
            if (list[(float)i] is string a && a.Length > 0 && Encoding.UTF8.GetByteCount(a) <= Limits.MaxAccessBytes)
                result.Add(a);
        return result.ToArray();
    }

    public static ShotState? WireShot(atom_movable movable)
    {
        float x, y, h, slope, angle;
        switch (movable)
        {
            case atom_movable_flick_visual melee when melee.dm_melee:
                (x, y, h, slope, angle) = (melee.dm_origin_x, melee.dm_origin_y, melee.dm_origin_h, melee.dm_slope, melee.dm_angle);
                break;
            case obj_projectile p when p.dm_shot:
                (x, y, h, slope, angle) = (p.dm_origin_x, p.dm_origin_y, p.dm_origin_h, p.dm_slope, Dm.num(p.angle));
                break;
            case obj_effect fx when fx.dm_shot:
                (x, y, h, slope, angle) = (fx.dm_origin_x, fx.dm_origin_y, fx.dm_origin_h, fx.dm_slope, fx.dm_shot_angle);
                break;
            default:
                return null;
        }
        double turns = ((angle % 360.0) + 360.0) % 360.0 / 360.0;
        return new ShotState(
            (int)Math.Clamp(Math.Round(x), 0, Limits.MaxShotCoordinate),
            (int)Math.Clamp(Math.Round(y), 0, Limits.MaxShotCoordinate),
            (short)Math.Clamp(Math.Round(h * 16.0), short.MinValue, short.MaxValue),
            (short)Math.Clamp(Math.Round(slope * 4096.0), short.MinValue, short.MaxValue),
            (ushort)((int)Math.Round(turns * 65536.0) & 0xFFFF));
    }

    /// <summary>
    /// The wire yaw byte of a capsule mob: the high byte of its capsule's yaw (the player's look), but a buckled mob faces the way tg turned
    /// it (DV126: <c>buckle_mob</c>'s <c>setDir</c>, the chair's <c>handle_rotation</c>; a buckled mob's look does not turn it).
    /// </summary>
    // DEVIATION(DV126): a buckled mob's wire yaw is his dir's
    public static byte WireYaw(atom_movable movable, ushort capsuleYaw)
        => movable is mob_living { buckled: not null } ? ShotGeometry.YawOfDir(WireDir(movable.dir)) : (byte)(capsuleYaw >> 8);

    /// <summary>The wire posture byte of a living mob: lying, the quarter turns of its lying angle, whether it is limp, and the side it was turned to (<see cref="PostureBits"/>).</summary>
    public static byte WirePosture(mob_living mob)
    {
        bool lying = mob.body_position == Defines.LYING_DOWN;
        float angle = mob.lying_angle;
        int quarter = float.IsFinite(angle) ? (int)(angle / 90f) : 0;
        return PostureBits.Encode(lying, lying ? quarter * 90 : 0, limp: Dm.num(mob.stat) != 0, // stat 0 is conscious (tg has no define for it);
            incorporeal: mob.incorporeal_move != 0, buckled: mob.buckled is not null and not mob_living,
            turnedFaceUp: mob.dm_turned_face_up); // version 99 (DV686): the side the radial's Turn over put it on
    }

    /// <summary>
    /// BYOND <c>dir</c> as the wire byte: one of the eight directions (NORTH 1, SOUTH 2, EAST 4, WEST 8 and the four
    /// diagonals 5, 6, 9, 10) or 0; anything else (UP, DOWN, a fraction, an impossible sum) is 0, the codec's "no direction".
    /// </summary>
    public static byte WireDir(float dir) => dir switch
    {
        1 or 2 or 4 or 5 or 6 or 8 or 9 or 10 => (byte)dir,
        _ => 0,
    };

    /// <summary>
    /// <c>glide_size</c> as the wire float: within 0 to <see cref="Limits.MaxGlideSize"/> (the codec's range; tg's
    /// <c>DELAY_TO_GLIDE_SIZE</c> gives 1 to 32); NaN, negatives and -0 become 0, BYOND's default glide.
    /// </summary>
    public static float WireGlide(float glide) => glide > 0f ? MathF.Min(glide, Limits.MaxGlideSize) : 0f;

    /// <summary>
    /// <c>name</c> within <see cref="Limits.MaxEntityNameBytes"/> UTF-8 bytes: cut at a character boundary (never between
    /// the halves of a surrogate pair). The same string when it fits.
    /// </summary>
    public static string WireName(string name)
    {
        if (name == null || name.Length * 3 <= Limits.MaxEntityNameBytes) return name;
        if (Encoding.UTF8.GetByteCount(name) <= Limits.MaxEntityNameBytes) return name;
        int bytes = 0;
        int i = 0;
        while (i < name.Length)
        {
            int width;
            int chars = 1;
            char c = name[i];
            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                width = 4;
                chars = 2;
            }
            else
            {
                width = c < 0x80 ? 1 : c < 0x800 ? 2 : 3; // a lone surrogate encodes as U+FFFD, 3 bytes
            }
            if (bytes + width > Limits.MaxEntityNameBytes) break;
            bytes += width;
            i += chars;
        }
        return name.Substring(0, i);
    }
}
