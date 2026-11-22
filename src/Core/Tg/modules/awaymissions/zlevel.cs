// Translated from tgstation (snapshot 1d99bd7f): code/modules/awaymissions/zlevel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CM1, D333): generateMapList, which SSmapping's shuttle preload reads the unbuyable shuttles with
// (config/unbuyableshuttles.txt). Not here: the away missions (createRandomZlevel, the awaystart landmarks,
// generate_map_list_from_directory): the gateway's away levels are not loaded (D108).

public static partial class GlobalProcs
{
    // tg: code/modules/awaymissions/zlevel.dm:72
    public static object generateMapList(object filename = null)
    {
        DmList dot = Dm.list();
        filename = Dm.text("[]/[]", config.directory, SANITIZE_FILENAME(filename));
        DmList Lines = world.file2list(filename) as DmList;

        if (!Dm.truthy(Lines?.len))
            return dot;
        foreach (object line in Dm.each(Lines))
        {
            object t = line;
            if (!Dm.truthy(t))
                continue;

            t = trim(t);
            if (length(t) == 0)
                continue;
            else if (Dm.eq(copytext(t, 1, 2), "#")) // t[1]
                continue;

            float pos = findtext(t, " ");
            object name = null;

            if (Dm.truthy(pos))
                name = LOWER_TEXT(copytext(t, 1, pos));
            else
                name = LOWER_TEXT(t);

            if (!Dm.truthy(name))
                continue;

            Dm.append(ref dot, t);
        }
        return dot;
    }
}
