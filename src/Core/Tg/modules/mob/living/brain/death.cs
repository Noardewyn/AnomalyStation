// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/brain/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M6 slice 5 (gibbing: a gibbed player's brain mob): the whole file.

// tg: code/modules/mob/living/brain/death.dm:1
public partial class mob_living_brain : mob_living
{
    // tg: code/modules/mob/living/brain/death.dm:1
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return null;
        await set_stat(DEAD);

        if (!Dm.truthy(gibbed) && container != null) //If not gibbed but in a container.
        {
            obj_item_brain_processor mmi = container;
            mmi.visible_message(span_warning(I18n.T("[]'s MMI flatlines!", this)),
                span_hear(I18n.T("You hear something flatline.")));
            mmi.update_appearance();
        }

        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/brain/death.dm:14
    public override async DmTask gib(float? drop_bitflags = null)
    {
        if (container != null)
            qdel(container); //Gets rid of the MMI if there is one
        if (Dm.truthy(loc))
        {
            if (loc is obj_item_organ_brain)
                qdel(loc); //Gets rid of the brain item
        }
        await base.gib(drop_bitflags);
    }
}
