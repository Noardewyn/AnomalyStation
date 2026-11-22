// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the ice block talisman's status effect translated (gaps RG1, D339: the icegrave cocktail freezes its drinker in an
// ice cube); the demonic frost miner, its attacks, its prism and its loot come with the megafauna.

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:235
public partial class datum_status_effect_ice_block_talisman : datum_status_effect
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:243
    [DmParams("new_owner", "set_duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object set_duration = Dm.arg(__extra, 0);
        if (isnum(set_duration))
            duration = Dm.num(set_duration);
        return await base.on_creation(new_owner, __extra);
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:253
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE, nameof(owner_moved));
        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_userdanger(I18n.T("You become frozen in a cube!")));
        cube = Dm.icon(Dm.resource("icons/effects/freeze.dmi"), "ice_cube");
        DmList icon_dimensions = get_icon_dimensions(owner.icon);
        cube.Scale(icon_dimensions["width"], icon_dimensions["height"]);
        owner.add_overlay(cube);
        return await base.on_apply(__extra);
    }

    /// Blocks movement from the status effect owner
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:264
    [SignalHandler]
    public virtual object owner_moved()
    {
        return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:268
    public override async DmTask be_replaced()
    {
        owner.cut_overlay(cube);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        await base.be_replaced();
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/demonic_frost_miner.dm:273
    public override async DmTask<object> on_remove()
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(owner))
            to_chat(owner, span_notice(I18n.T("The cube melts!")));
        owner.cut_overlay(cube);
        UnregisterSignal(owner, COMSIG_MOVABLE_PRE_MOVE);
        return null;
    }
}
