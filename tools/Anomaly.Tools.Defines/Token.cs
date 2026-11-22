using System.Text;

namespace Anomaly.Tools.Defines;

public enum TokenKind
{
    Identifier,
    Number,
    /// <summary>A text literal, a document string, a raw string or a resource literal (see <see cref="StringKind"/>).</summary>
    String,
    Punct,
    Whitespace,
    Newline,
    /// <summary>A backslash at the end of a line (the line continues).</summary>
    LineSplice,
    Comment,
    /// <summary><c>#define</c>, <c>#if</c>, <c>#error ...</c> and the other preprocessor keywords.</summary>
    Directive,
    /// <summary><c>#x</c> inside a macro body.</summary>
    Stringify,
    /// <summary><c>##x</c> inside a macro body.</summary>
    Concat,
}

public enum StringKind
{
    /// <summary><c>"..."</c>: escapes and embedded expressions apply.</summary>
    Normal,
    /// <summary><c>{"..."}</c>: may span lines; escapes and embedded expressions apply.</summary>
    Document,
    /// <summary><c>@"..."</c>, <c>@{"..."}</c>, <c>@(XYZ)...XYZ</c>: no escapes, no embedded expressions.</summary>
    Raw,
    /// <summary><c>'file.dmi'</c>: a resource (file) literal.</summary>
    Resource,
}

/// <summary>One literal segment (escapes not yet decoded) or one embedded expression of a DM text literal.</summary>
public sealed class StringPart
{
    public StringPart(string literal, List<Token> expression)
    {
        Literal = literal;
        Expression = expression;
    }

    /// <summary>The literal text with DM escapes still in place; null for an embedded expression.</summary>
    public string Literal { get; }

    /// <summary>The tokens of an embedded <c>[expression]</c>; null for a literal segment.</summary>
    public List<Token> Expression { get; }

    public bool IsExpression => Expression != null;
}

/// <summary>A preprocessor token of a DM source file (the token set of OpenDream's DMPreprocessorLexer).</summary>
public sealed class Token
{
    public Token(TokenKind kind, string text, int line)
    {
        Kind = kind;
        Text = text;
        Line = line;
    }

    public TokenKind Kind { get; }

    /// <summary>The source text of the token (for a string, the whole literal with its delimiters).</summary>
    public string Text { get; }

    /// <summary>1-based line of the token's first character.</summary>
    public int Line { get; }

    public StringKind StringKind { get; init; }

    /// <summary>Segments of a <see cref="StringKind.Normal"/>, <see cref="StringKind.Document"/> or <see cref="StringKind.Resource"/> literal.</summary>
    public List<StringPart> Parts { get; init; }

    /// <summary>The value of a <see cref="StringKind.Raw"/> literal.</summary>
    public string RawValue { get; init; }

    /// <summary>For a comment: the text without the comment markers.</summary>
    public string CommentBody { get; init; }

    /// <summary>For a comment: a dmdoc comment (<c>///</c> or <c>/** */</c>).</summary>
    public bool IsDocComment { get; init; }

    /// <summary>For a directive, stringify or concat token: the keyword or parameter name.</summary>
    public string Name { get; init; }

    /// <summary>For <c>#error</c> and <c>#warn</c>: the rest of the line.</summary>
    public string Rest { get; init; }

    public bool IsPunct(string p) => Kind == TokenKind.Punct && Text == p;

    public bool IsIdentifier(string name) => Kind == TokenKind.Identifier && Text == name;

    public bool IsTrivia => Kind is TokenKind.Whitespace or TokenKind.Comment or TokenKind.LineSplice;

    public override string ToString() => Text;

    /// <summary>The source text of a token sequence with every run of whitespace, comments and line splices collapsed to one space.</summary>
    public static string JoinText(IEnumerable<Token> tokens)
    {
        var sb = new StringBuilder();
        bool pendingSpace = false;
        foreach (Token t in tokens)
        {
            if (t.IsTrivia || t.Kind == TokenKind.Newline)
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace)
                sb.Append(' ');
            pendingSpace = false;
            sb.Append(t.Text);
        }
        return sb.ToString();
    }
}
