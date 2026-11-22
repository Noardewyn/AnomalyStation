using System.IO;
using Anomaly.Client.Shared.Speech;

namespace Anomaly.Server.Tests.Client.Speech;

/// <summary>D308: tg's TTS server's ffmpeg filters, silicon reverbs and radio clicks as the client applies them.</summary>
public class VoiceEffectsTests
{
    private const int Rate = 44100;

    private static float[] Tone(float hz, float seconds = 0.5f, float amplitude = 0.5f) =>
        Enumerable.Range(0, (int)(Rate * seconds)).Select(i => amplitude * MathF.Sin(2 * MathF.PI * hz * i / Rate)).ToArray();

    private static float Rms(float[] x) => MathF.Sqrt(x.Sum(v => v * v) / x.Length);

    [Fact]
    public void TheChainSplitsIntoThePitchTheTempoAndTheFilters()
    {
        VoicePlan plan = VoicePlan.Parse("acrusher=samples=9:level_out=7,aecho=delays=100:decays=0.4,rubberband=pitch=0.9,atempo=0.5");
        Assert.Equal(0.9f, plan.Pitch, 4);
        Assert.Equal(0.5f, plan.Tempo, 4);
        Assert.Equal(new[] { "acrusher", "aecho" }, plan.Stages.Select(s => s.Name));
        Assert.Equal("9", plan.Stages[0].Options["samples"]);
        Assert.Equal(1f / 0.7f, VoicePlan.Parse("atempo=1/0.7").Tempo, 4);
        // a filter graph (the security hailer) plays plain
        Assert.Empty(VoicePlan.Parse("[0:a] asetrate=%SAMPLE_RATE%*0.7,aresample=16000 [out]; [out]atempo=1.2 [final]").Stages);
        Assert.Empty(VoicePlan.Parse(null).Stages);
    }

    [Fact]
    public void TheMachinesHighpassCutsTheLowsAndTheGasMasksLowpassTheHighs()
    {
        VoicePlan machine = VoicePlan.Parse("highpass=f=1000");
        Assert.True(Rms(VoiceEffects.Apply(Tone(100), Rate, machine)) < 0.05f);
        Assert.True(Rms(VoiceEffects.Apply(Tone(5000), Rate, machine)) > 0.3f);
        VoicePlan mask = VoicePlan.Parse("lowpass=f=750,volume=2");
        Assert.True(Rms(VoiceEffects.Apply(Tone(6000), Rate, mask)) < 0.05f);
        Assert.True(Rms(VoiceEffects.Apply(Tone(200, amplitude: 0.2f), Rate, mask)) > 0.25f); // louder by its volume
        Assert.Equal(2f, VoicePlan.Number("6.0206dB", 1f), 3);
    }

    [Fact]
    public void EveryTgFilterKeepsTheLineInRange()
    {
        foreach (string chain in new[]
        {
            "alimiter=0.9,acompressor=threshold=0.2:ratio=20:attack=10:release=50:makeup=2,highpass=f=1000",
            "acrusher=samples=9:level_out=7,aecho=delays=100:decays=0.4,aemphasis=type=emi,crystalizer=i=6,acontrast=60,rubberband=pitch=0.9",
            "highpass=f=300,lowpass=f=3500,aecho=0.8:0.9:70|140:0.3|0.15,alimiter=0.9,acompressor=threshold=0.2:ratio=20:attack=10:release=50:makeup=2,highpass=f=1000",
        })
        {
            float[] y = VoiceEffects.Apply(Tone(440), Rate, VoicePlan.Parse(chain));
            Assert.All(y, v => Assert.InRange(v, -1f, 1f));
            Assert.True(Rms(y) > 0.01f, chain);
        }
        // the tram's echo adds its longest delay (140 ms)
        Assert.Equal(Tone(440).Length + (int)(0.14f * Rate), VoiceEffects.Apply(Tone(440), Rate, VoicePlan.Parse("aecho=0.8:0.9:70|140:0.3|0.15")).Length);
    }

    [Fact]
    public void ConvolutionWithAnImpulseShiftsAndScales()
    {
        float[] x = Tone(300, 0.1f);
        float[] impulse = new float[11];
        impulse[10] = 0.5f;
        float[] y = VoiceEffects.Convolve(x, impulse);
        Assert.Equal(x.Length, y.Length);
        for (int i = 10; i < x.Length; i += 97)
            Assert.Equal(0.5f * x[i - 10], y[i], 3);
    }

    [Fact]
    public void TheSiliconVoiceRingsOnAndIsLimited()
    {
        float[] synth = new float[200];
        synth[0] = 1f;
        synth[150] = 0.5f;
        float[] room = Enumerable.Range(0, 2000).Select(i => MathF.Exp(-i / 300f) * (i % 2 == 0 ? 1f : -1f)).ToArray();
        float[] y = VoiceEffects.Silicon(Tone(440, 0.3f), Rate, synth, room);
        Assert.Equal((int)(0.3f * Rate) + 2 * Rate, y.Length); // apad=pad_dur=2
        Assert.All(y, v => Assert.InRange(v, -0.51f, 0.51f)); // alimiter limit 0.5
        Assert.True(Rms(y) > 0.01f);
    }

    [Fact]
    public void TheRadioKeepsTheMiddleBandAndCutsTheLowsAndHighs()
    {
        // D309: a walkie-talkie's band; the hiss alone stays quiet
        float low = Rms(VoiceEffects.Radio(Tone(120), Rate, new Random(1)));
        float middle = Rms(VoiceEffects.Radio(Tone(1200), Rate, new Random(1)));
        float high = Rms(VoiceEffects.Radio(Tone(9000), Rate, new Random(1)));
        float hiss = Rms(VoiceEffects.Radio(new float[Rate / 2], Rate, new Random(1)));
        Assert.True(middle > 3 * low, $"middle {middle}, low {low}");
        Assert.True(middle > 3 * high, $"middle {middle}, high {high}");
        Assert.True(hiss < 0.05f && hiss < middle / 5, $"hiss {hiss}");
        Assert.All(VoiceEffects.Radio(Tone(1200, amplitude: 1f), Rate, new Random(1)), v => Assert.InRange(v, -1f, 1f));
    }

    // A voice-like line: the harmonics of 150 Hz.
    private static float[] Voiced() =>
        Enumerable.Range(0, Rate / 2).Select(i => Enumerable.Range(1, 12).Sum(h => 0.08f / h * MathF.Sin(2 * MathF.PI * 150 * h * i / Rate))).ToArray();

    // How much the line repeats itself one 150 Hz period later (1: a voiced line, near 0: noise).
    private static float Periodicity(float[] x)
    {
        int lag = Rate / 150;
        double ab = 0, aa = 0, bb = 0;
        for (int i = Rate / 10; i + lag < x.Length - Rate / 10; i++)
        {
            ab += x[i] * x[i + lag];
            aa += x[i] * x[i];
            bb += x[i + lag] * x[i + lag];
        }
        return (float)(ab / Math.Sqrt(aa * bb));
    }

    [Fact]
    public void AWhisperLosesThePitchAndKeepsTheLoudness()
    {
        // D312: breath through the same mouth: no periodic voice left, as loud, as long
        float[] voiced = Voiced();
        float[] whisper = VoiceEffects.Whisper(voiced, Rate, new Random(1));
        Assert.Equal(voiced.Length, whisper.Length);
        Assert.True(Periodicity(voiced) > 0.95f);
        Assert.True(MathF.Abs(Periodicity(whisper)) < 0.3f, $"periodicity {Periodicity(whisper)}");
        Assert.Equal(Rms(voiced), Rms(whisper), 2);
        Assert.All(whisper, v => Assert.InRange(v, -1f, 1f));
    }

    [Fact]
    public void AShoutIsStrainedAndHigher()
    {
        // D312: the presence raised over the lows, said higher and faster
        VoicePlan yell = VoicePlan.Parse(VoiceEffects.YellChain);
        float presence = Rms(VoiceEffects.Apply(Tone(2200, amplitude: 0.2f), Rate, yell));
        float low = Rms(VoiceEffects.Apply(Tone(90, amplitude: 0.2f), Rate, yell));
        Assert.True(presence > 2 * low, $"presence {presence}, low {low}");
        Assert.True(VoiceEffects.YellPitch > 1f && VoiceEffects.YellTempo > 1f);
    }

    [Fact]
    public void TheRadioClicksFrameTheLine()
    {
        float[] line = Tone(440, 0.1f);
        Assert.Equal(10 + line.Length + 20, VoiceEffects.WithClicks(line, new float[10], new float[20]).Length);
        Assert.Equal(line.Length, VoiceEffects.WithClicks(line, null, null).Length);
    }

    [Fact]
    public void TgsWavFilesAreReadAtTheLinesRate()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "tgstation", "tools", "tts", "tts-api");
        string on1 = Path.Combine(folder, "on1.wav");
        if (!File.Exists(on1))
            return; // the tg clone sits next to the repository on a developer's machine
        float[] click = VoiceEffects.ReadWav(File.ReadAllBytes(on1), Rate);
        Assert.InRange(click.Length, 1642 * 2 - 2, 1642 * 2 + 2); // 22050 Hz mono, read at 44100
        float[] room = VoiceEffects.ReadWav(File.ReadAllBytes(Path.Combine(folder, "RoomImpulse.wav")), Rate);
        Assert.Equal(62976, room.Length); // 44100 Hz stereo, mixed to mono
        Assert.Null(VoiceEffects.ReadWav(new byte[10], Rate));
    }

    [Fact]
    public void ResamplingReadsFasterAndHigher()
    {
        float[] x = Tone(200, 1f);
        Assert.Equal(x.Length / 2, VoiceEffects.Resample(x, 2f).Length);
        Assert.Same(x, VoiceEffects.Resample(x, 1f));
    }
}
