// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/random_items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file (the sane random item types, Santa's "anything" gifts).

public static partial class GlobalProcs
{
    ///Gets a list of all the item types that are valid for random item spawns
    // tg: code/__HELPERS/random_items.dm:3
    public static object get_sane_item_types(object requested_type = null)
    {
        if (!ispath(requested_type, typeof(obj_item)))
            return Dm.list();
        DmList all_valid_types = Dm.list();
        foreach (Type iter_type in Dm.each_cast<Type>(typesof((Type)requested_type)))
        {
            if ((Dm.initial(iter_type, (obj_item p) => p.abstract_type) == iter_type) || Dm.truthy(Dm.bitand(Dm.initial(iter_type, (obj_item p) => p.item_flags), ABSTRACT)))
                continue;
            if (Dm.truthy(Dm.initial(iter_type, (obj_item p) => p.spawn_blacklisted)))
                continue;
            // The original behavior also included inhand icon states but that seems dumb
            // if(!iter_type.icon_state || !iter_type.inhand_icon_state)
            if (!Dm.truthy(Dm.initial(iter_type, (obj_item p) => p.icon_state)))
                continue; // With the existance of abstract_type we could prob depricate this handling at some point
            Dm.append(ref all_valid_types, iter_type);
        }
        return all_valid_types;
    }
}
