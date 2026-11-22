using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.M3Contracts;

/// <summary>The M3 contracts step's inert seam (Tg/Infrastructure/InertSeam.cs, docs/ai/specs/types-converter.md section 7).</summary>
public class InertSeamContractTests
{
    [Fact]
    public void ATypeIsInertExactlyWhenTheGeneratorMarksIt()
    {
        // M3 P4 switched the seam on with dm_inert_initialize: the class's own [DmInert] (not inherited) decides.
        foreach (Type t in typeof(atom).Assembly.GetTypes())
        {
            if (!typeof(atom).IsAssignableFrom(t)) continue;
            Assert.Equal(t.IsDefined(typeof(DmInertAttribute), false), InertSeam.IsInertType(t));
        }
        Assert.False(InertSeam.IsInertType(typeof(atom)));
        Assert.False(InertSeam.IsInertType(typeof(turf)));
        Assert.False(InertSeam.IsInertType(typeof(area)));
    }

    [Fact]
    public void TheSeamRefusesNull()
    {
        Assert.Throws<ArgumentNullException>(() => InertSeam.IsInert(null));
        Assert.Throws<ArgumentNullException>(() => InertSeam.IsInertType(null));
    }

    [Fact]
    public void TheEnginePartIsDeclaredOnAtomTurfAndArea()
    {
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (Type t in new[] { typeof(atom), typeof(turf), typeof(area) })
        {
            var m = t.GetMethod("dm_inert_initialize", Flags);
            Assert.NotNull(m);
            Assert.Equal(typeof(object), m.ReturnType);
            Assert.Equal(typeof(DmList), Assert.Single(m.GetParameters()).ParameterType);
        }
    }
}
