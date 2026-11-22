// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/tool_flash.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the welder's flash on the user's eyes). The var block and the element flags are generated (D111).

// tg: code/datums/elements/tool_flash.dm:6
public partial class datum_element_tool_flash : datum_element
{
    // tg: code/datums/elements/tool_flash.dm:12
    [DmParams("target", "flash_strength")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object flash_strength = Dm.arg(__extra, 0);
        object dot = await base.Attach(target, Dm.forward(__extra, flash_strength));
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;

        this.flash_strength = flash_strength;

        RegisterSignal(target, COMSIG_TOOL_IN_USE, nameof(prob_flash));
        RegisterSignal(target, COMSIG_TOOL_START_USE, nameof(flash));
        return dot;
    }

    // tg: code/datums/elements/tool_flash.dm:22
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, Dm.list(COMSIG_TOOL_IN_USE, COMSIG_TOOL_START_USE));
        return dot;
    }

    // tg: code/datums/elements/tool_flash.dm:26
    [SignalHandler]
    public virtual void prob_flash(datum source = null, mob_living user = null)
    {
        // SIGNAL_HANDLER

        if (prob(90))
            return;
        flash(source, user);
    }

    // tg: code/datums/elements/tool_flash.dm:33
    [SignalHandler]
    public virtual void flash(datum source = null, mob_living user = null)
    {
        // SIGNAL_HANDLER

        if (user != null && Dm.num(flash_strength) > 0 && get_dist(get_turf(source as atom), get_turf(user)) <= 1)
            Waiting.NoSleep(user.flash_act(Math.Max(Dm.num(flash_strength), 1)));
    }
}
