// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/dizziness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice B: the whole file. The wobble animates the player's client (its pixel_x and pixel_y, as shake_camera does); the
// replicator sends the chain to that player as a view turn (DV106). The var block is generated.

// tg: code/datums/status_effects/debuffs/dizziness.dm:1
public partial class datum_status_effect_dizziness : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/dizziness.dm:7
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/dizziness.dm:11
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_LIVING_DEATH, nameof(clear_dizziness));
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/dizziness.dm:15
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, COMSIG_LIVING_DEATH);
        // In case our client's offset is somewhere wacky from the dizziness effect
        if (owner.client != null)
        {
            owner.client.pixel_x = 0; // initial(owner.client?.pixel_x)
            owner.client.pixel_y = 0;
        }
        return null;
    }

    /// Signal proc that self deletes our dizziness effect
    // tg: code/datums/status_effects/debuffs/dizziness.dm:22
    [SignalHandler]
    public virtual void clear_dizziness(datum source = null)
    {
        qdel(this);
    }

    // tg: code/datums/status_effects/debuffs/dizziness.dm:27
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float seconds = Dm.num(seconds_between_ticks);
        // How much time is left, in seconds
        float amount = Dm.num(duration) / 10;
        if (amount <= 0)
            return null;

        // How strong the dizziness effect is on us.
        // If we're resting, the effect is 5x as strong, but also decays 5x fast.
        // Meaning effectively, 1 tick is actually dizziness_strength ticks of duration
        float dizziness_strength = Dm.truthy(owner.resting) ? 5 : 1;

        // How much time will be left, in seconds, next tick
        float next_amount = max(amount - (dizziness_strength * seconds * 0.1f), 0);

        // If we have a dizziness strength > 1, we will subtract ticks off of the total duration
        if (Dm.truthy(remove_duration((dizziness_strength - 1) * seconds)))
            return null;

        // Now we can do the actual dizzy effects.
        // Don't bother animating if they're clientless.
        if (owner.client == null)
            return null;

        // Want to be able to offset things by the time the animation should be "playing" at
        float time = Dm.num(world.time);
        float delay = 0;
        float pixel_x_diff = 0;
        float pixel_y_diff = 0;

        // This shit is annoying at high strengthvar/pixel_x_diff = 0
        DmList view_range_list = getviewsize(owner.client.view);
        float view_range = Dm.num(view_range_list[1]);
        float amplitude = amount * (sin(amount * time) + 1);
        float x_diff = clamp(amplitude * sin(amount * time), -view_range, view_range);
        float y_diff = clamp(amplitude * cos(amount * time), -view_range, view_range);
        pixel_x_diff += x_diff;
        pixel_y_diff += y_diff;
        // DEVIATION(DV106): the chain that follows is a view turn (the replicator sends it as the client's recoil kind)
        owner.client.dm_camera_recoil = true;
        // Brief explanation. We're basically snapping between different pixel_x/ys instantly, with delays between
        // Doing this with relative changes. This way we don't override any existing pixel_x/y values
        // We use EASE_OUT here for similar reasons, we want to act at the end of the delay, not at its start
        // Relative animations are weird, so we do actually need this
        // (tg passes the time 3 positionally; in the two calls without an object the shim would take it for the object, so all
        // three name it: BYOND continues the chain with time 3)
        animate(owner.client, Dm.named("pixel_x", x_diff), Dm.named("pixel_y", y_diff), Dm.named("time", 3), Dm.named("easing", Dm.bitor(JUMP_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_RELATIVE));
        delay += 0.3f * SECONDS; // This counts as a 0.3 second wait, so we need to shift the sine wave by that much

        x_diff = amplitude * sin(next_amount * (time + delay));
        y_diff = amplitude * cos(next_amount * (time + delay));
        pixel_x_diff += x_diff;
        pixel_y_diff += y_diff;
        animate(Dm.named("pixel_x", x_diff), Dm.named("pixel_y", y_diff), Dm.named("time", 3), Dm.named("easing", Dm.bitor(JUMP_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_RELATIVE));

        // Now we reset back to our old pixel_x/y, since these animates are relative
        animate(Dm.named("pixel_x", -pixel_x_diff), Dm.named("pixel_y", -pixel_y_diff), Dm.named("time", 3), Dm.named("easing", Dm.bitor(JUMP_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_RELATIVE));
        return null;
    }
}
