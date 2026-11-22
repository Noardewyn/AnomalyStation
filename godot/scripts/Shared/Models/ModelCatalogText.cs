using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Anomaly.Client.Shared.Models;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// Edits one entry's <c>"scale"</c> and <c>"offset"</c> in the text of models.json in place (the dev menu's Tools tab saves with it, the owner,
/// 2026-11-06): the file is hand-written JSON with comments, so only those two properties' text changes and every comment and line stays.
/// </summary>
public static class ModelCatalogText
{
    /// <summary>
    /// The text with entry <paramref name="id"/>'s scale and offset set: a property it has is rewritten unless its numbers are already these; one it lacks is
    /// added on a line under the id's unless the value is the default (a scale of 1, a zero offset).
    /// </summary>
    public static string SetPlacement(string text, string id, float scale, Vector3 offset)
    {
        text = Set(text, id, "offset", new[] { offset.X, offset.Y, offset.Z }, offset == Vector3.Zero);
        return Set(text, id, "scale", new[] { scale }, scale == 1f);
    }

    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private readonly record struct Token(char Kind, int Start, int End); // Kind: one of {}[],: or 's' a string, 'v' another literal

    private static string Set(string text, string id, string key, float[] numbers, bool isDefault)
    {
        string value = numbers.Length == 1 ? F(numbers[0]) : "[ " + string.Join(", ", numbers.Select(F)) + " ]";
        List<Token> tokens = Tokens(text);
        int open = EntryOpen(text, tokens, id);
        Token? idComma = null;
        int depth = 0;
        for (int i = open + 1; i < tokens.Count; i++)
        {
            Token t = tokens[i];
            if (t.Kind is '{' or '[')
                depth++;
            else if (t.Kind is '}' or ']')
            {
                if (depth-- == 0)
                    break; // the entry's own close
            }
            else if (depth == 0 && t.Kind == 's' && i + 2 < tokens.Count && tokens[i + 1].Kind == ':')
            {
                // the value runs to the next comma or close at this depth
                int j = i + 2, d = 0;
                for (; j < tokens.Count; j++)
                {
                    char k = tokens[j].Kind;
                    if (k is '{' or '[') d++;
                    else if (k is '}' or ']') { if (d-- == 0) break; }
                    else if (k == ',' && d == 0) break;
                }
                string name = text[(t.Start + 1)..(t.End - 1)];
                if (name == "id" && tokens[j].Kind == ',')
                    idComma = tokens[j];
                if (name == key)
                {
                    float[] had = tokens.Skip(i + 2).Take(j - i - 2).Where(v => v.Kind == 'v')
                        .Select(v => float.TryParse(text[v.Start..v.End], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN).ToArray();
                    return had.SequenceEqual(numbers) ? text : text[..tokens[i + 2].Start] + value + text[tokens[j - 1].End..];
                }
                i = j - 1;
            }
        }
        if (isDefault)
            return text;
        if (idComma is not Token c)
            throw new InvalidDataException($"model {id}: its id is its last property, nowhere to put {key}");
        // on a line of its own under the id's, indented as it is
        int idLine = text.LastIndexOf('\n', c.Start) + 1;
        string indent = new(text.Skip(idLine).TakeWhile(ch => ch is ' ' or '\t').ToArray());
        int at = text.IndexOf('\n', c.End);
        if (at < 0)
            return text[..c.End] + " \"" + key + "\": " + value + "," + text[c.End..];
        string newline = text[at - 1] == '\r' ? "\r\n" : "\n";
        return text[..(at + 1)] + indent + "\"" + key + "\": " + value + "," + newline + text[(at + 1)..];
    }

    // The '{' of the entry whose "id" is id: an object in the "models" array (the third level).
    private static int EntryOpen(string text, List<Token> tokens, string id)
    {
        var stack = new Stack<int>();
        for (int i = 0; i < tokens.Count; i++)
        {
            Token t = tokens[i];
            if (t.Kind is '{' or '[')
                stack.Push(i);
            else if (t.Kind is '}' or ']')
                stack.Pop();
            else if (t.Kind == 's' && stack.Count == 3 && text[t.Start..t.End] == "\"id\"" && i + 2 < tokens.Count && tokens[i + 1].Kind == ':'
                     && tokens[i + 2].Kind == 's' && text[(tokens[i + 2].Start + 1)..(tokens[i + 2].End - 1)] == id)
                return stack.Peek();
        }
        throw new InvalidDataException($"no model {id} in the catalog");
    }

    private static List<Token> Tokens(string text)
    {
        var tokens = new List<Token>();
        int n = text.Length;
        for (int i = 0; i < n;)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
                i++;
            else if (c == '/' && i + 1 < n && text[i + 1] == '/')
                i = text.IndexOf('\n', i) is int e and >= 0 ? e : n;
            else if (c == '/' && i + 1 < n && text[i + 1] == '*')
                i = text.IndexOf("*/", i + 2, StringComparison.Ordinal) is int e and >= 0 ? e + 2 : n;
            else if (c == '"')
            {
                int j = i + 1;
                while (j < n && text[j] != '"')
                    j += text[j] == '\\' ? 2 : 1;
                tokens.Add(new Token('s', i, Math.Min(n, j + 1)));
                i = j + 1;
            }
            else if ("{}[],:".IndexOf(c) >= 0)
                tokens.Add(new Token(c, i, ++i));
            else
            {
                int j = i;
                while (j < n && !char.IsWhiteSpace(text[j]) && "{}[],:\"/".IndexOf(text[j]) < 0)
                    j++;
                j = Math.Max(j, i + 1);
                tokens.Add(new Token('v', i, j));
                i = j;
            }
        }
        return tokens;
    }
}
