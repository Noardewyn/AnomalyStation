using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Anomaly.Core.Tests.Apps;

/// <summary>
/// The server paces ticks with a host-side clock (docs/ai/specs/apps.md 1.3) and claims that pacing never changes
/// what a tick computes. That holds only while the core reads no clock and starts no threads: D31 (no Task and no
/// threads in gameplay, RNG seeded from the tick) and DV3 (the core has no wall clock). This test reads the
/// metadata of the three core assemblies and fails on any reference to a clock, an unseeded random generator,
/// a thread or System.Threading.Tasks.
/// </summary>
public class CoreHasNoWallClockTests
{
    private static readonly string[] CoreAssemblies = { "Anomaly.Core.Kernel", "Anomaly.Core.DmShim", "Anomaly.Core.Tg" };

    // Types the core must not reference at all.
    private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
    {
        "System.Diagnostics.Stopwatch",
        "System.Random",
        "System.TimeProvider",
        "System.Threading.Thread",
        "System.Threading.ThreadPool",
        "System.Threading.Timer",
        "System.Threading.PeriodicTimer",
        "System.Timers.Timer",
    };

    // Namespaces the core must not reference at all.
    private static readonly string[] ForbiddenNamespaces = { "System.Threading.Tasks" };

    // Members that read the clock, on types the core may otherwise use (DateTime values, Environment).
    private static readonly HashSet<string> ForbiddenMembers = new(StringComparer.Ordinal)
    {
        "System.DateTime.get_Now",
        "System.DateTime.get_UtcNow",
        "System.DateTime.get_Today",
        "System.DateTimeOffset.get_Now",
        "System.DateTimeOffset.get_UtcNow",
        "System.Environment.get_TickCount",
        "System.Environment.get_TickCount64",
    };

    public static IEnumerable<object[]> Assemblies() => CoreAssemblies.Select(a => new object[] { a });

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void TheCoreReferencesNoClockNoThreadAndNoUnseededRandom(string assemblyName)
    {
        string path = Assembly.Load(new AssemblyName(assemblyName)).Location;
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader md = pe.GetMetadataReader();

        var found = new List<string>();
        foreach (TypeReferenceHandle handle in md.TypeReferences)
        {
            string name = FullName(md, handle);
            if (ForbiddenTypes.Contains(name) || ForbiddenNamespaces.Any(ns => name.StartsWith(ns + ".", StringComparison.Ordinal)))
                found.Add(name);
        }
        foreach (MemberReferenceHandle handle in md.MemberReferences)
        {
            MemberReference member = md.GetMemberReference(handle);
            if (member.Parent.Kind != HandleKind.TypeReference)
                continue;
            string name = FullName(md, (TypeReferenceHandle)member.Parent) + "." + md.GetString(member.Name);
            if (ForbiddenMembers.Contains(name))
                found.Add(name);
        }

        Assert.True(found.Count == 0, $"{assemblyName} references {string.Join(", ", found.Distinct())}");
    }

    [Fact]
    public void TheScanSeesAClockWhereThereIsOne()
    {
        // This test assembly times its benchmarks with a Stopwatch (Kernel/HotPathBenchmarkTests); the same scan must find
        // it, or the test above proves nothing.
        using var stream = File.OpenRead(typeof(CoreHasNoWallClockTests).Assembly.Location);
        using var pe = new PEReader(stream);
        MetadataReader md = pe.GetMetadataReader();
        Assert.Contains("System.Diagnostics.Stopwatch", md.TypeReferences.Select(h => FullName(md, h)));
    }

    private static string FullName(MetadataReader md, TypeReferenceHandle handle)
    {
        TypeReference type = md.GetTypeReference(handle);
        string name = md.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            return FullName(md, (TypeReferenceHandle)type.ResolutionScope) + "+" + name;
        string ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }
}
