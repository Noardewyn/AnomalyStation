using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps train 12 (D339, D341): RG3's reagents met VI1's diseases at the merge. The vaccine cures the diseases it names and leaves
/// the host immune (other_reagents.dm:86), the fungal TB vaccine names tuberculosis (:106), the transformation toxins, the fungal
/// spores and Agent-S give their diseases when taken in (:954-1604, :2931; the gluttony toxin not below 1u), and kronkaine purged
/// under stamina damage gives one adrenal crisis (drug_reagents.dm:903-910).
/// </summary>
public class DiseaseReagentsTrain12Tests
{
    private const int W = 9, H = 9;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(W, H, seed: seed, mapExtras: _ =>
        {
            for (int y = 2; y < H; y++)
                for (int x = 2; x < W; x++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
        });
        Listen(r);
        r.In(() =>
        {
            if (SSdisease == null)
                Waiting.NoSleep(Dm.@new<datum_controller_subsystem_disease>().Initialize());
        });
        r.Step(Second);
        return r;
    }

    private static bool Has<T>(mob_living who) where T : datum_disease => Dm.locate<T>(who.diseases) != null;

    private static void Expose(mob_living who, Type reagent, float amount, float methods, DmList data = null)
    {
        var holder = Dm.@new<datum_reagents>(100);
        Waiting.NoSleep(holder.add_reagent(reagent, amount, data));
        Waiting.NoSleep(holder.expose(who, methods));
    }

    [Fact]
    public void TheVaccineCuresWhatItNamesAndLeavesTheHostImmune()
    {
        var r = Room(10380);
        mob_living_carbon_human bob = r.Dummy(4, 4);
        r.In(() =>
        {
            var cold = Dm.@new<datum_disease_cold>();
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.ForceContractDisease(cold, FALSE))));
            object id = cold.GetDiseaseID();
            // a touch does nothing (INGEST or INJECT only)
            Expose(bob, typeof(datum_reagent_vaccine), 5, TOUCH, Dm.list(id));
            Assert.True(Has<datum_disease_cold>(bob));
            Expose(bob, typeof(datum_reagent_vaccine), 5, INJECT, Dm.list(id));
            Assert.False(Has<datum_disease_cold>(bob));
            Assert.True(Dm.@in(id, bob.disease_resistances));
            Assert.False(Dm.truthy(Waiting.NoSleep(bob.ForceContractDisease(Dm.@new<datum_disease_cold>(), FALSE, TRUE))));

            var tb = Dm.@new<datum_reagent_vaccine_fungal_tb>();
            Assert.True(Dm.@in(Dm.text("[]", typeof(datum_disease_tuberculosis)), tb.data));
        });
    }

    [Theory]
    [InlineData(typeof(datum_reagent_aslimetoxin), typeof(datum_disease_transformation_slime))]
    [InlineData(typeof(datum_reagent_gluttonytoxin), typeof(datum_disease_transformation_morph))]
    [InlineData(typeof(datum_reagent_cyborg_mutation_nanomachines), typeof(datum_disease_transformation_robot))]
    [InlineData(typeof(datum_reagent_xenomicrobes), typeof(datum_disease_transformation_xeno))]
    [InlineData(typeof(datum_reagent_fungalspores), typeof(datum_disease_tuberculosis))]
    [InlineData(typeof(datum_reagent_snail), typeof(datum_disease_gastrolosis))]
    [InlineData(typeof(datum_reagent_gondola_mutation_toxin), typeof(datum_disease_transformation_gondola))]
    public void TheDiseaseReagentsInfectWhenTakenIn(Type reagent, Type disease)
    {
        var r = Room(10381);
        mob_living_carbon_human bob = r.Dummy(4, 4);
        r.In(() =>
        {
            if (reagent == typeof(datum_reagent_gluttonytoxin))
            {
                Expose(bob, reagent, 1, INGEST); // tg :968: no microdosing
                Assert.DoesNotContain(Dm.each<datum_disease>(bob.diseases), d => istype(d, disease));
            }
            Expose(bob, reagent, 5, INGEST);
            Assert.Contains(Dm.each<datum_disease>(bob.diseases), d => istype(d, disease));
        });
    }

    [Fact]
    public void KronkainePurgedUnderStaminaDamageGivesOneAdrenalCrisis()
    {
        var r = Room(10382);
        mob_living_carbon_human bob = r.Dummy(4, 4);
        r.In(() =>
        {
            var blood = (datum_reagents)bob.reagents;
            Waiting.NoSleep(blood.add_reagent(typeof(datum_reagent_drug_kronkaine), 10));
            var kronkaine = (datum_reagent)blood.has_reagent(typeof(datum_reagent_drug_kronkaine));
            Waiting.NoSleep(kronkaine.on_mob_life(bob, 1f, 1f));
            Assert.False(Has<datum_disease_adrenal_crisis>(bob)); // no purger, no stamina damage

            Waiting.NoSleep(blood.add_reagent(typeof(datum_reagent_medicine_haloperidol), 5));
            Waiting.NoSleep(kronkaine.on_mob_life(bob, 1f, 1f));
            Assert.False(Has<datum_disease_adrenal_crisis>(bob)); // a purger but no stamina damage

            Waiting.NoSleep(bob.adjust_stamina_loss(60));
            Waiting.NoSleep(kronkaine.on_mob_life(bob, 1f, 1f));
            Assert.True(Has<datum_disease_adrenal_crisis>(bob));
            Waiting.NoSleep(bob.adjust_stamina_loss(60));
            Waiting.NoSleep(kronkaine.on_mob_life(bob, 1f, 1f));
            Assert.Single(Dm.each<datum_disease_adrenal_crisis>(bob.diseases));
        });
    }
}
