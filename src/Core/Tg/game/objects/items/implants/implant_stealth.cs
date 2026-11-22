// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_stealth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3): the S3 implant and its implanter have no procs; the agent's box they make. The var blocks are
// generated (D111).

//Box Object

// tg: code/game/objects/items/implants/implant_stealth.dm:22
public partial class obj_structure_closet_cardboard_agent : obj_structure_closet_cardboard
{
    // tg: code/game/objects/items/implants/implant_stealth.dm:30
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        go_invisible();
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_stealth.dm:34
    public virtual void go_invisible()
    {
        animate(this, Dm.named("alpha", 0), Dm.named("time", 20));
    }

    // tg: code/game/objects/items/implants/implant_stealth.dm:37
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        qdel(this);
    }

    // tg: code/game/objects/items/implants/implant_stealth.dm:41
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        alpha = Dm.max(0, alpha - 50);
        return null;
    }

    // tg: code/game/objects/items/implants/implant_stealth.dm:44
    public virtual void reveal()
    {
        alpha = 255;
        _addtimer(Dm.@new<datum_callback>(this, nameof(go_invisible)), 10, Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/game/objects/items/implants/implant_stealth.dm", line: 46);
    }

    // tg: code/game/objects/items/implants/implant_stealth.dm:48
    public override async DmTask<object> Bump(atom A = null)
    {
        object dot = await base.Bump(A);
        if (A is obj_machinery_door)
        {
            foreach (mob mob_in_box in Dm.each<mob>(contents))
                A.Bumped(mob_in_box);
        }
        if (isliving(A))
            reveal();
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_stealth.dm:56
    public override object Bumped(atom_movable A = null)
    {
        object dot = base.Bumped(A);
        if (isliving(A))
            reveal();
        return dot;
    }
}
