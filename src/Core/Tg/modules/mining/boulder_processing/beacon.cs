// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/boulder_processing/beacon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111).

///Beacon to launch a new mining setup when activated. For testing and speed!
// tg: code/modules/mining/boulder_processing/beacon.dm:2
public partial class obj_item_boulder_beacon : obj_item
{
    // tg: code/modules/mining/boulder_processing/beacon.dm:10
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        visible_message(span_warning(I18n.T("\\The [] begins to beep loudly!", this)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(launch_payload)), 1 * SECONDS, file: "code/modules/mining/boulder_processing/beacon.dm", line: 12);
        return null;
    }

    /**
     * Spawns a new bouldertech machine from the beacon, then removes a use from the beacon.
     * Use one spawns a BRM teleporter, then a refinery, and lastly a smelter.
     */
    // tg: code/modules/mining/boulder_processing/beacon.dm:18
    public virtual void launch_payload()
    {
        playsound(this, SFX_SPARKS, 80, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
        switch (uses)
        {
            case 3:
                Dm.@new<obj_machinery_brm>(drop_location());
                break;
            case 2:
                Dm.@new<obj_machinery_bouldertech_refinery>(drop_location());
                break;
            case 1:
                Dm.@new<obj_machinery_bouldertech_refinery_smelter>(drop_location());
                qdel(this);
                break;
        }
        uses--;
    }
}
