using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M3 I1c: the fake feed draws tg's <c>post_init_icon_state</c> (atoms_initializing_EXPENSIVE.dm:138-139): a type whose map-editor preview state differs from
/// the state its init sets (a meter is <c>meterX</c> in the editor and <c>meter</c> in the game) draws the post-init one, like the real server's world.
/// </summary>
public sealed class PostInitIconStateTests
{
    private readonly ITestOutputHelper _output;

    public PostInitIconStateTests(ITestOutputHelper output) => _output = output;

    private const string Fixture = @"anomaly-map 1
size 4 1 1
chunk 32
default /turf/open/space/basic | /area/space

[chunk 1 1 1]
1,1: /obj/machinery/meter | /turf/open/floor/plating | /area/station/hallway
2,1: /obj/machinery/meter{icon_state = ""meterX""} | /turf/open/floor/plating | /area/station/hallway
3,1: /obj/machinery/portable_atmospherics/canister | /turf/open/floor/plating | /area/station/hallway
4,1: /obj/structure/table | /turf/open/floor/plating | /area/station/hallway
";

    [Fact]
    public void ThePostInitStateWinsOverThePreviewAndOverTheMapsOwnOverride()
    {
        var defaults = new TypeDefaults { Path = "/x", IconState = "preview", PostInitIconState = "game" };
        Assert.Equal("game", defaults.StateAfterInit(null));
        Assert.Equal("game", defaults.StateAfterInit("override"));
        var none = new TypeDefaults { Path = "/y", IconState = "preview" };
        Assert.Equal("preview", none.StateAfterInit(null));
        Assert.Equal("override", none.StateAfterInit("override"));
    }

    [Fact]
    public void TheClassesPostInitStateIsReadAndAnEmptyOneIsNone()
    {
        var types = new ReflectionTypeSource();
        Assert.True(types.TryGet("/obj/machinery/meter", out TypeDefaults meter));
        Assert.Equal("meter", meter.PostInitIconState);
        Assert.NotEqual("meter", meter.IconState); // the editor's preview
        // tg's canisters say post_init_icon_state = "" (canister.dm:11), which DM reads as unset.
        Assert.True(types.TryGet("/obj/machinery/portable_atmospherics/canister", out TypeDefaults canister));
        Assert.Null(canister.PostInitIconState);
    }

    [Fact]
    public void AGreyscaleTypeDrawsItsGeneratedIconWithThePostInitState()
    {
        var types = new ReflectionTypeSource();
        // The navy officer's beret: icons/map_icons/clothing/head/beret.dmi with the type's own path as the state is the map editor's preview; init (update_greyscale) replaces the icon with the
        // generated one, whose state is post_init_icon_state "beret_badge": the resource the real server replicates is gags:<config>|<colours>.
        Assert.True(types.TryGet("/obj/item/clothing/head/beret/sec/navyofficer", out TypeDefaults beret));
        Assert.Equal("beret_badge", beret.PostInitIconState);
        Assert.StartsWith("icons/map_icons/", beret.Icon);
        Assert.Equal("/datum/greyscale_config/beret_badge", beret.GreyscaleConfig);
        Assert.Equal("gags:/datum/greyscale_config/beret_badge|#638799#a52f29", beret.IconAfterInit(null));
        Assert.Equal("gags:/datum/greyscale_config/beret_badge|#112233", beret.IconAfterInit("#112233")); // the map's own greyscale_colors win
        // A greyscale type with no post-init state keeps its picture (a canister's is all overlays).
        Assert.True(types.TryGet("/obj/machinery/portable_atmospherics/canister", out TypeDefaults canister));
        Assert.NotNull(canister.GreyscaleConfig);
        Assert.Equal(canister.Icon, canister.IconAfterInit(null));
    }

    [Fact]
    public void AMeterDrawsItsGameStateAndTheChangeIsListed()
    {
        FakeMap map = FakeMap.Parse(Fixture, "post-init");
        FakeMapEntity[] meters = map.Entities.Where(e => e.TypePath == "/obj/machinery/meter").ToArray();
        Assert.Equal(2, meters.Length);
        Assert.All(meters, m => Assert.Equal("meter", m.IconState)); // the map's own icon_state = "meterX" is replaced too, as tg's init does
        Assert.Equal("meter", map.Entities.Where(e => e.TypePath == "/obj/machinery/meter").Select(e => e.IconState).Distinct().Single());
        Assert.Equal(2, map.PostInitChanges["/obj/machinery/meter"].Count);
        Assert.Equal("meter", map.PostInitChanges["/obj/machinery/meter"].Drawn);
        Assert.DoesNotContain("/obj/structure/table", map.PostInitChanges.Keys);
        Assert.DoesNotContain("/obj/machinery/portable_atmospherics/canister", map.PostInitChanges.Keys);
    }

    [Fact]
    public void MetaDrawsThePostInitStateOfEveryTypeThatHasOne()
    {
        string path = FakeMapTests.MetaPath();
        if (path == null)
        {
            _output.WriteLine("no converted MetaStation: skipped");
            return;
        }
        FakeMap map = FakeMap.Load(path);
        var changes = map.PostInitChanges;
        int things = changes.Values.Sum(c => c.Count);
        _output.WriteLine($"Meta: {changes.Count} types ({things} things of {map.Entities.Count}) draw their post-init icon state instead of the editor's preview state");
        foreach ((string type, PostInitChange change) in changes.OrderByDescending(c => c.Value.Count).ThenBy(c => c.Key))
            _output.WriteLine($"  {change.Count,4} x {type}: {change.Preview} -> {change.Drawn}");
        Assert.True(changes.Count > 0);
        Assert.All(changes, c => Assert.NotEqual(c.Value.Preview, c.Value.Drawn));
        Assert.Contains("/obj/machinery/meter", changes.Keys);
        foreach (FakeMapEntity e in map.Entities.Where(e => changes.ContainsKey(e.TypePath)))
            Assert.Equal(changes[e.TypePath].Drawn, e.IconState);
        int generated = map.Entities.Count(e => e.Icon != null && e.Icon.StartsWith("gags:", StringComparison.Ordinal) && changes.ContainsKey(e.TypePath));
        _output.WriteLine($"  of which {generated} things draw a generated greyscale icon (gags:...)");
    }
}
