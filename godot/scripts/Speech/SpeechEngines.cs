using SherpaOnnx;
using System.Threading;
using System.Threading.Tasks;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Client.Speech;

/// <summary>
/// D307 (D16): Supertonic 3 through sherpa-onnx on the client. The model loads on first use; lines are synthesized one at a time off the
/// main thread.
/// </summary>
public sealed class TtsEngine : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly OfflineTtsCallbackProgressWithArg Keep = (_, _, _, _) => 1;
    private OfflineTts _tts;
    private bool _disposed;

    public Task<(float[] Samples, int Rate)> Synthesize(string text, string language, int sid, float speed) => Task.Run(async () =>
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _tts ??= Load();
            // D310: Supertonic's own default of 5 flow steps (8 took about half again as long for little gain)
            var generation = new OfflineTtsGenerationConfig { Sid = sid, NumSteps = 5, Speed = speed };
            generation.Extra["lang"] = language;
            OfflineTtsGeneratedAudio audio = _tts.GenerateWithConfig(text, generation, Keep);
            return (audio.Samples, audio.SampleRate);
        }
        finally
        {
            _gate.Release();
        }
    });

    /// <summary>D310: loads the model ahead of the first line (loading is most of the first line's wait).</summary>
    public Task Warm() => Task.Run(async () =>
    {
        await _gate.WaitAsync();
        try
        {
            if (!_disposed)
                _tts ??= Load();
        }
        finally
        {
            _gate.Release();
        }
    });

    // half the cores, 2 to 4: the game keeps the rest
    internal static int Threads => Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    private static OfflineTts Load()
    {
        SpeechModel m = SpeechModel.Tts;
        var config = new OfflineTtsConfig();
        OfflineTtsSupertonicModelConfig s = config.Model.Supertonic;
        s.DurationPredictor = SpeechStore.PathOf(m, "duration_predictor.int8.onnx");
        s.TextEncoder = SpeechStore.PathOf(m, "text_encoder.int8.onnx");
        s.VectorEstimator = SpeechStore.PathOf(m, "vector_estimator.int8.onnx");
        s.Vocoder = SpeechStore.PathOf(m, "vocoder.int8.onnx");
        s.TtsJson = SpeechStore.PathOf(m, "tts.json");
        s.UnicodeIndexer = SpeechStore.PathOf(m, "unicode_indexer.bin");
        s.VoiceStyle = SpeechStore.PathOf(m, "voice.bin");
        config.Model.Supertonic = s;
        config.Model.NumThreads = Threads;
        config.Model.Provider = "cpu";
        return new OfflineTts(config);
    }

    // waits for a line being synthesized
    public void Dispose()
    {
        _gate.Wait();
        _disposed = true;
        _tts?.Dispose();
        _tts = null;
        _gate.Release();
    }
}

/// <summary>D307: Whisper base through sherpa-onnx on the client: a recorded phrase to text, off the main thread.</summary>
public sealed class SttEngine : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private OfflineRecognizer _recognizer;
    private SpokenLanguageIdentification _identifier;
    private bool _disposed;
    private string _language;
    private string _lastPhraseLanguage;

    /// <summary>
    /// The phrase in one of the player's <paramref name="languages"/> (Whisper language codes). D312: with several, Whisper's language
    /// identification hears it and the closest picked one is decoded (SpeechLanguages.Closest), never a language the player did not pick.
    /// </summary>
    public Task<string> Recognize(float[] samples, int rate, IReadOnlyList<string> languages) => Task.Run(async () =>
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string language = languages[0];
            if (languages.Count > 1)
            {
                _identifier ??= LoadIdentifier();
                using OfflineStream heard = _identifier.CreateStream();
                heard.AcceptWaveform(rate, samples);
                language = SpeechLanguages.Closest(_identifier.Compute(heard).Lang, languages, _lastPhraseLanguage);
            }
            _lastPhraseLanguage = language;
            if (_recognizer == null || _language != language)
            {
                _recognizer?.Dispose();
                _recognizer = Load(language);
                _language = language;
            }
            using OfflineStream stream = _recognizer.CreateStream();
            stream.AcceptWaveform(rate, samples);
            _recognizer.Decode(stream);
            return stream.Result.Text.Trim();
        }
        finally
        {
            _gate.Release();
        }
    });

    private static OfflineRecognizer Load(string language)
    {
        SpeechModel m = SpeechModel.Stt;
        var config = new OfflineRecognizerConfig();
        OfflineModelConfig model = config.ModelConfig;
        OfflineWhisperModelConfig whisper = model.Whisper;
        whisper.Encoder = SpeechStore.PathOf(m, "base-encoder.int8.onnx");
        whisper.Decoder = SpeechStore.PathOf(m, "base-decoder.int8.onnx");
        whisper.Language = language;
        whisper.Task = "transcribe";
        model.Whisper = whisper;
        model.Tokens = SpeechStore.PathOf(m, "base-tokens.txt");
        model.NumThreads = TtsEngine.Threads;
        model.Provider = "cpu";
        config.ModelConfig = model;
        return new OfflineRecognizer(config);
    }

    // D312: Whisper's own language identification, from the same model files
    private static SpokenLanguageIdentification LoadIdentifier()
    {
        SpeechModel m = SpeechModel.Stt;
        var config = new SpokenLanguageIdentificationConfig { NumThreads = TtsEngine.Threads, Provider = "cpu" };
        config.Whisper.Encoder = SpeechStore.PathOf(m, "base-encoder.int8.onnx");
        config.Whisper.Decoder = SpeechStore.PathOf(m, "base-decoder.int8.onnx");
        return new SpokenLanguageIdentification(config);
    }

    // waits for a phrase being recognized
    public void Dispose()
    {
        _gate.Wait();
        _disposed = true;
        _recognizer?.Dispose();
        _recognizer = null;
        _identifier?.Dispose();
        _identifier = null;
        _gate.Release();
    }
}
