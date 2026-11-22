using System.Linq;
using System.Threading.Tasks;
using Godot;
using Anomaly.Client.Audio;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Audio;
using Anomaly.Client.Shared.Speech;
using Anomaly.Client.Speech;
using Anomaly.Protocol;

namespace Anomaly.Client;

// D307 (D16, DV262): text to speech on the client (opt-in, Settings > Speech). The server sends each line this player hears as text with
// the speaker's voice and pitch (protocol version 63); the client picks the line's language out of the player's, synthesizes it off the
// main thread and plays it from the speaker, a speaker's lines one after another as tg's SStts queues them. tg's volume falls off with
// the distance as its playsound_local does; the voices' own volume and the master volume are the player's.
public partial class GameScene
{
    private TtsEngine _tts;
    private readonly Dictionary<long, SpeakerVoice> _voices = new();

    // A speaker's player and its lines waiting, in the order they were said.
    private sealed class SpeakerVoice
    {
        public Node Player;
        public readonly Queue<Task<(AudioStreamWav Stream, bool Radio, int Volume, float Range)>> Lines = new();
    }

    private void BuildTextToSpeech()
    {
        _feed.TtsReceived += OnTts;
        _optionsPanel.VoiceTested += TestVoice;
        _optionsPanel.ModelDeleteRequested += DeleteSpeechModel;
    }

    private void TeardownTextToSpeech()
    {
        if (_feed != null)
            _feed.TtsReceived -= OnTts;
        _tts?.Dispose();
        _tts = null;
    }

    private void OnTts(TtsMessage message)
    {
        // D311: the character's voice, for Settings' Test: its own lines and Character Setup's previews (speaker 0) carry it
        if (message.Speaker == 0 || message.Speaker == _feed.OwnEntity)
            (_ownVoice, _ownPitch) = (message.Voice, message.Pitch);
        if (_options?.TextToSpeech != true)
            return;
        if (!SpeechStore.IsInstalled(SpeechModel.Tts))
        {
            SpeechDownloads.Ensure(SpeechModel.Tts); // the option is on and the files went missing: fetch them again
            return;
        }
        bool radio = (message.Flags & TtsFlags.Radio) != 0;
        // D308: tg's radio preferences (can_hear_radio_tts, the hear-self-radio check of play_tts), the player's own here; D312: not on
        // Character Setup's radio preview (speaker 0)
        if (radio && message.Speaker != 0 && (_options.RadioSpeech == RadioSpeech.Off || _options.RadioSpeech == RadioSpeech.Departmental && (message.Flags & TtsFlags.Common) != 0
            || (message.Flags & TtsFlags.Own) != 0 && !_options.HearOwnRadio))
            return;
        // D319: another speaker's line the chat translator takes is voiced translated, alone
        if (message.Speaker != 0 && message.Speaker != _feed.OwnEntity && (message.Flags & TtsFlags.Own) == 0 && WillTranslate(message.Text))
        {
            SpeakTranslated(message);
            return;
        }
        string language = SpeechLanguages.Detect(message.Text, _options.SpeechLanguageCodes);
        if (language == null)
            return; // a language the player did not pick
        Speak(message.Speaker, message.Text, language, message.Voice, message.Pitch, message.Filter, message.Flags, message.Volume);
    }

    // A line into its speaker's queue: synthesized slower by the pitch factor and at the filter's tempo, then read faster by the pitch (the
    // pitch moves and the length stays: tg's rubberband), then tg's filters, the silicon reverbs, the walkie-talkie's band (D309) and the
    // radio's clicks (tts-api.py). D312: a whisper loses its pitch, a shout is said higher and strained.
    private void Speak(long speaker, string text, string language, string voiceName, int pitchSemitones, string filter, TtsFlags flags, int volume)
    {
        if (!_voices.TryGetValue(speaker, out SpeakerVoice voice))
            _voices[speaker] = voice = new SpeakerVoice();
        _tts ??= new TtsEngine();
        VoicePlan plan = VoicePlan.Parse(filter);
        bool whisper = (flags & TtsFlags.Whisper) != 0;
        bool yell = (flags & TtsFlags.Yell) != 0;
        if (yell)
        {
            plan.Pitch *= VoiceEffects.YellPitch;
            plan.Tempo *= VoiceEffects.YellTempo;
        }
        float range = SpeechVoices.RangeOf(flags);
        float pitch = SpeechVoices.PitchScale(pitchSemitones) * plan.Pitch;
        bool radio = (flags & TtsFlags.Radio) != 0;
        bool silicon = (flags & TtsFlags.Silicon) != 0;
        bool clicks = radio || (flags & TtsFlags.Clicks) != 0;
        voice.Lines.Enqueue(_tts.Synthesize(text, language, SpeechVoices.Sid(voiceName), plan.Tempo / pitch)
            .ContinueWith(t =>
            {
                int rate = t.Result.Rate;
                float[] samples = VoiceEffects.Apply(VoiceEffects.Resample(t.Result.Samples, pitch), rate, plan);
                if (whisper)
                    samples = VoiceEffects.Whisper(samples, rate, Random.Shared);
                if (yell)
                    samples = VoiceEffects.Apply(samples, rate, VoicePlan.Parse(VoiceEffects.YellChain));
                if (silicon && SpeechSounds.Get("SynthImpulse", rate) is float[] synth && SpeechSounds.Get("RoomImpulse", rate) is float[] room)
                    samples = VoiceEffects.Silicon(samples, rate, synth, room);
                if (radio)
                    samples = VoiceEffects.Radio(samples, rate, Random.Shared);
                if (clicks)
                    samples = VoiceEffects.WithClicks(samples, SpeechSounds.Get(Random.Shared.Next(2) == 0 ? "on1" : "on2", rate),
                        SpeechSounds.Get("off" + Random.Shared.Next(1, 5), rate));
                return (ToStream(samples, rate), radio, volume, range);
            }, TaskContinuationOptions.OnlyOnRanToCompletion));
    }

    // D309: Settings' Test, unplaced (speaker 0, as tg's preview), each press in the next of the player's languages that has a sample line;
    // D311: in the character's voice once the client has heard it, else in the first voice.
    private static readonly Dictionary<string, string> TestLines = new()
    {
        ["en"] = "Hello, this is my voice.", // tg's tts_play_voice
        ["ru"] = "Привет, это мой голос.",
    };

    private int _voiceTests;
    private string _ownVoice;
    private int _ownPitch;

    private void TestVoice(bool radio)
    {
        if (!_options.TextToSpeech)
        {
            _balloon.Add("Text to speech is off (Settings, Speech)", App.NowSeconds);
            return;
        }
        if (!SpeechStore.IsInstalled(SpeechModel.Tts))
        {
            SpeechDownloads.Ensure(SpeechModel.Tts);
            _balloon.Add(SpeechDownloads.Status(SpeechModel.Tts), App.NowSeconds);
            return;
        }
        List<string> languages = _options.SpeechLanguageCodes.Where(TestLines.ContainsKey).DefaultIfEmpty("en").ToList();
        string language = languages[_voiceTests++ % languages.Count];
        Speak(0, TestLines[language], language, _ownVoice ?? SpeechVoices.Names[0], _ownPitch, null, radio ? TtsFlags.Radio : TtsFlags.None, 100);
    }

    // D310: the model loads once the option is on and its files are there, before the first line needs it (checked once a second)
    private double _warmCheckedAt;

    private void WarmTextToSpeech()
    {
        if (_tts != null || !_options.TextToSpeech || App.NowSeconds - _warmCheckedAt < 1)
            return;
        _warmCheckedAt = App.NowSeconds;
        if (!SpeechStore.IsInstalled(SpeechModel.Tts))
            return;
        _tts = new TtsEngine();
        _ = _tts.Warm();
    }

    // D310: Settings' delete: the engine lets the files go first (Windows keeps open files)
    private void DeleteSpeechModel(SpeechModel model)
    {
        if (model == SpeechModel.Tts)
        {
            _tts?.Dispose();
            _tts = null;
        }
        else if (model == SpeechModel.Stt)
        {
            _stt?.Dispose();
            _stt = null;
        }
        else
            TeardownChatTranslation(); // D319: a translation model
        _balloon.Add(SpeechDownloads.Delete(model) ? $"{model.Title} files deleted" : $"{model.Title} files not deleted", App.NowSeconds);
    }

    // 16-bit PCM for Godot.
    private static AudioStreamWav ToStream(float[] samples, int rate)
    {
        var data = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)Math.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
            data[2 * i] = (byte)s;
            data[2 * i + 1] = (byte)(s >> 8);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
    }

    // Per frame: each speaker's voice follows its speaker; a finished line gives way to the next ready one.
    private void UpdateTextToSpeech()
    {
        WarmTextToSpeech();
        if (_voices.Count == 0)
            return;
        Camera3D camera = _rig.Camera;
        List<long> done = null;
        foreach ((long speaker, SpeakerVoice voice) in _voices)
        {
            bool playing = voice.Player switch
            {
                AudioStreamPlayer3D p => p.Playing,
                AudioStreamPlayer p => p.Playing,
                _ => false,
            };
            Vector3 at = default;
            bool placed = speaker != 0 && _entities.TryGetChatPoint(speaker, out at, out _);
            if (!playing && voice.Lines.TryPeek(out var next))
            {
                if (next.IsFaulted || next.IsCanceled)
                    voice.Lines.Dequeue();
                else if (next.IsCompleted)
                {
                    voice.Lines.Dequeue();
                    (AudioStreamWav stream, bool radio, int volume, float range) = next.Result;
                    GD.Print($"[anomaly] speech: voicing #{speaker}, {stream.GetLength():0.0} s");
                    voice.Player ??= NewVoicePlayer(speaker != 0);
                    if (voice.Player is AudioStreamPlayer3D p3)
                    {
                        p3.Stream = stream;
                        p3.SetMeta("volume", volume);
                        p3.SetMeta("radio", radio);
                        p3.SetMeta("range", range);
                        p3.Play();
                    }
                    else if (voice.Player is AudioStreamPlayer p2)
                    {
                        p2.Stream = stream;
                        p2.VolumeDb = SoundMixer.LinearToDb(Gain(radio) * volume / 100f);
                        p2.Play();
                    }
                    playing = true;
                }
            }
            if (voice.Player is AudioStreamPlayer3D player)
            {
                if (placed)
                    player.GlobalPosition = at;
                float tiles = camera == null ? 0f : camera.GlobalPosition.DistanceTo(player.GlobalPosition) / TileSpace.TileSize;
                int volume = player.HasMeta("volume") ? player.GetMeta("volume").AsInt32() : 85;
                bool radioLine = player.HasMeta("radio") && player.GetMeta("radio").AsBool();
                float range = player.HasMeta("range") ? player.GetMeta("range").AsSingle() : SpeechVoices.SoundRange;
                player.VolumeDb = SoundMixer.LinearToDb(Gain(radioLine) * SpeechVoices.Gain(volume, tiles, range));
            }
            if (!playing && voice.Lines.Count == 0)
                (done ??= new List<long>()).Add(speaker);
        }
        if (done == null)
            return;
        foreach (long speaker in done)
        {
            _voices[speaker].Player?.QueueFree();
            _voices.Remove(speaker);
        }
    }

    // The master volume times the voices' or the radio voices' own (tg's sound_tts_volume and sound_tts_radio_volume).
    private float Gain(bool radio) => _options.MasterVolumePercent / 100f * (radio ? _options.RadioSpeechVolumePercent : _options.SpeechVolumePercent) / 100f;

    private Node NewVoicePlayer(bool placed)
    {
        Node player = placed
            ? new AudioStreamPlayer3D
            {
                Name = "Speech",
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, // tg's falloff, as the world's sounds (DV36)
                MaxDistance = 0f,
                AttenuationFilterDb = 0f,
                DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled,
                Bus = SoundPlayer.SfxBus,
            }
            : new AudioStreamPlayer { Name = "Speech", Bus = SoundPlayer.SfxBus };
        AddChild(player);
        return player;
    }
}
