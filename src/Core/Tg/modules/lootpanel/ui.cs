// Translated from tgstation (snapshot 1d99bd7f): code/modules/lootpanel/ui.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package G1 (D291): the whole file.

public partial class datum_lootpanel
{
    /// UI helper for converting the associative list to a list of lists
    // tg: code/modules/lootpanel/ui.dm:2
    public virtual object get_contents()
    {
        DmList items = Dm.list();

        foreach (datum_search_object index in Dm.each_cast<datum_search_object>(contents))
        {
            UNTYPED_LIST_ADD(ref items, Dm.list(
                Dm.kv("icon_state", index.icon_state),
                Dm.kv("icon", index.icon),
                Dm.kv("name", index.name),
                Dm.kv("path", index.path),
                Dm.kv("ref", REF(index))
            ));
        }

        return items;
    }

    /// Clicks an object from the contents. Validates the object and the user
    // tg: code/modules/lootpanel/ui.dm:18
    public virtual async DmTask<object> grab(mob user = null, DmList @params = null)
    {
        object @ref = @params["ref"];
        if (@ref == null)
            return FALSE;

        datum_search_object index = Dm.locate_ref(@ref) as datum_search_object;
        if (index != null && !Dm.@in(index, contents))
            index = null;
        atom thing = index?.item;
        if (QDELETED(index) || QDELETED(thing)) // Obj is gone
            return FALSE;

        if (thing != source_turf && !Dm.@in(thing, source_turf.contents))
        {
            qdel(index); // Item has moved
            return TRUE;
        }

        string modifiers = "";
        if (Dm.truthy(@params["ctrl"]))
            modifiers += "ctrl=1;";
        if (Dm.truthy(@params["middle"]))
            modifiers += "middle=1;";
        if (Dm.truthy(@params["shift"]))
            modifiers += "shift=1;";
        if (Dm.truthy(@params["alt"]))
            modifiers += "alt=1;";
        if (Dm.truthy(@params["right"]))
            modifiers += "right=1;";

        await user.ClickOn(thing, modifiers);

        return TRUE;
    }
}
