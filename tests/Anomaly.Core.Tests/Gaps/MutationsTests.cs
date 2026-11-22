using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package P3 (D327): the mutations. Every mutation type is made by SSatoms (every human carries the gene blocks), gained and
/// lost on a human with no runtime; a sample of their effects: the traits and the body (dwarfism, gigantism, the hulk's skin and
/// strength), the light of glowy, the head H.A.R.S. takes and gives back, the physiology of hypermetabolic blood, the spells the
/// power mutations grant, the chemical allergy's health scan line, the venomous strikes' element and the speech mutations.
/// </summary>
public class MutationsTests
{
    private static (AudioRoom R, mob_living_carbon_human Subject) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        Listen(r);
        mob_living_carbon_human subject = r.Dummy(5, 4);
        r.Step(Second);
        return (r, subject);
    }

    private static datum_mutation Gain(AudioRoom r, mob_living_carbon_human who, Type mutation) => r.In(() =>
    {
        Waiting.NoSleep(who.dna.add_mutation(mutation, MUTATION_SOURCE_ACTIVATED));
        return who.dna.get_mutation(mutation) as datum_mutation;
    });

    private static void Lose(AudioRoom r, mob_living_carbon_human who, Type mutation) =>
        r.In(() => Waiting.NoSleep(who.dna.remove_mutation(mutation, MUTATION_SOURCE_ACTIVATED)));

    [Fact]
    public void Every_mutation_is_made_at_the_start_and_carried_in_every_humans_genes()
    {
        var (r, subject) = Room(9301);
        Assert.Equal(r.In(() => subtypesof(typeof(datum_mutation)).len), r.In(() => GLOB.all_mutations.len));
        Assert.Equal((float)DNA_MUTATION_BLOCKS, r.In(() => subject.dna.mutation_index.len));
        foreach (object path in r.In(() => Dm.each(subject.dna.mutation_index).ToList()))
            Assert.Equal(r.In(() => Dm.str(GET_SEQUENCE(path)).Length), r.In(() => Dm.str(subject.dna.mutation_index[path]).Length));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Every_mutation_is_gained_lived_with_and_lost_with_no_runtimes()
    {
        var r = new AudioRoom(11, 9, seed: 9302);
        Listen(r);
        // the ones that end or remake the body are left to their own tests: monkified, unstable DNA (random mutations), martyrdom
        var skip = new HashSet<Type> { typeof(datum_mutation_race), typeof(datum_mutation_bad_dna), typeof(datum_mutation_martyrdom) };
        List<Type> all = r.In(() => Dm.each(subtypesof(typeof(datum_mutation))).Cast<Type>().ToList());
        int x = 2;
        foreach (Type mutation in all.Where(t => !skip.Contains(t)))
        {
            mob_living_carbon_human subject = r.Dummy(x, 2);
            x = x == 9 ? 2 : x + 1;
            r.Step(2);
            datum_mutation gained = Gain(r, subject, mutation);
            Assert.True(gained != null, mutation.Name + " not gained: " + string.Join("\n", r.Room.RuntimeText));
            Assert.Same(subject, r.In(() => gained.owner));
            r.Step(3 * Second); // a few life ticks with it
            Lose(r, subject, mutation);
            Assert.Null(r.In(() => subject.dna.get_mutation(mutation)));
            r.In(() => qdel(subject));
            r.Step(2);
            Assert.True(r.Room.Runtimes.Count == 0, mutation.Name + ": " + string.Join("\n", r.Room.RuntimeText));
        }
    }

    [Fact]
    public void Body_mutations_change_size_traits_and_skin()
    {
        var (r, subject) = Room(9303);
        Gain(r, subject, typeof(datum_mutation_dwarfism));
        Assert.True(r.In(() => HAS_TRAIT(subject, TRAIT_DWARF)));
        Lose(r, subject, typeof(datum_mutation_dwarfism));
        Assert.False(r.In(() => HAS_TRAIT(subject, TRAIT_DWARF)));

        Gain(r, subject, typeof(datum_mutation_gigantism));
        Assert.True(r.In(() => HAS_TRAIT(subject, TRAIT_GIANT)));
        Lose(r, subject, typeof(datum_mutation_gigantism));
        Assert.False(r.In(() => HAS_TRAIT(subject, TRAIT_GIANT)));

        Gain(r, subject, typeof(datum_mutation_hulk));
        Assert.True(r.In(() => HAS_TRAIT(subject, TRAIT_HULK)));
        Lose(r, subject, typeof(datum_mutation_hulk));
        Assert.False(r.In(() => HAS_TRAIT(subject, TRAIT_HULK)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Glowy_lights_its_owner_and_its_power_widens_the_light()
    {
        var (r, subject) = Room(9304);
        var glow = (datum_mutation_glow)Gain(r, subject, typeof(datum_mutation_glow));
        obj_effect_dummy_lighting_obj_moblight light = r.In(() => glow.glow);
        Assert.NotNull(light);
        Assert.Contains(r.In(() => Dm.str(glow.glow_color)), new[] { COLOR_RED, COLOR_BLUE, COLOR_YELLOW, COLOR_GREEN, COLOR_PURPLE, COLOR_ORANGE });
        r.In(() => Waiting.NoSleep(glow.setup()));
        Assert.Equal(2.5f, r.In(() => light.light_range));
        r.In(() => glow.power_coeff = 1.5f);
        r.In(() => Waiting.NoSleep(glow.setup()));
        Assert.Equal(3.75f, r.In(() => light.light_range));
        Lose(r, subject, typeof(datum_mutation_glow));
        Assert.True(r.In(() => QDELETED(light)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Hars_takes_the_head_and_its_loss_regrows_it()
    {
        var (r, subject) = Room(9305);
        Gain(r, subject, typeof(datum_mutation_headless));
        Assert.Null(r.In(() => subject.get_bodypart(BODY_ZONE_HEAD)));
        var brain = r.In(() => subject.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain);
        Assert.NotNull(brain); // the brain recedes into the chest
        Assert.Equal(BODY_ZONE_CHEST, r.In(() => brain.zone));
        Assert.NotEqual((float)DEAD, r.In(() => subject.stat));
        Lose(r, subject, typeof(datum_mutation_headless));
        Assert.NotNull(r.In(() => subject.get_bodypart(BODY_ZONE_HEAD)));
        Assert.Equal(BODY_ZONE_HEAD, r.In(() => (subject.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain).zone));
        r.AssertNoRuntimes();
    }

    private static float Physiology(AudioRoom r, mob_living_carbon_human who, object key) =>
        r.In(() => who.physiology?[key] is object value ? Dm.num(value) : 1f);

    [Fact]
    public void Hypermetabolic_blood_and_fiery_sweat_change_the_physiology_and_back()
    {
        var (r, subject) = Room(9306);
        float bleed = Physiology(r, subject, PHYS_COEFF_BLEED);
        Gain(r, subject, typeof(datum_mutation_bloodier));
        Assert.Equal(bleed * 1.5f, Physiology(r, subject, PHYS_COEFF_BLEED), 3);
        Assert.Equal(6f, Physiology(r, subject, PHYS_COEFF_BLOOD_REGEN), 3);
        Lose(r, subject, typeof(datum_mutation_bloodier));
        Assert.Equal(bleed, Physiology(r, subject, PHYS_COEFF_BLEED), 3);

        float burn = Physiology(r, subject, BURN);
        Gain(r, subject, typeof(datum_mutation_fire));
        Assert.Equal(burn * 0.5f, Physiology(r, subject, BURN), 3);
        Lose(r, subject, typeof(datum_mutation_fire));
        Assert.Equal(burn, Physiology(r, subject, BURN), 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Power_mutations_grant_their_spells_and_take_them_back()
    {
        var (r, subject) = Room(9307);
        foreach ((Type mutation, Type spell) in new[]
        {
            (typeof(datum_mutation_firebreath), typeof(datum_action_cooldown_spell_cone_staggered_fire_breath)),
            (typeof(datum_mutation_tongue_spike), typeof(datum_action_cooldown_spell_tongue_spike)),
            (typeof(datum_mutation_shock), typeof(datum_action_cooldown_spell_touch_shock)),
            (typeof(datum_mutation_thermal), typeof(datum_action_cooldown_spell_thermal_vision)),
            (typeof(datum_mutation_olfaction), typeof(datum_action_cooldown_spell_olfaction)),
        })
        {
            Gain(r, subject, mutation);
            Assert.True(r.In(() => Dm.locate_type(spell, subject.actions) != null), mutation.Name);
            Lose(r, subject, mutation);
            Assert.True(r.In(() => Dm.locate_type(spell, subject.actions) == null), mutation.Name);
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_chemical_allergy_shows_on_the_health_scan_and_venom_coats_the_strikes()
    {
        var (r, subject) = Room(9308);
        Gain(r, subject, typeof(datum_mutation_chemical_allergy));
        DmList lines = r.In(() =>
        {
            DmList render = Dm.list();
            if (Dm.truthy(subject._listen_lookup?[COMSIG_LIVING_HEALTHSCAN]))
                subject._SendSignal(COMSIG_LIVING_HEALTHSCAN, Dm.list(subject, render, 0, null, 0, TRUE));
            return render;
        });
        Assert.Contains(r.In(() => Dm.each(lines).Select(Dm.str).ToList()), line => line.Contains("Chemical Allergy"));
        Assert.True(r.In(() => datum_mutation_chemical_allergy.danger_reagents.len) > 0);
        Assert.True(r.In(() => Dm.truthy(is_type_in_typecache(typeof(datum_reagent_medicine_c2_libital), datum_mutation_chemical_allergy.danger_reagents))));
        Assert.False(r.In(() => Dm.truthy(is_type_in_typecache(typeof(datum_reagent_medicine_mutadone), datum_mutation_chemical_allergy.danger_reagents))));
        Assert.False(r.In(() => Dm.truthy(is_type_in_typecache(typeof(datum_reagent_medicine_epinephrine), datum_mutation_chemical_allergy.danger_reagents))));

        Gain(r, subject, typeof(datum_mutation_venomous_strikes));
        Assert.True(r.In(() => Dm.truthy(subject._listen_lookup?[COMSIG_ON_HIT_EFFECT]))); // the venomous element's hit effect
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Speech_mutations_rewrite_what_their_owner_says()
    {
        var (r, subject) = Room(9309);
        Gain(r, subject, typeof(datum_mutation_swedish));
        // elvis's speech rewrite is a speechmod on the mutation (speech.dm's New), listening to its owner's speech while it is gained
        datum_mutation elvis = Gain(r, subject, typeof(datum_mutation_elvis));
        var speechmod = r.In(() => Dm.each_cast<datum_component_speechmod>(elvis.GetComponents(typeof(datum_component_speechmod)) as DmList).Single()); // speechmod allows several (COMPONENT_DUPE_ALLOWED)
        Assert.NotNull(speechmod);
        Assert.Same(subject, r.In(() => speechmod.targeted));
        Lose(r, subject, typeof(datum_mutation_elvis));
        Assert.Null(r.In(() => speechmod.targeted));
        r.AssertNoRuntimes();
    }
}
