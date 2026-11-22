// Translated from tgstation (snapshot 1d99bd7f): code/modules/mafia/map_pieces.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4d, D327): the game board a ghost clicks for the Mafia panel, and the Mafia maps loaded from
// _maps/minigame/mafia (the host mounts the converted maps there, MapSeam). tg's unit test map (#ifdef UNIT_TESTS) is not one of
// them: the port is not built with UNIT_TESTS. The var blocks are generated (D111).

// tg: code/modules/mafia/map_pieces.dm:11  for ghosts/admins
public partial class obj_mafia_game_board : obj
{
    // tg: code/modules/mafia/map_pieces.dm:18
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        MF ??= GLOB.mafia_game as datum_mafia_controller;
        MF ??= create_mafia_game();
        await MF.ui_interact(user);
        return dot;
    }
}

// tg: code/modules/mafia/map_pieces.dm:27
public partial class datum_map_template_mafia : datum_map_template
{
    // tg: code/modules/mafia/map_pieces.dm:35
    [DmParams("path", "rename", "cache")]
    public override async DmTask New(params object[] __extra)
    {
        object rename = Dm.arg(__extra, 1);
        object cache = Dm.arg(__extra, 2) ?? FALSE;
        object path = Dm.text("_maps/minigame/mafia/[]", map_suffix);
        await base.New(path, rename, cache);
    }
}
