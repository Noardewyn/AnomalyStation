using System.Linq;
using System.Globalization;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Audio;
using Anomaly.Protocol;

namespace Anomaly.Client.Audio;

/// <summary>
/// The client's sound playback (M4b audio slice 3; protocol.md section 22, apps.md 2.20): the replica's sound events go through
/// the engine-free <see cref="SoundMixer"/> (channels, stealing, decibels, pitch) and its commands run on a pool of 32
/// <see cref="AudioStreamPlayer3D"/> (positional) and 8 <see cref="AudioStreamPlayer"/> (centred).
/// <list type="bullet">
/// <item><description>tg's volume is the whole attenuation (BYOND adds no falloff of its own to tg's sounds: tg sets
/// <c>falloff = max_distance</c> and every listener is inside it): the 3D players have no attenuation (<c>Disabled</c>, no max
/// distance, no distance filter, no doppler); they only pan, from the current Camera3D (the first-person view, DV36).</description></item>
/// <item><description>A positional sound sits at its tile's centre at ear height; a positional voice told it has no tile plays at
/// the listener (just in front of the camera, so that it pans centred).</description></item>
/// <item><description>Two buses made here under Master: <c>Sfx</c> and <c>SfxMuffled</c> (a low-pass). A voice whose max(muffle,
/// occlusion) reaches the threshold goes through the muffled one (DV37, DV39).</description></item>
/// <item><description>D296: a voice with tg's environment (the listener's area's <c>sound_environment</c>) plays through that
/// environment's copy of its bus with a reverb (<see cref="ReverbPresets"/>), all made up front; a hard limiter on Master.</description></item>
/// </list>
/// </summary>
public partial class SoundPlayer : Node
{
    public const string SfxBus = "Sfx";
    public const string MuffledBus = "SfxMuffled";

    /// <summary>The client's own interface sounds (the Esc menu, the lobby): the Settings' interface volume.</summary>
    public const string UiBus = "Ui";
    public const float DefaultCutoffHz = 900f;

    private readonly AudioStreamPlayer3D[] _positional = new AudioStreamPlayer3D[SoundMixer.DefaultPositionalVoices];
    private readonly AudioStreamPlayer[] _centred = new AudioStreamPlayer[SoundMixer.DefaultCentredVoices];
    private readonly int[] _positionalGen = new int[SoundMixer.DefaultPositionalVoices];
    private readonly int[] _centredGen = new int[SoundMixer.DefaultCentredVoices];
    private readonly double[] _positionalAt = new double[SoundMixer.DefaultPositionalVoices];
    private readonly double[] _centredAt = new double[SoundMixer.DefaultCentredVoices];
    private ReplicaWorld _replica;
    private Func<Camera3D> _camera;
    private readonly List<AudioEffectLowPassFilter> _lowPasses = new();
    private readonly Dictionary<byte, List<AudioEffectReverb>> _reverbs = new();
    private float _masterVolume = 1f;
    private bool _muted;
    private float _cutoffHz = DefaultCutoffHz;
    private bool _reverb = true;
    private float _wetScale = ReverbPresets.DefaultWetScale;

    public SoundLibrary Library { get; private set; }

    public SoundMixer Mixer { get; private set; }

    /// <summary>Plays whose file could not be loaded.</summary>
    public long Missing => Mixer?.MissingPlays ?? 0;

    /// <summary>Voices that started playing a loaded file.</summary>
    public long VoicesStarted { get; private set; }

    /// <summary>0 to 1, the Master bus.</summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            ApplyMaster();
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            ApplyMaster();
        }
    }

    /// <summary>k of DV39 (volume x (1 - k x occlusion)); the playing voices follow at once.</summary>
    public float OcclusionFactor
    {
        get => Mixer.OcclusionFactor;
        set
        {
            if (Mixer.OcclusionFactor == value) return;
            Mixer.OcclusionFactor = value;
            Run(Mixer.Refresh());
        }
    }

    /// <summary>max(muffle, occlusion) from which a voice goes through the low-pass.</summary>
    public float MuffleThreshold
    {
        get => Mixer.MuffleThreshold;
        set
        {
            if (Mixer.MuffleThreshold == value) return;
            Mixer.MuffleThreshold = value;
            Run(Mixer.Refresh());
        }
    }

    /// <summary>The low-pass cutoff of the muffled bus.</summary>
    public float CutoffHz
    {
        get => _cutoffHz;
        set
        {
            _cutoffHz = Math.Clamp(value, 100f, 20000f);
            foreach (AudioEffectLowPassFilter lowPass in _lowPasses)
                lowPass.CutoffHz = _cutoffHz;
        }
    }

    /// <summary>D296: the area reverbs on; off plays every voice dry.</summary>
    public bool Reverb
    {
        get => _reverb;
        set
        {
            if (_reverb == value) return;
            _reverb = value;
            Run(Mixer.Refresh());
        }
    }

    /// <summary>D296: the presets' wet levels are multiplied by this (<see cref="ReverbPresets.DefaultWetScale"/>).</summary>
    public float ReverbWetScale
    {
        get => _wetScale;
        set
        {
            _wetScale = Math.Clamp(value, 0f, 2f);
            foreach ((byte environment, List<AudioEffectReverb> effects) in _reverbs)
                foreach (AudioEffectReverb effect in effects)
                    effect.Wet = ReverbPresets.For(environment, _wetScale)?.Wet ?? 0f;
        }
    }

    /// <summary>The Settings' volumes (DV226): every game sound, each kind of sound and the interface's own sounds, 0 to 1.</summary>
    public void SetVolumes(float master, float effects, float ambience, float buzz, float music, float ui)
    {
        Mixer.MasterGain = Math.Clamp(master, 0f, 1f);
        Mixer.SetGain(SoundCategory.Effects, effects);
        Mixer.SetGain(SoundCategory.Ambience, ambience);
        Mixer.SetGain(SoundCategory.Buzz, buzz);
        Mixer.SetGain(SoundCategory.Music, music);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(UiBus), SoundMixer.LinearToDb(Mixer.MasterGain * Math.Clamp(ui, 0f, 1f)));
        Run(Mixer.Refresh());
    }

    /// <summary>Builds the library, the mixer, the buses and the players; call once, before it enters the tree's process loop.</summary>
    public void Bind(ReplicaWorld replica, Func<Camera3D> camera)
    {
        _replica = replica;
        _camera = camera;
        Library = new SoundLibrary();
        Mixer = new SoundMixer(_positional.Length, _centred.Length, Library.Facts);
        // The mixer's thread is kept out while the buses are made: the engine's add_bus and set_bus_send take no lock of their own, and a mixer
        // that walked the list as a bus was added to it stopped the client at its start with "Index p_index = 3 is out of bounds (size() = 0)",
        // the more readily the busier the machine (the owner, 2026-11-18).
        AudioServer.Lock();
        try
        {
            BuildBuses();
        }
        finally
        {
            AudioServer.Unlock();
        }
        // DEVIATION(DV36): the voices pan from the first-person camera, not by the map's axes as BYOND does; tg's volume is the
        // whole attenuation (no falloff, filter or doppler of Godot's).
        for (int i = 0; i < _positional.Length; i++)
        {
            var p = new AudioStreamPlayer3D
            {
                Name = "Voice3D" + i,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
                MaxDistance = 0f, // 0: no limit
                AttenuationFilterDb = 0f, // no distance low-pass
                DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled,
                EmissionAngleEnabled = false,
                PanningStrength = 1f,
                Bus = SfxBus,
            };
            AddChild(p);
            _positional[i] = p;
        }
        for (int i = 0; i < _centred.Length; i++)
        {
            var p = new AudioStreamPlayer { Name = "Voice" + i, Bus = SfxBus };
            AddChild(p);
            _centred[i] = p;
        }
        GD.Print(string.Create(CultureInfo.InvariantCulture,
            $"[anomaly] audio: {_positional.Length} positional and {_centred.Length} centred voices, driver {AudioServer.GetDriverName()}, index {Library.IndexCount} files at {Library.Root}"));
    }

    /// <summary>Starts playing the replica's sounds; call after <see cref="SetVolumes"/>: subscribing plays the join tick's kept sounds at once (the lobby music).</summary>
    public void Listen() => _replica.SoundsReceived += OnSounds;

    public override void _ExitTree()
    {
        if (_replica != null)
            _replica.SoundsReceived -= OnSounds;
    }

    /// <summary>The smoke line (the headless Dummy driver mixes nothing: the check is the bookkeeping).</summary>
    public string SmokeLine() => string.Create(CultureInfo.InvariantCulture,
        $"[anomaly] audio: events {Mixer?.Events ?? 0} voices {VoicesStarted} missing {Missing} (active {Mixer?.ActiveVoices ?? 0}, stolen {Mixer?.Stolen ?? 0}, expired {Mixer?.Expired ?? 0}, files {Library?.LoadedFiles ?? 0}, driver {AudioServer.GetDriverName()})");

    private void BuildBuses()
    {
        if (AudioServer.GetBusIndex(SfxBus) < 0)
        {
            AudioServer.AddBus();
            int i = AudioServer.BusCount - 1;
            AudioServer.SetBusName(i, SfxBus);
            AudioServer.SetBusSend(i, "Master");
        }
        if (AudioServer.GetBusIndex(UiBus) < 0)
        {
            AudioServer.AddBus();
            int ui = AudioServer.BusCount - 1;
            AudioServer.SetBusName(ui, UiBus);
            AudioServer.SetBusSend(ui, "Master");
        }
        int muffled = AudioServer.GetBusIndex(MuffledBus);
        if (muffled < 0)
        {
            AudioServer.AddBus();
            muffled = AudioServer.BusCount - 1;
            AudioServer.SetBusName(muffled, MuffledBus);
            AudioServer.SetBusSend(muffled, "Master");
            AudioServer.AddBusEffect(muffled, new AudioEffectLowPassFilter { CutoffHz = _cutoffHz });
        }
        if (AudioServer.GetBusEffectCount(muffled) > 0 && AudioServer.GetBusEffect(muffled, 0) is AudioEffectLowPassFilter lowPass)
        {
            lowPass.CutoffHz = _cutoffHz;
            _lowPasses.Add(lowPass);
        }
        // the environments' buses up front: a bus added while voices play stalls the mixer
        for (byte environment = 1; environment <= ReverbPresets.Count; environment++)
        {
            EnvironmentBus(SfxBus, environment, false);
            EnvironmentBus(MuffledBus, environment, true);
        }
        // a hard limiter on Master: many loud sounds with their reverb tails (a supermatter's zaps) clipped into crackle
        bool limited = false;
        for (int e = 0; e < AudioServer.GetBusEffectCount(0); e++)
            limited |= AudioServer.GetBusEffect(0, e) is AudioEffectHardLimiter;
        if (!limited)
            AudioServer.AddBusEffect(0, new AudioEffectHardLimiter { CeilingDb = -0.3f, PreGainDb = 0f });
        ApplyMaster();
    }

    // The voice's bus: Sfx or SfxMuffled, or their copy with the voice's environment's reverb (D296).
    private string BusOf(VoiceCommand c)
    {
        string bus = c.Muffled ? MuffledBus : SfxBus;
        if (!_reverb || ReverbPresets.For(c.Environment, _wetScale) == null)
            return bus;
        return bus + "Env" + c.Environment;
    }

    private void EnvironmentBus(string bus, byte environment, bool muffled)
    {
        string name = bus + "Env" + environment;
        if (AudioServer.GetBusIndex(name) >= 0 || ReverbPresets.For(environment, _wetScale) is not ReverbSettings r)
            return;
        AudioServer.AddBus();
        int i = AudioServer.BusCount - 1;
        AudioServer.SetBusName(i, name);
        AudioServer.SetBusSend(i, "Master");
        if (muffled)
        {
            var lowPass = new AudioEffectLowPassFilter { CutoffHz = _cutoffHz };
            AudioServer.AddBusEffect(i, lowPass);
            _lowPasses.Add(lowPass);
        }
        var reverb = new AudioEffectReverb
        {
            RoomSize = r.RoomSize,
            Damping = r.Damping,
            Spread = 1f,
            Hipass = 0f,
            Dry = 1f,
            Wet = r.Wet,
            PredelayMsec = r.PredelayMsec,
            PredelayFeedback = 0f,
        };
        AudioServer.AddBusEffect(i, reverb);
        if (!_reverbs.TryGetValue(environment, out List<AudioEffectReverb> effects))
            _reverbs[environment] = effects = new List<AudioEffectReverb>();
        effects.Add(reverb);
    }

    private void ApplyMaster()
    {
        AudioServer.SetBusVolumeDb(0, SoundMixer.LinearToDb(_masterVolume));
        AudioServer.SetBusMute(0, _muted);
    }

    private void OnSounds(IReadOnlyList<SoundEvent> events)
    {
        using var probe = Anomaly.Client.Dev.FrameProbe.Scope("net/OnSounds");
        Run(Mixer.Apply(events, App.NowSeconds, Listener()));
    }

    /// <summary>
    /// A sound of the client's own, heard by the player alone (DV586: the own swing's whoosh): a centred play on any free channel at tg's
    /// <paramref name="volume"/> (0 to 100) with tg's vary (get_rand_frequency, 32000 to 55000), through the mixer as the server's sounds go.
    /// </summary>
    public void PlayOwn(string file, float volume)
    {
        if (Mixer == null)
            return;
        var play = new SoundEvent { Kind = SoundKind.Play, File = file, Volume = (byte)Math.Clamp(volume * 2f, 0f, 200f), Frequency = Random.Shared.Next(32000, 55001) };
        Run(Mixer.Apply(new[] { play }, App.NowSeconds, Listener()));
    }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("SoundPlayer._Process");
        if (Mixer == null) return;
        double now = App.NowSeconds;
        // A one-shot the player finished on its own frees its voice (a frame after its start at least: Play() is not instant).
        foreach (VoiceRef v in Mixer.Active().ToList())
        {
            bool playing = v.Pool == VoicePool.Positional ? _positional[v.Index].Playing : _centred[v.Index].Playing;
            double at = v.Pool == VoicePool.Positional ? _positionalAt[v.Index] : _centredAt[v.Index];
            if (!playing && now - at > 0.2)
                Mixer.Finished(v, Gen(v));
        }
        Run(Mixer.Expire(now));
        // Positional voices with no tile follow the listener.
        Camera3D camera = _camera?.Invoke();
        if (camera == null) return;
        foreach (VoiceRef v in Mixer.Active())
            if (v.Pool == VoicePool.Positional && !Mixer.Voice(v).Positional)
                _positional[v.Index].GlobalPosition = AtListener(camera);
    }

    private ListenerTile? Listener()
    {
        Camera3D camera = _camera?.Invoke();
        if (camera == null) return null;
        Vector3 p = camera.GlobalPosition;
        return new ListenerTile(p.X / TileSpace.TileSize + 0.5f, -p.Z / TileSpace.TileSize + 0.5f,
            (int)Math.Floor(p.Y / TileSpace.LevelHeight) + 1);
    }

    private static Vector3 AtListener(Camera3D camera) =>
        camera.GlobalPosition - camera.GlobalTransform.Basis.Z * 0.05f;

    private static Vector3 TilePosition(int x, int y, int z) =>
        new(TileSpace.WorldX(x), TileSpace.WorldY(z) + TileSpace.EyeHeight, TileSpace.WorldZ(y));

    private int Gen(VoiceRef v) => v.Pool == VoicePool.Positional ? _positionalGen[v.Index] : _centredGen[v.Index];

    private void Run(IReadOnlyList<VoiceCommand> commands)
    {
        // The list is the mixer's own: a missing file below must not call into the mixer while it is read.
        List<VoiceCommand> missing = null;
        foreach (VoiceCommand c in commands)
        {
            switch (c.Op)
            {
                case VoiceOp.Start:
                    AudioStream stream = Library.Get(c.File, c.Repeat);
                    if (stream == null)
                    {
                        StopPlayer(c.Voice);
                        (missing ??= new List<VoiceCommand>()).Add(c);
                        break;
                    }
                    Start(c, stream, c.FromSeconds);
                    break;
                case VoiceOp.Change:
                    Change(c);
                    break;
                case VoiceOp.Stop:
                    if (FadesOut(c.Channel))
                        FadeOut(c.Voice);
                    StopPlayer(c.Voice);
                    break;
                case VoiceOp.Move:
                    AudioStream moving = StreamOf(c.From);
                    float at = PositionOf(c.From);
                    StopPlayer(c.From);
                    if (moving == null)
                    {
                        (missing ??= new List<VoiceCommand>()).Add(c);
                        break;
                    }
                    Start(c, moving, at);
                    VoicesStarted--; // the same sound going on
                    break;
            }
        }
        if (missing != null)
            foreach (VoiceCommand c in missing)
                Mixer.Missing(c.Voice, c.Generation);
    }

    private void Start(VoiceCommand c, AudioStream stream, float from)
    {
        VoicesStarted++;
        double now = App.NowSeconds;
        if (c.Voice.Pool == VoicePool.Positional)
        {
            AudioStreamPlayer3D p = _positional[c.Voice.Index];
            p.Stop();
            p.Stream = stream;
            _positionalGen[c.Voice.Index] = c.Generation;
            _positionalAt[c.Voice.Index] = now;
            Apply3D(p, c);
            p.Play(from);
        }
        else
        {
            AudioStreamPlayer p = _centred[c.Voice.Index];
            if (FadesOut(c.Channel))
                FadeOut(c.Voice); // the channel's last sound, which this one replaces
            p.Stop();
            p.Stream = stream;
            _centredGen[c.Voice.Index] = c.Generation;
            _centredAt[c.Voice.Index] = now;
            p.VolumeDb = c.VolumeDb;
            p.PitchScale = c.Pitch;
            p.Bus = BusOf(c);
            p.Play(from);
            // DEVIATION(DV244): a lobby track fades in (a volume change inside the fade is overridden by it, shortcut).
            if (c.Channel == SoundMixer.ChannelLobby && from == 0f)
            {
                p.VolumeDb = SoundMixer.SilentDb;
                float target = Mathf.DbToLinear(c.VolumeDb);
                int gen = c.Generation;
                CreateTween().TweenMethod(Callable.From<float>(g =>
                {
                    if (_centredGen[c.Voice.Index] == gen) p.VolumeDb = SoundMixer.LinearToDb(g);
                }), 0f, target, LobbyFadeInSeconds);
            }
        }
    }

    private void Change(VoiceCommand c)
    {
        if (c.Voice.Pool == VoicePool.Positional)
            Apply3D(_positional[c.Voice.Index], c);
        else
        {
            AudioStreamPlayer p = _centred[c.Voice.Index];
            p.VolumeDb = c.VolumeDb;
            p.PitchScale = c.Pitch;
            p.Bus = BusOf(c);
        }
    }

    private void Apply3D(AudioStreamPlayer3D p, VoiceCommand c)
    {
        p.VolumeDb = c.VolumeDb;
        p.PitchScale = c.Pitch;
        p.Bus = BusOf(c);
        Camera3D camera = _camera?.Invoke();
        if (c.Positional)
            p.GlobalPosition = TilePosition(c.X, c.Y, c.Z);
        else if (camera != null)
            p.GlobalPosition = AtListener(camera);
    }

    /// <summary>D296 (DV228): how long an ambience track or an area buzz that tg cuts takes to fade out.</summary>
    public const float FadeOutSeconds = 4f; // the owner (2026-10-30): 1.5 s sounded abrupt

    private static bool FadesOut(ushort channel) => channel is SoundMixer.ChannelAmbience or SoundMixer.ChannelBuzz or SoundMixer.ChannelLobby;

    /// <summary>DV244: how long a lobby track takes to fade in.</summary>
    public const float LobbyFadeInSeconds = 3f;

    // DEVIATION(DV228): a cut ambience track or buzz goes on in a player of its own from where it was and fades out, instead of
    // stopping dead (tg replaces or stops the channel's sound at once).
    private void FadeOut(VoiceRef v)
    {
        if (v.Pool != VoicePool.Centred)
            return;
        AudioStreamPlayer from = _centred[v.Index];
        if (!from.Playing || from.Stream == null || from.VolumeDb <= SoundMixer.SilentDb)
            return;
        var fading = new AudioStreamPlayer { Name = "Fading", Stream = from.Stream, Bus = from.Bus, VolumeDb = from.VolumeDb, PitchScale = from.PitchScale };
        AddChild(fading);
        fading.Play(from.GetPlaybackPosition());
        float start = Mathf.DbToLinear(from.VolumeDb);
        Tween tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(g => fading.VolumeDb = SoundMixer.LinearToDb(g)), start, 0f, FadeOutSeconds);
        tween.TweenCallback(Callable.From(fading.QueueFree));
    }

    private void StopPlayer(VoiceRef v)
    {
        if (v.Pool == VoicePool.Positional)
        {
            _positional[v.Index].Stop();
            _positional[v.Index].Stream = null;
        }
        else
        {
            _centred[v.Index].Stop();
            _centred[v.Index].Stream = null;
        }
    }

    private AudioStream StreamOf(VoiceRef v) => v.Pool == VoicePool.Positional ? _positional[v.Index].Stream : _centred[v.Index].Stream;

    private float PositionOf(VoiceRef v) => v.Pool == VoicePool.Positional ? _positional[v.Index].GetPlaybackPosition() : _centred[v.Index].GetPlaybackPosition();
}
