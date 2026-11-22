// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/screen_blur.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Applies a blur to the user's screen, increasing in strength depending on duration remaining.
// tg: code/datums/status_effects/debuffs/screen_blur.dm:6
public partial class datum_status_effect_eye_blur : datum_status_effect
{
    /// This number is multiplied by the duration remaining (IN SECONDS, NOT DECISECONDS)
    /// of the eye blur status effect to determine the intensity of the blur on the user
    // tg: code/datums/status_effects/debuffs/screen_blur.dm:3  #define BLUR_DURATION_TO_INTENSITY 0.05 (file-local, #undef at :58)
    private const float BLUR_DURATION_TO_INTENSITY = 0.05f;

    // tg: code/datums/status_effects/debuffs/screen_blur.dm:12
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/screen_blur.dm:16
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(owner.mob_biotypes, Dm.bitor(Dm.bitor(MOB_ROBOTIC, MOB_SPIRIT), MOB_SPECIAL))))
            return FALSE;

        // Refresh the blur when a client jumps into the mob, in case we get put on a clientless mob with no hud
        RegisterSignals(owner, Dm.list(COMSIG_MOB_LOGIN, SIGNAL_ADDTRAIT(TRAIT_SIGHT_BYPASS), SIGNAL_REMOVETRAIT(TRAIT_SIGHT_BYPASS)), nameof(update_blur));

        // Apply initial blur
        update_blur();
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/screen_blur.dm:27
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_MOB_LOGIN);
        if (owner.hud_used == null)
            return null;

        atom_movable_plane_master_controller game_plane_master_controller = owner.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        game_plane_master_controller.remove_filter("eye_blur");
        return null;
    }

    // tg: code/datums/status_effects/debuffs/screen_blur.dm:35
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        // Blur lessens the closer we are to expiring, so we update per tick.
        update_blur();
        return null;
    }

    /// Updates the blur of the owner of the status effect.
    /// Also a signal proc for [COMSIG_MOB_LOGIN], to trigger then when the mob gets a client.
    // tg: code/datums/status_effects/debuffs/screen_blur.dm:41
    [SignalHandler]
    public virtual void update_blur(datum source = null)
    {
        if (owner.hud_used == null)
            return;

        atom_movable_plane_master_controller game_plane_master_controller = owner.hud_used.plane_master_controllers[PLANE_MASTERS_GAME] as atom_movable_plane_master_controller;
        if (HAS_TRAIT(owner, TRAIT_SIGHT_BYPASS))
        {
            game_plane_master_controller.remove_filter("eye_blur");
            return;
        }

        float time_left_in_seconds = Dm.num(duration) / 10;
        float amount_of_blur = clamp(time_left_in_seconds * BLUR_DURATION_TO_INTENSITY, 0.6f, 3);
        game_plane_master_controller.add_filter("eye_blur", 1, gauss_blur_filter(amount_of_blur) as DmList);
    }
}
