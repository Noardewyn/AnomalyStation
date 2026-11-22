using System.IO;
using System.Numerics;
using System.Text.Json;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

public sealed class StationaryPressureTankModelTests
{
    [Fact]
    public void EverySourceJunctionSelectsTheCorrectRoundedModuleAndKeepsTileScale()
    {
        string root = Path.GetDirectoryName(ModelCatalog.Locate()!)!;
        ModelCatalog catalog = ModelCatalog.Parse(File.ReadAllText(Path.Combine(root, ModelCatalog.FileName)));
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "../tg/icons/obj/pipes_n_cables/stationary_canisters.json")));
        int checkedStates = 0;
        foreach (JsonElement state in source.RootElement.GetProperty("states").EnumerateArray())
        {
            string name = state.GetProperty("name").GetString()!;
            if (!name.StartsWith("canister-", StringComparison.Ordinal)) continue;
            int junction = ModelRules.Junction(name);
            ModelEntry entry = catalog.Find("gags:/datum/greyscale_config/stationary_canister|#ffffff", name)!;
            Assert.NotNull(entry);
            Assert.True(entry.FlatColors);
            ModelPlacement placement = Assert.Single(ModelRules.Choose(entry, name, 2, 2));
            ModelPart part = entry.Parts[placement.Part];
            Assert.StartsWith("station_props/stationary_tanks/rounded/pressure_", part.File);
            int canonical = int.Parse(Path.GetFileNameWithoutExtension(part.File)["pressure_".Length..]);
            int rotated = canonical;
            for (int t = 0; t < (int)MathF.Round(placement.Yaw / (MathF.PI / 2)); t++)
                rotated = Rotate(rotated);
            Assert.Equal(junction, rotated);
            GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(root, part.File)));
            ModelMeshData data = ModelMesh.Build(file, entry, part, 2, 2.5f, .75f);
            Assert.InRange(data.Positions.Max(p => p.Y), 1.8299f, 1.8301f);
            Assert.All(data.Positions, p =>
            {
                Assert.InRange(MathF.Abs(p.X), 0, 1.00001f);
                Assert.InRange(MathF.Abs(p.Z), 0, 1.00001f);
            });
            int exposed = 4 - BitOperations.PopCount((uint)(junction & 15));
            Assert.Equal(exposed, file.MeshNodes.Count(n => n.Name.StartsWith("Gas_Window", StringComparison.Ordinal)));
            if (exposed > 0)
                Assert.Contains(file.Materials, m => m.Name.Contains("Tint_Gas", StringComparison.Ordinal));
            checkedStates++;
        }
        Assert.Equal(47, checkedStates);
    }

    private static int Rotate(int mask)
    {
        int result = 0;
        foreach ((int from, int to) in new[] { (4, 1), (1, 8), (8, 2), (2, 4), (16, 128), (128, 64), (64, 32), (32, 16) })
            if ((mask & from) != 0) result |= to;
        return result;
    }
}
