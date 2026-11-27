// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/wolf/wolf_extras.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The two pet commands are var blocks (generated).

// Contains pixel offset data for sprites riding wolves
// tg: code/modules/mob/living/basic/icemoon/wolf/wolf_extras.dm:11
public partial class datum_component_riding_creature_wolf : datum_component_riding_creature
{
    // tg: code/modules/mob/living/basic/icemoon/wolf/wolf_extras.dm:12
    public override DmList get_rider_offsets_and_layers(object pass_index = null, mob offsetter = null)
    {
        return Dm.list(
            Dm.kv(TEXT_NORTH, Dm.list(1f, 9f, OBJ_LAYER)),
            Dm.kv(TEXT_SOUTH, Dm.list(1f, 9f, ABOVE_MOB_LAYER)),
            Dm.kv(TEXT_EAST, Dm.list(0f, 9f, OBJ_LAYER)),
            Dm.kv(TEXT_WEST, Dm.list(2f, 9f, OBJ_LAYER)));
    }
}
