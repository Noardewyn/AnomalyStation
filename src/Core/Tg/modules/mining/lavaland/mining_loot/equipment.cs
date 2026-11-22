// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps P3, D327, with the void magnet): the hole in reality a body vanishes into (equipment.dm:167-226). Lavaland's mining
// loot (the talisman item, the paradox bag and the rest) comes with Lavaland's loot. The var blocks are generated (D111).

// tg: code/modules/mining/lavaland/mining_loot/equipment.dm:167
public partial class obj_effect_immortality_talisman : obj_effect
{
    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:176
    [DmParams("mapload", "new_user")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob new_user = Dm.arg(__extra, 0) as mob;
        object dot = base.Initialize(mapload, __extra);
        if (new_user != null)
            Waiting.NoSleep(vanish(new_user));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:181
    public override object Destroy(float? force = null)
    {
        // If we have a mob, we need to free it before cleanup
        // This is a safety to prevent nuking a human, not so much a good pattern in general
        Waiting.NoSleep(unvanish());
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:187
    public virtual async DmTask unvanish()
    {
        mob user = user_ref?.resolve() as mob;
        user_ref = null;

        if (user == null)
            return;

        user.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_NO_TRANSFORM), REF(this));
        await user.forceMove(get_turf(this));
        user.visible_message(span_danger(I18n.T("[] pops back into reality!", user)));
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:198
    public virtual async DmTask vanish(mob user = null)
    {
        user.visible_message(span_danger(I18n.T("[] [], leaving a hole in [] place!", user, vanish_description, user.p_their())));

        desc = I18n.T("It's shaped an awful lot like [].", user.name);
        await setDir(user.dir);

        await user.forceMove(this);
        user.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_NO_TRANSFORM), REF(this));

        user_ref = WEAKREF(user) as datum_weakref;

        _addtimer(Dm.@new<datum_callback>(this, nameof(dissipate)), 10 * SECONDS, file: "code/modules/mining/lavaland/mining_loot/equipment.dm", line: 209);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:211
    public virtual void dissipate()
    {
        qdel(this);
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:214
    public override async DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:217
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        // Won't really come into play since our mob has TRAIT_NO_TRANSFORM and cannot move,
        // but regardless block all relayed moves, because no, you cannot move in the void.
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/equipment.dm:222
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await DmTask.CompletedTask;
        return null;
    }
}
