// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/mook/mook_ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The controllers but the chief's are var blocks (generated); the trees are tg's compiled .bt.json (BehaviorTrees/).

public partial class datum_controller_global_vars
{
    ///commands the chief can pick from
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_ai.dm:2  GLOBAL_LIST_INIT(mook_commands, list(...))
    public DmList mook_commands;

    // tg: code/modules/mob/living/basic/lavaland/mook/mook_ai.dm:2  (GLOBAL_MANAGED(mook_commands, list(...)))
    public virtual void InitGlobalmook_commands()
    {
        mook_commands = Dm.list(
            Dm.@new<datum_pet_command_attack>(),
            Dm.@new<datum_pet_command_fetch>());
        Dm.append(ref gvars_datum_init_order, "mook_commands");
    }
}

// tg: code/modules/mob/living/basic/lavaland/mook/mook_ai.dm:20
public partial class datum_targeting_strategy_basic_mook : datum_targeting_strategy_basic
{
    ///check for faction if not a ash walker, otherwise just attack
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_ai.dm:24
    public override object faction_check(datum_ai_controller controller = null, mob_living living_mob = null, mob_living the_target = null)
    {
        if (Dm.truthy(living_mob.has_faction(FACTION_ASHWALKER)))
            return FALSE;

        return base.faction_check(controller, living_mob, the_target);
    }
}

///the chief would rather command his mooks to attack people than attack them himself
// tg: code/modules/mob/living/basic/lavaland/mook/mook_ai.dm:52
public partial class datum_ai_controller_basic_controller_mook_tribal_chief : datum_ai_controller_basic_controller_mook
{
    // tg: code/modules/mob/living/basic/lavaland/mook/mook_ai.dm:59
    [DmParams("new_pawn")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_blackboard_key(BB_MOOK_COMMANDS, GLOB.mook_commands);
    }
}
