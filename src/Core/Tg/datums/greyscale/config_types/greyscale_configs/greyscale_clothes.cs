// Translated from tgstation (snapshot 1d99bd7f): code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the jumpsuit configurations (M2 P3: the jumpsuit's GAGS descriptor, scope 2j A.2); the other clothing
// configurations of the file come with clothing (M4a).

// tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:257
[DmPath("/datum/greyscale_config/jumpsuit")]
public partial class datum_greyscale_config_jumpsuit : datum_greyscale_config
{
    // tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:258-260
    public datum_greyscale_config_jumpsuit()
    {
        name = "Jumpsuit";
        icon_file = Dm.resource("icons/obj/clothing/under/color.dmi");
        json_config = Dm.resource("code/datums/greyscale/json_configs/jumpsuit.json");
    }
}

// tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:262
[DmPath("/datum/greyscale_config/jumpsuit/worn")]
public partial class datum_greyscale_config_jumpsuit_worn : datum_greyscale_config_jumpsuit
{
    // tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:263-265
    public datum_greyscale_config_jumpsuit_worn()
    {
        name = "Jumpsuit (Worn)";
        icon_file = Dm.resource("icons/mob/clothing/under/color.dmi");
        json_config = Dm.resource("code/datums/greyscale/json_configs/jumpsuit_worn.json"); //Needs separate config for toggle states
    }
}

// M2 F1: the jumpsuit's inhand configurations, which /obj/item/clothing/under/color names (its inhand icons, update_greyscale)
// tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:267
[DmPath("/datum/greyscale_config/jumpsuit/inhand_left")]
public partial class datum_greyscale_config_jumpsuit_inhand_left : datum_greyscale_config_jumpsuit
{
    // tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:268-270
    public datum_greyscale_config_jumpsuit_inhand_left()
    {
        name = "Jumpsuit (Held, Left)";
        icon_file = Dm.resource("icons/mob/inhands/clothing/suits_lefthand.dmi");
        json_config = Dm.resource("code/datums/greyscale/json_configs/jumpsuit_inhand.json");
    }
}

// tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:272
[DmPath("/datum/greyscale_config/jumpsuit/inhand_right")]
public partial class datum_greyscale_config_jumpsuit_inhand_right : datum_greyscale_config_jumpsuit
{
    // tg: code/datums/greyscale/config_types/greyscale_configs/greyscale_clothes.dm:273-275
    public datum_greyscale_config_jumpsuit_inhand_right()
    {
        name = "Jumpsuit (Held, Right)";
        icon_file = Dm.resource("icons/mob/inhands/clothing/suits_righthand.dmi");
        json_config = Dm.resource("code/datums/greyscale/json_configs/jumpsuit_inhand.json");
    }
}
