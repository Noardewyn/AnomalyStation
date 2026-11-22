using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Integration;

/// <summary>
/// The leaves a click could still reach in M2 (M2 T1's open issue 3), in a world where a reached <c>Dm.NotTranslated</c>
/// throws (T2's room): a help click on a human in crit performs tg's CPR (human.dm:503, translated); a help click on a dead
/// one stops at the pronoun alert; checking yourself (a help click on yourself with blocked hands, or a click on the
/// health doll) reaches the self-examination report, a deferred leaf (human_defense.dm:559); a combat-mode click on a
/// human reaches the punch, a deferred leaf (melee, M4a); and stamina damage up to the crit threshold puts a carbon in
/// tg's stamina crit (stamcrit.dm, translated) until the stamina heals.
/// </summary>
public class ClickLeafTests
{
    private static void Click(T2Room r, mob_living_carbon_human who, atom what)
    {
        r.Command(() =>
        {
            who.next_click = -1;
            who.next_move = -1;
            ClientSeam.Click(who.client, what, ClientSeam.ClickParams(LEFT_CLICK));
        });
    }

    private static IReadOnlyList<string> Deferred(T2Room r) => DmWorldState.Of(r.World).DeferredSites;

    [Fact]
    public void AHelpClickOnAHumanInCritPerformsCprAndEachRoundHealsSevenOxygenDamage()
    {
        var r = new T2Room(1501);
        mob_living_carbon_human bob = r.Dummy(9, 8, name: "patient");
        mob_living_carbon_human alice = r.Join(8, 8, "Alice", name: "medic");
        var oxy = new List<float>();
        r.In(() =>
        {
            Waiting.NoSleep(bob.adjust_oxy_loss(110));
            Waiting.NoSleep(bob.set_resting(TRUE, TRUE));
            On(bob, COMSIG_LIVING_ADJUST_OXY_DAMAGE, "oxy", 0, act: a => oxy.Add(Dm.num(a[2])));
        });
        r.Step(2);
        Assert.True(r.In(() => bob.health <= bob.crit_threshold), "the patient is in crit");
        Assert.Equal(LYING_DOWN, r.In(() => bob.body_position));
        Click(r, alice, bob);
        r.Step(2 * 60 + 10); // two rounds: 3 s, then the panicked 0.8 s (20 ticks per second)
        Assert.Contains(-7f, oxy); // target.adjust_oxy_loss(-min(target.get_oxy_loss(), 7)), human.dm:561
        Assert.True(oxy.Count(v => v == -7f) >= 2, "CPR goes on while the patient stays in crit: " + string.Join(", ", oxy));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHelpClickOnADeadHumanStopsAtTheDeadAlert()
    {
        var r = new T2Room(1502);
        mob_living_carbon_human bob = r.Dummy(9, 8, name: "body");
        mob_living_carbon_human alice = r.Join(8, 8, "Alice", name: "medic");
        r.In(() => Waiting.NoSleep(bob.death()));
        r.Step(2);
        Assert.Equal(DEAD, r.In(() => bob.stat));
        var balloons = new BalloonCapture();
        PresentationSinks.Of(r.World).Messages = balloons;
        Click(r, alice, bob);
        r.Step(70);
        // the pronoun alert is translated since M8 slice D (it was a deferred site, human.dm:516)
        Assert.Contains(balloons.Texts, t => t.EndsWith("dead!", StringComparison.Ordinal));
        Assert.DoesNotContain("code/modules/mob/living/carbon/human/human.dm:516", Deferred(r));
        Assert.Equal(DEAD, r.In(() => bob.stat));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void CheckingYourselfReachesTheReportFromTheBlockedHandsClickAndFromTheHealthDoll()
    {
        // the self-examination report (check_self_for_injuries, human_defense.dm:556) is translated since M6 slice 2
        var r = new T2Room(1503);
        mob_living_carbon_human alice = r.Join(8, 8, "Alice", name: "patient");
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        int Reports() => chat.Html.Count(l => l.Contains("You check yourself for injuries.", StringComparison.Ordinal));
        r.In(() => ADD_TRAIT(alice, TRAIT_HANDS_BLOCKED, "test"));
        r.Command(() => Waiting.NoSleep(alice.UnarmedAttack(alice, TRUE, Dm.list())));
        r.Step(2); // SSchat delivers its queue when it fires
        Assert.Equal(1, Reports());
        atom_movable_screen_healthdoll doll = r.In(() => alice.hud_used.screen_objects[HUD_MOB_HEALTHDOLL] as atom_movable_screen_healthdoll);
        Assert.NotNull(doll);
        r.In(() => REMOVE_TRAIT(alice, TRAIT_HANDS_BLOCKED, "test"));
        Click(r, alice, doll);
        r.Step(2);
        Assert.Equal(2, Reports()); // the doll's click reached the report
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACombatModeClickOnAHumanPunchesWithTgsHarm()
    {
        // M2 left the punch a deferred leaf; the owner's play test (2026-10-09) translated tg's species harm: punches at tg's
        // click cooldown hurt (a miss is tg's own roll, UNARMED_MISS_CHANCE_BASE).
        var r = new T2Room(1504);
        mob_living_carbon_human bob = r.Dummy(9, 8, name: "target");
        mob_living_carbon_human alice = r.Join(8, 8, "Alice", name: "boxer");
        r.In(() => alice.set_combat_mode(TRUE, TRUE));
        r.Step(1);
        for (int i = 0; i < 6; i++)
        {
            Click(r, alice, bob);
            r.Step(20);
        }
        Assert.DoesNotContain("code/modules/mob/living/carbon/human/_species.dm:762", Deferred(r));
        Assert.True(r.In(() => bob.health) < 100f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void StaminaDamageToTheCritThresholdFloorsACarbonUntilItsStaminaHeals()
    {
        var r = new T2Room(1505);
        mob_living_carbon_human bob = r.Dummy(9, 8, name: "tired");
        r.In(() => Waiting.NoSleep(bob.adjust_stamina_loss(99)));
        r.Step(1);
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))));
        r.In(() => Waiting.NoSleep(bob.adjust_stamina_loss(1))); // maxHealth - staminaloss <= crit_threshold (100 - 100 <= 0)
        r.Step(1);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))));
        Assert.Equal(120f, r.In(() => Dm.num(bob.get_stamina_loss()))); // stamcrit.dm:20: 30 more, capped at max_stamina
        Assert.True(r.In(() => HAS_TRAIT_FROM(bob, TRAIT_FLOORED, STAMINA) && HAS_TRAIT_FROM(bob, TRAIT_INCAPACITATED, STAMINA)));
        r.Step(2);
        Assert.Equal(LYING_DOWN, r.In(() => bob.body_position));
        r.In(() => Waiting.NoSleep(bob.set_stamina_loss(0)));
        r.Step(1);
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_incapacitating_stamcrit))));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_FLOORED)));
        r.AssertNoRuntimes();
    }

    private sealed class BalloonCapture : IMessageSink
    {
        public List<string> Texts { get; } = new();
        public void Chat(object target, DmList message) { }
        public void Output(object target, string text) { }
        public void Balloon(object viewer, object atom, string text) => Texts.Add(text);
    }
}
