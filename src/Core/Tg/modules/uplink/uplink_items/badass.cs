// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/badass.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks). The balloon's
// item waits for G3 (DV343).

// tg: code/modules/uplink/uplink_items/badass.dm:10
public partial class datum_uplink_item_badass_balloon : datum_uplink_item_badass
{
    // tg: code/modules/uplink/uplink_items/badass.dm:19
    public override async DmTask<object> spawn_item(object spawn_path = null, mob user = null, datum_uplink_handler uplink_handler = null, atom_movable source = null)
    {
        object dot = await base.spawn_item(spawn_path, user, uplink_handler, source);

        if (!Dm.truthy(dot))
            return null;

        notify_ghosts(
            I18n.T("[] has purchased a BADASS Syndicate Balloon!", user.real_name),
            source: dot as atom,
            header: I18n.T("What are they THINKING?"));
        return dot;
    }
}
