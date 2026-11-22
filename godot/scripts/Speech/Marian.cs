using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Client.Speech;

/// <summary>
/// D319: an Opus-MT (Marian) model in its ONNX export through ONNX Runtime (sherpa-onnx's native one): the line split by its
/// source.spm, mapped to ids by vocab.json, encoded once, then decoded greedily one token at a time to the end token.
/// </summary>
// shortcut: greedy decoding with the whole prefix fed each step (no key/value cache, no beam search); upgrade to the merged decoder's
// cache if long lines translate slowly.
public sealed class Marian : IDisposable
{
    private readonly InferenceSession _encoder, _decoder;
    private readonly SentencePiece _source;
    private readonly Dictionary<string, int> _ids;
    private readonly string[] _pieces;
    private readonly int _eos, _pad, _start, _unknown;

    public Marian(string folder, int threads)
    {
        var options = new SessionOptions { IntraOpNumThreads = threads, InterOpNumThreads = 1 };
        // Xenova's exports name the quantized graphs so; others keep the plain names for their int8 ones
        string Graph(string name) => File.Exists(Path.Combine(folder, name + "_quantized.onnx")) ? Path.Combine(folder, name + "_quantized.onnx") : Path.Combine(folder, name + ".onnx");
        _encoder = new InferenceSession(Graph("encoder_model"), options);
        _decoder = new InferenceSession(Graph("decoder_model"), options);
        _source = SentencePiece.Load(File.ReadAllBytes(Path.Combine(folder, "source.spm")));
        _ids = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(Path.Combine(folder, "vocab.json")));
        _pieces = new string[_ids.Values.Max() + 1];
        foreach ((string piece, int id) in _ids)
            _pieces[id] = piece;
        using JsonDocument config = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "config.json")));
        _eos = config.RootElement.GetProperty("eos_token_id").GetInt32();
        _pad = config.RootElement.GetProperty("pad_token_id").GetInt32();
        _start = config.RootElement.GetProperty("decoder_start_token_id").GetInt32();
        _unknown = _ids["<unk>"];
    }

    /// <summary>
    /// <paramref name="text"/> translated; a model with several target languages takes the target's token (<c>&gt;&gt;rus&lt;&lt;</c>) in
    /// front of the text.
    /// </summary>
    public string Translate(string text, string targetToken = null)
    {
        IEnumerable<long> ids = _source.Encode(text).Select(p => (long)(_ids.TryGetValue(p, out int id) ? id : _unknown));
        if (targetToken != null)
            ids = ids.Prepend(_ids[targetToken]);
        long[] source = ids.Append(_eos).ToArray();
        var shape = new[] { 1, source.Length };
        var mask = new DenseTensor<long>(Enumerable.Repeat(1L, source.Length).ToArray(), shape);
        using var encoded = _encoder.Run(new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(source, shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", mask),
        });
        Tensor<float> hidden = encoded.First().AsTensor<float>();
        var output = new List<long> { _start };
        int limit = Math.Min(512, 2 * source.Length + 10);
        while (output.Count < limit)
        {
            using var decoded = _decoder.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(output.ToArray(), new[] { 1, output.Count })),
                NamedOnnxValue.CreateFromTensor("encoder_hidden_states", hidden),
                NamedOnnxValue.CreateFromTensor("encoder_attention_mask", mask),
            });
            var logits = (DenseTensor<float>)decoded.First().AsTensor<float>();
            int vocabulary = logits.Dimensions[2];
            ReadOnlySpan<float> last = logits.Buffer.Span.Slice((output.Count - 1) * vocabulary, vocabulary);
            int next = -1;
            for (int id = 0; id < vocabulary; id++)
            {
                if (id != _pad && (next < 0 || last[id] > last[next])) // the model's bad_words_ids: never the padding token
                    next = id;
            }
            if (next == _eos)
                break;
            output.Add(next);
        }
        return SentencePiece.Decode(output.Skip(1).Where(id => id != _unknown && id < _pieces.Length).Select(id => _pieces[id] ?? ""));
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _decoder.Dispose();
    }
}
