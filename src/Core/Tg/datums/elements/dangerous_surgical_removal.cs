// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/dangerous_surgical_removal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the surplus organs explode when they are taken out). The var block is generated (D111).

/**
 * ## DANGEROUS ORGAN REMOVAL ELEMENT
 *
 * Makes the organ explode when removed (potentially surgically!).
 * That's about it.
 */
// tg: code/datums/elements/dangerous_surgical_removal.dm:7
public partial class datum_element_dangerous_organ_removal : datum_element
{
    // tg: code/datums/elements/dangerous_surgical_removal.dm:14
    [DmParams("target", "surgical")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object surgical = Dm.arg(__extra, 0) ?? FALSE;
        object dot = await base.Attach(target, Dm.forward(__extra, surgical));
        if (!isorgan(target))
            return ELEMENT_INCOMPATIBLE;

        this.surgical = surgical;

        if (Dm.truthy(surgical))
            RegisterSignal(target, COMSIG_ORGAN_SURGICALLY_REMOVED, nameof(on_removal));
        else
            RegisterSignal(target, COMSIG_ORGAN_REMOVED, nameof(on_removal));
        return dot;
    }

    // tg: code/datums/elements/dangerous_surgical_removal.dm:26
    [DmParams("source")]
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);

        UnregisterSignal(target, Dm.list(COMSIG_ORGAN_SURGICALLY_REMOVED, COMSIG_ORGAN_REMOVED));
        return dot;
    }

    // tg: code/datums/elements/dangerous_surgical_removal.dm:31
    [SignalHandler]
    public virtual void on_removal(obj_item_organ source = null, mob_living user = null, mob_living_carbon old_owner = null, object target_zone = null, obj_item tool = null)
    {
        if (Dm.truthy(surgical) && Dm.truthy(Dm.bitand(source.organ_flags, Dm.bitor(ORGAN_FAILING, ORGAN_EMP))))
            return;
        if (user != null && Dm.truthy(Waiting.NoSleep(user.Adjacent(source))))
        {
            source.audible_message(I18n.T("[] explodes on []'s face!", source, user));
            Waiting.NoSleep(user.take_bodypart_damage(15));
        }
        else
            source.audible_message(I18n.T("[] explodes into tiny pieces!", source));

        Waiting.NoSleep(explosion(source, light_impact_range: 1, explosion_cause: source));
        qdel(source);
    }
}
