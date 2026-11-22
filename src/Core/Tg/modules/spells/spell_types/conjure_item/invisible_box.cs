// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/conjure_item/invisible_box.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/spells/spell_types/conjure_item/invisible_box.dm:3
public partial class datum_action_cooldown_spell_conjure_item_invisible_box : datum_action_cooldown_spell_conjure_item
{
    // tg: code/modules/spells/spell_types/conjure_item/invisible_box.dm:28
    public override object make_item(atom caster = null)
    {
        object dot = base.make_item(caster);
        obj_item made_box = dot as obj_item;
        made_box.alpha = 255;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cleanup_box), made_box), box_lifespan, file: "code/modules/spells/spell_types/conjure_item/invisible_box.dm", line: 32);
        return dot;
    }

    /// Callback that gets rid out of box and removes the weakref from our list
    // tg: code/modules/spells/spell_types/conjure_item/invisible_box.dm:35
    public virtual async DmTask cleanup_box(obj_item_storage_box box = null)
    {
        if (QDELETED(box) || box == null)
            return;

        await box.emptyStorage();
        LAZYREMOVE(ref item_refs, WEAKREF(box));
        qdel(box);
    }
}
