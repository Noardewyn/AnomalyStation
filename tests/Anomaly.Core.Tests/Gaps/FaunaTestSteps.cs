using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>The steps the fauna's test classes share: run the room for a while, run it until something holds, look on the floor.</summary>
internal static class FaunaTestSteps
{
    public const int Second = 20;

    public static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    public static bool RunUntil(AudioRoom r, float deciseconds, Func<bool> done)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
        {
            if (r.In(done))
                return true;
            r.Step();
        }
        return r.In(done);
    }

    public static bool OnFloor<T>(AudioRoom r, int dist, atom center) where T : class =>
        r.In(() => Dm.each<atom>(range(dist, center)).Any(a => a is T));

    /// <summary>A creature that a crusher killed alone (its crusher damage is its whole health) leaves its trophy: on the floor
    /// for a creature deleted on death, in its butchering results otherwise. tg's drop is a chance (crusher_loot.dm:55), so the
    /// creature is made and killed until the trophy turns up, at most twenty times.</summary>
    public static bool DropsTrophy(AudioRoom r, Type mobType, Type trophy, int x, int y)
    {
        for (int i = 0; i < 20; i++)
        {
            var mob = (mob_living)r.In(() => Dm.@new(mobType, locate(x, y, 1)));
            r.Step(2);
            r.In(() =>
            {
                var damage = Waiting.NoSleep(mob.apply_status_effect(typeof(datum_status_effect_crusher_damage))) as datum_status_effect_crusher_damage;
                damage.total_damage = Dm.num(mob.maxHealth);
                Waiting.NoSleep(mob.death(FALSE));
            });
            r.Step(2);
            bool dropped = r.In(() => (mob.guaranteed_butcher_results != null && mob.guaranteed_butcher_results[trophy] != null)
                || Dm.each<atom>(range(1, locate(x, y, 1))).Any(a => a.GetType() == trophy));
            if (dropped)
                return true;
            r.In(() =>
            {
                if (!QDELETED(mob))
                    qdel(mob);
            });
            r.Step(1);
        }
        return false;
    }

    /// <summary>An atom as the map loader makes it: Initialize with mapload (as BarH3Tests' Mapped).</summary>
    public static T Mapped<T>(AudioRoom r, int x, int y, params object[] args) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        T made = (T)Dm.@new(typeof(T), new object[] { locate(x, y, 1) }.Concat(args).ToArray());
        SSatoms.initialized = previous;
        return made;
    });

    /// <summary>Makes, runs for a while and deletes one of each given type, as the lifecycle sweeps do.</summary>
    public static void Lifecycle(AudioRoom r, Type path)
    {
        mob_living_carbon_human bob = r.Dummy(10, 4);
        var made = (datum)r.In(() => Dm.@new(path, locate(4, 4, 1)));
        Assert.False(r.In(() => made is atom a && InertSeam.IsInert(a)), path.Name + " is inert");
        RunFor(r, 60);
        r.In(() => qdel(bob));
        RunFor(r, 40);
        r.In(() =>
        {
            if (!QDELETED(made))
                qdel(made);
        });
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }
}
