using System.Collections.Immutable;

namespace Anomaly.Tools.Defines;

public sealed class ExpansionResult
{
    /// <summary>The expanded tokens, whitespace and comments removed.</summary>
    public List<Token> Tokens { get; } = new();

    public List<string> Errors { get; } = new();

    /// <summary>Names of the macros that were expanded, in first-use order.</summary>
    public List<string> UsedMacros { get; } = new();
}

/// <summary>
/// Textual macro expansion as DM does it where a macro is used (BYOND ref "#define directive"; OpenDream
/// <c>DMPreprocessor.TryMacro</c> and <c>DMMacro.Expand</c>): an object-like macro is replaced by its body and the
/// result is rescanned; a function-like macro is replaced only when a <c>(</c> follows, its parameters are
/// replaced by the argument tokens as written (no pre-expansion; the rescan expands them), <c>#p</c> makes text of
/// an argument, <c>##p</c> pastes it onto the preceding identifier, and the last <c>p...</c> parameter takes all
/// remaining arguments, an empty <c>##p</c> also removing the comma before it. Embedded expressions of text
/// literals are expanded too (BYOND ref: "[BIG]" ... "does get processed for macro substitution"), text outside
/// them is not. A macro is not expanded again inside its own expansion (C's rule; DM has no self-referencing macro
/// in tg, this only guarantees termination).
/// </summary>
public sealed class MacroExpander
{
    private const int MaxSteps = 200_000;

    private readonly MacroTable _table;

    public MacroExpander(MacroTable table)
    {
        _table = table;
    }

    private readonly struct Item
    {
        public Item(Token token, ImmutableHashSet<string> hide)
        {
            Token = token;
            Hide = hide;
        }

        public Token Token { get; }

        public ImmutableHashSet<string> Hide { get; }
    }

    /// <summary>
    /// Expands <paramref name="input"/> with the definitions live at <paramref name="position"/>
    /// (<see cref="MacroTable.Lookup"/>); <paramref name="self"/> is not expanded (the macro being evaluated).
    /// </summary>
    public ExpansionResult Expand(IReadOnlyList<Token> input, int position, string self)
    {
        var result = new ExpansionResult();
        ImmutableHashSet<string> hide = self == null ? ImmutableHashSet<string>.Empty : ImmutableHashSet.Create(StringComparer.Ordinal, self);
        result.Tokens.AddRange(ExpandItems(input, hide, position, result));
        return result;
    }

    private List<Token> ExpandItems(IReadOnlyList<Token> input, ImmutableHashSet<string> hide, int position, ExpansionResult result)
    {
        var stack = new List<Item>(input.Count);
        for (int i = input.Count - 1; i >= 0; i--)
            stack.Add(new Item(input[i], hide));
        var output = new List<Token>();
        int steps = 0;
        while (stack.Count > 0)
        {
            if (++steps > MaxSteps)
            {
                result.Errors.Add("macro expansion does not terminate");
                break;
            }
            Item item = Pop(stack);
            Token t = item.Token;
            if (t.IsTrivia || t.Kind == TokenKind.Newline)
                continue;
            if (t.Kind == TokenKind.String && t.Parts != null && t.Parts.Any(p => p.IsExpression))
            {
                output.Add(ExpandString(t, item.Hide, position, result));
                continue;
            }
            if (t.Kind != TokenKind.Identifier || item.Hide.Contains(t.Text))
            {
                output.Add(t);
                continue;
            }
            MacroDefinition definition = _table.Lookup(t.Text, position);
            if (definition == null)
            {
                output.Add(t);
                continue;
            }
            ImmutableHashSet<string> inner = item.Hide.Add(definition.Name);
            if (!definition.IsFunctionLike)
            {
                Note(result, definition.Name);
                PushAll(stack, definition.Body.Select(b => new Item(b, inner)));
                continue;
            }

            // A function-like macro needs a '(' (blanks allowed before it); otherwise the name stays as it is.
            int k = stack.Count - 1;
            while (k >= 0 && (stack[k].Token.IsTrivia || stack[k].Token.Kind == TokenKind.Newline))
                k--;
            if (k < 0 || !stack[k].Token.IsPunct("("))
            {
                output.Add(t);
                continue;
            }
            Note(result, definition.Name);
            while (stack.Count - 1 >= k)
                Pop(stack);
            List<List<Item>> arguments = CollectArguments(stack, out bool closed);
            if (!closed)
                result.Errors.Add($"unterminated call of macro {definition.Name}");
            if (definition.Parameters.Count == 0 && arguments.Count == 1 && arguments[0].Count == 0)
                arguments.Clear();
            if (!definition.IsVariadic && arguments.Count > definition.Parameters.Count)
                result.Errors.Add($"macro {definition.Name} takes {definition.Parameters.Count} argument(s), given {arguments.Count}");
            PushAll(stack, Substitute(definition, arguments, inner));
        }
        return output;
    }

    private static void Note(ExpansionResult result, string name)
    {
        if (!result.UsedMacros.Contains(name))
            result.UsedMacros.Add(name);
    }

    private static Item Pop(List<Item> stack)
    {
        Item item = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return item;
    }

    private static void PushAll(List<Item> stack, IEnumerable<Item> items)
    {
        List<Item> list = items as List<Item> ?? items.ToList();
        for (int i = list.Count - 1; i >= 0; i--)
            stack.Add(list[i]);
    }

    private static List<List<Item>> CollectArguments(List<Item> stack, out bool closed)
    {
        var arguments = new List<List<Item>>();
        var current = new List<Item>();
        int depth = 0;
        closed = false;
        while (stack.Count > 0)
        {
            Item a = Pop(stack);
            Token t = a.Token;
            if (t.IsPunct("(") || t.IsPunct("["))
            {
                depth++;
            }
            else if (t.IsPunct(")") || t.IsPunct("]"))
            {
                if (depth == 0 && t.IsPunct(")"))
                {
                    closed = true;
                    break;
                }
                depth--;
            }
            else if (t.IsPunct(",") && depth == 0)
            {
                arguments.Add(Trim(current));
                current = new List<Item>();
                continue;
            }
            current.Add(a);
        }
        arguments.Add(Trim(current));
        return arguments;
    }

    private static List<Item> Trim(List<Item> items)
    {
        int start = 0;
        int end = items.Count;
        while (start < end && (items[start].Token.IsTrivia || items[start].Token.Kind == TokenKind.Newline))
            start++;
        while (end > start && (items[end - 1].Token.IsTrivia || items[end - 1].Token.Kind == TokenKind.Newline))
            end--;
        return items.GetRange(start, end - start);
    }

    private static List<Item> Substitute(MacroDefinition definition, List<List<Item>> arguments, ImmutableHashSet<string> hide)
    {
        var output = new List<Item>();
        int variadicIndex = definition.IsVariadic ? definition.Parameters.Count - 1 : -1;
        foreach (Token bt in definition.Body)
        {
            string name = bt.Kind is TokenKind.Concat or TokenKind.Stringify ? bt.Name : bt.Kind == TokenKind.Identifier ? bt.Text : null;
            int index = name == null ? -1 : definition.Parameters.IndexOf(name);
            if (bt.Kind == TokenKind.Concat)
                TrimEnd(output); // blanks before ## are dropped (OpenDream DMMacro constructor)
            if (index >= 0)
            {
                List<Item> argument = index == variadicIndex
                    ? JoinVariadic(arguments, index, hide)
                    : index < arguments.Count ? arguments[index] : new List<Item>();
                if (bt.Kind == TokenKind.Stringify)
                {
                    output.Add(new Item(MakeText(Token.JoinText(argument.Select(a => a.Token)), bt.Line), hide));
                    continue;
                }
                if (argument.Count == 0 && index == variadicIndex && bt.Kind == TokenKind.Concat)
                {
                    // BYOND ref "##var concatenation": an empty variadic replacement removes the blanks and the comma before it.
                    TrimEnd(output);
                    if (output.Count > 0 && output[^1].Token.IsPunct(","))
                        output.RemoveAt(output.Count - 1);
                    continue;
                }
                bool first = true;
                foreach (Item a in argument)
                {
                    if (first && bt.Kind == TokenKind.Concat && output.Count > 0 && output[^1].Token.Kind == TokenKind.Identifier
                        && a.Token.Kind is TokenKind.Identifier or TokenKind.Number)
                        output[^1] = new Item(new Token(TokenKind.Identifier, output[^1].Token.Text + a.Token.Text, bt.Line), hide);
                    else
                        output.Add(a);
                    first = false;
                }
                continue;
            }
            if (bt.Kind == TokenKind.Stringify)
            {
                output.Add(new Item(MakeText(bt.Name, bt.Line), hide));
                continue;
            }
            if (bt.Kind == TokenKind.String && bt.Parts != null && bt.Parts.Any(p => p.IsExpression))
            {
                output.Add(new Item(SubstituteInText(bt, definition, arguments, variadicIndex), hide));
                continue;
            }
            if (bt.Kind == TokenKind.Concat)
            {
                if (output.Count > 0 && output[^1].Token.Kind == TokenKind.Identifier)
                    output[^1] = new Item(new Token(TokenKind.Identifier, output[^1].Token.Text + bt.Name, bt.Line), hide);
                else
                    output.Add(new Item(new Token(TokenKind.Identifier, bt.Name, bt.Line), hide));
                continue;
            }
            output.Add(new Item(bt, hide));
        }
        return output;
    }

    /// <summary>
    /// Parameters named inside the embedded expressions of a text literal in the body are replaced too: DM lexes an
    /// embedded expression as ordinary tokens (OpenDream DMPreprocessorLexer.LexString), so tg's
    /// <c>ELEMENT_TRAIT(source) "element_trait_[source]"</c> pastes its argument into the text.
    /// </summary>
    private static Token SubstituteInText(Token text, MacroDefinition definition, List<List<Item>> arguments, int variadicIndex)
    {
        var parts = new List<StringPart>(text.Parts.Count);
        foreach (StringPart part in text.Parts)
        {
            if (!part.IsExpression)
            {
                parts.Add(part);
                continue;
            }
            var tokens = new List<Token>();
            foreach (Token t in part.Expression)
            {
                int index = t.Kind == TokenKind.Identifier ? definition.Parameters.IndexOf(t.Text) : -1;
                if (index < 0)
                {
                    tokens.Add(t.Kind == TokenKind.String && t.Parts != null && t.Parts.Any(p => p.IsExpression)
                        ? SubstituteInText(t, definition, arguments, variadicIndex)
                        : t);
                    continue;
                }
                List<Item> argument = index == variadicIndex
                    ? JoinVariadic(arguments, index, ImmutableHashSet<string>.Empty)
                    : index < arguments.Count ? arguments[index] : new List<Item>();
                tokens.AddRange(argument.Select(a => a.Token));
            }
            parts.Add(new StringPart(null, tokens));
        }
        return new Token(TokenKind.String, text.Text, text.Line) { StringKind = text.StringKind, Parts = parts };
    }

    private static List<Item> JoinVariadic(List<List<Item>> arguments, int index, ImmutableHashSet<string> hide)
    {
        var joined = new List<Item>();
        for (int i = index; i < arguments.Count; i++)
        {
            if (i > index)
                joined.Add(new Item(new Token(TokenKind.Punct, ",", 0), hide));
            joined.AddRange(arguments[i]);
        }
        return joined;
    }

    private static void TrimEnd(List<Item> output)
    {
        while (output.Count > 0 && output[^1].Token.IsTrivia)
            output.RemoveAt(output.Count - 1);
    }

    /// <summary>The text literal that <c>#p</c> makes: the argument as written, no escapes applied.</summary>
    public static Token MakeText(string value, int line) =>
        new(TokenKind.String, "\"" + value.Replace("\"", "\\\"") + "\"", line) { StringKind = StringKind.Raw, RawValue = value };

    private Token ExpandString(Token t, ImmutableHashSet<string> hide, int position, ExpansionResult result)
    {
        var parts = new List<StringPart>(t.Parts.Count);
        foreach (StringPart part in t.Parts)
        {
            if (!part.IsExpression)
            {
                parts.Add(part);
                continue;
            }
            parts.Add(new StringPart(null, ExpandItems(part.Expression, hide, position, result)));
        }
        return new Token(TokenKind.String, t.Text, t.Line) { StringKind = t.StringKind, Parts = parts };
    }
}
