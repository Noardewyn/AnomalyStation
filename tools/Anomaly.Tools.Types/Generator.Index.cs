using System.Text;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    public const string TypeOrderPath = "src/Core/Tg/Generated/TypeOrder.g.cs";
    public const string IndexPath = "docs/ai/specs/generated/types-index.tsv";

    /// <summary>
    /// TypeOrder.g.cs from OpenDream's object tree (types-converter.md 3.10): tools/coloring's encoding, plus each type's
    /// class (a tab and the class name when it is not the rules-2.3 name, a tab and "-" when the Tg assembly has no class
    /// for it), which the type registry reads to register the classes without loading them (types-converter.md 9).
    /// </summary>
    private void EmitTypeOrder(Outputs outputs)
    {
        var lines = new List<string>();
        var depth = new Dictionary<Node, int>();
        int max = 0;
        foreach (Node n in _preorder)
        {
            int d = n.Parent == null ? 0 : depth[n.Parent] + 1;
            depth[n] = d;
            max = Math.Max(max, d);
            string seg = n.Parent == null ? n.Path : n.Path.StartsWith(n.Parent.Path + "/", StringComparison.Ordinal) ? n.Path.Substring(n.Parent.Path.Length) : "=" + n.Path;
            bool tgClass = n.HasClass;
            string cls = !tgClass ? "\t-" : n.ClassName == RuleName(n.Path) ? "" : "\t" + n.ClassName;
            lines.Add(d.ToString(System.Globalization.CultureInfo.InvariantCulture) + seg + cls);
        }
        var sb = new StringBuilder();
        sb.Append(Header("every DM type path in OpenDream's object-tree order (object-model.md 9)"));
        sb.Append(@"using System.Collections.Immutable;

namespace Anomaly.Core.Tg;

/// <summary>
/// Every DM type path of the production build in DM object-tree order (docs/ai/specs/object-model.md section 9):
/// depth-first preorder of the type tree, children in the order of their first declaration in compile order
/// (tgstation.dme include order, then line order), read from OpenDream's object tree (children by type id, ids in
/// compile order). Method: tools/Anomaly.Tools.Types, docs/ai/specs/types-converter.md sections 3.10 and 9.
/// </summary>
public static partial class TypeOrder
{
    /// <summary>Number of type paths in <see cref=""Paths""/>.</summary>
");
        sb.Append($"    public const int Count = {_preorder.Count};\n\n");
        sb.Append("    /// <summary>The type paths in DM object-tree order: \"/world\", \"/savefile\", \"/datum\", ...</summary>\n");
        sb.Append("    public static readonly ImmutableArray<string> Paths;\n\n");
        sb.Append("    // The index of each path's parent in Paths (-1 for a root), and the name of its class in this assembly (null when\n");
        sb.Append("    // another assembly implements the type or nothing does). The type registry reads both (types-converter.md 9).\n");
        sb.Append("    internal static readonly ImmutableArray<int> Parents;\n");
        sb.Append("    internal static readonly ImmutableArray<string> Classes;\n\n");
        sb.Append($"    private const int MaxDepth = {max + 1};\n\n");
        sb.Append(@"    static TypeOrder()
    {
        (Paths, Parents, Classes) = Decode(Encoded(), Count);
    }

    // One line per type in preorder: its depth below the root (0 for a child of the root), then the path segment it
    // adds to its parent's path (""2/global_vars""), or ""="" and its full path when its parent is not its path prefix
    // (""1=/atom"": /atom is a child of /datum); then a tab and the class name when it is not the path with '/' turned
    // into '_' (rules 2.3), or a tab and ""-"" when this assembly has no class for it. The parent of a line is the nearest
    // earlier line one level up.
    private static (ImmutableArray<string>, ImmutableArray<int>, ImmutableArray<string>) Decode(string encoded, int count)
    {
        ImmutableArray<string>.Builder paths = ImmutableArray.CreateBuilder<string>(count);
        ImmutableArray<int>.Builder parents = ImmutableArray.CreateBuilder<int>(count);
        ImmutableArray<string>.Builder classes = ImmutableArray.CreateBuilder<string>(count);
        int[] stack = new int[MaxDepth];
        string[] pathStack = new string[MaxDepth];
        foreach (string raw in encoded.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            string cls = null;
            int tab = line.IndexOf('\t');
            if (tab >= 0)
            {
                cls = line.Substring(tab + 1);
                line = line.Substring(0, tab);
            }
            int k = 0;
            int depth = 0;
            while (line[k] >= '0' && line[k] <= '9')
                depth = depth * 10 + (line[k++] - '0');
            string path = line[k] == '='
                ? line.Substring(k + 1)
                : (depth == 0 ? """" : pathStack[depth - 1]) + line.Substring(k);
            pathStack[depth] = path;
            stack[depth] = paths.Count;
            parents.Add(depth == 0 ? -1 : stack[depth - 1]);
            classes.Add(cls == ""-"" ? null : cls ?? path.Substring(1).Replace('/', '_'));
            paths.Add(path);
        }
        if (paths.Count != count)
            throw new InvalidOperationException(""TypeOrder: decoded "" + paths.Count + "" paths, expected "" + count);
        return (paths.MoveToImmutable(), parents.MoveToImmutable(), classes.MoveToImmutable());
    }

    // A method, not a const: a const string would be stored twice in the assembly.
    private static string Encoded() => """"""
");
        foreach (string l in lines) sb.Append(l).Append('\n');
        sb.Append("\"\"\";\n}\n");
        outputs.Files[TypeOrderPath] = sb.ToString();
        EmitConstants(outputs);
    }

    /// <summary>types-index.tsv, the translation tracker (types-converter.md 7).</summary>
    private void EmitIndex(Outputs outputs)
    {
        var sb = new StringBuilder();
        sb.Append(HashHeader("the DM type and proc index, the translation tracker"));
        sb.Append("# Rows: \"type\" rows in DM object-tree order, each followed by the \"proc\" rows of the procs and verbs tg defines at it;\n");
        sb.Append("# the global procs (owner /) come first. Columns are tab-separated; types-converter.md section 3.9 defines them.\n");
        sb.Append("#type\tpath\tparent\tfile:line\tstate\tclass\thand\tmeta\n");
        sb.Append("#proc\towner\tname\tfile:line\targs\ttranslated\tbody\tkind\n");
        foreach (ProcDef p in _model.GlobalProcs)
        {
            Translation.State s = _translation.ProcState("/", p.Name);
            sb.Append("proc\t/\t").Append(p.Name).Append('\t').Append(Loc(p.File, p.Line)).Append('\t').Append(string.Join(",", p.Args))
              .Append('\t').Append(Translation.Counts(s) ? "yes" : "no").Append('\t').Append(Translation.Body(s)).Append("\tglobal\n");
        }
        foreach (Node n in _preorder)
        {
            string state = _liveBase.Contains(n.Path) ? "base" : !n.HasClass ? "none" : n.Inert ? "inert" : "live";
            sb.Append("type\t").Append(n.Path).Append('\t').Append(n.Parent?.Path ?? "/").Append('\t').Append(Loc(n.Info.File, n.Info.Line))
              .Append('\t').Append(state).Append('\t').Append(n.ClassName ?? "-").Append('\t').Append(n.Hand != null ? "1" : "0")
              .Append('\t').Append(n.InMeta ? "1" : "0").Append('\n');
            foreach ((ProcDef p, Translation.State s, LivePartialRow row) in _procStates[n])
            {
                string translated = Translation.Counts(s) ? "yes" : row != null ? "stub" : "no";
                string body = row != null ? row.Kind : Translation.Body(s);
                sb.Append("proc\t").Append(n.Path).Append('\t').Append(p.Name).Append('\t').Append(Loc(p.File, p.Line)).Append('\t')
                  .Append(string.Join(",", p.Args)).Append('\t').Append(translated).Append('\t').Append(body).Append('\t')
                  .Append(p.Verb ? "verb" : "proc").Append(p.Override ? "" : " root").Append('\n');
            }
        }
        outputs.Files[IndexPath] = sb.ToString();
    }
}
