// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/persistence/_persistence.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SSpersistence, only its delamination counter (the incident displays, DV220): Initialize and collect_data call
// load_delamination_counter and save_delamination_counter; every other load and save (Poly, the engravings, the tattoos,
// the trophies, the maps, the photos, the outfits, the adventures, the recipes, the scars, the message
// bottles) is left out with a comment (docs/ai/specs/not-translated.md, M10+ round persistence). The var block is generated (D111).

// tg: code/controllers/subsystem/persistence/_persistence.dm:4  SUBSYSTEM_DEF(persistence)
public partial class datum_controller_subsystem_persistence : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(persistence) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSpersistence)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/persistence/_persistence.dm:4
    public override async DmTask New(params object[] __extra)
    {
        if (SSpersistence != this)
        {
            if (SSpersistence is datum_controller_subsystem_persistence)
            {
                Recover();
                qdel(SSpersistence);
            }
            SSpersistence = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/persistence/_persistence.dm:68
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        // load_poly(), load_wall_engravings(), load_prisoner_tattoos(), load_trophies(), load_recent_maps(),
        // load_photo_persistence(), load_custom_outfits(): not ported (M10+ round persistence)
        load_delamination_counter();
        load_tram_counter();
        // load_adventures(): not ported (M10+)
        return SS_INIT_SUCCESS;
    }

    ///Collects all data to persist.
    // tg: code/controllers/subsystem/persistence/_persistence.dm:82
    public virtual void collect_data()
    {
        // save_wall_engravings(), save_prisoner_tattoos(), collect_trophies(), collect_maps(), save_randomized_recipes(),
        // save_scars(), save_custom_outfits(): not ported (M10+ round persistence)
        save_delamination_counter();
        // save_queued_message_bottles(): not ported (M10+)
        if (Dm.truthy(SStransport.can_fire))
        {
            foreach (datum_transport_controller_linear_tram transport in Dm.each<datum_transport_controller_linear_tram>(SStransport.transports_by_type[TRANSPORT_TYPE_TRAM]))
                save_tram_history(transport.specific_transport_id);
            save_tram_counter();
        }
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/persistence/_persistence.dm:4  SUBSYSTEM_DEF(persistence): GLOBAL_REAL(SSpersistence, /datum/controller/subsystem/persistence)
    internal datum_controller_subsystem_persistence SSpersistence;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_persistence SSpersistence => ref TgState.Current.SSpersistence;
}
