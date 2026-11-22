using Anomaly.Core.DmShim;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Replication;

/// <summary>
/// Protocol version 9 (M4v slice 1, D143): records BYOND's <c>animate()</c> chains of the replicated movables from
/// <see cref="IAppearanceSink.AnimateStep"/> (inside the world: it keeps ids and plain numbers, never a reference to an object) and
/// gives the replicator each entity's <see cref="EntityAnimation"/> when it reads the entity (docs/ai/specs/replication.md section 6,
/// docs/ai/specs/protocol.md section 18).
/// <para>
/// BYOND's rules as the client needs them: a call with an object starts a chain (a track); a call without one adds a step to the chain
/// of the last call that named an object (the shim's subject). A chain without <c>ANIMATION_PARALLEL</c> on its first step replaces
/// every running track; a parallel one is added beside them; <c>ANIMATION_CONTINUE</c> adds the call's steps to the last track. A track
/// starts from the vars' values before the call (the shim's <c>before</c>; BYOND starts an interrupted animation from where it is
/// drawn, so a chain that interrupts another starts as <c>ANIMATION_END_NOW</c> would). Each step keeps its numbers as tg gave them
/// (relative ones as deltas), its time, delay, easing and flags. <c>loop</c> is the first step's.
/// </para>
/// <para>
/// Not recorded: tg's attack lunge (<see cref="Replicator"/> sends it as <see cref="EntityFlags.Lunge"/>), animations of anything but a
/// registered movable (turfs, images, screen objects, clients), the vars the 3D client does not show (icon_state, dir, layer, maptext,
/// ...), a colour matrix (a list), and the transform of the lying turn (tg's <c>update_transform</c> on a living mob names the
/// transform with <c>dir</c>: the client's posture draws it). Limits: <see cref="Limits.MaxAnimationTracks"/> tracks (the oldest goes),
/// <see cref="Limits.MaxAnimationSteps"/> steps (a longer chain is not sent: its end shows at once), loop 0 to
/// <see cref="Limits.MaxAnimationLoop"/>, times and delays to <see cref="Limits.MaxAnimationTime"/>, values to
/// <see cref="Limits.MaxAnimatedValue"/>.
/// </para>
/// <para>
/// Version 26 (D156): <c>animate(filter, ...)</c> of a filter in a replicated movable's filters list (<see cref="dm_filter.Owner"/>)
/// is a track of that movable naming the filter by its position (<see cref="AnimationTrack.Filter"/>): it replaces the running tracks
/// of the same filter only (not the atom's own, nor another filter's), unless parallel; its start values are the parameters before the
/// call, BYOND's default for one never given. BYOND continues a filter's sequence with a later call on its atom or another of its
/// filters; such a mixed chain is two tracks here.
/// </para>
/// </summary>
internal sealed class AnimationRecorder
{
    private const int Parallel = 4, Relative = 256, Continue = 512, LinearTransform = 2; // BYOND's ANIMATION_* flags

    private sealed class Track
    {
        public long StartTick = -1; // -1 until the replicator reads it (the tick of the read: the chain's tick)
        public int Loop = 1;
        public byte Filter; // 0: the atom's own vars; else the filter's position (version 26)
        public string FilterType;
        public FilterVars FilterStart;
        public bool Parallel;
        public bool Overflow;
        public AnimatedVars Start;
        public readonly List<AnimationStep> Steps = new();
        public AnimationTrack Built;
    }

    private sealed class Entry
    {
        public readonly List<Track> Tracks = new();
    }

    private readonly Dictionary<long, Entry> _current = new();
    private readonly HashSet<long> _changed = new();
    private readonly PriorityQueue<long, long> _ends = new(); // ids by the tick their animation ends (a read then sends "none")
    private Track _chain; // the chain the next step without an object continues; null: such a step is not recorded
    private long _chainId;
    private object _chainSubject; // the object of that chain (a movable or a filter), compared by reference

    /// <summary>Chains not sent because they had more than <see cref="Limits.MaxAnimationSteps"/> steps.</summary>
    public int DroppedLong { get; private set; }

    /// <summary>The ids whose animation changed since the replicator last read them.</summary>
    public IReadOnlyCollection<long> Changed => _changed;

    /// <summary>Version 26: whether chains are recorded for <paramref name="id"/> (a screen entry's vis_contents child, the doll's limb).</summary>
    public bool Has(long id) => _current.ContainsKey(id);

    /// <summary>One animate() step (the sink, inside the world). <paramref name="lunge"/>: the replicator sends this chain as the lunge.</summary>
    public void Step(object target, object subject, DmList arguments, DmList before, bool lunge)
    {
        if (arguments == null)
            return;
        if (target != null)
        {
            _chain = null;
            _chainId = 0;
            _chainSubject = null;
            if (lunge || !TryOwner(subject, out atom_movable movable, out byte filter))
                return;
            int flags = (int)NumberOr(arguments, "flags", 0);
            long id = movable.dm_id;
            if (!_current.TryGetValue(id, out Entry entry))
                _current[id] = entry = new Entry();
            Track last = entry.Tracks.FindLast(t => t.Filter == filter);
            if ((flags & Continue) != 0 && last != null)
            {
                _chain = last;
            }
            else
            {
                if ((flags & Parallel) == 0)
                    entry.Tracks.RemoveAll(t => t.Filter == filter); // BYOND: an animation without ANIMATION_PARALLEL stops the running ones
                float loop = NumberOr(arguments, "loop", 1);
                _chain = new Track
                {
                    Parallel = (flags & Parallel) != 0, Loop = loop < 0 ? 0 : (int)Math.Clamp(loop, 1, Limits.MaxAnimationLoop), Filter = filter,
                    FilterType = (subject as dm_filter)?.type,
                };
                entry.Tracks.Add(_chain);
                if (entry.Tracks.Count > Limits.MaxAnimationTracks)
                    entry.Tracks.RemoveAt(0);
            }
            _chainId = id;
            _chainSubject = subject;
        }
        else if (_chain == null || !ReferenceEquals(subject, _chainSubject))
        {
            return;
        }

        _chain.Built = null;
        _changed.Add(_chainId);
        if (_chain.Filter != 0)
            AddFilterStep(_chain, arguments, before);
        else
            AddStep(_chain, subject, arguments, before);
    }

    // The replicated movable a subject animates: itself, or a filter's owner with the filter's position in its filters list.
    private static bool TryOwner(object subject, out atom_movable movable, out byte filter)
    {
        filter = 0;
        movable = subject as atom_movable;
        if (subject is dm_filter f)
        {
            movable = f.Owner as atom_movable;
            if (movable == null || movable.filters is not DmList list)
                return false;
            for (int i = 1; i <= list.Count && i <= Limits.MaxAppearanceFilters; i++)
            {
                if (!ReferenceEquals(list[(float)i], f)) continue;
                filter = (byte)i;
                break;
            }
            if (filter == 0)
                return false;
        }
        return movable != null && movable.dm_id != 0;
    }

    private static readonly (string Name, FilterVar Bit)[] FilterNumbers =
    {
        ("x", FilterVar.X), ("y", FilterVar.Y), ("size", FilterVar.Size), ("offset", FilterVar.Offset), ("alpha", FilterVar.Alpha),
        ("radius", FilterVar.Radius), ("falloff", FilterVar.Falloff), ("repeat", FilterVar.Repeat), ("density", FilterVar.Density),
        ("threshold", FilterVar.Threshold), ("factor", FilterVar.Factor),
    };

    // A filter's step (version 26): its numeric parameters, its colour (a text, or a colour filter's matrix); the start of each from
    // the value before, or BYOND's default for one never given.
    private static void AddFilterStep(Track track, DmList arguments, DmList before)
    {
        if (track.Steps.Count >= Limits.MaxAnimationSteps)
        {
            track.Overflow = true;
            return;
        }
        int flags = (int)NumberOr(arguments, "flags", 0);
        var stepFlags = (flags & Relative) != 0 ? AnimationStepFlags.Relative : AnimationStepFlags.None;
        int easing = (int)NumberOr(arguments, "easing", 0);
        byte wireEasing = (byte)(easing & 0xCF);
        if (!AnimationEasing.IsValid(wireEasing)) wireEasing = AnimationEasing.Linear;

        var vars = new FilterVars();
        FilterVars start = track.FilterStart;
        foreach ((string name, FilterVar bit) in FilterNumbers)
        {
            if (!TryGet(arguments, name, out object value) || !TryNumber(value, out float n))
                continue;
            vars.Set(bit, Value(n));
            if (start.Has(bit))
                continue;
            float from = before != null && TryGet(before, name, out object was) && TryNumber(was, out float w) ? w : AppearanceTables.FilterDefault(track.FilterType, name);
            start.Set(bit, Value(from));
        }
        if (TryGet(arguments, "color", out object color))
        {
            object was = before != null && TryGet(before, "color", out object b) ? b : null;
            if (track.FilterType == "color")
            {
                float[] to = MatrixOf(color), from = MatrixOf(was) ?? Identity();
                if (to != null)
                {
                    vars.Matrix = to;
                    vars.Mask |= FilterVar.ColorMatrix;
                    if (!start.Has(FilterVar.ColorMatrix))
                    {
                        start.Matrix = from;
                        start.Mask |= FilterVar.ColorMatrix;
                    }
                }
            }
            else if (color is string text)
            {
                vars.Rgba = Rgba(text);
                vars.Mask |= FilterVar.Color;
                if (!start.Has(FilterVar.Color))
                {
                    start.Rgba = was is string old ? Rgba(old) : track.FilterType switch { "rays" => 0xFFFFFFFF, "drop_shadow" => 0x00000080, _ => 0x000000FF };
                    start.Mask |= FilterVar.Color;
                }
            }
        }
        track.FilterStart = start;
        track.Steps.Add(new AnimationStep(Time(arguments, "time"), Time(arguments, "delay"), wireEasing, stepFlags, default, vars));
    }

    // A colour filter's colour as its 20 numbers: a matrix list, or a colour text as the channels it multiplies; null otherwise.
    private static float[] MatrixOf(object color)
    {
        switch (color)
        {
            case DmList list:
                return AppearanceTables.FullMatrix(list);
            case string text:
            {
                uint rgba = Rgba(text);
                float[] m = Identity();
                m[0] = (rgba >> 24) / 255f;
                m[5] = ((rgba >> 16) & 255) / 255f;
                m[10] = ((rgba >> 8) & 255) / 255f;
                m[15] = (rgba & 255) / 255f;
                return m;
            }
            default:
                return null;
        }
    }

    private static float[] Identity()
    {
        var m = new float[20];
        m[0] = m[5] = m[10] = m[15] = 1f;
        return m;
    }

    private static void AddStep(Track track, object subject, DmList arguments, DmList before)
    {
        if (track.Steps.Count >= Limits.MaxAnimationSteps)
        {
            track.Overflow = true;
            return;
        }
        int flags = (int)NumberOr(arguments, "flags", 0);
        var stepFlags = AnimationStepFlags.None;
        if ((flags & Relative) != 0) stepFlags |= AnimationStepFlags.Relative;
        if ((flags & LinearTransform) != 0) stepFlags |= AnimationStepFlags.LinearTransform;
        int easing = (int)NumberOr(arguments, "easing", 0);
        byte wireEasing = (byte)(easing & 0xCF);
        if (!AnimationEasing.IsValid(wireEasing)) wireEasing = AnimationEasing.Linear;
        // tg's update_transform (living_update_icons.dm) turns a lying mob's transform with its dir: the client's posture draws it.
        bool lyingTurn = subject is mob_living && Dm.@in("transform", arguments) && Dm.@in("dir", arguments);

        var vars = new AnimatedVars();
        AnimatedVars start = track.Start;
        Take(arguments, before, "pixel_x", AnimatedVar.PixelX, ref vars, ref start);
        Take(arguments, before, "pixel_y", AnimatedVar.PixelY, ref vars, ref start);
        Take(arguments, before, "pixel_z", AnimatedVar.PixelZ, ref vars, ref start);
        Take(arguments, before, "pixel_w", AnimatedVar.PixelW, ref vars, ref start);
        Take(arguments, before, "alpha", AnimatedVar.Alpha, ref vars, ref start);
        Take(arguments, before, "color", AnimatedVar.Color, ref vars, ref start);
        if (!lyingTurn)
            Take(arguments, before, "transform", AnimatedVar.Transform, ref vars, ref start);
        Take(arguments, before, "maptext_x", AnimatedVar.MaptextX, ref vars, ref start); // version 74: tg's status display's scrolling lines
        track.Start = start;
        track.Steps.Add(new AnimationStep(Time(arguments, "time"), Time(arguments, "delay"), wireEasing, stepFlags, vars));
    }

    // The step's value of one var, and the track's start value of it the first time a step names it (BYOND: from the value before).
    private static void Take(DmList arguments, DmList before, string name, AnimatedVar bit, ref AnimatedVars vars, ref AnimatedVars start)
    {
        if (!TryGet(arguments, name, out object value) || !TrySet(ref vars, bit, value))
            return;
        if (start.Has(bit))
            return;
        if (before == null || !TryGet(before, name, out object from) || !TrySet(ref start, bit, from))
            vars.Mask &= ~bit; // the subject has no such var (or holds a value the client cannot show): the step does not name it
    }

    private static bool TrySet(ref AnimatedVars v, AnimatedVar bit, object value)
    {
        switch (bit)
        {
            case AnimatedVar.Color:
                if (value is DmList) return false; // a colour matrix: not sent (protocol.md 18.1)
                if (value != null && value is not string) return false;
                v.Rgba = Rgba(value as string);
                break;
            case AnimatedVar.Transform:
                if (value == null)
                {
                    (v.A, v.B, v.C, v.D, v.E, v.F) = (1f, 0f, 0f, 0f, 1f, 0f);
                    break;
                }
                if (value is not matrix m) return false;
                (v.A, v.B, v.C, v.D, v.E, v.F) = (Value(m.a), Value(m.b), Value(m.c), Value(m.d), Value(m.e), Value(m.f));
                break;
            default:
                if (!TryNumber(value, out float n)) return false;
                n = Value(n);
                if (bit == AnimatedVar.PixelX) v.PixelX = n;
                else if (bit == AnimatedVar.PixelY) v.PixelY = n;
                else if (bit == AnimatedVar.PixelZ) v.PixelZ = n;
                else if (bit == AnimatedVar.PixelW) v.PixelW = n;
                else if (bit == AnimatedVar.MaptextX) v.MaptextX = n;
                else v.Alpha = n;
                break;
        }
        v.Mask |= bit;
        return true;
    }

    /// <summary>
    /// The replicator reads entity <paramref name="id"/> at <paramref name="tick"/>; <paramref name="sent"/> is what it sent last. True
    /// with the animation to send (null: none) when it changed, or when the one sent has ended (a finished chain is not kept).
    /// </summary>
    public bool TryTake(long id, long tick, EntityAnimation sent, out EntityAnimation now)
    {
        if (_changed.Remove(id))
        {
            now = Build(id, tick);
            return !Equals(now, sent);
        }
        if (sent != null && sent.EndTick(MotionMath.TicksPerSecond) <= tick)
        {
            _current.Remove(id);
            now = null;
            return true;
        }
        now = sent;
        return false;
    }

    /// <summary>
    /// Version 74: the chains recorded for <paramref name="id"/> as they run at <paramref name="tick"/>, without taking its change (a world entity's
    /// <c>vis_contents</c> child that several holders share: tg's status display text); null for none.
    /// </summary>
    public EntityAnimation Peek(long id, long tick) => Build(id, tick);

    private EntityAnimation Build(long id, long tick)
    {
        if (!_current.TryGetValue(id, out Entry entry))
            return null;
        var tracks = new List<AnimationTrack>(entry.Tracks.Count);
        for (int i = entry.Tracks.Count - 1; i >= 0; i--)
        {
            Track t = entry.Tracks[i];
            if (t.StartTick < 0) t.StartTick = tick;
            if (t.Overflow)
            {
                DroppedLong++;
                entry.Tracks.RemoveAt(i);
                continue;
            }
            t.Built ??= new AnimationTrack
            {
                StartTick = t.StartTick, Loop = t.Loop, Parallel = t.Parallel, Start = t.Start, Steps = t.Steps.ToArray(), Filter = t.Filter,
                FilterStart = t.FilterStart,
            };
            bool shows = false;
            foreach (AnimationStep step in t.Steps) shows |= step.Vars.Mask != AnimatedVar.None || step.Filter.Mask != FilterVar.None;
            long end = t.Loop == 0 ? long.MaxValue : t.StartTick + (long)Math.Ceiling(EntityAnimation.CycleOf(t.Built) * t.Loop * MotionMath.TicksPerSecond / 10.0);
            if (!shows || end <= tick)
            {
                entry.Tracks.RemoveAt(i);
                continue;
            }
            tracks.Insert(0, t.Built);
        }
        if (tracks.Count == 0)
        {
            _current.Remove(id);
            return null;
        }
        var animation = new EntityAnimation { Tracks = tracks.ToArray() };
        long ends = animation.EndTick(MotionMath.TicksPerSecond);
        if (ends != long.MaxValue) _ends.Enqueue(id, ends);
        return animation;
    }

    /// <summary>The ids whose animation may have ended by <paramref name="tick"/> (the replicator reads them: a poll).</summary>
    public void Due(long tick, List<long> into)
    {
        while (_ends.TryPeek(out long id, out long end) && end <= tick)
        {
            _ends.Dequeue();
            into.Add(id);
        }
    }

    /// <summary>Forgets an entity (despawned or deleted) or an id that is no entity (a screen object's chain).</summary>
    public void Forget(long id)
    {
        _current.Remove(id);
        _changed.Remove(id);
    }

    /// <summary>Forgets everything, the open chain's object too (a replicator left with the old world, <see cref="Replicator.ForNewWorld"/>).</summary>
    public void Clear()
    {
        _current.Clear();
        _changed.Clear();
        _ends.Clear();
        _chain = null;
        _chainId = 0;
        _chainSubject = null;
    }

    private static bool TryGet(DmList list, string name, out object value)
    {
        if (!Dm.@in(name, list))
        {
            value = null;
            return false;
        }
        value = list[name];
        return true;
    }

    private static float NumberOr(DmList arguments, string name, float fallback)
        => TryGet(arguments, name, out object value) && TryNumber(value, out float n) ? n : fallback;

    private static float Time(DmList arguments, string name)
    {
        float t = NumberOr(arguments, name, 0);
        return float.IsFinite(t) ? Math.Clamp(t, 0f, Limits.MaxAnimationTime) : 0f;
    }

    private static bool TryNumber(object value, out float n)
    {
        switch (value)
        {
            case float f: n = f; return true;
            case int i: n = i; return true;
            case double d: n = (float)d; return true;
            case long l: n = l; return true;
            default: n = 0; return false;
        }
    }

    private static float Value(float n) => float.IsFinite(n) ? Math.Clamp(n, -Limits.MaxAnimatedValue, Limits.MaxAnimatedValue) : 0f;

    // BYOND colour text as 0xRRGGBBAA; null, empty or a text BYOND cannot read is white (no tint).
    private static uint Rgba(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0xFFFFFFFF;
        try
        {
            DmList parts = Dm.rgb2num(text);
            if (parts.Count < 3) return 0xFFFFFFFF;
            uint r = Byte(parts, 1), g = Byte(parts, 2), b = Byte(parts, 3);
            uint a = parts.Count >= 4 ? Byte(parts, 4) : 255u;
            return (r << 24) | (g << 16) | (b << 8) | a;
        }
        catch (DmRuntimeException)
        {
            return 0xFFFFFFFF;
        }
    }

    private static uint Byte(DmList parts, int index)
        => TryNumber(parts[(float)index], out float n) && float.IsFinite(n) ? (uint)Math.Clamp((int)MathF.Round(n), 0, 255) : 0u;
}
