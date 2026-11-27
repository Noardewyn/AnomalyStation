// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/throwbonus_on_windup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: an item held in throw mode winds up a bonus shown by a bar over its holder, dealt on a hit (the lavaloop fish).
// The var blocks are generated (D111).

// tg: code/datums/components/throwbonus_on_windup.dm:1
public partial class datum_component_throwbonus_on_windup : datum_component
{
    // tg: code/datums/components/throwbonus_on_windup.dm:23
    [DmParams("maximum_bonus", "windup_increment_speed", "pass_maximum_callback", "apply_bonus_callback", "sound_on_success", "effect_on_success", "throw_text")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object maximum_bonus = Dm.arg(__extra, 0) ?? 20;
        object windup_increment_speed = Dm.arg(__extra, 1) ?? 1;
        datum_callback pass_maximum_callback = Dm.arg<datum_callback>(__extra, 2);
        datum_callback apply_bonus_callback = Dm.arg<datum_callback>(__extra, 3);
        object sound_on_success = Dm.arg(__extra, 4);
        object effect_on_success = Dm.arg(__extra, 5);
        object throw_text = Dm.arg(__extra, 6);
        object dot = await base.Initialize(__extra);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.maximum_bonus = Dm.num(maximum_bonus);
        this.pass_maximum_callback = pass_maximum_callback;
        this.apply_bonus_callback = apply_bonus_callback;
        this.sound_on_success = sound_on_success;
        this.effect_on_success = effect_on_success as Type;
        this.windup_increment_speed = windup_increment_speed;
        this.throw_text = throw_text;
        return dot;
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:36
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob_living equipper = null, object slot = null)
    {
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)) || Dm.truthy(holder?.resolve()))
            return;
        holder = WEAKREF(equipper) as datum_weakref;
        RegisterSignal(equipper, COMSIG_LIVING_THROW_MODE_TOGGLE, nameof(throw_change));
        RegisterSignal(equipper, COMSIG_MOB_SWAP_HANDS, nameof(on_hands_swap));
        if (Dm.truthy(equipper.throw_mode))
            start_windup();
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:47
    public virtual void start_windup()
    {
        if (!QDELETED(our_bar))
            CRASH("throwbonus_on_windup component attempted to start windup while already winding up a throw!");

        throwforce_bonus = Dm.initial(this, (datum_component_throwbonus_on_windup p) => p.throwforce_bonus);
        mob_living our_holder = holder?.resolve() as mob_living;
        if (holder == null)
            return;

        if (Dm.truthy(throw_text))
            to_chat(our_holder, span_warning(Dm.str(throw_text)));

        float x_position = CEILING(Dm.num(our_holder.get_visual_width()) * 0.5f, 1);
        our_bar = Dm.@new<obj_effect_overlay_windup_bar>();
        our_bar.maximum_count = maximum_bonus;
        our_bar.pixel_x = x_position;
        Dm.combine(our_holder.vis_contents, our_bar);
        START_PROCESSING(SSfastprocess, this);
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:66
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_move));
        RegisterSignal(parent, COMSIG_MOVABLE_PRE_IMPACT, nameof(on_thrown));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:71
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_MOVABLE_MOVED, COMSIG_MOVABLE_PRE_IMPACT));
        atom our_holder = holder?.resolve() as atom;
        if (our_holder != null)
            UnregisterSignal(our_holder, Dm.list(COMSIG_LIVING_THROW_MODE_TOGGLE, COMSIG_MOB_SWAP_HANDS));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:77
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSfastprocess, this);
        QDEL_NULL(ref our_bar);
        holder = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:83
    [SignalHandler]
    public virtual void throw_change(mob_living source = null, object throw_mode = null)
    {
        if (source.get_active_held_item() != parent)
            return;

        if (Dm.truthy(throw_mode))
            start_windup();
        else
            end_windup();
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:94
    [SignalHandler]
    public virtual void on_hands_swap(mob_living source = null, obj_item swapped_to = null, obj_item swapped_from = null)
    {
        if (source.get_active_held_item() != parent)
        {
            end_windup();
            return;
        }

        if (Dm.truthy(source.throw_mode))
            start_windup();
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:104
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (throwforce_bonus > maximum_bonus)
        {
            mob_living our_holder = holder?.resolve() as mob_living;
            if (pass_maximum_callback != null)
                Waiting.NoSleep(pass_maximum_callback.Invoke(our_holder));
            end_windup();
            return PROCESS_KILL;
        }

        our_bar.recalculate_position(min(throwforce_bonus, maximum_bonus));
        throwforce_bonus += Dm.num(windup_increment_speed);
        return null;
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:114
    [SignalHandler]
    public virtual void on_move(obj_item source = null, atom entering_loc = null)
    {
        end_windup();
        mob_living our_holder = holder?.resolve() as mob_living;
        if (our_holder == null)
            return;
        holder = null;
        UnregisterSignal(our_holder, Dm.list(COMSIG_LIVING_THROW_MODE_TOGGLE, COMSIG_MOB_SWAP_HANDS));
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:123
    public virtual void end_windup()
    {
        QDEL_NULL(ref our_bar);
        STOP_PROCESSING(SSfastprocess, this);
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:127
    [SignalHandler]
    public virtual void on_thrown(datum source = null, atom hit_atom = null, datum_thrownthing throwingdatum = null)
    {
        float damage_to_apply = throwforce_bonus;
        throwforce_bonus = Dm.initial(this, (datum_component_throwbonus_on_windup p) => p.throwforce_bonus);
        if (!isliving(hit_atom))
            return;

        if (apply_bonus_callback != null && !Dm.truthy(Waiting.NoSleep(apply_bonus_callback.Invoke(hit_atom, damage_to_apply))))
            return;

        if (effect_on_success != null)
            Dm.@new(effect_on_success, get_turf(hit_atom));
        if (Dm.truthy(sound_on_success))
            playsound(hit_atom, sound_on_success, 50, TRUE);

        mob_living living_target = hit_atom as mob_living;
        Waiting.NoSleep(living_target.apply_damage(damage_to_apply));
    }
}

// tg: code/datums/components/throwbonus_on_windup.dm:144
public partial class obj_effect_overlay_windup_bar : obj_effect_overlay
{
    // tg: code/datums/components/throwbonus_on_windup.dm:158
    public virtual void recalculate_position(object input_count = null)
    {
        current_count = Dm.num(input_count);
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/datums/components/throwbonus_on_windup.dm:162
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        DmList bar_positions = TgState.Current.obj_effect_overlay_windup_bar__update_overlays__bar_positions ??= Dm.list(0, 2, 4, 6, 8);
        float current_percentage = current_count / maximum_count;
        float bars_to_add = ceil(length(bar_positions) * current_percentage);
        for (float curr_number = 1; curr_number <= bars_to_add; curr_number++)
        {
            string bar_color = null;
            if (curr_number >= 1 && curr_number <= 2)
                bar_color = "windup_red";
            else if (curr_number >= 2 && curr_number <= 4)
                bar_color = "windup_green";
            else if (curr_number >= 4 && curr_number <= 5)
                bar_color = "windup_purple";
            mutable_appearance bar_overlay = mutable_appearance(icon: icon, icon_state: bar_color, layer: ABOVE_HUD_PLANE) as mutable_appearance;
            bar_overlay.pixel_z = Dm.num(bar_positions[(int)curr_number]);
            Dm.append(ref dot, bar_overlay);
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/throwbonus_on_windup.dm:164  update_overlays' var/static/list/bar_positions (rules 10.6)
    internal DmList obj_effect_overlay_windup_bar__update_overlays__bar_positions;
}
