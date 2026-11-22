using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Medical.T1;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.DevEffects;

/// <summary>
/// The owner (2026-10-15): the dev fly is not moved by gravity or the air and no slowdown acts on it; under the dev godmode
/// no status effect applies and no slowdown acts. Both through tg's own admin-sourced traits: the fly's
/// <c>TRAIT_NEGATES_GRAVITY</c> (living <c>experience_pressure_difference</c> returns on <c>mob_negates_gravity</c>,
/// living.dm:1113), both switches' <c>TRAIT_IGNORESLOWDOWN</c> (human <c>get_movespeed_modifiers</c>, human_movement.dm:1),
/// the godmode's <c>TRAIT_GODMODE</c> (the stun family, <c>check_stun_immunity</c>) with <c>TRAIT_NO_STAGGER</c>,
/// <c>TRAIT_NO_SIDE_KICK</c> and <c>TRAIT_NOFIRE</c>. Turning a switch off takes its traits.
/// </summary>
public class DevFlyGodTests
{
    private static int X(mob m) => (int)Dm.num(m.x);

    // A strong space wind on the human's tile (tg's move_prob: 500 / pressure_resistance 5 * 75 - 25, certain)
    private static void Wind(mob_living m) => m.experience_pressure_difference(500, EAST);

    [Fact]
    public void TheWindPushesAWalkerButNotTheDevFlyerAndPushesItAgainOnceTheFlyIsOff()
    {
        var room = new MedRoom(seed: 921);
        var human = room.Human(3, 4);
        room.In(() => Wind(human));
        Assert.Equal(4, room.In(() => X(human))); // tg's step(src, direction)

        room.In(() =>
        {
            Assert.Contains("fly on", DevCommands.Fly(human));
            Assert.True(HAS_TRAIT(human, TRAIT_NEGATES_GRAVITY));
            Assert.True(HAS_TRAIT_FROM(human, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION)); // on_negate_gravity, a turf with ground
            Assert.True(Dm.truthy(human.mob_negates_gravity()));
            Wind(human);
        });
        Assert.Equal(4, room.In(() => X(human)));

        room.In(() =>
        {
            Assert.Contains("fly off", DevCommands.Fly(human));
            Assert.False(HAS_TRAIT(human, TRAIT_NEGATES_GRAVITY));
            Assert.False(HAS_TRAIT(human, TRAIT_IGNORING_GRAVITY));
            Assert.False(HAS_TRAIT(human, TRAIT_IGNORESLOWDOWN));
            Wind(human);
        });
        Assert.Equal(5, room.In(() => X(human)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void UnderTheFlyAndTheGodmodeOtherSlowdownsDoNotCountAndOffTheyCountAgain()
    {
        var room = new MedRoom(seed: 922);
        var human = room.Human(5, 5);
        room.Step(2);
        float baseline = room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        float Slowdown() => room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        // the stagger's slowdown (staggered.dm: /datum/movespeed_modifier/staggered, no IGNORE_NOSLOW)
        room.In(() => Waiting.NoSleep(human.add_movespeed_modifier(typeof(datum_movespeed_modifier_staggered))));
        float slowed = Slowdown();
        Assert.True(slowed > baseline);

        room.In(() => DevCommands.Fly(human));
        Assert.Equal(baseline, Slowdown()); // walk/run and the config speed are IGNORE_NOSLOW: they stay
        room.In(() => DevCommands.GodMode(human));
        Assert.Equal(baseline, Slowdown());
        room.In(() => DevCommands.Fly(human)); // fly off, godmode on: still ignored
        Assert.True(room.In(() => HAS_TRAIT_FROM(human, TRAIT_IGNORESLOWDOWN, ADMIN_TRAIT)));
        Assert.Equal(baseline, Slowdown());
        room.In(() => DevCommands.GodMode(human)); // both off
        Assert.False(room.In(() => HAS_TRAIT(human, TRAIT_IGNORESLOWDOWN)));
        Assert.Equal(slowed, Slowdown());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void UnderTheDevGodmodeStunsStaggerDazeAndFireDoNotApplyAndOffTheyDo()
    {
        var room = new MedRoom(seed: 923);
        var human = room.Human(5, 5);
        void Hit()
        {
            Waiting.NoSleep(human.Stun(5 * SECONDS));
            Waiting.NoSleep(human.Knockdown(5 * SECONDS));
            Waiting.NoSleep(human.Unconscious(5 * SECONDS));
            Waiting.NoSleep(human.Sleeping(5 * SECONDS));
            Waiting.NoSleep(human.apply_status_effect(typeof(datum_status_effect_staggered)));
            Waiting.NoSleep(human.apply_status_effect(typeof(datum_status_effect_dazed)));
            Waiting.NoSleep(human.adjust_fire_stacks(5));
            human.ignite_mob();
        }
        room.In(() =>
        {
            Assert.Contains("godmode on", DevCommands.GodMode(human));
            foreach (object trait in Dm.each(DevCommands.GodmodeTraits()))
                Assert.True(HAS_TRAIT(human, (string)trait), Dm.str(trait));
            Hit();
            Assert.DoesNotContain(Dm.each<datum_status_effect>(human.status_effects), e => e is not datum_status_effect_fire_handler);
            Assert.False(Dm.truthy(human.on_fire)); // TRAIT_NOFIRE: fire_stacks ignite (fire_stacks.dm:245)
            Assert.Equal(STANDING_UP, human.body_position);

            Assert.Contains("godmode off", DevCommands.GodMode(human));
            foreach (object trait in Dm.each(DevCommands.GodmodeTraits()))
                Assert.False(HAS_TRAIT(human, (string)trait), Dm.str(trait));
            Hit();
            Assert.True(Dm.truthy(human.IsStun()));
            Assert.True(Dm.truthy(human.has_status_effect(typeof(datum_status_effect_staggered))));
            Assert.True(Dm.truthy(human.on_fire));
        });
    }
}
