using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;

namespace Anomaly.Server;

/// <summary>
/// The host-to-engine half of the locomotion seam (<see cref="LocomotionSeam"/>, M2 P4): what the command phase, the join and
/// the private block call. <see cref="TgLocomotionEntryPoints"/> forwards to the seam; the interface lets the host tests
/// stand in for the bridge, as <see cref="IClientEntryPoints"/> does for the client layer (M2 P4 is written in parallel).
/// </summary>
public interface ILocomotionEntryPoints
{
    /// <summary>Adds the locomotion phase to the world, right after the host added its command phase (<see cref="LocomotionSeam.Install"/>).</summary>
    void Install(World world);

    /// <summary>Sets the world's capsule speed scale (<see cref="LocomotionSeam.SetSpeedScale"/>).</summary>
    void SetSpeedScale(World world, float scale);

    /// <summary>DV263: sets the world's movement feel (<see cref="LocomotionSeam.SetFeel"/>); a stand-in ignores it.</summary>
    void SetFeel(World world, MoveFeel feel)
    {
    }

    /// <summary>From now on the bridge drives the mob (<see cref="LocomotionSeam.Register"/>); inside the world.</summary>
    void Register(mob_living mob);

    /// <summary>DV68: from now on the bridge flies the ghost (<see cref="LocomotionSeam.RegisterGhost"/>); inside the world.</summary>
    void RegisterGhost(mob_dead_observer ghost);

    /// <summary>The bridge stops driving the mob (<see cref="LocomotionSeam.Unregister"/>).</summary>
    void Unregister(mob mob);

    /// <summary>Whether the bridge drives the mob.</summary>
    bool IsRegistered(mob mob);

    /// <summary>The frame for this tick's locomotion phase (<see cref="LocomotionSeam.SetFrame"/>); inside the world, in the command phase.</summary>
    void SetFrame(mob mob, in MotionInput frame);

    /// <summary>The capsule state after the last step and the parameters for the next tick (<see cref="LocomotionSeam.TryGetState"/>); read outside the world after the step.</summary>
    bool TryGetState(World world, long mobId, out MotionState state, out MotionParams parameters);

    /// <summary>The entity a session's client treats as its body (<see cref="LocomotionSeam.OwnBody"/>): the exosuit the mob sits in, else the mob.</summary>
    long OwnBody(World world, long mobId) => mobId;
}

/// <summary>The locomotion seam (M2 P4) as the host calls it.</summary>
public sealed class TgLocomotionEntryPoints : ILocomotionEntryPoints
{
    /// <summary>The one instance (stateless).</summary>
    public static readonly TgLocomotionEntryPoints Instance = new();

    private TgLocomotionEntryPoints()
    {
    }

    public void Install(World world) => LocomotionSeam.Install(world);

    public void SetSpeedScale(World world, float scale) => LocomotionSeam.SetSpeedScale(world, scale);

    public void SetFeel(World world, MoveFeel feel) => LocomotionSeam.SetFeel(world, feel);

    public void Register(mob_living mob) => LocomotionSeam.Register(mob);

    public void RegisterGhost(mob_dead_observer ghost) => LocomotionSeam.RegisterGhost(ghost);

    public void Unregister(mob mob) => LocomotionSeam.Unregister(mob);

    public bool IsRegistered(mob mob) => LocomotionSeam.IsRegistered(mob);

    public void SetFrame(mob mob, in MotionInput frame) => LocomotionSeam.SetFrame(mob, in frame);

    public bool TryGetState(World world, long mobId, out MotionState state, out MotionParams parameters)
        => LocomotionSeam.TryGetState(world, mobId, out state, out parameters);

    /// <inheritdoc/>
    public long OwnBody(World world, long mobId) => LocomotionSeam.OwnBody(world, mobId);
}
