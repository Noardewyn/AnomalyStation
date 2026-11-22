// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/nullrod_core.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

///Proxy element that attaches components, elements and traits that are common to more or less all nullrods.
// tg: code/datums/elements/nullrod_core.dm:2
public partial class datum_element_nullrod_core : datum_element
{
    /**
     * Called when the element is added to a datum. If the 'chaplain_spawnable' arg is TRUE and unit testing is enabled,
     * we check that the target is actually in the nullrod_variants global list
     */
    // tg: code/datums/elements/nullrod_core.dm:8
    [DmParams("target", "chaplain_spawnable", "rune_remove_line")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (target is not obj_item)
            return ELEMENT_INCOMPATIBLE;

        await target.AddComponent(typeof(datum_component_anti_magic), Dm.bitor(MAGIC_RESISTANCE, MAGIC_RESISTANCE_HOLY));
        Dm.Deferred("code/datums/elements/nullrod_core.dm:14", "Attach: the effect_remover (the runes), cult_kill_tracker and bane (the spirits) components: the cult and the spirits are not ported");
        ADD_TRAIT(target, TRAIT_NULLROD_ITEM, ELEMENT_TRAIT(type));
        return dot;
    }

    /// Callback for effect remover, invoked when a cult rune is cleared
    // tg: code/datums/elements/nullrod_core.dm:32
    public virtual void on_cult_rune_removed(obj_item nullrod = null, obj_effect target = null, mob_living user = null)
    {
        Dm.Deferred("code/datums/elements/nullrod_core.dm:32", "on_cult_rune_removed: the cult's runes (not ported)");
    }
}
