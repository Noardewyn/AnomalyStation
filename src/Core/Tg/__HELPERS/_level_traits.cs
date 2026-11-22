// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/_level_traits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only is_secret_level translated (wave 4, package C: /mob/abstract_move, code/modules/mob/mob_movement.dm:584) and
// is_station_level with its cache, GLOB.station_levels_cache and GLOB.station_level_z_scratch (M3 P4: the walls' and floors'
// GLOB.station_turfs). M9 slice E: is_mining_level (a PDA's NTNet signal). The other is_*_level macros come with z-level traits (M3).

// tg: code/__HELPERS/_level_traits.dm:6
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/__HELPERS/_level_traits.dm:6  GLOBAL_LIST_EMPTY(station_levels_cache)
    public DmList station_levels_cache;

    // tg: code/__HELPERS/_level_traits.dm:6  (GLOBAL_MANAGED(station_levels_cache, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalstation_levels_cache()
    {
        station_levels_cache = Dm.list();
        Dm.append(ref gvars_datum_init_order, "station_levels_cache");
    }

    // Used to prevent z from being re-evaluated
    // tg: code/__HELPERS/_level_traits.dm:9  GLOBAL_VAR(station_level_z_scratch)
    public object station_level_z_scratch;

    // tg: code/__HELPERS/_level_traits.dm:9  (GLOBAL_UNMANAGED(station_level_z_scratch), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalstation_level_z_scratch()
    {
        return;
    }
}

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/_level_traits.dm:29  #define is_away_level(z) SSmapping.level_trait(z, ZTRAIT_AWAY) (the syndicate campaign G2: the syndicate teleporter)
    public static object is_away_level(object z) => SSmapping.level_trait(z, ZTRAIT_AWAY);

    // tg: code/__HELPERS/_level_traits.dm:4  #define is_centcom_level(z) SSmapping.level_trait(z, ZTRAIT_CENTCOM) (M10 slice G5: the communications console)
    public static object is_centcom_level(object z) => SSmapping.level_trait(z, ZTRAIT_CENTCOM);

    // tg: code/__HELPERS/_level_traits.dm  #define is_reserved_level(z) SSmapping.level_trait(z, ZTRAIT_RESERVED) (M10 slice G4)
    public static object is_reserved_level(object z) => SSmapping.level_trait(z, ZTRAIT_RESERVED);

    // Called a lot, somewhat slow, so has its own cache
    // tg: code/__HELPERS/_level_traits.dm:12  #define is_station_level(z_level) (a member, rules 11.1: z_level is evaluated once)
    public static object is_station_level(object z_level)
    {
        if (!Dm.truthy(z_level))
            return z_level;
        /* The right hand side of this guarantees that we'll have the space to fill later on, while also not failing the condition */
        if ((Dm.lt(GLOB.station_levels_cache.len, (GLOB.station_level_z_scratch = z_level)) && Dm.truthy(GLOB.station_levels_cache.len = Dm.num(GLOB.station_level_z_scratch)))
            || GLOB.station_levels_cache[GLOB.station_level_z_scratch] == null)
            return (GLOB.station_levels_cache[GLOB.station_level_z_scratch] = Dm.num(Dm.truthy(SSmapping.level_trait(GLOB.station_level_z_scratch, ZTRAIT_STATION))));
        return GLOB.station_levels_cache[GLOB.station_level_z_scratch];
    }

    // tg: code/__HELPERS/_level_traits.dm:25  #define is_mining_level(z) SSmapping.level_trait(z, ZTRAIT_MINING) (M9 slice E: a PDA's NTNet signal)
    public static object is_mining_level(object z) => SSmapping.level_trait(z, ZTRAIT_MINING);

    // tg: code/__HELPERS/_level_traits.dm:32  #define is_secret_level(z) SSmapping.level_trait(z, ZTRAIT_SECRET)
    public static object is_secret_level(object z) => SSmapping.level_trait(z, ZTRAIT_SECRET);

    // tg: code/__HELPERS/_level_traits.dm:33  #define is_multi_z_level(z) (SSmapping.level_trait(z, ZTRAIT_UP) || SSmapping.level_trait(z, ZTRAIT_DOWN))
    // (gaps G3: the maintenance MOD module spawner; rules 4.5: the value of the first true operand)
    public static object is_multi_z_level(object z) => SSmapping.level_trait(z, ZTRAIT_UP) is var up && Dm.truthy(up) ? up : SSmapping.level_trait(z, ZTRAIT_DOWN);
}
