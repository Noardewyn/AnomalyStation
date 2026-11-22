// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/examine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (X3): the medical HUD's suit sensor line.

// tg: code/modules/mob/living/carbon/human/examine.dm:2
public partial class mob_living_carbon_human : mob_living_carbon
{
    /// Collects information displayed about src when examined by a user with a medical HUD.
    // tg: code/modules/mob/living/carbon/human/examine.dm:2
    public override object get_medhud_examine_info(mob_living user = null, datum_record_crew target_record = null)
    {
        object dot = base.get_medhud_examine_info(user, target_record);

        if (w_uniform is obj_item_clothing_under undershirt)
        {
            object sensor_text = undershirt.get_sensor_text();
            if (Dm.truthy(sensor_text))
                Dm.append(ref dot, I18n.T("Sensor Status: []", sensor_text));
        }
        return dot;
    }
}
