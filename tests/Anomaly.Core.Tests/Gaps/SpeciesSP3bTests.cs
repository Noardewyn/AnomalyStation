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
/// Gaps package SP3b (D349): the abductor, the dullahan, the vampire, the spirit and the ghost, and the nightmare (the shadowperson's
/// aggressive subtype). Each gets its organs, limbs and traits from the species; the vampire bleeds out of a coffin and drinks, the dullahan
/// carries his head, the ghost toggles passthrough, the nightmare grows an armblade and Shadow Walk.
/// </summary>
public class SpeciesSP3bTests
{
    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => (get_area(locate(4, 4, 1)) as area).requires_power = FALSE);
        r.Step(Second);
        return (r, bob);
    }

    [Fact]
    public void An_abductor_has_no_heart_or_lungs_chunky_hands_and_the_grey_brain()
    {
        var (r, bob) = Room(9951);
        r.In(() => bob.set_species(typeof(datum_species_abductor)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_abductor>(bob.dna.species);
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBLOOD));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOHUNGER));
            Assert.True(HAS_TRAIT(bob, TRAIT_CHUNKYFINGERS));
            Assert.True(HAS_TRAIT(bob, TRAIT_ABDUCTOR_KNOWLEDGE));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_organ_brain_abductor>(bob.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.IsType<obj_item_organ_tongue_abductor>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.IsType<obj_item_bodypart_chest_abductor>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.NotNull(((obj_item_bodypart_chest_abductor)bob.get_bodypart(BODY_ZONE_CHEST)).get_butt_sprite());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_vampire_bleeds_outside_a_coffin_and_has_fangs_that_grant_the_drain_action()
    {
        var (r, bob) = Room(9952);
        r.In(() => bob.set_species(typeof(datum_species_human_vampire)));
        r.Step(2);
        float before = 0;
        r.In(() =>
        {
            Assert.Contains(Dm.each_cast<obj_item_organ>(bob.organs), o => o is obj_item_organ_fangs_vampire);
            Assert.True(HAS_TRAIT(bob, TRAIT_DRINKS_BLOOD));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOHUNGER));
            Assert.IsType<obj_item_organ_heart_vampire>(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.Equal("albino", bob.skin_tone);
            before = Dm.num(bob.get_blood_volume());
        });
        r.Step(5 * Second);
        r.In(() => Assert.True(Dm.num(bob.get_blood_volume()) < before));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dullahan_carries_his_head_in_his_hands_and_gets_it_back_when_he_changes_species()
    {
        var (r, bob) = Room(9953);
        r.In(() => bob.set_species(typeof(datum_species_dullahan)));
        r.Step(2);
        r.In(() =>
        {
            var dullahan = (datum_species_dullahan)bob.dna.species;
            Assert.NotNull(dullahan.my_head);
            Assert.True(HAS_TRAIT(bob, TRAIT_BRAINLESS_CARBON));
            Assert.IsType<obj_item_bodypart_head_dullahan>(dullahan.my_head.loc);
            Assert.Null(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.True(dullahan.my_head.owner != null, "owner null; deleted=" + QDELETED(dullahan.my_head) + " loc=" + dullahan.my_head.loc + " bobDeleted=" + QDELETED(bob));
            Assert.Same(bob, dullahan.my_head.owner);
            Assert.Same(bob, dullahan.my_head.get_listening_mob());
        });
        r.In(() => bob.set_species(typeof(datum_species_human)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.False(Dm.truthy(bob.dna.species is datum_species_dullahan));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_ghost_toggles_passthrough_and_the_spirit_floats_without_it()
    {
        var (r, bob) = Room(9954);
        r.In(() => bob.set_species(typeof(datum_species_spirit)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_GHOSTLY_MOB));
            Assert.True(HAS_TRAIT(bob, TRAIT_MOVE_FLYING));
            Assert.IsType<obj_item_bodypart_chest_ghost_spirit>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.Null(bob.get_bodypart(BODY_ZONE_L_LEG));
            bob.set_species(typeof(datum_species_spirit_ghost));
        });
        r.Step(2);
        r.In(() =>
        {
            var ghost = (datum_species_spirit_ghost)bob.dna.species;
            Assert.NotNull(ghost.passthrough_ability);
            Assert.False(HAS_TRAIT(bob, TRAIT_MOVE_PHASING));
            Waiting.NoSleep(ghost.passthrough_ability.swap_mode());
            Assert.True(HAS_TRAIT(bob, TRAIT_MOVE_PHASING));
            Assert.True(HAS_TRAIT(bob, TRAIT_HANDS_BLOCKED));
            Waiting.NoSleep(ghost.passthrough_ability.swap_mode());
            Assert.False(HAS_TRAIT(bob, TRAIT_MOVE_PHASING));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_nightmare_has_the_heart_of_darkness_an_armblade_and_shadow_walk()
    {
        var (r, bob) = Room(9955);
        r.In(() => bob.set_species(typeof(datum_species_shadow_nightmare)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_shadow_nightmare>(bob.dna.species);
            var heart = Assert.IsType<obj_item_organ_heart_nightmare>(bob.get_organ_slot(ORGAN_SLOT_HEART));
            var brain = Assert.IsType<obj_item_organ_brain_shadow_nightmare>(bob.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.NotNull(heart.blade_item);
            Assert.Contains(heart.blade_item, Dm.each_cast<obj_item>(bob.held_items).ToList());
            Assert.NotNull(brain.our_jaunt);
            Assert.True(HAS_TRAIT(bob, TRAIT_LIGHT_DRINKER));
            Assert.False(Dm.truthy(heart.Stop()));
        });
        r.Step(2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_shadowperson_has_the_nyxosynthesis_effect_on_his_limbs()
    {
        var (r, bob) = Room(9956);
        r.In(() => bob.set_species(typeof(datum_species_shadow)));
        r.Step(2);
        r.In(() => Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_grouped_bodypart_effect_nyxosynthesis))));
        r.AssertNoRuntimes();
    }
}
