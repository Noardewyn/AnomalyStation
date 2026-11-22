namespace Anomaly.Tools.Defines;

/// <summary>A DM expression tree, as far as constant folding needs it.</summary>
public abstract class Expr
{
}

public sealed class NumberExpr : Expr
{
    public NumberExpr(float value, string text)
    {
        Value = value;
        Text = text;
    }

    public float Value { get; }

    public string Text { get; }
}

public sealed class TextExpr : Expr
{
    public TextExpr(Token token)
    {
        Token = token;
    }

    public Token Token { get; }
}

public sealed class NullExpr : Expr
{
}

public sealed class IdentifierExpr : Expr
{
    public IdentifierExpr(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

/// <summary>A typepath literal: <c>/obj/item</c>, <c>/proc/x</c>, <c>.proc/x</c>.</summary>
public sealed class PathExpr : Expr
{
    public PathExpr(string path)
    {
        Path = path;
    }

    public string Path { get; }
}

public sealed class UnaryExpr : Expr
{
    public UnaryExpr(string op, Expr operand)
    {
        Op = op;
        Operand = operand;
    }

    public string Op { get; }

    public Expr Operand { get; }
}

public sealed class BinaryExpr : Expr
{
    public BinaryExpr(string op, Expr left, Expr right)
    {
        Op = op;
        Left = left;
        Right = right;
    }

    public string Op { get; }

    public Expr Left { get; }

    public Expr Right { get; }
}

public sealed class TernaryExpr : Expr
{
    public TernaryExpr(Expr condition, Expr whenTrue, Expr whenFalse)
    {
        Condition = condition;
        WhenTrue = whenTrue;
        WhenFalse = whenFalse;
    }

    public Expr Condition { get; }

    public Expr WhenTrue { get; }

    public Expr WhenFalse { get; }
}

public sealed class CallExpr : Expr
{
    public CallExpr(Expr callee, List<Expr> arguments)
    {
        Callee = callee;
        Arguments = arguments;
    }

    public Expr Callee { get; }

    public List<Expr> Arguments { get; }
}

/// <summary><c>a.b</c>, <c>a:b</c>, <c>a?.b</c>, <c>a?:b</c>, <c>A::b</c>.</summary>
public sealed class MemberExpr : Expr
{
    public MemberExpr(Expr target, string op, string member)
    {
        Target = target;
        Op = op;
        Member = member;
    }

    public Expr Target { get; }

    public string Op { get; }

    public string Member { get; }
}

public sealed class IndexExpr : Expr
{
    public IndexExpr(Expr target, Expr index)
    {
        Target = target;
        Index = index;
    }

    public Expr Target { get; }

    public Expr Index { get; }
}

public sealed class NewExpr : Expr
{
    public NewExpr(Expr type, List<Expr> arguments)
    {
        Type = type;
        Arguments = arguments;
    }

    public Expr Type { get; }

    public List<Expr> Arguments { get; }
}

/// <summary>An assignment (<c>=</c>, <c>+=</c>, ...), also a named argument <c>name = value</c>.</summary>
public sealed class AssignExpr : Expr
{
    public AssignExpr(string op, Expr target, Expr value)
    {
        Op = op;
        Target = target;
        Value = value;
    }

    public string Op { get; }

    public Expr Target { get; }

    public Expr Value { get; }
}

/// <summary><c>a in b</c> and <c>a in x to y</c>.</summary>
public sealed class InExpr : Expr
{
    public InExpr(Expr item, Expr container)
    {
        Item = item;
        Container = container;
    }

    public Expr Item { get; }

    public Expr Container { get; }
}

public sealed class DmSyntaxException : Exception
{
    public DmSyntaxException(string message)
        : base(message)
    {
    }
}
