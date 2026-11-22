using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8;

/// <summary>
/// M8 slice A: the reagent holder's metabolism in a body (holder/mob_life.dm, reagents.dm, _liver.dm, _stomach.dm). Every
/// number is tg's: a reagent metabolizes metabolization_rate * metabolism_efficiency * seconds_per_tick a Life tick (water
/// 0.2 * 1 * 2), an overdose starts when the volume reaches overdose_threshold and ends with the reagent, a body without a liver
/// keeps its non-self-consuming reagents and takes 0.6 toxin a second, and the stomach drips
/// min((round(0.05 * volume, 0.05) + max(rate, 0.25)) * seconds_per_tick, volume) into the blood.
/// </summary>
public class MetabolismTests
{
    private const int Second = 20;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(9, 9, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= 8; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        return r;
    }

    private static datum_reagents Blood(mob_living_carbon who) => (datum_reagents)who.reagents;

    private static float Amount(datum_reagents holder, Type reagent) => Dm.num(holder.get_reagent_amount(reagent));

    [Fact]
    public void WaterInTheBloodMetabolizesAtTgsRate()
    {
        var r = Room(8101);
        mob_living_carbon_human dummy = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_water), 10));
            var liver = (obj_item_organ_liver)dummy.get_organ_slot(ORGAN_SLOT_LIVER);
            Waiting.NoSleep(liver.on_life(SSMOBS_DT()));
            Assert.Equal(2f, SSMOBS_DT());
            Assert.Equal(9.6f, Amount(Blood(dummy), typeof(datum_reagent_water)), 3);
            var water = (datum_reagent)Blood(dummy).has_reagent(typeof(datum_reagent_water));
            Assert.Equal(TRUE, water.metabolizing);
            Assert.Equal(1f, water.current_cycle);
            for (int i = 0; i < 24; i++)
                Waiting.NoSleep(liver.on_life(SSMOBS_DT()));
            Assert.Equal(FALSE, Blood(dummy).has_reagent(typeof(datum_reagent_water)));
            Assert.Equal(0f, Blood(dummy).total_volume);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOverdoseStartsAtTheThresholdAndEndsWithTheReagent()
    {
        var r = Room(8102);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        mob_living_carbon_human dummy = r.Dummy(4, 4);
        r.In(() =>
        {
            // rat spit: overdose_threshold 20, metabolization_rate 0.006 (regal_rat_actions.dm:160), the base reagent's effects
            Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_rat_spit), 19));
            Waiting.NoSleep(Blood(dummy).metabolize(dummy, SSMOBS_DT(), can_overdose: TRUE));
            var spit = (datum_reagent)Blood(dummy).has_reagent(typeof(datum_reagent_rat_spit));
            Assert.Equal(FALSE, spit.overdosed);

            Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_rat_spit), 2));
            Waiting.NoSleep(Blood(dummy).metabolize(dummy, SSMOBS_DT(), can_overdose: TRUE));
            Assert.Equal(TRUE, spit.overdosed);
            Assert.Single(chat.Html, h => h.Contains("You feel like you took too much of Rat Spit!", StringComparison.Ordinal));
            // no second message while it lasts
            Waiting.NoSleep(Blood(dummy).metabolize(dummy, SSMOBS_DT(), can_overdose: TRUE));
            Assert.Single(chat.Html, h => h.Contains("took too much", StringComparison.Ordinal));

            // purged: the reagent ends its metabolization and leaves; a new dose under the threshold is no overdose
            Waiting.NoSleep(Blood(dummy).remove_reagent(typeof(datum_reagent_rat_spit), 100));
            Assert.Equal(FALSE, spit.metabolizing);
            Assert.True(QDELETED(spit));
            Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_rat_spit), 5));
            Waiting.NoSleep(Blood(dummy).metabolize(dummy, SSMOBS_DT(), can_overdose: TRUE));
            Assert.Equal(FALSE, ((datum_reagent)Blood(dummy).has_reagent(typeof(datum_reagent_rat_spit))).overdosed);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABodyWithoutALiverTakesTgsLiverlessPath()
    {
        var r = Room(8103);
        mob_living_carbon_human dummy = r.Dummy(4, 4);
        r.In(() =>
        {
            var liver = (obj_item_organ_liver)dummy.get_organ_slot(ORGAN_SLOT_LIVER);
            Waiting.NoSleep(liver.Remove(dummy));
            Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_water), 10));
            float tox = Dm.num(dummy.get_tox_loss());
            Waiting.NoSleep(dummy.handle_liver(SSMOBS_DT()));
            // water is not self_consuming: liverless, it is not metabolized (mob_life.dm:112), although compute_metabolization
            // has marked it metabolizing again after end_metabolization (tg's order: :105 before :112)
            Assert.Equal(10f, Amount(Blood(dummy), typeof(datum_reagent_water)), 3);
            Assert.Equal(0f, ((datum_reagent)Blood(dummy).has_reagent(typeof(datum_reagent_water))).current_cycle);
            // adjust_tox_loss(0.6 * seconds_per_tick, forced = TRUE) (carbon/life.dm:745)
            Assert.Equal(tox + 1.2f, Dm.num(dummy.get_tox_loss()), 3);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheStomachDripsAnIngestedReagentIntoTheBloodAtTgsRate()
    {
        var r = Room(8104);
        var chat = new ChatCapture();
        PresentationSinks.Of(r.World).Messages = chat;
        mob_living_carbon_human dummy = r.Dummy(4, 4);
        r.Step(6 * Second); // a taste needs world.time past last_taste_time + 50 (taste.dm:59), and the room starts at 0
        r.In(() =>
        {
            var cup = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(cup.add_reagent(typeof(datum_reagent_water), 10));
            Waiting.NoSleep(cup.trans_to(dummy, 10, methods: INGEST));
            var stomach = (obj_item_organ_stomach)dummy.get_organ_slot(ORGAN_SLOT_STOMACH);
            var belly = (datum_reagents)stomach.reagents;
            Assert.Equal(10f, Amount(belly, typeof(datum_reagent_water)), 3);
            Assert.Equal(0f, Amount(Blood(dummy), typeof(datum_reagent_water)), 3);
            Assert.Contains(chat.Html, h => h.Contains("You can taste water.", StringComparison.Ordinal));

            // (round(0.05 * 10, 0.05) + max(0.2, 0.25)) * 2 = 1.5
            Waiting.NoSleep(stomach.on_life(SSMOBS_DT()));
            Assert.Equal(8.5f, Amount(belly, typeof(datum_reagent_water)), 3);
            Assert.Equal(1.5f, Amount(Blood(dummy), typeof(datum_reagent_water)), 3);
            Assert.Equal(TRUE, dummy.has_reagent(typeof(datum_reagent_water)) is datum_reagent ? TRUE : FALSE);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LifeMetabolizesTheBloodAndTheStomachEveryTick()
    {
        var r = Room(8105);
        mob_living_carbon_human dummy = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_water), 10));
            var cup = Dm.@new<datum_reagents>(50);
            Waiting.NoSleep(cup.add_reagent(typeof(datum_reagent_fuel), 5));
            Waiting.NoSleep(cup.trans_to(dummy, 5, methods: INGEST));
        });
        r.Step(20 * Second);
        r.In(() =>
        {
            // ten Life ticks of 0.4: the water is metabolized away, the fuel dripped from the stomach and burns as toxin
            Assert.True(Amount(Blood(dummy), typeof(datum_reagent_water)) < 10f);
            var belly = (datum_reagents)((obj_item_organ_stomach)dummy.get_organ_slot(ORGAN_SLOT_STOMACH)).reagents;
            Assert.True(Amount(belly, typeof(datum_reagent_fuel)) < 5f);
            Assert.True(Dm.num(dummy.get_tox_loss()) > 0);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MetabolismIsDeterministic()
    {
        static string Run()
        {
            var r = Room(8106);
            mob_living_carbon_human dummy = r.Dummy(4, 4);
            r.In(() =>
            {
                Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_water), 10));
                Waiting.NoSleep(Blood(dummy).add_reagent(typeof(datum_reagent_rat_spit), 25));
                var cup = Dm.@new<datum_reagents>(50);
                Waiting.NoSleep(cup.add_reagent(typeof(datum_reagent_fuel), 5));
                Waiting.NoSleep(cup.trans_to(dummy, 5, methods: INGEST));
            });
            r.Step(30 * Second);
            r.AssertNoRuntimes();
            return r.In(() => r.World.ComputeStateHash().ToString() + " " + Dm.text("[]", Blood(dummy).get_reagent_log_string()));
        }

        Assert.Equal(Run(), Run());
    }
}
