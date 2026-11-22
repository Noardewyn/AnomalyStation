// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/traitor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate packages A1 and A2 (D316): the whole file: the steal targets (every live /datum/objective_item/steal, which GLOB.possible_items
// keeps when its item exists; DV340: not the inert ones), the traitors' code words and responses with their regexes (A2). The uplink's
// entries (uplink_items, uplink_items_by_type) are filled by the uplink asset, which the port generates here (DV44). The var block
// (name, flags, dependencies, the uplink lists) is generated.

// tg: code/controllers/subsystem/traitor.dm:1 SUBSYSTEM_DEF(traitor)
public partial class datum_controller_subsystem_traitor : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(traitor) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SStraitor) (__DEFINES/MC.dm:29) expanded
    // inline (it reads src).
    // tg: code/controllers/subsystem/traitor.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SStraitor != this)
        {
            if (SStraitor is datum_controller_subsystem_traitor)
            {
                Recover();
                qdel(SStraitor);
            }
            SStraitor = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/traitor.dm:23
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // DEVIATION(DV44): tg's SSearly_assets generates the early assets before this subsystem initializes, the uplink's among them
        // (/datum/asset/json/uplink, which fills uplink_items and uplink_items_by_type); the port sends no assets and has no
        // SSearly_assets, so the uplink's asset is generated here, first, and its data kept for the Uplink window's static data
        await load_uplink_asset();

        foreach (object theft_item in Dm.each(subtypesof(typeof(datum_objective_item_steal))))
        {
            // DEVIATION(DV340): an inert steal target (its own procs wait for its item or system: the nuclear disk, the plutonium core,
            // the hard drive, the supermatter sliver, a functional AI) is not made, so no objective names it
            if (Attribute.IsDefined((Type)theft_item, typeof(DmInertAttribute), inherit: false))
                continue;
            await Dm.new_async(theft_item as Type);
        }

        syndicate_code_phrase = generate_code_phrase(return_list: TRUE);
        syndicate_code_phrase_regex = Dm.@new<regex>(Dm.text("([])", jointext(syndicate_code_phrase, "|")), "ig");
        syndicate_code_response = generate_code_phrase(return_list: TRUE);
        syndicate_code_response_regex = Dm.@new<regex>(Dm.text("([])", jointext(syndicate_code_response, "|")), "ig");
        return SS_INIT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/traitor.dm:1  SUBSYSTEM_DEF(traitor): GLOBAL_REAL(SStraitor, /datum/controller/subsystem/traitor)
    internal datum_controller_subsystem_traitor SStraitor;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_traitor SStraitor => ref TgState.Current.SStraitor;
}
