using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anomaly.Core.Tests.Tools.Types;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    private static readonly JsonSerializerOptions Compact = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// The var-block conformance data (types-converter.md 3.4): for every hand-written class with a hand-written var
    /// block, OpenDream's value of each var it declares and of each override tg makes at its type; and the mismatches
    /// the check finds now, which the test pins until the lead decides each one.
    /// </summary>
    private void EmitConformance(Outputs outputs)
    {
        var rows = new List<VarBlockRow>();
        foreach (Node n in _preorder.Where(n => n.Mode == Mode.Hand))
        {
            var inits = n.Info.Inits.ToDictionary(i => i.Name, StringComparer.Ordinal);
            foreach (VarDecl v in n.Info.Vars)
            {
                if (!n.Hand.Fields.Contains(v.Name) && !n.Hand.Properties.Contains(v.Name)) continue;
                if (inits.TryGetValue(v.Name, out VarSet i)) rows.Add(Row(n, v.Name, "init", i.File, i.Line, i.Value));
                else rows.Add(Row(n, v.Name, "decl", v.File, v.Line, v.Value ?? DmVal.Null()));
            }
            foreach (VarSet o in n.Info.Overrides)
            {
                if (inits.TryGetValue(o.Name, out VarSet i)) rows.Add(Row(n, o.Name, "init", i.File, i.Line, i.Value));
                else rows.Add(Row(n, o.Name, "override", o.File, o.Line, o.Value ?? DmVal.Null()));
            }
        }
        var defaults = new StringBuilder();
        defaults.Append(HashHeader("the defaults of the hand-written var blocks, as OpenDream folds them"));
        defaults.Append("# path\tvar\twhere (decl, override, init)\ttg file:line\texpected value (types-converter.md 3.4)\n");
        foreach (VarBlockRow r in rows)
            defaults.Append(r.Path).Append('\t').Append(r.Var).Append('\t').Append(r.Where).Append('\t').Append(r.Location).Append('\t').Append(r.Expected).Append('\n');
        outputs.Files[VarBlockConformance.DefaultsFile] = defaults.ToString();

        var mismatches = new StringBuilder();
        mismatches.Append(HashHeader("the known mismatches of the hand-written var blocks; the lead decides each one"));
        mismatches.Append("# path\tvar\twhere\ttg file:line\texpected (OpenDream)\tactual (the hand-written class)\n");
        int count = 0;
        foreach (VarBlockRow r in rows)
        {
            string actual = VarBlockConformance.Check(r);
            if (actual == null) continue;
            count++;
            mismatches.Append(r.Path).Append('\t').Append(r.Var).Append('\t').Append(r.Where).Append('\t').Append(r.Location).Append('\t')
                .Append(Short(JsonSerializer.Deserialize<DmVal>(r.Expected))).Append('\t').Append(actual).Append('\n');
        }
        outputs.Files[VarBlockConformance.MismatchesFile] = mismatches.ToString();
        Report.Add($"conformance: {rows.Count} hand-written defaults checked over {_preorder.Count(n => n.Mode == Mode.Hand)} classes, {count} mismatches");
    }

    private static VarBlockRow Row(Node n, string name, string where, string file, int line, DmVal v)
        => new(n.Path, name, where, Loc(file, line), JsonSerializer.Serialize(v, Compact));
}
