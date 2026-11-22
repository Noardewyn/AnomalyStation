using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>Resource literals ('icons/x.dmi'), translation-rules.md 3.7: Dm.resource and the DmResource value.</summary>
public class ResourceTests
{
    [Fact]
    public void AResourceIsAFileAndAnImageResourceIsAnIcon()
    {
        Assert.True(Dm.isfile(Dm.resource("icons/turf/floors.dmi")));
        Assert.True(Dm.isicon(Dm.resource("icons/turf/floors.dmi")));
        Assert.True(Dm.isicon(Dm.resource("icons/x.PNG")));
        Assert.True(Dm.isfile(Dm.resource("sound/ambience/x.ogg")));
        Assert.False(Dm.isicon(Dm.resource("sound/ambience/x.ogg")));
        Assert.False(Dm.istext(Dm.resource("icons/turf/floors.dmi")));
    }

    [Fact]
    public void ResourcesOfOnePathAreEqualAndNeverEqualText()
    {
        object a = Dm.resource("icons/a.dmi");
        object b = Dm.resource("icons/a.dmi");
        Assert.True(Dm.eq(a, b));
        Assert.False(Dm.eq(a, Dm.resource("icons/b.dmi")));
        Assert.False(Dm.eq(a, "icons/a.dmi"));
        Assert.False(Dm.eq("icons/a.dmi", a));
        Assert.True(Dm.truthy(a));
    }

    [Fact]
    public void AResourceRendersAsItsPath()
    {
        object a = Dm.resource("sound/y.ogg");
        Assert.Equal("sound/y.ogg", Dm.text("[]", a));
        Assert.Equal("x sound/y.ogg", Dm.text("x []", a));
    }

    [Fact]
    public void ResourcesOfOnePathAreOneListKey()
    {
        DmList L = Dm.list();
        L[Dm.resource("sound/y.ogg")] = 1;
        L[Dm.resource("sound/y.ogg")] = 2;
        Assert.Equal(1, L.Count);
        Assert.True(Dm.eq(L[Dm.resource("sound/y.ogg")], 2));
        Assert.Null(L["sound/y.ogg"]);
    }

    [Fact]
    public void AnEmptyResourceIsARuntime()
    {
        Assert.Throws<DmRuntimeException>(() => Dm.resource(""));
    }
}
