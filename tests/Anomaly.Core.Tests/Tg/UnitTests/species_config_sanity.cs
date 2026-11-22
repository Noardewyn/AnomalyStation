// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/species_config_sanity.dm
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
 * Species IDs are used in keyed_list config entries and their config values can either be set implicitly or explicitly.
 *
 * In order to accomplish this, the keyed_list looks for a specific splitter that is meant to separate the key from the value.
 *
 * While it supports multiple instances of the splitter (for example, space) being present, the intent is ambiguous.
 *
 * To combat that, this unit test runs through every species ID and make sure it doesn't contain the splitter character, so
 * valid config entries are never ambiguous.
 */
// tg: code/modules/unit_tests/species_config_sanity.dm:11 (/datum/unit_test/species_config_sanity; class named by rules 2.3)
public sealed class datum_unit_test_species_config_sanity : TgUnitTest
{
    // tg: code/modules/unit_tests/species_config_sanity.dm:11
    public override async DmTask Run()
    {
        object first_splitter = Dm.initial(typeof(datum_config_entry_keyed_list_roundstart_races), (datum_config_entry_keyed_list p) => p.splitter);
        object second_splitter = Dm.initial(typeof(datum_config_entry_keyed_list_roundstart_no_hard_check), (datum_config_entry_keyed_list p) => p.splitter);
        foreach (Type species_type in Dm.each_cast<Type>(subtypesof(typeof(datum_species))))
        {
            object species_id = Dm.initial(species_type, (datum_species p) => p.id);
            if (Dm.truthy(findtext(species_id, first_splitter)))
                TEST_FAIL(Dm.text("A species ID contained a config_entry splitter: [] | Splitter: (\"[]\") | Species ID: (\"[]\")", species_type, first_splitter, species_id));
            if (Dm.truthy(findtext(species_id, second_splitter)))
                TEST_FAIL(Dm.text("A species ID contained a config_entry splitter: [] | Splitter: (\"[]\") | Species ID: (\"[]\")", species_type, second_splitter, species_id));
        }
        await DmTask.CompletedTask;
    }
}
