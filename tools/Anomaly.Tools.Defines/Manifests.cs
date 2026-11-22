using System.Text.RegularExpressions;

namespace Anomaly.Tools.Defines;

/// <summary>
/// <c>defines.manifest.json</c>: one entry per <c>#define</c> the preprocessor saw, in preprocessing order, with
/// what became of it (the C# constant, or the reason it is not emitted). <c>signals.manifest.json</c>: the
/// <c>COMSIG_*</c> signals with their dmdoc (<c>from base of ...: (args)</c>) and the defines tg indents under them
/// (return values or argument indexes).
/// </summary>
public static class Manifests
{
    public const string DefinesFile = "defines.manifest.json";
    public const string SignalsFile = "signals.manifest.json";

    /// <summary>
    /// The <c>source: (args)</c> line of a signal's dmdoc. The arguments may nest parentheses to any depth (a .NET
    /// balancing group): <c>COMSIG_MOVABLE_HEAR</c> documents <c>(proc args list(message, ..., list/message_mods = list(), message_range))</c>.
    /// </summary>
    private static readonly Regex SignalDoc = new(@"^(?<source>.*?)\s*:\s*\((?<args>(?>[^()]+|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!)))\)", RegexOptions.CultureInvariant);

    public static string Params(MacroDefinition d) =>
        d.IsFunctionLike ? string.Join(",", d.Parameters.Select((p, i) => d.IsVariadic && i == d.Parameters.Count - 1 && p != "..." ? p + "..." : p)) : null;

    private static List<string> ParamList(MacroDefinition d) =>
        d.IsFunctionLike ? d.Parameters.Select((p, i) => d.IsVariadic && i == d.Parameters.Count - 1 && p != "..." ? p + "..." : p).ToList() : null;

    public static string Defines(ConverterOutput output)
    {
        List<DefineResult> results = output.Results;
        var byKind = new JsonObject();
        foreach (IGrouping<string, DefineResult> g in results.GroupBy(r => r.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
            byKind.Add(g.Key, g.Count());
        var header = new JsonObject()
            .Add("generator", CSharpEmitter.Generator)
            .Add("tg_snapshot", output.Snapshot)
            .Add("spec", "docs/ai/specs/defines-converter.md")
            .AddRaw("configuration", new JsonObject()
                .Add("DM_VERSION", output.Config.DmVersion)
                .Add("DM_BUILD", output.Config.DmBuild)
                .Add("command_line_defines", output.Config.CommandLineSymbols.ToList())
                .ToString())
            .Add("files", output.Preprocessor.Files)
            .Add("seen", results.Count)
            .Add("emitted", results.Count(r => r.Emitted))
            .Add("not_emitted", results.Count(r => !r.Emitted))
            .AddRaw("by_kind", byKind.ToString());
        IEnumerable<JsonObject> entries = results.Select(r =>
        {
            MacroDefinition d = r.Definition;
            var e = new JsonObject()
                .Add("name", d.Name)
                .Add("file", d.File)
                .Add("line", d.Line)
                .Add("args", ParamList(d))
                .Add("body", d.BodyText)
                .Add("kind", r.Kind)
                .Add("status", r.Emitted ? "emitted" : "not_emitted");
            if (r.Emitted)
            {
                if (r.CsType == "method")
                    e.Add("cs", $"GlobalProcs.{r.CsSignature}");
                else
                    e.Add("cs", $"const {r.CsType}").AddValue("value", r.Value);
            }
            e.Add("reason", r.Reason)
                .Add("condition", d.InactiveCondition)
                .Add("end", d.EndReason)
                .Add("indented_under", r.IndentedUnder)
                .Add("features", r.Features)
                .Add("notes", r.Notes);
            return e;
        });
        return JsonText.Document(header, "defines", entries);
    }

    public static string Signals(ConverterOutput output)
    {
        var entries = new List<JsonObject>();
        int signalCount = 0;
        int factoryCount = 0;
        foreach (DefineResult r in output.Results)
        {
            MacroDefinition d = r.Definition;
            if (!d.Name.StartsWith("COMSIG_", StringComparison.Ordinal) || !d.Active || r.Kind is "superseded")
                continue;
            string source = null;
            string args = null;
            if (d.Doc != null)
            {
                foreach (string line in d.Doc.Split('\n'))
                {
                    Match m = SignalDoc.Match(line);
                    if (m.Success)
                    {
                        source = m.Groups["source"].Value;
                        if (source is "Args" or "Arguments" or "args" or "arguments")
                            source = null;
                        args = m.Groups["args"].Value.Trim();
                        break;
                    }
                }
            }
            if (r.Kind == "string")
            {
                signalCount++;
                entries.Add(new JsonObject()
                    .Add("name", d.Name)
                    .Add("kind", "signal")
                    .Add("value", (string)r.Value)
                    .Add("file", d.File)
                    .Add("line", d.Line)
                    .Add("source", source)
                    .Add("args", args)
                    .Add("doc", d.Doc)
                    .Add("indented", r.IndentedDefines));
            }
            else if (d.IsFunctionLike)
            {
                factoryCount++;
                entries.Add(new JsonObject()
                    .Add("name", d.Name)
                    .Add("kind", "signal_factory")
                    .Add("params", ParamList(d))
                    .Add("body", d.BodyText)
                    .Add("file", d.File)
                    .Add("line", d.Line)
                    .Add("source", source)
                    .Add("args", args)
                    .Add("doc", d.Doc)
                    .Add("indented", r.IndentedDefines));
            }
        }
        var header = new JsonObject()
            .Add("generator", CSharpEmitter.Generator)
            .Add("tg_snapshot", output.Snapshot)
            .Add("note", "COMSIG_* defines whose value is text (signals) or that are function-like (signal name factories). Dispatch is by the text value: two signals may share one. Numeric COMSIG_* defines are return flags or aliases and are only in defines.manifest.json. 'indented' lists the defines tg indents under a signal: its return values and, for some signals, the indexes of its argument list (HEARING_*, SPEECH_*).")
            .Add("signal_count", signalCount)
            .Add("factory_count", factoryCount);
        return JsonText.Document(header, "signals", entries);
    }
}
