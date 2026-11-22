using System.Text;

namespace Anomaly.Tools.Defines;

/// <summary>
/// A DM preprocessor that runs the directives of <c>tgstation.dme</c> in include order, from the top of the
/// <c>.dme</c> to the last <c>code/__DEFINES</c> file (the prologue <c>__byond_version_compat.dm</c>,
/// <c>_compile_options.dm</c>, <c>_experiments.dm</c> and <c>world.dm</c> included). It records every
/// <c>#define</c> it sees, in active and in skipped branches, with its dmdoc comments; it evaluates
/// <c>#if</c>/<c>#ifdef</c>/<c>#ifndef</c>/<c>#elif</c>/<c>#else</c>/<c>#endif</c> against the production
/// configuration (<see cref="PreprocessorConfig"/>), applies <c>#undef</c> and redefinitions, and follows
/// <c>#include</c>. Code lines are not expanded: only the define bodies matter to the converter.
/// </summary>
public sealed class Preprocessor
{
    private sealed class Frame
    {
        public bool ParentActive;
        public bool Active;
        public bool Taken;
        public string Text;
    }

    private readonly string _root;
    private readonly PreprocessorConfig _config;
    private readonly List<Frame> _frames = new();
    private readonly MacroExpander _expander;
    private int _seq;
    private bool _seenDefinesInclude;
    private bool _stop;

    public Preprocessor(string tgRoot, PreprocessorConfig config)
    {
        _root = tgRoot;
        _config = config ?? PreprocessorConfig.Production;
        _expander = new MacroExpander(Table);
        AddPredefined();
    }

    public MacroTable Table { get; } = new();

    /// <summary>Every <c>#define</c> directive seen in a tg file, in preprocessing order.</summary>
    public List<MacroDefinition> Definitions { get; } = new();

    /// <summary>The compiler's own macros: BYOND stddef constants, <c>DM_VERSION</c>, <c>DM_BUILD</c>, <c>-D</c> symbols.</summary>
    public List<MacroDefinition> Predefined { get; } = new();

    /// <summary>Files read, relative to the tg root, in order.</summary>
    public List<string> Files { get; } = new();

    /// <summary>Includes that were not followed (maps, interface files, files after <c>code/__DEFINES</c>).</summary>
    public List<string> SkippedIncludes { get; } = new();

    public List<string> Diagnostics { get; } = new();

    /// <summary><c>#error</c> and <c>#warn</c> directives met in active code.</summary>
    public List<string> ActiveErrorsAndWarnings { get; } = new();

    private bool IsActive => _frames.Count == 0 || _frames[^1].Active;

    private void AddPredefined()
    {
        var bodies = new List<(string Name, string Body)>
        {
            ("DM_VERSION", _config.DmVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("DM_BUILD", _config.DmBuild.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        bodies.AddRange(ByondBuiltins.Constants);
        foreach (string symbol in _config.CommandLineSymbols)
            bodies.Add((symbol, ""));
        foreach ((string name, string body) in bodies)
            AddPredefined(name, null, body);
        foreach ((string name, string[] parameters, string body) in ByondBuiltins.FunctionMacros)
            AddPredefined(name, parameters.ToList(), body);
    }

    private void AddPredefined(string name, List<string> parameters, string body)
    {
        List<Token> tokens = DmLexer.Lex(body).Where(t => t.Kind != TokenKind.Newline).ToList();
        var definition = new MacroDefinition(name, parameters, false, tokens) { Active = true, IsPredefined = true, Seq = 0 };
        Predefined.Add(definition);
        Table.Add(definition);
    }

    /// <summary>Runs the preprocessor over the tg tree from <paramref name="dmeName"/>.</summary>
    public void Run(string dmeName = "tgstation.dme")
    {
        ProcessFile(dmeName, isDme: true);
        if (_frames.Count != 0)
            Diagnostics.Add($"{_frames.Count} conditional block(s) left open at the end");
    }

    /// <summary>Preprocesses one in-memory file (tests and snippets); <c>#include</c>s resolve against the tg root, if any.</summary>
    public void RunText(string relativePath, string text)
    {
        ProcessText(relativePath, text, isDme: false);
        if (_frames.Count != 0)
            Diagnostics.Add($"{_frames.Count} conditional block(s) left open at the end");
    }

    private void ProcessFile(string relativePath, bool isDme)
    {
        string full = Path.Combine(_root ?? "", relativePath);
        if (!System.IO.File.Exists(full))
        {
            Diagnostics.Add($"missing include {relativePath}");
            return;
        }
        ProcessText(relativePath, System.IO.File.ReadAllText(full, Encoding.UTF8), isDme);
    }

    private void ProcessText(string relativePath, string text, bool isDme)
    {
        Files.Add(relativePath);
        var lexDiagnostics = new List<string>();
        List<Token> tokens = DmLexer.Lex(text, lexDiagnostics);
        foreach (string d in lexDiagnostics)
            Diagnostics.Add($"{relativePath}: {d}");
        int framesAtStart = _frames.Count;
        ProcessTokens(tokens, relativePath, isDme);
        if (!_stop && _frames.Count != framesAtStart)
            Diagnostics.Add($"{relativePath}: unbalanced conditional directives");
    }

    private void ProcessTokens(List<Token> tokens, string file, bool isDme)
    {
        var pendingDoc = new List<string>();
        int i = 0;
        int n = tokens.Count;
        var line = new List<Token>();
        while (i < n && !_stop)
        {
            line.Clear();
            while (i < n && tokens[i].Kind != TokenKind.Newline)
                line.Add(tokens[i++]);
            if (i < n)
                i++; // the newline

            int k = 0;
            while (k < line.Count && line[k].Kind == TokenKind.Whitespace)
                k++;
            if (k == line.Count)
            {
                pendingDoc.Clear(); // a blank line ends a doc comment
                continue;
            }
            if (line[k].Kind == TokenKind.Directive)
            {
                HandleDirective(line[k], line.Skip(k + 1).ToList(), file, isDme, indented: k > 0, pendingDoc);
                pendingDoc.Clear();
                continue;
            }
            bool onlyComments = true;
            foreach (Token t in line)
            {
                if (t.Kind is TokenKind.Whitespace or TokenKind.LineSplice)
                    continue;
                if (t.Kind != TokenKind.Comment)
                {
                    onlyComments = false;
                    break;
                }
            }
            if (!onlyComments)
            {
                pendingDoc.Clear();
                continue;
            }
            foreach (Token t in line)
            {
                if (t.Kind != TokenKind.Comment)
                    continue;
                if (t.IsDocComment)
                    pendingDoc.Add(t.CommentBody);
                else
                    pendingDoc.Clear();
            }
        }
    }

    private void HandleDirective(Token directive, List<Token> rest, string file, bool isDme, bool indented, List<string> pendingDoc)
    {
        string where = $"{file}:{directive.Line}";
        switch (directive.Name)
        {
            case "if":
                PushFrame($"#if {Token.JoinText(rest)}", () => EvaluateCondition(rest, where));
                return;
            case "ifdef":
            {
                string name = FirstIdentifier(rest);
                PushFrame($"#ifdef {name}", () => Table.Lookup(name, _seq + 1) != null);
                return;
            }
            case "ifndef":
            {
                string name = FirstIdentifier(rest);
                PushFrame($"#ifndef {name}", () => Table.Lookup(name, _seq + 1) == null);
                return;
            }
            case "elif":
            {
                if (_frames.Count == 0)
                {
                    Diagnostics.Add($"{where}: #elif without #if");
                    return;
                }
                Frame f = _frames[^1];
                if (f.ParentActive && !f.Taken)
                {
                    f.Active = EvaluateCondition(rest, where);
                    f.Taken = f.Active;
                }
                else
                {
                    f.Active = false;
                }
                f.Text = $"#elif {Token.JoinText(rest)} (after {f.Text})";
                return;
            }
            case "else":
            {
                if (_frames.Count == 0)
                {
                    Diagnostics.Add($"{where}: #else without #if");
                    return;
                }
                Frame f = _frames[^1];
                f.Active = f.ParentActive && !f.Taken;
                f.Taken = true;
                f.Text = $"#else of {f.Text}";
                return;
            }
            case "endif":
                if (_frames.Count == 0)
                    Diagnostics.Add($"{where}: #endif without #if");
                else
                    _frames.RemoveAt(_frames.Count - 1);
                return;
            case "define":
                HandleDefine(directive, rest, file, indented, pendingDoc);
                return;
            case "undef":
            {
                if (!IsActive)
                    return;
                string name = FirstIdentifier(rest);
                MacroDefinition current = name == null ? null : Table.Current(name);
                int seq = ++_seq;
                if (current == null)
                {
                    Diagnostics.Add($"{where}: #undef {name} of a name that is not a macro here");
                    return;
                }
                current.EndSeq = seq;
                current.EndReason = $"#undef at {where}";
                return;
            }
            case "include":
                if (IsActive)
                    HandleInclude(rest, file, isDme, where);
                return;
            case "error":
            case "warn":
            case "warning":
                if (IsActive)
                    ActiveErrorsAndWarnings.Add($"{where}: #{directive.Name} {directive.Rest}");
                return;
            case "pragma":
                return;
            default:
                Diagnostics.Add($"{where}: unknown directive #{directive.Name}");
                return;
        }
    }

    private void PushFrame(string text, Func<bool> condition)
    {
        bool parentActive = IsActive;
        bool active = parentActive && condition();
        _frames.Add(new Frame { ParentActive = parentActive, Active = active, Taken = !parentActive || active, Text = text });
    }

    private string InactiveCondition()
    {
        foreach (Frame f in _frames)
        {
            if (!f.Active)
                return f.Text;
        }
        return null;
    }

    private static string FirstIdentifier(List<Token> tokens) => tokens.FirstOrDefault(t => t.Kind == TokenKind.Identifier)?.Text;

    private bool EvaluateCondition(List<Token> rest, string where)
    {
        // defined(X) and defined X are resolved before macro expansion (C and DM).
        var tokens = new List<Token>();
        List<Token> significant = rest.Where(t => !t.IsTrivia).ToList();
        for (int i = 0; i < significant.Count; i++)
        {
            Token t = significant[i];
            if (t.IsIdentifier("defined"))
            {
                string name = null;
                if (i + 3 < significant.Count && significant[i + 1].IsPunct("(") && significant[i + 3].IsPunct(")"))
                {
                    name = significant[i + 2].Text;
                    i += 3; // defined ( X )
                }
                else if (i + 1 < significant.Count)
                {
                    name = significant[i + 1].Text;
                    i += 1;
                }
                bool isDefined = name != null && Table.Lookup(name, _seq + 1) != null;
                tokens.Add(new Token(TokenKind.Number, isDefined ? "1" : "0", t.Line));
                continue;
            }
            tokens.Add(t);
        }
        ExpansionResult expanded = _expander.Expand(tokens, _seq + 1, null);
        try
        {
            Expr expr = ExprParser.Parse(expanded.Tokens);
            DmValue value = ConstFolder.Evaluate(expr, new FoldContext { UnknownIdentifiersAreZero = true });
            if (value is DmNum num)
                return num.Value != 0;
            if (value is DmText text)
                return text.Value.Length != 0;
            Diagnostics.Add($"{where}: #if condition is not constant ({(value as DmNotConst)?.Reason}); taken as false");
            return false;
        }
        catch (DmSyntaxException e)
        {
            Diagnostics.Add($"{where}: cannot parse #if condition: {e.Message}; taken as false");
            return false;
        }
    }

    private void HandleInclude(List<Token> rest, string file, bool isDme, string where)
    {
        Token pathToken = rest.FirstOrDefault(t => t.Kind == TokenKind.String);
        if (pathToken == null)
        {
            Diagnostics.Add($"{where}: #include without a file name");
            return;
        }
        string raw = pathToken.StringKind == StringKind.Raw ? pathToken.RawValue : string.Concat(pathToken.Parts.Where(p => !p.IsExpression).Select(p => p.Literal));
        string path = raw.Replace('\\', '/');
        string directory = Path.GetDirectoryName(file)?.Replace('\\', '/') ?? "";
        string relative = directory.Length == 0 ? path : $"{directory}/{path}";
        if (!System.IO.File.Exists(Path.Combine(_root ?? "", relative)))
            relative = path;
        relative = NormalizeRelative(relative);
        if (isDme)
        {
            bool isDefines = relative.StartsWith("code/__DEFINES/", StringComparison.Ordinal);
            if (_seenDefinesInclude && !isDefines)
            {
                SkippedIncludes.Add(relative);
                _stop = true; // everything after code/__DEFINES is outside the converter's scope
                return;
            }
            _seenDefinesInclude |= isDefines;
        }
        string extension = Path.GetExtension(relative).ToLowerInvariant();
        if (extension is not (".dm" or ".dme"))
        {
            SkippedIncludes.Add(relative);
            return;
        }
        ProcessFile(relative, isDme: false);
    }

    private static string NormalizeRelative(string path)
    {
        var parts = new List<string>();
        foreach (string part in path.Split('/'))
        {
            if (part.Length == 0 || part == ".")
                continue;
            if (part == ".." && parts.Count > 0)
                parts.RemoveAt(parts.Count - 1);
            else
                parts.Add(part);
        }
        return string.Join('/', parts);
    }

    private void HandleDefine(Token directive, List<Token> rest, string file, bool indented, List<string> pendingDoc)
    {
        string where = $"{file}:{directive.Line}";
        int j = 0;
        while (j < rest.Count && rest[j].IsTrivia)
            j++;
        if (j >= rest.Count || rest[j].Kind != TokenKind.Identifier)
        {
            Diagnostics.Add($"{where}: #define without a name");
            return;
        }
        string name = rest[j].Text;
        j++;
        List<string> parameters = null;
        bool variadic = false;
        if (j < rest.Count && rest[j].IsPunct("("))
        {
            // A '(' right after the name (no blank) makes a function-like macro.
            parameters = new List<string>();
            j++;
            while (j < rest.Count && !rest[j].IsPunct(")"))
            {
                Token t = rest[j];
                if (t.Kind == TokenKind.Identifier)
                {
                    parameters.Add(t.Text);
                    if (j + 3 < rest.Count && rest[j + 1].IsPunct(".") && rest[j + 2].IsPunct(".") && rest[j + 3].IsPunct("."))
                    {
                        variadic = true;
                        j += 3;
                    }
                }
                else if (t.IsPunct("."))
                {
                    if (j + 2 < rest.Count && rest[j + 1].IsPunct(".") && rest[j + 2].IsPunct("."))
                    {
                        parameters.Add("...");
                        variadic = true;
                        j += 2;
                    }
                }
                else if (!t.IsPunct(",") && !t.IsTrivia)
                {
                    Diagnostics.Add($"{where}: unexpected '{t.Text}' in the parameters of {name}");
                }
                j++;
            }
            j++; // ')'
        }

        // The body: comments become doc text, line splices and blank runs one whitespace token.
        var body = new List<Token>();
        var trailing = new List<string>();
        for (; j < rest.Count; j++)
        {
            Token t = rest[j];
            if (t.Kind == TokenKind.Comment)
            {
                string text = t.CommentBody.Trim();
                if (text.Length > 0)
                    trailing.Add(t.CommentBody);
                AppendBlank(body, t.Line);
                continue;
            }
            if (t.Kind is TokenKind.Whitespace or TokenKind.LineSplice)
            {
                AppendBlank(body, t.Line);
                continue;
            }
            body.Add(t);
        }
        while (body.Count > 0 && body[0].Kind == TokenKind.Whitespace)
            body.RemoveAt(0);
        while (body.Count > 0 && body[^1].Kind == TokenKind.Whitespace)
            body.RemoveAt(body.Count - 1);

        var docLines = new List<string>(pendingDoc);
        docLines.AddRange(trailing);
        bool active = IsActive;
        var definition = new MacroDefinition(name, parameters, variadic, body)
        {
            File = file,
            Line = directive.Line,
            Indented = indented,
            Active = active,
            InactiveCondition = active ? null : InactiveCondition(),
            Doc = DocText.Clean(docLines),
            Seq = ++_seq,
            Index = Definitions.Count,
        };
        Definitions.Add(definition);
        if (!active)
            return;
        MacroDefinition previous = Table.Current(name);
        if (previous != null)
        {
            previous.EndSeq = definition.Seq;
            previous.EndReason = $"redefined at {definition.Location}";
            previous.SupersededBy = definition;
        }
        Table.Add(definition);
    }

    private static void AppendBlank(List<Token> body, int line)
    {
        if (body.Count > 0 && body[^1].Kind == TokenKind.Whitespace)
            return;
        body.Add(new Token(TokenKind.Whitespace, " ", line));
    }
}
