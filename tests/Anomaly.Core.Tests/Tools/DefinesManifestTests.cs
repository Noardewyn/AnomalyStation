using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anomaly.Core.Tg;
using Anomaly.Tools.Defines;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// No define is dropped silently: every <c>#define</c> line of the converted files is in the manifest with what
/// became of it, the committed output is what the tool generates now, and generating twice gives the same bytes.
/// </summary>
public class DefinesManifestTests
{
    private static readonly Regex DefineLine = new(@"^\s*#\s*define\b", RegexOptions.CultureInvariant);

    private static JsonDocument ReadJson(string file) => JsonDocument.Parse(File.ReadAllText(Path.Combine(TgClone.GeneratedDirectory, file)));

    [TgFact]
    public void EveryDefineLine_IsInTheConversion()
    {
        ConverterOutput output = TgClone.Output;
        var seen = new HashSet<(string, int)>(output.Results.Select(r => (r.Definition.File, r.Definition.Line)));
        int lines = 0;
        int inDefinesDirectory = 0;
        foreach (string file in output.Preprocessor.Files)
        {
            string[] text = File.ReadAllLines(Path.Combine(TgClone.Root, file));
            for (int i = 0; i < text.Length; i++)
            {
                if (!DefineLine.IsMatch(text[i]))
                    continue;
                lines++;
                if (file.StartsWith("code/__DEFINES/", StringComparison.Ordinal))
                    inDefinesDirectory++;
                Assert.True(seen.Contains((file, i + 1)), $"{file}:{i + 1} is not in the conversion: {text[i].Trim()}");
            }
        }
        Assert.Equal(lines, output.Results.Count);
        // defines.md section 1 counts 16,456 #define lines in code/__DEFINES at the snapshot.
        Assert.True(inDefinesDirectory >= 16456, $"only {inDefinesDirectory} defines in code/__DEFINES");
        Assert.Contains("code/__DEFINES/_flags.dm", output.Preprocessor.Files);
        Assert.Contains("code/__DEFINES/traits/sources.dm", output.Preprocessor.Files);
        Assert.Contains("code/_compile_options.dm", output.Preprocessor.Files);
    }

    [TgFact]
    public void EveryNotEmittedDefine_HasAKindAndAReason()
    {
        foreach (DefineResult r in TgClone.Output.Results)
        {
            Assert.False(string.IsNullOrEmpty(r.Kind), $"{r.Definition.Location} {r.Name} has no kind");
            if (!r.Emitted)
                Assert.False(string.IsNullOrEmpty(r.Reason), $"{r.Definition.Location} {r.Name} ({r.Kind}) has no reason");
        }
        Assert.Empty(TgClone.Output.Errors);
    }

    [Fact]
    public void TheCommittedManifest_ListsEveryDefine_AndMatchesTheCompiledConstants()
    {
        using JsonDocument manifest = ReadJson(Manifests.DefinesFile);
        JsonElement root = manifest.RootElement;
        JsonElement defines = root.GetProperty("defines");
        Assert.Equal(root.GetProperty("seen").GetInt32(), defines.GetArrayLength());
        Assert.True(defines.GetArrayLength() >= 16456);
        int emitted = 0;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement e in defines.EnumerateArray())
        {
            string name = e.GetProperty("name").GetString();
            string status = e.GetProperty("status").GetString();
            Assert.True(e.TryGetProperty("kind", out _), $"{name} has no kind");
            Assert.True(e.TryGetProperty("file", out _) && e.TryGetProperty("line", out _), $"{name} has no location");
            if (status != "emitted")
            {
                Assert.True(e.TryGetProperty("reason", out JsonElement reason) && reason.GetString().Length > 0, $"{name} has no reason");
                continue;
            }
            emitted++;
            Assert.True(names.Add(name), $"{name} is emitted twice");
            string cs = e.GetProperty("cs").GetString();
            if (cs.StartsWith("GlobalProcs.", StringComparison.Ordinal))
            {
                Assert.NotNull(typeof(GlobalProcs).GetMethod(name, BindingFlags.Public | BindingFlags.Static));
                continue;
            }
            FieldInfo field = typeof(Defines).GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.True(field != null && field.IsLiteral, $"Defines.{name} is missing");
            object value = field.GetRawConstantValue();
            JsonElement expected = e.GetProperty("value");
            switch (value)
            {
                case int i:
                    Assert.Equal("const int", cs);
                    Assert.Equal(expected.GetInt32(), i);
                    break;
                case float f:
                    Assert.Equal("const float", cs);
                    if (expected.ValueKind == JsonValueKind.Number)
                        Assert.Equal(expected.GetSingle(), f);
                    break;
                case string s:
                    Assert.Equal("const string", cs);
                    Assert.Equal(expected.GetString(), s);
                    break;
            }
        }
        Assert.Equal(root.GetProperty("emitted").GetInt32(), emitted);
        int constants = typeof(Defines).GetFields(BindingFlags.Public | BindingFlags.Static).Length;
        // the span macros the converter leaves for hand translation (docs/ai/specs/defines-converter.md: span_class and the *_ml helpers)
        string[] byHand = { "span_class", "span_notice_ml", "span_danger_ml", "span_info_ml" };
        int methods = typeof(GlobalProcs).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Count(m => m.Name.StartsWith("span_", StringComparison.Ordinal) && !byHand.Contains(m.Name));
        Assert.Equal(emitted, constants + methods);
    }

    [TgFact]
    public void TheCommittedOutput_IsUpToDate()
    {
        List<string> differences = DefinesConverter.Diff(TgClone.Output, TgClone.GeneratedDirectory);
        Assert.True(differences.Count == 0, "regenerate with dotnet run --project tools/Anomaly.Tools.Defines -- --out src/Core/Tg/Generated/Defines; differs: " + string.Join(", ", differences.Take(10)));
    }

    [TgFact]
    public void Generation_IsDeterministic()
    {
        ConverterOutput first = TgClone.Output;
        ConverterOutput second = DefinesConverter.Generate(TgClone.Root);
        Assert.Equal(first.Files.Keys, second.Files.Keys);
        foreach ((string path, string content) in first.Files)
            Assert.True(content == second.Files[path], $"{path} differs between two runs");
    }

    [Fact]
    public void SnippetConversion_IsDeterministic()
    {
        const string dm = "#define A (1<<3)\n#define B \"text\"\n#define C (A | 1)\n#define span_x(str) (\"<b>\" + str + \"</b>\")\n";
        ConverterOutput first = Snippet.Convert(dm);
        ConverterOutput second = Snippet.Convert(dm);
        Assert.Equal(first.Files, second.Files);
    }

    [Fact]
    public void TheSignalsManifest_DocumentsSignalsAndTheDefinesIndentedUnderThem()
    {
        using JsonDocument manifest = ReadJson(Manifests.SignalsFile);
        var signals = manifest.RootElement.GetProperty("signals").EnumerateArray().ToDictionary(e => e.GetProperty("name").GetString());
        Assert.True(manifest.RootElement.GetProperty("signal_count").GetInt32() >= 1400);

        // signals_atom_movable.dm:10-11  ///from base of atom/movable/Moved(): (atom/old_loc, dir, forced, list/old_locs)
        JsonElement moved = signals["COMSIG_MOVABLE_MOVED"];
        Assert.Equal("movable_moved", moved.GetProperty("value").GetString());
        Assert.Equal("from base of atom/movable/Moved()", moved.GetProperty("source").GetString());
        Assert.Equal("atom/old_loc, dir, forced, list/old_locs", moved.GetProperty("args").GetString());

        // signals_atom_movable.dm:8-9  COMSIG_MOVABLE_PRE_MOVE with the indented COMPONENT_MOVABLE_BLOCK_PRE_MOVE under it.
        Assert.Equal(new[] { "COMPONENT_MOVABLE_BLOCK_PRE_MOVE" },
            signals["COMSIG_MOVABLE_PRE_MOVE"].GetProperty("indented").EnumerateArray().Select(f => f.GetString()).ToArray());
        // signals_atom_movable.dm:69-78  tg also indents the indexes of a signal's argument list under it
        // (HEARING_SPEAKER 1 ... HEARING_RANGE 9 index the args of COMSIG_MOVABLE_HEAR): they are not return values.
        JsonElement hearing = signals["COMSIG_MOVABLE_HEAR"];
        // Its doc nests parentheses two deep: "from base of atom/movable/Hear(): (proc args list(message, ..., list/message_mods = list(), message_range))".
        Assert.Equal("from base of atom/movable/Hear()", hearing.GetProperty("source").GetString());
        Assert.StartsWith("proc args list(message, atom/movable/speaker,", hearing.GetProperty("args").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("list/message_mods = list(), message_range)", hearing.GetProperty("args").GetString(), StringComparison.Ordinal);
        string[] hear = hearing.GetProperty("indented").EnumerateArray().Select(f => f.GetString()).ToArray();
        Assert.Equal(9, hear.Length);
        Assert.Equal("HEARING_SPEAKER", hear[0]);
        Assert.Equal("HEARING_RANGE", hear[^1]);

        // Two signals that share one text value stay two entries (dispatch is by the text, defines.md 5.3).
        Assert.Equal(signals["COMSIG_PROJECTILE_PREHIT"].GetProperty("value").GetString(), signals["COMSIG_PROJECTILE_SELF_PREHIT"].GetProperty("value").GetString());

        // A name factory (cooldowns.dm:74 COMSIG_CD_STOP(cd_index) "cooldown_[cd_index]") is listed with its parameters.
        Assert.Equal("signal_factory", signals["COMSIG_CD_STOP"].GetProperty("kind").GetString());

        // A numeric COMSIG_ define is a return flag or alias, not a signal.
        Assert.False(signals.ContainsKey("COMSIG_ATOM_NO_UPDATE_NAME"));
    }

    [Fact]
    public void IndentedDefines_AreNotAllCalledReturnFlags()
    {
        // signals_atom_movable.dm:69-72  #define COMSIG_MOVABLE_HEAR "movable_hear" / (tab) #define HEARING_RAW_MESSAGE 3:
        // an index into the signal's argument list, documented as indented under the signal, never as a return flag.
        string text = File.ReadAllText(Path.Combine(TgClone.GeneratedDirectory, "__DEFINES", "dcs", "signals", "signals_atom", "signals_atom_movable.g.cs"));
        Assert.Contains("Indented under COMSIG_MOVABLE_HEAR in tg: one of its return values or argument indexes.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Return flag", text, StringComparison.Ordinal);
        using JsonDocument manifest = ReadJson(Manifests.DefinesFile);
        JsonElement raw = manifest.RootElement.GetProperty("defines").EnumerateArray().Single(e => e.GetProperty("name").GetString() == "HEARING_RAW_MESSAGE");
        Assert.Equal("COMSIG_MOVABLE_HEAR", raw.GetProperty("indented_under").GetString());
    }

    [Fact]
    public void Locator_FindsTheMainCheckoutOfAWorktree()
    {
        string temp = Path.Combine(Path.GetTempPath(), "anomaly-locator-" + Guid.NewGuid().ToString("N"));
        try
        {
            string main = Path.Combine(temp, "repo");
            string worktree = Path.Combine(main, ".claude", "worktrees", "wt");
            string gitDir = Path.Combine(main, ".git", "worktrees", "wt");
            Directory.CreateDirectory(gitDir);
            Directory.CreateDirectory(worktree);
            Directory.CreateDirectory(Path.Combine(temp, "tgstation"));
            File.WriteAllText(Path.Combine(main, TgLocator.SolutionFile), "");
            File.WriteAllText(Path.Combine(worktree, TgLocator.SolutionFile), "");
            File.WriteAllText(Path.Combine(temp, "tgstation", "tgstation.dme"), "");
            File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..\n");
            File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: " + gitDir + "\n");

            Assert.Equal(worktree, TgLocator.FindRepositoryRoot(Path.Combine(worktree)));
            Assert.Equal(Path.GetFullPath(main), TgLocator.MainCheckout(worktree));
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TgLocator.EnvironmentVariable)))
                Assert.Equal(Path.GetFullPath(Path.Combine(temp, "tgstation")), TgLocator.Locate(null, worktree));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
