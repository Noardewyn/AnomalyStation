using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg;

/// <summary>
/// What tg's world start has done before any atom exists, for tests that create atoms in a bare world (wave 3, the
/// atoms package): <c>GLOB</c> and <c>SSatoms</c> exist (<c>/world/proc/Genesis</c> creates the Master, whose
/// <c>New</c> creates every subsystem, then <c>GLOB</c>: code/game/world.dm:100, controllers/master.dm:110-128), and
/// <c>/atom/New</c> reads <c>GLOB.use_preloader</c> and <c>SSatoms.initialized</c>
/// (atoms_initializing_EXPENSIVE.dm:67-70). Both are made by their C# constructors only: no world id, no <c>New</c>,
/// so the ids and refs a test sees do not move. SSatoms is in its initial state, <c>INITIALIZATION_INSSATOMS</c>, as
/// before <c>Master.Initialize</c>: <c>/atom/New</c> then defers <c>Initialize</c>, which is what these tests assume.
/// A test that needs a booted world uses the boot package's world bootstrap instead.
/// </summary>
internal static class AtomsStandIn
{
    /// <summary>Gives the current world a <c>GLOB</c> and an <c>SSatoms</c> when it has none.</summary>
    public static void Install()
    {
        if (GLOB__var == null)
            GLOB__var = new datum_controller_global_vars();
        if (SSatoms == null)
            SSatoms = new datum_controller_subsystem_atoms();
    }

    /// <summary>Enters <paramref name="world"/> and installs the stand-in there.</summary>
    public static void Install(World world)
    {
        using (world.Enter())
            Install();
    }
}
