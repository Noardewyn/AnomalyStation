using System.Globalization;

namespace Anomaly.Tools.Defines;

/// <summary>
/// Parses an expanded define body as a DM expression with DM's precedence, which is OpenDream's
/// <c>DMParser</c> chain (translation-rules.md 4.11): <c>in</c> &lt; assignment &lt; <c>?:</c> &lt; <c>||</c> &lt;
/// <c>&amp;&amp;</c> &lt; <c>|</c> &lt; <c>^</c> &lt; <c>&amp;</c> &lt; <c>== != &lt;&gt; ~= ~!</c> &lt;
/// <c>&lt;&lt; &gt;&gt;</c> &lt; <c>&lt; &lt;= &gt; &gt;=</c> &lt; <c>+ -</c> &lt; <c>* / % %%</c> &lt;
/// <c>**</c> (right-associative) &lt; prefix <c>! ~ ++ --</c> &lt; sign <c>+ -</c> &lt; <c>new</c>, member access,
/// calls and indexing. Shifts bind more loosely than the relational operators, unlike C#. Anything that is not an
/// expression (a statement keyword, a <c>;</c>, a stray token) throws <see cref="DmSyntaxException"/>.
/// </summary>
public sealed class ExprParser
{
    private static readonly HashSet<string> StatementKeywords = new(StringComparer.Ordinal)
    {
        "var", "if", "else", "for", "while", "do", "return", "switch", "spawn", "del", "set", "goto", "break", "continue",
        "throw", "try", "catch", "as", "proc", "verb", "to",
    };

    private static readonly HashSet<string> AssignOps = new(StringComparer.Ordinal)
    {
        "=", "+=", "-=", "*=", "/=", "%=", "%%=", "|=", "&=", "^=", "<<=", ">>=", "||=", "&&=", ":=",
    };

    private readonly List<Token> _tokens;
    private int _p;
    private bool _inTernaryTrueBranch;

    private ExprParser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.Where(t => !t.IsTrivia && t.Kind != TokenKind.Newline).ToList();
    }

    public static Expr Parse(IEnumerable<Token> tokens)
    {
        var parser = new ExprParser(tokens);
        if (parser._tokens.Count == 0)
            throw new DmSyntaxException("empty expression");
        Expr expr = parser.ParseIn();
        if (!parser.AtEnd)
            throw new DmSyntaxException($"unexpected '{parser.Cur.Text}' after the expression");
        return expr;
    }

    private bool AtEnd => _p >= _tokens.Count;

    private Token Cur => _p < _tokens.Count ? _tokens[_p] : null;

    private Token PeekAt(int k) => _p + k < _tokens.Count ? _tokens[_p + k] : null;

    private bool IsPunct(string p) => Cur != null && Cur.IsPunct(p);

    private Token Advance() => _tokens[_p++];

    private void Expect(string p)
    {
        if (!IsPunct(p))
            throw new DmSyntaxException(AtEnd ? $"expected '{p}' at the end" : $"expected '{p}', found '{Cur.Text}'");
        _p++;
    }

    private Expr ParseIn()
    {
        Expr e = ParseAssign();
        while (Cur != null && Cur.IsIdentifier("in"))
        {
            Advance();
            e = new InExpr(e, ParseAssign());
        }
        return e;
    }

    private Expr ParseAssign()
    {
        Expr e = ParseTernary();
        if (Cur != null && Cur.Kind == TokenKind.Punct && AssignOps.Contains(Cur.Text))
        {
            string op = Advance().Text;
            return new AssignExpr(op, e, ParseAssign());
        }
        return e;
    }

    private Expr ParseTernary()
    {
        Expr condition = ParseBinary(0);
        if (!IsPunct("?"))
            return condition;
        Advance();
        bool saved = _inTernaryTrueBranch;
        _inTernaryTrueBranch = true;
        Expr whenTrue = ParseTernary();
        _inTernaryTrueBranch = saved;
        Expect(":");
        Expr whenFalse = ParseTernary();
        return new TernaryExpr(condition, whenTrue, whenFalse);
    }

    // Binary levels from the loosest to the tightest; '**' is handled by ParsePower.
    private static readonly string[][] Levels =
    {
        new[] { "||" },
        new[] { "&&" },
        new[] { "|" },
        new[] { "^" },
        new[] { "&" },
        new[] { "==", "!=", "<>", "~=", "~!" },
        new[] { "<<", ">>" },
        new[] { "<", "<=", ">", ">=" },
        new[] { "+", "-" },
        new[] { "*", "/", "%", "%%" },
    };

    private Expr ParseBinary(int level)
    {
        if (level == Levels.Length)
            return ParsePower();
        Expr left = ParseBinary(level + 1);
        while (Cur != null && Cur.Kind == TokenKind.Punct && Array.IndexOf(Levels[level], Cur.Text) >= 0)
        {
            string op = Advance().Text;
            left = new BinaryExpr(op == "<>" ? "!=" : op, left, ParseBinary(level + 1));
        }
        return left;
    }

    private Expr ParsePower()
    {
        Expr left = ParseUnary();
        if (IsPunct("**"))
        {
            Advance();
            return new BinaryExpr("**", left, ParsePower());
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (Cur != null && Cur.Kind == TokenKind.Punct && Cur.Text is "!" or "~" or "++" or "--" or "&" or "*")
        {
            string op = Advance().Text;
            return new UnaryExpr(op, ParseUnary());
        }
        return ParseSign();
    }

    private Expr ParseSign()
    {
        if (Cur != null && Cur.Kind == TokenKind.Punct && Cur.Text is "+" or "-")
        {
            string op = Advance().Text;
            return new UnaryExpr(op, ParseSign());
        }
        Expr e = ParsePostfix(ParsePrimary());
        if (IsPunct("++") || IsPunct("--"))
            e = new UnaryExpr("post" + Advance().Text, e);
        return e;
    }

    private Expr ParsePrimary()
    {
        Token t = Cur ?? throw new DmSyntaxException("expression expected at the end");
        switch (t.Kind)
        {
            case TokenKind.Number:
                Advance();
                if (!DmNumbers.TryParseLiteral(t.Text, out float value))
                    throw new DmSyntaxException($"bad number '{t.Text}'");
                return new NumberExpr(value, t.Text);
            case TokenKind.String:
                Advance();
                return new TextExpr(t);
            case TokenKind.Identifier:
                return ParseIdentifier();
            case TokenKind.Directive:
                throw new DmSyntaxException($"directive '{t.Text}' inside the expression");
            case TokenKind.Stringify:
            case TokenKind.Concat:
                throw new DmSyntaxException($"'{t.Text}' outside a macro body");
        }
        if (t.IsPunct("("))
        {
            Advance();
            bool saved = _inTernaryTrueBranch;
            _inTernaryTrueBranch = false;
            Expr inner = ParseIn();
            _inTernaryTrueBranch = saved;
            Expect(")");
            return inner;
        }
        if (t.IsPunct("/") && PeekAt(1)?.Kind == TokenKind.Identifier)
            return new PathExpr(ParsePathSegments(""));
        if (t.IsPunct(".") && PeekAt(1)?.Kind == TokenKind.Identifier && PeekAt(2) != null && PeekAt(2).IsPunct("/"))
        {
            Advance();
            string first = Advance().Text;
            return new PathExpr(ParsePathSegments("." + first));
        }
        if (t.IsPunct("."))
        {
            Advance();
            if (IsPunct("."))
            {
                Advance();
                return new IdentifierExpr("..");
            }
            return new IdentifierExpr(".");
        }
        throw new DmSyntaxException($"unexpected '{t.Text}'");
    }

    private string ParsePathSegments(string prefix)
    {
        var path = new System.Text.StringBuilder(prefix);
        while (IsPunct("/") && PeekAt(1)?.Kind == TokenKind.Identifier)
        {
            Advance();
            path.Append('/').Append(Advance().Text);
        }
        return path.ToString();
    }

    private Expr ParseIdentifier()
    {
        Token t = Advance();
        string name = t.Text;
        if (name == "null")
            return new NullExpr();
        if (StatementKeywords.Contains(name))
            throw new DmSyntaxException($"statement keyword '{name}'");
        if (name == "new")
        {
            Expr type = null;
            if (IsPunct("/") && PeekAt(1)?.Kind == TokenKind.Identifier)
                type = new PathExpr(ParsePathSegments(""));
            else if (Cur != null && Cur.Kind == TokenKind.Identifier)
                type = new IdentifierExpr(Advance().Text);
            List<Expr> arguments = null;
            if (IsPunct("("))
                arguments = ParseArguments();
            return new NewExpr(type, arguments);
        }
        return new IdentifierExpr(name);
    }

    private List<Expr> ParseArguments()
    {
        Expect("(");
        bool saved = _inTernaryTrueBranch;
        _inTernaryTrueBranch = false;
        var arguments = new List<Expr>();
        if (IsPunct(")"))
        {
            Advance();
            _inTernaryTrueBranch = saved;
            return arguments;
        }
        while (true)
        {
            if (IsPunct(",") || IsPunct(")"))
                arguments.Add(new NullExpr()); // an empty argument
            else
                arguments.Add(ParseIn());
            if (IsPunct(","))
            {
                Advance();
                continue;
            }
            Expect(")");
            break;
        }
        _inTernaryTrueBranch = saved;
        return arguments;
    }

    private Expr ParsePostfix(Expr e)
    {
        while (Cur != null)
        {
            if (IsPunct("("))
            {
                e = new CallExpr(e, ParseArguments());
            }
            else if (IsPunct("["))
            {
                Advance();
                bool saved = _inTernaryTrueBranch;
                _inTernaryTrueBranch = false;
                Expr index = ParseIn();
                _inTernaryTrueBranch = saved;
                Expect("]");
                e = new IndexExpr(e, index);
            }
            else if (IsPunct(".") && PeekAt(1)?.Kind == TokenKind.Identifier)
            {
                Advance();
                e = new MemberExpr(e, ".", Advance().Text);
            }
            else if (IsPunct("::") && PeekAt(1)?.Kind == TokenKind.Identifier)
            {
                Advance();
                e = new MemberExpr(e, "::", Advance().Text);
            }
            else if (IsPunct(":") && !_inTernaryTrueBranch && PeekAt(1)?.Kind == TokenKind.Identifier)
            {
                Advance();
                e = new MemberExpr(e, ":", Advance().Text);
            }
            else if (IsPunct("?") && PeekAt(1) != null && (PeekAt(1).IsPunct(".") || PeekAt(1).IsPunct(":")) && PeekAt(2)?.Kind == TokenKind.Identifier)
            {
                Advance();
                string op = "?" + Advance().Text;
                e = new MemberExpr(e, op, Advance().Text);
            }
            else if (IsPunct("?") && PeekAt(1) != null && PeekAt(1).IsPunct("["))
            {
                Advance();
                Advance();
                Expr index = ParseIn();
                Expect("]");
                e = new IndexExpr(e, index);
            }
            else
            {
                break;
            }
        }
        return e;
    }
}

public static class DmNumbers
{
    /// <summary>DM integers are exact up to 2^24 (IEEE binary32; translation-rules.md 3.1).</summary>
    public const float ExactIntegerLimit = 16777216f;

    /// <summary>
    /// A DM number literal (OpenDream DMLexer: <c>1.#INF</c>, <c>1.#IND</c>, <c>0x</c> hex, then decimal) as the
    /// binary32 value DM stores. A decimal literal is rounded to binary32 once (<see cref="float.Parse(string)"/> is
    /// correctly rounded).
    /// </summary>
    public static bool TryParseLiteral(string text, out float value)
    {
        switch (text)
        {
            case "1.#INF":
            case "1#INF":
                value = float.PositiveInfinity;
                return true;
            case "1.#IND":
            case "1#IND":
                value = float.NaN;
                return true;
        }
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out long hex))
            {
                value = hex;
                return true;
            }
            value = 0;
            return false;
        }
        return float.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>True when <paramref name="v"/> is an integer in DM's exact range [-2^24, 2^24].</summary>
    public static bool IsExactInteger(float v) => float.IsFinite(v) && v == MathF.Truncate(v) && MathF.Abs(v) <= ExactIntegerLimit;
}
