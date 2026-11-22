// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/ablativecoat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J4, D298). The hood's security HUD is its clothing trait.

// tg: code/modules/clothing/suits/ablativecoat.dm:1
public partial class obj_item_clothing_head_hooded_ablative : obj_item_clothing_head_hooded
{
    // tg: code/modules/clothing/suits/ablativecoat.dm:21
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        if (!Dm.eq(def_zone, BODY_ZONE_HEAD)) //If not shot where ablative is covering you, you don't get the reflection bonus!
            return FALSE;
        if (prob(hit_reflect_chance))
            return TRUE;
        return null;
    }
}

// tg: code/modules/clothing/suits/ablativecoat.dm:27
public partial class obj_item_clothing_suit_hooded_ablative : obj_item_clothing_suit_hooded
{
    // tg: code/modules/clothing/suits/ablativecoat.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        allowed = GLOB.security_vest_allowed;
        return dot;
    }

    // tg: code/modules/clothing/suits/ablativecoat.dm:46
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        if (!Dm.@in(def_zone, Dm.list(BODY_ZONE_CHEST, BODY_ZONE_PRECISE_GROIN, BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG))) //If not shot where ablative is covering you, you don't get the reflection bonus!
            return FALSE;
        if (prob(hit_reflect_chance))
            return TRUE;
        return null;
    }

    // tg: code/modules/clothing/suits/ablativecoat.dm:52
    public override void on_hood_up(obj_item_clothing_head_hooded hood = null)
    {
        base.on_hood_up(hood);
        balloon_alert(loc as mob, I18n.T("hud enabled"));
    }

    // tg: code/modules/clothing/suits/ablativecoat.dm:57
    public override void on_hood_down(obj_item_clothing_head_hooded hood = null)
    {
        balloon_alert(loc as mob, I18n.T("hud disabled"));
        base.on_hood_down(hood);
    }
}
