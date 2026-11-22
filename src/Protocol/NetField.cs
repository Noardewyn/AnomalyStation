namespace Anomaly.Protocol;

// Owner: M1 P2 (contract: M1 contracts step); M2 P5 for version 2 (contract: M2 contracts step); M3 P5 for version 3 (contract: M3 contracts step); M4a P6 for version 4 (contract: M4a contracts step). The field set is D40's whitelist: the replicator maps every value of this
// enum to the world and nothing else leaves the server; its test asserts that the wire field set equals this enum.

/// <summary>
/// The replicated fields of an entity (scope 2d "M1 minimum"). An entity is a movable that stands directly on a turf
/// (container contents are not entities until M4); turfs travel in <see cref="MapGrid"/>. The set on the wire is exactly
/// this enum (D40: the server sends no hidden data): <c>real_name</c>, <c>key</c>, health and every other tg var stay on
/// the server until a milestone adds a field here, with a protocol version bump.
/// <para>
/// As a mask (<see cref="EntityChange.Mask"/>) only the bits of <see cref="Changeable"/> may be set; values follow the
/// mask in bit order.
/// </para>
/// </summary>
[Flags]
public enum NetField : uint
{
    None = 0,

    /// <summary>The entity's identity: the object's world id (<c>dm_id</c>, object-model.md section 3), unique and never reused in a world. The key of every record; never in a change mask.</summary>
    Id = 1 << 0,

    /// <summary>An index into the connection's type table (<see cref="Snapshot.Types"/>, <see cref="Delta.NewTypes"/>): the object's DM type path. Sent once, at spawn (a DM object never changes type); never in a change mask.</summary>
    Type = 1 << 1,

    /// <summary>BYOND <c>x</c> of the movable: its turf's x, 1 to <c>world.maxx</c>.</summary>
    X = 1 << 2,

    /// <summary>BYOND <c>y</c>: its turf's y.</summary>
    Y = 1 << 3,

    /// <summary>BYOND <c>z</c>: its turf's z level.</summary>
    Z = 1 << 4,

    /// <summary>BYOND <c>dir</c>: one of the eight BYOND directions (NORTH 1, SOUTH 2, EAST 4, WEST 8 and their sums).</summary>
    Dir = 1 << 5,

    /// <summary>tg's <c>name</c>, the visible name (behind a mask tg already shows "Unknown"); never <c>real_name</c> (D40, D56).</summary>
    Name = 1 << 6,

    /// <summary>BYOND <c>glide_size</c>: pixels the movable glides per tick (32 pixels are one tile); 0 means BYOND's default glide.</summary>
    GlideSize = 1 << 7,

    /// <summary>The server tick in which X, Y or Z last changed (the replicator's own observation, not a tg var); it changes whenever they do.</summary>
    LastMoveTick = 1 << 8,

    /// <summary>
    /// "This entity is the connection's own mob": per connection, so it is never part of an entity record. It travels as
    /// the own entity id in the per-connection headers (<see cref="Welcome.OwnEntity"/>, <see cref="Snapshot.OwnEntity"/>,
    /// <see cref="Delta.OwnEntity"/>), which change when the client's mob changes.
    /// </summary>
    OwnMob = 1 << 9,

    // ---- protocol version 2 (M2 contracts, scope 2h; the change mask on the wire becomes a varuint, M2 P5) ----

    /// <summary>
    /// Version 2: a capsule mob's continuous position, x and y in 1/256 tile (Anomaly.Core.Motion's units; <see cref="EntityState.PosX"/>,
    /// <see cref="EntityState.PosY"/>), sent every tick it moves. Its tile is <see cref="X"/>, <see cref="Y"/> (the invariant
    /// of the locomotion bridge). In version 2 <see cref="LastMoveTick"/> travels relative to the Delta's tick.
    /// </summary>
    Pos = 1 << 10,

    /// <summary>Version 2: where a capsule mob looks, one byte (256 per turn): the billboard frame by viewer angle (D104).</summary>
    Yaw = 1 << 11,

    /// <summary>Version 2: tg's body position and lying angle, one byte (<see cref="PostureBits"/>); the client also needs it for collision (lying capsules do not collide, D51).</summary>
    Posture = 1 << 12,

    /// <summary>Version 2: the entity's appearance, a reference into the public appearance table (<see cref="AppearanceEntry"/>); what others see of injuries, posture and clothing (D7, D40: no numbers).</summary>
    Appearance = 1 << 13,

    // ---- protocol version 3 (M3 contracts, scope 2h and 2j; the encoding is M3 P5's) ----

    /// <summary>
    /// Version 3: the entity's light source (<see cref="LightState"/>: tg's <c>light_range</c>, <c>light_power</c>,
    /// <c>light_color</c>, <c>light_on</c>, <c>light_angle</c>, <c>light_dir</c>, <c>light_height</c>, the visual offset and
    /// <c>light_system</c>), present while the atom has a light (docs/ai/specs/light-grid.md section 1). D24: the server sends the
    /// state of the sources, the client computes the light.
    /// </summary>
    Light = 1 << 14,

    // ---- protocol version 4 (M4a contracts, MessagesV4.cs; the encoding is M4a P6's, protocol.md section 14.4) ----

    /// <summary>
    /// Version 4: the entity's flags byte (<see cref="EntityFlags"/>): <see cref="EntityFlags.Thrown"/> while tg's
    /// <c>throwing</c> is set (D127: the client draws the arc and the spin of a thrown item; the tiles are tg's).
    /// </summary>
    Flags = 1 << 15,

    // ---- protocol version 8 (D142: a mechanic ports with its visuals) ----

    /// <summary>
    /// Version 8: the entity's last BYOND <c>flick(state, atom)</c>: the appearance it plays (the atom's own with the flicked
    /// icon state, or icon, in its place) and the tick it started in (<see cref="EntityState.FlickAppearance"/>,
    /// <see cref="EntityState.FlickTick"/>). The client plays the flicked state's frames once over the entity's appearance, as
    /// BYOND does (a vent's <c>vent_out-shutdown</c> when it loses its pipe).
    /// </summary>
    Flick = 1 << 16,

    // ---- protocol version 9 (M4v slice 1, D143: BYOND's animate() reaches the clients) ----

    /// <summary>
    /// Version 9: the atom's running BYOND <c>animate()</c> chains (<see cref="EntityState.Animation"/>, null for none): each track's
    /// start tick, loop, start values and steps (time, delay, easing, flags and the pixel offsets, alpha, colour and transform they
    /// name). The client plays them over the entity's appearance, which holds their end values (docs/ai/specs/protocol.md section 18).
    /// </summary>
    Animation = 1 << 17,

    // ---- protocol version 10 (M4v slice 3, D143: tg's short-lived overlay images reach the clients) ----

    /// <summary>
    /// Version 10: the entity is a short-lived visual drawn on another atom (tg's <c>flick_overlay_view</c>: an
    /// <c>/atom/movable/flick_visual</c> in that atom's <c>vis_contents</c>, deleted after its duration): the id of the entity it
    /// is drawn on, or 0 for the turf at its own X, Y and Z (<see cref="EntityState.VisHost"/>). The client draws it over that
    /// atom with its own appearance (pixel offsets, layer, transform), flick and animation (docs/ai/specs/protocol.md section 19).
    /// </summary>
    VisHost = 1 << 18,

    // ---- protocol version 34 (M7x slice A, D177, MessagesV34.cs) ----

    /// <summary>
    /// Version 34: a shot's 3D line (<see cref="ShotState"/>): its start in tile pixels, its height there, its slope and its heading, sent for
    /// tg's projectiles (<c>/obj/projectile</c>), their tracers, muzzles and impacts (<c>/obj/effect/projectile</c>) and the impact effects a
    /// shot leaves (<c>/obj/effect/temp_visual/impact_effect</c>). The client draws them at the line's height (DV78).
    /// </summary>
    Shot = 1 << 19,

    /// <summary>Version 43: an airlock's access and state (<see cref="DoorState"/>), for the client's guess of its opening (M10 slice E); none for any other entity.</summary>
    Door = 1 << 20,

    /// <summary>Version 77 (DV371): a level change announced when its wait starts (<see cref="RideState"/>): an elevator's ride, a ladder's climb.</summary>
    Ride = 1 << 21,

    /// <summary>Version 80 (DV403): a tram's trip announced when it starts (<see cref="Anomaly.Core.Motion.TramRide"/>), on the tram's platform.</summary>
    Tram = 1 << 22,

    /// <summary>Version 81 (DV416): a movable's free motion (<see cref="Anomaly.Core.Motion.FlightArc"/>): a throw's arc, a fall, a slide, a rest point.</summary>
    Flight = 1 << 23,

    /// <summary>The bits a change mask may hold (version 81): every field but <see cref="Id"/>, <see cref="Type"/> and <see cref="OwnMob"/>.</summary>
    Changeable = X | Y | Z | Dir | Name | GlideSize | LastMoveTick | Pos | Yaw | Posture | Appearance | Light | Flags | Flick | Animation | VisHost | Shot | Door | Ride | Tram | Flight,

    /// <summary>Every field (version 34).</summary>
    All = Id | Type | Changeable | OwnMob,
}
