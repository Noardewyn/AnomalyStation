using System.Linq;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Speech;
using Anomaly.Client.Speech;
using Anomaly.Protocol;

namespace Anomaly.Client;

// D307: speech to text (opt-in, Settings > Speech). Holding the push to talk key records the microphone; on release Whisper turns the
// phrase into text, which goes after what is typed in the open chat input (the say window opens when no input is open). The player
// reads, fixes and sends it as any typed line. tg has nothing of this.
public partial class GameScene
{
    private const string MicBus = "Mic";

    private AudioStreamPlayer _mic;
    private AudioEffectCapture _micCapture;
    private readonly List<float> _micSamples = new();
    private bool _micRecording;
    private SttEngine _stt;

    // Before the GUI: the push to talk key starts and stops the recording, and its repeats while held never type into the input it opened.
    // D309 (the owner, 2026-11-03: speech is wanted most while typing): in the chat's or the say window's input the typing key (Alt) does
    // it, a key that types nothing; the game's key (N) types there as any letter.
    private bool PushToTalkInput(InputEventKey key)
    {
        string name = key.PhysicalKeycode.ToString();
        bool game = _options.Bindings.Has(PlayAction.PushToTalk, name), typing = _options.Bindings.Has(PlayAction.PushToTalkTyping, name);
        if (!game && !typing)
            return false;
        if (_micRecording)
        {
            if (!key.Pressed)
                StopMic();
            GetViewport().SetInputAsHandled();
            return true;
        }
        bool chat = SayOpen || ChatTyping && TextFieldTyping;
        if (!key.Pressed || key.Echo || (chat ? !typing : !game || TextFieldTyping) || _console.IsOpen || _optionsPanel.IsOpen || _escMenu.IsOpen)
            return false;
        GetViewport().SetInputAsHandled();
        StartPushToTalk();
        return true;
    }

    // D309: the chat window's microphone button, held
    private void MicButton(bool down)
    {
        if (down && !_micRecording)
            StartPushToTalk();
        else if (!down && _micRecording)
            StopMic();
    }

    private void StartPushToTalk()
    {
        if (!_options.SpeechToText)
        {
            _balloon.Add("Speech to text is off (Settings, Speech)", App.NowSeconds);
            return;
        }
        if (!SpeechStore.IsInstalled(SpeechModel.Stt))
        {
            SpeechDownloads.Ensure(SpeechModel.Stt);
            _balloon.Add(SpeechDownloads.Status(SpeechModel.Stt), App.NowSeconds);
            return;
        }
        StartMic();
    }

    private void StartMic()
    {
        if (_mic == null)
        {
            int bus = AudioServer.GetBusIndex(MicBus);
            if (bus < 0)
            {
                AudioServer.Lock(); // (the mixer's thread is kept out: the engine's add_bus takes no lock of its own, see SoundPlayer.Bind)
                try
                {
                    AudioServer.AddBus();
                    bus = AudioServer.BusCount - 1;
                    AudioServer.SetBusName(bus, MicBus);
                    AudioServer.SetBusMute(bus, true); // captured, never heard
                    AudioServer.AddBusEffect(bus, new AudioEffectCapture { BufferLength = 1f });
                }
                finally
                {
                    AudioServer.Unlock();
                }
            }
            _micCapture = (AudioEffectCapture)AudioServer.GetBusEffect(bus, 0);
            _mic = new AudioStreamPlayer { Name = "Microphone", Stream = new AudioStreamMicrophone(), Bus = MicBus };
            AddChild(_mic);
        }
        _micSamples.Clear();
        _micCapture.ClearBuffer();
        _mic.Play();
        _micRecording = true;
        _balloon.Add("Listening...", App.NowSeconds);
    }

    // Per frame while recording: the captured frames, mixed to mono.
    private void UpdateMic()
    {
        if (!_micRecording || _micCapture == null)
            return;
        int frames = _micCapture.GetFramesAvailable();
        if (frames <= 0)
            return;
        foreach (Vector2 frame in _micCapture.GetBuffer(frames))
            _micSamples.Add((frame.X + frame.Y) * 0.5f);
    }

    private async void StopMic()
    {
        UpdateMic();
        _micRecording = false;
        _mic.Stop();
        int rate = (int)AudioServer.GetMixRate();
        if (_micSamples.Count < rate / 4)
            return; // a tap, not a phrase
        float[] samples = _micSamples.ToArray();
        _balloon.Add("Recognizing...", App.NowSeconds);
        // D312: one of the player's languages, never another; D313: the one chosen for speech to text
        List<string> languages = _options.RecognitionLanguages();
        string text;
        try
        {
            _stt ??= new SttEngine();
            text = await _stt.Recognize(samples, rate, languages);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[anomaly] speech to text: {e.Message}");
            return;
        }
        if (!IsInsideTree() || string.IsNullOrWhiteSpace(text))
            return;
        // the input being typed in: the say window over the chat window
        if (SayOpen)
            _sayWindow.AppendText(text);
        else if (ChatWindowOpen)
            _chatWindow.AppendInput(text);
        else
        {
            _sayWindow.Open(SayChannels.Say);
            _sayWindow.AppendText(text);
        }
    }

    private void TeardownSpeechToText()
    {
        _micRecording = false;
        _stt?.Dispose();
        _stt = null;
    }
}
