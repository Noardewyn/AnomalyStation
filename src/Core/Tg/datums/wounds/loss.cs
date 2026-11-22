// Translated from tgstation (snapshot 1d99bd7f): code/datums/wounds/loss.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M6 slice 2: check_wounding's outright dismemberment makes this wound; the dismemberment it calls is
// slice 4). The var blocks are generated (D111). loss.dm is the earliest file of tgstation.dme that declares something on
// /obj/item/bodypart (get_dismember_message, :58), so this part carries its [DmPath] (rules 2.3).

// tg: code/datums/wounds/loss.dm:14
public partial class datum_wound_loss : datum_wound
{
    /// Our special proc for our special dismembering, the wounding type only matters for what text we have
    // tg: code/datums/wounds/loss.dm:29
    public virtual async DmTask<object> apply_dismember(obj_item_bodypart dismembered_part = null, string wounding_type = null, float? outright = null, object attack_direction = null)
    {
        wounding_type ??= WOUND_SLASH;
        outright ??= FALSE;

        if (dismembered_part == null || dismembered_part.owner == null || !Dm.@in(dismembered_part.body_zone, get_viable_zones()) || isalien(dismembered_part.owner) || !Dm.truthy(dismembered_part.can_dismember__proc()))
        {
            qdel(this);
            return null;
        }

        await set_victim(dismembered_part.owner);
        string self_msg = null;

        if (Dm.eq(dismembered_part.body_zone, BODY_ZONE_CHEST))
        {
            occur_text = I18n.T("is split open, causing [] internal organs to spill out!", victim.p_their());
            self_msg = I18n.T("is split open, causing your internal organs to spill out!");
        }
        else
            occur_text = Dm.text("[]", dismembered_part.get_dismember_message(wounding_type, outright));

        string msg = span_bolddanger(I18n.T("[]'s [] []", victim, dismembered_part.plaintext_zone, occur_text));

        victim.visible_message(msg, span_userdanger(I18n.T("Your [] []", dismembered_part.plaintext_zone, Dm.truthy(self_msg) ? self_msg : occur_text)));

        loss_wounding_type = wounding_type;

        await set_limb(dismembered_part);
        await second_wind();
        log_wound(victim, this);
        if (Dm.truthy(dismembered_part.can_bleed()) && wounding_type != WOUND_BURN && Dm.truthy(victim.get_blood_volume()))
            await victim.spray_blood(attack_direction, severity);
        await dismembered_part.dismember(wounding_type == WOUND_BURN ? BURN : BRUTE, wounding_type: wounding_type);
        qdel(this);
        return TRUE;
    }
}

// tg: code/datums/wounds/loss.dm:58
[DmPath("/obj/item/bodypart")]
public partial class obj_item_bodypart : obj_item
{
    // tg: code/datums/wounds/loss.dm:58
    public virtual object get_dismember_message(object wounding_type = null, object outright = null)
    {
        string occur_text = null;

        if (Dm.truthy(outright))
        {
            switch (wounding_type)
            {
                case WOUND_BLUNT:
                    occur_text = I18n.T("is outright smashed to a gross pulp, severing it completely!");
                    break;
                case WOUND_SLASH:
                    occur_text = I18n.T("is outright slashed off, severing it completely!");
                    break;
                case WOUND_PIERCE:
                    occur_text = I18n.T("is outright blasted apart, severing it completely!");
                    break;
                case WOUND_BURN:
                    occur_text = I18n.T("is outright incinerated, falling to dust!");
                    break;
            }
        }
        else
        {
            object bone_text = get_internal_description();
            object tissue_text = get_external_description();

            switch (wounding_type)
            {
                case WOUND_BLUNT:
                    occur_text = I18n.T("is shattered through the last [] holding it together, severing it completely!", bone_text);
                    break;
                case WOUND_SLASH:
                    occur_text = I18n.T("is slashed through the last [] holding it together, severing it completely!", tissue_text);
                    break;
                case WOUND_PIERCE:
                    occur_text = I18n.T("is pierced through the last [] holding it together, severing it completely!", tissue_text);
                    break;
                case WOUND_BURN:
                    occur_text = I18n.T("is completely incinerated, falling to dust!");
                    break;
            }
        }

        return occur_text;
    }
}
