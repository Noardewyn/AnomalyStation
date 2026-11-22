using Anomaly.Client.Shared.Audio;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Audio;

/// <summary>
/// D296 on the client: the Settings' ambience and ship hum volumes scale tg's CHANNEL_AMBIENCE and CHANNEL_BUZZ (DV226), a
/// voice's commands carry tg's environment for its reverb bus, and BYOND's environments map to Godot reverbs.
/// </summary>
public class AmbienceMixTests
{
    private static SoundEvent Play(ushort channel, float volume = 50, byte environment = 0, bool repeat = false) => new()
    {
        Kind = SoundKind.Play,
        Channel = channel,
        Path = 1,
        File = "sound/ambience/general/shipambience.ogg",
        Volume = (byte)(volume * 2),
        Environment = environment,
        Repeat = repeat,
    };

    [Fact]
    public void TheAmbienceVolumesScaleOnlyTheirChannels()
    {
        var m = new SoundMixer();
        m.SetGain(SoundCategory.Ambience, 0.5f);
        m.SetGain(SoundCategory.Buzz, 0.25f);
        List<VoiceCommand> c = m.Apply(new[] { Play(SoundMixer.ChannelAmbience), Play(SoundMixer.ChannelBuzz, 35, repeat: true), Play(5) }, 0).ToList();
        Assert.Equal(SoundMixer.LinearToDb(0.5f * 0.5f), c[0].VolumeDb, 3);
        Assert.Equal(SoundMixer.LinearToDb(0.35f * 0.25f), c[1].VolumeDb, 3);
        Assert.Equal(SoundMixer.LinearToDb(0.5f), c[2].VolumeDb, 3);

        // a changed volume reaches the playing loop without a restart
        m.SetGain(SoundCategory.Buzz, 0f);
        VoiceCommand buzz = Assert.Single(m.Refresh(), x => x.Repeat);
        Assert.Equal(VoiceOp.Change, buzz.Op);
        Assert.Equal(SoundMixer.SilentDb, buzz.VolumeDb);
    }

    [Fact]
    public void ACommandCarriesTheSoundsEnvironment()
    {
        var m = new SoundMixer();
        VoiceCommand start = Assert.Single(m.Apply(new[] { Play(3, environment: 21) }, 0));
        Assert.Equal(21, start.Environment); // tg's SOUND_ENVIRONMENT_PARKING_LOT (20) + 1
        Assert.Equal("parking lot", ReverbPresets.Name(start.Environment));
        Assert.Equal("none", ReverbPresets.Name(0));
    }

    [Fact]
    public void EveryEnvironmentIsAReverbAndNoneIsDry()
    {
        Assert.Null(ReverbPresets.For(0));
        Assert.Null(ReverbPresets.For((byte)(ReverbPresets.Count + 1)));
        Assert.Equal(26, ReverbPresets.Count); // SOUND_ENVIRONMENT_GENERIC 0 .. SOUND_ENVIRONMENT_PSYCHOTIC 25
        for (byte e = 1; e <= ReverbPresets.Count; e++)
        {
            ReverbSettings r = ReverbPresets.For(e).Value;
            Assert.InRange(r.RoomSize, 0f, 0.98f);
            Assert.InRange(r.Damping, 0f, 1f);
            Assert.InRange(r.Wet, 0f, 0.8f);
            Assert.InRange(r.PredelayMsec, 20f, 500f);
        }
        // tg's area kinds (sound.dm:106-117): a small enclosed room (bathroom) rings more than a station corridor (parking lot),
        // a large hall (quarry) holds longer than a small room, space (underwater) is dark
        ReverbSettings bathroom = ReverbPresets.For(4).Value, parking = ReverbPresets.For(21).Value, quarry = ReverbPresets.For(19).Value,
            room = ReverbPresets.For(3).Value, underwater = ReverbPresets.For(23).Value;
        Assert.True(bathroom.Wet > parking.Wet);
        Assert.True(quarry.RoomSize > room.RoomSize);
        Assert.True(underwater.Damping > parking.Damping);
        Assert.Equal(0f, ReverbPresets.For(4, 0f).Value.Wet);
    }

    [Fact]
    public void TheOwnStepsStealAnyVoiceButTheAmbienceAndTheBuzz()
    {
        var m = new SoundMixer(positionalVoices: 1, centredVoices: 3);
        m.Apply(new[] { Play(SoundMixer.ChannelAmbience, 27), Play(SoundMixer.ChannelBuzz, 20, repeat: true), Play(0, 60) }, 0);
        // the pool is full: a step takes the louder step's voice, not the quieter ambience or buzz
        List<VoiceCommand> c = m.Apply(new[] { Play(0, 50) }, 1).ToList();
        Assert.Equal(1, m.Stolen);
        Assert.Contains(c, x => x.Op == VoiceOp.Stop && x.Channel == 0);
        Assert.True(m.TryGetChannel(SoundMixer.ChannelAmbience, out _));
        Assert.True(m.TryGetChannel(SoundMixer.ChannelBuzz, out _));
        Assert.True(SoundMixer.IsReserved(SoundMixer.ChannelAmbience) && !SoundMixer.IsReserved(1011));
    }

    [Fact]
    public void ALoopWhoseEndsFadeIsCrossfadedIntoASeamlessCopy()
    {
        const int rate = 1000;
        float[] Noise(int seconds, bool fadeEnd)
        {
            var rng = new Random(7);
            int n = seconds * rate;
            var f = new float[2 * n];
            for (int i = 0; i < n; i++)
            {
                float g = fadeEnd && i > n - rate / 4 ? (n - i) / (rate / 4f) * 0.1f : 1f; // the last quarter second fades 20 dB and more
                f[2 * i] = f[2 * i + 1] = (float)(rng.NextDouble() * 2 - 1) * 0.3f * g;
            }
            return f;
        }
        Assert.Null(LoopSeam.Seamless(Noise(10, false), rate)); // a level loop plays as it is
        float[] fading = Noise(10, true);
        float[] loop = LoopSeam.Seamless(fading, rate);
        Assert.NotNull(loop);
        int fade = (int)(rate * LoopSeam.CrossfadeSeconds);
        Assert.Equal(2 * (10 * rate - fade), loop.Length);
        // the seam: the copy's end runs on into its start, which begins with the original's tail (the crossfade's first frame)
        Assert.Equal(fading[2 * (10 * rate - fade)], loop[0], 1);
        Assert.Equal(fading[2 * (10 * rate - fade) - 2], loop[^2], 6);
        Assert.Null(LoopSeam.Seamless(Noise(1, true), rate)); // too short to crossfade
    }

    [Fact]
    public void EachKindOfSoundHasItsVolumeUnderTheMaster()
    {
        Assert.Equal(SoundCategory.Music, SoundMixer.CategoryOf(1024)); // CHANNEL_LOBBYMUSIC
        Assert.Equal(SoundCategory.Music, SoundMixer.CategoryOf(1021)); // CHANNEL_JUKEBOX
        Assert.Equal(SoundCategory.Ambience, SoundMixer.CategoryOf(1012)); // CHANNEL_WEATHER
        Assert.Equal(SoundCategory.Effects, SoundMixer.CategoryOf(1020)); // CHANNEL_HEARTBEAT
        Assert.Equal(SoundCategory.Effects, SoundMixer.CategoryOf(0));
        var m = new SoundMixer { MasterGain = 0.5f };
        m.SetGain(SoundCategory.Effects, 0.5f);
        m.SetGain(SoundCategory.Music, 0f);
        List<VoiceCommand> c = m.Apply(new[] { Play(3, 80), Play(1024, 100) }, 0).ToList();
        Assert.Equal(SoundMixer.LinearToDb(0.8f * 0.5f * 0.5f), c[0].VolumeDb, 3);
        Assert.Equal(SoundMixer.SilentDb, c[1].VolumeDb);
    }
}
