using System.Text;
using System.Text.RegularExpressions;

namespace Anomaly.Tools.Defines;

public sealed class FoldContext
{
    /// <summary><c>#if</c> semantics: an identifier that is not a macro counts as 0.</summary>
    public bool UnknownIdentifiersAreZero { get; init; }

    /// <summary>Problems that make the generated value questionable (a bit operand outside 24 bits, ...).</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Hard errors: a shift by 24 or more bits. DM drops every bit then (BYOND ref "&lt;&lt; shift operator": bits
    /// shifted beyond the 24 low bits are lost), so <c>(1&lt;&lt;24)</c> is 0; tg never does it, and a flag that
    /// folds to 0 would be a tg bug, so the tool refuses to write (defines.md 5.2).
    /// </summary>
    public List<string> Errors { get; } = new();
}

/// <summary>
/// Folds a DM expression with DM's run-time semantics, so that the C# constant equals what tg computes:
/// numbers are IEEE binary32 and every operation rounds to binary32 (C# <c>float</c> arithmetic), <c>/</c> is
/// real division (BYOND ref "/ operator"), <c>%</c> truncates both operands to integers and keeps the dividend's
/// sign (translation-rules.md 4.3), <c>%%</c> is <c>B * fract(A / B)</c> (BYOND ref "%% operator"; OpenDream
/// <c>ModulusModulusValues</c>), the bit operators work on 24 bits (BYOND ref: "A and B must be between 0 and
/// 2**24 - 1"; <c>~A</c> is <c>(~A) &amp; 0xFFFFFF</c> as in OpenDream <c>BitNot</c>; shifts are masked as in
/// translation-rules.md 4.4), null counts as 0 in arithmetic, text <c>+</c> text concatenates, text <c>+</c> number
/// is a type mismatch (not a constant), <c>&amp;&amp;</c> and <c>||</c> return an operand, comparisons give 1 or 0,
/// <c>rgb(r, g, b)</c> gives <c>"#rrggbb"</c> in lower case (BYOND ref "rgb proc": rgb(255,0,128) is "#ff0080").
/// Everything else (a list, a typepath, a call, a member access, a var of the caller) is not a constant and comes
/// back as <see cref="DmNotConst"/> with the reason.
/// </summary>
public static class ConstFolder
{
    private static readonly Regex SubsystemName = new("^SS[a-z_]", RegexOptions.CultureInvariant);

    public static DmValue Evaluate(Expr e, FoldContext ctx)
    {
        switch (e)
        {
            case NumberExpr n:
                return new DmNum(n.Value);
            case TextExpr t:
                return EvaluateText(t.Token, ctx);
            case NullExpr:
                return DmNull.Instance;
            case IdentifierExpr id:
                return ctx.UnknownIdentifiersAreZero ? new DmNum(0) : Identifier(id.Name);
            case PathExpr p:
                return new DmNotConst("typepath", $"typepath {p.Path} (needs the type tree)");
            case MemberExpr m:
                if (m.Op == "::")
                    return new DmNotConst("type_var", $"{Describe(m.Target)}::{m.Member} (a type's initial value, needs the type tree)");
                return new DmNotConst("runtime", $"reads {Describe(m.Target)}{m.Op}{m.Member} at run time");
            case IndexExpr:
                return new DmNotConst("runtime", "indexes a list at run time");
            case CallExpr c:
                return EvaluateCall(c, ctx);
            case NewExpr:
                return new DmNotConst("runtime", "creates an object (new)");
            case AssignExpr a:
                return new DmNotConst("statement", $"an assignment ({a.Op})");
            case InExpr:
                return new DmNotConst("runtime", "an 'in' test");
            case UnaryExpr u:
                return EvaluateUnary(u, ctx);
            case BinaryExpr b:
                return EvaluateBinary(b, ctx);
            case TernaryExpr t:
            {
                DmValue c = Evaluate(t.Condition, ctx);
                if (c is DmNotConst)
                    return c;
                return Evaluate(Truthy(c) ? t.WhenTrue : t.WhenFalse, ctx);
            }
        }
        throw new InvalidOperationException($"unknown expression {e.GetType().Name}");
    }

    private static string Describe(Expr e) => e switch
    {
        IdentifierExpr id => id.Name,
        PathExpr p => p.Path,
        MemberExpr m => $"{Describe(m.Target)}{m.Op}{m.Member}",
        CallExpr c => $"{Describe(c.Callee)}(...)",
        _ => "(...)",
    };

    private static DmValue Identifier(string name)
    {
        switch (name)
        {
            case "__FILE__":
            case "__LINE__":
            case "__PROC__":
            case "__TYPE__":
            case "__IMPLIED_TYPE__":
                return new DmNotConst("location", $"uses {name}, a value of the place where the macro is used");
            case "src":
            case "usr":
            case ".":
            case "..":
            case "args":
                return new DmNotConst("runtime", $"refers to the caller's {name}");
            case "world":
            case "GLOB":
            case "Master":
            case "global":
                return new DmNotConst("runtime", $"reads {name} at run time");
        }
        if (SubsystemName.IsMatch(name))
            return new DmNotConst("runtime", $"reads the subsystem {name} at run time");
        return new DmNotConst("unresolved", $"'{name}' is not a macro (a var, proc or type name at the use site)");
    }

    public static bool Truthy(DmValue v) => v switch
    {
        DmNum n => n.Value != 0,
        DmText t => t.Value.Length != 0,
        _ => false,
    };

    private static DmValue EvaluateText(Token token, FoldContext ctx)
    {
        switch (token.StringKind)
        {
            case StringKind.Raw:
                return new DmText(token.RawValue);
            case StringKind.Resource:
                return new DmNotConst("resource", $"resource file {token.Text}");
        }
        var sb = new StringBuilder();
        foreach (StringPart part in token.Parts)
        {
            if (!part.IsExpression)
            {
                string decoded = DmTextRules.Decode(part.Literal, out string reason);
                if (decoded == null)
                    return new DmNotConst("text_macro", reason);
                sb.Append(decoded);
                continue;
            }
            if (part.Expression.Count == 0)
                return new DmNotConst("text_format", "an empty [] in the text (a text() format placeholder)");
            DmValue inner;
            try
            {
                inner = Evaluate(ExprParser.Parse(part.Expression), ctx);
            }
            catch (DmSyntaxException ex)
            {
                return new DmNotConst("runtime", $"an embedded expression that is not an expression ({ex.Message})");
            }
            switch (inner)
            {
                case DmNum n:
                    if (!float.IsFinite(n.Value))
                        return new DmNotConst("runtime", "an embedded non-finite number");
                    sb.Append(DmTextRules.NumberToText(n.Value));
                    break;
                case DmText t:
                    sb.Append(t.Value);
                    break;
                case DmNull:
                    break;
                case DmNotConst nc:
                    return new DmNotConst(nc.Kind == "unresolved" ? "runtime" : nc.Kind, $"embedded [{Token.JoinText(part.Expression)}]: {nc.Reason}");
            }
        }
        return new DmText(sb.ToString());
    }

    private static DmValue EvaluateCall(CallExpr c, FoldContext ctx)
    {
        string name = c.Callee is IdentifierExpr id ? id.Name : null;
        switch (name)
        {
            case "list":
            case "alist":
                return new DmNotConst("list", $"{name}(...) literal: a new list at every use");
            case "rgb":
                return Rgb(c, ctx);
            case "nameof":
                return new DmNotConst("runtime", "nameof(...) proc or var reference");
        }
        return new DmNotConst("runtime", name == null ? "a call" : $"calls {name}()");
    }

    private static DmValue Rgb(CallExpr c, FoldContext ctx)
    {
        if (c.Arguments.Count is not (3 or 4))
            return new DmNotConst("runtime", "rgb() with a colour space or named arguments");
        var sb = new StringBuilder("#");
        foreach (Expr a in c.Arguments)
        {
            if (Evaluate(a, ctx) is not DmNum n || !DmNumbers.IsExactInteger(n.Value) || n.Value < 0 || n.Value > 255)
                return new DmNotConst("runtime", "rgb() with arguments that are not constant integers 0-255");
            sb.Append(((int)n.Value).ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return new DmText(sb.ToString());
    }

    private static DmValue EvaluateUnary(UnaryExpr u, FoldContext ctx)
    {
        if (u.Op is "++" or "--" or "post++" or "post--")
            return new DmNotConst("statement", $"an increment ({u.Op.Replace("post", "")})");
        if (u.Op is "&" or "*")
            return new DmNotConst("runtime", $"a pointer operation ({u.Op})");
        DmValue v = Evaluate(u.Operand, ctx);
        if (v is DmNotConst)
            return v;
        switch (u.Op)
        {
            case "!":
                return Bool(!Truthy(v));
            case "-":
                return v switch
                {
                    DmNum n => new DmNum(-n.Value),
                    DmNull => new DmNum(0),
                    _ => Mismatch("-text"),
                };
            case "+":
                return v is DmNum ? v : v is DmNull ? new DmNum(0) : Mismatch("+text");
            case "~":
                if (v is DmNull)
                    return new DmNum(0xFFFFFF); // OpenDream BitNot: a non-number gives 2^24 - 1
                if (v is not DmNum num)
                    return Mismatch("~text");
                return new DmNum(~ToBits(num.Value, "~", ctx) & 0xFFFFFF);
        }
        throw new InvalidOperationException($"unknown unary operator {u.Op}");
    }

    private static DmValue Bool(bool b) => new DmNum(b ? 1 : 0);

    private static DmNotConst Mismatch(string what) => new("error", $"type mismatch: {what} is a DM runtime error");

    private static int ToBits(float v, string op, FoldContext ctx)
    {
        if (!DmNumbers.IsExactInteger(v) || v < 0 || v > 0xFFFFFF)
            ctx.Warnings.Add($"operand {v.ToString(System.Globalization.CultureInfo.InvariantCulture)} of '{op}' is not an integer in 0..2^24-1");
        return (int)MathF.Truncate(v) & 0xFFFFFF;
    }

    private static DmValue EvaluateBinary(BinaryExpr b, FoldContext ctx)
    {
        DmValue left = Evaluate(b.Left, ctx);
        if (left is DmNotConst)
            return left;
        if (b.Op == "&&")
            return Truthy(left) ? Evaluate(b.Right, ctx) : left;
        if (b.Op == "||")
            return Truthy(left) ? left : Evaluate(b.Right, ctx);
        DmValue right = Evaluate(b.Right, ctx);
        if (right is DmNotConst)
            return right;

        switch (b.Op)
        {
            case "+":
                if (left is DmText lt && right is DmText rt)
                    return new DmText(lt.Value + rt.Value);
                if (left is DmText && right is DmNull)
                    return left;
                if (left is DmNull)
                    return right;
                if (left is DmNum && right is DmText || left is DmText && right is DmNum)
                    return Mismatch("text + number");
                return new DmNum(Num(left) + Num(right));
            case "-":
                if (left is DmText || right is DmText)
                    return Mismatch("text - x");
                return new DmNum(Num(left) - Num(right));
            case "*":
                if (left is DmText || right is DmText)
                    return Mismatch("text * x");
                return new DmNum(Num(left) * Num(right));
            case "/":
            {
                if (left is DmText || right is DmText)
                    return Mismatch("text / x");
                float divisor = Num(right);
                if (divisor == 0)
                    return new DmNotConst("error", "division by zero is a DM runtime error");
                return new DmNum(Num(left) / divisor);
            }
            case "%":
            {
                if (left is DmText || right is DmText)
                    return Mismatch("text % x");
                long divisor = (long)MathF.Truncate(Num(right));
                if (divisor == 0)
                    return new DmNotConst("error", "modulo by zero is a DM runtime error");
                return new DmNum((long)MathF.Truncate(Num(left)) % divisor);
            }
            case "%%":
            {
                if (left is DmText || right is DmText)
                    return Mismatch("text %% x");
                float a = Num(left);
                float d = Num(right);
                if (d == 0)
                    return new DmNotConst("error", "modulo by zero is a DM runtime error");
                float q = a / d;
                q -= MathF.Truncate(q);
                return new DmNum(q * d);
            }
            case "**":
                if (left is DmText || right is DmText)
                    return Mismatch("text ** x");
                return new DmNum((float)Math.Pow(Num(left), Num(right)));
            case "&":
            case "|":
            case "^":
            {
                if (left is DmText || right is DmText)
                    return Mismatch($"text {b.Op} x");
                if (b.Op == "|" && left is DmNull)
                    return right is DmNull ? new DmNum(0) : right;
                if (b.Op == "|" && right is DmNull)
                    return left;
                int l = ToBits(Num(left), b.Op, ctx);
                int r = ToBits(Num(right), b.Op, ctx);
                int result = b.Op switch { "&" => l & r, "|" => l | r, _ => l ^ r };
                return new DmNum(result & 0xFFFFFF);
            }
            case "<<":
            case ">>":
            {
                if (left is DmText || right is DmText)
                    return Mismatch($"text {b.Op} x");
                float shift = Num(right);
                if (!DmNumbers.IsExactInteger(shift) || shift < 0 || shift >= 24)
                {
                    ctx.Errors.Add($"shift by {shift.ToString(System.Globalization.CultureInfo.InvariantCulture)}: DM keeps 24 bits, so every bit is lost");
                    return new DmNotConst("error", "a shift by 24 or more bits (DM loses every bit; refused as a likely tg bug)");
                }
                int l = ToBits(Num(left), b.Op, ctx);
                int n = (int)shift;
                return new DmNum(b.Op == "<<" ? (l << n) & 0xFFFFFF : (l & 0xFFFFFF) >> n);
            }
            case "==":
                return Bool(DmEquals(left, right));
            case "!=":
                return Bool(!DmEquals(left, right));
            case "~=":
                return Bool(DmEquals(left, right));
            case "~!":
                return Bool(!DmEquals(left, right));
            case "<":
            case "<=":
            case ">":
            case ">=":
            {
                if (left is DmText || right is DmText)
                    return new DmNotConst("runtime", "a text comparison");
                float l = Num(left);
                float r = Num(right);
                return Bool(b.Op switch { "<" => l < r, "<=" => l <= r, ">" => l > r, _ => l >= r });
            }
        }
        throw new InvalidOperationException($"unknown binary operator {b.Op}");
    }

    private static float Num(DmValue v) => v is DmNum n ? n.Value : 0f;

    private static bool DmEquals(DmValue a, DmValue b) => (a, b) switch
    {
        (DmNum x, DmNum y) => x.Value == y.Value,
        (DmText x, DmText y) => string.Equals(x.Value, y.Value, StringComparison.Ordinal),
        (DmNull, DmNull) => true,
        _ => false,
    };
}
