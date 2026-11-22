using System.IO;
using System.Text.Json;
using Anomaly.Tools.Icons;

namespace Anomaly.Core.Tests.Tools.Icons;

/// <summary>The metadata reader and the JSON of tools/Anomaly.Tools.Icons (M2 P7), on built files and on a few real tg files.</summary>
public class DmiReaderTests
{
    private const string Description = @"# BEGIN DMI
version = 4.0
	width = 32
	height = 32
state = ""floor""
	dirs = 1
	frames = 1
state = ""human""
	dirs = 4
	frames = 2
	delay = 1,2
	loop = 3
	rewind = 1
state = ""walk""
	dirs = 8
	frames = 1
	movement = 1
	hotspot = 12,13,1
# END DMI
";

    [Fact]
    public void StatesTakeTheirFramesInSheetOrder()
    {
        // 1 + 4 * 2 + 8 = 17 frames on a sheet 5 frames wide.
        DmiFile dmi = DmiReader.Read(PngBuilder.Make(160, 128, Description));
        Assert.Equal(32, dmi.Width);
        Assert.Equal(160, dmi.SheetWidth);
        Assert.Equal(128, dmi.SheetHeight);
        Assert.Equal(new[] { "floor", "human", "walk" }, dmi.States.Select(s => s.Name));
        Assert.Equal(new[] { 0, 1, 9 }, dmi.States.Select(s => s.First));
        DmiState human = dmi.States[1];
        Assert.Equal(4, human.Dirs);
        Assert.Equal(2, human.Frames);
        Assert.Equal(new double[] { 1, 2 }, human.Delays);
        Assert.Equal(3, human.Loop);
        Assert.True(human.Rewind);
        Assert.Null(dmi.States[0].Delays);
        Assert.True(dmi.States[2].Movement);
        Assert.Equal("12,13,1", dmi.States[2].Hotspot);
        Assert.Equal((32, 0), dmi.FramePosition(1));
        Assert.Equal((0, 32), dmi.FramePosition(5));
    }

    [Fact]
    public void TheJsonListsEveryFramesRectangleDirectionMajor()
    {
        DmiFile dmi = DmiReader.Read(PngBuilder.Make(160, 128, Description));
        using JsonDocument json = JsonDocument.Parse(DmiJson.Write(dmi));
        JsonElement human = json.RootElement.GetProperty("states")[1];
        Assert.Equal("human", human.GetProperty("name").GetString());
        JsonElement rects = human.GetProperty("rects");
        Assert.Equal(8, rects.GetArrayLength());
        // The sheet is frame-major (BYOND's order: each frame holds its four directions), the rectangles direction-major. The first frame
        // of the state is sheet index 1: x 32, y 0; direction 0's second frame is index 1 + 4 = 5: x 0, y 32; direction 2's first is index 3: x 96, y 0.
        Assert.Equal(new[] { 32, 0, 32, 32 }, rects[0].EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(new[] { 0, 32, 32, 32 }, rects[1].EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(new[] { 96, 0, 32, 32 }, rects[4].EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(new[] { 96, 32, 32, 32 }, rects[7].EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("states")[0].GetProperty("delays").ValueKind);
    }

    [Fact]
    public void ASheetThatIsTooSmallForItsStatesIsAnError()
    {
        DmiFile dmi = DmiReader.Read(PngBuilder.Make(64, 32, Description));
        Assert.Throws<InvalidDataException>(() => DmiJson.Write(dmi));
    }

    [Fact]
    public void MissingDelaysAreOnesAndAStillStateKeepsNone()
    {
        DmiFile dmi = DmiReader.ParseDescription("# BEGIN DMI\nversion = 4.0\n\twidth = 16\n\theight = 16\nstate = \"a\"\n\tdirs = 1\n\tframes = 3\nstate = \"b\"\n\tdirs = 1\n\tframes = 1\n\tdelay = 4\n# END DMI\n");
        Assert.Equal(new double[] { 1, 1, 1 }, dmi.States[0].Delays);
        Assert.Null(dmi.States[1].Delays);
        Assert.Equal(16, dmi.Width);
    }

    [Theory]
    [InlineData("not a png")]
    [InlineData("")]
    public void NotAPngIsRejected(string text)
        => Assert.Throws<InvalidDataException>(() => DmiReader.Read(System.Text.Encoding.ASCII.GetBytes(text)));

    [Fact]
    public void APngWithoutADescriptionIsNotADmi()
        => Assert.Throws<InvalidDataException>(() => DmiReader.Read(PngBuilder.Make(32, 32, null)));

    [Fact]
    public void ADirectionCountOtherThanOneFourOrEightIsAnError()
        => Assert.Throws<InvalidDataException>(() => DmiReader.ParseDescription("# BEGIN DMI\nversion = 4.0\n\twidth = 32\n\theight = 32\nstate = \"a\"\n\tdirs = 3\n\tframes = 1\n# END DMI\n"));

    [TgFact]
    public void TheHumanBodypartSheetOfTgReadsAsTheSpritersMadeIt()
    {
        DmiFile dmi = DmiReader.Read(File.ReadAllBytes(Path.Combine(TgClone.Root, "icons/mob/human/bodyparts_greyscale.dmi")));
        Assert.Equal(32, dmi.Width);
        Assert.Equal(32, dmi.Height);
        Assert.Equal(93, dmi.States.Count);
        DmiState head = dmi.States.Single(s => s.Name == "human_head_m");
        Assert.Equal(4, head.Dirs);
        Assert.Equal(1, head.Frames);
        // Frames of consecutive states follow each other, and the whole sheet is used within its capacity.
        for (int i = 1; i < dmi.States.Count; i++)
            Assert.Equal(dmi.States[i - 1].First + dmi.States[i - 1].Dirs * dmi.States[i - 1].Frames, dmi.States[i].First);
        DmiState last = dmi.States[^1];
        Assert.True(last.First + last.Dirs * last.Frames <= dmi.Capacity);
    }

    [TgFact]
    public void TheTurfSheetHasTheFloorAndPlatingStatesTheTestRoomUses()
    {
        DmiFile floors = DmiReader.Read(File.ReadAllBytes(Path.Combine(TgClone.Root, "icons/turf/floors.dmi")));
        Assert.Contains(floors.States, s => s.Name == "floor");
        Assert.Contains(floors.States, s => s.Name == "plating");
        DmiFile walls = DmiReader.Read(File.ReadAllBytes(Path.Combine(TgClone.Root, "icons/turf/walls.dmi")));
        Assert.Contains(walls.States, s => s.Name == string.Empty);
    }

    [TgFact]
    public void AnAnimatedStateOfTgKeepsItsDelays()
    {
        // Any state with several frames has one delay per frame.
        int checkedStates = 0;
        foreach (string file in new[] { "icons/obj/machines/computer.dmi", "icons/effects/fire.dmi", "icons/effects/effects.dmi" })
        {
            string path = Path.Combine(TgClone.Root, file);
            if (!File.Exists(path))
                continue;
            foreach (DmiState state in DmiReader.Read(File.ReadAllBytes(path)).States.Where(s => s.Frames > 1))
            {
                Assert.Equal(state.Frames, state.Delays.Length);
                checkedStates++;
            }
        }
        Assert.True(checkedStates > 0);
    }
}
