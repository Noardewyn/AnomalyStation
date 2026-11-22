// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/gender.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Gender preference
// tg: code/modules/client/preferences/gender.dm:2
[DmPath("/datum/preference/choiced/gender")]
public partial class datum_preference_choiced_gender : datum_preference_choiced
{
    // tg: code/modules/client/preferences/gender.dm:7
    public override DmList init_possible_values()
    {
        return Dm.list(MALE, FEMALE, PLURAL, NEUTER);
    }

    // tg: code/modules/client/preferences/gender.dm:10
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (!Dm.truthy(((datum_species)target.dna.species).sexes))
            value = PLURAL; //disregard gender preferences on this species
        target.gender = Dm.str(value);
    }

    // tg: code/modules/client/preferences/gender.dm:15
    public override object create_default_value()
    {
        // The only reason I'm limiting this to male or female
        // is that hairstyle randomization handles enbies poorly
        return pick(MALE, FEMALE);
    }
}
