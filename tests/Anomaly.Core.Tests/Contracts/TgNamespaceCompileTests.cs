using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// Translated code lives in namespace Anomaly.Core.Tg and imports Dm, Builtins, Defines and GlobalProcs with
/// <c>using static</c> (docs/ai/translation-rules.md 1.2). <c>TgNamespaceProbe</c> compiling is the main check (bare
/// names and qualified <c>Dm.str</c>, <c>Dm.@new</c> resolve without ambiguity); these tests run its implemented part
/// and guard the namespaces against the wave-0 shadowing bug.
/// </summary>
public class TgNamespaceCompileTests
{
    [Fact]
    public void TheProbeRunsTheImplementedSurfaceFromTheTgNamespace()
    {
        var w = new World();
        var log = new List<string>();
        DmWorldState.Of(w).WorldLog = log.Add;
        w.Step(3);
        var player = new mob();
        var mc = new datum_controller_master();
        List<string> results = TgNamespaceProbe.RunImplemented(w, player, mc);
        Assert.Equal(new[] { "1", "truthy ok", "a", "b", "assoc ok", "usr ok", "Master ok", "GLOB null", "can't reach!", "1.5", "crash" }, results);
        Assert.Equal(new[] { "logged" }, log);
        Assert.Same(mc, TgState.Of(w).Master);
        Assert.Null(TgState.Of(new World()).Master); // per world
    }

    [Fact]
    public void InitStaticsRunsOncePerWorld()
    {
        // InitStatics runs world.Genesis() (genesis_call.dme), which reaches the skeletons of the other wave-3 packages until
        // they are merged: the worlds report those paths instead of throwing.
        var w = new World();
        DmWorldState.Of(w).NotTranslatedThrows = false;
        TgState.Of(w).InitStatics();
        Assert.Throws<InvalidOperationException>(() => TgState.Of(w).InitStatics());
        var other = new World();
        DmWorldState.Of(other).NotTranslatedThrows = false;
        TgState.Of(other).InitStatics();
    }

    [Fact]
    public void NoNamespaceSegmentShadowsAClassThatTranslatedCodeNamesQualified()
    {
        // C# finds a namespace member (Anomaly.Core.Dm) before a using-static or using-namespace import, so a namespace
        // segment with one of these names breaks every qualified call such as Dm.str (the wave-0 bug).
        var qualified = new HashSet<string>(StringComparer.Ordinal)
        {
            "Dm", "I18n", "Waiting", "Sleeping", "World", "DmTypes", "DmList", "GlobalProcs", "Builtins", "Defines", "TgState",
        };
        Assembly[] assemblies = { typeof(World).Assembly, typeof(Dm).Assembly, typeof(TgState).Assembly, typeof(TgNamespaceCompileTests).Assembly };
        string[] bad = assemblies.SelectMany(a => a.GetTypes())
            .Select(t => t.Namespace)
            .Where(ns => ns != null)
            .Distinct()
            .Where(ns => ns.Split('.').Any(qualified.Contains))
            .ToArray();
        Assert.True(bad.Length == 0, "namespaces that shadow shim classes: " + string.Join(", ", bad));
    }
}
