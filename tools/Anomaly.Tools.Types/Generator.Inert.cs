using System.Text;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    /// <summary>The live-partial list (types-converter.md 5), relative to the repository.</summary>
    public const string LivePartialPath = "docs/ai/specs/live-partial.txt";

    /// <summary>The frozen live base (types-converter.md 4.1).</summary>
    public const string LiveBasePath = "docs/ai/specs/generated/live-base.txt";

    /// <summary>One row of live-partial.txt.</summary>
    internal sealed record LivePartialRow(string Path, string Proc, string Milestone, string Kind, string Why, string Section, int Line);

    // The procs of each node with their state (types-converter.md 4.1, 6), in the model's order.
    private readonly Dictionary<Node, List<(ProcDef Proc, Translation.State State, LivePartialRow Row)>> _procStates = new();

    private readonly Dictionary<(string Path, string Proc), LivePartialRow> _livePartial = new();

    private HashSet<string> _liveBase;

    /// <summary>A builtin type the shim implements that the frozen live base does not list (gaps CI1).</summary>
    private const string ShimBuiltinMissedByTheFreeze = "/regex";

    private void ReadLivePartial()
    {
        string file = Path.Combine(_repo, LivePartialPath);
        if (!File.Exists(file)) return;
        int lineNo = 0;
        string section = null;
        foreach (string raw in File.ReadLines(file))
        {
            lineNo++;
            string line = raw.TrimEnd();
            if (line.StartsWith("## ", StringComparison.Ordinal)) { section = line.Substring(3).Trim(); continue; }
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#')) continue;
            string[] f = line.Split('\t');
            string where = $"{LivePartialPath}:{lineNo}";
            if (f.Length < 5) throw new ToolException($"{where}: a row is <type path>\\t<proc name>\\t<milestone>\\t<kind>\\t<why> (types-converter.md 5)");
            var row = new LivePartialRow(f[0].Trim(), f[1].Trim(), f[2].Trim(), f[3].Trim(), string.Join(" ", f.Skip(4)).Trim(), section, lineNo);
            if (section == null) throw new ToolException($"{where}: a row before the first ## section");
            if (row.Kind is not ("deferred" or "not_translated")) throw new ToolException($"{where}: kind is deferred or not_translated, not {row.Kind}");
            if (!_nodes.TryGetValue(row.Path, out Node n) || !n.HasClass) throw new ToolException($"{where}: no generated class for {row.Path}");
            if (!n.Info.Procs.Any(p => p.Name == row.Proc)) throw new ToolException($"{where}: {row.Path} defines no tg proc {row.Proc}");
            if (Translation.Counts(_translation.ProcState(row.Path, row.Proc)))
                throw new ToolException($"{where}: {row.Path}/{row.Proc} is declared by a hand-written part; remove the row");
            if (!_livePartial.TryAdd((row.Path, row.Proc), row)) throw new ToolException($"{where}: a second row for {row.Path}/{row.Proc}");
        }
    }

    /// <summary>
    /// Types-converter.md 4.1: the live base is frozen at the first run (main 68f3a0d: every class with a hand-written
    /// part, and the builtin types the engine parts and the shim implement); later runs read it.
    /// </summary>
    private void ReadOrFreezeLiveBase(Outputs outputs)
    {
        string file = Path.Combine(_repo, LiveBasePath);
        if (File.Exists(file))
        {
            _liveBase = new HashSet<string>(File.ReadLines(file).Where(l => l.Length > 0 && l[0] == '/'), StringComparer.Ordinal);
            outputs.Files[LiveBasePath] = File.ReadAllText(file);
            return;
        }
        _liveBase = new HashSet<string>(_preorder.Where(n => n.Hand != null || n.Existing != null).Select(n => n.Path), StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.Append("# The live base (docs/ai/specs/types-converter.md 4.1): the types that are live whatever their procs, frozen by the\n");
        sb.Append("# types converter (tools/Anomaly.Tools.Types) at its first run on main 68f3a0d: every type whose class had a hand-written\n");
        sb.Append("# part (src/Core/Tg) and the builtin types the shim implements. Nobody adds to it; types translated from M3 on are\n");
        sb.Append("# live by section 4.2 only. One type path per line, sorted.\n");
        foreach (string p in _liveBase.OrderBy(p => p, StringComparer.Ordinal)) sb.Append(p).Append('\n');
        outputs.Files[LiveBasePath] = sb.ToString();
        Report.Add($"live base frozen: {_liveBase.Count} types into {LiveBasePath}");
    }

    private void DecideInert()
    {
        foreach (Node n in _preorder)
        {
            var list = new List<(ProcDef, Translation.State, LivePartialRow)>();
            foreach (ProcDef p in n.Info.Procs)
            {
                Translation.State s = n.HasClass || n.Existing != null ? _translation.ProcState(n.Path, p.Name) : Translation.State.No;
                LivePartialRow row = Translation.Counts(s) ? null : _livePartial.GetValueOrDefault((n.Path, p.Name));
                list.Add((p, s, row));
            }
            _procStates[n] = list;
            // One stub per row, for the proc's last definition (an earlier duplicate is covered by it).
            foreach (LivePartialRow row in _livePartial.Values.Where(r => r.Path == n.Path).OrderBy(r => r.Line))
                n.Stubs.Add((n.Info.Procs.Last(p => p.Name == row.Proc), row.Milestone, row.Kind + "\t" + row.Why + "\t" + row.Section));
        }

        // Types-converter.md 4.2: live when in the live base, or when the parent is live and every tg proc of the type is
        // declared (a live-partial stub counts). Otherwise inert.
        foreach (Node n in _preorder)
        {
            if (!n.HasClass || _liveBase.Contains(n.Path)) { n.Inert = false; continue; }
            bool undeclared = _procStates[n].Any(x => !Translation.Counts(x.State) && x.Row == null);
            // Gaps CI1: /regex is a builtin the shim implements (DmShim's regex) that the frozen live base missed; its subtypes
            // (wiremod's /regex/format_component) are live by section 4.2 like any other.
            bool parentLive = n.Parent == null || _liveBase.Contains(n.Parent.Path) || (n.Parent.HasClass && !n.Parent.Inert)
                || n.Parent.Path == ShimBuiltinMissedByTheFreeze;
            n.Inert = undeclared || !parentLive;
        }
        foreach (Node n in _preorder.Where(n => n.IsHandWritten && n.Inert))
            Report.Add($"hand-written but inert: {n.Path}: " + string.Join(", ", _procStates[n].Where(x => !Translation.Counts(x.State) && x.Row == null).Select(x => x.Proc.Name).Distinct().Take(12)));
        foreach (LivePartialRow row in _livePartial.Values.Where(r => _liveBase.Contains(r.Path)))
            Report.Add($"live-partial: {row.Path} is in the live base; the stub of {row.Proc} only declares the proc");
    }
}
