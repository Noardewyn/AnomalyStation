using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Client.Speech;

/// <summary>
/// D319: the chat translator's models (<see cref="Marian"/>), each loaded on first use; lines are translated one at a time off the main
/// thread, sentence by sentence, through each language pair of the route (<see cref="ChatTranslation.Route"/>).
/// </summary>
public sealed class ChatTranslator : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Marian> _models = new();
    private bool _disposed;

    private readonly Dictionary<(string, string), Task<string>> _recent = new();

    /// <summary>
    /// The words in <paramref name="target"/>, or null when they need no translation. Called on the main thread: the chat line, the text
    /// over the head and the voice of one line share one translation.
    /// </summary>
    public Task<string> Translate(string text, string target)
    {
        if (_recent.TryGetValue((text, target), out Task<string> known))
            return known;
        if (_recent.Count >= 64)
            _recent.Clear();
        return _recent[(text, target)] = Run(text, target);
    }

    private Task<string> Run(string text, string target) => Task.Run(async () =>
    {
        IReadOnlyList<string> route = ChatTranslation.Route(text, target);
        if (route.Count == 0)
            return null;
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (string pair in route)
            {
                if (!_models.TryGetValue(pair, out Marian model))
                    _models[pair] = model = new Marian(SpeechStore.FolderOf(ChatTranslation.Models[pair]), TtsEngine.Threads);
                text = string.Join(' ', ChatTranslation.Sentences(text).Select(s => model.Translate(s, ChatTranslation.TargetToken(pair, target))));
            }
            return text;
        }
        finally
        {
            _gate.Release();
        }
    });

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            _disposed = true;
            foreach (Marian model in _models.Values)
                model.Dispose();
            _models.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }
}
