using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// tg's playsound and playsound_local (game/sound/sound.dm) end to end in a booted room: the falloff of
/// CALCULATE_SOUND_VOLUME_RATIO (__DEFINES/sound.dm:32) with playsound_local's min_volume 5 (playsound forwards 14 arguments,
/// so its own min_volume 3 never reaches it), the pressure factor, who hears (the spatial grid's client channel, the
/// euclidean range, the client and deafness checks, hearers() for the sounds that do not pass walls) and the shared
/// /sound that playsound rewrites for each listener.
/// </summary>
public class PlaysoundTests
{
    private const string Airlock = "sound/machines/airlock/airlock.ogg";

    // tg's formula in double precision: vol - ((max(d - fd, 0) / (max(maxd, d) - fd)) ** (1 / exp)) * (vol - min_volume)
    private static double Falloff(double vol, double d, double maxd, double fd, double exp, double min_volume = 5)
        => vol - Math.Pow(Math.Max(d - fd, 0) / (Math.Max(maxd, d) - fd), 1 / exp) * (vol - min_volume);

    [Fact]
    public void TheFormulaGivesThePlansNumbers()
    {
        // airlock open: vol 30, range 15, exponent 2.5, falloff distance 0, min_volume 5
        Assert.Equal(30, Falloff(30, 0, 15, 0, 2.5), 3);
        Assert.Equal(21.537, Falloff(30, 1, 15, 0, 2.5), 3);
        Assert.Equal(13.890, Falloff(30, 5, 15, 0, 2.5), 3);
        Assert.Equal(8.743, Falloff(30, 10, 15, 0, 2.5), 3);
        Assert.Equal(5, Falloff(30, 15, 15, 0, 2.5), 3);
        // human floor footstep: vol 75, maxdistance 8 (15 - 1 - 6), falloff distance 1
        Assert.Equal(75, Falloff(75, 1, 8, 1, 2.5), 3);
        Assert.Equal(25.12, Falloff(75, 4, 8, 1, 2.5), 2);
        // the pressure factor at 50 kPa on both ends, and the airlock at 5 tiles under it
        double factor = (50 - 10) / (101.325 - 10);
        Assert.Equal(0.43800, factor, 5);
        Assert.Equal(6.084, Falloff(30, 5, 15, 0, 2.5) * factor, 3);
        // vacuum: 0, but 15 % when touching the source
        Assert.Equal(3.2306, Falloff(30, 1, 15, 0, 2.5) * 0.15, 4);
    }

    [Fact]
    public void AnAirlockSoundFallsOffWithDistanceAsTgComputesIt()
    {
        var room = new AudioRoom(20, 7, seed: 5101);
        int[] distances = { 0, 1, 5, 10, 15 };
        var listeners = distances.Select(d => room.Join(2 + d, 3, "Ear" + d)).ToList();
        room.Sounds.Heard.Clear();

        room.In(() => playsound(locate(2, 3, 1), Dm.resource(Airlock), 30, TRUE));

        double[] expected = { 30, 21.537, 13.890, 8.743, 5 };
        for (int i = 0; i < distances.Length; i++)
        {
            HeardSound heard = Assert.Single(room.Sounds.Of(listeners[i], Airlock));
            Assert.Equal(expected[i], heard.Volume, 3);
            Assert.InRange(heard.Frequency, 32000, 55000); // vary: one get_rand_frequency for every listener
            Assert.Equal(-distances[i], heard.X); // the source is west of the listener: dx = source.x - listener.x
            Assert.Equal(15, heard.Falloff); // falloff = max_distance
        }
        Assert.Single(room.Sounds.Heard.Select(h => h.Frequency).Distinct());
        Assert.Single(room.Sounds.Heard.Select(h => h.Channel).Distinct()); // the channel is allocated once, the same for everyone
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AFootstepKeepsItsFullVolumeInsideItsFalloffDistance()
    {
        var room = new AudioRoom(12, 7, seed: 5102);
        mob_living_carbon_human near = room.Join(3, 3, "Near");
        mob_living_carbon_human far = room.Join(6, 3, "Far");
        room.Sounds.Heard.Clear();

        // the human footstep's call shape (datums/elements/footstep.dm): extrarange -7 gives maxdistance 8, falloff distance 1
        room.In(() => playsound(locate(2, 3, 1), Dm.resource("sound/effects/footstep/floor1.ogg"), 75, TRUE, -7, falloff_distance: 1));

        Assert.Equal(75, Assert.Single(room.Sounds.Of(near, "sound/effects/footstep/floor1.ogg")).Volume, 3);
        Assert.Equal(25.12, Assert.Single(room.Sounds.Of(far, "sound/effects/footstep/floor1.ogg")).Volume, 2);
        room.AssertNoRuntimes();
    }

    private static void SetPressure(turf t, float kpa)
    {
        var air = (datum_gas_mixture)((turf_open)t).air;
        float now = Dm.num(air.return_pressure());
        air.remove_ratio(1 - kpa / now);
    }

    [Fact]
    public void LowPressureAndVacuumLowerTheVolumeByTgsFactor()
    {
        var room = new AudioRoom(20, 7, seed: 5103);
        mob_living_carbon_human touching = room.Join(3, 3, "Touching");
        mob_living_carbon_human five = room.Join(7, 3, "Five");
        room.Sounds.Heard.Clear();

        room.In(() =>
        {
            SetPressure(locate(2, 3, 1), 50);
            SetPressure(locate(7, 3, 1), 50);
            SetPressure(locate(3, 3, 1), 50);
            playsound(locate(2, 3, 1), Dm.resource(Airlock), 30, TRUE);
        });
        Assert.Equal(6.084, Assert.Single(room.Sounds.Of(five, Airlock)).Volume, 2);
        Assert.Equal(21.537 * 0.438, Assert.Single(room.Sounds.Of(touching, Airlock)).Volume, 2);

        room.Sounds.Heard.Clear();
        room.In(() =>
        {
            SetPressure(locate(2, 3, 1), 0);
            SetPressure(locate(7, 3, 1), 0);
            SetPressure(locate(3, 3, 1), 0);
            playsound(locate(2, 3, 1), Dm.resource(Airlock), 30, TRUE);
        });
        // vacuum: nothing at five tiles (space is the same: tg's space turfs hold an empty mixture), 15 % when touching
        Assert.Equal(0, Assert.Single(room.Sounds.Of(five, Airlock)).Volume, 4);
        Assert.Equal(3.2306, Assert.Single(room.Sounds.Of(touching, Airlock)).Volume, 3);

        room.Sounds.Heard.Clear();
        room.In(() => playsound(locate(2, 3, 1), Dm.resource(Airlock), 30, TRUE, pressure_affected: FALSE));
        Assert.Equal(13.890, Assert.Single(room.Sounds.Of(five, Airlock)).Volume, 3); // pressure_affected FALSE ignores it
        room.AssertNoRuntimes();
    }

    [Fact]
    public void OnlyPlayersInTheEuclideanRangeThatAreNotDeafHear()
    {
        var room = new AudioRoom(26, 26, seed: 5104);
        mob_living_carbon_human at15 = room.Join(20, 5, "At15");
        mob_living_carbon_human diagonal = room.Join(16, 16, "Diagonal"); // get_dist 11, euclidean 15.56
        mob_living_carbon_human at16 = room.Join(21, 5, "At16");
        mob_living_carbon_human deaf = room.Join(7, 5, "Deaf");
        mob_living_carbon_human at8 = room.Join(13, 5, "At8");
        mob_living_carbon_human at9 = room.Join(14, 5, "At9");
        mob_living_carbon_human dummy = room.Dummy(6, 5);
        room.In(() => ADD_TRAIT(deaf, TRAIT_DEAF, "test"));
        room.Sounds.Heard.Clear();

        object listeners = room.In(() => playsound(locate(5, 5, 1), Dm.resource(Airlock), 30, FALSE));

        Assert.Single(room.Sounds.Of(at15, Airlock));
        Assert.Empty(room.Sounds.Of(diagonal, Airlock));
        Assert.Empty(room.Sounds.Of(at16, Airlock));
        Assert.Empty(room.Sounds.Of(deaf, Airlock));
        Assert.Single(room.Sounds.Of(at8, Airlock));
        Assert.DoesNotContain(room.Sounds.Heard, h => ReferenceEquals(h.Target, dummy));
        // the grid search returns the deaf player and the diagonal one too (the filters are playsound_local's and the range's)
        var list = Assert.IsType<DmList>(listeners);
        room.In(() =>
        {
            Assert.True(Dm.@in(deaf, list));
            Assert.True(Dm.@in(diagonal, list));
            Assert.False(Dm.@in(at16, list));
            Assert.False(Dm.@in(dummy, list)); // no client: not in the client channel
        });

        room.Sounds.Heard.Clear();
        room.In(() => playsound(locate(5, 5, 1), Dm.resource(Airlock), 30, FALSE, -7)); // maxdistance 8
        Assert.Single(room.Sounds.Of(at8, Airlock));
        Assert.Empty(room.Sounds.Of(at9, Airlock));
        Assert.Empty(room.Sounds.Of(at15, Airlock));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ASoundThatDoesNotPassWallsAsksHearers()
    {
        // a wall column at x = 8 between the source (5, 6) and the far listener (11, 6)
        var room = new AudioRoom(16, 12, seed: 5105, mapExtras: _ =>
        {
            for (int y = 2; y <= 10; y++)
                Dm.@new<turf_closed_indestructible>(locate(8, y, 1));
        });
        mob_living_carbon_human near = room.Join(6, 6, "Near");
        mob_living_carbon_human behind = room.Join(11, 6, "Behind");
        room.Sounds.Heard.Clear();

        room.In(() => playsound(locate(5, 6, 1), Dm.resource(Airlock), 30, FALSE, ignore_walls: FALSE));
        Assert.Single(room.Sounds.Of(near, Airlock));
        Assert.Empty(room.Sounds.Of(behind, Airlock));

        room.Sounds.Heard.Clear();
        room.In(() => playsound(locate(5, 6, 1), Dm.resource(Airlock), 30, FALSE));
        Assert.Single(room.Sounds.Of(near, Airlock));
        Assert.Single(room.Sounds.Of(behind, Airlock)); // ignore_walls TRUE, tg's default: through the wall
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSharedSoundIsRewrittenForEachListener()
    {
        var room = new AudioRoom(16, 7, seed: 5106);
        mob_living_carbon_human a = room.Join(3, 3, "A");
        mob_living_carbon_human b = room.Join(12, 3, "B");
        RecordingSinks sinks = RecordingSinks.Install(room.World);

        room.In(() => playsound(locate(2, 3, 1), Dm.resource(Airlock), 30, FALSE));

        var lines = sinks.Lines.Where(l => l.Contains(" sound ")).ToList();
        Assert.Equal(2, lines.Count);
        string va = lines.Single(l => l.Contains("#" + a.dm_id + " ")), vb = lines.Single(l => l.Contains("#" + b.dm_id + " "));
        Assert.Contains(Airlock, va);
        Assert.NotEqual(va.Substring(va.IndexOf("volume=", StringComparison.Ordinal)), vb.Substring(vb.IndexOf("volume=", StringComparison.Ordinal)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheSoundDebugTraitWritesTgsAdminLine()
    {
        var room = new AudioRoom(12, 7, seed: 5107);
        mob_living_carbon_human a = room.Join(7, 3, "A");
        var chat = new ChatCapture();
        PresentationSinks.Of(room.World).Messages = chat;
        room.In(() => ADD_TRAIT(a, TRAIT_SOUND_DEBUGGED, "test"));

        room.In(() => playsound(locate(2, 3, 1), Dm.resource(Airlock), 30, FALSE));
        room.Step(2); // SSchat delivers its queue when it fires

        Assert.Contains(chat.Html, l => l.Contains("Max Range-15 Distance-5 Vol-13.89 Sound-" + Airlock));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void StopAndVolumeChangeSendTheirSounds()
    {
        var room = new AudioRoom(8, 7, seed: 5108);
        mob_living_carbon_human a = room.Join(3, 3, "A");
        room.Sounds.Heard.Clear();

        room.In(() =>
        {
            a.stop_sound_channel(CHANNEL_HEARTBEAT);
            a.set_sound_channel_volume(CHANNEL_AMBIENCE, 40);
        });

        Assert.Equal(2, room.Sounds.Heard.Count);
        Assert.Null(room.Sounds.Heard[0].File);
        Assert.Equal(CHANNEL_HEARTBEAT, room.Sounds.Heard[0].Channel);
        Assert.Equal(CHANNEL_AMBIENCE, room.Sounds.Heard[1].Channel);
        Assert.Equal(40, room.Sounds.Heard[1].Volume);
        Assert.Equal(SOUND_UPDATE, room.Sounds.Heard[1].Status);
        room.AssertNoRuntimes();
    }
}

/// <summary>The html of every chat message (rules 15: a recording harness).</summary>
internal sealed class ChatCapture : IMessageSink
{
    public List<string> Html { get; } = new List<string>();

    public void Chat(object target, DmList message) => Html.Add(Dm.str(message?["html"]));

    public void Output(object target, string text) { }

    public void Balloon(object viewer, object atom, string text) { }
}
