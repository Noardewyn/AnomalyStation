// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/jousting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * ##jousting
 *
 * Given to items, it allows you to charge into people with additional damage and potential knockdown
 * by being buckled onto something. If the other person is also jousting, can knock eachother down.
 */
// tg: code/datums/components/jousting.dm:10
public partial class datum_component_jousting : datum_component
{
    ///Amount of time each timer has, used to reset the jousting back, indicating that the person has stopped moving.
    // tg: code/datums/components/jousting.dm:2  #define MOVEMENT_RESET_COOLDOWN_TIME (0.3 SECONDS)
    private const float MOVEMENT_RESET_COOLDOWN_TIME = 0.3f * SECONDS;

    // tg: code/datums/components/jousting.dm:29
    [DmParams("damage_boost_per_tile", "knockdown_chance_per_tile", "knockdown_time", "max_tile_charge", "min_tile_charge")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object damage_boost_per_tile = Dm.arg(__extra, 0) ?? 2;
        object knockdown_chance_per_tile = Dm.arg(__extra, 1) ?? 20;
        object knockdown_time = Dm.arg(__extra, 2) ?? 2 * SECONDS;
        object max_tile_charge = Dm.arg(__extra, 3) ?? 5;
        object min_tile_charge = Dm.arg(__extra, 4) ?? 2;
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        this.damage_boost_per_tile = damage_boost_per_tile;
        this.knockdown_chance_per_tile = knockdown_chance_per_tile;
        this.knockdown_time = knockdown_time;
        this.max_tile_charge = max_tile_charge;
        this.min_tile_charge = min_tile_charge;

        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equip));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_drop));
        RegisterSignal(parent, COMSIG_ITEM_AFTERATTACK, nameof(on_successful_attack));
        RegisterSignal(parent, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return null;
    }

    // tg: code/datums/components/jousting.dm:50
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_EXAMINE,
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_DROPPED,
            COMSIG_ITEM_AFTERATTACK,
            COMSIG_TRANSFORMING_ON_TRANSFORM));
    }

    // tg: code/datums/components/jousting.dm:60
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        examine_list.Add(span_notice(I18n.T("It can be used on a vehicle for jousting, dealing potential knockdowns and additional damage.")));
    }

    // tg: code/datums/components/jousting.dm:64
    [SignalHandler]
    public virtual void on_transform(obj_item source = null, mob user = null, object active = null)
    {
        if (user == null)
            return;

        if (Dm.truthy(active))
            Waiting.DetachSync(() => on_equip(source, user)); // INVOKE_ASYNC(src, PROC_REF(on_equip), source, user)
        else
            Waiting.DetachSync(() => on_drop(source, user)); // INVOKE_ASYNC(src, PROC_REF(on_drop), source, user)
    }

    ///Called when a mob equips the spear, registers them as the holder and checks their signals for moving.
    // tg: code/datums/components/jousting.dm:75
    [SignalHandler]
    public virtual void on_equip(datum source = null, mob user = null, object slot = null)
    {
        if (current_holder != null)
            Waiting.DetachSync(() => on_drop(source, user)); // INVOKE_ASYNC(src, PROC_REF(on_drop), source, user)

        current_holder = user as mob_living;
        RegisterSignal(current_holder, COMSIG_MOVABLE_MOVED, nameof(mob_move), TRUE);
    }

    // tg: code/datums/components/jousting.dm:83
    [SignalHandler]
    public virtual void on_drop(datum source = null, mob user = null)
    {
        if (current_holder == null)
            return;

        reset_charge();
        UnregisterSignal(current_holder, COMSIG_MOVABLE_MOVED);
        current_holder = null;
    }

    /**
     * Performs the actual attack, handling damage/knockdown depending on how far you've jousted.
     * We deduct the minimum tile charge from the current tile charge to get what will actually be buffed
     * So your charge will only get benefits from each extra tile after the minimum (and before the maximum).
     */
    // tg: code/datums/components/jousting.dm:97
    [SignalHandler]
    public virtual void on_successful_attack(datum source = null, mob_living target = null, mob user = null)
    {
        if (user != current_holder || ((mob_living)user).buckled == null)
            return;
        float usable_charge = current_tile_charge - Dm.num(min_tile_charge);
        if (!Dm.truthy(current_direction) || usable_charge <= 0)
            return;

        turf target_turf = get_step(user, current_direction) as turf;
        if (Dm.@in(target, Builtins.range(1, target_turf)))
        {
            obj_item parent_item = (obj_item)parent;
            object sharp = parent_item.get_sharpness();
            string msg = I18n.T("[] [] [] [] their []", user, Dm.truthy(sharp) ? I18n.T("impales") : I18n.T("slams into"), target, Dm.truthy(sharp) ? I18n.T("on") : I18n.T("with"), parent);
            Waiting.NoSleep(target.apply_damage(Dm.num(damage_boost_per_tile) * usable_charge, BRUTE, user.zone_selected, 0));
            if (prob(Dm.num(knockdown_chance_per_tile) * usable_charge))
            {
                msg += I18n.T(" and knocks [] []", target, target.buckled != null ? I18n.T("off of []", target.buckled) : I18n.T("down"));
                if (target.buckled != null)
                    Waiting.NoSleep(target.buckled.unbuckle_mob(target));
                Waiting.NoSleep(target.Paralyze(knockdown_time));
            }
            user.visible_message(span_danger(Dm.text("[]!", msg)));
        }
    }

    /**
     * Called when a mob moves.
     * Handles checking their direction, changing it if they turned,
     * and increments how many tiles they've been charging for.
     * Lastly, refreshes their charge reset timer, giving them a new one instead.
     */
    // tg: code/datums/components/jousting.dm:124
    [SignalHandler]
    public virtual void mob_move(datum source = null, object newloc = null, object dir = null)
    {
        if (current_holder == null)
            throw Dm.CRASH(Dm.text("[] called mob_move despite supposedly not having a mob registed to joust as.", this), "code/datums/components/jousting.dm", 128);
        if (current_holder.buckled == null)
            return;

        if (!Dm.eq(dir, current_direction))
        {
            current_tile_charge = Dm.num(Dm.initial(this, nameof(current_tile_charge)));
            current_direction = Dm.num(dir);
        }
        if (current_tile_charge < Dm.num(max_tile_charge))
            current_tile_charge++;
        _addtimer(Dm.@new<datum_callback>(this, nameof(reset_charge)), MOVEMENT_RESET_COOLDOWN_TIME, Dm.bitor(TIMER_UNIQUE, TIMER_OVERRIDE), file: "code/datums/components/jousting.dm", line: 137);
    }

    /**
     * reset charge
     *
     * Resets their direction and tile charge back to their initial values.
     * This is used when someone is no longer jousting and it should cleanup.
     */
    // tg: code/datums/components/jousting.dm:145
    public virtual void reset_charge()
    {
        current_direction = Dm.num(Dm.initial(this, nameof(current_direction)));
        current_tile_charge = Dm.num(Dm.initial(this, nameof(current_tile_charge)));
    }
}
