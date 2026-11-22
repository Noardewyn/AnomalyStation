using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

/// <summary>
/// The shim's per-world state (docs/ai/specs/object-model.md section 10): one instance per <see cref="World"/>,
/// reached through the Kernel extension slot. Holds what BYOND keeps per world and the shim must not keep in
/// statics: the presentation sinks, the <c>world.log</c> hook, the map size behind <c>world.maxx/maxy/maxz</c>,
/// the real-time start, and the not-translated policy.
/// <para>
/// A partial class: each shim package adds its own state in its own file (<c>DmWorldState.Values.cs</c>,
/// <c>DmWorldState.Dynamic.cs</c>, for example the tag index behind <c>locate(tag)</c>), docs/ai/specs/shim-ownership.md.
/// Creating it registers the state hash contributor "DmWorldState" (map size, real-time start and the list ref
/// counters of <c>DmWorldState.Refs.cs</c>), which adds nothing while they are at their defaults, so creating the
/// state does not change the hash.
/// </para>
/// </summary>
public sealed partial class DmWorldState
{
    private HashSet<string> _notTranslatedReported;

    private DmWorldState(World world)
    {
        Sinks = new PresentationSinks(world);
        world.AddStateHasher("DmWorldState", static (World w, ref StateHasher h) => Of(w).HashState(ref h));
        world.AddStateHasher("DmWorldState.World", HashWorldVars); // DmWorldState.World.cs
    }

    /// <summary>The state of <paramref name="world"/>, created on first use.</summary>
    public static DmWorldState Of(World world) => world.GetExtension(static w => new DmWorldState(w));

    /// <summary>The state of the ambient world (<see cref="World.Current"/>).</summary>
    public static DmWorldState Current => Of(World.Current);

    /// <summary>The world's presentation sinks (docs/ai/specs/presentation-seam.md); null sinks until a host installs some.</summary>
    public PresentationSinks Sinks { get; }

    /// <summary>
    /// The object of the last <c>animate</c> call that named one: a step without an object continues it (BYOND). Weak, so
    /// it never keeps a deleted object alive (the registry's references are weak, object-model.md section 3).
    /// </summary>
    internal WeakReference<object> LastAnimateTarget { get; set; }

    /// <summary>
    /// Where <c>world.log &lt;&lt; x</c> goes (<c>Dm.output_to(world.log, x)</c>, presentation-seam.md section 3): the
    /// host's log. Null discards.
    /// </summary>
    public Action<string> WorldLog { get; set; }

    /// <summary>
    /// BYOND's <c>world/Reboot</c> (M10 slice H): set when tg's <c>world/Reboot</c> reaches BYOND's own part (its <c>..()</c>). BYOND
    /// restarts the world and its clients reconnect; the port's host replaces the world with a fresh one after the tick (D15) and
    /// keeps the connections. A host-built world without a host leaves it set.
    /// </summary>
    public bool RebootRequested { get; set; }

    /// <summary>
    /// <c>Dm.NotTranslated</c> throws (true, the default: tests fail loudly) or reports once per site and returns
    /// (false: a running server carries on), rules 1.4.
    /// </summary>
    public bool NotTranslatedThrows { get; set; } = true;

    /// <summary>
    /// BYOND <c>world.realtime</c> at world time 0, in deciseconds since 00:00:00 GMT, January 1, 2000 (BYOND ref
    /// <c>/world/var/realtime</c>). Set by the host; <c>world.realtime</c> and <c>world.timeofday</c> advance with
    /// <c>world.time</c> from it, never from a wall clock (D31, DV3). Default 0.
    /// </summary>
    public double RealtimeStart { get; set; }

    /// <summary>BYOND <c>world.maxx</c>: the map width in turfs. Set by the map loader; tg also changes it.</summary>
    public float maxx;

    /// <summary>BYOND <c>world.maxy</c>: the map height in turfs.</summary>
    public float maxy;

    /// <summary>BYOND <c>world.maxz</c>: the number of z-levels (tg's <c>incrementMaxZ</c> raises it).</summary>
    public float maxz;

    // First report of a not-translated site in this world?
    internal bool FirstNotTranslatedReport(string where, string what)
        => (_notTranslatedReported ??= new HashSet<string>(StringComparer.Ordinal)).Add(where + "\n" + what);

    // Adds nothing in the default state, so creating this extension (a host installing sinks, a read of
    // world.realtime) never changes the world's state hash (World.AddStateHasher).
    private void HashState(ref StateHasher h)
    {
        if (maxx == 0f && maxy == 0f && maxz == 0f && RealtimeStart == 0d && ListRefsAtDefault) return;
        h.Add(maxx);
        h.Add(maxy);
        h.Add(maxz);
        h.Add(RealtimeStart);
        HashListRefs(ref h); // DmWorldState.Refs.cs
    }
}
