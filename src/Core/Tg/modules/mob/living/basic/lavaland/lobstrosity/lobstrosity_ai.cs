// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity_ai.dm:8
public partial class datum_ai_controller_basic_controller_lobstrosity : datum_ai_controller_basic_controller
{
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity_ai.dm:32
    public override async DmTask<object> TryPossessPawn(atom new_pawn = null)
    {
        object dot = await base.TryPossessPawn(new_pawn);
        if (Dm.truthy(Dm.bitand(dot, AI_CONTROLLER_INCOMPATIBLE)))
            return dot;
        DmList food_types = TgState.Current.datum_ai_controller_basic_controller_lobstrosity__TryPossessPawn__food_types ??= typecacheof(Dm.list(typeof(obj_item_fish_lavaloop))) as DmList;
        set_blackboard_key(BB_BASIC_FOODS, food_types);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/lavaland/lobstrosity/lobstrosity_ai.dm:36 (var/static/list/food_types)
    internal DmList datum_ai_controller_basic_controller_lobstrosity__TryPossessPawn__food_types;
}
