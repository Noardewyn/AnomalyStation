// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/handlers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole file, production branch (rules 1.7: the `#if !defined(UNIT_TESTS)` guard is active).

public partial class datum_lootpanel
{
    /// On contents change, either reset or update
    // tg: code/modules/lootpanel/handlers.dm:2
    [SignalHandler]
    public virtual void on_searchable_deleted(datum_search_object source = null)
    {
        Dm.remove(ref contents, source);
        Dm.remove(ref to_image, source);

        datum_tgui window = SStgui.get_open_ui(owner.mob, this) as datum_tgui;
        // #if !defined(UNIT_TESTS) // we dont want to delete contents if we're testing
        if (window == null)
        {
            reset_contents();
            return;
        }
        // #endif

        if (isturf(source.item))
        {
            Waiting.NoSleep(populate_contents());
            return;
        }

        Waiting.NoSleep(window.send_update());
    }
}
