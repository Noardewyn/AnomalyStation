// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/footsteps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio, slice 4: the four GLOBAL_LIST_INITs (footstep, barefootstep, clawfootstep, heavyfootstep) and the function-like
// macros SHOULD_ATOM_DISABLE_FOOTSTEPS and SHOULD_MOB_DISABLE_FOOTSTEPS; the constant defines of the file (the FOOTSTEP_*
// keys, the STEP_SOUND_* priorities) are generated into Defines.

/*
Below is how the following lists are defined

id = list(
list(sounds),
base volume,
extra range addition
)
*/

public partial class datum_controller_global_vars
{
    // tg: code/__DEFINES/footsteps.dm:52  GLOBAL_LIST_INIT(footstep, list(...))
    public DmList footstep;

    // tg: code/__DEFINES/footsteps.dm:52  (GLOBAL_MANAGED(footstep, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfootstep()
    {
        footstep = Dm.list(
            Dm.kv(FOOTSTEP_WOOD, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/wood1.ogg"),
                Dm.resource("sound/effects/footstep/wood2.ogg"),
                Dm.resource("sound/effects/footstep/wood3.ogg"),
                Dm.resource("sound/effects/footstep/wood4.ogg"),
                Dm.resource("sound/effects/footstep/wood5.ogg")), 100, 0)),
            Dm.kv(FOOTSTEP_FLOOR, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/floor1.ogg"),
                Dm.resource("sound/effects/footstep/floor2.ogg"),
                Dm.resource("sound/effects/footstep/floor3.ogg"),
                Dm.resource("sound/effects/footstep/floor4.ogg"),
                Dm.resource("sound/effects/footstep/floor5.ogg")), 75, -1)),
            Dm.kv(FOOTSTEP_PLATING, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/plating1.ogg"),
                Dm.resource("sound/effects/footstep/plating2.ogg"),
                Dm.resource("sound/effects/footstep/plating3.ogg"),
                Dm.resource("sound/effects/footstep/plating4.ogg"),
                Dm.resource("sound/effects/footstep/plating5.ogg")), 100, 1)),
            Dm.kv(FOOTSTEP_CARPET, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/carpet1.ogg"),
                Dm.resource("sound/effects/footstep/carpet2.ogg"),
                Dm.resource("sound/effects/footstep/carpet3.ogg"),
                Dm.resource("sound/effects/footstep/carpet4.ogg"),
                Dm.resource("sound/effects/footstep/carpet5.ogg")), 75, -1)),
            Dm.kv(FOOTSTEP_SAND, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/asteroid1.ogg"),
                Dm.resource("sound/effects/footstep/asteroid2.ogg"),
                Dm.resource("sound/effects/footstep/asteroid3.ogg"),
                Dm.resource("sound/effects/footstep/asteroid4.ogg"),
                Dm.resource("sound/effects/footstep/asteroid5.ogg")), 75, 0)),
            Dm.kv(FOOTSTEP_GRASS, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/grass1.ogg"),
                Dm.resource("sound/effects/footstep/grass2.ogg"),
                Dm.resource("sound/effects/footstep/grass3.ogg"),
                Dm.resource("sound/effects/footstep/grass4.ogg")), 75, 0)),
            Dm.kv(FOOTSTEP_WATER, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/water/water1.ogg"),
                Dm.resource("sound/effects/footstep/water/water2.ogg"),
                Dm.resource("sound/effects/footstep/water/water3.ogg"),
                Dm.resource("sound/effects/footstep/water/water4.ogg")), 100, 1)),
            Dm.kv(FOOTSTEP_LAVA, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/lava1.ogg"),
                Dm.resource("sound/effects/footstep/lava2.ogg"),
                Dm.resource("sound/effects/footstep/lava3.ogg")), 100, 0)),
            Dm.kv(FOOTSTEP_MEAT, Dm.list(Dm.list(
                Dm.resource("sound/effects/meatslap.ogg")), 100, 0)),
            Dm.kv(FOOTSTEP_CATWALK, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/catwalk1.ogg"),
                Dm.resource("sound/effects/footstep/catwalk2.ogg"),
                Dm.resource("sound/effects/footstep/catwalk3.ogg"),
                Dm.resource("sound/effects/footstep/catwalk4.ogg"),
                Dm.resource("sound/effects/footstep/catwalk5.ogg")), 100, 1)),
            Dm.kv(FOOTSTEP_CONCRETE, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/concrete1.ogg"),
                Dm.resource("sound/effects/footstep/concrete2.ogg"),
                Dm.resource("sound/effects/footstep/concrete3.ogg"),
                Dm.resource("sound/effects/footstep/concrete4.ogg"),
                Dm.resource("sound/effects/footstep/concrete5.ogg")), 100, 1)));
        Dm.append(ref gvars_datum_init_order, "footstep");
    }

    // tg: code/__DEFINES/footsteps.dm:114  GLOBAL_LIST_INIT(barefootstep, list(...))
    public DmList barefootstep;

    // tg: code/__DEFINES/footsteps.dm:114  (GLOBAL_MANAGED(barefootstep, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalbarefootstep()
    {
        barefootstep = Dm.list(
            Dm.kv(FOOTSTEP_WOOD_BAREFOOT, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/woodbarefoot1.ogg"),
                Dm.resource("sound/effects/footstep/woodbarefoot2.ogg"),
                Dm.resource("sound/effects/footstep/woodbarefoot3.ogg"),
                Dm.resource("sound/effects/footstep/woodbarefoot4.ogg"),
                Dm.resource("sound/effects/footstep/woodbarefoot5.ogg")), 80, -1)),
            Dm.kv(FOOTSTEP_HARD_BAREFOOT, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/hardbarefoot1.ogg"),
                Dm.resource("sound/effects/footstep/hardbarefoot2.ogg"),
                Dm.resource("sound/effects/footstep/hardbarefoot3.ogg"),
                Dm.resource("sound/effects/footstep/hardbarefoot4.ogg"),
                Dm.resource("sound/effects/footstep/hardbarefoot5.ogg")), 80, -1)),
            Dm.kv(FOOTSTEP_CARPET_BAREFOOT, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/carpetbarefoot1.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot2.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot3.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot4.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot5.ogg")), 75, -2)),
            Dm.kv(FOOTSTEP_SAND, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/asteroid1.ogg"),
                Dm.resource("sound/effects/footstep/asteroid2.ogg"),
                Dm.resource("sound/effects/footstep/asteroid3.ogg"),
                Dm.resource("sound/effects/footstep/asteroid4.ogg"),
                Dm.resource("sound/effects/footstep/asteroid5.ogg")), 75, 0)),
            Dm.kv(FOOTSTEP_GRASS, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/grass1.ogg"),
                Dm.resource("sound/effects/footstep/grass2.ogg"),
                Dm.resource("sound/effects/footstep/grass3.ogg"),
                Dm.resource("sound/effects/footstep/grass4.ogg")), 75, 0)),
            Dm.kv(FOOTSTEP_WATER, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/water/water1.ogg"),
                Dm.resource("sound/effects/footstep/water/water2.ogg"),
                Dm.resource("sound/effects/footstep/water/water3.ogg"),
                Dm.resource("sound/effects/footstep/water/water4.ogg")), 100, 1)),
            Dm.kv(FOOTSTEP_LAVA, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/lava1.ogg"),
                Dm.resource("sound/effects/footstep/lava2.ogg"),
                Dm.resource("sound/effects/footstep/lava3.ogg")), 100, 0)),
            Dm.kv(FOOTSTEP_MEAT, Dm.list(Dm.list(
                Dm.resource("sound/effects/meatslap.ogg")), 100, 0)));
        Dm.append(ref gvars_datum_init_order, "barefootstep");
    }

    // tg: code/__DEFINES/footsteps.dm:158  GLOBAL_LIST_INIT(clawfootstep, list(...))
    public DmList clawfootstep;

    // tg: code/__DEFINES/footsteps.dm:158  (GLOBAL_MANAGED(clawfootstep, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalclawfootstep()
    {
        clawfootstep = Dm.list(
            Dm.kv(FOOTSTEP_WOOD_CLAW, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/woodclaw1.ogg"),
                Dm.resource("sound/effects/footstep/woodclaw2.ogg"),
                Dm.resource("sound/effects/footstep/woodclaw3.ogg"),
                Dm.resource("sound/effects/footstep/woodclaw2.ogg"),
                Dm.resource("sound/effects/footstep/woodclaw1.ogg")), 90, 1)),
            Dm.kv(FOOTSTEP_HARD_CLAW, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/hardclaw1.ogg"),
                Dm.resource("sound/effects/footstep/hardclaw2.ogg"),
                Dm.resource("sound/effects/footstep/hardclaw3.ogg"),
                Dm.resource("sound/effects/footstep/hardclaw4.ogg"),
                Dm.resource("sound/effects/footstep/hardclaw1.ogg")), 90, 1)),
            Dm.kv(FOOTSTEP_CARPET_BAREFOOT, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/carpetbarefoot1.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot2.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot3.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot4.ogg"),
                Dm.resource("sound/effects/footstep/carpetbarefoot5.ogg")), 75, -2)),
            Dm.kv(FOOTSTEP_SAND, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/asteroid1.ogg"),
                Dm.resource("sound/effects/footstep/asteroid2.ogg"),
                Dm.resource("sound/effects/footstep/asteroid3.ogg"),
                Dm.resource("sound/effects/footstep/asteroid4.ogg"),
                Dm.resource("sound/effects/footstep/asteroid5.ogg")), 75, 0)),
            Dm.kv(FOOTSTEP_GRASS, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/grass1.ogg"),
                Dm.resource("sound/effects/footstep/grass2.ogg"),
                Dm.resource("sound/effects/footstep/grass3.ogg"),
                Dm.resource("sound/effects/footstep/grass4.ogg")), 75, 0)),
            Dm.kv(FOOTSTEP_WATER, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/water/water1.ogg"),
                Dm.resource("sound/effects/footstep/water/water2.ogg"),
                Dm.resource("sound/effects/footstep/water/water3.ogg"),
                Dm.resource("sound/effects/footstep/water/water4.ogg")), 100, 1)),
            Dm.kv(FOOTSTEP_LAVA, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/lava1.ogg"),
                Dm.resource("sound/effects/footstep/lava2.ogg"),
                Dm.resource("sound/effects/footstep/lava3.ogg")), 100, 0)),
            Dm.kv(FOOTSTEP_MEAT, Dm.list(Dm.list(
                Dm.resource("sound/effects/meatslap.ogg")), 100, 0)));
        Dm.append(ref gvars_datum_init_order, "clawfootstep");
    }

    // tg: code/__DEFINES/footsteps.dm:202  GLOBAL_LIST_INIT(heavyfootstep, list(...))
    public DmList heavyfootstep;

    // tg: code/__DEFINES/footsteps.dm:202  (GLOBAL_MANAGED(heavyfootstep, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalheavyfootstep()
    {
        heavyfootstep = Dm.list(
            Dm.kv(FOOTSTEP_GENERIC_HEAVY, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/heavy1.ogg"),
                Dm.resource("sound/effects/footstep/heavy2.ogg")), 100, 2)),
            Dm.kv(FOOTSTEP_WATER, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/water/water1.ogg"),
                Dm.resource("sound/effects/footstep/water/water2.ogg"),
                Dm.resource("sound/effects/footstep/water/water3.ogg"),
                Dm.resource("sound/effects/footstep/water/water4.ogg")), 100, 2)),
            Dm.kv(FOOTSTEP_LAVA, Dm.list(Dm.list(
                Dm.resource("sound/effects/footstep/lava1.ogg"),
                Dm.resource("sound/effects/footstep/lava2.ogg"),
                Dm.resource("sound/effects/footstep/lava3.ogg")), 100, 0)),
            Dm.kv(FOOTSTEP_MEAT, Dm.list(Dm.list(
                Dm.resource("sound/effects/meatslap.ogg")), 100, 0)));
        Dm.append(ref gvars_datum_init_order, "heavyfootstep");
    }

}

public static partial class GlobalProcs
{
    /// Check if the atom should not be playing foodsteps for whatever reason
    // tg: code/__DEFINES/footsteps.dm:220  #define SHOULD_ATOM_DISABLE_FOOTSTEPS(source)
    public static bool SHOULD_ATOM_DISABLE_FOOTSTEPS(atom_movable source)
    {
        // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm, the lag switch measures of admins) is not declared;
        // SSlag_switch__gap stands in for it (rules 2.5), read as null: no measure is on, as in a round without admin lag
        // measures (as mob_lists.dm:74 in modules/mob/mob_lists.cs).
        datum SSlag_switch__gap = null;
        return false
            || (Dm.truthy((Dm.get_var_or_null(SSlag_switch__gap, "measures") as DmList)?[DISABLE_FOOTSTEPS]) && !HAS_TRAIT(source, TRAIT_BYPASS_MEASURES))
            || Dm.truthy(CHECK_MOVE_LOOP_FLAGS(source, MOVEMENT_LOOP_OUTSIDE_CONTROL))
            || source.moving_diagonally == SECOND_DIAG_STEP
            || Dm.truthy(Dm.bitand(source.movement_type, Dm.bitor(VENTCRAWLING, FLYING)))
            || Dm.truthy(source.throwing);
    }

    /// Check if the mob should not be playing footsteps for whatever reason
    // tg: code/__DEFINES/footsteps.dm:229  #define SHOULD_MOB_DISABLE_FOOTSTEPS(source)
    // DEVIATION(DV71): walk_heard leaves out the walk intent; the footstep element plays a walking carbon's steps at
    // WALK_FOOTSTEP_VOLUME instead (FOOTSTEP_WALKING).
    public static bool SHOULD_MOB_DISABLE_FOOTSTEPS(mob_living source, bool walk_heard = false)
    {
        return false
            || (!walk_heard && FOOTSTEP_WALKING(source))
            || !Dm.truthy(Dm.bitand(source.mobility_flags, MOBILITY_MOVE))
            || Dm.truthy(source.buckled);
    }

    /// DEVIATION(DV71): a carbon on walk intent, whose steps tg does not play (SHOULD_MOB_DISABLE_FOOTSTEPS); the footstep
    /// element plays them at WALK_FOOTSTEP_VOLUME of a run's (the owner, 2026-10-19: steps should be heard walking too).
    public static bool FOOTSTEP_WALKING(mob_living source)
        => iscarbon(source) && source.move_intent == MOVE_INTENT_WALK;

    /// DV71: a walking step's volume, a share of a running one's.
    public const float WALK_FOOTSTEP_VOLUME = 0.5f;

    /// DV233: a capsule mob walked one tile's distance (LocomotionSeam.Stride); the footstep element plays from it as from a Moved.
    /// Args as COMSIG_MOVABLE_MOVED's: (source, old_loc, dir, forced, old_locs, momentum_change).
    public const string COMSIG_LIVING_CAPSULE_STRIDE = "anomaly_living_capsule_stride";

    /// DV233: a walking capsule mob stopped (LocomotionSeam.Stride); the footstep element lands the step in the air. Args: (source).
    public const string COMSIG_LIVING_CAPSULE_HALT = "anomaly_living_capsule_halt";
}
