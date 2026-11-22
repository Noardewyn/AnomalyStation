using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.M1Client.P1;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.M4bAudio;

/// <summary>
/// A booted toy room of any size with players (M4b audio slice 1): the real Master (SSsounds, SSspatial_grid and their
/// dependencies initialize as in a round), the client seam's host and a player command phase; humans join with a client as
/// the server's join does. The world keeps <c>NotTranslatedThrows</c> on. Its sound sink copies every sound it is handed
/// (tg's playsound shares one /sound across listeners and rewrites it before each SEND_SOUND, so a sink must copy at call time).
/// </summary>
internal sealed class AudioRoom
{
    public TgToyRoom Room { get; }

    public World World => Room.World;

    public SoundCapture Sounds { get; }

    private readonly Queue<Action> _commands = new Queue<Action>();

    public AudioRoom(int width, int height, ulong seed = 51, Action<World> mapExtras = null, int levels = 1, Action<World> configure = null)
    {
        Room = TgToyRoom.Boot(width, height, levels: levels, seed: seed, mapExtras: mapExtras, configure: configure);
        DmWorldState.Of(World).NotTranslatedThrows = true;
        ClientSeam.SetHost(World, new RecordingHost());
        World.AddPhase("test player commands", _ =>
        {
            while (_commands.Count > 0)
                _commands.Dequeue()();
        });
        Sounds = new SoundCapture();
        PresentationSinks.Of(World).Sounds = Sounds;
    }

    public void Command(Action command) => _commands.Enqueue(command);

    public void Step(int ticks = 1) => World.Step(ticks);

    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    /// <summary>A human on (x, y), joined by a player of that key (the host's join).</summary>
    public mob_living_carbon_human Join(int x, int y, string key)
    {
        mob_living_carbon_human body = In(() => Dm.@new<mob_living_carbon_human>(locate(x, y, 1)));
        ClientConnection connection = null;
        Command(() =>
        {
            ClientSeam.KeyMob(body, key);
            connection = ClientSeam.Connect(key);
        });
        Step();
        Assert.NotNull(connection);
        Assert.True(connection.Accepted, "the player did not join");
        Assert.Same(body.client, connection.Client);
        return body;
    }

    /// <summary>A human on (x, y) without a player.</summary>
    public mob_living_carbon_human Dummy(int x, int y) => In(() => Dm.@new<mob_living_carbon_human>(locate(x, y, 1)));

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));
}

/// <summary>One sound as a listener got it: the fields of the shared /sound copied at the SEND_SOUND.</summary>
internal sealed record HeardSound(object Target, string File, float Volume, float Channel, float Frequency, float X, float Y, float Z, float Falloff, object Environment, float Status, float Offset = 0, float Repeat = 0, long Tick = 0, float Wait = 0);

/// <summary>A sound sink that copies what it is handed (rules 15: a recording harness).</summary>
internal sealed class SoundCapture : ISoundSink
{
    public List<HeardSound> Heard { get; } = new List<HeardSound>();

    public void Play(object target, sound s)
        => Heard.Add(new HeardSound(target, s.file == null ? null : s.file is DmResource resource ? resource.Path : Dm.str(s.file), s.volume, s.channel, s.frequency, s.x, s.y, s.z, s.falloff, s.environment, s.status, s.offset, s.repeat, World.CurrentOrNull?.Tick ?? 0, s.wait));

    /// <summary>The sounds <paramref name="who"/> heard of <paramref name="file"/>.</summary>
    public List<HeardSound> Of(mob who, string file) => Heard.Where(h => ReferenceEquals(h.Target, who) && h.File == file).ToList();

    public string Dump() => string.Join("\n", Heard.Select(h => (h.Target as datum)?.dm_id.ToString(CultureInfo.InvariantCulture) + " " + h.File + " " + h.Volume.ToString("R", CultureInfo.InvariantCulture)));
}
