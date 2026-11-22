using System.IO;
using System.Numerics;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// DV397 (the owner, 2026-11-11): a ladder's hatch is a 1.2 x 0.6 m opening at the ladder's side of the tile, not the whole tile: the tile kit's hatch floor and
/// ceiling cover the tile but the opening, each strip with its share of the floor's picture, and the ladder models' frame lines the same opening; the frame is
/// picked apart from the rails, so the rails' box does not hold the eye of someone standing on the tile.
/// </summary>
public sealed class LadderHatchTests
{
    private const float Wall = 2f;

    [Theory]
    [InlineData(TileKitParts.HatchFloor, 0f)]
    [InlineData(TileKitParts.HatchCeiling, Wall)]
    public void TheHatchCoversTheTileButTheOpeningWithTheFloorsPicture(int part, float height)
    {
        List<GeoQuad> strips = WorldGeometry.TileKit(Wall).Where(q => q.Part == part).ToList();
        Assert.Equal(4, strips.Count);
        float area = 0f;
        foreach (GeoQuad q in strips)
        {
            Assert.All(q.Corners, c => Assert.Equal(height, c.Y));
            area += (q.Corners.Max(c => c.X) - q.Corners.Min(c => c.X)) * (q.Corners.Max(c => c.Z) - q.Corners.Min(c => c.Z));
            // the floor's uv: u east, v south, one picture over the tile
            for (int i = 0; i < 4; i++)
                Assert.Equal(new Vector2((q.Corners[i].X + 1f) / 2f, (q.Corners[i].Z + 1f) / 2f), q.Uvs[i]);
        }
        float opening = (WorldGeometry.HatchEast - WorldGeometry.HatchWest) * (WorldGeometry.HatchNorth - WorldGeometry.HatchSouth);
        Assert.Equal(4f - opening, area, 4);
        // the opening's middle (north is -z) is open, the tile's middle is floor
        Vector2 hole = new((WorldGeometry.HatchWest + WorldGeometry.HatchEast) / 2f, -(WorldGeometry.HatchSouth + WorldGeometry.HatchNorth) / 2f);
        Assert.DoesNotContain(strips, q => Covers(q, hole));
        Assert.Contains(strips, q => Covers(q, Vector2.Zero));
    }

    private static bool Covers(GeoQuad q, Vector2 p)
        => p.X > q.Corners.Min(c => c.X) && p.X < q.Corners.Max(c => c.X) && p.Y > q.Corners.Min(c => c.Z) && p.Y < q.Corners.Max(c => c.Z);

    [Theory]
    [InlineData("ladder01")]
    [InlineData("ladder11")]
    public void TheLadderModelsFrameTheHatchAndPickItApartFromTheRails(string state)
    {
        string root = Path.GetDirectoryName(ModelCatalog.Locate()!)!;
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(root, ModelCatalog.FileName)));
        ModelEntry entry = catalog.Find("icons/obj/structures.dmi", state)!;
        Assert.NotNull(entry);
        ModelPart part = entry.Parts.Values.Single();
        ModelMeshData data = ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(root, part.File))), entry, part, 2, Wall, .75f);
        Assert.NotNull(data.PickBoxes);
        Assert.Equal(2, data.PickBoxes.Count);
        (Vector3 rails, Vector3 railsMax) = data.PickBoxes[0];
        (Vector3 frame, Vector3 frameMax) = data.PickBoxes[1];
        // the frame lies on the floor around the opening (its inner edges are the hatch's)
        Assert.True(frameMax.Y < 0.05f && frame.X < WorldGeometry.HatchWest && frameMax.X > WorldGeometry.HatchEast);
        Assert.True(-frameMax.Z < WorldGeometry.HatchSouth && -frame.Z >= WorldGeometry.HatchNorth);
        // the rails stand along the north side, well north of the opening's south edge: an eye over the tile's middle or at the climbing spot is outside
        Assert.True(-railsMax.Z > 0.75f && railsMax.Y > 0.5f, $"rails from {rails} to {railsMax}");
    }
}
