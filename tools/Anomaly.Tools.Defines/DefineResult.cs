namespace Anomaly.Tools.Defines;

/// <summary>What the converter made of one <c>#define</c>.</summary>
public sealed class DefineResult
{
    public DefineResult(MacroDefinition definition)
    {
        Definition = definition;
    }

    public MacroDefinition Definition { get; }

    public string Name => Definition.Name;

    /// <summary>
    /// The body kind: <c>int</c>, <c>float</c>, <c>string</c>, <c>unit</c> (postfix unit), <c>function</c>,
    /// <c>empty</c>, <c>poison</c>, <c>list</c>, <c>typepath</c>, <c>type_var</c>, <c>resource</c>, <c>runtime</c>,
    /// <c>unresolved</c>, <c>location</c>, <c>statement</c>, <c>comma_splice</c>, <c>fragment</c>, <c>error</c>,
    /// <c>text_macro</c>, <c>text_format</c>, <c>null</c>, <c>inactive</c>, <c>superseded</c>.
    /// </summary>
    public string Kind { get; set; }

    public bool Emitted { get; set; }

    /// <summary>For an emitted define: <c>int</c>, <c>float</c>, <c>string</c> or <c>method</c>.</summary>
    public string CsType { get; set; }

    /// <summary>For an emitted constant: the C# literal; for a method: its expression body.</summary>
    public string CsValue { get; set; }

    /// <summary>For an emitted constant: the value as <see cref="int"/>, <see cref="float"/> or <see cref="string"/>.</summary>
    public object Value { get; set; }

    /// <summary>For an emitted method: the C# signature (<c>string span_notice(string str)</c>).</summary>
    public string CsSignature { get; set; }

    /// <summary>Why the define is not emitted.</summary>
    public string Reason { get; set; }

    /// <summary>Hazards and remarks for translators (unparenthesised body, file-local, typo, ...).</summary>
    public List<string> Notes { get; } = new();

    /// <summary>For a function-like macro: traits that decide its translation (translation-rules.md 11.1).</summary>
    public List<string> Features { get; } = new();

    /// <summary>
    /// For a define tg indents under a signal: that signal. tg indents a signal's return values there
    /// (<c>COMPONENT_MOVABLE_BLOCK_PRE_MOVE</c>) and also the indexes of its argument list
    /// (<c>HEARING_RAW_MESSAGE</c>, <c>SPEECH_SPANS</c>), so this does not say which of the two a define is.
    /// </summary>
    public string IndentedUnder { get; set; }

    /// <summary>For a signal: the defines tg indents under it (return values or argument indexes).</summary>
    public List<string> IndentedDefines { get; } = new();

    /// <summary>The C# member name (the DM name, with <c>@</c> for a C# keyword).</summary>
    public string CsName { get; set; }
}
