// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/silicon_gender.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/silicon_gender.dm:1-4  the file-local #defines (#undef at :35-38)
// tg: code/modules/client/preferences/silicon_gender.dm:6
public partial class datum_preference_choiced_silicon_gender : datum_preference_choiced
{
    private const string SILICON_MALE = "He/Him";
    private const string SILICON_FEMALE = "She/Her";
    private const string SILICON_PLURAL = "They/Them";
    private const string SILICON_NEUTER = "It/Its";

    // tg: code/modules/client/preferences/silicon_gender.dm:11  var/static/use_character_gender
    public static ref string use_character_gender => ref TgState.Current.datum_preference_choiced_silicon_gender__use_character_gender;
    ///Used to convert the read value of this preference into a gender
    // tg: code/modules/client/preferences/silicon_gender.dm:13  var/static/list/pronouns_to_genders
    public static ref DmList pronouns_to_genders => ref TgState.Current.datum_preference_choiced_silicon_gender__pronouns_to_genders;

    // tg: code/modules/client/preferences/silicon_gender.dm:20
    public override DmList init_possible_values()
    {
        return Dm.list(
            use_character_gender,
            SILICON_MALE,
            SILICON_FEMALE,
            SILICON_PLURAL,
            SILICON_NEUTER);
    }

    // tg: code/modules/client/preferences/silicon_gender.dm:29
    public override object create_default_value()
    {
        return SILICON_NEUTER;
    }

    // tg: code/modules/client/preferences/silicon_gender.dm:32
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/client/preferences/silicon_gender.dm:11
    internal string datum_preference_choiced_silicon_gender__use_character_gender = "Use character gender";
    // tg: code/modules/client/preferences/silicon_gender.dm:13
    internal DmList datum_preference_choiced_silicon_gender__pronouns_to_genders = Dm.list(
        Dm.kv("He/Him", MALE),
        Dm.kv("She/Her", FEMALE),
        Dm.kv("They/Them", PLURAL),
        Dm.kv("It/Its", NEUTER));
}
