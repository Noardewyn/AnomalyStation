using System.IO;
using System.Numerics;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

public sealed class ComputerIntegrationTests
{
    private static readonly string Root = Path.GetDirectoryName(ModelCatalog.Locate())!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Fact]
    public void FinishedComputerAndConstructionChassisHaveIdenticalWorldBounds()
    {
        var bounds = new List<(Vector3, Vector3)>();
        foreach (string id in new[] { "computer", "modular_console", "computer_frame" })
        {
            ModelEntry e = Catalog.Entries.Single(e => e.Id == id);
            ModelPart p = e.Parts.Values.Single(p => p.Node is "Body" or "Console_Housing");
            var mesh = ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, p.File))), e, p, 2, 2.5f, .75f);
            bounds.Add((mesh.Positions.Aggregate(Vector3.Min), mesh.Positions.Aggregate(Vector3.Max)));
            Assert.False(p.Live);
        }
        foreach (var b in bounds.Skip(1))
        {
            Assert.InRange(Vector3.Distance(bounds[0].Item1, b.Item1), 0, .0001f);
            Assert.InRange(Vector3.Distance(bounds[0].Item2, b.Item2), 0, .0001f);
        }
        Assert.InRange(bounds[0].Item2.Y, 1.4449f, 1.4451f); // the owner's 1.7 m fit (2026-10-29): 1.7 m of the 2 m source
    }

    [Theory]
    [InlineData("computer")]
    [InlineData("computer_ratvar")]
    [InlineData("modular_console")]
    [InlineData("medical_laptop")]
    public void LiveSurfacesResolveOriginalFramesAndFollowAnyProgramOrPowerState(string id)
    {
        ModelEntry e = Catalog.Entries.Single(e => e.Id == id);
        string stem = Path.Combine(Root, "../tg", e.Icons[0][..^4]);
        var sheet = IconSheet.Parse(File.ReadAllText(stem + ".json"), null);
        var liveParts = e.Parts.Values.Where(p => p.Picture == "appearance").ToArray();
        Assert.Equal(2, liveParts.Length);
        foreach (ModelPart p in liveParts)
        {
            GlbFile f = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, p.File)));
            foreach (int index in ModelMesh.MaterialsOf(f, p))
            {
                var material = f.Materials[index];
                Assert.Equal(ModelFrame.ImageHash(File.ReadAllBytes(stem + ".png")), ModelFrame.ImageHash(f.Images[material.Image]));
                var size = ModelMesh.SheetSize(f, material);
                var frame = ModelFrame.Of(material, ModelMesh.UvsOf(f, p, index), sheet, size.Item1, size.Item2);
                Assert.NotNull(frame);
                var live = p.LiveFrame(frame);
                Assert.True(ModelFrame.Follows(live.State, "console-off"));
                Assert.True(ModelFrame.Follows(live.State, "different_program"));
                Assert.Equal(frame.Local(new Vector2(.3f, .4f), size.Item1, size.Item2), live.Local(new Vector2(.3f, .4f), size.Item1, size.Item2));
            }
        }
    }

    [Fact]
    public void EachMachineConstructionStageHasExactlyOneElectronicsPanelAndFiniteGeometry()
    {
        ModelEntry e = Catalog.Entries.Single(e => e.Id == "machine_frame");
        foreach (string state in e.States)
        {
            var parts = e.Parts.Values.Where(p => p.ShownIn(state)).ToArray();
            Assert.Single(parts, p => p.Node.StartsWith("Electronics_"));
            Assert.Equal(state == "box_2", parts.Any(p => p.Node == "Upper_Circuit_Board"));
            foreach (ModelPart p in parts)
            {
                var mesh = ModelMesh.Build(GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, p.File))), e, p, 2, 2.5f, .75f);
                Assert.All(mesh.Positions, v => Assert.True(float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z)));
                Assert.True(Vector3.Distance(mesh.Positions.Aggregate(Vector3.Min), mesh.Positions.Aggregate(Vector3.Max)) > .01f);
            }
        }
    }
}
