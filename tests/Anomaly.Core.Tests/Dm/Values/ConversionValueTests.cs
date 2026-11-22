using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Contracts;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// The values-owned type tests and conversions beyond the contracts' core (docs/ai/translation-rules.md 3.5, 4.1, 10.4):
/// BYOND ref <c>/proc/isfile</c>, <c>/proc/isicon</c>, <c>/proc/isnum</c>, <c>/operator/!</c>.
/// </summary>
public class ConversionValueTests
{
    [Fact]
    public void NoValueThePortHoldsIsAFileOrAnIcon()
    {
        // isfile is true only for a resource or file() object and isicon only for an icon (BYOND ref; OpenDream
        // NativeProc_isfile, NativeProc_isicon); text naming a file and every other value below give false, as in DM.
        object[] values = { null, 0f, "icons/obj/x.dmi", Dm.list(), new TestDatum(), new TestAtom(), typeof(DmList), Dm.matrix(), Dm.sound("a.ogg") };
        foreach (object value in values)
        {
            Assert.False(Dm.isfile(value));
            Assert.False(Dm.isicon(value));
        }
    }

    [Fact]
    public void NotAndNumOfEveryKind()
    {
        // BYOND ref /operator/!: "The number 0, an empty string, or null are all false values. Everything else is true."
        Assert.Equal(1f, Dm.not((object)0f));
        Assert.Equal(1f, Dm.not((object)""));
        Assert.Equal(1f, Dm.not((object)null));
        Assert.Equal(0f, Dm.not((object)Dm.list()));
        Assert.Equal(0f, Dm.not((object)float.NaN));
        Assert.Equal(0f, Dm.not(typeof(DmList)));
        Assert.True(Dm.isnum(true));                  // a boxed C# bool counts as a number (rules 3.1)
        V.Error(() => Dm.num(Dm.matrix()));
    }
}
