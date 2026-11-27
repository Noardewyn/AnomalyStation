// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/basic_ranged_ready_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Fade in an overlay x seconds after a basic mob makes a ranged attack
 * Indicates that it will be ready to fire again
 */
// tg: code/datums/components/basic_ranged_ready_overlay.dm:5
public partial class datum_component_basic_ranged_ready_overlay : datum_component
{
    // tg: code/datums/components/basic_ranged_ready_overlay.dm:13
    [DmParams("overlay_state", "display_after")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object overlay_state = Dm.arg(__extra, 0);
        object display_after = Dm.arg(__extra, 1);
        overlay_state ??= "";
        display_after ??= 2.5f * SECONDS;
        object dot = await base.Initialize(Dm.forward(__extra, overlay_state, display_after));
        if (!isbasicmob(parent))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(overlay_state))
            throw Dm.CRASH("Attempted to assign basic ranged ready overlay with a null or empty overlay state", "code/datums/components/basic_ranged_ready_overlay.dm", 18);
        this.overlay_state = overlay_state;
        this.display_after = display_after;
        restore_overlay(parent as mob_living_basic);
        return dot;
    }

    // tg: code/datums/components/basic_ranged_ready_overlay.dm:23
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        RegisterSignal(parent, COMSIG_BASICMOB_POST_ATTACK_RANGED, nameof(on_ranged_attack));
        RegisterSignal(parent, COMSIG_MOB_STATCHANGE, nameof(on_stat_changed));
    }

    // tg: code/datums/components/basic_ranged_ready_overlay.dm:28
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_BASICMOB_POST_ATTACK_RANGED, COMSIG_LIVING_REVIVE));
        await base.UnregisterFromParent(__extra);
    }

    // tg: code/datums/components/basic_ranged_ready_overlay.dm:32
    public override object Destroy(float? force = null)
    {
        deltimer(waiting_timer);
        return base.Destroy(force);
    }

    /// When we shoot, get rid of our overlay and queue its return
    // tg: code/datums/components/basic_ranged_ready_overlay.dm:37
    [SignalHandler]
    public virtual void on_ranged_attack(mob_living_basic firer = null, atom target = null, object modifiers = null)
    {
        firer.cut_overlay(overlay_state);
        waiting_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(restore_overlay), firer), Dm.num(display_after), Dm.bitor(Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), TIMER_DELETE_ME), file: "code/datums/components/basic_ranged_ready_overlay.dm", line: 40);
    }

    /// Don't show overlay on a dead man
    // tg: code/datums/components/basic_ranged_ready_overlay.dm:43
    [SignalHandler]
    public virtual void on_stat_changed(mob_living_basic gunman = null)
    {
        if (gunman.stat == DEAD)
        {
            gunman.cut_overlay(overlay_state);
            return;
        }
        if (Dm.num(timeleft(waiting_timer)) <= 0)
            restore_overlay(parent as mob_living_basic);
    }

    /// Try putting our overlay back
    // tg: code/datums/components/basic_ranged_ready_overlay.dm:52
    public virtual void restore_overlay(mob_living_basic gunman = null)
    {
        if (QDELETED(gunman) || gunman.stat == DEAD)
            return;
        gunman.cut_overlay(overlay_state);
        gunman.add_overlay(overlay_state);
    }
}
