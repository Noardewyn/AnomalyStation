// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/outfits/event.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: Santa's outfit (his name, his job and his white beard). The var block is generated (D111).

// tg: code/modules/clothing/outfits/event.dm:1
public partial class datum_outfit_santa : datum_outfit
{
    // tg: code/modules/clothing/outfits/event.dm:17
    [DmParams("user", "visuals_only")]
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        if (Dm.truthy(visuals_only))
            return;
        await user.fully_replace_character_name(user.real_name, "Santa Claus");
        user.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_santa)) as datum_job);

        user.hairstyle = "Long Hair 3";
        user.facial_hairstyle = "Beard (Full)";
        user.hair_color = COLOR_WHITE;
        user.facial_hair_color = COLOR_WHITE;
        await user.update_body_parts(update_limb_data: TRUE);
    }
}
