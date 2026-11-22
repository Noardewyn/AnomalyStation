// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/honkspam.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S3, D298: the April Fools paper of the paper bin). The var block is generated.

/// Attachable to items. Plays a bikehorn sound whenever attack_self is called (with a cooldown).
// tg: code/datums/elements/honkspam.dm:2
public partial class datum_element_honkspam : datum_element
{
    // tg: code/datums/elements/honkspam.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ITEM_ATTACK_SELF, nameof(interact));
        return dot;
    }

    // tg: code/datums/elements/honkspam.dm:10
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ITEM_ATTACK_SELF);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/honkspam.dm:14
    [SignalHandler]
    public virtual void interact(obj_item source = null, mob user = null)
    {
        if (HAS_TRAIT(source, TRAIT_HONKSPAMMING))
            return;
        ADD_TRAIT(source, TRAIT_HONKSPAMMING, ELEMENT_TRAIT(GetType()));
        playsound(source.loc as atom, Dm.resource("sound/items/bikehorn.ogg"), 50, TRUE);
        _addtimer(TRAIT_CALLBACK_REMOVE(source, TRAIT_HONKSPAMMING, ELEMENT_TRAIT(GetType())), 2 * SECONDS, file: "code/datums/elements/honkspam.dm", line: 20);
    }
}
