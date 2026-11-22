// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/stacking_effect.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice B: the whole file (the saline glucose dilution stacks), but the stack overlay of an effect with an overlay_file, which
// sizes itself by get_cached_height (the atom HUDs, M8 slice F; no M8 stacking effect has one). The var block is generated.

// tg: code/datums/status_effects/stacking_effect.dm:4
public partial class datum_status_effect_stacking : datum_status_effect
{
    // tg: code/datums/status_effects/stacking_effect.dm:34
    public override object Destroy(float? force = null)
    {
        if (!Dm.truthy(owner) || status_overlay == null)
            return base.Destroy(force);

        owner.cut_overlay(status_overlay);
        QDEL_NULL(ref status_overlay);
        return base.Destroy(force);
    }

    /// Effects that occur when the stack count crosses stack_threshold
    // tg: code/datums/status_effects/stacking_effect.dm:42
    public virtual void threshold_cross_effect()
    {
        return;
    }

    /// Effects that occur if the status effect is removed due to the stack_threshold being crossed
    // tg: code/datums/status_effects/stacking_effect.dm:46
    public virtual void stacks_consumed_effect()
    {
        return;
    }

    /// Effects that occur if the status is removed due to being under 1 remaining stack
    // tg: code/datums/status_effects/stacking_effect.dm:50
    public virtual void fadeout_effect()
    {
        return;
    }

    /// Runs every time tick(), causes stacks to decay over time
    // tg: code/datums/status_effects/stacking_effect.dm:54
    public virtual void stack_decay_effect()
    {
        return;
    }

    /// Called when the stack_threshold is crossed (stacks go over the threshold)
    // tg: code/datums/status_effects/stacking_effect.dm:58
    public virtual void on_threshold_cross()
    {
        threshold_cross_effect();
        if (Dm.truthy(consumed_on_threshold))
        {
            stacks_consumed_effect();
            qdel(this);
        }
    }

    /// Called when the stack_threshold is uncrossed / dropped (stacks go under the threshold after being over it)
    // tg: code/datums/status_effects/stacking_effect.dm:65
    public virtual void on_threshold_drop()
    {
        return;
    }

    /// Whether the owner can have the status effect.
    /// Return FALSE if the owner is not in a valid state (self-deletes the effect), or TRUE otherwise
    // tg: code/datums/status_effects/stacking_effect.dm:70
    public virtual object can_have_status()
    {
        return owner.stat != DEAD;
    }

    /// Whether the owner can currently gain stacks or not
    /// Return FALSE if the owner is not in a valid state, or TRUE otherwise
    // tg: code/datums/status_effects/stacking_effect.dm:75
    public virtual object can_gain_stacks()
    {
        return owner.stat != DEAD;
    }

    // tg: code/datums/status_effects/stacking_effect.dm:78
    [DmParams("new_owner", "stacks_to_apply")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object stacks_to_apply = Dm.arg(__extra, 0);
        object dot = await base.on_creation(new_owner, __extra);
        if (Dm.truthy(dot))
            add_stacks(stacks_to_apply);
        return dot;
    }

    // tg: code/datums/status_effects/stacking_effect.dm:83
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!Dm.truthy(can_have_status()))
            return FALSE;
        if (!Dm.truthy(overlay_file) || !Dm.truthy(overlay_state))
            return await base.on_apply(__extra);
        // status_overlay = mutable_appearance(overlay_file, "[overlay_state][stacks]") sized by owner.get_cached_height() (the atom
        // HUDs' /atom proc, game/data_huds.dm, M8 slice F); no M8 stacking effect has an overlay_file
        Dm.NotTranslated("code/datums/status_effects/stacking_effect.dm:88", "on_apply: the stack overlay (get_cached_height: the atom HUDs, M8 slice F)");
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/stacking_effect.dm:96
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!Dm.truthy(can_have_status()))
        {
            qdel(this);
        }
        else
        {
            add_stacks(-stack_decay);
            stack_decay_effect();
        }
        return null;
    }

    /// Add (or remove) [stacks_added] stacks to our current stack count.
    // tg: code/datums/status_effects/stacking_effect.dm:104
    public virtual object add_stacks(object stacks_added = null)
    {
        float added = Dm.num(stacks_added);
        if (added > 0 && !Dm.truthy(can_gain_stacks()))
            return FALSE;
        if (status_overlay != null)
            owner.cut_overlay(status_overlay);
        stacks = min(stacks + added, Dm.num(max_stacks));
        if (stacks <= 0)
        {
            fadeout_effect();
            qdel(this); // deletes status if stacks fall under one
            return null;
        }

        if (stacks >= Dm.num(stack_threshold) && !Dm.truthy(threshold_crossed)) // threshold_crossed check prevents threshold effect from occurring if changing from above threshold to still above threshold
        {
            threshold_crossed = TRUE;
            on_threshold_cross();
            if (Dm.truthy(consumed_on_threshold))
                return null;
        }
        else if (stacks < Dm.num(stack_threshold) && Dm.truthy(threshold_crossed))
        {
            threshold_crossed = FALSE; // resets threshold effect if we fall below threshold so threshold effect can trigger again
            on_threshold_drop();
        }

        if (added > 0)
            time_until_next_tick = Dm.add(time_until_next_tick, delay_before_decay); // refreshes time until decay

        if (status_overlay != null)
        {
            status_overlay.icon_state = Dm.text("[][]", overlay_state, stacks);
            owner.add_overlay(status_overlay);
        }
        return null;
    }
}
