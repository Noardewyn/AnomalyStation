using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

// The dev menu's world looks (WorldLooks): a type's look by hand, a subtype taking its nearest tuned ancestor's, applied by the planner.
public class WorldLooksTests
{
    private const string Blood = "/obj/effect/decal/cleanable/blood";

    [Fact]
    public void ASubtypeTakesItsNearestTunedAncestorsLook()
    {
        var looks = WorldLooks.Parse("""{ "/obj/item/bodypart": { "scale": 1.3 }, "/obj/item/bodypart/leg": { "mode": "floor", "x": 0.1 } }""");
        Assert.Equal(WorldLookMode.Floor, looks.Find("/obj/item/bodypart/leg/left").Mode);
        Assert.Equal("/obj/item/bodypart/leg", looks.Source("/obj/item/bodypart/leg/left"));
        Assert.Equal(1.3f, looks.Find("/obj/item/bodypart/arm/right").Size);
        Assert.True(looks.Find("/obj/item/bodyparts").IsNone); // a name that only starts the same is no subtype
        Assert.True(looks.Find("/obj/item").IsNone);
        Assert.False(looks.Unsaved);
    }

    [Fact]
    public void ALookRoundTripsThroughTheFileAndNoneRemovesIt()
    {
        var looks = WorldLooks.Parse(null);
        looks.Set(Blood, new WorldLook(WorldLookMode.Floor, 1.5f, 0f, 0.1f, -0.2f));
        Assert.True(looks.Unsaved);
        Assert.Equal(new WorldLook(WorldLookMode.Floor, 1.5f, 0f, 0.1f, -0.2f), WorldLooks.Parse(looks.ToJson()).Find(Blood));
        looks.MarkSaved();
        looks.Set(Blood, default);
        Assert.False(looks.HasOwn(Blood));
        Assert.Equal(WorldLookMode.Floor, looks.Saved(Blood).Mode);
        Assert.True(looks.Unsaved);
    }

    [Fact]
    public void ThePlannerDrawsATunedTypeFlatOrStandingAndCarriesItsSizeAndMove()
    {
        // tg's gibs (GIB_LAYER on the game plane) lie on the floor by the rules; a leg (an item) stands
        PlanEntity gibs = new(1, Blood + "/gibs", 5, 5, 1, ByondDirConst.South, TableRef.Public(1), 0, 0, LightState.None, "gib1", FloorLayers.GamePlane, FloorLayers.Gib);
        PlanEntity leg = new(2, "/obj/item/bodypart/leg/left", 5, 5, 1, ByondDirConst.South, TableRef.Public(1), 0, 0, LightState.None, "default_human_l_leg");
        var looks = WorldLooks.Parse("""{ "/obj/effect/decal/cleanable": { "mode": "billboard" }, "/obj/item/bodypart/leg": { "mode": "floor", "scale": 1.4, "x": 0.25 } }""");
        Assert.True(ChunkPlanner.TryPlace(gibs, new PlanProbe { IsWall = (x, y, z) => false }, out Placed rules, new WorldLooks()));
        Assert.Equal(PlacedKind.FloorDecal, rules.Kind);
        Assert.True(ChunkPlanner.TryPlace(gibs, new PlanProbe { IsWall = (x, y, z) => false }, out Placed standing, looks));
        Assert.Equal(PlacedKind.Billboard, standing.Kind);
        Assert.True(ChunkPlanner.TryPlace(leg, new PlanProbe { IsWall = (x, y, z) => false }, out Placed flat, looks));
        Assert.Equal(PlacedKind.FloorDecal, flat.Kind);
        Assert.Equal(FloorLayers.TunedLift, flat.Height);
        Assert.Equal(1.4f, flat.Scale);
        Assert.Equal(0.25f, flat.OffsetX);
        Assert.True(ChunkPlanner.TryPlace(leg, new PlanProbe { IsWall = (x, y, z) => false }, out Placed item, new WorldLooks()));
        Assert.Equal(PlacedKind.Item, item.Kind);
        Assert.Equal(1f, item.Scale);
    }
}
