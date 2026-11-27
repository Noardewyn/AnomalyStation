// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/grass.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The grass (Lavaland's patch of eden, seed vault and mook village): its smoothing picture and its damaged states. The var
// blocks are generated.

// tg: code/game/turfs/open/grass.dm:1
public partial class turf_open_misc_grass : turf_open_misc
{
    // tg: code/game/turfs/open/grass.dm:28
    public override object broken_states()
    {
        if (!Dm.truthy(smoothing_junction) || !Dm.truthy(smooth_broken))
            return Dm.list(Dm.text("[]-255", base_broken_icon_state));

        return Dm.list(Dm.text("[]-[]", base_broken_icon_state, smoothing_junction));
    }

    // tg: code/game/turfs/open/grass.dm:34
    public override object burnt_states()
    {
        if (!Dm.truthy(smoothing_junction) || !Dm.truthy(smooth_burnt))
            return Dm.list(Dm.text("[]-255", base_burnt_icon_state));

        return Dm.list(Dm.text("[]-[]", base_burnt_icon_state, smoothing_junction));
    }

    // tg: code/game/turfs/open/grass.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(smoothing_flags))
        {
            var translation = Dm.@new<matrix>();
            translation.Translate(LARGE_TURF_SMOOTHING_X_OFFSET, LARGE_TURF_SMOOTHING_Y_OFFSET);
            transform = translation;
            icon = smooth_icon;
        }

        if (Dm.truthy(is_station_level(z)))
            Dm.append(ref GLOB.station_turfs, this);
        return dot;
    }

    // tg: code/game/turfs/open/grass.dm:52
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        object dot = base.get_smooth_underlay_icon(underlay_appearance, asking_turf, adjacency_dir);
        if (!Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(smoothing_flags))
            return dot;

        underlay_appearance.transform = transform;
        return dot;
    }
}
