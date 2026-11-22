// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/autotomy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): Autotomy, dropping a random limb at will. The var blocks are generated (D111).

// tg: code/datums/mutations/autotomy.dm:12
public partial class datum_action_cooldown_spell_self_amputation : datum_action_cooldown_spell
{
    // tg: code/datums/mutations/autotomy.dm:20
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return iscarbon(cast_on) ? TRUE : FALSE;
    }

    // tg: code/datums/mutations/autotomy.dm:23
    public override async DmTask<object> cast(atom target = null)
    {
        mob_living_carbon cast_on = target as mob_living_carbon;
        object dot = await base.cast(target);
        if (HAS_TRAIT(cast_on, TRAIT_NODISMEMBER))
        {
            to_chat(cast_on, span_notice(I18n.T("You concentrate really hard, but nothing happens.")));
            return dot;
        }

        DmList parts = Dm.list();
        foreach (obj_item_bodypart part in Dm.each_cast<obj_item_bodypart>(cast_on.get_bodyparts()))
        {
            if (Dm.eq(part.body_zone, BODY_ZONE_HEAD) || Dm.eq(part.body_zone, BODY_ZONE_CHEST))
                continue;
            if (Dm.truthy(Dm.bitand(part.bodypart_flags, BODYPART_UNREMOVABLE)))
                continue;
            Dm.append(ref parts, part);
        }

        if (!Dm.truthy(length(parts)))
        {
            to_chat(cast_on, span_notice(I18n.T("You can't shed any more limbs!")));
            return dot;
        }

        obj_item_bodypart to_remove = pick(parts) as obj_item_bodypart;
        await to_remove.dismember();
        return dot;
    }
}
