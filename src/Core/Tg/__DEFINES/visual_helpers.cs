// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/visual_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only NEXT_VISUAL_TICK translated (M4b slice 1: the move loops' start); the pixel offset macros come with their
// users.

public static partial class GlobalProcs
{
    /// What world.time will we next complete a visual tick?
    /// This is important because we often want to avoid moving an object twice in a tick, since this can lead to teleportation instead of gliding
    /// (such as when you move an atom that just spawned)
    // tg: code/__DEFINES/visual_helpers.dm:30  #define NEXT_VISUAL_TICK (GLOB.last_maptick_time + world.tick_lag)
    public static float NEXT_VISUAL_TICK() => (GLOB.last_maptick_time + world.tick_lag);
}
