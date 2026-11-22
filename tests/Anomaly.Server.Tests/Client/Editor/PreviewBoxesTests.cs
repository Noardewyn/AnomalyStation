using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Editor;
using Xunit;

namespace Anomaly.Server.Tests.Client.Editor;

/// <summary>The boxes the view tints for the map editor's preview (docs/ai/specs/map-editor.md 5.9), from the shapes a click is tested against.</summary>
public sealed class PreviewBoxesTests
{
    private const int Precision = 2; // (a box reaches a millimetre past its tile)

    private static void Same(float expected, float actual) => Assert.Equal(expected, actual, Precision);

    [Fact]
    public void ATurfsBoxIsItsTilesBlockOrItsFloor()
    {
        // tile (3, 2) spans east 4 to 6 and north 2 to 4, which is world z -4 to -2
        PreviewBox wall = PreviewBoxes.OfTurf(3, 2, 1, closed: true);
        Assert.True(wall.Turf);
        Same(4f, wall.MinX);
        Same(6f, wall.MaxX);
        Same(-4f, wall.MinZ);
        Same(-2f, wall.MaxZ);
        Same(0f, wall.MinY);
        Assert.InRange(wall.MaxY, TileSpace.LevelHeight + 0.02f, TileSpace.LevelHeight + 0.05f); // (a wall's top with nothing over it stands 2 cm higher)

        PreviewBox floor = PreviewBoxes.OfTurf(3, 2, 2, closed: false);
        Assert.InRange(TileSpace.WorldY(2), floor.MinY, floor.MaxY);
        Assert.True(floor.MaxY - floor.MinY < 0.05f); // the floor's plane, not what stands on it
    }

    [Fact]
    public void AThingsBoxHoldsEveryShapeOfIt()
    {
        // a model's two pieces on level 2, a picture turned to the eye, and a thing that is no preview
        PickShape seat = PickShape.BoxOf(7, PickSurface.StaticProp, 3, 2, 2, 4.5, 2.5, 0.0, 5.5, 3.5, 0.5, 2.0);
        PickShape back = PickShape.BoxOf(7, PickSurface.StaticProp, 3, 2, 2, 4.5, 3.3, 0.5, 5.5, 3.5, 1.2, 2.0);
        PickShape item = PickShape.StandingPicture(8, PickSurface.StaticProp, 3, 2, 1, 5.0, 3.0, 0.8, new Anomaly.Client.Shared.Icons.SpriteRect(-0.5, 0.5, 0.2, 1.0, 0.0625), null, 0, 0);
        PickShape other = PickShape.BoxOf(9, PickSurface.StaticProp, 3, 2, 1, 4.0, 2.0, 0.0, 6.0, 4.0, 2.0, 2.0);
        var boxes = new List<PreviewBox>();
        PreviewBoxes.AddThings(new[] { seat, other, back, item }, id => id != 9, boxes);

        Assert.Equal(2, boxes.Count);
        PreviewBox chair = boxes[0];
        Assert.False(chair.Turf);
        float level = TileSpace.WorldY(2);
        Assert.InRange(chair.MinX, 4.4f, 4.5f);
        Assert.InRange(chair.MaxX, 5.5f, 5.6f);
        Assert.InRange(chair.MinZ, -3.6f, -3.5f); // north 2.5 to 3.5 is world z -3.5 to -2.5
        Assert.InRange(chair.MaxZ, -2.5f, -2.4f);
        Assert.InRange(chair.MinY, level - 0.1f, level);
        Assert.InRange(chair.MaxY, level + 1.2f, level + 1.3f);

        // a picture turned to the eye is asked where it stands (its depth on the screen is not its own): a small box around that place
        PreviewBox picture = boxes[1];
        Assert.InRange(5.0f, picture.MinX, picture.MaxX);
        Assert.InRange(-3.0f, picture.MinZ, picture.MaxZ);
        Assert.InRange(0.8f, picture.MinY, picture.MaxY);
        Assert.True(picture.MaxX - picture.MinX < 0.1f && picture.MaxY - picture.MinY < 0.1f);
    }

    [Fact]
    public void NoMoreBoxesThanTheViewTakes()
    {
        var shapes = Enumerable.Range(1, PreviewBoxes.Most + 20).Select(id => PickShape.BoxOf(id, PickSurface.StaticProp, 1, 1, 1, 0, 0, 0, 1, 1, 1, 2.0)).ToList();
        var boxes = new List<PreviewBox>();
        PreviewBoxes.AddThings(shapes, _ => true, boxes);
        Assert.Equal(PreviewBoxes.Most, boxes.Count);
    }
}
