// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/appearance_on_aggro.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

/// Changes visuals of the attached mob while it has a target
// tg: code/datums/components/appearance_on_aggro.dm:7
public partial class datum_component_appearance_on_aggro : datum_component
{
    // tg: code/datums/components/appearance_on_aggro.dm:19
    [DmParams("aggro_state", "overlay_icon", "overlay_state", "alpha_on_aggro", "alpha_on_deaggro")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object aggro_state = Dm.arg(__extra, 0);
        object overlay_icon = Dm.arg(__extra, 1);
        object overlay_state = Dm.arg(__extra, 2);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.aggro_state = aggro_state;
        this.alpha_on_aggro = Dm.arg(__extra, 3);
        this.alpha_on_deaggro = Dm.arg(__extra, 4);
        if (overlay_icon != null && overlay_state != null)
            aggro_overlay = mutable_appearance(overlay_icon, overlay_state as string) as mutable_appearance;
        return null;
    }

    // tg: code/datums/components/appearance_on_aggro.dm:28
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_AI_BLACKBOARD_KEY_SET(target_key), nameof(on_set_target));
        RegisterSignals(parent, Dm.list(COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_key), COMSIG_LIVING_DEATH, COMSIG_MOB_LOGIN), nameof(revert_appearance));
        if (aggro_state != null)
            RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(on_icon_state_updated));
        if (aggro_overlay != null)
            RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_overlays_updated));
    }

    // tg: code/datums/components/appearance_on_aggro.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        UnregisterSignal(parent, Dm.list(COMSIG_AI_BLACKBOARD_KEY_SET(target_key), COMSIG_AI_BLACKBOARD_KEY_CLEARED(target_key), COMSIG_LIVING_DEATH, COMSIG_MOB_LOGIN));
    }

    // tg: code/datums/components/appearance_on_aggro.dm:45
    [SignalHandler]
    public virtual void on_set_target(mob_living source = null)
    {
        atom target = (source.ai_controller as datum_ai_controller)?.blackboard[target_key] as atom;
        if (QDELETED(target))
            return;

        if (aggro_overlay != null || aggro_state != null)
            source.update_appearance(UPDATE_ICON);
        if (alpha_on_aggro != null)
            Dm.animate(source, Dm.named("alpha", alpha_on_aggro), Dm.named("time", 2 * SECONDS));
    }

    // tg: code/datums/components/appearance_on_aggro.dm:57
    public override object Destroy(float? force = null)
    {
        revert_appearance(parent as mob_living);
        return base.Destroy(force);
    }

    // tg: code/datums/components/appearance_on_aggro.dm:61
    [SignalHandler]
    public virtual void revert_appearance(mob_living source = null)
    {
        if (aggro_overlay != null || aggro_state != null)
            source.update_appearance(UPDATE_ICON);
        if (alpha_on_deaggro != null)
            Dm.animate(source, Dm.named("alpha", alpha_on_deaggro), Dm.named("time", 2 * SECONDS));
    }

    // tg: code/datums/components/appearance_on_aggro.dm:68
    [SignalHandler]
    public virtual void on_icon_state_updated(mob_living source = null)
    {
        if (source.stat != DEAD)
            source.icon_state = (Dm.truthy((source.ai_controller as datum_ai_controller)?.blackboard_key_exists(target_key)) ? aggro_state : Dm.initial(source.GetType(), (atom p) => p.icon_state)) as string;
    }

    // tg: code/datums/components/appearance_on_aggro.dm:73
    [SignalHandler]
    public virtual void on_overlays_updated(mob_living_basic source = null, DmList overlays = null)
    {
        if (Dm.truthy((source.ai_controller as datum_ai_controller)?.blackboard_key_exists(target_key)) && source.stat != DEAD)
            Dm.append(ref overlays, aggro_overlay);
    }
}
