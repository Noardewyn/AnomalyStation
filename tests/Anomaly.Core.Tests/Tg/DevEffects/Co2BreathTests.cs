using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Medical.T1;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.DevEffects;

/// <summary>
/// The owner's play test (2026-10-15): "the CO2 debuff never goes away, even after a fullheal". Written from tg's DM: the
/// lungs' <c>too_much_co2</c> (_lungs.dm:316) counts from the first bad breath (<c>co2overloadtime</c>), after 12 s each bad
/// breath throws the alert, knocks the mob out for 6 s (<c>Unconscious</c>) and deals 3 oxygen damage, after 30 s 8 more;
/// <c>safe_co2</c> (:342) resets the counter and the alert at the first breath without too much CO2 (the gas gone from the
/// breath, check_breath :723, or below <c>safe_co2_max</c>, :318 BREATH_LOST). <c>fully_heal</c> heals the damage and removes
/// the knockout (incapacitating effects: <c>remove_on_fullheal</c>, <c>HEAL_CC_STATUS</c>) but keeps the lungs (tg's
/// <c>regenerate_organs</c> with <c>replace_current = FALSE</c>, _organ.dm:346) and so their counter: in bad air the effects
/// come back at the next breath, as in tg, and stop only when the air is clean.
/// </summary>
public class Co2BreathTests
{
    private const int Fire = 2 * MedRoom.TicksPerSecond; // SSmobs: 2 s
    private const float R = 8.31f, T = 293.15f, CellVolume = 2500f;

    // 30 kPa of carbon dioxide in every open turf of the room (safe_co2_max is 10 kPa)
    private static void Co2Room(MedRoom room, float kpa)
        => room.In(() => MedRoom.RoomAir(air => air.set_gas(typeof(datum_gas_carbon_dioxide), kpa * CellVolume / (R * T))));

    private static bool Alert(mob_living_carbon_human h) => Dm.truthy(h.alerts?[ALERT_TOO_MUCH_CO2]);

    private static bool Out(mob_living_carbon_human h) => Dm.truthy(h.has_status_effect(typeof(datum_status_effect_incapacitating_unconscious)));

    [Fact]
    public void TheCo2EffectsLastWhileTheBadAirIsBreathedAndEndAtTheFirstCleanBreath()
    {
        var room = new MedRoom(seed: 901);
        var human = room.Human(5, 5);
        Co2Room(room, 30f);
        var rec = room.Record(human);
        // within ~20 s the overload passes 12 s: the alert, the knockout and the damage (_lungs.dm:331-336)
        room.StepUntil(() => Alert(human) && Out(human), 30 * MedRoom.TicksPerSecond);
        room.In(() =>
        {
            Assert.NotNull(human.co2overloadtime);
            Assert.True(MedRoom.Oxy(human) > 0);
        });

        // clean air: the next breath has no carbon dioxide over safe_co2_max, safe_co2 resets everything
        room.In(() => MedRoom.RoomAir(air => air.set_gas(typeof(datum_gas_carbon_dioxide), 0)));
        int breaths = rec.Breaths.Count;
        room.StepUntil(() => rec.Breaths.Count > breaths, 10 * Fire);
        room.In(() =>
        {
            Assert.Equal(0f, Dm.num(human.co2overloadtime));
            Assert.False(Alert(human));
        });
        // and nothing comes back: no knockout after the last one runs out, the oxygen damage heals (5 per good breath)
        room.Step(20 * Fire);
        room.In(() =>
        {
            Assert.False(Out(human));
            Assert.False(Alert(human));
            Assert.Equal(0f, MedRoom.Oxy(human));
            Assert.Equal(STABLE, human.stat);
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AFullHealInBadAirHealsAndWakesTheMobButTgsLungsKeepTheirCounterUntilTheAirIsClean()
    {
        var room = new MedRoom(seed: 902);
        var human = room.Human(5, 5);
        Co2Room(room, 30f);
        var rec = room.Record(human);
        room.StepUntil(() => Alert(human) && Out(human), 30 * MedRoom.TicksPerSecond);
        object counter = room.In(() => human.co2overloadtime);

        room.In(() => DevCommands.FullHeal(human));
        room.In(() =>
        {
            Assert.Equal(0f, MedRoom.Oxy(human));
            Assert.False(Out(human)); // the knockout is an incapacitating effect: remove_on_fullheal, HEAL_CC_STATUS (debuffs.dm:16)
            Assert.Empty(Dm.each<datum_status_effect>(human.status_effects));
            // tg's fully_heal does not touch the lungs' counter or the alert (living.dm:977, carbon.dm:673)
            Assert.True(Dm.eq(counter, human.co2overloadtime));
            Assert.True(Alert(human));
        });

        // still in the bad air: the next breath is past 12 s of overload at once (tg's), the effects are back
        int breaths = rec.Breaths.Count;
        room.StepUntil(() => rec.Breaths.Count > breaths, 10 * Fire);
        room.In(() =>
        {
            Assert.True(Out(human));
            Assert.True(MedRoom.Oxy(human) > 0);
        });

        // the air cleaned: they end for good
        room.In(() => MedRoom.RoomAir(air => air.set_gas(typeof(datum_gas_carbon_dioxide), 0)));
        room.Step(24 * Fire);
        room.In(() =>
        {
            Assert.Equal(0f, Dm.num(human.co2overloadtime));
            Assert.False(Alert(human));
            Assert.False(Out(human));
            Assert.Equal(0f, MedRoom.Oxy(human));
        });
        room.AssertNoRuntimes();
    }

    [Fact]
    public void UnderTheDevGodmodeCarbonDioxideDoesNothing()
    {
        // tg's check_breath returns at once under TRAIT_GODMODE (_lungs.dm:640)
        var room = new MedRoom(seed: 903);
        var human = room.Human(5, 5);
        room.In(() => DevCommands.GodMode(human));
        Co2Room(room, 30f);
        room.Step(40 * MedRoom.TicksPerSecond);
        room.In(() =>
        {
            Assert.Null(human.co2overloadtime);
            Assert.False(Alert(human));
            Assert.Empty(Dm.each<datum_status_effect>(human.status_effects));
            Assert.Equal(0f, MedRoom.Oxy(human));
        });
        room.AssertNoRuntimes();
    }
}
