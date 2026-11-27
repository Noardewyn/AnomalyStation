using System.IO;
using System.Text;
using System.Text.Json;
using Anomaly.Tools.Icons;

namespace Anomaly.Core.Tests.Tools.Icons;

/// <summary>The conversion of tools/Anomaly.Tools.Icons (M2 P7): a built mini clone, then the real clone of tg.</summary>
public class IconConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "anomaly-icons-" + Guid.NewGuid().ToString("N"));

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

    private string MakeClone()
    {
        string tg = Path.Combine(_root, "tg");
        Write(Path.Combine(tg, "icons/turf/floors.dmi"), PngBuilder.Make(64, 32, "# BEGIN DMI\nversion = 4.0\n\twidth = 32\n\theight = 32\nstate = \"floor\"\n\tdirs = 1\n\tframes = 1\nstate = \"plating\"\n\tdirs = 1\n\tframes = 1\n# END DMI\n"));
        Write(Path.Combine(tg, "icons/mob/under.dmi"), PngBuilder.Make(32, 128, "# BEGIN DMI\nversion = 4.0\n\twidth = 32\n\theight = 32\nstate = \"jumpsuit\"\n\tdirs = 4\n\tframes = 1\n# END DMI\n"));
        Write(Path.Combine(tg, "icons/broken.dmi"), Encoding.ASCII.GetBytes("this is no png"));
        Write(Path.Combine(tg, "code/__DEFINES/files.dm"), Encoding.UTF8.GetBytes("#define SPECIAL_ICON 'icons/mob/under.dmi'\n"));
        Write(Path.Combine(tg, "code/datums/greyscale/config_types/greyscale_configs/a.dm"), Encoding.UTF8.GetBytes(
            "/datum/greyscale_config/jumpsuit\n\tname = \"Jumpsuit\"\n\ticon_file = 'icons/mob/under.dmi'\n\tjson_config = 'code/datums/greyscale/json_configs/jumpsuit.json'\n\n"
            + "/datum/greyscale_config/jumpsuit/worn\n\tname = \"Jumpsuit (Worn)\" // the worn one\n\tjson_config = 'code/datums/greyscale/json_configs/jumpsuit_worn.json'\n\n"
            + "/datum/greyscale_config/special\n\tname = \"Special\"\n\ticon_file = SPECIAL_ICON\n\tjson_config = 'code/datums/greyscale/json_configs/special.json'\n\n"
            + "/datum/greyscale_config/base\n\tname = \"A base type with no json\"\n"));
        string state = "{\"jumpsuit\": [{\"type\": \"icon_state\", \"icon_state\": \"jumpsuit\", \"blend_mode\": \"overlay\", \"color_ids\": [1]}]}";
        Write(Path.Combine(tg, "code/datums/greyscale/json_configs/jumpsuit.json"), Encoding.UTF8.GetBytes(state));
        Write(Path.Combine(tg, "code/datums/greyscale/json_configs/jumpsuit_worn.json"), Encoding.UTF8.GetBytes(state));
        Write(Path.Combine(tg, "code/datums/greyscale/json_configs/special.json"), Encoding.UTF8.GetBytes(state));
        return tg;
    }

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
    }

    [Fact]
    public void IconsAndGagsConfigsAreWrittenAndABrokenFileIsReported()
    {
        string tg = MakeClone();
        string output = Path.Combine(_root, "out");
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        int code = IconConverter.Run(new IconToolOptions(tg, output), stdout, stderr);

        Assert.Equal(1, code); // icons/broken.dmi failed
        Assert.Contains("icons/broken.dmi", stderr.ToString());
        Assert.Contains("2 icons", stdout.ToString());
        Assert.Equal(File.ReadAllBytes(Path.Combine(tg, "icons/turf/floors.dmi")), File.ReadAllBytes(Path.Combine(output, "icons/turf/floors.png")));
        using JsonDocument floors = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "icons/turf/floors.json")));
        Assert.Equal(2, floors.RootElement.GetProperty("states").GetArrayLength());
        Assert.True(File.Exists(Path.Combine(output, ".gdignore")));

        // The worn config inherits the icon file from its parent; a define resolves; a base type with no JSON is not written.
        using JsonDocument worn = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "gags/jumpsuit/worn.json")));
        Assert.Equal("/datum/greyscale_config/jumpsuit/worn", worn.RootElement.GetProperty("type").GetString());
        Assert.Equal("icons/mob/under.dmi", worn.RootElement.GetProperty("icon_file").GetString());
        Assert.Equal("Jumpsuit (Worn)", worn.RootElement.GetProperty("name").GetString());
        Assert.Equal("icons/mob/under.dmi", JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "gags/special.json"))).RootElement.GetProperty("icon_file").GetString());
        Assert.False(File.Exists(Path.Combine(output, "gags/base.json")));
        using JsonDocument index = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "gags/index.json")));
        Assert.Equal(3, index.RootElement.GetArrayLength());
    }

    [Fact]
    public void ASecondRunWritesNothing()
    {
        string tg = MakeClone();
        File.Delete(Path.Combine(tg, "icons/broken.dmi"));
        string output = Path.Combine(_root, "out");
        Assert.Equal(0, IconConverter.Run(new IconToolOptions(tg, output), new StringWriter(), new StringWriter()));
        var second = new StringWriter();
        Assert.Equal(0, IconConverter.Run(new IconToolOptions(tg, output), second, new StringWriter()));
        Assert.Contains("(0 files written", second.ToString());
    }

    [Fact]
    public void ADirectoryThatIsNoCloneIsRefused()
    {
        var stderr = new StringWriter();
        Assert.Equal(2, IconConverter.Run(new IconToolOptions(_root, Path.Combine(_root, "out")), new StringWriter(), stderr));
        Assert.Contains("--tg must be a tgstation clone", stderr.ToString());
    }

    [TgFact]
    public void EveryIconOfTgConvertsAndTheJumpsuitConfigsExist()
    {
        string output = Path.Combine(_root, "real");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int code = IconConverter.Run(new IconToolOptions(TgClone.Root, output), stdout, stderr);
        Assert.True(code == 0, stderr.ToString());
        int dmis = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons"), "*.dmi", SearchOption.AllDirectories).Count();
        // M8z slice B: besides the converted .dmi files, the strip menu's plain slot pictures (icons/ui/inventory/*.png) are copied unchanged
        int inventory = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons", "ui", "inventory"), "*.png").Count();
        Assert.True(inventory >= 17);
        Assert.Equal(inventory, Directory.EnumerateFiles(Path.Combine(output, "icons", "ui", "inventory"), "*.png").Count());
        // M9 slice E: NtOS's program pictures (icons/ui/programs/*.gif) convert to a png and a json each
        int programs = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons", "ui", "programs"), "*.gif").Count();
        Assert.True(programs > 0);
        // gaps S4c: the arcade's bosses (icons/ui/arcade/*.gif) the same way
        int arcade = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons", "ui", "arcade"), "*.gif").Count();
        Assert.Equal(6, arcade);
        // gaps P3: the DNA console's gene pictures (icons/ui/dna/*.gif) the same way
        int dna = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons", "ui", "dna"), "*.gif").Count();
        Assert.Equal(3, dna);
        programs += dna;
        programs += arcade;
        // M10 slice F: the lobby's pictures (icons/hud/lobby/png/*.png, tgui-lobby's buttons and screens) are copied unchanged too
        int lobby = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons", "hud", "lobby", "png"), "*.png").Count();
        Assert.True(lobby > 0);
        Assert.Equal(lobby, Directory.EnumerateFiles(Path.Combine(output, "icons", "hud", "lobby", "png"), "*.png").Count());
        // gaps package G1: the safe's dial (icons/ui/safe/safe_dial.png, the Safe window's picture) is copied unchanged too
        Assert.True(File.Exists(Path.Combine(output, "icons", "ui", "safe", "safe_dial.png")));
        // the title's backdrop, the one atom icon that is a plain PNG, is a sheet with its .json like a .dmi
        using (JsonDocument title = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "icons", "blanks", "blank_title.json"))))
            Assert.Equal(608, title.RootElement.GetProperty("width").GetInt32());
        Assert.True(File.Exists(Path.Combine(output, "icons", "blanks", "blank_title.png")));
        // syndicate N1b: the NTOS radar's three pictures (icons/ui/tgui/ntosradar_*.png) are copied unchanged too
        Assert.True(File.Exists(Path.Combine(output, "icons", "ui", "tgui", "ntosradar_background.png")));
        // the battle arcade's inn and campfire (icons/ui/arcade/shopkeeper.png and fireplace.png) are copied unchanged too
        Assert.True(File.Exists(Path.Combine(output, "icons", "ui", "arcade", "shopkeeper.png")));
        Assert.True(File.Exists(Path.Combine(output, "icons", "ui", "arcade", "fireplace.png")));
        // the exploration drones' adventure screens (icons/ui/adventure/*.png, tg's asset/simple/adventure) are copied unchanged too
        int adventure = Directory.EnumerateFiles(Path.Combine(TgClone.Root, "icons", "ui", "adventure"), "*.png").Count();
        Assert.Equal(4, adventure);
        Assert.Equal(adventure, Directory.EnumerateFiles(Path.Combine(output, "icons", "ui", "adventure"), "*.png").Count());
        Assert.Equal(dmis + inventory + programs + lobby + adventure + 2 + 3 + 2, Directory.EnumerateFiles(Path.Combine(output, "icons"), "*.png", SearchOption.AllDirectories).Count());
        Assert.Equal(dmis + programs + 1, Directory.EnumerateFiles(Path.Combine(output, "icons"), "*.json", SearchOption.AllDirectories).Count());
        using JsonDocument worn = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "gags/jumpsuit/worn.json")));
        Assert.Equal("icons/mob/clothing/under/color.dmi", worn.RootElement.GetProperty("icon_file").GetString());
        Assert.Equal(2, worn.RootElement.GetProperty("states").GetProperty("jumpsuit").GetArrayLength());
        // The cerulean config sets only its icon file through a #define of tg's species clothing paths.
        using JsonDocument cerulean = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "gags/modular_mod_parts_cerulean/basic.json")));
        Assert.EndsWith("cerulean_modsuit_gen.dmi", cerulean.RootElement.GetProperty("icon_file").GetString());
        // Every config's icon file was converted (the client composes from them).
        using JsonDocument index = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "gags/index.json")));
        Assert.True(index.RootElement.GetArrayLength() > 200);
        foreach (JsonElement entry in index.RootElement.EnumerateArray())
        {
            string icon = entry.GetProperty("icon_file").GetString();
            Assert.True(File.Exists(Path.Combine(output, icon.Substring(0, icon.Length - ".dmi".Length) + ".json")), entry.GetProperty("type").GetString() + " needs " + icon);
        }
    }
}
