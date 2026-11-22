// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/itempicky.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the aliens' picky claws (they hold only their typecache's items). The var block is generated
// (D111).

/// You can only hold whitelisted items
// tg: code/datums/components/itempicky.dm:2
public partial class datum_component_itempicky : datum_component
{
    // tg: code/datums/components/itempicky.dm:11
    [DmParams("whitelist", "message", "tertiary_condition")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object whitelist = Dm.arg(__extra, 0);
        object message = Dm.arg(__extra, 1);
        object tertiary_condition = Dm.arg(__extra, 2);
        if (!ismob(parent))
            return COMPONENT_INCOMPATIBLE;
        this.whitelist = whitelist;
        if (Dm.truthy(message))
            this.message = Dm.str(message);
        if (Dm.truthy(tertiary_condition))
            this.tertiary_condition = tertiary_condition as datum_callback;
        return null;
    }

    // tg: code/datums/components/itempicky.dm:20
    public override object Destroy(float? force = null)
    {
        tertiary_condition = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/itempicky.dm:24
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_LIVING_TRY_PUT_IN_HAND, nameof(particularly));
    }

    // tg: code/datums/components/itempicky.dm:27
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_LIVING_TRY_PUT_IN_HAND);
    }

    // tg: code/datums/components/itempicky.dm:30
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        if (!ismob(new_parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/itempicky.dm:34
    [DmParams("friend", "i_am_original", "arguments")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        datum_component_itempicky friend = C as datum_component_itempicky;
        if (Dm.truthy(i_am_original))
        {
            whitelist = friend.whitelist;
            message = friend.message;
        }
    }

    // tg: code/datums/components/itempicky.dm:39
    [SignalHandler]
    public virtual object particularly(datum source = null, obj_item pickingup = null)
    {
        // if we were passed the output of a callback, check against that
        if (!(tertiary_condition != null && Dm.truthy(Waiting.NoSleep(tertiary_condition.Invoke()))) && !Dm.truthy(is_type_in_typecache(pickingup, whitelist)))
        {
            to_chat(source, span_warning(Dm.text("[]", replacetext(message, "%TARGET", pickingup))));
            return COMPONENT_LIVING_CANT_PUT_IN_HAND;
        }
        return null;
    }
}
