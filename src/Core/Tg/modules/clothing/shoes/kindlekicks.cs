// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/kindlekicks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the Kindle Kicks light up in random colours for a few seconds). The var block is generated
// (D111).

// tg: code/modules/clothing/shoes/kindlekicks.dm:1
public partial class obj_item_clothing_shoes_kindle_kicks : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/kindlekicks.dm:14
    [DmParams("user", "action")]
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (Dm.truthy(active))
            return null;
        active = TRUE;
        set_light_color(rgb(rand(0, 255), rand(0, 255), rand(0, 255)));
        await set_light_on(active);
        _addtimer(Dm.@new<datum_callback>(this, nameof(lightUp)), 0.5f * SECONDS, file: "code/modules/clothing/shoes/kindlekicks.dm", line: 20);
        return null;
    }

    // tg: code/modules/clothing/shoes/kindlekicks.dm:22
    public virtual void lightUp(mob user = null)
    {
        if (lightCycle < 15)
        {
            set_light_color(rgb(rand(0, 255), rand(0, 255), rand(0, 255)));
            lightCycle++;
            _addtimer(Dm.@new<datum_callback>(this, nameof(lightUp)), 0.5f * SECONDS, file: "code/modules/clothing/shoes/kindlekicks.dm", line: 26);
        }
        else
        {
            lightCycle = 0;
            active = FALSE;
            Waiting.NoSleep(set_light_on(active));
        }
    }
}
