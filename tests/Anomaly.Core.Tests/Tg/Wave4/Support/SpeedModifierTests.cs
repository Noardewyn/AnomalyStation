using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Support;

/// <summary>
/// The movespeed and actionspeed modifier arithmetic (code/modules/movespeed/_movespeed_modifier.dm,
/// code/modules/actionspeed/_actionspeed_modifier.dm): the global cache of static modifiers, variable modifiers, the
/// priority order BINARY_INSERT keeps, the conflict tracker with its <c>abs()</c> comparison, movetypes, and
/// <c>LAZYCOPY</c> minus immunities.
/// </summary>
public class SpeedModifierTests
{
    private static mob_test_support_mob Mob() => Dm.@new<mob_test_support_mob>(locate(1, 1, 1));

    private static datum_movespeed_modifier Instance(string id, float slowdown, float priority = 0, object conflicts_with = null)
    {
        var mod = Dm.@new<datum_movespeed_modifier_test_support_variable>();
        mod.id = id;
        mod.multiplicative_slowdown = slowdown;
        mod.priority = priority;
        mod.conflicts_with = conflicts_with;
        return mod;
    }

    private static object[] Keys(DmList list) => Dm.each(list).Cast<object>().ToArray();

    [Fact]
    public void AStaticModifierIsCachedOnceSharedBetweenMobsAndIdentifiedByItsPath()
    {
        using var w = new SupportWorld();
        var a = Mob();
        var b = Mob();
        Assert.True(Dm.eq(Waiting.NoSleep(a.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow))), TRUE));
        Assert.True(Dm.eq(Waiting.NoSleep(b.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow))), TRUE));
        var cached = (datum_movespeed_modifier)GLOB.movespeed_modification_cache[typeof(datum_movespeed_modifier_test_support_slow)];
        Assert.Equal("/datum/movespeed_modifier/test_support_slow", cached.id); // New: id = "[type]"
        Assert.Same(cached, a.movespeed_modification["/datum/movespeed_modifier/test_support_slow"]);
        Assert.Same(cached, b.movespeed_modification["/datum/movespeed_modifier/test_support_slow"]);
        Assert.True(Dm.eq(a.cached_multiplicative_slowdown, 2));

        // Adding it again finds the same datum and touches nothing.
        Assert.True(Dm.eq(Waiting.NoSleep(a.add_movespeed_modifier(cached)), TRUE));
        Assert.Equal(1, a.movespeed_modification.Count);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void UpdateMovespeedSumsTheModifiersAndSendsTheSignal()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        var listener = Dm.@new<obj_test_support_listener>();
        listener.RegisterSignal(mob, COMSIG_MOB_MOVESPEED_UPDATED, nameof(obj_test_support_listener.on_mock));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow)));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_fast)));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 1.5f));
        Assert.True(Dm.eq(mob.update_movespeed(), 1.5f));
        Assert.True(Dm.eq(listener.times_called, 3));
        // update = FALSE: no recalculation and no signal
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named), FALSE));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 1.5f));
        Assert.True(Dm.eq(listener.times_called, 3));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ModifiersApplyOnlyToTheirMovetypesAndNotToBlacklistedOnes()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow)));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_flying)));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_not_on_ground)));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 2)); // GROUND: the flying one does not apply, the other is blacklisted

        mob.movement_type = FLYING;
        Assert.True(Dm.eq(mob.update_movespeed(), 14)); // 2 + 4 + 8
        mob.movement_type = Dm.bitor(FLYING, GROUND);
        Assert.True(Dm.eq(mob.update_movespeed(), 6));  // the blacklist wins over a matching movetype
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheIdsAreKeptInPriorityOrderWithEqualPrioritiesInArrivalOrder()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Waiting.NoSleep(mob.add_movespeed_modifier(Instance("p5", 0, 5)));
        Waiting.NoSleep(mob.add_movespeed_modifier(Instance("p1", 0, 1)));
        Waiting.NoSleep(mob.add_movespeed_modifier(Instance("p3", 0, 3)));
        Waiting.NoSleep(mob.add_movespeed_modifier(Instance("p3b", 0, 3)));
        Waiting.NoSleep(mob.add_movespeed_modifier(Instance("p9", 0, 9)));
        Waiting.NoSleep(mob.add_movespeed_modifier(Instance("p0", 0, 0)));
        Assert.Equal(new object[] { "p0", "p1", "p3", "p3b", "p5", "p9" }, Keys(mob.movespeed_modification));
        foreach (object key in Keys(mob.movespeed_modification))
            Assert.Equal(key, ((datum_movespeed_modifier)mob.movespeed_modification[key]).id);
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ARepeatedIdReplacesTheOldModifier()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        var first = Instance("same", 1);
        var second = Instance("same", 3);
        Waiting.NoSleep(mob.add_movespeed_modifier(first));
        Waiting.NoSleep(mob.add_movespeed_modifier(second));
        Assert.Same(second, mob.movespeed_modification["same"]);
        Assert.Equal(1, mob.movespeed_modification.Count);
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 3));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void AConflictKeepsALargerAbsoluteAmountOnlyWhenItComesLaterAndEveryPassingOneIsAdded()
    {
        using var w = new SupportWorld();
        // tg's tracker (update_movespeed, :186-193): a conflicting modifier counts when its abs() is strictly larger
        // than the last counted one of its group; the ones counted before it stay in the sum.
        var later = Mob();
        Waiting.NoSleep(later.add_movespeed_modifier(Instance("a", 1, 0, "group")));
        Waiting.NoSleep(later.add_movespeed_modifier(Instance("b", -2, 1, "group")));
        Assert.True(Dm.eq(later.cached_multiplicative_slowdown, -1)); // 1 counted, then -2 counted too

        var earlier = Mob();
        Waiting.NoSleep(earlier.add_movespeed_modifier(Instance("b", -2, 0, "group")));
        Waiting.NoSleep(earlier.add_movespeed_modifier(Instance("a", 1, 1, "group")));
        Assert.True(Dm.eq(earlier.cached_multiplicative_slowdown, -2)); // abs(-2) < abs(1) is false: 1 is skipped

        var tie = Mob();
        Waiting.NoSleep(tie.add_movespeed_modifier(Instance("a", 1, 0, "group")));
        Waiting.NoSleep(tie.add_movespeed_modifier(Instance("c", -1, 1, "group")));
        Waiting.NoSleep(tie.add_movespeed_modifier(Instance("free", 0.25f, 2)));
        Assert.True(Dm.eq(tie.cached_multiplicative_slowdown, 1.25f)); // the abs() tie keeps the first; no conflict, always added

        var zero = Mob();
        Waiting.NoSleep(zero.add_movespeed_modifier(Instance("z", 0, 0, "group")));
        Waiting.NoSleep(zero.add_movespeed_modifier(Instance("a", 1, 1, "group")));
        Assert.True(Dm.eq(zero.cached_multiplicative_slowdown, 1)); // abs(null) < abs(0) is false: a zero never claims the group
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ImmunitiesAreLeftOutOfTheCopyButStayInTheModificationList()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow)));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_fast)));
        mob.movespeed_mod_immunities = Dm.list("/datum/movespeed_modifier/test_support_slow");
        var mods = (DmList)mob.get_movespeed_modifiers();
        Assert.NotSame(mob.movespeed_modification, mods);
        Assert.Equal(new object[] { "/datum/movespeed_modifier/test_support_fast" }, Keys(mods));
        Assert.Equal(2, mob.movespeed_modification.Count);
        Assert.True(Dm.eq(mob.update_movespeed(), -0.5f));

        var bare = Mob();
        Assert.Equal(0, ((DmList)bare.get_movespeed_modifiers()).Count); // LAZYCOPY of a null list is list()
        Assert.True(Dm.eq(bare.update_movespeed(), 0));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void RemovalWorksByTypeByIdAndByDatumAndNullsTheEmptyList()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow)));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named)));
        var instance = Instance("inst", 4);
        Waiting.NoSleep(mob.add_movespeed_modifier(instance));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 7));

        Assert.True(Dm.eq(mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named)), TRUE)); // initial(id) "named_mod"
        Assert.True(Dm.eq(mob.remove_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named)), FALSE));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 6));
        Assert.True(Dm.eq(mob.remove_movespeed_modifier("/datum/movespeed_modifier/test_support_slow", FALSE), TRUE));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 6)); // update = FALSE
        Assert.True(Dm.eq(mob.remove_movespeed_modifier(instance), TRUE));
        Assert.Null(mob.movespeed_modification); // LAZYREMOVE nulls the emptied list
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 0));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void HasMovespeedModifierFindsByTypeByIdAndByDatum()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Assert.Null(mob.has_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named)));
        Waiting.NoSleep(mob.add_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named)));
        object named = mob.has_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_named));
        Assert.IsType<datum_movespeed_modifier_test_support_named>(named);
        Assert.Same(named, mob.has_movespeed_modifier("named_mod"));
        Assert.Same(named, mob.has_movespeed_modifier(named));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void AVariableModifierIsMadePerMobAndUpdatedInPlace()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        var mod = (datum_movespeed_modifier)Waiting.NoSleep(mob.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_variable), multiplicative_slowdown: 3));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 3));
        Assert.Same(mod, Waiting.NoSleep(mob.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_variable), multiplicative_slowdown: 5)));
        Assert.True(Dm.eq(mod.multiplicative_slowdown, 5));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 5));
        Assert.Same(mod, Waiting.NoSleep(mob.add_or_update_variable_movespeed_modifier("/datum/movespeed_modifier/test_support_variable")));
        Assert.Null(GLOB.movespeed_modification_cache[typeof(datum_movespeed_modifier_test_support_variable)]);

        var other = Mob();
        Assert.NotSame(mod, Waiting.NoSleep(other.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_variable), multiplicative_slowdown: 1)));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheModifierProcsCrashOnTheWrongKindOfModifier()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Assert.Contains("is a variable modifier, and can never be cached",
            Assert.ThrowsAny<Exception>(() => Waiting.NoSleep(get_cached_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_variable)))).Message);
        Assert.Contains("is not a movespeed modification typepath",
            Assert.ThrowsAny<Exception>(() => Waiting.NoSleep(get_cached_movespeed_modifier(typeof(datum_actionspeed_modifier_test_support_slow)))).Message);
        Assert.Contains("Not a variable modifier",
            Assert.ThrowsAny<Exception>(() => Waiting.NoSleep(mob.add_or_update_variable_movespeed_modifier(typeof(datum_movespeed_modifier_test_support_slow)))).Message);
        Assert.Contains("Couldn't find existing modification when provided a text ID.",
            Assert.ThrowsAny<Exception>(() => Waiting.NoSleep(mob.add_or_update_variable_movespeed_modifier("nothing"))).Message);
    }

    [Fact]
    public void TheConfigSpeedIsLookedUpByTheMobsExactType()
    {
        using var w = new SupportWorld();
        GLOB.mob_config_movespeed_type_lookup[typeof(mob_test_support_mob)] = 1.5f;
        var mob = Mob();
        Waiting.NoSleep(mob.update_config_movespeed());
        var mod = (datum_movespeed_modifier)mob.has_movespeed_modifier(typeof(datum_movespeed_modifier_mob_config_speedmod));
        Assert.True(Dm.eq(mod.multiplicative_slowdown, 1.5f));
        Assert.True(Dm.eq(mob.cached_multiplicative_slowdown, 1.5f));

        var other = Dm.@new<mob_test_support_mob_other>();
        Waiting.NoSleep(other.update_config_movespeed());
        Assert.True(Dm.eq(((datum_movespeed_modifier)other.has_movespeed_modifier(typeof(datum_movespeed_modifier_mob_config_speedmod))).multiplicative_slowdown, 0));

        GLOB.mob_config_movespeed_type_lookup = null;
        Assert.True(Dm.eq(mob.get_config_multiplicative_speed(), 0));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void TheDataNullChecksAreTrueForANoOpModifier()
    {
        using var w = new SupportWorld();
        Assert.True(Dm.eq(movespeed_data_null_check(Dm.@new<datum_movespeed_modifier>()), TRUE));
        Assert.True(Dm.eq(movespeed_data_null_check(Dm.@new<datum_movespeed_modifier_test_support_slow>()), FALSE));
        Assert.True(Dm.eq(actionspeed_data_null_check(Dm.@new<datum_actionspeed_modifier>()), TRUE));
        Assert.True(Dm.eq(actionspeed_data_null_check(Dm.@new<datum_actionspeed_modifier_test_support_slow>()), FALSE));
    }

    [Fact]
    public void ActionspeedStartsUnsetAndInitializeActionspeedMakesItOne()
    {
        using var w = new SupportWorld();
        var mob = Mob();
        Assert.Null(mob.cached_multiplicative_actions_slowdown); // the do_after trap of turf-area-obj-mob.md 0.4a
        Waiting.NoSleep(mob.initialize_actionspeed());
        Assert.True(Dm.eq(mob.cached_multiplicative_actions_slowdown, 1));
        Assert.NotNull(mob.has_actionspeed_modifier(typeof(datum_actionspeed_modifier_base)));

        Waiting.NoSleep(mob.add_actionspeed_modifier(typeof(datum_actionspeed_modifier_test_support_slow)));
        Assert.True(Dm.eq(mob.cached_multiplicative_actions_slowdown, 1.5f));
        Assert.Same(GLOB.actionspeed_modification_cache[typeof(datum_actionspeed_modifier_test_support_slow)], mob.has_actionspeed_modifier("/datum/actionspeed_modifier/test_support_slow"));

        mob.actionspeed_mod_immunities = Dm.list("/datum/actionspeed_modifier/base");
        Assert.True(Dm.eq(mob.update_actionspeed(), 0.5f));
        Assert.True(Dm.eq(mob.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_test_support_slow)), TRUE));
        Assert.True(Dm.eq(mob.cached_multiplicative_actions_slowdown, 0));
        Assert.Empty(w.Runtimes);
    }

    [Fact]
    public void ActionspeedModifiersTakeAnIdAtCreationAndShareTheConflictRules()
    {
        using var w = new SupportWorld();
        Assert.Equal("custom", Dm.@new<datum_actionspeed_modifier_test_support_slow>("custom").id); // New(init_id)
        Assert.Equal("/datum/actionspeed_modifier/test_support_slow", Dm.@new<datum_actionspeed_modifier_test_support_slow>().id);

        var mob = Mob();
        var a = Dm.@new<datum_actionspeed_modifier_test_support_variable>("a");
        a.multiplicative_slowdown = 1;
        a.conflicts_with = "group";
        var b = Dm.@new<datum_actionspeed_modifier_test_support_variable>("b");
        b.multiplicative_slowdown = -1;
        b.conflicts_with = "group";
        b.priority = 1;
        Waiting.NoSleep(mob.add_actionspeed_modifier(a));
        Waiting.NoSleep(mob.add_actionspeed_modifier(b));
        Assert.True(Dm.eq(mob.cached_multiplicative_actions_slowdown, 1)); // the abs() tie keeps the first
        var v = (datum_actionspeed_modifier)Waiting.NoSleep(mob.add_or_update_variable_actionspeed_modifier(typeof(datum_actionspeed_modifier_test_support_variable), multiplicative_slowdown: 2));
        Assert.Equal("/datum/actionspeed_modifier/test_support_variable", v.id);
        Assert.True(Dm.eq(mob.cached_multiplicative_actions_slowdown, 3));
        Assert.Empty(w.Runtimes);
    }
}
