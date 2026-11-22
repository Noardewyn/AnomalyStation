// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/preference_species.dm
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
// ADAPTED: tg's CI reads tgstation's config (game_options.txt's ROUNDSTART_RACES); the harness has no config files, so the test sets
// the roundstart races the server writes (ServerHost.TgConfigText) before it runs.

/**
 * Checks that all enabled roundstart species
 * selectable within the preferences menu
 * have their info / page setup correctly.
 */
// tg: code/modules/unit_tests/preference_species.dm:6 (/datum/unit_test/preference_species; class named by rules 2.3)
public sealed class datum_unit_test_preference_species : TgUnitTest
{
    public override void New()
    {
        base.New();
        TgToyRoom.Install(this);
        ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_keyed_list_roundstart_races)]).config_entry_value =
            Dm.list(Dm.kv(SPECIES_HUMAN, TRUE), Dm.kv(SPECIES_FELINE, TRUE), Dm.kv(SPECIES_LIZARD, TRUE));
        GLOB.roundstart_races = Dm.list();
    }

    // tg: code/modules/unit_tests/preference_species.dm:8
    public override async DmTask Run()
    {
        TEST_ASSERT_EQUAL(Dm.length(get_selectable_species()), 3, "The human, the felinid and the lizard are not the selectable species.");

        // Go though all selectable species to see if they have their page setup correctly.
        foreach (object species_id in Dm.each(get_selectable_species()))
        {
            Type species_type = GLOB.species_list[species_id] as Type;
            datum_species species = GLOB.species_prototypes[species_type] as datum_species;

            // Check the species decription.
            // If it's not overridden, a stack trace will be thrown (and fail the test).
            // If it's null, it was improperly overriden. Fail the test.
            object species_desc = species.get_species_description();
            if (species_desc == null)
                TEST_FAIL(Dm.text("Species [] ([]) is selectable, but did not properly implement get_species_description().", species, species_type));

            // Check the species lore.
            // If it's not overridden, a stack trace will be thrown (and fail the test).
            // If it's null, or returned a list, it was improperly overriden. Fail the test.
            object species_lore = species.get_species_lore();
            if (species_lore == null)
                TEST_FAIL(Dm.text("Species [] ([]) is selectable, but did not properly implement get_species_lore().", species, species_type));
            else if (!islist(species_lore))
                TEST_FAIL(Dm.text("Species [] ([]) is selectable, but did not properly implement get_species_lore() (Did not return a list).", species, species_type));
        }
        await DmTask.CompletedTask;
    }
}
