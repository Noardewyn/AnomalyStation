// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/secure/secure_closets.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the secure closet's Initialize (docs/ai/specs/m4a-ownership.md); Meta's secure closet types' PopulateContents are
// in the mirrors of their tg files. grey_tide since gaps E1b (the grey tide event).

// tg: code/game/objects/structures/crates_lockers/closets/secure/secure_closets.dm:21
public partial class obj_structure_closet_secure_closet : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/secure_closets.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_GREY_TIDE, nameof(grey_tide));
        return dot;
    }
}

// tg: code/game/objects/structures/crates_lockers/closets/secure/secure_closets.dm:25
public partial class obj_structure_closet_secure_closet : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/secure/secure_closets.dm:25
    [SignalHandler]
    public virtual void grey_tide(datum source = null, DmList grey_tide_areas = null)
    {
        if (!Dm.truthy(is_station_level(z)))
            return;

        foreach (object area_type in Dm.each(grey_tide_areas))
        {
            if (!istype(get_area(this), area_type as Type))
                continue;
            locked = FALSE;
            update_appearance(UPDATE_ICON);
        }
    }
}
