using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Audio;

// M4b audio slice 3 (docs/ai/specs/protocol.md section 22, apps.md 2.20). Engine-free: no Godot API here (it is compiled into
// tests/Anomaly.Server.Tests too). The Godot side (Audio/SoundPlayer.cs) runs the commands on its players.

/// <summary>The two pools of voices: positional (Godot's 3D players, panned from the camera) and centred (plain players).</summary>
public enum VoicePool : byte
{
    Positional = 0,
    Centred = 1,
}

/// <summary>One voice: its pool and its index in the pool.</summary>
public readonly record struct VoiceRef(VoicePool Pool, int Index)
{
    public override string ToString() => (Pool == VoicePool.Positional ? "P" : "C") + Index;
}

/// <summary>What a <see cref="VoiceCommand"/> asks the player to do.</summary>
public enum VoiceOp : byte
{
    /// <summary>Play the file on the voice from <see cref="VoiceCommand.FromSeconds"/> (whatever the voice played stops).</summary>
    Start,

    /// <summary>The playing voice takes the new volume, bus, pitch and position, without restarting.</summary>
    Change,

    /// <summary>The voice stops.</summary>
    Stop,

    /// <summary>The sound on <see cref="VoiceCommand.From"/> goes on on <see cref="VoiceCommand.Voice"/> where it is (a centred voice became positional).</summary>
    Move,
}

/// <summary>What the index says about a file: its sample rate (for a frequency in Hz) and its length (0 when unknown).</summary>
public readonly record struct SoundFileFacts(int SampleRate, float LengthSeconds);

/// <summary>The Settings' kinds of sound (D296, DV226).</summary>
public enum SoundCategory
{
    Effects,
    Ambience,
    Buzz,
    Music,
}

/// <summary>The listener's place in tile units (tg's x east, y north, the level), for the distance in the dev panel's log.</summary>
public readonly record struct ListenerTile(float X, float Y, int Z);

/// <summary>One command for the player, with everything the voice needs.</summary>
public readonly record struct VoiceCommand
{
    public VoiceOp Op { get; init; }
    public VoiceRef Voice { get; init; }

    /// <summary>tg's channel of the voice's sound (a stop's: the sound it stops).</summary>
    public ushort Channel { get; init; }

    /// <summary>For <see cref="VoiceOp.Move"/>: the voice the sound leaves.</summary>
    public VoiceRef From { get; init; }

    /// <summary>The voice's generation (a new one for every start or move): <see cref="SoundMixer.Finished"/> and <see cref="SoundMixer.Missing"/> name it.</summary>
    public int Generation { get; init; }

    public string File { get; init; }
    public bool Repeat { get; init; }
    public float VolumeDb { get; init; }

    /// <summary>On the muffled bus (the low-pass): max(muffle, occlusion) at or above the threshold.</summary>
    public bool Muffled { get; init; }

    public float Pitch { get; init; }

    /// <summary>tg's environment + 1 (0 none): the reverb preset's bus (D296).</summary>
    public byte Environment { get; init; }

    public float FromSeconds { get; init; }
    public bool Positional { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
}

/// <summary>A voice's sound as the mixer keeps it.</summary>
public sealed class VoiceState
{
    public bool Active { get; internal set; }
    public int Generation { get; internal set; }

    /// <summary>tg's channel; 0 for a play on any free channel (no later play replaces it, only a stop of every channel stops it).</summary>
    public ushort Channel { get; internal set; }

    public string File { get; internal set; }
    public bool Repeat { get; internal set; }

    /// <summary>tg's volume, 0 to 100.</summary>
    public float Volume { get; internal set; }

    /// <summary>0 to 1.</summary>
    public float Muffle { get; internal set; }

    /// <summary>0 to 1.</summary>
    public float Occlusion { get; internal set; }

    public bool Mute { get; internal set; }
    public bool Positional { get; internal set; }
    public int X { get; internal set; }
    public int Y { get; internal set; }
    public int Z { get; internal set; }
    public float Pitch { get; internal set; } = 1f;
    public byte Environment { get; internal set; }
    public double StartedAt { get; internal set; }

    /// <summary>When a one-shot of known length is over (its length from the offset at its pitch, plus a grace); infinity for a loop or an unknown length.</summary>
    public double EndsAt { get; internal set; }

    internal long Order;
    internal int LogSerial;

    internal void CopyFrom(VoiceState o)
    {
        Channel = o.Channel;
        File = o.File;
        Repeat = o.Repeat;
        Volume = o.Volume;
        Muffle = o.Muffle;
        Occlusion = o.Occlusion;
        Mute = o.Mute;
        Positional = o.Positional;
        X = o.X;
        Y = o.Y;
        Z = o.Z;
        Pitch = o.Pitch;
        Environment = o.Environment;
        StartedAt = o.StartedAt;
        EndsAt = o.EndsAt;
        Order = o.Order;
        LogSerial = o.LogSerial;
    }
}

/// <summary>One line of the dev panel's log of the last events.</summary>
public sealed class SoundLogEntry
{
    public int Serial { get; internal set; }
    public double At { get; internal set; }
    public SoundKind Kind { get; internal set; }
    public ushort Channel { get; internal set; }
    public string File { get; internal set; }
    public float Volume { get; internal set; }
    public float Muffle { get; internal set; }
    public float Occlusion { get; internal set; }
    public bool Positional { get; internal set; }

    /// <summary>tg's environment + 1 (0 none).</summary>
    public byte Environment { get; internal set; }

    /// <summary>Tiles from the listener to the source (0 for a centred sound; -1 when the listener was not known).</summary>
    public float Distance { get; internal set; }

    /// <summary>What became of it: the voice it took, "stole P3", "no voice", "missing file"...</summary>
    public string Outcome { get; internal set; }
}

/// <summary>
/// The client's sound bookkeeping: tg's sound events (protocol 13) in, voice commands out. BYOND's rules as tg relies on them:
/// a channel plays one sound, a play on a busy channel replaces it (BYOND <c>wait = 0</c>), a stop frees it, a stop on channel 0
/// stops everything, a play on channel 0 takes any free voice (tg's <c>SEND_SOUND</c> with no channel), an update changes a playing
/// sound's volume, muffle, occlusion, mute and position without restarting it, a mute keeps it playing silently (a token out of
/// range). When a pool is full the quietest voice is stolen (the oldest of equals). tg's volume is the whole attenuation:
/// <see cref="VolumeDb"/> is tg's volume, lowered by the occlusion (DV39), nothing for the distance.
/// </summary>
public sealed class SoundMixer
{
    public const int DefaultPositionalVoices = 32;
    public const int DefaultCentredVoices = 8;
    public const float DefaultOcclusionFactor = 0.65f;
    public const float DefaultMuffleThreshold = 0.5f;
    public const int DefaultSampleRate = 44100;

    /// <summary>The volume of a silent voice (a mute, a volume of 0): Godot's players reach silence there.</summary>
    public const float SilentDb = -80f;

    public const float MinPitch = 0.01f;
    public const float MaxPitch = 16f;

    /// <summary>Seconds a one-shot is kept after its computed end (the player's own end usually comes first).</summary>
    public const double EndGrace = 0.25;

    public const int LogSize = 16;

    private readonly VoiceState[][] _voices;
    private readonly Dictionary<ushort, VoiceRef> _channels = new();
    private readonly Func<string, SoundFileFacts?> _facts;
    private readonly List<VoiceCommand> _commands = new();
    private readonly SoundLogEntry[] _log = new SoundLogEntry[LogSize];
    private int _logNext;
    private int _logSerial;
    private long _order;
    private int _generation;

    /// <param name="facts">The index's facts of a file (null when the index does not know it: 44.1 kHz, length unknown).</param>
    public SoundMixer(int positionalVoices = DefaultPositionalVoices, int centredVoices = DefaultCentredVoices, Func<string, SoundFileFacts?> facts = null)
    {
        if (positionalVoices < 1 || centredVoices < 1)
            throw new ArgumentOutOfRangeException(nameof(positionalVoices), "each pool needs a voice");
        _voices = new[] { NewPool(positionalVoices), NewPool(centredVoices) };
        _facts = facts ?? (_ => null);
        for (int i = 0; i < LogSize; i++)
            _log[i] = new SoundLogEntry();
    }

    private static VoiceState[] NewPool(int n)
    {
        var pool = new VoiceState[n];
        for (int i = 0; i < n; i++)
            pool[i] = new VoiceState();
        return pool;
    }

    /// <summary>k of DV39: the volume is tg's x (1 - k x occlusion).</summary>
    public float OcclusionFactor { get; set; } = DefaultOcclusionFactor;

    /// <summary>A voice whose max(muffle, occlusion) is at or above this goes through the low-pass.</summary>
    public float MuffleThreshold { get; set; } = DefaultMuffleThreshold;

    /// <summary>tg's CHANNEL_AMBIENCE and CHANNEL_BUZZ (__DEFINES/sound.dm:8-9): the area's ambience and its buzz.</summary>
    public const ushort ChannelAmbience = 1018, ChannelBuzz = 1017, ChannelLobby = 1024;

    private readonly float[] _gains = { 1f, 1f, 1f, 1f };

    /// <summary>0 to 1, the Settings' volume of a kind of sound (DV226: tg's volume preferences, by channel).</summary>
    public float GainOf(SoundCategory category) => _gains[(int)category];

    public void SetGain(SoundCategory category, float gain) => _gains[(int)category] = Math.Clamp(gain, 0f, 1f);

    /// <summary>0 to 1, the Settings' master volume of every game sound.</summary>
    public float MasterGain { get; set; } = 1f;

    /// <summary>
    /// The Settings' kind of a sound by tg's channel (__DEFINES/sound.dm:2-14): the music channels (the lobby's, the admins', the
    /// jukebox, the boss and antagonist music, the elevator), the area ambience (and the weather), the area buzz; the rest effects.
    /// </summary>
    public static SoundCategory CategoryOf(ushort channel) => channel switch
    {
        1024 or 1023 or 1021 or 1019 or 1016 or 1014 => SoundCategory.Music,
        ChannelAmbience or 1012 => SoundCategory.Ambience,
        ChannelBuzz => SoundCategory.Buzz,
        _ => SoundCategory.Effects,
    };

    public int PoolSize(VoicePool pool) => _voices[(int)pool].Length;

    public VoiceState Voice(VoiceRef v) => _voices[(int)v.Pool][v.Index];

    /// <summary>The voice of a channel, if it plays.</summary>
    public bool TryGetChannel(ushort channel, out VoiceRef voice) => _channels.TryGetValue(channel, out voice);

    public int ActiveVoices
    {
        get
        {
            int n = 0;
            foreach (VoiceState[] pool in _voices)
                foreach (VoiceState v in pool)
                    if (v.Active) n++;
            return n;
        }
    }

    /// <summary>Every active voice.</summary>
    public IEnumerable<VoiceRef> Active()
    {
        for (int p = 0; p < _voices.Length; p++)
            for (int i = 0; i < _voices[p].Length; i++)
                if (_voices[p][i].Active)
                    yield return new VoiceRef((VoicePool)p, i);
    }

    // ---- counters (the smoke line, the dev panel) ----
    public long Events { get; private set; }
    public long Plays { get; private set; }
    public long Started { get; private set; }
    public long Replaced { get; private set; }
    public long Stolen { get; private set; }
    public long Stops { get; private set; }
    public long Updates { get; private set; }
    public long UpdatesWithoutVoice { get; private set; }
    public long Expired { get; private set; }
    public long MissingPlays { get; private set; }

    /// <summary>The last <see cref="LogSize"/> events, newest first.</summary>
    public IEnumerable<SoundLogEntry> Log
    {
        get
        {
            int n = Math.Min(_logSerial, LogSize);
            for (int k = 1; k <= n; k++)
                yield return _log[((_logNext - k) % LogSize + LogSize) % LogSize];
        }
    }

    // ---- the maths ----

    /// <summary>tg's volume (0 to 100) lowered by the occlusion (DV39) as decibels; a mute or nothing left is <see cref="SilentDb"/>.</summary>
    public static float VolumeDb(float volume, float occlusion, float occlusionFactor, bool mute = false) =>
        LinearToDb(mute ? 0f : Linear(volume, occlusion, occlusionFactor));

    /// <summary>tg's volume (0 to 100) x (1 - k x occlusion) as a linear gain 0 to 1.</summary>
    public static float Linear(float volume, float occlusion, float occlusionFactor)
    {
        // DEVIATION(DV39): the occlusion lowers tg's volume; tg hears through walls at full volume.
        float v = Math.Clamp(volume, 0f, 100f) / 100f;
        float o = 1f - Math.Clamp(occlusionFactor, 0f, 1f) * Math.Clamp(occlusion, 0f, 1f);
        return v * o;
    }

    public static float LinearToDb(float linear) =>
        linear <= 0f || float.IsNaN(linear) ? SilentDb : Math.Max(SilentDb, 20f * MathF.Log10(linear));

    /// <summary>
    /// BYOND's frequency as a playback speed (Godot's pitch scale changes speed and pitch together, as BYOND does): 0 is none
    /// (1); a magnitude up to 100 is a multiplier; more is a rate in Hz over the file's own rate (tg's 32000 to 55000 on 44.1 kHz
    /// files). A negative one (BYOND plays backwards) plays forwards here.
    /// </summary>
    // DEVIATION(DV603): no reverse playback; a negative frequency (the circuits' sound emitter played backwards) plays forwards.
    public static float Pitch(float frequency, int sampleRate)
    {
        if (frequency == 0f || !float.IsFinite(frequency))
            return 1f;
        float f = Math.Abs(frequency);
        float p = f <= 100f ? f : f / (sampleRate > 0 ? sampleRate : DefaultSampleRate);
        return Math.Clamp(p, MinPitch, MaxPitch);
    }

    /// <summary>A token's offset (DV38: tg writes deciseconds) in seconds; 0 for none.</summary>
    // DEVIATION(DV38): tg's offset is deciseconds; BYOND would read it as seconds.
    public static float OffsetSeconds(float offset) => offset > 0f && float.IsFinite(offset) ? offset / 10f : 0f;

    // DEVIATION(DV37, DV39): the low-pass for a muffled or occluded sound; tg only changes the volume.
    private bool IsMuffled(VoiceState v) => Math.Max(v.Muffle, v.Occlusion) >= MuffleThreshold;

    // DEVIATION(DV226): the player's volumes scale the channels on the client.
    private float Gain(VoiceState v) => v.Mute ? 0f
        : Linear(v.Volume, v.Occlusion, OcclusionFactor) * Math.Clamp(MasterGain, 0f, 1f) * _gains[(int)CategoryOf(v.Channel)];

    // ---- events in, commands out ----

    /// <summary>
    /// Applies a message's events in tg's order; the commands (valid until the next call) say what the players do. The listener's
    /// tile only feeds the log's distance.
    /// </summary>
    public IReadOnlyList<VoiceCommand> Apply(IReadOnlyList<SoundEvent> events, double now, ListenerTile? listener = null)
    {
        _commands.Clear();
        foreach (SoundEvent e in events)
        {
            Events++;
            switch (e.Kind)
            {
                case SoundKind.Play:
                    Play(e, now, listener);
                    break;
                case SoundKind.Update:
                    Update(e, now, listener);
                    break;
                case SoundKind.Stop:
                    Stop(e, now);
                    break;
            }
        }
        return _commands;
    }

    // Version 78 (silicons SI4): the plays waiting on their channels (BYOND's sound.wait), oldest first.
    private readonly Dictionary<ushort, Queue<(SoundEvent Event, ListenerTile? Listener)>> _waiting = new();

    private bool ChannelBusy(ushort channel, double now)
        => channel != 0 && _channels.TryGetValue(channel, out VoiceRef voice) && Voice(voice).Active && Voice(voice).EndsAt > now;

    private void Play(SoundEvent e, double now, ListenerTile? listener)
    {
        // BYOND's sound.wait: a play waits in its channel's queue while the channel sounds (or other plays wait before it)
        if (e.Wait && e.Channel != 0 && (ChannelBusy(e.Channel, now) || _waiting.TryGetValue(e.Channel, out var ahead) && ahead.Count > 0))
        {
            if (!_waiting.TryGetValue(e.Channel, out var queue))
                _waiting[e.Channel] = queue = new Queue<(SoundEvent, ListenerTile?)>();
            queue.Enqueue((e, listener));
            return;
        }
        Plays++;
        SoundLogEntry log = AddLog(e, now, e.File, listener);
        SoundFileFacts? facts = e.File == null ? null : _facts(e.File);
        int rate = facts?.SampleRate ?? DefaultSampleRate;
        float length = facts?.LengthSeconds ?? 0f;
        float pitch = Pitch(e.Frequency, rate);
        float from = OffsetSeconds(e.Offset);
        VoicePool pool = e.Positional ? VoicePool.Positional : VoicePool.Centred;

        // The channel's sound stops whatever happens to the new one (BYOND replaces it).
        VoiceRef? target = null;
        if (e.Channel != 0 && _channels.TryGetValue(e.Channel, out VoiceRef old))
        {
            Replaced++;
            if (old.Pool == pool)
            {
                target = old;
                _channels.Remove(e.Channel);
            }
            else
            {
                StopVoice(old);
            }
        }

        if (length > 0f && from > 0f)
        {
            if (e.Repeat)
                from %= length;
            else if (from >= length)
            {
                if (target.HasValue) StopVoice(target.Value);
                log.Outcome = "past its end";
                return;
            }
        }

        VoiceRef voice = target ?? Allocate(pool, log);
        VoiceState v = Voice(voice);
        v.Active = true;
        v.Generation = ++_generation;
        v.Channel = e.Channel;
        v.File = e.File;
        v.Repeat = e.Repeat;
        v.Volume = e.VolumeValue;
        v.Muffle = e.Muffle / 255f;
        v.Occlusion = e.Occlusion / 255f;
        v.Mute = e.Mute;
        v.Positional = e.Positional;
        v.X = e.X;
        v.Y = e.Y;
        v.Z = e.Z;
        v.Pitch = pitch;
        v.Environment = e.Environment;
        v.StartedAt = now;
        v.EndsAt = e.Repeat || length <= 0f ? double.PositiveInfinity : now + (length - from) / pitch + EndGrace;
        v.Order = ++_order;
        v.LogSerial = log.Serial;
        if (e.Channel != 0)
            _channels[e.Channel] = voice;
        Started++;
        log.Outcome ??= "voice " + voice;
        _commands.Add(Command(VoiceOp.Start, voice, v) with { FromSeconds = from });
    }

    private void Update(SoundEvent e, double now, ListenerTile? listener)
    {
        Updates++;
        VoiceRef voice = default;
        bool has = e.Channel != 0 && _channels.TryGetValue(e.Channel, out voice);
        SoundLogEntry log = AddLog(e, now, has ? Voice(voice).File : null, listener);
        if (!has)
        {
            UpdatesWithoutVoice++;
            log.Outcome = "no voice";
            return;
        }
        VoiceState v = Voice(voice);
        v.Volume = e.VolumeValue;
        v.Muffle = e.Muffle / 255f;
        v.Occlusion = e.Occlusion / 255f;
        v.Mute = e.Mute;
        v.Environment = e.Environment;
        if (e.Frequency != 0f)
            v.Pitch = Pitch(e.Frequency, _facts(v.File ?? string.Empty)?.SampleRate ?? DefaultSampleRate);
        v.Positional = e.Positional;
        if (e.Positional)
        {
            v.X = e.X;
            v.Y = e.Y;
            v.Z = e.Z;
        }
        if (e.Positional && voice.Pool == VoicePool.Centred)
        {
            // A centred sound now has a tile: it goes on in the positional pool where it is.
            VoiceRef to = Allocate(VoicePool.Positional, log);
            VoiceState n = Voice(to);
            n.CopyFrom(v);
            n.Active = true;
            n.Generation = ++_generation;
            v.Active = false;
            v.File = null;
            _channels[e.Channel] = to;
            log.Outcome ??= "moved " + voice + " to " + to;
            _commands.Add(Command(VoiceOp.Move, to, n) with { From = voice });
            return;
        }
        // A positional voice told it has no tile stays where it is and plays centred (at the listener).
        log.Outcome = "voice " + voice;
        _commands.Add(Command(VoiceOp.Change, voice, v));
    }

    private void Stop(SoundEvent e, double now)
    {
        Stops++;
        SoundLogEntry log = AddLog(e, now, null, null);
        if (e.Channel == 0)
            _waiting.Clear();
        else
            _waiting.Remove(e.Channel);
        if (e.Channel == 0)
        {
            int n = 0;
            foreach (VoiceRef v in Active().ToList())
            {
                StopVoice(v);
                n++;
            }
            _channels.Clear();
            log.Outcome = "stopped all (" + n + ")";
            return;
        }
        if (_channels.TryGetValue(e.Channel, out VoiceRef voice))
        {
            log.File = Voice(voice).File;
            StopVoice(voice);
            log.Outcome = "stopped " + voice;
        }
        else
            log.Outcome = "nothing playing";
    }

    /// <summary>tg's reserved channels (above CHANNEL_HIGHEST_AVAILABLE 1011: the lobby music, the ambience and buzz, the heartbeat...).</summary>
    public static bool IsReserved(ushort channel) => channel > 1011;

    // A free voice of the pool, else the quietest (the oldest of equals) is stolen; a voice of one of tg's reserved channels
    // only when every voice is one (D296: the quiet ambience and buzz were the first stolen by the own steps).
    private VoiceRef Allocate(VoicePool pool, SoundLogEntry log)
    {
        VoiceState[] voices = _voices[(int)pool];
        int best = -1;
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].Active)
                return new VoiceRef(pool, i);
            if (best < 0)
            {
                best = i;
                continue;
            }
            bool r = IsReserved(voices[i].Channel), rb = IsReserved(voices[best].Channel);
            if (r != rb)
            {
                if (rb)
                    best = i;
                continue;
            }
            float g = Gain(voices[i]), b = Gain(voices[best]);
            if (g < b || (g == b && voices[i].Order < voices[best].Order))
                best = i;
        }
        var stolen = new VoiceRef(pool, best);
        Stolen++;
        StopVoice(stolen);
        log.Outcome = "stole " + stolen;
        return stolen;
    }

    private void StopVoice(VoiceRef voice)
    {
        VoiceState v = Voice(voice);
        if (!v.Active) return;
        Free(voice);
        _commands.Add(new VoiceCommand { Op = VoiceOp.Stop, Voice = voice, Generation = v.Generation, Channel = v.Channel });
    }

    private void Free(VoiceRef voice)
    {
        VoiceState v = Voice(voice);
        if (v.Channel != 0 && _channels.TryGetValue(v.Channel, out VoiceRef mapped) && mapped == voice)
            _channels.Remove(v.Channel);
        v.Active = false;
        v.File = null;
    }

    private VoiceCommand Command(VoiceOp op, VoiceRef voice, VoiceState v) => new()
    {
        Op = op,
        Voice = voice,
        Generation = v.Generation,
        Channel = v.Channel,
        File = v.File,
        Repeat = v.Repeat,
        VolumeDb = v.Mute ? SilentDb : LinearToDb(Gain(v)),
        Muffled = IsMuffled(v),
        Pitch = v.Pitch,
        Environment = v.Environment,
        Positional = v.Positional,
        X = v.X,
        Y = v.Y,
        Z = v.Z,
    };

    /// <summary>The player saw the voice end on its own (a one-shot played out): it is free. False when the voice already moved on.</summary>
    public bool Finished(VoiceRef voice, int generation)
    {
        VoiceState v = Voice(voice);
        if (!v.Active || v.Generation != generation) return false;
        Free(voice);
        return true;
    }

    /// <summary>The player could not load the voice's file: the voice is free and the play counts as missing.</summary>
    public void Missing(VoiceRef voice, int generation)
    {
        VoiceState v = Voice(voice);
        if (!v.Active || v.Generation != generation) return;
        MissingPlays++;
        foreach (SoundLogEntry entry in _log)
            if (entry.Serial == v.LogSerial)
                entry.Outcome = "missing file";
        Free(voice);
    }

    /// <summary>One-shots past their end (by the index's length) stop; the commands are valid until the next call.</summary>
    public IReadOnlyList<VoiceCommand> Expire(double now)
    {
        _commands.Clear();
        for (int p = 0; p < _voices.Length; p++)
            for (int i = 0; i < _voices[p].Length; i++)
                if (_voices[p][i].Active && _voices[p][i].EndsAt <= now)
                {
                    Expired++;
                    StopVoice(new VoiceRef((VoicePool)p, i));
                }
        // version 78: a waiting play starts once its channel is free
        if (_waiting.Count > 0)
            foreach (ushort channel in _waiting.Keys.ToList())
            {
                Queue<(SoundEvent Event, ListenerTile? Listener)> queue = _waiting[channel];
                if (!ChannelBusy(channel, now) && queue.Count > 0)
                {
                    (SoundEvent next, ListenerTile? listener) = queue.Dequeue();
                    if (queue.Count == 0)
                        _waiting.Remove(channel);
                    Play(next with { Wait = false }, now, listener);
                }
            }
        return _commands;
    }

    /// <summary>A change of every active voice (after <see cref="OcclusionFactor"/>, <see cref="MuffleThreshold"/> or a channel gain changed).</summary>
    public IReadOnlyList<VoiceCommand> Refresh()
    {
        _commands.Clear();
        foreach (VoiceRef voice in Active())
            _commands.Add(Command(VoiceOp.Change, voice, Voice(voice)));
        return _commands;
    }

    private SoundLogEntry AddLog(SoundEvent e, double now, string file, ListenerTile? listener)
    {
        SoundLogEntry entry = _log[_logNext];
        _logNext = (_logNext + 1) % LogSize;
        entry.Serial = ++_logSerial;
        entry.At = now;
        entry.Kind = e.Kind;
        entry.Channel = e.Channel;
        entry.File = file;
        entry.Volume = e.Kind == SoundKind.Stop ? 0f : e.VolumeValue;
        entry.Muffle = e.Muffle / 255f;
        entry.Occlusion = e.Occlusion / 255f;
        entry.Positional = e.Positional;
        entry.Environment = e.Kind == SoundKind.Stop ? (byte)0 : e.Environment;
        entry.Distance = !e.Positional ? 0f : listener is { } l
            ? MathF.Sqrt((e.X - l.X) * (e.X - l.X) + (e.Y - l.Y) * (e.Y - l.Y) + (e.Z - l.Z) * (e.Z - l.Z))
            : -1f;
        entry.Outcome = null;
        return entry;
    }
}
