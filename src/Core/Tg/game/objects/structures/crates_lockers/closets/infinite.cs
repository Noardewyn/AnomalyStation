// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/infinite.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole, the infinite closet (an admin's toy, not mapped): shut, it makes copies of the first thing put in it
// up to four, and it shuts itself 15 seconds after it is closed. The var block is generated (D111).

// tg: code/game/objects/structures/crates_lockers/closets/infinite.dm:1
public partial class obj_structure_closet_infinite : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/infinite.dm:8
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/infinite.dm:12
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/infinite.dm:16
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (!Dm.truthy(replicating_type))
        {
            if (contents.Count == 0)
                return null;
            else
                replicating_type = ((datum)contents[1]).type;
        }

        if (Dm.truthy(replicating_type) && !Dm.truthy(opened) && (contents.Count < stop_replicating_at))
            Dm.@new((Type)replicating_type, this);
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/infinite.dm:26
    public override async DmTask after_close(mob_living user = null, params object[] __extra)
    {
        await base.after_close(user, __extra);
        if (Dm.truthy(auto_close_time))
            _addtimer(Dm.@new<datum_callback>(this, nameof(close_on_my_own)), auto_close_time, TIMER_OVERRIDE | TIMER_UNIQUE, file: "code/game/objects/structures/crates_lockers/closets/infinite.dm", line: 29);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/infinite.dm:31
    public virtual async DmTask close_on_my_own()
    {
        if (Dm.truthy(await close()))
            visible_message(span_notice(I18n.T("\\The [] closes on its own.", this)));
    }
}
