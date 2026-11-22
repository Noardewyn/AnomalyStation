// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/point_of_interest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10x slice C (D281): the whole file.

/// Designates the atom as a "point of interest", meaning it can be directly orbited
// tg: code/datums/elements/point_of_interest.dm:2
[DmPath("/datum/element/point_of_interest")]
public partial class datum_element_point_of_interest : datum_element
{
    // tg: code/datums/elements/point_of_interest.dm:5
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;

        // New players are abstract mobs assigned to people who are still in the lobby screen.
        // As a result, they are not a valid POI and should never be a valid POI. If they
        // somehow get this element attached to them, there's something we need to debug.
        if (isnewplayer(target))
            return ELEMENT_INCOMPATIBLE;

        SSpoints_of_interest.on_poi_element_added((atom)target);
        return await base.Attach(target, __extra);
    }

    // tg: code/datums/elements/point_of_interest.dm:17
    public override object Detach(datum source = null, params object[] __extra)
    {
        SSpoints_of_interest.on_poi_element_removed((atom)source);
        return base.Detach(source, __extra);
    }
}
