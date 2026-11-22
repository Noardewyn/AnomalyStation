using System.IO;
using System.Text;
using System.Text.Json;
using Anomaly.Tools.Icons;
using Anomaly.Tools.Sounds;

namespace Anomaly.Core.Tests.Tools.Sounds;

/// <summary>tools/Anomaly.Tools.Sounds (M4b): the Ogg header reader, the index over a built clone, non-commercial routing, the incremental copy, the attribution formats.</summary>
public class SoundToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "anomaly-sounds-" + Guid.NewGuid().ToString("N"));

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

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteText(string path, string text) => Write(path, Encoding.UTF8.GetBytes(text));

    /// <summary>A mini clone: three Ogg files (mono 44.1 kHz 1 s, stereo 22.05 kHz 0.5 s, mono 48 kHz 2.5 s), a .mid and a .mp3 to skip, and an attribution file for sound/b.</summary>
    private string MakeClone(string attributionOfB = "three.ogg - made by somebody, license: CC0\n")
    {
        string tg = Path.Combine(_root, "tg");
        Write(Path.Combine(tg, "sound/a/one.ogg"), OggBuilder.Make(44100, 1, 44100));
        Write(Path.Combine(tg, "sound/a/two.ogg"), OggBuilder.Make(22050, 2, 11025));
        Write(Path.Combine(tg, "sound/b/three.ogg"), OggBuilder.Make(48000, 1, 120000));
        Write(Path.Combine(tg, "sound/b/song.mid"), new byte[] { 1, 2, 3 });
        if (attributionOfB != null)
            WriteText(Path.Combine(tg, "sound/b/attribution.txt"), attributionOfB);
        return tg;
    }

    private (int Code, string Out, string Err, JsonElement Index) Run(string tg, bool skipNc = false, string name = "out")
    {
        var output = new StringWriter();
        var errors = new StringWriter();
        string outDir = Path.Combine(_root, name);
        int code = SoundCopier.Run(new SoundToolOptions(tg, outDir, skipNc), output, errors);
        string indexPath = Path.Combine(outDir, "sound", "index.json");
        JsonElement index = File.Exists(indexPath) ? JsonDocument.Parse(File.ReadAllText(indexPath)).RootElement.Clone() : default;
        return (code, output.ToString(), errors.ToString(), index);
    }

    private static JsonElement Entry(JsonElement index, string path) =>
        index.GetProperty("files").EnumerateArray().Single(e => e.GetProperty("path").GetString() == path);

    [Fact]
    public void AFileWithNoLicenceIsIndexedButNotCopiedAndASourcePageNcGoesToSoundNc()
    {
        string tg = MakeClone();
        Write(Path.Combine(tg, "sound/announcer/alarm/clownops.ogg"), OggBuilder.Make(44100, 1, 44100));
        Write(Path.Combine(tg, "sound/items/can/can_shake.ogg"), OggBuilder.Make(44100, 1, 44100));
        // a credits.txt is read like an attribution file: a folder-wide statement
        Write(Path.Combine(tg, "sound/items/vacuum/vacuum_use.ogg"), OggBuilder.Make(44100, 1, 44100));
        WriteText(Path.Combine(tg, "sound/items/vacuum/credits.txt"), "Sounds in this folder are CC-BY-NC\n");
        // a stale copy from an earlier run is removed
        Write(Path.Combine(_root, "out", "sound/announcer/alarm/clownops.ogg"), new byte[] { 1 });
        var run = Run(tg);
        Assert.Equal(0, run.Code);

        JsonElement none = Entry(run.Index, "sound/announcer/alarm/clownops.ogg");
        Assert.Equal(JsonValueKind.Null, none.GetProperty("where").ValueKind);
        Assert.StartsWith("no licence", none.GetProperty("licence").GetString());
        Assert.False(File.Exists(Path.Combine(_root, "out", "sound/announcer/alarm/clownops.ogg")));

        foreach (string nc in new[] { "sound/items/can/can_shake.ogg", "sound/items/vacuum/vacuum_use.ogg" })
        {
            JsonElement e = Entry(run.Index, nc);
            Assert.True(e.GetProperty("nc").GetBoolean(), nc);
            Assert.Equal("sound-nc", e.GetProperty("where").GetString());
        }
        // D345: the licence notes go beside the sounds, so a build names the authors
        Assert.True(File.Exists(Path.Combine(_root, "out", "sound/items/vacuum/credits.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "out", "sound/b/attribution.txt")));
    }

    // ---- The Ogg header reader ----

    [Fact]
    public void TheHeaderGivesSampleRateChannelsAndLengthFromTheLastGranule()
    {
        OggInfo mono = OggInfo.Read(OggBuilder.Make(44100, 1, 110250));
        Assert.Equal(44100, mono.SampleRate);
        Assert.Equal(1, mono.Channels);
        Assert.Equal(110250, mono.Granule);
        Assert.Equal(25.0, mono.Deciseconds);

        OggInfo stereo = OggInfo.Read(OggBuilder.Make(22050, 2, 11025));
        Assert.Equal((22050, 2), (stereo.SampleRate, stereo.Channels));
        Assert.Equal(5.0, stereo.Deciseconds);

        // A fraction is kept, as rust-g's sound_len reports it: 1 frame at 44.1 kHz is 0.000227 ds.
        Assert.Equal(Math.Round(10.0 * 12345 / 44100, 4), OggInfo.Read(OggBuilder.Make(44100, 1, 12345)).Deciseconds);
    }

    [Fact]
    public void ALastPageWithoutAGranuleIsSkippedAndTrailingGarbageIsIgnored()
    {
        byte[] noGranule = OggBuilder.Make(44100, 1, 44100, lastGranule: -1);
        Assert.Equal(0, OggInfo.Read(noGranule).Granule);   // only the first page (granule 0) has one

        var ms = new MemoryStream();
        ms.Write(OggBuilder.Make(44100, 1, 4410));
        ms.Write(new byte[] { 0x4F, 0x67, 0x67, 0x53, 0, 2, 1, 2, 3 });   // a cut-off "OggS" page header at the end
        Assert.Equal(4410, OggInfo.Read(ms.ToArray()).Granule);
    }

    [Fact]
    public void NonOggAndNonVorbisInputIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => OggInfo.Read(Encoding.ASCII.GetBytes("this is not an ogg file at all")));
        var opus = new MemoryStream();
        OggBuilder.Page(opus, 2, 0, Encoding.ASCII.GetBytes("OpusHead\u0001\u0001\0\0\u0080»\0\0\0\0\0"));
        Assert.Throws<InvalidDataException>(() => OggInfo.Read(opus.ToArray()));
        Assert.Throws<InvalidDataException>(() => OggInfo.Read(Array.Empty<byte>()));
    }

    [Fact]
    public void AWaveFileUnderAnOggNameIsRead()
    {
        OggInfo wave = OggInfo.Read(OggBuilder.Wave(44100, 2, 44100));
        Assert.Equal(("wav", 44100, 2, 44100L), (wave.Format, wave.SampleRate, wave.Channels, wave.Granule));
        Assert.Equal(10.0, wave.Deciseconds);
    }

    /// <summary>An empty ID3v2 tag, MPEG-1 Layer III frames (128 kbps, 44.1 kHz, stereo: 417 bytes each) and an ID3v1 trailer.</summary>
    private static byte[] Mp3(int frames)
    {
        var ms = new MemoryStream();
        ms.Write("ID3\x03\0\0\0\0\0\0"u8);
        for (int i = 0; i < frames; i++)
        {
            var frame = new byte[417];
            frame[0] = 0xFF; frame[1] = 0xFB; frame[2] = 0x90; frame[3] = 0x00;
            ms.Write(frame);
        }
        ms.Write("TAG"u8);
        ms.Write(new byte[125]);
        return ms.ToArray();
    }

    [Fact]
    public void AnMp3IsReadFrameByFramePastItsTagsAndCopiedAsIs()
    {
        OggInfo mp3 = OggInfo.Read(Mp3(3));
        Assert.Equal(("mp3", 44100, 2, 3456L), (mp3.Format, mp3.SampleRate, mp3.Channels, mp3.Granule));
        Assert.Equal(Math.Round(34560.0 / 44100, 4), mp3.Deciseconds);

        // tg's one mp3 (items/weapons/earthcracker_bang.mp3) is copied under its own path with the format the client loads it by
        string tg = MakeClone();
        Write(Path.Combine(tg, "sound/b/bang.mp3"), Mp3(3));
        var run = Run(tg);
        Assert.Equal(0, run.Code);
        JsonElement entry = Entry(run.Index, "sound/b/bang.mp3");
        Assert.Equal("mp3", entry.GetProperty("format").GetString());
        Assert.Equal(Mp3(3), File.ReadAllBytes(Path.Combine(_root, "out", entry.GetProperty("file").GetString())));
    }

    // ---- The index and the copy ----

    [Fact]
    public void TheIndexListsEveryOggWithLengthRateChannelsAndLicence()
    {
        string tg = MakeClone();
        var run = Run(tg);
        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Err);
        string outDir = Path.Combine(_root, "out");

        var files = run.Index.GetProperty("files").EnumerateArray().Select(e => e.GetProperty("path").GetString()).ToArray();
        Assert.Equal(new[] { "sound/a/one.ogg", "sound/a/two.ogg", "sound/b/three.ogg" }, files);

        JsonElement one = Entry(run.Index, "sound/a/one.ogg");
        Assert.Equal(10.0, one.GetProperty("ds").GetDouble());
        Assert.Equal(44100, one.GetProperty("rate").GetInt32());
        Assert.Equal(1, one.GetProperty("channels").GetInt32());
        Assert.Equal("CC BY-SA 3.0 (tg README)", one.GetProperty("licence").GetString());
        Assert.Equal("sound", one.GetProperty("where").GetString());
        Assert.Equal("sound/a/one.ogg", one.GetProperty("file").GetString());
        Assert.False(one.GetProperty("nc").GetBoolean());

        JsonElement two = Entry(run.Index, "sound/a/two.ogg");
        Assert.Equal((5.0, 22050, 2), (two.GetProperty("ds").GetDouble(), two.GetProperty("rate").GetInt32(), two.GetProperty("channels").GetInt32()));

        JsonElement three = Entry(run.Index, "sound/b/three.ogg");
        Assert.Equal(25.0, three.GetProperty("ds").GetDouble());
        Assert.Equal("CC0 1.0", three.GetProperty("licence").GetString());
        Assert.Equal("sound/b/attribution.txt", three.GetProperty("attribution").GetString());

        // The files are copied byte for byte to the same paths below sound/; .mid is neither copied nor indexed, but counted.
        Assert.Equal(File.ReadAllBytes(Path.Combine(tg, "sound/a/two.ogg")), File.ReadAllBytes(Path.Combine(outDir, "sound/a/two.ogg")));
        Assert.False(File.Exists(Path.Combine(outDir, "sound/b/song.mid")));
        Assert.Contains(".mid 1", run.Out);
        Assert.Contains("3 sound files", run.Out);
        Assert.True(File.Exists(Path.Combine(outDir, ".gdignore")));
        Assert.Contains(SoundCopier.SourceMarker, File.ReadAllText(Path.Combine(outDir, "SOURCE.txt")));
    }

    [Fact]
    public void ANonCommercialFileGoesToSoundNcWithANoticeAndTheIndexPointsThere()
    {
        string tg = MakeClone("three.ogg - https://freesound.org/people/x/sounds/1/ , License: CC BY-NC 4.0\n");
        var run = Run(tg);
        string outDir = Path.Combine(_root, "out");
        JsonElement three = Entry(run.Index, "sound/b/three.ogg");
        Assert.True(three.GetProperty("nc").GetBoolean());
        Assert.Equal("CC BY-NC 4.0", three.GetProperty("licence").GetString());
        Assert.Equal("sound-nc", three.GetProperty("where").GetString());
        Assert.Equal("sound-nc/b/three.ogg", three.GetProperty("file").GetString());
        Assert.True(File.Exists(Path.Combine(outDir, "sound-nc/b/three.ogg")));
        Assert.False(File.Exists(Path.Combine(outDir, "sound/b/three.ogg")));
        Assert.True(File.Exists(Path.Combine(outDir, "sound/a/one.ogg")));
        string notice = File.ReadAllText(Path.Combine(outDir, "sound-nc/NOTICE.txt"));
        Assert.Contains("NON-COMMERCIAL", notice);
        Assert.Contains("sound/b/three.ogg: CC BY-NC 4.0", notice);
        Assert.Contains("non-commercial files: 1", run.Out);
    }

    [Fact]
    public void SkipNcLeavesTheFileOutButKeepsItsLengthInTheIndex()
    {
        string tg = MakeClone("three.ogg - https://freesound.org/people/x/sounds/1/ , License: CC BY-NC 4.0\n");
        Run(tg);                                    // a first run without --skip-nc leaves sound-nc/ behind ...
        var run = Run(tg, skipNc: true);            // ... which the second removes
        string outDir = Path.Combine(_root, "out");
        JsonElement three = Entry(run.Index, "sound/b/three.ogg");
        Assert.True(three.GetProperty("nc").GetBoolean());
        Assert.Equal(JsonValueKind.Null, three.GetProperty("where").ValueKind);
        Assert.Equal(JsonValueKind.Null, three.GetProperty("file").ValueKind);
        Assert.Equal(25.0, three.GetProperty("ds").GetDouble());
        Assert.False(Directory.Exists(Path.Combine(outDir, "sound-nc")));
        Assert.False(File.Exists(Path.Combine(outDir, "sound/b/three.ogg")));
        Assert.Contains("absent (non-commercial, --skip-nc) 1", run.Out);
    }

    [Fact]
    public void ACopyWhoseSizeAndTimeMatchIsLeftAlone()
    {
        string tg = MakeClone();
        var first = Run(tg);
        Assert.Contains("copied 3, already up to date 0", first.Out);
        string copy = Path.Combine(_root, "out", "sound/a/one.ogg");
        string source = Path.Combine(tg, "sound/a/one.ogg");
        Assert.Equal(File.GetLastWriteTimeUtc(source), File.GetLastWriteTimeUtc(copy));

        var second = Run(tg);
        Assert.Contains("copied 0, already up to date 3", second.Out);

        // Same size and time, other bytes: the tool trusts the match and does not rewrite it ...
        byte[] sentinel = new byte[new FileInfo(source).Length];
        File.WriteAllBytes(copy, sentinel);
        File.SetLastWriteTimeUtc(copy, File.GetLastWriteTimeUtc(source));
        Assert.Contains("copied 0, already up to date 3", Run(tg).Out);
        Assert.Equal(sentinel, File.ReadAllBytes(copy));

        // ... a different time (or size) copies it again.
        File.SetLastWriteTimeUtc(copy, File.GetLastWriteTimeUtc(source).AddMinutes(-5));
        Assert.Contains("copied 1, already up to date 2", Run(tg).Out);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(copy));
    }

    [Fact]
    public void ABrokenFileIsReportedAndTheRestIsStillIndexed()
    {
        string tg = MakeClone();
        Write(Path.Combine(tg, "sound/a/broken.ogg"), Encoding.ASCII.GetBytes("not an ogg"));
        var run = Run(tg);
        Assert.Equal(1, run.Code);
        Assert.Contains("sound/a/broken.ogg", run.Err);
        Assert.Equal(3, run.Index.GetProperty("files").GetArrayLength());
    }

    [Fact]
    public void ARerunWritesTheSameIndexAndTheMissingCloneIsRefused()
    {
        string tg = MakeClone();
        Run(tg);
        string a = File.ReadAllText(Path.Combine(_root, "out", "sound/index.json"));
        Run(tg);
        Assert.Equal(a, File.ReadAllText(Path.Combine(_root, "out", "sound/index.json")));

        var err = new StringWriter();
        Assert.Equal(2, SoundCopier.Run(new SoundToolOptions(Path.Combine(_root, "nowhere"), Path.Combine(_root, "o")), new StringWriter(), err));
        Assert.Contains("--tg must be a tgstation clone", err.ToString());
    }

    [Fact]
    public void TheSourceNoteSurvivesTheIconToolAndTheOtherWayRound()
    {
        string tg = MakeClone();
        Directory.CreateDirectory(Path.Combine(tg, "icons"));
        string outDir = Path.Combine(_root, "out");
        string sourceFile = Path.Combine(outDir, "SOURCE.txt");

        Run(tg);                                                         // sounds first: its paragraph alone
        string soundsOnly = File.ReadAllText(sourceFile);
        Assert.StartsWith(SoundCopier.SourceMarker, soundsOnly);

        Assert.Equal(0, IconConverter.Run(new IconToolOptions(tg, outDir), new StringWriter(), new StringWriter()));
        string both = File.ReadAllText(sourceFile);
        Assert.StartsWith("Generated by tools/Anomaly.Tools.Icons", both);
        Assert.EndsWith(soundsOnly, both);                               // the icon tool kept the sound paragraph

        Run(tg);                                                         // and the sound tool changes nothing again
        Assert.Equal(both, File.ReadAllText(sourceFile));
        Assert.Equal(0, IconConverter.Run(new IconToolOptions(tg, outDir), new StringWriter(), new StringWriter()));
        Assert.Equal(both, File.ReadAllText(sourceFile));
    }

    // ---- The command line ----

    [Fact]
    public void TheCommandLineTakesTgOutAndSkipNc()
    {
        Assert.True(SoundToolOptions.TryParse(new[] { "--tg", "../tgstation", "--out", "godot/assets/tg" }, out SoundToolOptions a, out _));
        Assert.Equal(new SoundToolOptions("../tgstation", "godot/assets/tg", false), a);
        Assert.True(SoundToolOptions.TryParse(new[] { "--skip-nc", "--out=o", "--tg=t" }, out SoundToolOptions b, out _));
        Assert.Equal(new SoundToolOptions("t", "o", true), b);
        Assert.False(SoundToolOptions.TryParse(new[] { "--tg", "t" }, out _, out string error));
        Assert.NotNull(error);
        Assert.False(SoundToolOptions.TryParse(new[] { "--tg", "t", "--out", "o", "--nc" }, out _, out error));
        Assert.NotNull(error);
        Assert.False(SoundToolOptions.TryParse(new[] { "--help" }, out _, out error));
        Assert.Null(error);
    }

    // ---- The attribution formats ----

    [Theory]
    [InlineData("male_sniff.ogg - https://freesound.org/x , License: CC BY-NC 3.0", "CC BY-NC 3.0", true)]
    [InlineData("roaring_fire.ogg -- License: Attribution NonCommercial 4.0", "CC BY-NC 4.0", true)]
    [InlineData("TribalDrums.wav, which is licensed under Noncommercial 4.0 License", "CC BY-NC 4.0", true)]
    [InlineData("license: \"CC-BY-NC-SA-3.0\"", "CC BY-NC-SA 3.0", true)]
    [InlineData("a.ogg: https://x , license: CC0 1.0 DEED", "CC0 1.0", false)]
    [InlineData("made by sadboysuss, License: CC-by-SA", "CC BY-SA", false)]
    [InlineData("hereby licensed under CC BY-SA 3.0", "CC BY-SA 3.0", false)]
    [InlineData("which is licensed under CC Attribution 3.0", "CC BY 3.0", false)]
    [InlineData("License: Attribution 4.0 International", "CC BY 4.0", false)]
    [InlineData("zip.ogg adapted from Zip.wav (CC 4)", "CC BY 4.0", false)]
    [InlineData("tada.ogg is public domain (CC 0)", "CC0 1.0", false)]
    [InlineData("License: Sampling+", "CC Sampling+ 1.0", false)]
    public void TheLicencePhrasesOfTgsAttributionFilesAreRecognised(string line, string label, bool nc)
    {
        List<LicenceMention> found = Licences.Find(line);
        Assert.Contains(new LicenceMention(label, nc), found);
        Assert.Equal(nc, found.Any(f => f.NonCommercial));
    }

    private Attributions Read(string attributionText, params string[] oggs)
    {
        string dir = Path.Combine(_root, "attr", Guid.NewGuid().ToString("N"));
        foreach (string ogg in oggs)
            Write(Path.Combine(dir, ogg), new byte[] { 0 });
        WriteText(Path.Combine(dir, "attribution.txt"), attributionText);
        return Attributions.Load(dir, oggs);
    }

    [Fact]
    public void BraceBlocksListsRangesAndStemsAreAllRead()
    {
        Attributions a = Read(
            "{\nbrap1.ogg\nbrap2.ogg\n} - Whoopee Cushion by T -- License: Attribution NonCommercial 4.0\n\n"
            + "{\none.ogg - u , License: CC0\ntwo.ogg - u , License: CC BY-NC 4.0\n} - edited by x\n\n"
            + "cig[1,...,3].ogg - grungussusss, license: CC-BY-SA\n\n"
            + "step 1-3 made by sadboysuss\nlicense: CC-BY-SA 3.0\n\n"
            + "visor_up and visor_down are:\nBike helmet visor.wav by D -- License: Attribution NonCommercial 4.0\n",
            "brap1.ogg", "brap2.ogg", "one.ogg", "two.ogg", "cig1.ogg", "cig2.ogg", "cig3.ogg", "cig4.ogg", "step1.ogg", "step2.ogg", "step3.ogg",
            "visor_up1.ogg", "visor_up2.ogg", "visor_down1.ogg", "plain.ogg");
        Assert.True(a.Of("brap1.ogg").NonCommercial && a.Of("brap2.ogg").NonCommercial);
        Assert.Equal("CC0 1.0", a.Of("one.ogg").Label);
        Assert.True(a.Of("two.ogg").NonCommercial);
        Assert.Equal("CC BY-SA", a.Of("cig2.ogg").Label);
        Assert.Equal(Licences.Default, a.Of("cig4.ogg").Label);      // the bracket list stops at 3
        Assert.Equal("CC BY-SA 3.0", a.Of("step3.ogg").Label);
        Assert.All(new[] { "visor_up1.ogg", "visor_up2.ogg", "visor_down1.ogg" }, f => Assert.True(a.Of(f).NonCommercial, f));
        Assert.Equal(Licences.Default, a.Of("plain.ogg").Label);
        Assert.False(a.Of("plain.ogg").Unknown);
    }

    [Fact]
    public void ANamedFileWithoutALicenceIsUnknownAndASoundThatIsGoneTakesItsLicenceLinesWithIt()
    {
        Attributions a = Read(
            "bloop.ogg by my man Tim\n(https://freesound.org/people/t/sounds/1/)\n\n"
            + "kept.ogg is adapted from a clip\n\n"
            + "gone.ogg made from: a thing -- License: Attribution NonCommercial 4.0\n",
            "bloop.ogg", "kept.ogg", "other.ogg");
        Assert.True(a.Of("bloop.ogg").Unknown);
        Assert.StartsWith("unspecified", a.Of("bloop.ogg").Label);
        Assert.True(a.Of("kept.ogg").Unknown);
        // The non-commercial line is about gone.ogg, which is not in the tree: it must not leak onto kept.ogg or the folder, but it is reported.
        Assert.False(a.Of("kept.ogg").NonCommercial);
        Assert.False(a.Of("other.ogg").NonCommercial);
        Assert.Contains(a.Warnings, w => w.Contains("non-commercial") && w.Contains("gone.ogg"));
    }

    [Fact]
    public void ALineNamingSeveralSoundsMakesTheOnesDerivedFromANonCommercialSoundNonCommercialToo()
    {
        Attributions a = Read(
            "inactive.ogg - Fn P90 by P -- License: Attribution NonCommercial 4.0\n"
            + "active.ogg is inactive.ogg spliced with Taser by J -- License: Attribution 4.0\n",
            "inactive.ogg", "active.ogg");
        Assert.True(a.Of("inactive.ogg").NonCommercial);
        FileLicence active = a.Of("active.ogg");
        Assert.True(active.NonCommercial);
        Assert.True(active.Derived);
        Assert.Contains("CC BY 4.0", active.Label);
    }

    [Fact]
    public void ALicenceLineWithoutNamesIsAFolderStatement()
    {
        Attributions a = Read("cutlery sounds made by grungussuss, license: CC-BY-SA\n", "cutlery_drop1.ogg", "cutlery_drop2.ogg");
        Assert.Equal("CC BY-SA", a.Of("cutlery_drop2.ogg").Label);
    }
}
