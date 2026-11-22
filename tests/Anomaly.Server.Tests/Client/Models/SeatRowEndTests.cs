using System.IO;
using System.Linq;
using System.Numerics;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// tg's sofa.dm ends: <c>sofaend_left</c>, <c>corp_sofaend_left</c> and <c>bench_left</c> draw their armrest on the sitter's left, the east end of a south-facing
/// seat; the models look south (+Z) unturned, so a left end's armrest is at +X. tg's pews are drawn the other way round (<c>pewend_left</c>'s arm is west).
/// </summary>
public sealed class SeatRowEndTests
{
    [Theory]
    [InlineData("station_props/furniture_wave/bench_left.glb", 1)]
    [InlineData("station_props/furniture_wave/bench_right.glb", -1)]
    [InlineData("station_props/shuttle_furniture/sofa_left.glb", 1)]
    [InlineData("station_props/shuttle_furniture/sofa_right.glb", -1)]
    [InlineData("station_props/shuttle_furniture/corporate_sofa_left.glb", 1)]
    [InlineData("station_props/shuttle_furniture/corporate_sofa_right.glb", -1)]
    [InlineData("station_props/shuttle_furniture/wooden_pew_left.glb", -1)]
    [InlineData("station_props/shuttle_furniture/wooden_pew_right.glb", 1)]
    public void The_armrest_is_on_the_end_tg_draws_it(string file, int armSide)
    {
        GlbFile glb = GlbFile.Read(File.ReadAllBytes(Path.Combine(ModelsRoot(), file)), _ => null);
        Vector3[] points = glb.MeshNodes.SelectMany(n => n.Primitives.SelectMany(p => p.Positions.Select(v => Vector3.Transform(v, n.World)))).ToArray();
        float lo = points.Min(p => p.X), hi = points.Max(p => p.X), w = hi - lo;
        // the front half's tallest point at each end: an armrest rises above the seat, the open end does not
        float west = points.Where(p => p.Z > 0.1f && p.X < lo + 0.2f * w).Max(p => p.Y);
        float east = points.Where(p => p.Z > 0.1f && p.X > hi - 0.2f * w).Max(p => p.Y);
        Assert.Equal(armSide, Math.Sign(east - west));
    }

    private static string ModelsRoot()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string path = Path.Combine(dir.FullName, "godot", "assets", "models");
            if (File.Exists(Path.Combine(path, ModelCatalog.FileName)))
                return path;
        }
        throw new InvalidOperationException("godot/assets/models not found above the test's folder");
    }
}
