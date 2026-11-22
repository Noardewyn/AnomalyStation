using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Anomaly.Client.Shared.Speech;

/// <summary>
/// D319: a SentencePiece unigram model (an Opus-MT model's source.spm) read from its protobuf: the pieces and their scores, and a line
/// split into the pieces of the best total score (Viterbi), as SentencePiece's encoder does.
/// </summary>
// shortcut: NFKC and collapsed spaces stand in for the model's precompiled nmt_nfkc character map; upgrade to reading the map if odd
// characters (full-width forms, control characters) tokenize badly.
public sealed class SentencePiece
{
    public const char Space = '▁';

    private readonly Dictionary<string, float> _scores = new(StringComparer.Ordinal);
    private readonly int _longest;
    private readonly float _unknownScore;

    public SentencePiece(IEnumerable<(string Piece, float Score)> pieces)
    {
        foreach ((string piece, float score) in pieces)
        {
            _scores[piece] = score;
            _longest = Math.Max(_longest, piece.Length);
        }
        _unknownScore = (_scores.Count > 0 ? _scores.Values.Min() : 0f) - 10f; // SentencePiece's kUnkPenalty under the lowest piece
    }

    /// <summary>Reads a ModelProto: its repeated field 1 (piece: 1 the text, 2 the score, 3 the type); only normal and user pieces split text.</summary>
    public static SentencePiece Load(byte[] proto)
    {
        var pieces = new List<(string, float)>();
        int at = 0;
        while (at < proto.Length)
        {
            ulong key = Varint(proto, ref at);
            if (key >> 3 == 1 && (key & 7) == 2)
            {
                int length = (int)Varint(proto, ref at);
                int end = at + length;
                string text = null;
                float score = 0;
                ulong type = 1;
                while (at < end)
                {
                    ulong field = Varint(proto, ref at);
                    switch (field)
                    {
                        case (1 << 3) | 2:
                            int n = (int)Varint(proto, ref at);
                            text = Encoding.UTF8.GetString(proto, at, n);
                            at += n;
                            break;
                        case (2 << 3) | 5:
                            score = BitConverter.ToSingle(proto, at);
                            at += 4;
                            break;
                        case (3 << 3) | 0:
                            type = Varint(proto, ref at);
                            break;
                        default:
                            Skip(proto, ref at, field);
                            break;
                    }
                }
                if (text != null && type is 1 or 4)
                    pieces.Add((text, score));
            }
            else
                Skip(proto, ref at, key);
        }
        return new SentencePiece(pieces);
    }

    /// <summary>The pieces of <paramref name="text"/>: normalized, a ▁ for each space and one in front; a stretch no piece covers is one unknown piece.</summary>
    public List<string> Encode(string text)
    {
        string line = Space + string.Join(Space, text.Normalize(NormalizationForm.FormKC).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        int n = line.Length;
        var best = new float[n + 1];
        var from = new int[n + 1];
        Array.Fill(best, float.NegativeInfinity);
        best[0] = 0;
        for (int end = 1; end <= n; end++)
        {
            for (int start = Math.Max(0, end - _longest); start < end; start++)
            {
                if (float.IsNegativeInfinity(best[start]) || !_scores.TryGetValue(line.Substring(start, end - start), out float score))
                    continue;
                if (best[start] + score > best[end])
                {
                    best[end] = best[start] + score;
                    from[end] = start;
                }
            }
            if (float.IsNegativeInfinity(best[end]) && best[end - 1] + _unknownScore > best[end])
            {
                best[end] = best[end - 1] + _unknownScore;
                from[end] = end - 1;
            }
        }
        var pieces = new List<string>();
        for (int end = n; end > 0; end = from[end])
            pieces.Add(line.Substring(from[end], end - from[end]));
        pieces.Reverse();
        return pieces;
    }

    /// <summary>The text of decoded pieces: joined, each ▁ a space, the leading one dropped.</summary>
    public static string Decode(IEnumerable<string> pieces) => string.Concat(pieces).Replace(Space, ' ').Trim();

    private static ulong Varint(byte[] data, ref int at)
    {
        ulong value = 0;
        for (int shift = 0; ; shift += 7)
        {
            byte b = data[at++];
            value |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80)
                return value;
        }
    }

    private static void Skip(byte[] data, ref int at, ulong key)
    {
        switch (key & 7)
        {
            case 0: Varint(data, ref at); break;
            case 1: at += 8; break;
            case 2:
                int length = (int)Varint(data, ref at); // read before adding: `at += Varint(ref at)` would add to the old position
                at += length;
                break;
            case 5: at += 4; break;
            default: throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"wire type {key & 7}"));
        }
    }
}
