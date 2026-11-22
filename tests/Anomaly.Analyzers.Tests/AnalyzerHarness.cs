using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Anomaly.Analyzers.Tests;

/// <summary>
/// Compiles C# snippets against the runtime and the real Anomaly.Core.Kernel and Anomaly.Core.DmShim assemblies, runs the
/// waiting-model analyzer on them and returns its diagnostics as (id, flagged source text) pairs.
/// </summary>
internal static class AnalyzerHarness
{
    /// <summary>The usings every snippet starts with.</summary>
    public const string Prelude = """
        using System;
        using System.Collections.Generic;
        using Anomaly.Core.Kernel;
        using Anomaly.Core.DmShim;
        using static Anomaly.Core.DmShim.Dm; // sleep, spawn: the DM spellings translated code uses

        """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly ImmutableArray<MetadataReference> BaseReferences = LoadReferences();

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        // The shared framework the tests run on, then the kernel and the shim.
        string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
        var refs = tpa
            .Where(p => string.Equals(Path.GetDirectoryName(p), runtimeDir, StringComparison.Ordinal))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Anomaly.Core.Kernel.DmTask).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(typeof(Anomaly.Core.DmShim.ShouldNotSleepAttribute).Assembly.Location));
        return refs.ToImmutableArray();
    }

    /// <summary>
    /// Compiles <paramref name="source"/> (with <see cref="Prelude"/>), asserts it has no compiler errors and returns it.
    /// </summary>
    public static CSharpCompilation Compile(string source, string assemblyName = "Snippet", IEnumerable<MetadataReference> extra = null, bool withKernel = true)
    {
        var tree = CSharpSyntaxTree.ParseText(withKernel ? Prelude + source : source, ParseOptions, path: assemblyName + ".cs");
        var references = withKernel
            ? BaseReferences.AddRange(extra ?? Enumerable.Empty<MetadataReference>())
            : BaseReferences.RemoveRange(BaseReferences.Skip(BaseReferences.Length - 2));
        var compilation = CSharpCompilation.Create(assemblyName, new[] { tree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "The snippet does not compile:\n" + string.Join("\n", errors));
        return compilation;
    }

    /// <summary>A compiled snippet as a reference for another snippet (an "other assembly").</summary>
    public static MetadataReference AsReference(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    /// <summary>
    /// Runs the analyzer on a snippet. <paramref name="translated"/> sets the MSBuild property AnomalyTranslatedCode (the
    /// translated-code rules ANOM0002 and ANOM0003).
    /// </summary>
    public static List<(string Id, string Text)> Analyze(string source, bool translated = false, IEnumerable<MetadataReference> extra = null, bool withKernel = true)
        => Run(Compile(source, extra: extra, withKernel: withKernel), translated);

    public static List<(string Id, string Text)> Run(CSharpCompilation compilation, bool translated) =>
        Diagnostics(compilation, translated)
            .Select(d => (d.Id, d.Location.SourceTree.ToString().Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length)))
            .ToList();

    /// <summary>The messages of the diagnostics, for the tests that check the wording.</summary>
    public static List<string> Messages(string source, bool translated = false) =>
        Diagnostics(Compile(source), translated).Select(d => d.Id + ": " + d.GetMessage()).ToList();

    private static List<Diagnostic> Diagnostics(CSharpCompilation compilation, bool translated)
    {
        var global = new Dictionary<string, string>();
        if (translated) global[WaitingModelAnalyzer.TranslatedCodeOption] = "true";
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new OptionsProvider(global));
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new WaitingModelAnalyzer()), options);
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Asserts exactly these diagnostics (in source order), compared by id and flagged text.</summary>
    public static void Expect(List<(string Id, string Text)> actual, params (string Id, string Text)[] expected)
    {
        Assert.True(actual.SequenceEqual(expected),
            "Expected:\n  " + string.Join("\n  ", expected.Select(e => $"{e.Id} '{e.Text}'"))
            + "\nActual:\n  " + string.Join("\n  ", actual.Select(a => $"{a.Id} '{a.Text}'")));
    }

    private sealed class OptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty = new Options(new Dictionary<string, string>());

        public OptionsProvider(Dictionary<string, string> global) => GlobalOptions = new Options(global);

        public override AnalyzerConfigOptions GlobalOptions { get; }
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;
        public Options(Dictionary<string, string> values) => _values = values;
        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value);
    }
}
