using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

// M4b audio slice 2 (protocol version 13, docs/ai/specs/protocol.md section 22, presentation-seam.md section 4): tg's sounds for
// the sessions. tg's SEND_SOUND (Dm.output_to with a /sound) reaches ISoundSink.Play for one listener; the feed copies the
// datum at the call (tg shares one /sound across the listeners of a playsound and rewrites its volume and position for each,
// and a token reuses its master sound), maps the target to its session, and adds what tg does not compute: the muffle (DV37:
// 1 minus tg's pressure factor without its "touching" floor, for the client's low-pass) and the occlusion (DV39, D43: how far
// the station's walls and doors stand between the two tiles, from LINDA's atmos_adjacent_turfs). Both are pure presentation:
// the feed only reads the world (turfs, their air, their adjacency lists) and never changes it; the same run with and without
// it keeps the same state hash. The host builds each joined session's message after the tick (Build) and sends it after the
// Delta and the Airflow.

/// <summary>One sound as the feed copied it at tg's <c>SEND_SOUND</c> for one listener (<see cref="SoundFeed.Record"/>).</summary>
/// <param name="Kind">Play, update (tg's <c>SOUND_UPDATE</c>) or stop (a null file).</param>
/// <param name="Channel">tg's channel, 0 to 1024 (0: a play on any free channel, a stop of every channel).</param>
/// <param name="Path">A play's file, tg's resource path (null for an update or a stop).</param>
/// <param name="Volume">tg's final volume, 0 to 100.</param>
/// <param name="Muffle">1 - tg's pressure factor without the touching floor, 0 to 255.</param>
/// <param name="Occlusion">The station graph's occlusion, 0 to 255 (section 10 of the audio plan).</param>
/// <param name="Environment">tg's environment + 1 (0 none).</param>
/// <param name="X">The source tile (when <paramref name="Positional"/>).</param>
public readonly record struct FeedSound(SoundKind Kind, int Channel, string Path, float Volume = 0, byte Muffle = 0, byte Occlusion = 0,
    byte Environment = 0, bool Repeat = false, bool Mute = false, bool Positional = false, int X = 0, int Y = 0, int Z = 0,
    float Frequency = 0, float Offset = 0, bool Wait = false)
{
    /// <summary>Whether the budget keeps it whatever the tick's load: a stop, an update or a mute keeps the client's channels right.</summary>
    public bool Protected => Kind != SoundKind.Play || Mute;
}

/// <summary>
/// What one session was sent of its sounds (its path table) and what it heard in the current tick (the pending copies, the
/// occlusion search from its listener's tile). The host keeps one per session (<c>Session.Sounds</c>).
/// </summary>
public sealed class SoundView
{
    internal readonly List<FeedSound> Pending = new();
    internal readonly Dictionary<string, uint> Paths = new(StringComparer.Ordinal);

    // The occlusion search (a Dijkstra over the station graph from the listener's tile, grown on demand): the settled costs by
    // tile key, the best tentative ones, the frontier. It is kept while the listener stays on its tile, for at most
    // SoundFeed.SearchReuseTicks (M10 slice J: rebuilt every tick it cost 100 sessions a quarter of the tick), so a door that opened
    // or closed reaches the occlusion within half a second; a turf replaced meanwhile only ends the search early there.
    internal long SearchTick = -1;
    internal int SearchOrigin = -1;
    internal readonly Dictionary<int, float> Settled = new();
    internal readonly Dictionary<int, float> Best = new();
    internal readonly PriorityQueue<turf, float> Frontier = new();

    /// <summary>Entries of the session's path table (what its client was sent).</summary>
    public int PathCount => Paths.Count;

    /// <summary>The copies recorded for the session in the current tick.</summary>
    public IReadOnlyList<FeedSound> PendingSounds => Pending;

    /// <summary>A join: the client's path table starts empty (a MapGrid clears it), so the session's does too; the tick's pending sounds stay.</summary>
    public void Restart() => Paths.Clear();

    /// <summary>Drops the tick's pending sounds and the occlusion search (a session that is not joined at the send).</summary>
    public void Discard()
    {
        Pending.Clear();
        ClearSearch();
    }

    internal void ClearSearch()
    {
        SearchTick = -1;
        SearchOrigin = -1;
        Settled.Clear();
        Best.Clear();
        Frontier.Clear();
    }
}

/// <summary>The sound sink of a server's world (<see cref="ISoundSink"/>): records each session's sounds of the tick and builds its <see cref="Sounds"/> message.</summary>
public sealed class SoundFeed : ISoundSink
{
    /// <summary>The search depth of the occlusion, in tiles of path (the owner's design: 32).</summary>
    public const float OcclusionDepth = 32f;

    /// <summary>Ticks a session's occlusion search is reused while its listener stays on the same tile (10: half a second).</summary>
    public const int SearchReuseTicks = 10;

    /// <summary>A path at most this many tiles longer than the straight line is an open line (occlusion 0).</summary>
    public const float OpenLineSlack = 2f;

    /// <summary>Tiles of detour per unit of occlusion (a detour of 8 more tiles: 1, capped at <see cref="MaxDetourOcclusion"/>).</summary>
    public const float DetourTilesPerUnit = 8f;

    /// <summary>The most occlusion a sound heard around corners gets (a sealed-off one gets 1).</summary>
    public const float MaxDetourOcclusion = 0.5f;

    private const float Diagonal = 1.41421356f;
    private const float OneAtmosphere = 101.325f; // tg's ONE_ATMOSPHERE
    private const float MinimumPressure = 10f;    // tg's SOUND_MINIMUM_PRESSURE
    private const int StatusMute = 1, StatusUpdate = 16; // BYOND's SOUND_MUTE and SOUND_UPDATE

    private readonly Func<client, SoundView> _viewOf;
    private readonly Func<IEnumerable<client>> _everyone;

    /// <summary>
    /// A feed whose sessions are found by <paramref name="viewOf"/> (a client's session view, null for one with no session) and,
    /// for a sound sent to the world, <paramref name="everyone"/> (the clients of the sessions).
    /// </summary>
    public SoundFeed(Func<client, SoundView> viewOf, Func<IEnumerable<client>> everyone)
    {
        _viewOf = viewOf ?? throw new ArgumentNullException(nameof(viewOf));
        _everyone = everyone ?? (() => Array.Empty<client>());
    }

    /// <summary>Plays dropped by the budget, over the run.</summary>
    public long Dropped { get; private set; }

    /// <summary>Exceptions the feed caught in a sink call (a feed bug; the world goes on), over the run, and the first one's text.</summary>
    public long Errors { get; private set; }

    public string FirstError { get; private set; }

    /// <summary>Installs the feed as the world's sound sink.</summary>
    public void Attach(World world) => PresentationSinks.Of(world).Sounds = this;

    // ---- the sink: inside the world, in tg's order ----

    /// <inheritdoc/>
    public void Play(object target, sound s)
    {
        if (s == null) return;
        try
        {
            Route(target, s, 0);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A presentation sink never changes the world, and a bug in it must not break tg's proc that played the sound.
            Errors++;
            FirstError ??= e.GetType().Name + ": " + e.Message;
        }
    }

    // tg's targets as ChatRouter.Route takes them: a client, a mob with a client, a list of those, the world.
    private void Route(object target, sound s, int depth)
    {
        switch (target)
        {
            case null:
                return;
            case client c:
                Hear(c, c.mob, s);
                return;
            case mob m when m.client != null:
                Hear(m.client, m, s);
                return;
            case DmList list when depth < 4:
                for (int i = 1; i <= list.Count; i++)
                    Route(list[(float)i], s, depth + 1);
                return;
            case DmWorld:
                foreach (client c in _everyone())
                    Hear(c, c?.mob, s);
                return;
        }
    }

    private void Hear(client c, mob listener, sound s)
    {
        if (c == null) return;
        SoundView view = _viewOf(c);
        if (view == null) return;
        FeedSound? copy = Copy(view, listener, s);
        if (copy.HasValue) Record(view, copy.Value);
    }

    /// <summary>Records a copied sound for a session (the sink's own path; tests feed it directly).</summary>
    public void Record(SoundView view, in FeedSound sound)
    {
        ArgumentNullException.ThrowIfNull(view);
        view.Pending.Add(sound);
    }

    // The copy of tg's datum for one listener, with the feed's muffle and occlusion; null for what the wire cannot carry
    // (an update on channel 0, a play of no file the client could load).
    private FeedSound? Copy(SoundView view, mob listener, sound s)
    {
        int status = (int)s.status;
        int channel = Math.Clamp((int)s.channel, 0, Limits.MaxSoundChannel);
        string path = PathOf(s.file);
        SoundKind kind = (status & StatusUpdate) != 0 ? SoundKind.Update : path == null ? SoundKind.Stop : SoundKind.Play;
        if (kind == SoundKind.Stop)
            return new FeedSound(SoundKind.Stop, channel, null);
        if (kind == SoundKind.Update && channel == 0)
            return null; // BYOND updates the sound of a channel; channel 0 names none
        if (kind == SoundKind.Play && System.Text.Encoding.UTF8.GetByteCount(path) > Limits.MaxSoundPathBytes)
            return null;

        float volume = float.IsFinite(s.volume) ? Math.Clamp(s.volume, 0f, 100f) : 0f;
        float? env = NumberOf(s.environment);
        byte environment = env is float e && e >= 0 && e <= Limits.MaxSoundEnvironment && e == MathF.Floor(e) ? (byte)(e + 1) : (byte)0;
        // playsound_local's use_reverb FALSE writes the room levels -10000 into the echo (sound.dm:166-169): BYOND plays it dry
        if (s.echo is DmList echo && echo.len >= 4 && NumberOf(echo[3]) is float room && room <= -10000f)
            environment = 0;
        // tg's playsound_local writes environment and echo only for a sound from a turf (sound.dm:127-148): those are the ones
        // the air between the two tiles muffles, as tg's pressure factor scales them.
        bool spatial = s.echo != null || env != -1f;
        bool offset = s.x != 0 || s.y != 0 || s.z != 0;

        turf here = TurfOf(listener);
        bool positional = false;
        int x = 0, y = 0, z = 0;
        byte muffle = 0, occlusion = 0;
        if (here != null)
        {
            turf source = here;
            if (offset && float.IsFinite(s.x) && float.IsFinite(s.y) && float.IsFinite(s.z))
            {
                DmWorldState world = DmWorldState.Current;
                x = Math.Clamp((int)here.x + (int)MathF.Round(s.x), 1, Math.Min((int)world.maxx, Limits.MaxMapSize));
                y = Math.Clamp((int)here.y + (int)MathF.Round(s.z), 1, Math.Min((int)world.maxy, Limits.MaxMapSize));
                z = Math.Clamp((int)here.z + (int)MathF.Round(s.y / 5f), 1, Math.Min((int)world.maxz, Limits.MaxMapLevels));
                positional = true;
                source = Builtins.locate(x, y, z) ?? here;
            }
            if (spatial && !s.port_pressure_unaffected) // playsound's pressure_affected = FALSE: no muffle (DV37)
                muffle = ToByte(Muffle(here, source));
            if (positional && !ReferenceEquals(source, here) && volume > 0)
                occlusion = ToByte(Occlusion(view, here, source));
        }

        float frequency = float.IsFinite(s.frequency) && MathF.Abs(s.frequency) <= Limits.MaxSoundFrequency ? s.frequency : 0f;
        float start = float.IsFinite(s.offset) && s.offset > 0 && s.offset <= Limits.MaxSoundOffset ? s.offset : 0f;
        return new FeedSound(kind, channel, kind == SoundKind.Play ? path : null, volume, muffle, occlusion, environment,
            s.repeat != 0, (status & StatusMute) != 0, positional, x, y, z, frequency, start, kind == SoundKind.Play && s.wait != 0); // version 78: sound.wait
    }

    private static float? NumberOf(object value) => value switch
    {
        float f => f,
        int i => i,
        double d => (float)d,
        _ => null,
    };

    // tg's resource path of a sound's file (a resource, or a path text), null for none.
    private static string PathOf(object file) => file switch
    {
        DmResource r when !string.IsNullOrEmpty(r.Path) => r.Path,
        string text when text.Length > 0 => text,
        _ => null,
    };

    // The turf an atom is on, through what holds it (a locker, a mech); null in nullspace.
    private static turf TurfOf(atom thing)
    {
        for (int depth = 0; depth < 16 && thing != null; depth++)
        {
            if (thing is turf t) return t;
            thing = thing.loc as atom;
        }
        return null;
    }

    private static byte ToByte(float fraction) => (byte)Math.Clamp((int)MathF.Round(fraction * 255f), 0, 255);

    // ---- the muffle (DV37) ----

    /// <summary>
    /// 1 minus tg's pressure factor (sound.dm:131-141) of the two tiles without its "touching the source" floor: the lower of the
    /// two pressures, 1 atmosphere or more gives 0, 10 kPa or less gives 1. A tile with no air of its own (a closed turf, whose
    /// tg <c>return_air</c> is a fresh copy of its initial mix) does not count; with neither, 0.
    /// </summary>
    public static float Muffle(turf hearer, turf source)
    {
        float? a = PressureOf(hearer), b = PressureOf(source);
        if (a == null && b == null) return 0f;
        float pressure = Math.Min(a ?? float.MaxValue, b ?? float.MaxValue);
        if (pressure >= OneAtmosphere) return 0f;
        float factor = Math.Max((pressure - MinimumPressure) / (OneAtmosphere - MinimumPressure), 0f);
        return 1f - factor;
    }

    // An open turf's air pressure (tg's return_air is its air), read without calling into the world's procs that create things.
    private static float? PressureOf(turf t)
    {
        if (t is not turf_open open) return null;
        if (open.air == null) return 0f; // tg's "else //space" branch: no mixture
        float pressure = Dm.num(open.air.return_pressure());
        return float.IsFinite(pressure) ? pressure : 0f;
    }

    // ---- the occlusion (DV39, D43) ----

    /// <summary>
    /// The occlusion between the listener's tile and the source's from the station graph (LINDA's <c>atmos_adjacent_turfs</c>:
    /// walls, closed doors, windows and airtight things cut it, open doors join it): 0 when a path of air is at most
    /// <see cref="OpenLineSlack"/> tiles longer than the straight line, the detour over <see cref="DetourTilesPerUnit"/> up to
    /// <see cref="MaxDetourOcclusion"/> when the path goes around, 1 when no path of at most <see cref="OcclusionDepth"/> tiles
    /// joins them. The path moves along the graph's cardinal and vertical links (1 a step) and diagonally (1.414) where both
    /// cardinal ways round the corner are open, so a diagonal across an open room is an open line. A source on an airtight tile
    /// (a closed door, a wall: no links of its own) is reached through its neighbours, as a sound from the door's face.
    /// The search runs from the listener's tile, grows only as far as the sounds need, and is kept while the listener stays on its
    /// tile for up to <see cref="SearchReuseTicks"/>.
    /// </summary>
    public float Occlusion(SoundView view, turf listener, turf source)
    {
        if (listener == null || source == null || ReferenceEquals(listener, source)) return 0f;
        float straight = MathF.Sqrt(Sq(source.x - listener.x) + Sq(source.y - listener.y) + Sq(source.z - listener.z));
        float path = PathCost(view, listener, source);
        if (!float.IsFinite(path) && !HasLinks(source))
        {
            // the source's own tile takes no air: reach it through the open tiles next to it
            foreach (turf next in NeighboursOf(source))
                path = Math.Min(path, PathCost(view, listener, next) + 1f);
        }
        if (!float.IsFinite(path) || path > OcclusionDepth + 1f) return 1f;
        if (path <= straight + OpenLineSlack) return 0f;
        return Math.Clamp((path - straight) / DetourTilesPerUnit, 0f, MaxDetourOcclusion);
    }

    private static float Sq(float v) => v * v;

    private static int Key(turf t) => ((int)t.z << 16) | ((int)t.y << 8) | (int)t.x;

    private static bool HasLinks(turf t) => t.atmos_adjacent_turfs is { Count: > 0 };

    // The tiles beside an airtight one: the four cardinals and the levels above and below that exist.
    private static IEnumerable<turf> NeighboursOf(turf t)
    {
        int x = (int)t.x, y = (int)t.y, z = (int)t.z;
        turf n;
        if ((n = Builtins.locate(x + 1, y, z)) != null) yield return n;
        if ((n = Builtins.locate(x - 1, y, z)) != null) yield return n;
        if ((n = Builtins.locate(x, y + 1, z)) != null) yield return n;
        if ((n = Builtins.locate(x, y - 1, z)) != null) yield return n;
    }

    // The cost of the cheapest path of air from the listener's tile to the target (infinity past the depth), the tick's search
    // grown until the target is settled or the frontier passes the depth.
    private float PathCost(SoundView view, turf listener, turf target)
    {
        long tick = World.CurrentOrNull?.Tick ?? -1;
        int origin = Key(listener);
        if (view.SearchOrigin != origin || tick < view.SearchTick || tick - view.SearchTick >= SearchReuseTicks)
        {
            view.ClearSearch();
            view.SearchTick = tick;
            view.SearchOrigin = origin;
            view.Best[origin] = 0f;
            view.Frontier.Enqueue(listener, 0f);
        }

        int goal = Key(target);
        if (view.Settled.TryGetValue(goal, out float known)) return known;
        while (view.Frontier.TryDequeue(out turf t, out float cost))
        {
            int key = Key(t);
            if (view.Settled.ContainsKey(key)) continue;
            if (cost > OcclusionDepth)
            {
                view.Frontier.Enqueue(t, cost); // keep it: a later query finds the frontier as it was
                break;
            }
            view.Settled[key] = cost;
            Expand(view, t, cost);
            if (key == goal) return cost;
        }
        return float.PositiveInfinity;
    }

    private static void Expand(SoundView view, turf t, float cost)
    {
        DmList links = t.atmos_adjacent_turfs;
        if (links == null) return;
        for (int i = 1; i <= links.Count; i++)
        {
            if (links[(float)i] is not turf a) continue;
            Relax(view, a, cost + 1f);
            // a diagonal step where both cardinal ways round the corner are open (a -> d and b -> d with t -> b)
            if (a.z != t.z || a.x == t.x) continue;
            for (int j = 1; j <= links.Count; j++)
            {
                if (links[(float)j] is not turf b || b.z != t.z || b.y == t.y) continue;
                turf d = LinkAt(a, (int)a.x, (int)b.y, (int)t.z);
                if (d != null && LinkAt(b, (int)a.x, (int)b.y, (int)t.z) != null)
                    Relax(view, d, cost + Diagonal);
            }
        }
    }

    // The link of a tile to the tile at (x, y, z), or null when air does not pass between them.
    private static turf LinkAt(turf from, int x, int y, int z)
    {
        DmList links = from.atmos_adjacent_turfs;
        if (links == null) return null;
        for (int i = 1; i <= links.Count; i++)
        {
            if (links[(float)i] is turf n && (int)n.x == x && (int)n.y == y && (int)n.z == z) return n;
        }
        return null;
    }

    private static void Relax(SoundView view, turf next, float cost)
    {
        int key = Key(next);
        if (view.Settled.ContainsKey(key)) return;
        if (view.Best.TryGetValue(key, out float best) && best <= cost) return;
        view.Best[key] = cost;
        view.Frontier.Enqueue(next, cost);
    }

    // ---- the send: outside the world, after the tick ----

    /// <summary>
    /// The session's <see cref="Sounds"/> messages of the tick (none when it heard nothing; more than one only when its stops,
    /// updates and mutes alone pass <see cref="Limits.MaxSoundEvents"/>), its pending sounds cleared. The budget: at most
    /// <see cref="Limits.MaxSoundEvents"/> events and <see cref="Limits.MaxSoundPaths"/> new paths; past it the quietest plays
    /// are dropped first (counted in <see cref="Dropped"/>), never a stop, an update or a mute. The events keep tg's order.
    /// </summary>
    public List<Sounds> Build(SoundView view, out int dropped)
    {
        ArgumentNullException.ThrowIfNull(view);
        dropped = 0;
        var messages = new List<Sounds>();
        List<FeedSound> pending = view.Pending;
        if (pending.Count == 0) return messages;

        int protectedCount = 0;
        foreach (FeedSound s in pending)
            if (s.Protected) protectedCount++;

        // the plays the budget keeps: the loudest first, each needing a slot and, for a path the client lacks, a new path
        var keep = new bool[pending.Count];
        var plays = new List<int>();
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].Protected) keep[i] = true;
            else plays.Add(i);
        }
        plays.Sort((a, b) =>
        {
            int c = pending[b].Volume.CompareTo(pending[a].Volume);
            return c != 0 ? c : a.CompareTo(b);
        });
        int slots = Math.Max(0, Limits.MaxSoundEvents - protectedCount);
        var newPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (int i in plays)
        {
            FeedSound s = pending[i];
            bool known = s.Path == null || view.Paths.ContainsKey(s.Path) || newPaths.Contains(s.Path);
            if (slots == 0 || (!known && newPaths.Count == Limits.MaxSoundPaths))
            {
                dropped++;
                continue;
            }
            if (!known) newPaths.Add(s.Path);
            keep[i] = true;
            slots--;
        }

        var message = new Sounds();
        for (int i = 0; i < pending.Count; i++)
        {
            if (!keep[i]) continue;
            FeedSound s = pending[i];
            uint index = 0;
            if (s.Kind == SoundKind.Play && s.Path != null && !view.Paths.TryGetValue(s.Path, out index))
            {
                if (message.NewPaths.Count == 0) message.FirstPath = (uint)view.Paths.Count + 1;
                index = (uint)view.Paths.Count + 1;
                view.Paths[s.Path] = index;
                message.NewPaths.Add(s.Path);
            }
            if (message.Events.Count == Limits.MaxSoundEvents)
            {
                messages.Add(message);
                message = new Sounds();
            }
            message.Events.Add(ToEvent(s, index));
        }
        if (message.Events.Count > 0 || message.NewPaths.Count > 0) messages.Add(message);
        pending.Clear();
        Dropped += dropped;
        return messages;
    }

    private static SoundEvent ToEvent(in FeedSound s, uint path)
    {
        if (s.Kind == SoundKind.Stop)
            return new SoundEvent { Kind = SoundKind.Stop, Channel = (ushort)s.Channel };
        return new SoundEvent
        {
            Kind = s.Kind,
            Channel = (ushort)s.Channel,
            Path = s.Kind == SoundKind.Play ? path : 0,
            Volume = (byte)Math.Clamp((int)MathF.Round(s.Volume * 2f), 0, Limits.MaxSoundVolume),
            Muffle = s.Muffle,
            Occlusion = s.Occlusion,
            Environment = s.Environment,
            Repeat = s.Repeat,
            Mute = s.Mute,
            Wait = s.Wait, // version 78
            Positional = s.Positional,
            X = s.Positional ? (byte)s.X : (byte)0,
            Y = s.Positional ? (byte)s.Y : (byte)0,
            Z = s.Positional ? s.Z : 0,
            Frequency = s.Frequency,
            Offset = s.Offset,
        };
    }
}
