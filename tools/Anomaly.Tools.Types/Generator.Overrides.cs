using System.Reflection;
using System.Text;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    /// <summary>The guard's table (types-converter.md 10), relative to the repository.</summary>
    public const string UndeclaredOverridesPath = TypesDir + "/UndeclaredOverrides.g.cs";

    /// <summary>
    /// The DM names the interaction guard covers: <c>Anomaly.Core.Tg.InertSeam.GuardedProcs</c>, read from the Tg assembly by
    /// reflection so that the table and the guard never differ (types-converter.md 10).
    /// </summary>
    private static IReadOnlyList<string> GuardedProcs()
    {
        Type seam = typeof(Anomaly.Core.Tg.datum).Assembly.GetType("Anomaly.Core.Tg.InertSeam")
            ?? throw new ToolException("Anomaly.Core.Tg.InertSeam is not in the Tg assembly");
        FieldInfo f = seam.GetField("GuardedProcs", BindingFlags.Public | BindingFlags.Static)
            ?? throw new ToolException("InertSeam.GuardedProcs is not declared");
        return ((IEnumerable<string>)f.GetValue(null)).ToList();
    }

    /// <summary>
    /// Types-converter.md 10 (M4a P1): every tg definition of a guarded proc at a type under <c>/atom</c> whose C# class
    /// does not declare it (section 4.1's "declared": the binder's names, an earlier duplicate <c>__d0</c> does not count, a
    /// live-partial stub counts), in <c>TypeOrder</c>'s path order, then by name; one row per (type, name).
    /// </summary>
    private void EmitUndeclaredOverrides(Outputs outputs)
    {
        var guarded = new HashSet<string>(GuardedProcs(), StringComparer.Ordinal);
        var rows = new List<(Node Node, string Proc, ProcDef Def)>();
        foreach (Node n in _preorder)
        {
            if (!n.HasClass || n.ClassName == null) continue;
            if (n.Path != "/atom" && !n.Ancestors().Any(a => a.Path == "/atom")) continue; // every atom: /area, /turf, /obj, /mob are children of /atom
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((ProcDef proc, Translation.State state, LivePartialRow row) in _procStates[n])
            {
                if (!guarded.Contains(proc.Name) || Translation.Counts(state) || row != null) continue;
                if (!seen.Add(proc.Name)) continue;
                rows.Add((n, proc.Name, proc));
            }
        }
        var sb = new StringBuilder();
        sb.Append(Header("the interaction guard's table of undeclared overrides (types-converter.md 10)"));
        sb.Append("using System.Collections.Frozen;\n\nnamespace Anomaly.Core.Tg;\n\n");
        sb.Append("/// <summary>\n");
        sb.Append("/// The tg definitions of the interaction chain's guarded procs (<see cref=\"InertSeam.GuardedProcs\"/>) at types whose C#\n");
        sb.Append("/// class does not declare them (docs/ai/specs/types-converter.md 10; D132, DV27): ").Append(rows.Count).Append(" rows over ")
            .Append(rows.Select(r => r.Node).Distinct().Count()).Append(" types.\n");
        sb.Append("/// <see cref=\"InertSeam.HasUndeclaredOverride\"/> walks a runtime class up to the class whose C# method would run.\n");
        sb.Append("/// </summary>\n");
        sb.Append("internal static partial class UndeclaredOverrides\n{\n");
        sb.Append("    // Built once on first use (the type initializer); frozen, so no mutable static state (NoStaticStateTests).\n");
        sb.Append("    private static readonly FrozenSet<(Type Type, string Proc)> Rows = Table().ToFrozenSet();\n\n");
        sb.Append("    /// <summary>Whether tg defines <paramref name=\"proc\"/> at the class <paramref name=\"type\"/> and the class does not declare it.</summary>\n");
        sb.Append("    public static bool Has(Type type, string proc) => Rows.Contains((type, proc));\n\n");
        sb.Append("    /// <summary>The number of rows (tests).</summary>\n");
        sb.Append("    public static int Count => Rows.Count;\n\n");
        sb.Append("    // One row per (type, DM proc name): TypeOrder's path order, then the name.\n");
        sb.Append("    private static (Type, string)[] Table() => new (Type, string)[]\n    {\n");
        var order = new Dictionary<Node, int>();
        for (int i = 0; i < _preorder.Count; i++) order[_preorder[i]] = i;
        foreach (var (node, proc, def) in rows.OrderBy(r => order[r.Node]).ThenBy(r => r.Proc, StringComparer.Ordinal))
            sb.Append("        (typeof(").Append(node.ClassName).Append("), ").Append(CSharpValues.Literal(proc)).Append("), // ")
                .Append(node.Path).Append(" (tg: ").Append(Loc(def.File, def.Line)).Append(")\n");
        sb.Append("    };\n}\n");
        outputs.Files[UndeclaredOverridesPath] = sb.ToString();
        Report.Add($"undeclared overrides: {rows.Count} rows over {rows.Select(r => r.Node).Distinct().Count()} types");
    }
}
