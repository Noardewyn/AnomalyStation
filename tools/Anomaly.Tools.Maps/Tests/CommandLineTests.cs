namespace Anomaly.Tools.Maps.Tests;

/// <summary>The command line (map-format.md 3): the output layout, --check, the exit codes.</summary>
public class CommandLineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "anomaly-maps-cli-" + Guid.NewGuid().ToString("N"));

    public CommandLineTests()
    {
        string maps = Path.Combine(_root, "tg", "_maps");
        Directory.CreateDirectory(Path.Combine(maps, "map_jsons"));
        Directory.CreateDirectory(Path.Combine(maps, "map_files", "Small"));
        File.WriteAllText(Path.Combine(maps, "map_jsons", "small.json"), "{\n\t\"version\": 1,\n\t\"map_name\": \"Small\",\n\t\"map_path\": \"map_files/Small\",\n\t\"map_file\": \"Small.dmm\"\n}\n");
        File.WriteAllText(Path.Combine(maps, "map_files", "Small", "Small.dmm"), ConverterTests.Fixture("p2_small.dmm"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private int Run(params string[] args) => Program.Run(args, TextWriter.Null, TextWriter.Null);

    [Fact]
    public void Writes_the_mirror_of_maps_and_checks_it()
    {
        string tg = Path.Combine(_root, "tg");
        string output = Path.Combine(_root, "out");
        Assert.Equal(0, Run("--tg", tg, "--out", output, "--map", "small"));
        string converted = Path.Combine(output, "map_files", "Small", "Small.as3dmap");
        Assert.True(File.Exists(converted));
        Assert.Equal(File.ReadAllText(Path.Combine(tg, "_maps", "map_jsons", "small.json")), File.ReadAllText(Path.Combine(output, "map_jsons", "small.json")));
        Assert.StartsWith($"tg unknown\ntool {MapConverter.ToolVersion}\nformat 1\n", File.ReadAllText(Path.Combine(output, ".as3dmaps-stamp")));
        Assert.Equal(0, Run("--tg", tg, "--out", output, "--map", "small", "--check"));
        File.AppendAllText(converted, "# edited\n");
        Assert.Equal(1, Run("--tg", tg, "--out", output, "--map", "small", "--check"));
        Assert.Equal(0, Run("--tg", tg, "--out", output, "--map", "small"));
        Assert.Equal(0, Run("--tg", tg, "--out", output, "--map", "small", "--check"));
    }

    [Fact]
    public void Exit_codes_for_usage_and_validation()
    {
        string tg = Path.Combine(_root, "tg");
        Assert.Equal(3, Run("--no-such-option"));
        Assert.Equal(3, Run("--tg", tg, "--out", Path.Combine(_root, "o"), "--map", "missing"));
        Assert.Equal(3, Run("--tg", tg, "--types", Path.Combine(_root, "no-index.tsv")));
        string dmm = Path.Combine(tg, "_maps", "map_files", "Small", "Small.dmm");
        File.WriteAllText(dmm, File.ReadAllText(dmm).Replace("/area/station/hallway)\n\"c\"", "/obj/item/crowbar)\n\"c\""));
        Assert.Equal(2, Run("--tg", tg, "--out", Path.Combine(_root, "o"), "--map", "small"));
        Assert.False(File.Exists(Path.Combine(_root, "o", "map_files", "Small", "Small.as3dmap")));
    }
}
