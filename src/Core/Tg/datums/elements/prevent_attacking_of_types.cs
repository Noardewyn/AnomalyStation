// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/prevent_attacking_of_types.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2).

/// This hostile will not be able to attack a given typecache, and will receive
/// a balloon alert when it tries to.
// tg: code/datums/elements/prevent_attacking_of_types.dm:4
public partial class datum_element_prevent_attacking_of_types : datum_element
{
    // tg: code/datums/elements/prevent_attacking_of_types.dm:13
    [DmParams("target", "typecache", "alert_message")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        DmList typecache = Dm.arg<DmList>(__extra, 0);
        object alert_message = Dm.arg(__extra, 1);
        object dot = await base.Attach(target, __extra);

        if (!isanimal_or_basicmob(target))
            return ELEMENT_INCOMPATIBLE;

        this.alert_message = alert_message;
        this.typecache = typecache;

        RegisterSignal(target, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_pre_attacking_target));
        return dot;
    }

    // tg: code/datums/elements/prevent_attacking_of_types.dm:24
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_HOSTILE_PRE_ATTACKINGTARGET);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/prevent_attacking_of_types.dm:28
    [SignalHandler]
    public virtual object on_pre_attacking_target(mob source = null, atom target = null)
    {
        if (!Dm.truthy(typecache[target.type]))
            return null;

        target.balloon_alert(source, alert_message);

        return COMPONENT_HOSTILE_NO_ATTACK;
    }
}
