// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the constants (MIN_GLIDE_SIZE, the movement loop flags and priorities, the z-move flags, FIRST_DIAG_STEP,
// the teleport channels, NEWTONS, ...) are generated; the global var and the function-like macros are here (rules 10.6,
// 11.1).

/// Compensating for time dilation
// tg: code/__DEFINES/movement.dm:8
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__DEFINES/movement.dm:8  GLOBAL_VAR_INIT(glide_size_multiplier, 1.0)
    public float glide_size_multiplier;

    // tg: code/__DEFINES/movement.dm:8  (GLOBAL_MANAGED(glide_size_multiplier, 1.0), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalglide_size_multiplier()
    {
        glide_size_multiplier = 1.0f;
        Dm.append(ref gvars_datum_init_order, "glide_size_multiplier");
    }
}

public static partial class GlobalProcs
{
    ///Broken down, here's what this does:
    /// divides the world icon_size by delay divided by ticklag to get the number of pixels something should be moving each tick.
    /// The division result is given a min value of 1 to prevent obscenely slow glide sizes from being set
    /// Then that's multiplied by the global glide size multiplier. 1.25 by default feels pretty close to spot on. This is just to try to get byond to behave.
    /// The whole result is then clamped to within the range above.
    /// Not very readable but it works
    // tg: code/__DEFINES/movement.dm:16  #define DELAY_TO_GLIDE_SIZE(delay) (clamp(((ICON_SIZE_ALL / max((delay) / world.tick_lag, 1)) * GLOB.glide_size_multiplier), MIN_GLIDE_SIZE, MAX_GLIDE_SIZE))
    public static float DELAY_TO_GLIDE_SIZE(float delay) => (clamp((Dm.div(ICON_SIZE_ALL, max(Dm.div((delay), world.tick_lag), 1)) * GLOB.glide_size_multiplier), MIN_GLIDE_SIZE, MAX_GLIDE_SIZE));

    ///Similar to DELAY_TO_GLIDE_SIZE, except without the clamping, and it supports piping in an unrelated scalar
    // tg: code/__DEFINES/movement.dm:19  #define MOVEMENT_ADJUSTED_GLIDE_SIZE(delay, movement_disparity) (ICON_SIZE_ALL / ((delay) / world.tick_lag) * movement_disparity * GLOB.glide_size_multiplier)
    public static float MOVEMENT_ADJUSTED_GLIDE_SIZE(float delay, float movement_disparity) => (Dm.div(ICON_SIZE_ALL, (Dm.div((delay), world.tick_lag))) * movement_disparity * GLOB.glide_size_multiplier);

    /**
     * Returns a bitfield containing flags both present in `flags` arg and the `processing_move_loop_flags` move_packet variable.
     * Has no use outside of procs called within the movement proc chain.
     */
    // tg: code/__DEFINES/movement.dm:57  #define CHECK_MOVE_LOOP_FLAGS(movable, flags) (movable.move_packet ? (movable.move_packet.processing_move_loop_flags & (flags)) : NONE)
    public static float CHECK_MOVE_LOOP_FLAGS(object movable, float flags) => (Dm.truthy(((atom_movable)movable).move_packet) ? Dm.bitand(((atom_movable)movable).move_packet.processing_move_loop_flags, (flags)) : NONE);
}
