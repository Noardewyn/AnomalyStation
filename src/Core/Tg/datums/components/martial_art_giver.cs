// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/martial_art_giver.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X3a (D316): an item that teaches its wearer a martial art (the carp gloves), whole.

/// when equipped and unequipped this item gives a martial art
// tg: code/datums/components/martial_art_giver.dm:2
public partial class datum_component_martial_art_giver : datum_component
{
    // tg: code/datums/components/martial_art_giver.dm:6
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        Type style_type = Dm.arg<Type>(__extra, 0);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        style = Dm.@new(style_type, this) as datum_martial_art;
        return null;
    }

    // tg: code/datums/components/martial_art_giver.dm:12
    public override object Destroy(float? force = null)
    {
        var item = parent as obj_item;
        if (item?.loc is mob_living wearer)
            Waiting.NoSleep(style.unlearn(wearer));
        QDEL_NULL(ref style);
        return base.Destroy(force);
    }

    // tg: code/datums/components/martial_art_giver.dm:20
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(equipped));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(dropped));

        var item = parent as obj_item;
        if (item.loc is mob_living wearer)
            equipped(item, wearer, wearer.get_slot_by_item(item));
    }

    // tg: code/datums/components/martial_art_giver.dm:29
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
    }

    // tg: code/datums/components/martial_art_giver.dm:32
    [SignalHandler]
    public virtual void equipped(obj_item source = null, mob user = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(source.slot_flags, slot)))
            return;
        Waiting.NoSleep(style.teach(user as mob_living));
    }

    // tg: code/datums/components/martial_art_giver.dm:38
    [SignalHandler]
    public virtual void dropped(obj_item source = null, mob user = null)
    {
        Waiting.NoSleep(style.unlearn(user as mob_living));
    }
}
