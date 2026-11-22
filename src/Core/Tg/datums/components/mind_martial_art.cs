// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/mind_martial_art.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): a martial art owned by a mind (the scrolls' and the manuals'), whole.

/// A martial art that is owned by this mind and will transfer as mind moves
// tg: code/datums/components/mind_martial_art.dm:2
public partial class datum_component_mindbound_martial_arts : datum_component
{
    // tg: code/datums/components/mind_martial_art.dm:7
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type style_type = Dm.arg<Type>(__extra, 0);
        if (parent is not datum_mind)
            return COMPONENT_INCOMPATIBLE;

        style = Dm.@new(style_type, this) as datum_martial_art;
        return null;
    }

    // tg: code/datums/components/mind_martial_art.dm:13
    public override async DmTask<object> CheckDupeComponent(datum_component C = null, params object[] __extra)
    {
        Type new_style_type = Dm.arg<Type>(__extra, 0);
        return style.GetType() == new_style_type ? TRUE : FALSE;
    }

    // tg: code/datums/components/mind_martial_art.dm:16
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MIND_TRANSFERRED, nameof(mind_transferred));

        var mind = parent as datum_mind;
        await style.teach(mind.current as mob_living);
    }

    // tg: code/datums/components/mind_martial_art.dm:22
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MIND_TRANSFERRED);

        var mind = parent as datum_mind;
        await style.unlearn(mind.current as mob_living);
    }

    // tg: code/datums/components/mind_martial_art.dm:28
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref style);
        return base.Destroy(force);
    }

    /// Signal proc for [COMSIG_MOB_MIND_TRANSFERRED_OUT_OF] to pass martial arts between bodies on mind transfer
    /// By this point the martial art's holder is the old body, but the mind that owns it is in the new body
    // tg: code/datums/components/mind_martial_art.dm:34
    [SignalHandler]
    public virtual void mind_transferred(datum_mind source = null, mob_living old_body = null)
    {
        Waiting.NoSleep(style.unlearn(old_body));
        Waiting.NoSleep(style.teach(source.current as mob_living));
    }
}
