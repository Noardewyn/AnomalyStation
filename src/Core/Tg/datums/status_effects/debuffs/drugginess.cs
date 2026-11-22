// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/drugginess.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG1 (D339): the whole file (the drinks' and the drugs' "high": the rainbow fullscreen, the drugged sound environment and
// the beach bum's language). The var block is generated; the alert's is too.

/// Drugginess / "high" effect, makes your screen rainbow
// tg: code/datums/status_effects/debuffs/drugginess.dm:2
public partial class datum_status_effect_drugginess : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/drugginess.dm:7
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/drugginess.dm:11
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(remove_drugginess));

        await owner.add_mood_event(id, typeof(datum_mood_event_high));
        owner.overlay_fullscreen(id, typeof(atom_movable_screen_fullscreen_high));
        owner.sound_environment_override = SOUND_ENVIRONMENT_DRUGGED;
        owner.grant_language(typeof(datum_language_beachbum), source: Dm.str(id));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/drugginess.dm:20
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);

        owner.clear_mood_event(id);
        owner.clear_fullscreen(id);
        if (owner.sound_environment_override == SOUND_ENVIRONMENT_DRUGGED)
            owner.sound_environment_override = SOUND_ENVIRONMENT_NONE;
        owner.remove_language(typeof(datum_language_beachbum), source: Dm.str(id));
        return null;
    }

    /// Removes all of our drugginess (self delete) on signal
    // tg: code/datums/status_effects/debuffs/drugginess.dm:30
    [SignalHandler]
    public virtual void remove_drugginess(datum source = null, object admin_revive = null)
    {
        qdel(this);
    }
}
