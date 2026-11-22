// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/onwear_mood.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the fresh laundry's mood while the washed jumpsuit is worn; the mood event itself waits for
// moods, add_mood_event). The var block is generated (D111).

/// Add to clothing to give the wearer a mood buff and a unique examine text
// tg: code/datums/components/onwear_mood.dm:2
public partial class datum_component_onwear_mood : datum_component
{
    // tg: code/datums/components/onwear_mood.dm:10
    [DmParams("saved_event_type", "examine_string", "slot_equip")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object saved_event_type = Dm.arg(__extra, 0);
        object examine_string = Dm.arg(__extra, 1);
        object slot_equip = Dm.arg(__extra, 2) ?? ITEM_SLOT_ON_BODY;

        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.saved_event_type = saved_event_type as Type;
        this.examine_string = examine_string;
        if (!isnum(slot_equip))
        {
            stack_trace(Dm.text("Attempted to initialize onwear component with improper slot_equip []", slot_equip), "code/datums/components/onwear_mood.dm", 18);
            slot_equip = ITEM_SLOT_ON_BODY;
        }
        this.slot_equip = slot_equip;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/onwear_mood.dm:22
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(affect_wearer));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/onwear_mood.dm:26
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ATOM_EXAMINE));
        clear_effects();
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/onwear_mood.dm:30
    [SignalHandler]
    public virtual void affect_wearer(datum source = null, mob_living target = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(slot, slot_equip)))
            return; // only affects "worn" slots by default

        Waiting.NoSleep(target.add_mood_event(REF(this), saved_event_type));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(target, COMSIG_MOB_UNEQUIPPED_ITEM, nameof(clear_effects));
    }

    // tg: code/datums/components/onwear_mood.dm:39
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_text = null)
    {
        Dm.append(ref examine_text, span_notice(Dm.str(examine_string)));
    }

    /// clears the effects on the wearer
    // tg: code/datums/components/onwear_mood.dm:44
    [SignalHandler]
    public virtual void clear_effects(mob_living source = null, obj_item dropped_item = null)
    {
        obj_item clothing = parent as obj_item;
        // if called from a signal, check clothing
        if (dropped_item != null && dropped_item != clothing)
            return;
        source ??= clothing.loc as mob_living;
        if (!(source is mob_living))
            return;
        UnregisterSignal(source, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_MOB_UNEQUIPPED_ITEM));
        source.clear_mood_event(REF(this));
    }
}
