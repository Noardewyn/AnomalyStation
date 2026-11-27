// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/combo_attacks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/datums/components/combo_attacks.dm:1
public partial class datum_component_combo_attacks : datum_component
{
    // tg: code/datums/components/combo_attacks.dm:21
    [DmParams("combos", "examine_message", "reset_message", "max_combo_length", "leniency_time", "can_attack_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object combos = Dm.arg(__extra, 0);
        object examine_message = Dm.arg(__extra, 1);
        object reset_message = Dm.arg(__extra, 2);
        object max_combo_length = Dm.arg(__extra, 3);
        object leniency_time = Dm.arg(__extra, 4) ?? (5 * SECONDS);
        datum_callback can_attack_callback = Dm.arg<datum_callback>(__extra, 5);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        combo_list = combos as DmList;
        foreach (object combo in Dm.each(combo_list))
        {
            DmList combo_specifics = combo_list[combo] as DmList;
            object step_string = english_list(combo_specifics[COMBO_STEPS] as DmList);
            Dm.append(combo_strings, span_notice(Dm.text("<b>[]</b> - []", combo, step_string)));
        }
        this.examine_message = examine_message;
        this.reset_message = reset_message;
        this.max_combo_length = max_combo_length;
        this.leniency_time = leniency_time;
        this.can_attack_callback = can_attack_callback;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/combo_attacks.dm:35
    public override object Destroy(float? force = null)
    {
        can_attack_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/combo_attacks.dm:39
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE_MORE, nameof(on_examine_more));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_SELF, nameof(on_attack_self));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(on_attack));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/combo_attacks.dm:46
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_EXAMINE_MORE, COMSIG_ITEM_ATTACK_SELF, COMSIG_ITEM_DROPPED, COMSIG_ITEM_ATTACK));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/combo_attacks.dm:49
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (!Dm.truthy(examine_message))
            return;
        Dm.append(examine_list, examine_message);
    }

    // tg: code/datums/components/combo_attacks.dm:56
    [SignalHandler]
    public virtual void on_examine_more(datum source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(examine_list, combo_strings);
    }

    // tg: code/datums/components/combo_attacks.dm:61
    [SignalHandler]
    public virtual void on_attack_self(obj_item source = null, mob user = null)
    {
        reset_inputs(user, deltimer: TRUE);
    }

    // tg: code/datums/components/combo_attacks.dm:66
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob dropper = null)
    {
        reset_inputs(user: null, deltimer: TRUE);
    }

    // tg: code/datums/components/combo_attacks.dm:71
    public virtual object check_input(mob_living target = null, mob user = null)
    {
        foreach (object combo in Dm.each(combo_list))
        {
            DmList combo_specifics = combo_list[combo] as DmList;
            if (Dm.truthy(compare_list(input_list, combo_specifics[COMBO_STEPS] as DmList)))
            {
                Waiting.Detach(Dm.Call(parent, (string)combo_specifics[COMBO_PROC], target, user));
                return TRUE;
            }
        }
        return FALSE;
    }

    // tg: code/datums/components/combo_attacks.dm:79
    public virtual void reset_inputs(mob user = null, object deltimer = null)
    {
        atom atom_parent = parent as atom;
        input_list.Cut();
        if (user != null)
            atom_parent.balloon_alert(user, reset_message);
        if (Dm.truthy(deltimer) && Dm.truthy(timerid))
            GlobalProcs.deltimer(timerid);
    }

    // tg: code/datums/components/combo_attacks.dm:87
    [SignalHandler]
    public virtual object on_attack(datum source = null, mob_living target = null, mob user = null, DmList modifiers = null)
    {
        if (can_attack_callback != null && !Dm.truthy(Waiting.NoSleep(can_attack_callback.Invoke(user, target))))
            return NONE;
        if (HAS_TRAIT(user, TRAIT_PACIFISM))
            return NONE;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            Dm.append(input_list, RIGHT_ATTACK);
        if (Dm.truthy(LAZYACCESS(modifiers, LEFT_CLICK)))
            Dm.append(input_list, LEFT_ATTACK);
        if (Dm.num(length(input_list)) > Dm.num(max_combo_length))
            reset_inputs(user, deltimer: TRUE);
        if (Dm.truthy(check_input(target, user)))
        {
            reset_inputs(user: null, deltimer: TRUE);
            return COMPONENT_SKIP_ATTACK;
        }
        if (Dm.truthy(leniency_time))
            timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(reset_inputs), user, FALSE), Dm.num(leniency_time), Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), TIMER_STOPPABLE), file: "code/datums/components/combo_attacks.dm", line: 104);
        return NONE;
    }
}
