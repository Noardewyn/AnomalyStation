// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/operative_species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316). The var block is generated (D111).

/// When TRUE, will spawn you as a human when selected for an operative role
/// When FALSE, players will be placed into the game as their character's species
// tg: code/modules/client/preferences/operative_species.dm:3
public partial class datum_preference_toggle_nuke_ops_species : datum_preference_toggle
{
    // tg: code/modules/client/preferences/operative_species.dm:11
    public override object is_accessible(datum_preferences preferences = null)
    {
        object dot = base.is_accessible(preferences);
        if (!Dm.truthy(dot))
            return FALSE;

        // If one of the roles is ticked in the antag prefs menu, this option will show.
        TgState.Current.datum_preference_toggle_nuke_ops_species__is_accessible__ops_roles ??= Dm.list(ROLE_OPERATIVE, ROLE_LONE_OPERATIVE, ROLE_OPERATIVE_MIDROUND, ROLE_CLOWN_OPERATIVE, ROLE_CLOWN_OPERATIVE_MIDROUND);
        if (Dm.truthy(length(Dm.bitand(TgState.Current.datum_preference_toggle_nuke_ops_species__is_accessible__ops_roles, preferences.be_special))))
            return TRUE;

        return FALSE;
    }

    // tg: code/modules/client/preferences/operative_species.dm:22
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }
}
