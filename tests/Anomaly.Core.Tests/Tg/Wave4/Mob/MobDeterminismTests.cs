using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>Determinism of the mob, obj and timed action state (translation-rules.md 15): the same scenario twice gives the same world state hash, tick by tick.</summary>
public class MobDeterminismTests
{
    /// <summary>A busy scenario with mobs and objs in a room: creation, tags, nutrition from the RNG, spins, do_afters, steps and deletions.</summary>
    private static List<ulong> RunScenario(ulong seed, int variant = 0)
    {
        var room = new MobRoom(seed);
        var hashes = new List<ulong>();
        var mobs = new List<TgMob>();
        var tasks = new List<DmTask<object>>();
        room.In(() =>
        {
            for (int i = 0; i < 8; i++)
                mobs.Add(Dm.@new<TgMob>(MobRoom.T(1 + i % 4, 1 + i / 4)));
            for (int i = 0; i < 5; i++)
                Dm.@new<obj_test_mob_tagged_n>(MobRoom.T(5, 1 + i));
            Dm.@new<obj_test_mob_processing>(MobRoom.T(6, 6));
        });
        hashes.Add(room.World.ComputeStateHash());
        for (int tick = 1; tick <= 40; tick++)
        {
            room.In(() =>
            {
                if (tick == 2)
                {
                    mobs[0].spin(6, 1);
                    mobs[1].spin(4, 2);
                    for (int i = 2; i < 6; i++)
                        tasks.Add(do_after(mobs[i], 1 * SECONDS, locate(1 + i % 4, 3, 1)));
                }
                if (tick == 10)
                {
                    Waiting.NoSleep(step(mobs[2], EAST)); // cancels mobs[2]'s do_after
                    Waiting.NoSleep(mobs[7].adjust_nutrition(-25 - variant));
                    Waiting.NoSleep(mobs[6].set_stat(DEAD));
                }
                if (tick == 15)
                    qdel(mobs[3]);
                if (tick == 25)
                    ADD_TRAIT(mobs[4], TRAIT_INCAPACITATED, "scenario");
            });
            room.Tick();
            if (tick % 3 == 0 || tick == 10 || tick == 15 || tick == 25)
                hashes.Add(room.World.ComputeStateHash());
        }
        Assert.Empty(room.RuntimeText);
        return hashes;
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameStateHashAtEverySampledTick()
    {
        List<ulong> a = RunScenario(11), b = RunScenario(11);
        Assert.Equal(a, b);
        Assert.True(a.Distinct().Count() >= 10, "the state changes while the scenario runs");
    }

    [Fact]
    public void ADifferentSeedGivesADifferentStateBecauseNutritionIsRandom()
    {
        ulong Created(ulong seed)
        {
            var room = new MobRoom(seed);
            room.In(() =>
            {
                for (int i = 0; i < 8; i++)
                    Dm.@new<TgMob>(MobRoom.T(1 + i, 1));
            });
            return room.World.ComputeStateHash();
        }

        Assert.Equal(Created(11), Created(11));
        Assert.NotEqual(Created(11), Created(12));
    }

    [Fact]
    public void ADifferentScenarioGivesADifferentStateHash()
    {
        List<ulong> a = RunScenario(11, variant: 0), b = RunScenario(11, variant: 1);
        Assert.Equal(a[0], b[0]); // identical until the nutrition change at tick 10
        Assert.Equal(a[1], b[1]);
        Assert.NotEqual(a[^1], b[^1]);
    }

    [Fact]
    public void ABootedWorldWithMobsAndObjsIsDeterministicAndRunsWithoutRuntimes()
    {
        List<ulong> Run(out int runtimes)
        {
            var room = new BootedRoom(9);
            var hashes = new List<ulong>();
            room.In(() =>
            {
                for (int i = 0; i < 6; i++)
                    Dm.@new<TgMob>(locate(1 + i, 1, 1));
                for (int i = 0; i < 6; i++)
                    Dm.@new<obj_test_mob_processing>(locate(1 + i, 2, 1));
            });
            room.World.AddPhase("script", w =>
            {
                if (w.Tick % 15 == 0)
                {
                    var m = Dm.each<TgMob>(locate(1 + (int)(w.Tick / 15) % 6, 1, 1).contents).FirstOrDefault();
                    if (m != null)
                        Waiting.Detach(do_after(m, 1 * SECONDS));
                }
            });
            for (int i = 0; i < 80; i++)
            {
                room.World.Step();
                if (i % 10 == 9)
                    hashes.Add(room.World.ComputeStateHash());
            }
            runtimes = room.Log.Runtimes.Count;
            return hashes;
        }

        List<ulong> a = Run(out int ra), b = Run(out int rb);
        Assert.Equal(0, ra);
        Assert.Equal(0, rb);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ObjectsProcessedBySSobjAreProcessedByTheMasterAndStopAtDestroy()
    {
        var room = new BootedRoom();
        obj_test_mob_processing obj = null;
        room.In(() =>
        {
            obj = Dm.@new<obj_test_mob_processing>(locate(2, 2, 1));
            START_PROCESSING(SSobj, obj);
        });
        room.World.Step(120); // SSobj's wait is 2 SECONDS: 40 ticks
        float processed = 0;
        room.In(() =>
        {
            processed = obj.processed;
            Assert.InRange(processed, 2f, 4f);
            qdel(obj);
        });
        room.World.Step(120);
        room.In(() => Assert.Equal(processed, obj.processed));
        Assert.Empty(room.RuntimeText);
    }
}

/// <summary>An obj with an id_tag from its world id (distinct tags for the scenario's many objs).</summary>
[DmPath("/obj/test_mob_tagged_n")]
public class obj_test_mob_tagged_n : obj
{
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        id_tag = Dm.text("scenario_[]", dm_id);
        return base.Initialize(mapload, __extra);
    }
}
