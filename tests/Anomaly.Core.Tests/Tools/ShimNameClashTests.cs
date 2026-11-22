using System.IO;
using System.Reflection;
using System.Text.Json;
using Anomaly.Core.Tg;
using Anomaly.Tools.Defines;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// translation-rules.md 1.6: <c>Dm</c> and <c>Builtins</c>, which translated files import with <c>using static</c>
/// next to <c>Defines</c> and <c>GlobalProcs</c>, never define a name that tg defines (as a <c>#define</c> or a
/// global proc): two <c>using static</c> classes with the same member make every use ambiguous (CS0229, CS0121).
/// The classes are looked up by name, so the test also guards them once the shim packages add them.
/// </summary>
public class ShimNameClashTests
{
    private static readonly BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static HashSet<string> MemberNames(Type type) =>
        type == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(type.GetMembers(PublicStatic).Select(m => m.Name), StringComparer.Ordinal);

    private static HashSet<string> TgNames()
    {
        HashSet<string> names = MemberNames(typeof(Defines));
        names.UnionWith(MemberNames(typeof(GlobalProcs)));
        // Every name tg #defines in the converted files, emitted or not (function-like macros become GlobalProcs).
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(TgClone.GeneratedDirectory, Manifests.DefinesFile)));
        foreach (JsonElement e in manifest.RootElement.GetProperty("defines").EnumerateArray())
        {
            if (e.GetProperty("kind").GetString() != "inactive")
                names.Add(e.GetProperty("name").GetString());
        }
        return names;
    }

    [Fact]
    public void DmAndBuiltins_DefineNoTgName()
    {
        Type dm = Type.GetType("Anomaly.Core.DmShim.Dm, Anomaly.Core.DmShim");
        Type builtins = typeof(Defines).Assembly.GetType("Anomaly.Core.Tg.Builtins");
        HashSet<string> tg = TgNames();
        Assert.Contains("NONE", tg);
        Assert.Contains("PI", tg);
        Assert.Contains("ASSERT", tg);
        foreach (Type shim in new[] { dm, builtins }.Where(t => t != null))
        {
            List<string> clashes = MemberNames(shim).Where(tg.Contains).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(clashes.Count == 0, $"{shim.FullName} defines tg names: {string.Join(", ", clashes)}");
        }
    }

    [Fact]
    public void ByondBuiltinConstants_AreNotEmitted()
    {
        // The converter folds tg defines over BYOND's stddef constants but leaves those names to the shim.
        foreach ((string name, string _) in ByondBuiltins.Constants)
        {
            if (name == "TURF_LAYER")
                continue; // tg redefines it as a poison macro (layers.dm:160); not emitted either way
            Assert.False(TgClone.HasConstant(name), $"BYOND's {name} is emitted into Defines");
        }
    }
}
