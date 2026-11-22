// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_clown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186): the clown's outfit implants a sad trombone.

/// A passive implant that plays a funny noise (defaults to sound/misc/sadtrombone.ogg) when you deathgasp for any reason
// tg: code/game/objects/items/implants/implant_clown.dm:2
public partial class obj_item_implant_sad_trombone : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_clown.dm:15
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (Dm.truthy(dot))
            RegisterSignal(target, COMSIG_MOB_EMOTED("deathgasp"), nameof(on_deathgasp));
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_clown.dm:20
    public override async DmTask<object> removed(mob_living target = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (Dm.truthy(dot))
            UnregisterSignal(target, COMSIG_MOB_EMOTED("deathgasp"));
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_clown.dm:25
    [SignalHandler]
    public virtual void on_deathgasp(mob source = null)
    {
        playsound(loc as atom, death_noise, 50, FALSE);
    }
}
