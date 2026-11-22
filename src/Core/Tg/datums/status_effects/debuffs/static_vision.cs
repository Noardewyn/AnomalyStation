// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/static_vision.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the static that fills the screen of a bitrunner entering or leaving a netpod and of the
// avatar being connected (set_static_vision, __DEFINES/status_effects.dm:206). The var block is generated (D111).

// tg: code/datums/status_effects/debuffs/static_vision.dm:1
public partial class datum_status_effect_static_vision : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/static_vision.dm:6
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 3 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/static_vision.dm:10
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(remove_static_vision));

        owner.overlay_fullscreen(id, typeof(atom_movable_screen_fullscreen_static_vision));
        owner.sound_environment_override = SOUND_ENVIRONMENT_UNDERWATER;

        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/static_vision.dm:18
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);

        owner.clear_fullscreen(id);
        if (Dm.eq(owner.sound_environment_override, SOUND_ENVIRONMENT_UNDERWATER))
            owner.sound_environment_override = SOUND_ENVIRONMENT_NONE;
        await DmTask.CompletedTask;
        return null;
    }

    /// Handles clearing on death
    // tg: code/datums/status_effects/debuffs/static_vision.dm:25
    [SignalHandler]
    public virtual void remove_static_vision(datum source = null, object admin_revive = null)
    {
        qdel(this);
    }
}
