using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Medical.T1;

/// <summary>
/// M2 T1's world: a booted toy room (the real Master; SSmobs, SSpriority_effects, SSfastprocess, SStimed_actions and SStimer
/// fire) with <c>NotTranslatedThrows</c> on, so a reached <c>Dm.NotTranslated</c> is a failure. The tests are written from the
/// tg source (DM), not from the C#: each asserts what tg's code does, with the tg file and line it follows.
/// </summary>
internal sealed class MedRoom
{
    // World.Fps: 20 ticks per second, 2 ticks per decisecond.
    public const int TicksPerSecond = 20;

    public TgToyRoom Room { get; }

    public World World => Room.World;

    public MedRoom(ulong seed = 71, bool strict = true, int size = TgToyRoom.Size)
    {
        Room = TgToyRoom.Boot(size, size, seed: seed);
        DmWorldState.Of(World).NotTranslatedThrows = strict;
    }

    public void In(Action body) => Room.In(body);

    public T In<T>(Func<T> body) => Room.In(body);

    public void Step(int ticks = 1) => Room.Step(ticks);

    public long Tick => World.Tick;

    /// <summary>The world time in deciseconds (what DM's world.time reads).</summary>
    public float Time => In(() => Dm.world.time);

    /// <summary>A real <c>/mob/living/carbon/human/consistent</c> (tg's unit test human: stock dna, no randomness).</summary>
    public mob_living_carbon_human_consistent Human(int x = 3, int y = 3)
        => In(() => Dm.@new<mob_living_carbon_human_consistent>(locate(x, y, 1)));

    /// <summary>A real <c>/mob/living/carbon/human</c> with tg's own randomized setup (what a joining player gets).</summary>
    public mob_living_carbon_human PlainHuman(int x = 3, int y = 3)
        => In(() => Dm.@new<mob_living_carbon_human>(locate(x, y, 1)));

    /// <summary>Steps until <paramref name="condition"/> holds (read inside the world), at most <paramref name="maxTicks"/>; returns the ticks stepped.</summary>
    public int StepUntil(Func<bool> condition, int maxTicks)
    {
        for (int i = 1; i <= maxTicks; i++)
        {
            Step();
            if (In(condition))
                return i;
        }
        throw new Xunit.Sdk.XunitException($"the condition did not hold within {maxTicks} ticks\n" + string.Join("\n", Room.RuntimeText));
    }

    public void AssertNoRuntimes() => Assert.True(Room.Runtimes.Count == 0, string.Join("\n", Room.RuntimeText));

    /// <summary>The room's air at a turf (inside the world).</summary>
    public static datum_gas_mixture AirAt(int x, int y) => ((turf_open)locate(x, y, 1)).air;

    /// <summary>
    /// Applies <paramref name="edit"/> to the air of every open turf of the room (inside the world). Since M4b slice 1 the air
    /// moves (tg's LINDA): one turf's changed air is shared with its neighbours, so a test of "a room with this air" changes
    /// all of it.
    /// </summary>
    public static void RoomAir(Action<datum_gas_mixture> edit)
    {
        foreach (turf t in Dm.each<turf>(block(1, 1, 1, Dm.world.maxx, Dm.world.maxy, 1)))
        {
            if (t is turf_open open && open.air != null)
                edit(open.air);
        }
    }

    /// <summary>The oxygen moles of a mixture.</summary>
    public static float O2(datum_gas_mixture air) => Dm.num(air.moles[typeof(datum_gas_oxygen)]);

    /// <summary>The carbon dioxide moles of a mixture.</summary>
    public static float CO2(datum_gas_mixture air) => Dm.num(air.moles[typeof(datum_gas_carbon_dioxide)]);

    // ---- synchronous calls of tg's coloured procs (rules: Waiting.NoSleep for a synchronous caller) ----

    public static object Apply(mob_living m, float amount, string type, object zone = null, float? forced = null, float? blocked = null, float? spread = null)
        => Waiting.NoSleep(m.apply_damage(amount, type, zone, blocked, forced, spread));

    public static void Heal(mob_living m, float amount, string type) => Waiting.NoSleep(m.heal_damage_type(amount, type));

    public static float Brute(mob_living m) => Dm.num(m.get_brute_loss());

    public static float Burn(mob_living m) => Dm.num(m.get_fire_loss());

    public static float Tox(mob_living m) => Dm.num(m.get_tox_loss());

    public static float Oxy(mob_living m) => Dm.num(m.get_oxy_loss());

    public static float Stam(mob_living m) => Dm.num(m.get_stamina_loss());

    /// <summary>A recorder watching the given mob's life, breath, death and stat changes.</summary>
    public datum_test_t1_recorder Record(mob_living_carbon m, bool life = true, bool breath = true, bool death = true, bool stat = true, bool position = false)
        => In(() =>
        {
            var r = Dm.@new<datum_test_t1_recorder>();
            if (life) r.WatchLife(m);
            if (breath) r.WatchBreath(m);
            if (death) r.WatchDeath(m);
            if (stat) r.WatchStat(m);
            if (position) r.WatchPosition(m);
            return r;
        });

    public static obj_item_bodypart Part(mob_living_carbon c, string zone) => (obj_item_bodypart)c.get_bodypart(zone);
}

/// <summary>
/// A recording listener (a fixture type of the port's own, <c>/datum/test_t1_recorder</c>, rules 15): the signals the
/// medical scenarios count, each with the world tick and SSmobs' fire count at that moment.
/// </summary>
[DmPath("/datum/test_t1_recorder")]
public class datum_test_t1_recorder : datum
{
    public readonly List<(long Tick, float TimesFired)> Lives = new List<(long, float)>();
    public readonly List<(long Tick, float TimesFired)> Breaths = new List<(long, float)>();
    public readonly List<long> Deaths = new List<long>();
    public readonly List<(long Tick, object NewStat, object OldStat)> StatChanges = new List<(long, object, object)>();
    public readonly List<(long Tick, object NewPosition)> Positions = new List<(long, object)>();
    public readonly List<(long Tick, float Flags)> Revives = new List<(long, float)>();
    public readonly List<(long Tick, float Flags)> PostHeals = new List<(long, float)>();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public void WatchLife(mob_living m) => RegisterSignal(m, COMSIG_LIVING_LIFE, nameof(on_life));

    public void WatchBreath(mob_living_carbon m) => RegisterSignal(m, COMSIG_CARBON_PRE_BREATHE, nameof(on_breathe));

    public void WatchDeath(mob_living m) => RegisterSignal(m, COMSIG_LIVING_DEATH, nameof(on_death));

    public void WatchStat(mob m) => RegisterSignal(m, COMSIG_MOB_STATCHANGE, nameof(on_stat));

    public void WatchPosition(mob_living m) => RegisterSignal(m, COMSIG_LIVING_SET_BODY_POSITION, nameof(on_position));

    public void WatchRevive(mob_living m) => RegisterSignal(m, COMSIG_LIVING_REVIVE, nameof(on_revive));

    public void WatchPostHeal(mob_living m) => RegisterSignal(m, COMSIG_LIVING_POST_FULLY_HEAL, nameof(on_post_heal));

    [SignalHandler]
    public virtual void on_life(datum source = null, object seconds_per_tick = null)
        => Lives.Add((World.Current.Tick, SSmobs.times_fired));

    [SignalHandler]
    public virtual void on_breathe(datum source = null, object seconds_per_tick = null)
        => Breaths.Add((World.Current.Tick, SSmobs.times_fired));

    [SignalHandler]
    public virtual void on_death(datum source = null, object gibbed = null)
        => Deaths.Add(World.Current.Tick);

    [SignalHandler]
    public virtual void on_stat(datum source = null, object new_stat = null, object old_stat = null)
        => StatChanges.Add((World.Current.Tick, new_stat, old_stat));

    [SignalHandler]
    public virtual void on_position(datum source = null, object new_position = null, object old_position = null)
        => Positions.Add((World.Current.Tick, new_position));

    [SignalHandler]
    public virtual void on_revive(datum source = null, object full_heal_flags = null)
        => Revives.Add((World.Current.Tick, Dm.num(full_heal_flags)));

    [SignalHandler]
    public virtual void on_post_heal(datum source = null, object heal_flags = null)
        => PostHeals.Add((World.Current.Tick, Dm.num(heal_flags)));
}
