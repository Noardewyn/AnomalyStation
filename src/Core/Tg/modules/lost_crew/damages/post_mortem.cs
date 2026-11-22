// Translated from tgstation (snapshot 1d99bd7f): code/modules/lost_crew/damages/post_mortem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1: the recovered crew). The var blocks are generated (D111).

/// Damn space vultures man! At least they dont go for the brain
// tg: code/modules/lost_crew/damages/post_mortem.dm:2
public partial class datum_corpse_damage_post_mortem_organ_loss : datum_corpse_damage_post_mortem
{
    // tg: code/modules/lost_crew/damages/post_mortem.dm:12
    [DmParams("body", "severity", "saved_movables", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        DmList saved_movables = storage;
        float organs_to_take = round(min_organs + (max_organs - min_organs) * Dm.num(severity));
        DmList organs_we_can_take = Dm.sub(body.organs, body.get_organ_slot(ORGAN_SLOT_BRAIN)) as DmList;

        if (!Dm.truthy(length(organs_we_can_take)))
            return null;
        for (int i = 1; i <= organs_to_take; i++)
        {
            var organ = pick(organs_we_can_take) as obj;
            if (prob(organ_save_chance) && saved_movables != null) //if lucky, we can save the organ and have it be delivered with the body
            {
                await organ.moveToNullspace();
                Dm.append(ref saved_movables, organ);
            }
            else
                qdel(organ);
        }
        return null;
    }
}

/// Damn space vultures man! At least they dont go for the chest or head, or they do but we don't get to see those bodies :O
// tg: code/modules/lost_crew/damages/post_mortem.dm:27
public partial class datum_corpse_damage_post_mortem_limb_loss : datum_corpse_damage_post_mortem
{
    // tg: code/modules/lost_crew/damages/post_mortem.dm:37
    [DmParams("body", "severity", "saved_movables", "on_revive_and_player_occupancy")]
    public override async DmTask<object> apply_to_body(mob_living_carbon_human body = null, object severity = null, DmList storage = null, params object[] __extra)
    {
        DmList saved_movables = storage;
        float limbs_to_take = round(min_limbs + (max_limbs - min_limbs) * Dm.num(severity));
        DmList limbs_we_can_take = Dm.list();
        foreach (object zone in Dm.each(GLOB.limb_zones))
        {
            object bodypart = body.get_bodypart(zone as string);
            if (Dm.truthy(bodypart))
                Dm.append(ref limbs_we_can_take, bodypart);
        }

        if (!Dm.truthy(limbs_we_can_take.len))
            return null;
        for (int i = 1; i <= limbs_to_take; i++)
        {
            var limb = pick(limbs_we_can_take) as obj;
            if (prob(limb_save_chance) && saved_movables != null)
            {
                await limb.moveToNullspace();
                Dm.append(ref saved_movables, limb);
            }
            else
                qdel(limb);
        }
        return null;
    }
}
