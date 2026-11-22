using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Medical.T1;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.DevEffects;

/// <summary>
/// The owner (2026-10-15): the console's full heal heals everything, the status effects and the movespeed modifiers they
/// leave too. The dev <c>fullheal</c> is tg's admin heal without the revival, <c>fully_heal(ADMIN_HEAL_ALL)</c>; each status
/// effect with tg's <c>remove_on_fullheal</c> goes on <c>COMSIG_LIVING_POST_FULLY_HEAL</c> (_status_effect.dm:187) and its
/// <c>on_remove</c> takes its modifier and traits. tg keeps the effects without it (here: <c>tox_vomit</c>, a grouped
/// blindness from another source).
/// </summary>
public class DevFullHealTests
{
    private static List<string> Mods(mob_living m) => Dm.each(m.movespeed_modification).Select(k => Dm.str(k)).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static List<string> Traits(mob_living m) => Dm.each(m._status_traits).Select(k => Dm.str(k)).OrderBy(x => x, StringComparer.Ordinal).ToList();

    [Fact]
    public void TheDevFullHealRemovesTgsFullHealEffectsWithTheirSlowdownsAndTraitsAndKeepsTheOthers()
    {
        var room = new MedRoom(seed: 911);
        var human = room.Human(5, 5);
        room.Step(4);
        List<string> mods = room.In(() => Mods(human));
        List<string> traits = room.In(() => Traits(human));
        float slowdown = room.In(() => Dm.num(human.cached_multiplicative_slowdown));
        room.In(() =>
        {
            // what tg's fullheal removes: the stun family (HEAL_CC_STATUS), the stagger with its slowdown, the temporary blindness
            foreach (string path in new[] { "incapacitating/stun", "incapacitating/knockdown", "incapacitating/paralyzed", "incapacitating/immobilized", "incapacitating/unconscious", "incapacitating/sleeping", "staggered", "temporary_blindness" })
                Assert.Contains("applied", DevCommands.AddEffect(human, DevCommands.FindStatusEffect(path, out _), 30));
            Waiting.NoSleep(human.adjust_fire_stacks(5));
            // and what it keeps (no remove_on_fullheal: debuffs/tox_vomit.dm, vision/blindness.dm grouped)
            Assert.Contains("applied", DevCommands.AddEffect(human, typeof(datum_status_effect_tox_vomit)));
            Assert.Contains("applied", DevCommands.AddEffect(human, typeof(datum_status_effect_grouped_blindness)));
            Waiting.NoSleep(human.adjust_brute_loss(40));
        });
        room.Step(4);
        room.In(() =>
        {
            Assert.Contains("/datum/movespeed_modifier/staggered", Mods(human));
            Assert.True(Dm.num(human.cached_multiplicative_slowdown) > slowdown);
            Assert.Equal(LYING_DOWN, human.body_position);
            Assert.Contains(TRAIT_INCAPACITATED, Traits(human));

            Assert.Contains("fully healed", DevCommands.FullHeal(human));

            var left = Dm.each<datum_status_effect>(human.status_effects).Select(e => e.GetType()).ToList();
            Assert.Equal(new[] { typeof(datum_status_effect_tox_vomit), typeof(datum_status_effect_grouped_blindness) }.OrderBy(t => t.Name), left.OrderBy(t => t.Name));
            Assert.Equal(100f, human.health);
            Assert.Equal(0f, Dm.num(human.fire_stacks));
        });
        // the blindness from the admin source stays (tg's); removing it with the dev command returns the body to its start
        room.In(() =>
        {
            Assert.Contains("removed", DevCommands.RemoveEffect(human, typeof(datum_status_effect_grouped_blindness)));
            Assert.Contains("removed", DevCommands.RemoveEffect(human, typeof(datum_status_effect_tox_vomit)));
        });
        room.Step(2 * MedRoom.TicksPerSecond); // tg's get_up takes a second (living.dm get_up: do_after 1 SECONDS)
        room.In(() =>
        {
            Assert.Empty(Dm.each<datum_status_effect>(human.status_effects));
            Assert.Equal(STANDING_UP, human.body_position); // the floored trait gone: tg's on_floored_end, get_up (living.dm:2737)
            Assert.Equal(mods, Mods(human));
            Assert.Equal(slowdown, Dm.num(human.cached_multiplicative_slowdown));
            Assert.Equal(traits, Traits(human));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheDevFullHealIsTgsAdminHealItFeedsTheMob()
    {
        // ADMIN_HEAL_ALL holds HEAL_ADMIN (the nutrition, living.dm:992) and HEAL_RESTRAINTS (the cuffs, carbon.dm:702), which
        // HEAL_ALL leaves out (mobs.dm:1035); the cuffs (set_handcuffed) are not ported yet, the nutrition is
        var room = new MedRoom(seed: 912);
        var human = room.Human(5, 5);
        room.In(() =>
        {
            Waiting.NoSleep(human.set_nutrition(50));
            Waiting.NoSleep(human.fully_heal(HEAL_ALL));
            Assert.Equal(50f, Dm.num(human.nutrition)); // tg's HEAL_ALL does not feed
            DevCommands.FullHeal(human);
            Assert.Equal(NUTRITION_LEVEL_FED + 50, Dm.num(human.nutrition));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheEffectCommandsApplyAndRemoveThroughTgsProcsAndListTheLiveTypes()
    {
        var room = new MedRoom(seed: 913);
        var human = room.Human(5, 5);
        room.In(() =>
        {
            List<Type> live = DevCommands.LiveStatusEffects();
            Assert.Contains(typeof(datum_status_effect_incapacitating_stun), live);
            Assert.Contains(typeof(datum_status_effect_staggered), live);
            Assert.DoesNotContain(typeof(datum_status_effect_incapacitating), live); // abstract (STATUS_EFFECT_ID_ABSTRACT)
            Assert.DoesNotContain(typeof(datum_status_effect_grouped), live);
            Assert.All(live, t => Assert.False(InertSeam.IsInertType(t)));
            Assert.DoesNotContain(live, t => DmTypes.PathOf(t).Contains("unit_test", StringComparison.Ordinal));

            // the names: a full path, a path below /datum/status_effect/, a unique id
            Assert.Equal(typeof(datum_status_effect_incapacitating_stun), DevCommands.FindStatusEffect("/datum/status_effect/incapacitating/stun", out _));
            Assert.Equal(typeof(datum_status_effect_incapacitating_stun), DevCommands.FindStatusEffect("incapacitating/stun", out _));
            Assert.Equal(typeof(datum_status_effect_staggered), DevCommands.FindStatusEffect("staggered", out _));
            Assert.Null(DevCommands.FindStatusEffect("incapacitating", out string problem));
            Assert.Contains("no status effect", problem);

            // a duration in seconds for an effect whose on_creation takes one (tg's Stun(amount): deciseconds)
            Assert.Contains("stun: 7 s left", DevCommands.AddEffect(human, typeof(datum_status_effect_incapacitating_stun), 7));
            Assert.Equal(70f, Dm.num(((datum_status_effect)human.has_status_effect(typeof(datum_status_effect_incapacitating_stun))).duration));
            Assert.True(Dm.truthy(human.IsStun()));
            // a grouped effect gets the admin source
            Assert.Contains("from admin", DevCommands.AddEffect(human, typeof(datum_status_effect_grouped_blindness)));
            Assert.True(Dm.truthy(human.is_blind()));
            string list = DevCommands.Effects(human);
            Assert.Contains("incapacitating/stun", list);
            Assert.Contains("grouped/blindness", list);

            Assert.Contains("removed", DevCommands.RemoveEffect(human, typeof(datum_status_effect_incapacitating_stun)));
            Assert.False(Dm.truthy(human.IsStun()));
            Assert.Contains("was not on", DevCommands.RemoveEffect(human, typeof(datum_status_effect_incapacitating_stun)));
            Assert.Contains("removed", DevCommands.RemoveEffect(human, typeof(datum_status_effect_grouped_blindness)));
            Assert.False(Dm.truthy(human.is_blind()));
            Assert.Contains("no status effects", DevCommands.Effects(human));
        });
        // the duration counts down in tg's process
        room.In(() => DevCommands.AddEffect(human, typeof(datum_status_effect_staggered), 2));
        room.Step(3 * MedRoom.TicksPerSecond);
        room.In(() => Assert.Null(human.has_status_effect(typeof(datum_status_effect_staggered))));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void MobInfoListsTheEffectsByTypeAndTheLiveTypes()
    {
        var room = new MedRoom(seed: 914);
        var human = room.Human(5, 5);
        room.In(() =>
        {
            DevCommands.AddEffect(human, typeof(datum_status_effect_incapacitating_knockdown), 5);
            List<DevCommands.InfoRow> rows = DevCommands.MobInfo(human);
            DevCommands.InfoRow effect = Assert.Single(rows, r => r.Section == "Status effects" && r.Key == "/datum/status_effect/incapacitating/knockdown");
            Assert.Equal("knockdown: 5 s left", effect.Value);
            DevCommands.InfoRow types = Assert.Single(rows, r => r.Section == "Effect types");
            Assert.Contains("incapacitating/knockdown", types.Value.Split(' '));
            Assert.Contains(rows, r => r.Section == "Movement" && r.Key == "modifiers" && r.Value.Contains("carbon_crawling", StringComparison.Ordinal));
        });
        room.AssertNoRuntimes();
    }
}
