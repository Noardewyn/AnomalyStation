using System.IO;
using Anomaly.Client.Shared.Light;

namespace Anomaly.Server.Tests.M3Contracts;

/// <summary>The M3 contracts step's light grid shader interface (docs/ai/specs/light-grid.md section 7), shared by M3 P6 and P7.</summary>
public class LightGridInterfaceTests
{
    private static string GodotFile(string relative)
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "AnomalyStation.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir, "godot", relative));
    }

    [Fact]
    public void ProjectGodotDeclaresEveryGlobal()
    {
        string project = GodotFile("project.godot");
        Assert.Contains("[shader_globals]", project);
        foreach (string name in LightGridUniforms.All)
            Assert.Contains("\n" + name + "={", project);
    }

    [Fact]
    public void TheIncludeDeclaresTheGlobalsAndTheFunction()
    {
        string include = GodotFile(LightGridUniforms.IncludePath.Substring("res://".Length));
        foreach (string name in LightGridUniforms.All)
            Assert.Contains(" " + name + ";", include);
        Assert.Contains("vec3 " + LightGridUniforms.LightFunction + "(vec3 world_pos)", include);
    }
}
