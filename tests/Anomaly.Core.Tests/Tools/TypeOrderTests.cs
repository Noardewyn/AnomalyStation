using System.Collections.Immutable;
using System.Reflection;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// The generated type and proc order (src/Core/Tg/Generated/TypeOrder.g.cs, ProcOrder.g.cs; tools/coloring,
/// docs/ai/specs/colored-procs.md "Type order") and how the shim finds it (<c>[assembly: DmTypeOrder]</c>,
/// docs/ai/specs/object-model.md section 9). Expected facts come from the order rule and tg's sources, not from the port.
/// </summary>
public class TypeOrderTests
{
    [Fact]
    public void TheCountMatchesThePathsAndTheRootChildrenComeFirst()
    {
        Assert.Equal(TypeOrder.Count, TypeOrder.Paths.Length);
        Assert.Equal(TypeOrder.Paths.Length, TypeOrder.Paths.Distinct(StringComparer.Ordinal).Count());
        // The children of the root in OpenDream's order: /world, /savefile, /datum, ... (colored-procs.md).
        Assert.Equal(new[] { "/world", "/savefile", "/datum" }, TypeOrder.Paths.Take(3));
    }

    [Fact]
    public void AParentPrecedesItsSubtreeInPreorder()
    {
        // Depth-first preorder: /atom before /atom/movable, and /obj, whose parent is /atom/movable, after both.
        int atom = TypeOrder.Paths.IndexOf("/atom");
        int movable = TypeOrder.Paths.IndexOf("/atom/movable");
        int obj = TypeOrder.Paths.IndexOf("/obj");
        Assert.True(atom >= 0 && movable >= 0 && obj >= 0);
        Assert.True(atom < movable, "/atom precedes /atom/movable");
        Assert.True(atom < obj, "/atom precedes /obj");
        Assert.True(movable < obj, "/atom/movable precedes /obj");
        Assert.True(TypeOrder.Paths.IndexOf("/datum") < atom, "/atom is under /datum");
    }

    [Fact]
    public void ProcsOfListsTheInitGlobalProcsOfGlobalVarsAndThrowsForOtherTypes()
    {
        // tg runs GLOB's initializers by typesof(/datum/controller/global_vars/proc) (controllers/globals.dm:51) and
        // checks that there is one InitGlobal<var> per global var.
        ImmutableArray<string> procs = ProcOrder.ProcsOf("/datum/controller/global_vars");
        Assert.True(procs.Length > 1000, "expected tg's GLOBAL_* initializers, got " + procs.Length);
        Assert.All(procs, name => Assert.StartsWith("InitGlobal", name, StringComparison.Ordinal));
        Assert.Equal(procs.Length, procs.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(procs, ProcOrder.Procs["/datum/controller/global_vars"]);
        Assert.Throws<KeyNotFoundException>(() => ProcOrder.ProcsOf("/datum"));
        Assert.Empty(ProcOrder.Verbs);
    }

    [Fact]
    public void TheTgAssemblyNamesItsOrderAndTheRegistryReadsIt()
    {
        DmTypeOrderAttribute attribute = typeof(TypeOrder).Assembly.GetCustomAttribute<DmTypeOrderAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(typeof(TypeOrder), attribute.TypeOrder);
        Assert.Equal(typeof(ProcOrder), attribute.ProcOrder);

        IDmTypeOrder order = DmTypes.Registry.Order;
        Assert.Equal(TypeOrder.Count, order.TypePaths.Count);
        Assert.Equal("/world", order.TypePaths[0]);
        Assert.True(order.TryGetProcs("/datum/controller/global_vars", verbs: false, out IReadOnlyList<string> procs));
        Assert.Equal(ProcOrder.ProcsOf("/datum/controller/global_vars"), procs);
        Assert.False(order.TryGetProcs("/datum/controller/global_vars", verbs: true, out _));
    }

    [Fact]
    public void WithoutTheAttributeThereIsNoOrderSource()
    {
        // The fallback: an assembly set without [assembly: DmTypeOrder] gives the empty order (the shim has none).
        DmTypeOrder order = DmTypeOrder.FromGenerated(new[] { typeof(Dm).Assembly, typeof(TypeOrderTests).Assembly });
        Assert.Empty(order.TypePaths);
        Assert.False(order.TryGetProcs("/datum/controller/global_vars", verbs: false, out _));
    }

    [Fact]
    public void TwoOrderSourcesOrAClassWithoutPathsAreConfigurationErrors()
    {
        Assembly tg = typeof(TypeOrder).Assembly;
        Assert.Throws<InvalidOperationException>(() => DmTypeOrder.FromGenerated(new[] { tg, tg }));
        Assert.Throws<InvalidOperationException>(() => DmTypeOrder.FromClasses(typeof(ProcOrder), null));
        DmTypeOrder pathsOnly = DmTypeOrder.FromClasses(typeof(TypeOrder), null);
        Assert.Equal(TypeOrder.Count, pathsOnly.TypePaths.Count);
        Assert.False(pathsOnly.TryGetProcs("/datum/controller/global_vars", verbs: false, out _));
    }
}
