// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/spill.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the spaghetti in a pocket. The var block is generated (D111).

// This component is for forcing strange things into your pocket that fall out if you fall down
// Yes this exists purely for the spaghetti meme

// tg: code/datums/components/spill.dm:4
public partial class datum_component_spill : datum_component
{
    // droptext is an arglist for visible_message
    // dropsound is a list of potential sounds that gets picked from
    // tg: code/datums/components/spill.dm:14
    [DmParams("_droptext", "_dropsound", "_drop_memory")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object _droptext = Dm.arg(__extra, 0);
        object _dropsound = Dm.arg(__extra, 1);
        object _drop_memory = Dm.arg(__extra, 2);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        if (Dm.truthy(_droptext) && !islist(_droptext))
            _droptext = Dm.list(_droptext);
        droptext = _droptext as DmList;

        if (Dm.truthy(_dropsound) && !islist(_dropsound))
            _dropsound = Dm.list(_dropsound);

        dropsound = _dropsound as DmList;
        drop_memory = _drop_memory;
        return null;
    }

    // tg: code/datums/components/spill.dm:28
    public override async DmTask<object> PostTransfer(datum new_parent = null)
    {
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    // tg: code/datums/components/spill.dm:32
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(equip_react));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(drop_react));
        obj_item master = (obj_item)parent;
        preexisting_slot_flags = master.slot_flags;
        Dm.combine(ref master.slot_flags, ITEM_SLOT_POCKETS);
    }

    // tg: code/datums/components/spill.dm:39
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        obj_item master = (obj_item)parent;
        if (!Dm.truthy(Dm.bitand(preexisting_slot_flags, ITEM_SLOT_POCKETS)))
            Dm.mask(ref master.slot_flags, Dm.bitnot(ITEM_SLOT_POCKETS));
    }

    // tg: code/datums/components/spill.dm:45
    [SignalHandler]
    public virtual void equip_react(obj_item source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, Dm.bitor(ITEM_SLOT_LPOCKET, ITEM_SLOT_RPOCKET))))
            RegisterSignal(equipper, COMSIG_LIVING_STATUS_KNOCKDOWN, nameof(knockdown_react), TRUE);
        else
            UnregisterSignal(equipper, COMSIG_LIVING_STATUS_KNOCKDOWN);
    }

    // tg: code/datums/components/spill.dm:53
    [SignalHandler]
    public virtual void drop_react(obj_item source = null, mob dropper = null)
    {
        UnregisterSignal(dropper, COMSIG_LIVING_STATUS_KNOCKDOWN);
    }

    // tg: code/datums/components/spill.dm:58
    [SignalHandler]
    public virtual void knockdown_react(mob_living fool = null, object amount = null)
    {
        if (Dm.num(amount) <= 0)
            return;

        obj_item master = (obj_item)parent;
        Waiting.NoSleep(fool.dropItemToGround(master));
        if (Dm.truthy(droptext))
            Waiting.NoSleep(Dm.Call(fool, nameof(mob.visible_message), Dm.arglist(droptext)));
        if (Dm.truthy(dropsound))
            playsound(master, pick(dropsound), 30);
        if (Dm.truthy(drop_memory))
            fool.add_mob_memory(drop_memory);
    }
}
