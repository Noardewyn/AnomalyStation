// Translated from tgstation (snapshot 1d99bd7f): code/modules/awaymissions/exile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4a P3: the PopulateContents of Meta's containers in this file (D133), translated with the help of m4/p3w/popgen2.py
// (the pure `new /path(src)` bodies, their loops, prob and switch); the file's other procs are their milestones'.

// tg: code/modules/awaymissions/exile.dm:6
public partial class obj_structure_closet_secure_closet_exile : obj_structure_closet_secure_closet
{
    // tg: code/modules/awaymissions/exile.dm:6
    public override async DmTask PopulateContents(params object[] __extra)
    {
        Dm.@new<obj_item_implanter_exile>(this); // tg: code/modules/awaymissions/exile.dm:7
        for (float i = 1; i <= 5; i++) // tg: code/modules/awaymissions/exile.dm:8
        {
            Dm.@new<obj_item_implantcase_exile>(this); // tg: code/modules/awaymissions/exile.dm:9
        }
    }
}
