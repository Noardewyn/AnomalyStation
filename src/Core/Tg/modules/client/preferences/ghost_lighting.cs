// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/ghost_lighting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// How bright a ghost's lighting plane is
// tg: code/modules/client/preferences/ghost_lighting.dm:2
public partial class datum_preference_choiced_ghost_lighting : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ghost_lighting.dm:7
    public override object create_default_value()
    {
        return "Darker";
    }

    // tg: code/modules/client/preferences/ghost_lighting.dm:10
    public override DmList init_possible_values()
    {
        DmList values = Dm.list();
        foreach (object option_name in Dm.each(GLOB.ghost_lightings))
            Dm.append(ref values, option_name);
        return values;
    }

    // tg: code/modules/client/preferences/ghost_lighting.dm:16
    public override void apply_to_client(client client = null, object value = null)
    {
        mob current_mob = client?.mob;
        if (!isobserver(current_mob))
            return;
        current_mob.lighting_cutoff = Dm.num_or_null(current_mob.default_lighting_cutoff());
        current_mob.update_sight();
    }
}
