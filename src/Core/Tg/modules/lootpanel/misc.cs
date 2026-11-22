// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole file. The BYOND version notice (`#if !defined(OPENDREAM) && !defined(UNIT_TESTS)`, active in
// production) is left out: the 3D client is no BYOND client and always draws the icons itself (search_object.cs).

public partial class datum_lootpanel
{
    /// Helper to open the panel
    // tg: code/modules/lootpanel/misc.dm:2
    public virtual async DmTask open(turf tile = null)
    {
        if (tile != source_turf)
        {
            if (source_turf != null)
                UnregisterSignal(source_turf, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON));
            RegisterSignals(tile, Dm.list(COMSIG_ATOM_ENTERED, COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON), nameof(on_source_turf_entered));
        }

        source_turf = tile;

        // tg: #if !defined(OPENDREAM) && !defined(UNIT_TESTS) ... the notice to a BYOND client older than 515.1635 ... #endif: not translated, the
        // 3D client is no BYOND client (see the header)

        await populate_contents();
        await ui_interact(owner.mob);
    }

    /**
     * Called by SSlooting whenever this datum is added to its backlog.
     * Iterates over to_image list to create icons, then removes them.
     * Returns boolean - whether this proc has finished the queue or not.
     */
    // tg: code/modules/lootpanel/misc.dm:34
    public virtual async DmTask<object> process_images()
    {
        foreach (datum_search_object index in Dm.each_cast<datum_search_object>(to_image))
        {
            Dm.remove(ref to_image, index);

            if (QDELETED(index) || Dm.truthy(index.icon))
                continue;

            index.generate_icon(owner);

            if (TICK_CHECK())
                break;
        }

        datum_tgui window = SStgui.get_open_ui(owner.mob, this) as datum_tgui;
        if (window == null)
        {
            reset_contents();
            return TRUE;
        }

        await window.send_update();

        return !Dm.truthy(length(to_image)) ? TRUE : FALSE;
    }
}
