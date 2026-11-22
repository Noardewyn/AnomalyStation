using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Replication;

/// <summary>What the random scenario of a <see cref="ReplicationWorld"/> does each tick (probabilities per tick).</summary>
internal sealed class ScenarioMix
{
    /// <summary>Each mover steps through tg's <c>step()</c> every this many ticks (tg's run delay in M1, D98: 3).</summary>
    public int StepEvery { get; init; } = 3;

    public double Spawn { get; init; } = 0.10;
    public double Delete { get; init; } = 0.06;
    public double AbstractMove { get; init; } = 0.05;
    public double ForceMove { get; init; } = 0.05;
    public double Rename { get; init; } = 0.05;
    public double Turn { get; init; } = 0.10;
    public double Glide { get; init; } = 0.05;
    public double IntoContainer { get; init; } = 0.04;
    public double OutOfContainer { get; init; } = 0.04;
    public double ToNullspace { get; init; } = 0.02;
    public double FromNullspace { get; init; } = 0.02;
    public double Invisibility { get; init; } = 0.02;

    /// <summary>Only the movers step: no spawns, deletes, teleports or renames (the measurements' steady state).</summary>
    public static ScenarioMix MoversOnly { get; } = new()
    {
        Spawn = 0, Delete = 0, AbstractMove = 0, ForceMove = 0, Rename = 0, Turn = 0, Glide = 0,
        IntoContainer = 0, OutOfContainer = 0, ToNullspace = 0, FromNullspace = 0, Invisibility = 0,
    };

    /// <summary>Everything but renames (no change that only the sweep can find).</summary>
    public ScenarioMix WithoutRenames() => new()
    {
        StepEvery = StepEvery, Spawn = Spawn, Delete = Delete, AbstractMove = AbstractMove, ForceMove = ForceMove, Rename = 0,
        Turn = Turn, Glide = Glide, IntoContainer = IntoContainer, OutOfContainer = OutOfContainer, ToNullspace = ToNullspace,
        FromNullspace = FromNullspace, Invisibility = Invisibility,
    };
}

/// <summary>
/// A booted tg world for the replication tests and measurements: tg's real boot (Genesis, GLOB, the Master) with the toy
/// room of the given size as its map, at RUNLEVEL_GAME, with plain <c>/mob</c> movers that step through tg's
/// <c>step()</c> and a seeded random scenario in a player-command phase (spawns, qdels, <c>abstract_move</c>,
/// <c>forceMove</c>, renames, turns, glide changes, moves into and out of containers and nullspace, abstract invisibility).
/// The scenario's randomness is the test's own seeded <see cref="Random"/>, a scripted player: two runs of one seed do the
/// same things.
/// </summary>
internal sealed class ReplicationWorld
{
    private readonly Random _random;
    private readonly ScenarioMix _mix;
    private readonly List<mob> _movers = new();
    private readonly List<float> _headings = new();
    private readonly List<atom_movable> _things = new(); // what the scenario spawned and may act on
    private readonly List<atom_movable> _away = new(); // things it put into a container or nullspace
    private readonly List<obj> _containers = new();
    private int _names;

    private static readonly float[] Dirs = { Dm.NORTH, Dm.SOUTH, Dm.EAST, Dm.WEST };

    public World World { get; }

    public List<Exception> Runtimes { get; } = new();

    public int Size { get; }

    /// <summary>Whether the random scenario runs (the movers always step once there are any).</summary>
    public bool ScenarioOn { get; set; } = true;

    /// <summary>How many of the movers step (the first ones); all by default.</summary>
    public int ActiveMovers { get; set; } = int.MaxValue;

    /// <summary>Steps tried and steps that succeeded.</summary>
    public long Steps { get; private set; }

    public long StepsMoved { get; private set; }

    /// <summary>Tick of the last rename of each renamed object (for lateness).</summary>
    public Dictionary<long, long> RenamedAt { get; } = new();

    /// <summary>How often the scenario did each kind of thing.</summary>
    public SortedDictionary<string, int> Actions { get; } = new(StringComparer.Ordinal);

    public IReadOnlyList<mob> Movers => _movers;

    private ReplicationWorld(int size, int seed, ScenarioMix mix, bool notTranslatedThrows)
    {
        Size = size;
        _random = new Random(seed);
        _mix = mix ?? new ScenarioMix();
        World = new World((ulong)seed) { RuntimeErrorSink = Runtimes.Add };
        DmWorldState.Of(World).NotTranslatedThrows = notTranslatedThrows;
        // tg's strings/ files, as the server mounts them (ServerHost.MountTgStrings): M10 slice F's ticker and a client's preferences
        // (its random name) read them
        if (TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tg)
            TgStrings.Mount(DmWorldState.Of(World).Files, tg);
    }

    /// <summary>Boots a world with a room of <paramref name="size"/> x <paramref name="size"/> and <paramref name="movers"/> plain mobs on random floor.</summary>
    public static ReplicationWorld Boot(int size = 24, int movers = 20, int seed = 7, ScenarioMix mix = null, bool notTranslatedThrows = true, int containers = 4)
    {
        var w = new ReplicationWorld(size, seed, mix, notTranslatedThrows);
        SoundLengths.Install(w.World, Integration.RigSoundLengths.Lengths); // M9 slice B: a hurt human's scream is a sound token
        TgWorld.Boot(w.World, _ => ToyRoom.Build(size, size));
        while ((!Initialized(w.World) || w.World.Tick < 30) && w.World.Tick < 400) w.World.Step();
        if (!Initialized(w.World)) throw new InvalidOperationException("the Master did not initialize");
        using (w.World.Enter())
        {
            Master.SetRunLevel(RUNLEVEL_GAME);
            for (int i = 0; i < movers; i++)
            {
                mob m = Dm.@new<mob>(w.RandomFloor());
                m.name = "mover " + i;
                w._movers.Add(m);
                w._headings.Add(Dirs[w._random.Next(4)]);
            }
            for (int i = 0; i < containers; i++)
            {
                obj box = Dm.@new<obj>(w.RandomFloor());
                box.name = "box " + i;
                w._containers.Add(box);
            }
        }
        w.World.AddPhase("player commands", w.Commands);
        return w;
    }

    /// <summary>Adds <paramref name="count"/> plain objs on random floor (static clutter, for the measurements).</summary>
    public void AddClutter(int count)
    {
        using (World.Enter())
        {
            for (int i = 0; i < count; i++)
            {
                obj o = Dm.@new<obj>(RandomFloor());
                o.name = "clutter " + i;
            }
        }
    }

    public void Step() => World.Step();

    private static bool Initialized(World world)
    {
        using (world.Enter())
            return Master != null && Dm.eq(datum_controller_master.init_stage_completed, INITSTAGE_MAX);
    }

    private turf RandomFloor() => locate(_random.Next(2, Size), _random.Next(2, Size), 1);

    // The player command phase (inside the world).
    private void Commands(World world)
    {
        for (int i = 0; i < _movers.Count && i < ActiveMovers; i++)
        {
            mob m = _movers[i];
            if ((world.Tick + i) % _mix.StepEvery != 0 || m.loc is not turf) continue;
            if (_random.Next(8) == 0) _headings[i] = Dirs[_random.Next(4)];
            object moved = Waiting.NoSleep(step(m, _headings[i]));
            Steps++;
            if (Dm.truthy(moved)) StepsMoved++;
            else _headings[i] = Dirs[_random.Next(4)];
        }
        if (!ScenarioOn) return;

        if (Chance(_mix.Spawn))
        {
            atom_movable thing = _random.Next(3) == 0 ? Dm.@new<mob>(RandomFloor()) : Dm.@new<obj>(RandomFloor());
            thing.name = "thing " + (++_names);
            _things.Add(thing);
            Count("spawn");
        }
        if (Chance(_mix.Delete) && Pick(_things, out atom_movable doomed))
        {
            _things.Remove(doomed);
            _away.Remove(doomed);
            qdel(doomed);
            Count("qdel");
        }
        // abstract_move on objs only: tg's mob override asks is_secret_level, a z-level trait leaf of M3.
        if (Chance(_mix.AbstractMove) && PickActor(out atom_movable a1) && a1 is not mob)
        {
            Waiting.NoSleep(a1.abstract_move(RandomFloor()));
            Count("abstract_move");
        }
        if (Chance(_mix.ForceMove) && PickActor(out atom_movable a2))
        {
            Waiting.NoSleep(a2.forceMove(RandomFloor()));
            Count("forceMove");
        }
        if (Chance(_mix.Rename) && PickActor(out atom_movable a3))
        {
            a3.name = "renamed " + (++_names);
            RenamedAt[a3.dm_id] = world.Tick;
            Count("rename");
        }
        if (Chance(_mix.Turn) && PickActor(out atom_movable a4))
        {
            Waiting.NoSleep(a4.setDir(Dirs[_random.Next(4)]));
            Count("setDir");
        }
        if (Chance(_mix.Glide) && PickActor(out atom_movable a5))
        {
            a5.set_glide_size(_random.Next(1, 33));
            Count("set_glide_size");
        }
        if (Chance(_mix.IntoContainer) && Pick(_things, out atom_movable a6) && a6.loc is turf && _containers.Count > 0)
        {
            Waiting.NoSleep(a6.forceMove(_containers[_random.Next(_containers.Count)]));
            _away.Add(a6);
            Count("into container");
        }
        if (Chance(_mix.ToNullspace) && Pick(_things, out atom_movable a7) && a7.loc is turf)
        {
            Waiting.NoSleep(a7.moveToNullspace());
            _away.Add(a7);
            Count("moveToNullspace");
        }
        if ((Chance(_mix.OutOfContainer) || Chance(_mix.FromNullspace)) && Pick(_away, out atom_movable back))
        {
            _away.Remove(back);
            Waiting.NoSleep(back.forceMove(RandomFloor()));
            Count("back onto a turf");
        }
        if (Chance(_mix.Invisibility) && Pick(_things, out atom_movable a8))
        {
            a8.invisibility = a8.invisibility >= INVISIBILITY_ABSTRACT ? 0 : INVISIBILITY_ABSTRACT;
            Count("invisibility");
        }
    }

    private void Count(string action) => Actions[action] = Actions.GetValueOrDefault(action) + 1;

    private bool Chance(double p) => p > 0 && _random.NextDouble() < p;

    private bool Pick<T>(List<T> list, out T item)
    {
        if (list.Count == 0)
        {
            item = default;
            return false;
        }
        item = list[_random.Next(list.Count)];
        return true;
    }

    // A mover or a spawned thing, on a turf.
    private bool PickActor(out atom_movable actor)
    {
        int total = _movers.Count + _things.Count;
        actor = null;
        if (total == 0) return false;
        int i = _random.Next(total);
        actor = i < _movers.Count ? _movers[i] : _things[i - _movers.Count];
        return actor.loc is turf;
    }
}
