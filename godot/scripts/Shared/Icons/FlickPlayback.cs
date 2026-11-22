namespace Anomaly.Client.Shared.Icons;

/// <summary>
/// D142 / M4v slice 2: when a moving entity's last flick (<c>NetField.Flick</c>) plays at the render tick. BYOND's <c>flick(state, atom)</c>
/// plays the flicked appearance's states once from the tick it started in and then shows the atom's own picture again; the entity layer
/// draws at its interpolated render tick, which lags the replica's latest tick, so a flick starts when the render tick reaches it.
/// </summary>
public static class FlickPlayback
{
    /// <summary>
    /// The seconds into a flick that started at server tick <paramref name="flickTick"/> and lasts <paramref name="lengthSeconds"/>
    /// (its <see cref="AnimationTimeline"/> played once) at <paramref name="renderTick"/>; false while it has not started yet, once it is
    /// over (a flick older than its own length is never played), and for a length that is not positive (not known yet).
    /// </summary>
    public static bool TryAge(long flickTick, double renderTick, int ticksPerSecond, double lengthSeconds, out double seconds)
    {
        seconds = 0;
        if (lengthSeconds <= 0 || ticksPerSecond <= 0 || double.IsNaN(renderTick))
            return false;
        double age = (renderTick - flickTick) / ticksPerSecond;
        if (age < 0 || age >= lengthSeconds)
            return false;
        seconds = age;
        return true;
    }

    /// <summary>
    /// The length in seconds of a flick of an appearance whose picture is drawn from <paramref name="animatedStates"/> (the compositor's
    /// <c>AnimatedStates</c>; null or empty: a still state, which BYOND shows for one tick).
    /// </summary>
    public static double LengthOf(IReadOnlyList<IIconState> animatedStates) => AnimationTimeline.Of(animatedStates, once: true).Length;
}
