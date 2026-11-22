// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/shielded.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps RD4, D333: the necrotech relics' shields): the shielded item that blocks its wearer's hits while it has charges,
// drawing its shield over the wearer (its alpha the charge left), recharging after a quiet spell, and running its hit callback (the
// sparks and message, or its owner's own). The var block is generated (D111).

/**
 * The shielded component causes the parent item to nullify a certain number of attacks against the wearer, see: shielded vests.
 */
// tg: code/datums/components/shielded.dm:5
public partial class datum_component_shielded : datum_component
{
    // tg: code/datums/components/shielded.dm:41
    [DmParams("max_charges", "recharge_start_delay", "charge_increment_delay", "charge_recovery", "lose_multiple_charges", "show_charge_as_alpha", "recharge_path", "can_block_overwhelming", "starting_charges", "shield_icon_file", "shield_icon", "shield_inhand", "run_hit_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await DmTask.CompletedTask;
        object max_charges = Dm.arg(__extra, 0) ?? 3;
        object recharge_start_delay = Dm.arg(__extra, 1) ?? 20 * SECONDS;
        object charge_increment_delay = Dm.arg(__extra, 2) ?? 1 * SECONDS;
        object charge_recovery = Dm.arg(__extra, 3) ?? 1;
        object lose_multiple_charges = Dm.arg(__extra, 4) ?? FALSE;
        object show_charge_as_alpha = Dm.arg(__extra, 5) ?? FALSE;
        object recharge_path = Dm.arg(__extra, 6);
        object can_block_overwhelming = Dm.arg(__extra, 7) ?? FALSE;
        object starting_charges = Dm.arg(__extra, 8);
        object shield_icon_file = Dm.arg(__extra, 9) ?? Dm.resource("icons/effects/effects.dmi");
        object shield_icon = Dm.arg(__extra, 10) ?? "shield-old";
        object shield_inhand = Dm.arg(__extra, 11) ?? FALSE;
        var run_hit_callback = Dm.arg(__extra, 12) as datum_callback;

        if (!isitem(parent) || Dm.num(max_charges) <= 0)
            return COMPONENT_INCOMPATIBLE;

        this.max_charges = max_charges;
        this.recharge_start_delay = Dm.num(recharge_start_delay);
        this.charge_increment_delay = Dm.num(charge_increment_delay);
        this.charge_recovery = Dm.num(charge_recovery);
        this.lose_multiple_charges = Dm.num(lose_multiple_charges);
        this.show_charge_as_alpha = Dm.num(show_charge_as_alpha);
        this.recharge_path = recharge_path;
        this.can_block_overwhelming = Dm.num(can_block_overwhelming);
        this.shield_icon_file = shield_icon_file;
        this.shield_icon = Dm.str(shield_icon);
        this.shield_inhand = Dm.num(shield_inhand);
        this.on_hit_effects = run_hit_callback ?? Dm.@new<datum_callback>(this, nameof(default_run_hit_callback));
        if (starting_charges == null)
            current_charges = max_charges;
        else
            current_charges = starting_charges;
        if (Dm.truthy(this.recharge_start_delay))
            START_PROCESSING(SSdcs, this);
        return null;
    }

    // tg: code/datums/components/shielded.dm:65
    public override object Destroy(float? force = null)
    {
        if (wearer != null)
        {
            shield_icon = "broken";
            UnregisterSignal(wearer, COMSIG_ATOM_UPDATE_OVERLAYS);
            wearer.update_appearance(UPDATE_ICON);
            wearer = null;
        }
        on_hit_effects = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/shielded.dm:74
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equipped));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(lost_wearer));
        RegisterSignal(parent, COMSIG_ITEM_HIT_REACT, nameof(on_hit_react));
        var shield = (atom)parent;
        if (ismob(shield.loc))
        {
            var holder = (mob)shield.loc;
            if (Dm.truthy(holder.is_holding(parent as obj_item)) && !Dm.truthy(shield_inhand))
                return;
            await set_wearer(holder);
        }
    }

    // tg: code/datums/components/shielded.dm:85
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED, COMSIG_ITEM_HIT_REACT));
        var shield = (atom)parent;
        if (shield.loc == wearer)
            lost_wearer(this, wearer);
    }

    // Handle recharging, if we want to
    // tg: code/datums/components/shielded.dm:92
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.num(current_charges) >= Dm.num(max_charges))
        {
            STOP_PROCESSING(SSdcs, this);
            return null;
        }

        // COOLDOWN_FINISHED(src, recently_hit_cd) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(recently_hit_cd <= world.time))
            return null;
        // COOLDOWN_FINISHED(src, charge_add_cd)
        if (!(charge_add_cd <= world.time))
            return null;

        var item_parent = (obj_item)parent;
        charge_add_cd = world.time + charge_increment_delay; // COOLDOWN_START(src, charge_add_cd, charge_increment_delay)
        adjust_charge(charge_recovery); // set the number of charges to current + recovery per increment, clamped from zero to max_charges
        playsound(item_parent, Dm.resource("sound/effects/magic/charge.ogg"), 50, TRUE);
        if (Dm.eq(current_charges, max_charges))
            playsound(item_parent, Dm.resource("sound/machines/ding.ogg"), 50, TRUE);
        return null;
    }

    // tg: code/datums/components/shielded.dm:109
    public virtual void adjust_charge(object change = null)
    {
        current_charges = clamp(Dm.num(current_charges) + Dm.num(change), 0, Dm.num(max_charges));
        if (wearer != null)
            wearer.update_appearance(UPDATE_ICON);
    }

    /// Check if we've been equipped to a valid slot to shield
    // tg: code/datums/components/shielded.dm:115
    [SignalHandler]
    public virtual void on_equipped(datum source = null, mob user = null, object slot = null)
    {
        if (Dm.truthy(user.is_holding(parent as obj_item)) && !Dm.truthy(shield_inhand))
        {
            lost_wearer(source, user);
            return;
        }
        Waiting.NoSleep(set_wearer(user));
    }

    /// Either we've been dropped or our wearer has been QDEL'd. Either way, they're no longer our problem
    // tg: code/datums/components/shielded.dm:124
    [SignalHandler]
    public virtual void lost_wearer(datum source = null, mob user = null)
    {
        if (wearer != null)
        {
            UnregisterSignal(wearer, Dm.list(COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_QDELETING));
            wearer.update_appearance(UPDATE_ICON);
            wearer = null;
        }
    }

    // tg: code/datums/components/shielded.dm:132
    public virtual async DmTask set_wearer(mob user = null)
    {
        if (wearer == user)
            return;
        if (wearer != null)
            throw Dm.CRASH(Dm.text("[] called set_wearer with [] but [] was already the wearer!", type, user, wearer), "code/datums/components/shielded.dm", 136);

        wearer = user as mob_living;
        RegisterSignal(wearer, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(wearer, COMSIG_QDELETING, nameof(lost_wearer));
        if (Dm.truthy(current_charges))
            wearer.update_appearance(UPDATE_ICON);
    }

    /// Used to draw the shield overlay on the wearer
    // tg: code/datums/components/shielded.dm:146
    [SignalHandler]
    public virtual void on_update_overlays(atom parent_atom = null, DmList overlays = null)
    {
        var shield_appearance = mutable_appearance(shield_icon_file, Dm.num(current_charges) > 0 ? shield_icon : "broken", MOB_SHIELD_LAYER) as mutable_appearance;
        if (Dm.truthy(show_charge_as_alpha))
            shield_appearance.alpha = Dm.num(current_charges) / Dm.num(max_charges) * 255;
        Dm.append(ref overlays, shield_appearance);
    }

    /**
     * This proc fires when we're hit, and is responsible for checking if we're charged, then deducting one + returning that we're blocking if so.
     * It then runs the callback in [/datum/component/shielded/var/on_hit_effects] which handles the messages/sparks (so the visuals)
     */
    // tg: code/datums/components/shielded.dm:158
    [SignalHandler]
    public virtual object on_hit_react(datum source = null, mob_living_carbon_human owner = null, atom_movable hitby = null, object attack_text = null, object final_block_chance = null, object damage = null, object attack_type = null, object damage_type = null)
    {
        object dot = null;
        recently_hit_cd = world.time + recharge_start_delay; // COOLDOWN_START(src, recently_hit_cd, recharge_start_delay)

        //No wearer? No block.
        if (wearer == null)
            return dot;

        //if our wearer isn't the owner of the block, don't block
        if (owner != wearer)
            return dot;

        if (Dm.num(current_charges) <= 0)
            return dot;

        // if our shield is unable to block OVERWHELMING_ATTACK type attacks, we just let it pass.
        if (Dm.eq(attack_type, OVERWHELMING_ATTACK) && !Dm.truthy(can_block_overwhelming))
            return dot;

        dot = COMPONENT_HIT_REACTION_BLOCK;

        object charge_loss = 1; // how many charges do we lose

        if (Dm.truthy(lose_multiple_charges)) // if the shield has health like damage we'll lose charges equal to the damage of the hit
            charge_loss = damage;
        else if (Dm.num(damage) < 3)
            charge_loss = 0;
        else if (Dm.eq(attack_type, OVERWHELMING_ATTACK) && !Dm.truthy(lose_multiple_charges)) // Always expend all charges when blocking an overwhelming attack unless we're using our shield like health.
            charge_loss = max_charges;

        adjust_charge(-Dm.num(charge_loss));

        Waiting.Detach(actually_run_hit_callback(owner, attack_text, current_charges)); // INVOKE_ASYNC(src, PROC_REF(actually_run_hit_callback), ...)

        if (!Dm.truthy(recharge_start_delay)) // if recharge_start_delay is 0, we don't recharge
            return dot;

        START_PROCESSING(SSdcs, this); // if we DO recharge, start processing so we can do that
        return dot;
    }

    /// The wrapper to invoke the on_hit callback, so we don't have to worry about blocking in the signal handler
    // tg: code/datums/components/shielded.dm:197
    public virtual async DmTask actually_run_hit_callback(mob_living owner = null, object attack_text = null, object current_charges = null)
    {
        await on_hit_effects.Invoke(owner, attack_text, current_charges);
    }

    /// Default on_hit proc, since cult robes are stupid and have different descriptions/sparks
    // tg: code/datums/components/shielded.dm:201
    public virtual void default_run_hit_callback(mob_living owner = null, object attack_text = null, object current_charges = null)
    {
        do_sparks(2, TRUE, owner);
        owner.visible_message(span_danger(I18n.T("[]'s shields deflect [] in a shower of sparks!", owner, attack_text)));
        if (Dm.num(current_charges) <= 0)
            owner.visible_message(span_warning(I18n.T("[]'s shield overloads!", owner)));
    }
}
