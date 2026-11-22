using static Anomaly.Analyzers.Tests.AnalyzerHarness;

namespace Anomaly.Analyzers.Tests;

/// <summary>Which rules run where (docs/ai/specs/waiting-model.md section 5.6).</summary>
public class ScopeTests
{
    [Fact]
    public void TheAnalyzerDeclaresItsFiveRulesAsErrorsInTheWaitingModelCategory()
    {
        var rules = new WaitingModelAnalyzer().SupportedDiagnostics;
        Assert.Equal(new[] { "ANOM0001", "ANOM0002", "ANOM0003", "ANOM0004", "ANOM0005" }, rules.Select(r => r.Id));
        Assert.All(rules, r =>
        {
            Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Error, r.DefaultSeverity);
            Assert.Equal("WaitingModel", r.Category);
            Assert.True(r.IsEnabledByDefault);
        });
    }

    [Fact]
    public void ACompilationThatDoesNotSeeTheKernelIsNotAnalyzed()
    {
        // A type named DmTask in another namespace is not the kernel's.
        Expect(Analyze("""
            namespace Other
            {
                public struct DmTask { }
                public static class C
                {
                    public static DmTask Foo() => default;
                    public static void M() { Foo(); _ = Foo(); }
                }
            }
            """, withKernel: false));
    }

    [Fact]
    public void TheTranslatedCodeRulesRunOnlyWhenTheProjectSaysSo()
    {
        const string source = """
            public static class C
            {
                public static async DmTask Sleeps() { await sleep(1); }
                public static async DmTask M()
                {
                    var t = Sleeps();
                    bool done = t.IsCompleted;
                    await t;
                    _ = Sleeps();
                }
            }
            """;
        Expect(Analyze(source, translated: false), ("ANOM0001", "Sleeps()"));
        Expect(Analyze(source, translated: true),
            ("ANOM0002", "Sleeps()"), ("ANOM0003", "t.IsCompleted"), ("ANOM0001", "Sleeps()"));
    }
}
