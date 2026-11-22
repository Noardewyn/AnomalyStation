using System.Globalization;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client;

/// <summary>
/// M3 R1 (D119): walls, doors, windows and the ceiling are as high as a tile is wide, and that height is ONE setting (<see cref="WorldScale"/>)
/// from which every height of the world derives. These tests read the setting; none changes it (it is process-wide state), they give the
/// pure functions their own height instead.
/// </summary>
public sealed class WorldScaleTests
{
    [Fact]
    public void TheWallIsAsHighAsATileIsWideByDefault()
    {
        Assert.Equal(TileSpace.TileSize, WorldScale.DefaultWallHeight);
        Assert.Equal(2f, WorldScale.WallHeight);
        Assert.Equal(WorldScale.WallHeight, TileSpace.LevelHeight);
        Assert.Equal(TileSpace.LevelHeight, TileSpace.WorldY(2) - TileSpace.WorldY(1));
        Assert.Equal(1.0, TurfKitPlan.WallVerticalRepeats);
    }

    [Theory]
    [InlineData("2", true, 2f)]
    [InlineData("2.4", true, 2.4f)]
    [InlineData("4", true, 4f)]
    [InlineData("1.99", false, 2f)]
    [InlineData("4.01", false, 2f)]
    [InlineData("NaN", false, 2f)]
    [InlineData("tall", false, 2f)]
    [InlineData("", false, 2f)]
    public void OnlyAHeightThatClearsTheEyeAndTheMobsIsTaken(string text, bool valid, float expected)
    {
        Assert.Equal(valid, WorldScale.TryParseWallHeight(text, out float metres));
        Assert.Equal(expected, metres);
        float parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : float.NaN;
        Assert.Equal(valid, WorldScale.IsValidWallHeight(parsed));
    }

    [Fact]
    public void EveryMountIsAShareOfTheWallSoItsHeightsFollowTheSetting()
    {
        float wall = WorldScale.WallHeight;
        Assert.Equal(wall * WorldScale.LampShare, PropKinds.MountHeight("/obj/machinery/light/small/directional/east"));
        Assert.Equal(wall * WorldScale.SignShare, PropKinds.MountHeight("/obj/structure/sign/poster/official/random/directional/north"));
        Assert.Equal(wall * WorldScale.SignShare, PropKinds.MountHeight("/obj/structure/noticeboard"));
        Assert.Equal(wall * WorldScale.PanelShare, PropKinds.MountHeight("/obj/machinery/airalarm/directional/west"));
        Assert.Equal(wall * WorldScale.PanelShare, PropKinds.MountHeight("/obj/item/radio/intercom/directional/south"));
        Assert.Equal(wall * WorldScale.CameraShare, PropKinds.MountHeight("/obj/machinery/camera/autoname/directional/north"));
        Assert.Equal(wall * WorldScale.OtherShare, PropKinds.MountHeight("/obj/structure/extinguisher_cabinet/directional/west"));
        Assert.Equal(WorldScale.LampHeight, PropKinds.MountHeight("/obj/machinery/light"));
        Assert.Equal(wall * WorldScale.LiftIndicatorShare, PropKinds.MountHeight("/obj/machinery/lift_indicator/directional/north"));
        Assert.Equal(wall * WorldScale.ElevatorButtonShare, PropKinds.MountHeight("/obj/machinery/button/elevator/directional/north"));
    }

    [Fact]
    public void AnElevatorsCallButtonAndItsIndicatorOnOneTileDoNotCoverEachOtherOrTheLamp()
    {
        // tg's multiz.dmm "de": the light, the elevator button and the lift indicator on one north wall; both pictures are 12 pixels (0.75 m) tall
        const float picture = 0.75f, lampBoxBottom = 1.85f - 0.07f;
        float button = PropKinds.MountHeight("/obj/machinery/button/elevator/directional/north");
        float indicator = PropKinds.MountHeight("/obj/machinery/lift_indicator/directional/north");
        Assert.True(button + picture / 2 < indicator - picture / 2, "the button's top is under the indicator's bottom");
        Assert.True(indicator + picture / 2 <= lampBoxBottom, "the indicator is under the lamp's box");
        Assert.True(button - picture / 2 > 0, "the button is above the floor");
    }

    [Fact]
    public void AtTheDefaultTheMountsAreTheOwnersHeights()
    {
        Assert.Equal(1.85f, WorldScale.LampHeight, 4);   // lamps near the top
        Assert.Equal(1.5f, WorldScale.SignHeight, 4);    // signs
        Assert.Equal(1.1f, WorldScale.PanelHeight, 4);   // buttons, APCs, alarms
        Assert.Equal(1.2f, WorldScale.OtherMountHeight, 4);
        Assert.Equal(1.7f, WorldScale.CameraHeight, 4);
    }

    [Fact]
    public void EveryMountAndTheLightBoxStayOnTheFaceBelowTheCeiling()
    {
        const float halfBox = 0.07f; // the light fixture's box is 0.14 m high (WorldGeometry)
        foreach (float wall in new[] { 2f, 2.4f, 3f, 4f })
        {
            foreach (float share in new[] { WorldScale.LampShare, WorldScale.SignShare, WorldScale.PanelShare, WorldScale.CameraShare, WorldScale.OtherShare })
            {
                float height = wall * share;
                Assert.InRange(height - halfBox, 0f, wall);
                Assert.InRange(height + halfBox, 0f, wall - 0.05f);
            }
        }
    }

    [Theory]
    [InlineData(1.1f, 0.8f)]    // an APC: stays where it is
    [InlineData(1.5f, 1.6f)]    // a poster that fills the sprite: moved down so that its top is under the ceiling
    [InlineData(1.85f, 0.5f)]   // a lamp's picture near the top
    [InlineData(1.0f, 1.9f)]    // a picture almost as tall as the wall
    [InlineData(0.1f, 0.4f)]    // one wanted too low: lifted off the floor
    public void AMountedPictureIsFullyOnTheFaceUnderTheCeiling(float wanted, float pictureHeight)
    {
        const float wall = 2f;
        float centre = WorldScale.FitMount(wanted, pictureHeight, wall);
        float bottom = centre - pictureHeight / 2f, top = centre + pictureHeight / 2f;
        Assert.InRange(bottom, 0f, wall);
        Assert.InRange(top, 0f, wall - 0.03f);
        bool fits = wanted - pictureHeight / 2f >= 0.04f && wanted + pictureHeight / 2f <= wall - 0.04f;
        if (fits)
            Assert.Equal(wanted, centre); // a picture that fits is not moved
    }

    [Fact]
    public void APictureTallerThanTheWallIsCentredOnIt()
    {
        Assert.Equal(1f, WorldScale.FitMount(1.5f, 2f, 2f));
        Assert.Equal(1.5f, WorldScale.FitMount(0.3f, 3.4f, 3f));
    }
}
