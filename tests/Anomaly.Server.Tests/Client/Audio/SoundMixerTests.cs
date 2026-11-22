using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Audio;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Audio;

/// <summary>M4b audio slice 3: the client's engine-free sound bookkeeping (SoundMixer), the index, the cache, the settings and the fake server's door sound.</summary>
public class SoundMixerTests
{
    private const string Door = "sound/machines/airlock/airlock.ogg";
    private const string Bolt = "sound/machines/airlock/boltsdown.ogg";

    private static SoundEvent Play(ushort channel, string file = Door, float volume = 50, bool positional = false, byte occlusion = 0, byte muffle = 0,
        bool repeat = false, bool mute = false, float frequency = 0, float offset = 0) => new()
    {
        Kind = SoundKind.Play,
        Channel = channel,
        Path = 1,
        File = file,
        Volume = (byte)(volume * 2),
        Occlusion = occlusion,
        Muffle = muffle,
        Repeat = repeat,
        Mute = mute,
        Positional = positional,
        X = positional ? (byte)5 : (byte)0,
        Y = positional ? (byte)6 : (byte)0,
        Z = positional ? 1 : 0,
        Frequency = frequency,
        Offset = offset,
    };

    private static SoundEvent Update(ushort channel, float volume = 50, bool mute = false, bool positional = false, byte x = 0, byte y = 0, byte occlusion = 0) => new()
    {
        Kind = SoundKind.Update,
        Channel = channel,
        Volume = (byte)(volume * 2),
        Mute = mute,
        Positional = positional,
        X = x,
        Y = y,
        Z = positional ? 1 : 0,
        Occlusion = occlusion,
    };

    private static SoundEvent Stop(ushort channel) => new() { Kind = SoundKind.Stop, Channel = channel };

    private static List<VoiceCommand> Apply(SoundMixer m, double now, params SoundEvent[] events) => m.Apply(events, now).ToList();

    private static SoundFileFacts? Facts(string file) => file switch
    {
        Door => new SoundFileFacts(44100, 1.2829f),
        Bolt => new SoundFileFacts(22050, 2f),
        _ => null,
    };

    [Fact]
    public void APlayOnABusyChannelReplacesItsSound()
    {
        var m = new SoundMixer(facts: Facts);
        VoiceCommand first = Apply(m, 0, Play(7)).Single();
        Assert.Equal(VoiceOp.Start, first.Op);
        Assert.True(m.TryGetChannel(7, out VoiceRef v));

        List<VoiceCommand> second = Apply(m, 0.1, Play(7, Bolt));
        VoiceCommand start = Assert.Single(second);
        Assert.Equal(VoiceOp.Start, start.Op);
        Assert.Equal(v, start.Voice); // the same voice restarts with the new file
        Assert.Equal(Bolt, start.File);
        Assert.NotEqual(first.Generation, start.Generation);
        Assert.Equal(1, m.ActiveVoices);
        Assert.Equal(1, m.Replaced);

        // A positional play on a channel that played centred: the centred voice stops, a positional one starts.
        List<VoiceCommand> third = Apply(m, 0.2, Play(7, positional: true));
        Assert.Equal(new[] { VoiceOp.Stop, VoiceOp.Start }, third.Select(c => c.Op));
        Assert.Equal(VoicePool.Positional, third[1].Voice.Pool);
        Assert.Equal(1, m.ActiveVoices);
    }

    [Fact]
    public void AStopFreesItsChannelAndAStopOnChannelZeroStopsEverything()
    {
        var m = new SoundMixer(facts: Facts);
        Apply(m, 0, Play(1), Play(2, positional: true), Play(0), Play(0, positional: true));
        Assert.Equal(4, m.ActiveVoices);

        VoiceCommand stop = Assert.Single(Apply(m, 0.1, Stop(1)));
        Assert.Equal(VoiceOp.Stop, stop.Op);
        Assert.False(m.TryGetChannel(1, out _));
        Assert.Equal(3, m.ActiveVoices);
        Assert.Empty(Apply(m, 0.1, Stop(1))); // nothing playing there any more

        List<VoiceCommand> all = Apply(m, 0.2, Stop(0));
        Assert.Equal(3, all.Count);
        Assert.All(all, c => Assert.Equal(VoiceOp.Stop, c.Op));
        Assert.Equal(0, m.ActiveVoices);
        Assert.False(m.TryGetChannel(2, out _));
        Assert.Equal("stopped all (3)", m.Log.First().Outcome);
    }

    [Fact]
    public void APlayOnChannelZeroTakesAnyFreeVoiceThatLaterPlaysDoNotReplace()
    {
        var m = new SoundMixer(facts: Facts);
        VoiceCommand a = Apply(m, 0, Play(0)).Single();
        VoiceCommand b = Apply(m, 0.1, Play(0)).Single();
        Assert.NotEqual(a.Voice, b.Voice); // the second takes another free voice
        Assert.Equal(2, m.ActiveVoices);
        Assert.False(m.TryGetChannel(0, out _)); // not in the channel table

        // A play on a numbered channel takes a third voice; neither anonymous voice is touched.
        VoiceCommand c = Apply(m, 0.2, Play(3)).Single();
        Assert.NotEqual(a.Voice, c.Voice);
        Assert.NotEqual(b.Voice, c.Voice);
        Assert.True(m.Voice(a.Voice).Active);
        Assert.True(m.Voice(b.Voice).Active);
        Assert.Equal(0, m.Replaced);
    }

    [Fact]
    public void AnUpdateChangesThePlayingSoundWithoutRestartingIt()
    {
        var m = new SoundMixer(facts: Facts);
        VoiceCommand start = Apply(m, 0, Play(9, positional: true, repeat: true)).Single();
        VoiceCommand change = Apply(m, 0.5, Update(9, volume: 25, positional: true, x: 8, y: 9, occlusion: 128)).Single();
        Assert.Equal(VoiceOp.Change, change.Op);
        Assert.Equal(start.Voice, change.Voice);
        Assert.Equal(start.Generation, change.Generation);
        Assert.Equal((8, 9), (change.X, change.Y));
        Assert.Equal(SoundMixer.VolumeDb(25, 128 / 255f, SoundMixer.DefaultOcclusionFactor), change.VolumeDb, 4);
        VoiceState v = m.Voice(start.Voice);
        Assert.Equal(25f, v.Volume);
        Assert.Equal(0.0, v.StartedAt);
        Assert.Equal(1, m.Started);

        // An update on a channel with nothing is dropped.
        Assert.Empty(Apply(m, 0.6, Update(10)));
        Assert.Equal(1, m.UpdatesWithoutVoice);

        // A positional voice told it has no tile stays and plays centred; a centred one given a tile moves where it is.
        VoiceCommand centred = Apply(m, 0.7, Update(9, positional: false)).Single();
        Assert.Equal(VoiceOp.Change, centred.Op);
        Assert.False(centred.Positional);
        VoiceCommand c2 = Apply(m, 0.8, Play(11)).Single();
        Assert.Equal(VoicePool.Centred, c2.Voice.Pool);
        VoiceCommand move = Apply(m, 0.9, Update(11, positional: true, x: 3, y: 4)).Single();
        Assert.Equal(VoiceOp.Move, move.Op);
        Assert.Equal(c2.Voice, move.From);
        Assert.Equal(VoicePool.Positional, move.Voice.Pool);
        Assert.True(m.TryGetChannel(11, out VoiceRef moved));
        Assert.Equal(move.Voice, moved);
        Assert.False(m.Voice(c2.Voice).Active);
    }

    [Fact]
    public void AMuteKeepsTheVoicePlayingSilently()
    {
        var m = new SoundMixer(facts: Facts);
        VoiceCommand start = Apply(m, 0, Play(4, repeat: true)).Single();
        VoiceCommand muted = Apply(m, 1, Update(4, volume: 40, mute: true)).Single();
        Assert.Equal(VoiceOp.Change, muted.Op);
        Assert.Equal(SoundMixer.SilentDb, muted.VolumeDb);
        Assert.True(m.Voice(start.Voice).Active);
        Assert.True(m.Voice(start.Voice).Mute);

        VoiceCommand back = Apply(m, 2, Update(4, volume: 40)).Single();
        Assert.Equal(SoundMixer.LinearToDb(0.4f), back.VolumeDb, 4);
        Assert.Equal(start.Generation, back.Generation);

        // A play that starts muted (a token out of range) takes a voice too.
        VoiceCommand silent = Apply(m, 3, Play(5, mute: true)).Single();
        Assert.Equal(VoiceOp.Start, silent.Op);
        Assert.Equal(SoundMixer.SilentDb, silent.VolumeDb);
    }

    [Fact]
    public void AFullPoolStealsTheQuietestVoiceTheOldestOfEquals()
    {
        var m = new SoundMixer(positionalVoices: 3, centredVoices: 2, facts: Facts);
        // tg's 80 behind a wall is 80 x (1 - 0.65) = 28, quieter than a clear 30.
        VoiceCommand walled = Apply(m, 0, Play(1, volume: 80, positional: true, occlusion: 255)).Single();
        VoiceCommand oldThirty = Apply(m, 0.1, Play(2, volume: 30, positional: true)).Single();
        VoiceCommand newThirty = Apply(m, 0.2, Play(3, volume: 30, positional: true)).Single();

        List<VoiceCommand> fourth = Apply(m, 0.3, Play(4, volume: 50, positional: true));
        Assert.Equal(new[] { VoiceOp.Stop, VoiceOp.Start }, fourth.Select(c => c.Op));
        Assert.Equal(walled.Voice, fourth[0].Voice);
        Assert.Equal(walled.Voice, fourth[1].Voice);
        Assert.False(m.TryGetChannel(1, out _));
        Assert.Equal(1, m.Stolen);
        Assert.StartsWith("stole ", m.Log.First().Outcome);

        // Two equal 30s: the older goes.
        List<VoiceCommand> fifth = Apply(m, 0.4, Play(5, volume: 60, positional: true));
        Assert.Equal(oldThirty.Voice, fifth[0].Voice);
        Assert.True(m.TryGetChannel(3, out VoiceRef three));
        Assert.Equal(newThirty.Voice, three);

        // A muted voice is the quietest of all, whatever its volume.
        Apply(m, 0.5, Update(4, volume: 50, mute: true, positional: true, x: 5, y: 6));
        List<VoiceCommand> sixth = Apply(m, 0.6, Play(6, volume: 40, positional: true));
        Assert.Equal(fourth[1].Voice, sixth[0].Voice);
        Assert.False(m.TryGetChannel(4, out _));
        Assert.Equal(3, m.Stolen);

        // The centred pool is its own.
        Assert.Equal(VoiceOp.Start, Apply(m, 0.7, Play(7)).Single().Op);
        Assert.Equal(VoiceOp.Start, Apply(m, 0.8, Play(8)).Single().Op);
        Assert.Equal(3, m.Stolen);
        Assert.Equal(new[] { VoiceOp.Stop, VoiceOp.Start }, Apply(m, 0.9, Play(9)).Select(c => c.Op));
    }

    [Fact]
    public void DecibelsAreTgsVolumeLoweredByTheOcclusion()
    {
        Assert.Equal(0f, SoundMixer.VolumeDb(100, 0, 0.65f), 4);
        Assert.Equal(-6.0206f, SoundMixer.VolumeDb(50, 0, 0.65f), 3);
        // 50 behind a wall (occlusion 1): 0.5 x (1 - 0.65) = 0.175 -> -15.14 dB.
        Assert.Equal(20f * MathF.Log10(0.175f), SoundMixer.VolumeDb(50, 1, 0.65f), 3);
        // Around a corner (128/255): 0.5 x (1 - 0.65 x 0.502).
        Assert.Equal(20f * MathF.Log10(0.5f * (1 - 0.65f * 128 / 255f)), SoundMixer.VolumeDb(50, 128 / 255f, 0.65f), 3);
        Assert.Equal(-6.0206f, SoundMixer.VolumeDb(50, 1, 0f), 3); // k 0: no occlusion
        Assert.Equal(SoundMixer.SilentDb, SoundMixer.VolumeDb(0, 0, 0.65f));
        Assert.Equal(SoundMixer.SilentDb, SoundMixer.VolumeDb(100, 1, 1f));
        Assert.Equal(SoundMixer.SilentDb, SoundMixer.VolumeDb(100, 0, 0.65f, mute: true));

        // Through the mixer, with the wire's bytes (28 half units is tg's 14, occlusion 255) and k tuned.
        var m = new SoundMixer(facts: Facts) { OcclusionFactor = 0.5f };
        VoiceCommand c = Apply(m, 0, Play(1, volume: 14, positional: true, occlusion: 255)).Single();
        Assert.Equal(20f * MathF.Log10(0.14f * 0.5f), c.VolumeDb, 3);
        Assert.True(c.Muffled); // occlusion 1 >= 0.5
        Assert.False(Apply(m, 0, Play(2, occlusion: 100, muffle: 120)).Single().Muffled); // max 0.47 < 0.5
        Assert.True(Apply(m, 0, Play(3, muffle: 128)).Single().Muffled); // 0.502
        m.MuffleThreshold = 0.9f;
        m.OcclusionFactor = 0f;
        List<VoiceCommand> refreshed = m.Refresh().ToList();
        Assert.Equal(3, refreshed.Count);
        Assert.True(refreshed.Single(r => r.Generation == c.Generation).Muffled); // occlusion 1 >= 0.9
        Assert.Equal(1, refreshed.Count(r => r.Muffled)); // 0.502 < 0.9
        Assert.Equal(20f * MathF.Log10(0.14f), refreshed.Single(r => r.Generation == c.Generation).VolumeDb, 3);
    }

    [Fact]
    public void PitchComesFromHzOverTheFilesRateOrFromAMultiplier()
    {
        Assert.Equal(1f, SoundMixer.Pitch(0, 44100));
        Assert.Equal(1f, SoundMixer.Pitch(44100, 44100), 5);
        Assert.Equal(32000f / 44100f, SoundMixer.Pitch(32000, 44100), 5);
        Assert.Equal(55000f / 44100f, SoundMixer.Pitch(55000, 44100), 5);
        Assert.Equal(1.5f, SoundMixer.Pitch(1.5f, 44100), 5);
        Assert.Equal(SoundMixer.MaxPitch, SoundMixer.Pitch(100, 44100), 5); // 100 is still a multiplier (kept at the most)
        Assert.Equal(SoundMixer.MinPitch, SoundMixer.Pitch(101, 44100), 5); // over 100 is Hz: 101 Hz is kept at the least
        Assert.Equal(2f, SoundMixer.Pitch(8000, 4000), 5);
        Assert.Equal(0.5f, SoundMixer.Pitch(-0.5f, 44100), 5); // BYOND's backwards plays forwards
        Assert.Equal(48000f / SoundMixer.DefaultSampleRate, SoundMixer.Pitch(48000, 0), 5);
        Assert.Equal(1f, SoundMixer.Pitch(float.NaN, 44100));

        // Through the mixer: each file's rate from the index; an unknown file is 44.1 kHz.
        var m = new SoundMixer(facts: Facts);
        Assert.Equal(1f, Apply(m, 0, Play(1, Bolt, frequency: 22050)).Single().Pitch, 5);
        Assert.Equal(40000f / 44100f, Apply(m, 0, Play(2, Door, frequency: 40000)).Single().Pitch, 5);
        Assert.Equal(1f, Apply(m, 0, Play(3, "sound/unknown.ogg", frequency: 44100)).Single().Pitch, 5);
        Assert.Equal(2f, Apply(m, 0, Play(4, Door, frequency: 2)).Single().Pitch, 5);
    }

    [Fact]
    public void AnOffsetIsReadAsDecisecondsAndAOneShotEndsAfterItsLength()
    {
        Assert.Equal(2.5f, SoundMixer.OffsetSeconds(25), 5);
        Assert.Equal(0f, SoundMixer.OffsetSeconds(0));
        Assert.Equal(0f, SoundMixer.OffsetSeconds(-3));

        var m = new SoundMixer(facts: Facts);
        // Bolt: 2 s. A loop 25 ds in starts at 0.5 s (2.5 mod 2).
        VoiceCommand loop = Apply(m, 0, Play(1, Bolt, repeat: true, offset: 25)).Single();
        Assert.Equal(0.5f, loop.FromSeconds, 4);
        Assert.True(double.IsPositiveInfinity(m.Voice(loop.Voice).EndsAt));
        // A one-shot 5 ds in: 1.5 s left, so it ends at 1.5 s plus the grace.
        VoiceCommand once = Apply(m, 10, Play(2, Bolt, offset: 5)).Single();
        Assert.Equal(0.5f, once.FromSeconds, 4);
        Assert.Equal(10 + 1.5 + SoundMixer.EndGrace, m.Voice(once.Voice).EndsAt, 4);
        // A one-shot past its end plays nothing (and its channel's old sound stops).
        Assert.Equal(VoiceOp.Stop, Apply(m, 11, Play(2, Bolt, offset: 30)).Single().Op);
        Assert.False(m.TryGetChannel(2, out _));
        Assert.Equal("past its end", m.Log.First().Outcome);

        // The expiry: at half speed a 2 s file lasts 4 s.
        VoiceCommand slow = Apply(m, 20, Play(3, Bolt, frequency: 0.5f)).Single();
        Assert.Empty(m.Expire(23.9));
        VoiceCommand expired = Assert.Single(m.Expire(24 + SoundMixer.EndGrace));
        Assert.Equal(slow.Voice, expired.Voice);
        Assert.False(m.TryGetChannel(3, out _));
        Assert.Equal(1, m.Expired);
        Assert.True(m.Voice(loop.Voice).Active); // the loop plays on
    }

    [Fact]
    public void AFinishedOrMissingVoiceIsFreedOnlyForItsOwnGeneration()
    {
        var m = new SoundMixer(facts: Facts);
        VoiceCommand a = Apply(m, 0, Play(1)).Single();
        VoiceCommand b = Apply(m, 0.1, Play(1, Bolt)).Single(); // replaces on the same voice
        Assert.False(m.Finished(a.Voice, a.Generation)); // the old sound's end does not stop the new one
        Assert.True(m.Voice(b.Voice).Active);
        Assert.True(m.Finished(b.Voice, b.Generation));
        Assert.False(m.TryGetChannel(1, out _));

        VoiceCommand c = Apply(m, 1, Play(2, "sound/nothing.ogg")).Single();
        m.Missing(c.Voice, c.Generation);
        Assert.Equal(1, m.MissingPlays);
        Assert.Equal(0, m.ActiveVoices);
        Assert.Equal("missing file", m.Log.First().Outcome);
    }

    [Fact]
    public void TheLogKeepsTheLastSixteenEventsNewestFirstWithTheirDistance()
    {
        var m = new SoundMixer(facts: Facts);
        for (int i = 1; i <= 20; i++)
            m.Apply(new[] { Play((ushort)i, positional: true) }, i, new ListenerTile(5, 2, 1));
        List<SoundLogEntry> log = m.Log.ToList();
        Assert.Equal(SoundMixer.LogSize, log.Count);
        Assert.Equal(20, log[0].Channel);
        Assert.Equal(5, log[^1].Channel);
        Assert.Equal(4f, log[0].Distance, 4); // (5,6) from (5,2)
        Assert.Equal(Door, log[0].File);
        Assert.Equal(50f, log[0].Volume);
    }

    [Fact]
    public void TheIndexGivesEachFilesPlaceRateAndLength()
    {
        const string json = """
            {"version":1,"files":[
            {"path":"sound/machines/airlock/airlock.ogg","ds":12.829,"rate":44100,"channels":1,"format":"ogg","bytes":13574,"where":"sound","file":"sound/machines/airlock/airlock.ogg","nc":false},
            {"path":"sound/effects/brap/brap1.ogg","ds":1.66,"rate":48000,"channels":2,"format":"ogg","bytes":13075,"where":"sound-nc","file":"sound-nc/effects/brap/brap1.ogg","nc":true},
            {"path":"sound/vehicles/mecha/justice_shield_broken.ogg","ds":8.5,"rate":44100,"channels":2,"format":"wav","bytes":226336,"file":"sound/vehicles/mecha/justice_shield_broken.ogg"},
            {"ds":3}
            ]}
            """;
        SoundIndex index = SoundIndex.Parse(json);
        Assert.Equal(3, index.Count);
        Assert.True(index.TryGet("sound/effects/brap/brap1.ogg", out SoundIndexEntry nc));
        Assert.Equal("sound-nc/effects/brap/brap1.ogg", nc.File);
        Assert.True(nc.NonCommercial);
        SoundFileFacts facts = index.Facts("sound/effects/brap/brap1.ogg").Value;
        Assert.Equal(48000, facts.SampleRate);
        Assert.Equal(0.166f, facts.LengthSeconds, 4);
        Assert.True(index.TryGet("sound/vehicles/mecha/justice_shield_broken.ogg", out SoundIndexEntry wav));
        Assert.Equal("wav", wav.Format);
        Assert.Null(index.Facts("sound/none.ogg"));
        Assert.Throws<FormatException>(() => SoundIndex.Parse("{\"version\":1}"));
        Assert.Null(SoundIndex.Load(Path.Combine(Path.GetTempPath(), "anomaly-no-such-index-" + Guid.NewGuid() + ".json"), out string problem));
        Assert.StartsWith("there is no ", problem);
    }

    [Fact]
    public void TheCacheDropsTheLeastRecentlyUsedPastItsBudget()
    {
        var cache = new SoundCache<string>(100);
        cache.Add("a", "A", 40);
        cache.Add("b", "B", 40);
        Assert.True(cache.TryGet("a", out _)); // a is now the newest
        cache.Add("c", "C", 40); // 120 > 100: b goes
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("c", out _));
        Assert.Equal(80, cache.Bytes);
        cache.Add("big", "BIG", 500); // over the budget alone: kept, the rest goes
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("big", out _));
        Assert.Equal(3, cache.Evicted);
    }

    [Fact]
    public void TheAudioSettingsAreDevLinesOfTheOptionsFile()
    {
        var defaults = new DevMenuOptions();
        Assert.Equal(100, defaults.AudioVolumePercent);
        Assert.False(defaults.AudioMute);
        Assert.Equal(SoundMixer.DefaultOcclusionFactor, defaults.AudioOcclusionFactor);
        Assert.Equal(SoundMixer.DefaultMuffleThreshold, defaults.AudioMuffleThreshold);

        var options = new PlayOptions();
        options.Dev.AudioVolumePercent = 35;
        options.Dev.AudioMute = true;
        options.Dev.AudioOcclusionFactor = 0.4f;
        options.Dev.AudioLowPassHz = 1500f;
        options.Dev.AudioMuffleThreshold = 0.25f;
        string text = options.ToText();
        Assert.Contains("dev.audio_volume=35\n", text);
        Assert.Contains("dev.audio_occlusion_factor=0.4\n", text);
        PlayOptions back = PlayOptions.Parse(text);
        Assert.Equal(35, back.Dev.AudioVolumePercent);
        Assert.True(back.Dev.AudioMute);
        Assert.Equal(0.4f, back.Dev.AudioOcclusionFactor);
        Assert.Equal(1500f, back.Dev.AudioLowPassHz);
        Assert.Equal(0.25f, back.Dev.AudioMuffleThreshold);
        Assert.False(new DevMenuOptions().TrySet("audio_occlusion_factor", "1.5", out string problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void TheFakeServerPlaysADoorNearThePlayerEveryTwoSeconds()
    {
        var feed = new FakeFeed("Tester", doorSounds: true);
        var received = new List<SoundEvent>();
        feed.Poll(0.0);
        feed.Replica.SoundsReceived += events => received.AddRange(events);
        double now = 0;
        while (now < 4.5)
        {
            now += 1.0 / 60;
            feed.Poll(now);
        }
        Assert.InRange(received.Count, 2, 3);
        SoundEvent e = received[0];
        Assert.Equal(SoundKind.Play, e.Kind);
        Assert.Equal(FakeServer.DoorSoundPath, e.File);
        Assert.True(e.Positional);
        Assert.InRange(e.Frequency, 32000, 55000);
        Assert.NotEqual(0, e.Channel);

        // Through the mixer: each one takes a voice.
        var m = new SoundMixer(facts: Facts);
        m.Apply(received, now);
        Assert.Equal(received.Count, m.Started);

        // The older fakes send no sounds.
        var quiet = new FakeFeed("Tester");
        int heard = 0;
        quiet.Poll(0.0);
        quiet.Replica.SoundsReceived += events => heard += events.Count;
        for (double t = 0; t < 2.5; t += 1.0 / 60)
            quiet.Poll(t);
        Assert.Equal(0, heard);
    }
}
