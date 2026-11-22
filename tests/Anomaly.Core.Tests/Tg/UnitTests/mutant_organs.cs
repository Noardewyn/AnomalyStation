// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/mutant_organs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

// Gaps SP1 (D338): the whole file.

/**
 * Iterates over all species to ensure that organs are valid after being set to a mutant species.
 */
// ADAPTED: the species whose types are inert (not yet ported: the moth, the ethereal, the plasmaman and the rest, gaps SP2 and SP3)
// are left out with tg's dullahan; the room is the toy room (TgToyRoom.Install), whose GLOB is made before tg's strings files are
// mounted, so the test mounts them and reads the gross adjectives the fly's organs are named with (names.dm:288).
// tg: code/modules/unit_tests/mutant_organs.dm:4 (/datum/unit_test/mutant_organs; class named by rules 2.3)
public sealed class datum_unit_test_mutant_organs : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        if (Tools.TgClone.Root != null)
        {
            TgStrings.Mount(DmWorldState.Of(World.Current).Files, Tools.TgClone.Root);
            GLOB.gross_adjectives = world.file2list("strings/names/gross_adjectives.txt") as DmList;
        }
    }

    // tg: code/modules/unit_tests/mutant_organs.dm:6
    public override async DmTask Run()
    {
        DmList ignore = Dm.list(
            typeof(datum_species_dullahan));
        DmList species = Dm.sub(subtypesof(typeof(datum_species)), ignore) as DmList;
        DmList organs_we_care_about = Dm.list(
            ORGAN_SLOT_BRAIN,
            ORGAN_SLOT_HEART,
            ORGAN_SLOT_LUNGS,
            ORGAN_SLOT_EYES,
            ORGAN_SLOT_EARS,
            ORGAN_SLOT_TONGUE,
            ORGAN_SLOT_LIVER,
            ORGAN_SLOT_STOMACH,
            ORGAN_SLOT_APPENDIX);

        foreach (Type species_type in Dm.each_cast<Type>(species))
        {
            if (Attribute.IsDefined(species_type, typeof(DmInertAttribute), inherit: false))
                continue; // ADAPTED: not ported yet
            // get our dummy
            mob_living_carbon_human_consistent dummy = allocate<mob_living_carbon_human_consistent>();

            // change them to the species
            dummy.set_species(species_type);

            // check all their organs
            foreach (object organ_slot in Dm.each(organs_we_care_about))
            {
                object expected_type = ((datum_species)dummy.dna.species).get_mutant_organ_type_for_slot(organ_slot);
                obj_item_organ actual_organ = dummy.get_organ_slot(organ_slot) as obj_item_organ;
                if (actual_organ == null)
                {
                    if (expected_type != null)
                    {
                        TEST_FAIL(Dm.text("[] did not update their [] organ to [], no organ was found", species_type, organ_slot, expected_type));
                        continue;
                    }
                }
                else
                {
                    if (expected_type == null)
                    {
                        TEST_FAIL(Dm.text("[] did not remove their [] organ", species_type, organ_slot));
                        continue;
                    }

                    if (actual_organ.GetType() != expected_type as Type)
                    {
                        TEST_FAIL(Dm.text("[] did not update their [] organ to [], instead it was []", species_type, organ_slot, expected_type, actual_organ.GetType()));
                        continue;
                    }
                }
            }
        }
        await DmTask.CompletedTask;
    }
}
