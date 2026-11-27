// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/restaurant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the holder of the restaurant's venues and its customer kinds (one of each, made at its init), and the cache of
// the ordered foods' looks. The var block is generated.

/*!
This subsystem exists to serve as a holder for important info for the restaurant system for chef and bartender.
*/

// tg: code/controllers/subsystem/restaurant.dm:5  SUBSYSTEM_DEF(restaurant)
public partial class datum_controller_subsystem_restaurant : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(restaurant) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSrestaurant) (__DEFINES/MC.dm:29) expanded
    // inline
    // tg: code/controllers/subsystem/restaurant.dm:5
    public override async DmTask New(params object[] __extra)
    {
        if (SSrestaurant != this)
        {
            if (SSrestaurant is datum_controller_subsystem_restaurant)
            {
                Recover();
                qdel(SSrestaurant);
            }
            SSrestaurant = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // tg: code/controllers/subsystem/restaurant.dm:16
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        foreach (Type key in Dm.each<Type>(subtypesof(typeof(datum_venue))))
            all_venues[key] = Dm.@new(key);
        foreach (Type key in Dm.each<Type>(subtypesof(typeof(datum_customer_data))))
            all_customers[key] = Dm.@new(key);
        await DmTask.CompletedTask;
        return SS_INIT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/restaurant.dm:5  SUBSYSTEM_DEF(restaurant): GLOBAL_REAL(SSrestaurant, /datum/controller/subsystem/restaurant)
    internal datum_controller_subsystem_restaurant SSrestaurant;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_restaurant SSrestaurant => ref TgState.Current.SSrestaurant;
}
