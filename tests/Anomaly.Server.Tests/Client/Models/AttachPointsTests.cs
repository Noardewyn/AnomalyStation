using System.Numerics;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// Attach points (docs/ai/specs/models.md): where a thing stuck to another goes (C-4's charge). A thing's bounds give its sides and top; a model names its own in
/// the catalog or as Attach_* nodes of its file; what is stuck takes the point facing its planter, the top when none does, a low thing's top always.
/// </summary>
public class AttachPointsTests
{
    private const byte North = 1, South = 2, East = 4, West = 8;

    [Fact]
    public void ATallThingHasItsFourSidesAndItsTopAtTheHeightAsked()
    {
        AttachPoint[] points = AttachPoints.OfBox(new Vector3(-1, 0, -0.5f), new Vector3(1, 2, 0.5f), 1.1f);
        Assert.Equal(5, points.Length);
        AttachPoint south = Assert.Single(points, p => p.Name == AttachPoints.South);
        Assert.Equal(new Vector3(0, 1.1f, 0.5f), south.Position);
        Assert.Equal(Vector3.UnitZ, south.Normal);
        Assert.Equal(new Vector3(0, 2, 0), Assert.Single(points, p => p.IsTop).Position);
        // kept inside the face: a short locker's side point is not above its top
        AttachPoint low = Assert.Single(AttachPoints.OfBox(Vector3.Zero, new Vector3(1, 1.2f, 1), 1.1f), p => p.Name == AttachPoints.North);
        Assert.Equal(1.2f - AttachPoints.Inset, low.Position.Y, 4);
    }

    [Fact]
    public void ALowThingHasOnlyItsTop()
    {
        AttachPoint point = Assert.Single(AttachPoints.OfBox(Vector3.Zero, new Vector3(2, 0.8f, 2), 1.1f));
        Assert.True(point.IsTop);
        Assert.Equal(point, AttachPoints.Choose(new[] { point }, South));
    }

    [Theory]
    [InlineData(South, AttachPoints.South)]
    [InlineData(North, AttachPoints.North)]
    [InlineData(East, AttachPoints.East)]
    [InlineData(North | East, AttachPoints.North)] // a diagonal: the first of the best (the long north face of a door wins over its end in practice)
    [InlineData(0, AttachPoints.Top)]
    public void TheSideFacingThePlanterIsTaken(byte dir, string expected)
    {
        AttachPoint[] points = AttachPoints.OfBox(new Vector3(-1, 0, -1), new Vector3(1, 2, 1), 1.1f);
        Assert.Equal(expected, AttachPoints.Choose(points, dir)?.Name);
    }

    [Fact]
    public void AWallTakesOnlyItsOpenSidesAndFallsBackToTheNearest()
    {
        // a wall with floor only to its south: a charge from the west (a diagonal approach) still goes on the south face
        AttachPoint[] faces = AttachPoints.OfBox(new Vector3(-1, 0, -1), new Vector3(1, 2, 1), 1.1f, side => side == South).Where(p => !p.IsTop).ToArray();
        Assert.Equal(AttachPoints.South, AttachPoints.Choose(faces, West)?.Name);
    }

    [Fact]
    public void AModelsOwnPointsAreScaledAndTurnedWithIt()
    {
        var file = new GlbFile();
        // an Attach_front empty 0.1 m in front of the model's middle, its +Z out of the front
        file.AttachNodes.Add(("front", Matrix4x4.CreateTranslation(0, 0.4f, 0.1f)));
        AttachPoint local = Assert.Single(AttachPoints.OfFile(file, 2f, 2.5f));
        Assert.Equal("front", local.Name);
        Assert.Equal(new Vector3(0, 1f, 0.2f), local.Position);
        Assert.Equal(Vector3.UnitZ, local.Normal);
        // turned a quarter turn (a positive yaw takes +Z to +X) and moved to its tile
        AttachPoint placed = AttachPoints.Placed(local, new Vector3(10, 0, 20), MathF.PI / 2f);
        Assert.Equal(10.2f, placed.Position.X, 4);
        Assert.Equal(20f, placed.Position.Z, 4);
        Assert.Equal(1f, placed.Normal.X, 4);
        Assert.Equal(AttachPoints.Choose(new[] { placed }, East), placed);
    }

    [Fact]
    public void TheCatalogNamesAModelsPoints()
    {
        ModelCatalog catalog = ModelCatalog.Parse("""
            { "models": [ { "id": "x", "icons": [ "a.dmi" ], "rule": "single", "texture": "model", "parts": { "model": "x.glb" },
              "attachPoints": [ { "name": "panel", "at": [ 0, 1.2, 0.3 ], "normal": [ 0, 0, 2 ] } ] } ] }
            """);
        AttachPoint point = Assert.Single(catalog.Entries.Single().AttachPoints);
        Assert.Equal(("panel", new Vector3(0, 1.2f, 0.3f), Vector3.UnitZ), (point.Name, point.Position, point.Normal));
    }
}
