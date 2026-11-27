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
/// The lizard's own limbs (species_parts/lizard_bodyparts.dm) wherever a lizard is made: a human turned lizard by a species change gets
/// the lizard's six bodyparts drawn from the lizard's greyscale sheet in his mutant colour, and a human again on the way back; the ash
/// walker's forced digitigrade legs; a lizard's arm cut off keeps the lizard's sprite on the floor. The preferences' lizard is
/// SpeciesSP1Tests'.
/// </summary>
public class LizardLimbsTests
{
    private const string LizardSheet = "icons/mob/human/species/lizard/bodyparts.dmi";

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(9, 7, seed: seed);
        Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        return (r, bob);
    }

    private static string Path(object icon) => (icon as DmResource)?.Path ?? Dm.str(icon);

    [Fact]
    public void A_species_change_gives_the_lizards_limbs_and_takes_them_back()
    {
        var (r, bob) = Room(13720);
        r.In(() =>
        {
            bob.dna.features[FEATURE_MUTANT_COLOR] = "#3355cc";
            bob.set_species(typeof(datum_species_lizard));
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.IsType<obj_item_bodypart_head_lizard>(bob.get_bodypart(BODY_ZONE_HEAD));
            Assert.IsType<obj_item_bodypart_chest_lizard>(bob.get_bodypart(BODY_ZONE_CHEST));
            Assert.IsType<obj_item_bodypart_arm_left_lizard>(bob.get_bodypart(BODY_ZONE_L_ARM));
            Assert.IsType<obj_item_bodypart_arm_right_lizard>(bob.get_bodypart(BODY_ZONE_R_ARM));
            Assert.IsType<obj_item_bodypart_leg_left_lizard>(bob.get_bodypart(BODY_ZONE_L_LEG));
            Assert.IsType<obj_item_bodypart_leg_right_lizard>(bob.get_bodypart(BODY_ZONE_R_LEG));
            foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(bob.bodyparts))
            {
                Assert.Equal(SPECIES_LIZARD, part.limb_id);
                Assert.True(Dm.truthy(part.should_draw_greyscale), part.body_zone + " is not drawn greyscale");
                Assert.Equal(LizardSheet, Path(part.icon_greyscale));
                Assert.Equal("#3355cc", Dm.str(part.draw_color));
            }
            bob.set_species(typeof(datum_species_human));
        });
        r.Step(2);
        r.In(() =>
        {
            foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(bob.bodyparts))
                Assert.Equal(SPECIES_HUMAN, part.limb_id);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_ash_walker_stands_on_digitigrade_legs_and_a_severed_lizard_arm_stays_a_lizards()
    {
        var (r, bob) = Room(13721);
        r.In(() => { bob.set_species(typeof(datum_species_lizard_ashwalker)); return 0; });
        r.Step(2);
        r.In(() =>
        {
            var leg = Assert.IsType<obj_item_bodypart_leg_left_digitigrade>(bob.get_bodypart(BODY_ZONE_L_LEG));
            Assert.Equal(BODYPART_ID_DIGITIGRADE, leg.limb_id);
            Assert.IsType<obj_item_bodypart_arm_left_lizard_ashwalker>(bob.get_bodypart(BODY_ZONE_L_ARM));
        });
        var arm = r.In(() => (obj_item_bodypart)bob.get_bodypart(BODY_ZONE_L_ARM));
        r.In(() => Waiting.NoSleep(arm.dismember()));
        r.Step(2);
        r.In(() =>
        {
            Assert.IsAssignableFrom<turf>(arm.loc);
            Assert.Equal(SPECIES_LIZARD, arm.limb_id);
            // the picture on the floor is its overlays (update_icon_dropped): the lizard's arm from his sheet
            var limb = Dm.each_cast<image>((DmList)Waiting.NoSleep(arm.get_limb_icon(TRUE))).First();
            Assert.Equal(LizardSheet, Path(limb.icon));
            Assert.Equal("lizard_l_arm", limb.icon_state);
        });
    }
}
