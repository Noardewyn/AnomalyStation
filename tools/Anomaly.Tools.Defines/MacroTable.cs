namespace Anomaly.Tools.Defines;

/// <summary>
/// Every active macro definition with its lifetime. DM expands a macro where it is used, with the definitions live
/// there, so a define may refer to names defined later in the include order. The lifetime of a definition runs from
/// its directive (<see cref="MacroDefinition.Seq"/>) to its <c>#undef</c> or redefinition
/// (<see cref="MacroDefinition.EndSeq"/>).
/// </summary>
public sealed class MacroTable
{
    private readonly Dictionary<string, List<MacroDefinition>> _byName = new(StringComparer.Ordinal);

    /// <summary>The position after all directives: what a use outside <c>code/__DEFINES</c> sees.</summary>
    public const int End = int.MaxValue;

    public void Add(MacroDefinition definition)
    {
        if (!_byName.TryGetValue(definition.Name, out List<MacroDefinition> list))
            _byName[definition.Name] = list = new List<MacroDefinition>(1);
        list.Add(definition);
    }

    /// <summary>The definition of <paramref name="name"/> that is live at <paramref name="position"/>, or null.</summary>
    public MacroDefinition Lookup(string name, int position)
    {
        if (!_byName.TryGetValue(name, out List<MacroDefinition> list))
            return null;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            MacroDefinition d = list[i];
            if (d.Seq < position && position <= d.EndSeq)
                return d;
        }
        return null;
    }

    /// <summary>The definition currently live while the preprocessor runs (the last one not ended).</summary>
    public MacroDefinition Current(string name)
    {
        if (!_byName.TryGetValue(name, out List<MacroDefinition> list))
            return null;
        MacroDefinition last = list[^1];
        return last.EndSeq == End ? last : null;
    }
}
