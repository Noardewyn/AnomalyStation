using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI10 (D326): the android species. A human made an android gets tg's android limbs, the cybernetic brain and robotic
/// voicebox and no heart, lungs, liver or stomach; it does not breathe or bleed and is immune to toxins; it speaks with the robot span
/// and understands the machine language; an EMP hurts its cybernetic brain and a multitool repairs it.
/// </summary>
public class AndroidTests
{
    private const int Second = 20;

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[] { typeof(datum_species_android), typeof(obj_item_organ_brain_cybernetic), typeof(obj_item_organ_tongue_robot) })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AnAndroidIsBuiltOfParts()
    {
        var r = new AudioRoom(11, 9, seed: 9971);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.Step(Second);
        r.In(() => bob.set_species(typeof(datum_species_android)));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<datum_species_android>(bob.dna.species);
            Assert.IsType<obj_item_organ_brain_cybernetic>(bob.get_organ_slot(ORGAN_SLOT_BRAIN));
            Assert.IsType<obj_item_organ_tongue_robot>(bob.get_organ_slot(ORGAN_SLOT_TONGUE));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_HEART));
            Assert.Null(bob.get_organ_slot(ORGAN_SLOT_LUNGS));
            Assert.IsType<obj_item_bodypart_chest_robot_android>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.True(HAS_TRAIT(bob, TRAIT_NOBREATH));
            Assert.True(HAS_TRAIT(bob, TRAIT_TOXIMMUNE));
            Assert.True(Dm.truthy(bob.has_language(typeof(datum_language_machine))));
            Assert.True(Dm.truthy(Dm.bitand(bob.mob_biotypes, MOB_ROBOTIC)));
        });

        // an EMP hurts the cybernetic brain
        var brain = r.In(() => (obj_item_organ_brain_cybernetic)bob.get_organ_slot(ORGAN_SLOT_BRAIN));
        float before = r.In(() => Dm.num(brain.damage));
        r.In(() => Waiting.NoSleep(brain.emp_act(EMP_HEAVY)));
        Assert.True(r.In(() => Dm.num(brain.damage)) > before);
        r.AssertNoRuntimes();
    }
}
