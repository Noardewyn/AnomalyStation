// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/beds_chairs/pew.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8y slice A (D223): the whole file (the chapel's pews buckle as chairs). The var blocks are generated.

// tg: code/game/objects/structures/beds_chairs/pew.dm:1
public partial class obj_structure_chair_pew : obj_structure_chair
{
    ///This proc adds the rotate component, overwrite this if you for some reason want to change some specific args.
    // tg: code/game/objects/structures/beds_chairs/pew.dm:14
    public override void MakeRotate()
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation), Dm.bitor(ROTATION_REQUIRE_WRENCH, ROTATION_IGNORE_ANCHORED)));
    }
}
