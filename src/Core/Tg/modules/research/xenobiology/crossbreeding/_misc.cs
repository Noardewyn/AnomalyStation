// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_misc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /datum/saved_bodypart (New), /mob/living/carbon/proc/apply_saved_bodyparts and save_bodyparts (gaps MO2,
// D333: the limbs the dejavu component, datums/components/dejavu.dm, puts back for the chrono suit's MOD rewinder) and the ice
// stasis block (gaps RG2, D339: the frozen stasis of cryogelidia, the inverse of cryostylane) translated. The slime crossbreeds'
// items of this file (the rewind and timefreeze cameras, the capture device and the rest) come with xenobiology's crossbreeding.
// The var blocks are generated (D111).

// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:16
public partial class datum_saved_bodypart : datum
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:22
    [DmParams("part")]
    public override async DmTask New(params object[] __extra)
    {
        obj_item_bodypart part = Dm.arg(__extra, 0) as obj_item_bodypart;
        old_part = part;
        bodypart_type = part.type;
        brute_dam = part.brute_dam;
        burn_dam = part.burn_dam;
    }
}

public partial class mob_living_carbon
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:28
    public virtual async DmTask apply_saved_bodyparts(DmList parts = null)
    {
        DmList dont_chop = Dm.list();
        foreach (object zone in Dm.each(parts))
        {
            datum_saved_bodypart saved_part = parts[zone] as datum_saved_bodypart;
            obj_item_bodypart already = get_bodypart(zone as string) as obj_item_bodypart;
            if (QDELETED(saved_part.old_part))
                saved_part.old_part = Dm.@new(saved_part.bodypart_type as System.Type) as obj_item_bodypart;
            if (already == null || already != saved_part.old_part)
                await saved_part.old_part.replace_limb(this);
            await saved_part.old_part.heal_damage(INFINITY, INFINITY, null, FALSE);
            await saved_part.old_part.receive_damage(Dm.num(saved_part.brute_dam), Dm.num(saved_part.burn_dam), wound_bonus: CANT_WOUND);
            dont_chop[zone] = TRUE;
        }
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:41
    public virtual object save_bodyparts()
    {
        DmList ret = Dm.list();
        foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(get_bodyparts(include_stumps: TRUE)))
        {
            datum_saved_bodypart saved_part = Dm.@new<datum_saved_bodypart>(part);
            ret[part.body_zone] = saved_part;
        }
        return ret;
    }
}

//Ice stasis block - Chilling Dark Blue
// tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:127
public partial class obj_structure_ice_stasis : obj_structure
{
    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:143
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        playsound(this, Dm.resource("sound/effects/magic/ethereal_exit.ogg"), 50, TRUE);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/_misc.dm:147
    public override object Destroy(float? force = null)
    {
        foreach (atom_movable M in Dm.each<atom_movable>(contents))
            Waiting.NoSleep(M.forceMove(loc));
        playsound(this, Dm.resource("sound/effects/glass/glassbr3.ogg"), 50, TRUE);
        return base.Destroy(force);
    }
}
