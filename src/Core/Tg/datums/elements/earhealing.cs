// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/earhealing.dm, code/datums/components/earprotection.dm, code/datums/components/wearertargeting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/datums/elements/earhealing.dm:1
public partial class datum_element_earhealing : datum_element
{
    // tg: code/datums/elements/earhealing.dm:5
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isitem(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignals(target, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED), nameof(on_equip));
        return dot;
    }

    // tg: code/datums/elements/earhealing.dm:12
    public override object Detach(datum target = null, params object[] __extra)
    {
        object dot = base.Detach(target, __extra);
        UnregisterSignal(target, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));
        user_by_item.Remove(target);
        return dot;
    }

    // tg: code/datums/elements/earhealing.dm:17
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob user = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_EARS)) && user is mob_living_carbon)
        {
            START_PROCESSING(SSdcs, this);
            user_by_item[source] = user;
        }
        else
            user_by_item.Remove(source);
    }

    // tg: code/datums/elements/earhealing.dm:26
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (object i in Dm.each(user_by_item))
        {
            mob_living_carbon user = user_by_item[i] as mob_living_carbon;
            obj_item_organ_ears ears = user.get_organ_slot(ORGAN_SLOT_EARS) as obj_item_organ_ears;
            if (ears == null || Dm.truthy(Dm.bitand(ears.organ_flags, ORGAN_FAILING)) || Dm.truthy(IS_ROBOTIC_ORGAN(ears)))
                continue;
            ears.adjust_temporary_deafness(-0.5f * SECONDS * Dm.num(seconds_per_tick));
            Waiting.NoSleep(ears.apply_organ_damage(-0.025f * Dm.num(seconds_per_tick)));
        }
        return null;
    }
}

// A dummy parent type used for easily making components that target an item's wearer rather than the item itself.
// tg: code/datums/components/wearertargeting.dm:2
public partial class datum_component_wearertargeting : datum_component
{
    // tg: code/datums/components/wearertargeting.dm:9
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
        return null;
    }

    // tg: code/datums/components/wearertargeting.dm:15
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob equipper = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(valid_slots, slot)) && istype(equipper, mobtype))
            RegisterSignals(equipper, signals, proctype, TRUE);
        else
            UnregisterSignal(equipper, signals);
    }

    // tg: code/datums/components/wearertargeting.dm:22
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        UnregisterSignal(user, signals);
    }
}

///Whenever get_ear_protection() is called, this proc adds 'protection_value' to the wearer's natural ear protection.
// tg: code/datums/components/earprotection.dm:2
public partial class datum_component_wearertargeting_earprotection : datum_component_wearertargeting
{
    // tg: code/datums/components/earprotection.dm:9 (gaps MO1: the named parameter, which the MODsuit's sealed helmet passes)
    [DmParams("protection_amount")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object dot = await base.Initialize(__extra);
        this.protection_amount = Dm.num(Dm.arg(__extra, 0) ?? EAR_PROTECTION_NORMAL);
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_tags));
        return dot;
    }

    // tg: code/datums/components/earprotection.dm:14
    [SignalHandler]
    public virtual void reducebang(datum source = null, DmList reflist = null)
    {
        reflist[EAR_PROTECTION_ARG] = Dm.num(reflist[EAR_PROTECTION_ARG]) + protection_amount;
    }

    // tg: code/datums/components/earprotection.dm:19
    [SignalHandler]
    public virtual void get_examine_tags(atom source = null, mob user = null, DmList examine_list = null)
    {
        if (Dm.eq(protection_amount, EAR_PROTECTION_NORMAL))
            examine_list["sound-proof"] = I18n.T("It protects the ears from flashbangs and other loud noises.");
        else if (protection_amount >= EAR_PROTECTION_HEAVY)
            examine_list["sound-proof"] = I18n.T("It provides [] protection against flashbangs and other loud noises.", Dm.eq(protection_amount, EAR_PROTECTION_FULL) ? "full" : "heavy");
    }
}
