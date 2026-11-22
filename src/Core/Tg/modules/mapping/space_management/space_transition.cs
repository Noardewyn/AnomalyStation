// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/space_management/space_transition.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M3 P2): the two /datum/space_level procs. setup_map_transitions and the /datum/space_transition_point
// grid (:22-140) link the space levels' edges to each other; D108 loads the station level only, so SSmapping does not
// call them (controllers/subsystem/mapping.cs, Initialize) and they come with the space levels.

// tg: code/modules/mapping/space_management/space_transition.dm:1
public partial class datum_space_level : datum
{
    // tg: code/modules/mapping/space_management/space_transition.dm:1
    public virtual void set_linkage(object new_linkage = null)
    {
        linkage = (string)new_linkage;
        if (linkage == SELFLOOPING)
        {
            neigbours = Dm.list(TEXT_NORTH, TEXT_SOUTH, TEXT_EAST, TEXT_WEST);
            foreach (object A in Dm.each(neigbours))
                neigbours[A] = this;
        }
    }

    // tg: code/modules/mapping/space_management/space_transition.dm:8
    public virtual void set_neigbours(DmList L = null)
    {
        Dm.Deferred("code/modules/mapping/space_management/space_transition.dm:8", "set_neigbours: the space transition grid comes with the space levels (D108: the station level only in M3)");
    }
}
