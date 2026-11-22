using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg;

/// <summary>
/// For test worlds that do not boot (TgTestWorld.Boot does the real thing): what /world/Genesis and Master/New create
/// before any subsystem runs and that every logging, chat and config read needs. Genesis creates the logger
/// (game/world.dm:97); Master/New creates SSchat with the other subsystems and the configuration (<c>if(!config)
/// config = new</c>), which world/New loads; with no config files every entry keeps its default.
/// </summary>
internal static class TgTestGenesis
{
    /// <summary>Creates whichever of the logger, SSchat and a loaded default configuration the current world lacks.</summary>
    public static void Install()
    {
        logger ??= Dm.@new<datum_log_holder>();
        if (SSchat == null)
            Dm.@new<datum_controller_subsystem_chat>(); // NEW_SS_GLOBAL(SSchat)
        if (config == null)
        {
            config = Dm.@new<datum_controller_configuration>();
            Waiting.NoSleep(config.Load(null));
        }
    }
}
