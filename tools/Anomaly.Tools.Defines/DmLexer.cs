using System.Text;

namespace Anomaly.Tools.Defines;

/// <summary>
/// Splits DM source text into preprocessor tokens. It follows OpenDream's
/// <c>DMCompiler/Compiler/DMPreprocessor/DMPreprocessorLexer.cs</c> (the reference for what BYOND's DM compiler
/// accepts): nested block comments, a <c>//</c> inside a block comment hiding the rest of that line, text
/// literals with embedded <c>[expressions]</c> that are lexed as tokens, document strings <c>{"..."}</c>, the three
/// raw string forms, resource literals <c>'...'</c>, <c>#keyword</c>, <c>#x</c> and <c>##x</c>, and numbers in DM's
/// greedy form (<c>0x1F</c>, <c>1e-3</c>, <c>1.#INF</c>). Unlike OpenDream it keeps comments as tokens so that
/// dmdoc comments can be carried over to the generated C#. Comment and string handling is string-aware:
/// <c>byond://</c> inside a literal is text, not a comment.
/// </summary>
public sealed class DmLexer
{
    private static readonly HashSet<string> DirectiveKeywords = new(StringComparer.Ordinal)
    {
        "define", "undef", "if", "ifdef", "ifndef", "elif", "else", "endif", "include", "error", "warn", "warning", "pragma",
    };

    private readonly string _s;
    private readonly List<string> _diagnostics;
    private int _i;
    private int _line = 1;

    private DmLexer(string text, List<string> diagnostics)
    {
        _s = text;
        _diagnostics = diagnostics;
    }

    /// <summary>Lexes a whole file. Line endings are normalised to <c>\n</c> first.</summary>
    public static List<Token> Lex(string text, List<string> diagnostics = null)
    {
        text = Normalize(text);
        var lexer = new DmLexer(text, diagnostics ?? new List<string>());
        return lexer.LexUntil(stopAtBracket: false);
    }

    public static string Normalize(string text)
    {
        if (text.Length > 0 && text[0] == '﻿')
            text = text.Substring(1);
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private char Cur => _i < _s.Length ? _s[_i] : '\0';

    private char Peek(int k = 1) => _i + k < _s.Length ? _s[_i + k] : '\0';

    private bool AtEnd => _i >= _s.Length;

    private void Diag(string message) => _diagnostics.Add($"line {_line}: {message}");

    private List<Token> LexUntil(bool stopAtBracket)
    {
        var tokens = new List<Token>();
        int depth = 0;
        while (!AtEnd)
        {
            if (stopAtBracket && Cur == ']' && depth == 0)
                return tokens;
            Token t = Next();
            if (t == null)
                continue;
            if (stopAtBracket)
            {
                if (t.IsPunct("["))
                    depth++;
                else if (t.IsPunct("]"))
                    depth--;
            }
            tokens.Add(t);
        }
        if (stopAtBracket)
            Diag("unterminated embedded expression in a text literal");
        return tokens;
    }

    private Token Make(TokenKind kind, int start, int line) => new(kind, _s.Substring(start, _i - start), line);

    private Token Next()
    {
        int start = _i;
        int line = _line;
        char c = Cur;
        switch (c)
        {
            case '\n':
                _i++;
                _line++;
                return new Token(TokenKind.Newline, "\n", line);
            case ' ':
            case '\t':
                while (Cur is ' ' or '\t')
                    _i++;
                return Make(TokenKind.Whitespace, start, line);
            case '\\':
            {
                // A backslash before the end of the line splices the next line on. Trailing blanks between the
                // backslash and the line end are tolerated (DM's own lexer requires the newline right after it).
                int j = _i + 1;
                while (j < _s.Length && _s[j] is ' ' or '\t')
                    j++;
                if (j >= _s.Length || _s[j] == '\n')
                {
                    if (j > _i + 1)
                        Diag("blanks between a line-splice backslash and the line end");
                    _i = Math.Min(j + 1, _s.Length);
                    var splice = new Token(TokenKind.LineSplice, "\\", line);
                    _line++;
                    return splice;
                }
                // An escaped identifier: the next character becomes an identifier (OpenDream).
                _i += 2;
                return new Token(TokenKind.Identifier, _s.Substring(start + 1, 1), line);
            }
            case '/':
                if (Peek() == '/')
                    return LineComment(start, line);
                if (Peek() == '*')
                    return BlockComment(start, line);
                if (Peek() == '=')
                {
                    _i += 2;
                    return Make(TokenKind.Punct, start, line);
                }
                _i++;
                return Make(TokenKind.Punct, start, line);
            case '"':
                return LexString(start, line, '"', document: false, resource: false);
            case '\'':
                return LexString(start, line, '\'', document: false, resource: true);
            case '{':
                if (Peek() == '"')
                {
                    _i++;
                    return LexString(start, line, '"', document: true, resource: false);
                }
                _i++;
                return Make(TokenKind.Punct, start, line);
            case '@':
                return LexRawString(start, line);
            case '#':
                return LexHash(start, line);
        }

        if (char.IsAsciiLetter(c) || c == '_')
        {
            while (char.IsAsciiLetterOrDigit(Cur) || Cur == '_')
                _i++;
            return Make(TokenKind.Identifier, start, line);
        }
        if (char.IsAsciiDigit(c))
            return LexNumber(start, line);
        return LexPunct(start, line);
    }

    private Token LineComment(int start, int line)
    {
        while (!AtEnd && Cur != '\n')
            _i++;
        string text = _s.Substring(start, _i - start);
        bool doc = text.StartsWith("///", StringComparison.Ordinal) && !text.StartsWith("////", StringComparison.Ordinal);
        // "//!" is dmdoc for the enclosing file or scope: not the doc of the next define, but its text is plain.
        bool scopeDoc = text.StartsWith("//!", StringComparison.Ordinal);
        string body = doc || scopeDoc ? text.Substring(3) : text.Substring(2);
        if (doc && body.StartsWith('<'))
            body = body.Substring(1);
        return new Token(TokenKind.Comment, text, line) { CommentBody = body, IsDocComment = doc };
    }

    private Token BlockComment(int start, int line)
    {
        _i += 2;
        int depth = 1;
        while (depth > 0)
        {
            if (AtEnd)
            {
                Diag("unterminated block comment");
                break;
            }
            char c = Cur;
            if (c == '/' && Peek() == '*')
            {
                depth++;
                _i += 2;
            }
            else if (c == '/' && Peek() == '/')
            {
                // OpenDream: a line comment inside a block comment hides the rest of that line, including a "*/".
                while (!AtEnd && Cur != '\n')
                    _i++;
            }
            else if (c == '*' && Peek() == '/')
            {
                depth--;
                _i += 2;
            }
            else
            {
                if (c == '\n')
                    _line++;
                _i++;
            }
        }
        string text = _s.Substring(start, _i - start);
        bool doc = text.StartsWith("/**", StringComparison.Ordinal) && !text.StartsWith("/***", StringComparison.Ordinal) && text != "/**/";
        string body = text.Length >= 4 ? text.Substring(doc ? 3 : 2, Math.Max(0, text.Length - (doc ? 5 : 4))) : "";
        return new Token(TokenKind.Comment, text, line) { CommentBody = body, IsDocComment = doc };
    }

    private Token LexString(int start, int line, char terminator, bool document, bool resource)
    {
        _i++; // the opening quote
        var parts = new List<StringPart>();
        var literal = new StringBuilder();
        bool terminated = false;
        while (!AtEnd)
        {
            char c = Cur;
            if (c == '\n')
            {
                if (!document)
                    break;
                literal.Append('\n');
                _i++;
                _line++;
                continue;
            }
            if (c == '\\')
            {
                // Keep the escape as written; a backslash before the line end splices the next line, dropping its
                // leading blanks (BYOND ref "text").
                if (Peek() == '\n')
                {
                    _i += 2;
                    _line++;
                    while (Cur is ' ' or '\t')
                        _i++;
                    continue;
                }
                literal.Append(c);
                if (!AtEnd && _i + 1 < _s.Length)
                    literal.Append(_s[_i + 1]);
                _i += 2;
                continue;
            }
            if (c == '[' && !resource)
            {
                parts.Add(new StringPart(literal.ToString(), null));
                literal.Clear();
                _i++;
                List<Token> expr = LexUntil(stopAtBracket: true);
                if (Cur == ']')
                    _i++;
                parts.Add(new StringPart(null, expr));
                continue;
            }
            if (c == terminator)
            {
                if (document)
                {
                    if (Peek() == '}')
                    {
                        _i += 2;
                        terminated = true;
                        break;
                    }
                    literal.Append(c);
                    _i++;
                    continue;
                }
                _i++;
                terminated = true;
                break;
            }
            literal.Append(c);
            _i++;
        }
        if (!terminated)
            Diag("unterminated text literal");
        parts.Add(new StringPart(literal.ToString(), null));
        return new Token(TokenKind.String, _s.Substring(start, _i - start), line)
        {
            StringKind = resource ? StringKind.Resource : document ? StringKind.Document : StringKind.Normal,
            Parts = parts,
        };
    }

    private Token LexRawString(int start, int line)
    {
        // OpenDream DMPreprocessorLexer, case '@'.
        _i++;
        char delimiter = Cur;
        if (AtEnd || delimiter == '\n')
        {
            Diag("'@' without a raw string");
            return Make(TokenKind.Punct, start, line);
        }
        string value;
        if (delimiter == '(')
        {
            int close = _s.IndexOf(')', _i);
            string marker = close < 0 ? "" : _s.Substring(_i + 1, close - _i - 1);
            if (close < 0 || marker.Length == 0)
            {
                Diag("bad raw string delimiter");
                _i++;
                return Make(TokenKind.Punct, start, line);
            }
            _i = close + 1;
            if (Cur == '\n')
            {
                _i++;
                _line++;
            }
            int end = _s.IndexOf(marker, _i, StringComparison.Ordinal);
            if (end < 0)
            {
                Diag("unterminated raw string");
                end = _s.Length;
            }
            value = _s.Substring(_i, end - _i);
            _line += value.Count(ch => ch == '\n');
            if (value.EndsWith('\n'))
                value = value.Substring(0, value.Length - 1);
            _i = Math.Min(end + marker.Length, _s.Length);
        }
        else if (delimiter == '{' && Peek() == '"')
        {
            _i += 2;
            int end = _s.IndexOf("\"}", _i, StringComparison.Ordinal);
            if (end < 0)
            {
                Diag("unterminated raw string");
                end = _s.Length;
            }
            value = _s.Substring(_i, end - _i);
            _line += value.Count(ch => ch == '\n');
            if (value.StartsWith('\n'))
                value = value.Substring(1);
            if (value.EndsWith('\n'))
                value = value.Substring(0, value.Length - 1);
            _i = Math.Min(end + 2, _s.Length);
        }
        else
        {
            _i++;
            int j = _i;
            while (j < _s.Length && _s[j] != delimiter && _s[j] != '\n')
                j++;
            value = _s.Substring(_i, j - _i);
            if (j < _s.Length && _s[j] == delimiter)
                j++;
            else
                Diag("unterminated raw string");
            _i = j;
        }
        return new Token(TokenKind.String, _s.Substring(start, _i - start), line) { StringKind = StringKind.Raw, RawValue = value };
    }

    private Token LexHash(int start, int line)
    {
        _i++;
        bool concat = false;
        if (Cur == '#')
        {
            concat = true;
            _i++;
        }
        while (Cur is ' ' or '\t')
            _i++;
        int nameStart = _i;
        while (char.IsAsciiLetterOrDigit(Cur) || Cur == '_')
            _i++;
        string name = _s.Substring(nameStart, _i - nameStart);
        if (name.Length == 0)
            return null; // OpenDream skips a lone '#'
        if (concat)
            return new Token(TokenKind.Concat, _s.Substring(start, _i - start), line) { Name = name };
        if (DirectiveKeywords.Contains(name))
        {
            string rest = null;
            if (name is "error" or "warn" or "warning")
            {
                int restStart = _i;
                while (!AtEnd && Cur != '\n')
                    _i++;
                rest = _s.Substring(restStart, _i - restStart).Trim();
            }
            return new Token(TokenKind.Directive, _s.Substring(start, _i - start), line) { Name = name, Rest = rest };
        }
        return new Token(TokenKind.Stringify, _s.Substring(start, _i - start), line) { Name = name };
    }

    private Token LexNumber(int start, int line)
    {
        // OpenDream: a digit, then any of [0-9a-fA-F.x#eEpP], with a sign right after an exponent letter and the
        // 1.#INF / 1.#IND forms.
        char prev = Cur;
        _i++;
        while (!AtEnd)
        {
            char c = Cur;
            if ((prev == 'e' || prev == 'E') && (c == '-' || c == '+'))
            {
                _i++;
                prev = c;
                continue;
            }
            if (prev == '#' && c == 'I')
            {
                if (Peek() == 'N' && (Peek(2) == 'F' || Peek(2) == 'D'))
                {
                    _i += 3;
                    prev = 'F';
                    continue;
                }
                break;
            }
            if (char.IsAsciiHexDigit(c) || c == '.' || c == 'x' || c == '#' || c == 'p' || c == 'P')
            {
                // A '.' followed by a second '.' is not part of the number ("1...").
                if (c == '.' && Peek() == '.')
                    break;
                _i++;
                prev = c;
                continue;
            }
            break;
        }
        return Make(TokenKind.Number, start, line);
    }

    private static readonly string[] Puncts =
    {
        "<<=", ">>=", "||=", "&&=", "%%=", "[]=",
        "::", ":=", "[]", ">>", ">=", "<<", "<=", "<>", "||", "|=", "**", "*=", "++", "+=", "&&", "&=", "~=", "~!",
        "%=", "%%", "^=", "!=", "==", "--", "-=",
        "}", ";", ".", ":", ",", "(", ")", "[", "]", "?", ">", "<", "|", "*", "+", "&", "~", "%", "^", "!", "=", "-",
    };

    private Token LexPunct(int start, int line)
    {
        foreach (string p in Puncts)
        {
            if (string.CompareOrdinal(_s, _i, p, 0, p.Length) == 0)
            {
                _i += p.Length;
                return Make(TokenKind.Punct, start, line);
            }
        }
        Diag($"unknown character '{Cur}'");
        _i++;
        return Make(TokenKind.Punct, start, line);
    }
}
