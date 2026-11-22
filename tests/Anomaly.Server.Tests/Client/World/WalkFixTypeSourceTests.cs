using System.Reflection;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 R2: the fake feed's type source: the generated class is the truth for the vars it sets, the path rules add only what tg's init does on top,
/// the loading does not depend on which assemblies the process had loaded, and Meta joins through the real codec with the classes' lights.
/// </summary>
public sealed class WalkFixTypeSourceTests : WalkFixBase
{
    public WalkFixTypeSourceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("/obj/machinery/firealarm/directional/north", 0, 26)]
    [InlineData("/obj/machinery/firealarm/directional/south", 0, -26)]
    [InlineData("/obj/machinery/firealarm/directional/east", 26, 0)]
    [InlineData("/obj/machinery/airalarm/directional/west", -27, 0)]
    [InlineData("/obj/structure/extinguisher_cabinet/directional/east", 29, 0)]
    [InlineData("/obj/machinery/button/door/directional/north", 0, 24)]
    [InlineData("/obj/machinery/power/apc/auto_name/directional/south", 0, -25)]
    [InlineData("/obj/item/radio/intercom/directional/west", -27, 0)]
    [InlineData("/obj/machinery/camera/directional/north", 0, 0)]
    [InlineData("/obj/machinery/light/directional/east", 0, 0)]
    [InlineData("/obj/structure/chair/stool/directional/north", 0, 0)]
    [InlineData("/obj/structure/chair/stool/bar/directional/west", 0, 0)]
    [InlineData("/obj/structure/sink/directional/north", 0, -14)]
    [InlineData("/obj/machinery/shower/directional/west", 16, 0)]
    [InlineData("/obj/machinery/door/window/left/directional/north", 0, 0)]
    [InlineData("/obj/structure/sign/departments/medbay/directional/north", 0, 32)]
    public void DirectionalMountsGetTgsRealOffsets(string path, int px, int py)
    {
        Assert.True(FakeMap.DefaultTypes().TryGet(path, out TypeDefaults d), path);
        Assert.Equal((px, py), ((int)d.PixelX, (int)d.PixelY));
    }

    [Theory]
    [InlineData("/obj/machinery/door/poddoor/shutters/preopen")]
    [InlineData("/obj/machinery/door/poddoor/preopen")]
    [InlineData("/obj/machinery/door/poddoor/shutters/radiation/preopen")]
    public void PreopenDoorsAreOpenInTheTypeSource(string path)
    {
        Assert.True(FakeMap.DefaultTypes().TryGet(path, out TypeDefaults d));
        Assert.Equal("open", d.IconState);
        Assert.False(d.Density);
        Assert.False(d.Opacity);
        // The closed family they belong to is still closed.
        Assert.True(FakeMap.DefaultTypes().TryGet(path.Replace("/preopen", string.Empty), out TypeDefaults closed));
        Assert.True(closed.Density);
    }

    [Fact]
    public void ALightWithoutALightSystemIsNoLight()
    {
        // tg's /obj/item/camera has light_range 6 and light_power 2 but light_system NO_LIGHT_SUPPORT (0): not a light (light-grid.md 1).
        var source = new ReflectionTypeSource();
        Assert.True(source.TryGet("/obj/item/camera", out TypeDefaults camera));
        Assert.True(camera.Light.IsNone);
        // And no class's light can break the codec: every light a class gives is one the wire accepts (system 1..4, range and power in range).
        foreach (string path in AllClassPaths())
        {
            if (!source.TryGet(path, out TypeDefaults d) || d.Light.IsNone)
                continue;
            Assert.InRange((int)d.Light.System, 1, 4);
            Assert.InRange(d.Light.Range, 0.0001f, Limits.MaxLightRange);
            Assert.InRange(Math.Abs(d.Light.Power), 0f, Limits.MaxLightPower);
        }
    }

    private static void AssertSame(TypeDefaults a, TypeDefaults b)
    {
        foreach (PropertyInfo property in typeof(TypeDefaults).GetProperties())
            Assert.True(Equals(property.GetValue(a), property.GetValue(b)), $"{a.Path}: {property.Name} {property.GetValue(a)} vs {property.GetValue(b)}");
    }

    [Fact]
    public void APathTgHasNotGotIsAMissWithoutTheFullIndex()
    {
        // The directional window's renderer asks for "<parent>/fulltile" up its parents: those paths are not tg's, so they are misses
        // straight away (the full index, seconds to build, is only for tg's paths whose class the name does not give); tg's own resolve.
        var types = new ReflectionTypeSource();
        Assert.False(types.TryGet("/obj/structure/window/reinforced/spawner/directional/east/fulltile", out _));
        Assert.True(types.TryGet("/obj/structure/window/reinforced/fulltile", out TypeDefaults full));
        Assert.Equal("/obj/structure/window/reinforced/fulltile", full.Path);
    }

    [Fact]
    public void TheTypeSourceGivesTheSameAnswerWhetherOrNotTgWasLoadedBefore()
    {
        // The default source names the Tg assembly itself instead of scanning the AppDomain, so it is the same source in the game (where
        // nothing had loaded it yet) and here (where the test project did): the explicit source over the same assemblies answers alike.
        var byDefault = new ReflectionTypeSource();
        var explicitly = new ReflectionTypeSource(typeof(DmPathAttribute).Assembly, typeof(TgWorld).Assembly);
        Assert.True(byDefault.ClassCount > 40000, "the default source has the generated classes: " + byDefault.ClassCount);
        Assert.Equal(explicitly.ClassCount, byDefault.ClassCount);
        Assert.Equal(typeof(TgWorld).Assembly, ReflectionTypeSource.TgAssembly);
        foreach (string path in AllClassPaths().Where(p => p.StartsWith("/obj/machinery", StringComparison.Ordinal)).Take(400))
        {
            bool a = byDefault.TryGet(path, out TypeDefaults x), b = explicitly.TryGet(path, out TypeDefaults y);
            Assert.Equal(a, b);
            if (a)
                AssertSame(x, y);
        }
        // The merged source built twice, and over the explicit assemblies, answers alike for what a map uses.
        ITypeSource one = FakeMap.DefaultTypes(), two = new MergedTypeSource(explicitly, new PathRuleTypeSource(specificOnly: true), new PathRuleTypeSource());
        foreach (string path in new[] { "/turf/closed/wall", "/obj/structure/table", "/obj/machinery/door/poddoor/shutters/preopen", "/obj/machinery/light/directional/north", "/obj/machinery/atmospherics/pipe/smart/simple/general" })
        {
            Assert.True(one.TryGet(path, out TypeDefaults x));
            Assert.True(two.TryGet(path, out TypeDefaults y));
            AssertSame(x, y);
        }
    }

    [Fact]
    public void TheMergeKeepsTheClassesVarsAndAddsOnlyWhatTheRuleKnows()
    {
        var cls = new TypeDefaults { Path = "/obj/x", Icon = "icons/a.dmi", IconState = "open", Density = false, Opacity = false, Layer = 2.65f, PixelX = 26, Dir = 4, OnBorder = false };
        var rule = new TypeDefaults { Path = "/obj/x", Icon = "icons/b.dmi", IconState = "closed", Density = true, Opacity = true, Layer = 3.3f, PixelX = 32, Dir = 2, OnBorder = true, BaseIconState = "door" };
        TypeDefaults merged = MergedTypeSource.Merge(cls, rule);
        Assert.Equal(("icons/a.dmi", "open", false, false, 2.65f, (short)26, (byte)4), (merged.Icon, merged.IconState, merged.Density, merged.Opacity, merged.Layer, merged.PixelX, merged.Dir));
        Assert.True(merged.OnBorder); // a border flag a rule knows is kept
        Assert.Equal("door", merged.BaseIconState);

        // A light the class does not have yet (tg sets a fixture's in its init) comes from the rule; one the class has is not replaced.
        var lit = new LightState(8f, 1f, 0xFFFFFFFF, true, 170f, 0, 1f, 0f, 0f, 1);
        var other = new LightState(1.5f, 0.5f, 0xFFFFFFFF, true, 360f, 0, 1f, 0f, 0f, 1);
        Assert.Equal(lit, MergedTypeSource.Merge(cls, new TypeDefaults { Path = "/obj/x", Light = lit }).Light);
        Assert.Equal(other, MergedTypeSource.Merge(new TypeDefaults { Path = "/obj/x", Icon = "icons/a.dmi", Light = other }, new TypeDefaults { Path = "/obj/x", Light = lit }).Light);

        // A hidden family is final (tg hides it in init); a type with no class is the rule's, and one with no rule the class's.
        TypeDefaults hidden = new() { Path = "/obj/x", Invisibility = 101f };
        Assert.Same(hidden, MergedTypeSource.Merge(cls, hidden));
        Assert.Same(rule, MergedTypeSource.Merge(null, rule));
        Assert.Same(cls, MergedTypeSource.Merge(cls, null));

        // A class with no picture takes the rule's, and so does one with an icon but no state: a camera's class has the fax machine's file and no state.
        TypeDefaults bare = MergedTypeSource.Merge(new TypeDefaults { Path = "/obj/x", Density = true }, rule);
        Assert.Equal(("icons/b.dmi", "closed", true), (bare.Icon, bare.IconState, bare.Density));
        TypeDefaults noState = MergedTypeSource.Merge(new TypeDefaults { Path = "/obj/x", Icon = "icons/obj/machines/fax.dmi" }, rule);
        Assert.Equal(("icons/b.dmi", "closed"), (noState.Icon, noState.IconState));
        Assert.True(FakeMap.DefaultTypes().TryGet("/obj/machinery/camera/directional/north", out TypeDefaults camera));
        Assert.Equal(("icons/obj/machines/camera.dmi", "camera"), (camera.Icon, camera.IconState));
    }

    [Fact]
    public void OnBorderIsTgsFlagBit()
    {
        // ON_BORDER_1 is 1 << 1 in this snapshot (code/__DEFINES/_flags.dm:25); it was read as 1 << 9, so no class ever had it.
        var source = new ReflectionTypeSource();
        foreach (string path in new[] { "/obj/structure/railing", "/obj/machinery/door/window/left/directional/north", "/obj/structure/window/spawner/directional/north", "/obj/machinery/door/firedoor/border_only" })
        {
            Assert.True(source.TryGet(path, out TypeDefaults d), path);
            Assert.True(d.OnBorder, path);
        }
        Assert.True(source.TryGet("/obj/structure/window/fulltile", out TypeDefaults fulltile));
        Assert.False(fulltile.OnBorder);
        Assert.True(source.TryGet("/obj/machinery/door/airlock", out TypeDefaults airlock));
        Assert.False(airlock.OnBorder);
    }

    [Fact]
    public void OnlyTypesThatSmoothHaveASmoothingStem()
    {
        var source = new ReflectionTypeSource();
        Assert.True(source.TryGet("/turf/closed/wall", out TypeDefaults wall));
        Assert.Equal("wall", wall.BaseIconState);
        Assert.True(source.TryGet("/obj/structure/table", out TypeDefaults table));
        Assert.Equal("table", table.BaseIconState);
        // A door and a button have a base_icon_state (the stem of their own states) but do not smooth: no junction may be written into their state.
        Assert.True(source.TryGet("/obj/machinery/door/airlock", out TypeDefaults airlock));
        Assert.Null(airlock.BaseIconState);
        Assert.True(source.TryGet("/obj/machinery/button/door", out TypeDefaults button));
        Assert.Null(button.BaseIconState);
    }

    // ---- Meta through the fake feed ----

    [Fact]
    public void MetaJoinsThroughTheFakeFeedWithTheGeneratedClassesAndPreopenDoorsAreOpen()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        // The join encodes every entity's light: the codec refuses a light system outside 1..4 (a /obj/item/camera had one of 0).
        var feed = new FakeFeed("Tester", seed: 1, bots: 0, oneWayLatencyMs: 0, jitterMs: 0, map: world.Map);
        feed.Poll(0);
        feed.Poll(0.1);
        Assert.Equal(FeedPhase.Playing, feed.Phase);

        int preopen = 0;
        foreach (ReplicaEntity e in feed.Replica.Entities)
        {
            if (!e.TypePath.StartsWith("/obj/machinery/door/poddoor", StringComparison.Ordinal) || !e.TypePath.EndsWith("/preopen", StringComparison.Ordinal))
                continue;
            preopen++;
            Assert.True(feed.Replica.TryGetAppearance(e.State.Appearance, out AppearanceEntry entry));
            Assert.Equal("open", feed.Replica.ResolveString(entry.IconState));
        }
        Assert.True(preopen > 100, "Meta has over a hundred preopen shutters and blast doors: " + preopen);
        // An open shutter or blast door leaves its cell passable unless something else stands in it: the R&D shutters of the owner's shots 2 and 5 are
        // behind full-tile windows (solid), the corridor's are not.
        int passable = 0;
        foreach (FakeMapEntity door in world.Map.Entities.Where(e => e.TypePath.StartsWith("/obj/machinery/door/poddoor", StringComparison.Ordinal) && e.TypePath.EndsWith("/preopen", StringComparison.Ordinal)))
        {
            Assert.False(door.Density);
            Assert.False(door.Opacity);
            bool otherSolid = world.Closed[(door.X - 1) + (door.Y - 1) * world.Map.Width]
                || world.Map.Entities.Any(o => o.X == door.X && o.Y == door.Y && o.Id != door.Id && o.Density && !o.OnBorder);
            if (otherSolid)
                continue;
            passable++;
            Assert.False(feed.Replica.IsSolid(door.X, door.Y, 1), $"({door.X},{door.Y}) under an open {door.TypePath}");
        }
        Assert.True(passable > 20, "dozens of Meta's open doors have nothing else in their cell: " + passable);
        ReplicaEntity foyer = feed.Replica.Entities.First(e => e.State.X == 94 && e.State.Y == 107 && e.TypePath == "/obj/machinery/door/poddoor/preopen");
        Assert.True(feed.Replica.TryGetAppearance(foyer.State.Appearance, out AppearanceEntry foyerEntry));
        Assert.Equal("open", feed.Replica.ResolveString(foyerEntry.IconState));
    }

    [RealIconsFact]
    public void EveryEntityOfMetaHasAPictureInTheConvertedIcons()
    {
        MetaWorld world = RequireMeta();
        if (world == null)
            return;
        // A class that only declares what it changed (a camera's, under a hand-written skeleton) has the base's icon and no state: the entity would be
        // a placeholder box in the corridor. Every (icon, state) a Meta entity replicates must be in the converted icons.
        IconLibrary library = RealIcons.NewLibrary();
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (FakeMapEntity e in world.Map.Entities)
        {
            bool ok = e.Icon != null && library.TryGetIcon(e.Icon, out IIcon icon) && icon.TryGetState(e.IconState ?? string.Empty, out _);
            if (!ok)
                missing.Add($"{e.Icon}:{e.IconState} ({e.TypePath})");
        }
        Assert.True(missing.Count == 0, string.Join("\n", missing.Take(10)));
    }
}
