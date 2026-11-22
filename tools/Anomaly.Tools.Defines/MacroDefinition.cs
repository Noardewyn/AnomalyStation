namespace Anomaly.Tools.Defines;

/// <summary>One <c>#define</c> the preprocessor saw (active or not), or a predefined macro.</summary>
public sealed class MacroDefinition
{
    public MacroDefinition(string name, List<string> parameters, bool variadic, List<Token> body)
    {
        Name = name;
        Parameters = parameters;
        IsVariadic = variadic;
        Body = body;
    }

    public string Name { get; }

    /// <summary>Parameter names of a function-like macro (the variadic one without its <c>...</c>); null for an object-like macro.</summary>
    public List<string> Parameters { get; }

    public bool IsFunctionLike => Parameters != null;

    /// <summary>The last parameter takes all remaining arguments (<c>name...</c> or <c>...</c>).</summary>
    public bool IsVariadic { get; }

    /// <summary>The replacement tokens: comments removed, line splices and blank runs reduced to one whitespace token, trimmed.</summary>
    public List<Token> Body { get; }

    /// <summary>The body as written, with blank runs collapsed to one space.</summary>
    public string BodyText => Token.JoinText(Body);

    /// <summary>Path relative to the tg root with forward slashes (<c>code/__DEFINES/_flags.dm</c>); null for a predefined macro.</summary>
    public string File { get; init; }

    public int Line { get; init; }

    /// <summary>The directive is indented (tg indents the return values and argument indexes of a signal under it).</summary>
    public bool Indented { get; init; }

    /// <summary>False when the directive sits in a branch the production configuration skips.</summary>
    public bool Active { get; init; }

    /// <summary>For an inactive definition: the conditional directive that skipped it.</summary>
    public string InactiveCondition { get; init; }

    /// <summary>A predefined macro (BYOND stddef constant or a configuration symbol), not seen in a tg file.</summary>
    public bool IsPredefined { get; init; }

    /// <summary>The dmdoc comments above the directive and the comment on its line, markers stripped.</summary>
    public string Doc { get; init; }

    /// <summary>Position of the directive in preprocessing order; the definition is live after it.</summary>
    public int Seq { get; init; }

    /// <summary>Position of the <c>#undef</c> or the redefinition that ends this definition; <see cref="int.MaxValue"/> while live.</summary>
    public int EndSeq { get; set; } = int.MaxValue;

    /// <summary>What ended the definition: <c>#undef at file:line</c> or <c>redefined at file:line</c>.</summary>
    public string EndReason { get; set; }

    /// <summary>The definition that replaced this one without an <c>#undef</c>.</summary>
    public MacroDefinition SupersededBy { get; set; }

    /// <summary>Index among all definitions the preprocessor saw, in order.</summary>
    public int Index { get; init; }

    public string Location => File == null ? "(predefined)" : $"{File}:{Line}";

    public override string ToString() => $"{Name} @ {Location}";
}
