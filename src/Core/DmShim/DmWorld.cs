using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: contracts (complete for the vars of docs/ai/translation-rules.md 10.8; W3.0 added map_cpu, the map grid hook
// behind maxx/maxy/maxz and the /world vars of code/world.dm: turf, area, mob, name, view, params, system_type,
// sleep_offline).
public static partial class Dm
{
    /// <summary>
    /// DM <c>world</c>, written bare (<c>world.time</c>, <c>Dm.each&lt;mob&gt;(world)</c>, <c>world.log</c>): the facade
    /// over the ambient <see cref="World"/> (rules 10.8). One shared, stateless instance; every member reads
    /// <see cref="World.Current"/>.
    /// </summary>
    public static DmWorld world => DmWorld.Instance;
}

/// <summary>
/// The BYOND <c>/world</c> object as translated code sees it (rules 10.8). Its class is not named <c>world</c>:
/// a type named <c>world</c> in <c>Anomaly.Core.DmShim</c> or <c>Anomaly.Core.Tg</c> would make the bare <c>world</c> of
/// translated code ambiguous (CS0229) or resolve it to the type instead of <see cref="Dm.world"/>.
/// </summary>
[DmPath("/world")]
public sealed class DmWorld
{
    internal static readonly DmWorld Instance = new();

    private DmWorld()
    {
    }

    /// <summary><c>world.time</c>: deciseconds since the world started (<c>float</c>), <see cref="World.Time"/>.</summary>
    public float time => World.Current.Time;

    /// <summary><c>world.tick_lag</c>: 0.5 ds, a constant in the port (DV6; tg's <c>change_fps</c> is not supported).</summary>
    public float tick_lag => World.TickLag;

    /// <summary><c>world.fps</c>: 20, a constant in the port (DV6).</summary>
    public float fps => World.Fps;

    /// <summary><c>world.tick_usage</c>: the virtual tick usage of the current tick (DV3, DV33: work charged with <see cref="DmWorldState.ChargeTick"/>, 0 when none).</summary>
    public float tick_usage => DmWorldState.Current.TickUsage;

    /// <summary><c>world.cpu</c>: always 0 (DV3).</summary>
    public float cpu => 0f;

    /// <summary>
    /// <c>world.realtime</c>: deciseconds since 00:00:00 GMT, January 1, 2000, advancing with <c>world.time</c>
    /// from <see cref="DmWorldState.RealtimeStart"/> (no wall clock, DV3), in whole deciseconds as BYOND's (a 0.5 ds
    /// tick does not show: tg's client-time timers rely on a 1 ds resolution, controllers/subsystem/timer.dm:621). A DM
    /// number: like BYOND's, it is a 32-bit float and loses precision at this magnitude.
    /// </summary>
    public float realtime => (float)Math.Floor(DmWorldState.Current.RealtimeStart + World.Current.Time);

    /// <summary>
    /// <c>world.timeofday</c>: deciseconds since midnight, derived from <see cref="realtime"/>'s start plus
    /// <c>world.time</c> (DV3), in whole deciseconds as BYOND's ("REALTIMEOFDAY has a resolution of 1 decisecond",
    /// controllers/subsystem/timer.dm:621).
    /// </summary>
    public float timeofday
    {
        get
        {
            double t = Math.Floor(DmWorldState.Current.RealtimeStart + World.Current.Time) % 864000.0;
            return (float)(t < 0 ? t + 864000.0 : t);
        }
    }

    /// <summary>
    /// <c>world.timezone</c>: the host's offset from UTC in hours. The world's local time is UTC (DV3), so 0.
    /// </summary>
    public float timezone => 0f;

    /// <summary>
    /// <c>world.map_cpu</c>: the share of the tick BYOND spent on map updates (tg's
    /// <c>MAPTICK_LAST_INTERNAL_TICK_USAGE</c>, read by <c>TICK_LIMIT_RUNNING</c>, <c>__DEFINES/_tick.dm:3-7</c>): always
    /// 0, tick usage is virtual (DV3).
    /// </summary>
    public float map_cpu => 0f;

    /// <summary>
    /// <c>world.maxx</c>: map width in turfs (<see cref="DmWorldState.maxx"/>). Setting a different value calls the
    /// map grid hook <see cref="DmWorldState.MapResized"/> after storing it, as BYOND creates (or drops) the turfs
    /// of the changed map at the assignment.
    /// </summary>
    public float maxx
    {
        get => DmWorldState.Current.maxx;
        set
        {
            DmWorldState state = DmWorldState.Current;
            float old = state.maxx;
            if (old == value) return;
            state.maxx = value;
            state.MapResized?.Invoke(old, state.maxy, state.maxz);
        }
    }

    /// <summary><c>world.maxy</c>: map height in turfs; a change calls <see cref="DmWorldState.MapResized"/>.</summary>
    public float maxy
    {
        get => DmWorldState.Current.maxy;
        set
        {
            DmWorldState state = DmWorldState.Current;
            float old = state.maxy;
            if (old == value) return;
            state.maxy = value;
            state.MapResized?.Invoke(state.maxx, old, state.maxz);
        }
    }

    /// <summary><c>world.maxz</c>: number of z-levels; a change calls <see cref="DmWorldState.MapResized"/>.</summary>
    public float maxz
    {
        get => DmWorldState.Current.maxz;
        set
        {
            DmWorldState state = DmWorldState.Current;
            float old = state.maxz;
            if (old == value) return;
            state.maxz = value;
            state.MapResized?.Invoke(state.maxx, state.maxy, old);
        }
    }

    /// <summary>
    /// <c>world.turf</c>: the default turf type, which fills the map when it grows (BYOND ref <c>/world/var/turf</c>).
    /// Storage only (<see cref="DmWorldState.turf"/>); the Tg bootstrap sets tg's value from <c>code/world.dm</c>.
    /// </summary>
    public Type turf
    {
        get => DmWorldState.Current.turf;
        set => DmWorldState.Current.turf = value;
    }

    /// <summary><c>world.area</c>: the default area type, which holds the turfs of a growing map (<see cref="DmWorldState.area"/>).</summary>
    public Type area
    {
        get => DmWorldState.Current.area;
        set => DmWorldState.Current.area = value;
    }

    /// <summary><c>world.mob</c>: the mob type a connecting player gets (<see cref="DmWorldState.mob"/>).</summary>
    public Type mob
    {
        get => DmWorldState.Current.mob;
        set => DmWorldState.Current.mob = value;
    }

    /// <summary><c>world.name</c>: the world's name (<see cref="DmWorldState.name"/>); tg renames the world at run time.</summary>
    public string name
    {
        get => DmWorldState.Current.name;
        set => DmWorldState.Current.name = value;
    }

    /// <summary>
    /// <c>world.view</c>: the default view size, a number or text such as <c>"15x15"</c> (<see cref="DmWorldState.view"/>;
    /// BYOND's default 5).
    /// </summary>
    public object view
    {
        get => DmWorldState.Current.view;
        set => DmWorldState.Current.view = value;
    }

    /// <summary>
    /// <c>world.params</c>: the parameters the world was started with, an associative list of text (BYOND ref
    /// <c>/world/var/params</c>); empty unless the host sets some (<see cref="DmWorldState.@params"/>).
    /// </summary>
    public DmList @params
    {
        get => DmWorldState.Current.@params;
        set => DmWorldState.Current.@params = value;
    }

    /// <summary>
    /// <c>world.system_type</c>: the operating system, <see cref="Dm.UNIX"/> or <see cref="Dm.MS_WINDOWS"/> (read-only).
    /// Always <see cref="Dm.UNIX"/> in the port (<see cref="DmWorldState.system_type"/>), so tg never takes its
    /// Windows-only branches (<c>shelleo</c>, the DLL names of <c>rust_g.dm</c>).
    /// </summary>
    public string system_type => DmWorldState.Current.system_type;

    /// <summary><c>world.sleep_offline</c>: whether the world sleeps without players (<see cref="DmWorldState.sleep_offline"/>); stored only.</summary>
    public float sleep_offline
    {
        get => DmWorldState.Current.sleep_offline;
        set => DmWorldState.Current.sleep_offline = value;
    }

    /// <summary><c>world.log</c>: the target of <c>Dm.output_to(world.log, x)</c>, which goes to <see cref="DmWorldState.WorldLog"/>.</summary>
    public DmWorldLog log => DmWorldLog.Instance;

    /// <summary>
    /// <c>world.contents</c>: "all areas, turfs, mobs, and objs" (BYOND ref <c>/world/var/contents</c>): a new list of
    /// the live registered atoms in the world's order: mobs, other movables, areas, turfs, each group in id order
    /// (OpenDream <c>AtomManager.EnumerateAtoms</c>). <c>for (x in world)</c> is <c>Dm.each(world)</c>, which walks the
    /// registry in the same order without building this list.
    /// </summary>
    public DmList contents
    {
        get
        {
            var list = new DmList();
            ObjectRegistry objects = World.Current.Objects;
            for (int group = 0; group <= DmWorldOrder.Last; group++)
            {
                long cursor = 0;
                while (objects.TryGetNextLive(cursor, out long id, out object obj))
                {
                    if (obj is IDmAtom atom && atom.dm_world_order == group) list.AppendItem(obj);
                    cursor = id;
                }
            }
            return list;
        }
    }
}

/// <summary>The value of <c>world.log</c>: a marker that <c>Dm.output_to</c> routes to the host log.</summary>
public sealed class DmWorldLog
{
    internal static readonly DmWorldLog Instance = new();

    private DmWorldLog()
    {
    }
}
