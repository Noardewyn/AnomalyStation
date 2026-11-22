using System.Globalization;
using System.Text;

namespace Anomaly.Tools.Defines;

/// <summary>
/// Decides, for every define the preprocessor saw, what it is (by its body, never by its name or file) and whether
/// it becomes a C# constant. An object-like define is expanded with the macros live where it would be used (the end
/// of the include order, or its own <c>#undef</c> for a file-local one), parsed with DM precedence and folded with
/// DM semantics (<see cref="ConstFolder"/>). A number is a <c>const int</c> when it is an integer in DM's exact
/// range (|v| &lt;= 2^24) and a <c>const float</c> otherwise; text is a <c>const string</c>. A postfix unit
/// (<c>SECONDS *10</c>) is the constant of its multiplier. Function-like macros are listed for hand translation,
/// except the trivially safe <c>span_*</c> text builders, which become <c>GlobalProcs</c> methods.
/// </summary>
public sealed class Classifier
{
    private static readonly HashSet<string> StatementWords = new(StringComparer.Ordinal)
    {
        "var", "if", "else", "for", "while", "do", "return", "switch", "spawn", "del", "set", "goto", "break", "continue",
        "throw", "try", "catch",
    };

    private static readonly HashSet<string> BinaryOperators = new(StringComparer.Ordinal)
    {
        "+", "-", "*", "/", "%", "%%", "**", "&", "|", "^", "<<", ">>", "==", "!=", "<>", "~=", "~!", "<", "<=", ">", ">=",
        "&&", "||", "?", ":", "in",
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while",
    };

    private static readonly HashSet<string> ObjectMembers = new(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "GetType", "ToString", "ReferenceEquals", "MemberwiseClone", "Finalize", "Defines", "GlobalProcs",
    };

    private readonly MacroExpander _expander;

    public Classifier(MacroTable table)
    {
        _table = table;
        _expander = new MacroExpander(table);
    }

    private readonly MacroTable _table;

    /// <summary>Shift errors and other problems that make the output wrong; the tool refuses to write then.</summary>
    public List<string> Errors { get; } = new();

    public List<string> Warnings { get; } = new();

    public List<DefineResult> ClassifyAll(IEnumerable<MacroDefinition> definitions)
    {
        var results = definitions.Select(Classify).ToList();
        ResolveDuplicates(results);
        MarkIndentedUnderSignals(results);
        return results;
    }

    public DefineResult Classify(MacroDefinition d)
    {
        var r = new DefineResult(d) { CsName = CsName(d.Name) };
        if (!d.Active)
        {
            r.Kind = "inactive";
            r.Reason = $"in a branch the production build skips: {d.InactiveCondition}";
            return r;
        }
        if (d.SupersededBy != null)
        {
            r.Kind = "superseded";
            r.Reason = $"redefined without #undef at {d.SupersededBy.Location}; the later definition counts";
            return r;
        }
        if (d.IsFunctionLike)
            ClassifyFunction(d, r);
        else
            ClassifyObject(d, r);
        if (d.EndReason != null && d.EndReason.StartsWith("#undef", StringComparison.Ordinal))
            r.Notes.Add($"file-local in tg: {d.EndReason}");
        if (r.Emitted && ObjectMembers.Contains(d.Name))
        {
            r.Emitted = false;
            r.Reason = $"the name {d.Name} clashes with a member of System.Object or a generated class";
        }
        return r;
    }

    public static string CsName(string name) => CSharpKeywords.Contains(name) ? "@" + name : name;

    private void ClassifyObject(MacroDefinition d, DefineResult r)
    {
        if (d.Name == "FILE_DIR")
        {
            r.Kind = "empty";
            r.Reason = "FILE_DIR adds a resource search path for the compiler (BYOND ref \"FILE_DIR definition\"); not a value";
            return;
        }
        if (d.Body.Count == 0)
        {
            r.Kind = "empty";
            r.Reason = "an empty body: a flag for #ifdef, or a marker that expands to nothing";
            return;
        }
        Token directive = d.Body.FirstOrDefault(t => t.Kind == TokenKind.Directive);
        if (directive != null)
        {
            r.Kind = "poison";
            r.Reason = $"the body holds #{directive.Name}: any use is a compile error in tg ({directive.Rest})";
            return;
        }

        ExpansionResult expansion = _expander.Expand(d.Body, d.EndSeq, d.Name);
        if (expansion.Errors.Count > 0)
        {
            r.Kind = "fragment";
            r.Reason = string.Join("; ", expansion.Errors);
            return;
        }
        List<Token> tokens = expansion.Tokens;
        if (tokens.Count == 0)
        {
            r.Kind = "empty";
            r.Reason = $"expands to nothing in the production build ({d.BodyText})";
            return;
        }

        var ctx = new FoldContext();
        bool unit = tokens[0].IsPunct("*") || tokens[0].IsPunct("/") && tokens.Count > 1 && tokens[1].Kind == TokenKind.Number;
        if (unit)
        {
            // A postfix unit: '5 SECONDS' is '5 *10' (translation-rules.md 11.2). Its constant is the multiplier.
            r.Kind = "unit";
            var withOne = new List<Token> { new(TokenKind.Number, "1", d.Line) };
            withOne.AddRange(tokens);
            DmValue unitValue = Fold(withOne, ctx, out string unitError);
            Collect(d, ctx);
            if (unitValue is DmNum n)
            {
                SetNumber(r, n.Value);
                r.Kind = "unit";
                r.Notes.Add($"postfix unit: 'x {d.Name}' is 'x * {d.Name}' at the same place, no parentheses added (translation-rules.md 11.2)");
            }
            else
            {
                r.Reason = $"a run-time unit: {unitError ?? (unitValue as DmNotConst)?.Reason}";
            }
            return;
        }

        if (tokens[0].IsPunct("="))
        {
            r.Notes.Add("tg typo: the body starts with '='; the value after it is emitted (the macro is unusable as a value in DM)");
            tokens = tokens.Skip(1).ToList();
        }

        DmValue value = Fold(tokens, ctx, out string syntaxError);
        Collect(d, ctx);
        if (syntaxError != null)
        {
            r.Kind = ClassifyUnparsable(tokens);
            r.Reason = $"not an expression: {syntaxError}";
            return;
        }
        switch (value)
        {
            case DmNum n:
                SetNumber(r, n.Value);
                if (HasTopLevelOperator(tokens))
                    r.Notes.Add("unparenthesised body: tg's textual expansion binds differently next to a stronger operator; check each use (translation-rules.md 4.11)");
                break;
            case DmText t:
                r.Kind = "string";
                r.Emitted = true;
                r.CsType = "string";
                r.Value = t.Value;
                r.CsValue = CSharpText.Literal(t.Value);
                break;
            case DmNull:
                r.Kind = "null";
                r.Reason = "the value is null: write null at the use site";
                break;
            case DmNotConst nc:
                r.Kind = nc.Kind;
                r.Reason = nc.Reason;
                break;
        }
    }

    private void Collect(MacroDefinition d, FoldContext ctx)
    {
        foreach (string w in ctx.Warnings)
            Warnings.Add($"{d.Location} {d.Name}: {w}");
        foreach (string e in ctx.Errors)
            Errors.Add($"{d.Location} {d.Name}: {e}");
    }

    private static DmValue Fold(List<Token> tokens, FoldContext ctx, out string syntaxError)
    {
        syntaxError = null;
        try
        {
            return ConstFolder.Evaluate(ExprParser.Parse(tokens), ctx);
        }
        catch (DmSyntaxException ex)
        {
            syntaxError = ex.Message;
            return null;
        }
    }

    private static void SetNumber(DefineResult r, float v)
    {
        r.Emitted = true;
        if (DmNumbers.IsExactInteger(v))
        {
            int i = (int)v;
            r.Kind = "int";
            r.CsType = "int";
            r.Value = i;
            r.CsValue = i.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            r.Kind = "float";
            r.CsType = "float";
            r.Value = v;
            r.CsValue = CSharpText.FloatLiteral(v);
        }
    }

    private static string ClassifyUnparsable(List<Token> tokens)
    {
        if (tokens.Any(t => t.IsPunct(";")))
            return "statement";
        if (tokens[0].Kind == TokenKind.Identifier && StatementWords.Contains(tokens[0].Text))
            return "statement";
        int depth = 0;
        foreach (Token t in tokens)
        {
            if (t.IsPunct("(") || t.IsPunct("[") || t.IsPunct("{"))
                depth++;
            else if (t.IsPunct(")") || t.IsPunct("]") || t.IsPunct("}"))
                depth--;
            else if (depth == 0 && t.IsPunct(","))
                return "comma_splice";
        }
        if (tokens.Any(t => t.Kind == TokenKind.Identifier && StatementWords.Contains(t.Text)))
            return "statement";
        return "fragment";
    }

    /// <summary>True when the expanded body has a binary operator outside every bracket.</summary>
    private static bool HasTopLevelOperator(List<Token> tokens)
    {
        int depth = 0;
        Token previous = null;
        foreach (Token t in tokens)
        {
            if (t.IsPunct("(") || t.IsPunct("["))
                depth++;
            else if (t.IsPunct(")") || t.IsPunct("]"))
                depth--;
            else if (depth == 0 && previous != null && IsOperand(previous)
                     && (t.Kind == TokenKind.Punct && BinaryOperators.Contains(t.Text) || t.IsIdentifier("in")))
                return true;
            previous = t;
        }
        return false;
    }

    private static bool IsOperand(Token t) =>
        t.Kind is TokenKind.Identifier or TokenKind.Number or TokenKind.String || t.IsPunct(")") || t.IsPunct("]");

    private void ClassifyFunction(MacroDefinition d, DefineResult r)
    {
        r.Kind = "function";
        if (d.IsVariadic)
            r.Features.Add("variadic");
        if (d.Body.Any(t => t.Kind == TokenKind.Stringify))
            r.Features.Add("stringify");
        if (d.Body.Any(t => t.Kind == TokenKind.Concat))
            r.Features.Add("paste");
        // What the macro expands to where it is used: its own body and, because DM rescans the expansion, the
        // bodies of the macros it names. QDEL_IN (qdel.dm:59) calls addtimer, which is
        // _addtimer(args, file = __FILE__, line = __LINE__) (subsystems.dm:218), so QDEL_IN takes the use site's
        // location too (translation-rules.md 11.1 (c), (d), (f)). Parameters are placeholders, not names.
        var expansion = new ExpansionTraits();
        CollectTraits(d.Body, d.Parameters, d.EndSeq, new HashSet<string>(StringComparer.Ordinal) { d.Name }, expansion);
        if (expansion.Identifiers.Overlaps(LocationNames))
            r.Features.Add("location");
        if (expansion.Identifiers.Contains("src"))
            r.Features.Add("src");
        if (expansion.Identifiers.Contains("usr"))
            r.Features.Add("usr");
        if (expansion.CallerDot)
            r.Features.Add("dot");
        if (expansion.Identifiers.Contains("return"))
            r.Features.Add("return");
        if (expansion.Semicolon || expansion.Identifiers.Overlaps(StatementWords))
            r.Features.Add("statement");
        if (d.Body.Count == 0)
        {
            r.Reason = "a function-like macro that expands to nothing in the production build (an annotation; translation-rules.md 7.9)";
            return;
        }
        if (d.Name.StartsWith("span_", StringComparison.Ordinal) && TrySpanMethod(d, r))
            return;
        r.Reason = "a function-like macro: hand-expanded inline or a GlobalProcs member (translation-rules.md 11.1)";
    }

    private static readonly string[] LocationNames = { "__FILE__", "__LINE__", "__PROC__", "__TYPE__" };

    private sealed class ExpansionTraits
    {
        public HashSet<string> Identifiers { get; } = new(StringComparer.Ordinal);

        public bool Semicolon { get; set; }

        /// <summary>The expansion uses the caller's <c>.</c> (its return value: <c>. += x</c>, <c>return .</c>).</summary>
        public bool CallerDot { get; set; }
    }

    /// <summary>
    /// Collects the names, <c>;</c> and caller <c>.</c> of <paramref name="body"/> (embedded text expressions
    /// included) and, for each name that is a macro live at <paramref name="position"/> and would expand there (an
    /// object-like one, or a function-like one followed by <c>(</c>), of that macro's body, once per macro.
    /// </summary>
    private void CollectTraits(IReadOnlyList<Token> body, List<string> parameters, int position, HashSet<string> expanded, ExpansionTraits traits)
    {
        List<Token> tokens = body.Where(t => !t.IsTrivia && t.Kind != TokenKind.Newline).ToList();
        for (int i = 0; i < tokens.Count; i++)
        {
            Token t = tokens[i];
            if (t.Kind == TokenKind.String && t.Parts != null)
            {
                foreach (StringPart p in t.Parts.Where(p => p.IsExpression))
                    CollectTraits(p.Expression, parameters, position, expanded, traits);
                continue;
            }
            if (t.IsPunct(";"))
                traits.Semicolon = true;
            else if (t.IsPunct(".") && IsCallerDot(tokens, i))
                traits.CallerDot = true;
            if (t.Kind != TokenKind.Identifier || parameters != null && parameters.Contains(t.Text))
                continue;
            traits.Identifiers.Add(t.Text);
            MacroDefinition nested = _table.Lookup(t.Text, position);
            if (nested == null || expanded.Contains(nested.Name))
                continue;
            if (nested.IsFunctionLike && !(i + 1 < tokens.Count && tokens[i + 1].IsPunct("(")))
                continue; // a function-like macro name without '(' is not expanded
            expanded.Add(nested.Name);
            CollectTraits(nested.Body, nested.Parameters, position, expanded, traits);
        }
    }

    /// <summary>
    /// A <c>.</c> that is a value of its own, the caller's return value (<c>. += x</c>, <c>return .</c>): not a
    /// member access (<c>a.b</c>), a relative path (<c>.proc/x</c>) or a parent call (<c>..()</c>).
    /// </summary>
    private static bool IsCallerDot(List<Token> tokens, int i)
    {
        Token previous = i > 0 ? tokens[i - 1] : null;
        Token next = i + 1 < tokens.Count ? tokens[i + 1] : null;
        if (previous != null && (previous.IsPunct(".") || IsOperand(previous) && !previous.IsIdentifier("return")))
            return false;
        return next == null || !(next.IsPunct(".") || next.IsPunct("/") || next.Kind == TokenKind.Identifier);
    }

    /// <summary>
    /// A <c>span_*</c> macro whose body is text literals and parameters joined by <c>+</c>, each parameter used
    /// once, is the C# method <c>string span_x(string p) =&gt; "..." + p + "...";</c>. C# <c>+</c> on strings is DM's
    /// text <c>+</c> for text and null arguments; a number argument does not compile, where DM raises a type mismatch.
    /// </summary>
    private bool TrySpanMethod(MacroDefinition d, DefineResult r)
    {
        List<Token> body = d.Body.Where(t => !t.IsTrivia).ToList();
        if (body.Count >= 2 && body[0].IsPunct("(") && body[^1].IsPunct(")"))
            body = body.GetRange(1, body.Count - 2);
        if (body.Count == 0 || body.Count % 2 == 0 || d.IsVariadic)
            return false;
        var used = new List<string>();
        var csharp = new StringBuilder();
        for (int i = 0; i < body.Count; i++)
        {
            Token t = body[i];
            if (i % 2 == 1)
            {
                if (!t.IsPunct("+"))
                    return false;
                csharp.Append(" + ");
                continue;
            }
            if (t.Kind == TokenKind.Identifier && d.Parameters.Contains(t.Text))
            {
                if (used.Contains(t.Text))
                    return false;
                used.Add(t.Text);
                csharp.Append(CsName(t.Text));
                continue;
            }
            if (t.Kind != TokenKind.String || t.StringKind != StringKind.Normal || t.Parts.Count != 1)
                return false;
            string text = DmTextRules.Decode(t.Parts[0].Literal, out _);
            if (text == null)
                return false;
            csharp.Append(CSharpText.Literal(text));
        }
        if (used.Count != d.Parameters.Count || used.Count == 0)
            return false;
        r.Emitted = true;
        r.CsType = "method";
        r.CsValue = csharp.ToString();
        r.CsSignature = $"string {CsName(d.Name)}({string.Join(", ", d.Parameters.Select(p => "string " + CsName(p)))})";
        return true;
    }

    /// <summary>
    /// A name defined more than once in active code (a file-local define that is <c>#undef</c>ed and defined
    /// again) is emitted once: the definition live at the end, else the last one.
    /// </summary>
    private static void ResolveDuplicates(List<DefineResult> results)
    {
        foreach (IGrouping<string, DefineResult> group in results.Where(r => r.Emitted).GroupBy(r => r.Name, StringComparer.Ordinal))
        {
            if (group.Count() < 2)
                continue;
            DefineResult keep = group.FirstOrDefault(r => r.Definition.EndSeq == MacroTable.End) ?? group.Last();
            foreach (DefineResult other in group.Where(r => r != keep))
            {
                other.Emitted = false;
                other.Kind = "superseded";
                other.Reason = $"another definition of the same name is emitted ({keep.Definition.Location})";
            }
        }
    }

    /// <summary>
    /// tg indents the defines that belong to a signal right under it: its return values (bit flags such as
    /// <c>COMPONENT_MOVABLE_BLOCK_PRE_MOVE</c>, <c>NONE</c>, aliases) and, for some signals, the indexes of its
    /// argument list (<c>HEARING_RAW_MESSAGE 3</c>, <c>SPEECH_SPANS 3</c>). Record which signal each one is under;
    /// telling the two apart needs the define's own doc.
    /// </summary>
    private static void MarkIndentedUnderSignals(List<DefineResult> results)
    {
        DefineResult signal = null;
        string file = null;
        foreach (DefineResult r in results)
        {
            MacroDefinition d = r.Definition;
            if (!d.Active)
                continue;
            if (d.File != file)
            {
                file = d.File;
                signal = null;
            }
            if (!d.Indented)
            {
                signal = d.Name.StartsWith("COMSIG_", StringComparison.Ordinal) && (r.Kind == "string" || d.IsFunctionLike) ? r : null;
                continue;
            }
            if (signal != null)
            {
                r.IndentedUnder = signal.Name;
                signal.IndentedDefines.Add(r.Name);
            }
        }
    }
}
