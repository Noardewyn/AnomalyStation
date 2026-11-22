// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/statue/mannequin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6).

// tg: code/modules/mob/living/basic/space_fauna/statue/mannequin.dm:1
public partial class mob_living_basic_statue_mannequin : mob_living_basic_statue
{
    // tg: code/modules/mob/living/basic/space_fauna/statue/mannequin.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/statue/mannequin.dm:24
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        // tg's vars hold item paths read through ::; no type or map of tg sets them, so an instance stands for the path here
        if (held_item != null)
            Dm.append(ref dot, mutable_appearance(Dm.initial(held_item, (obj_item p) => p.righthand_file), Dm.initial(held_item, (obj_item p) => p.inhand_icon_state) as string));
        if (hat != null)
            Dm.append(ref dot, mutable_appearance(hat.worn_icon, (hat.worn_icon_state ?? hat.post_init_icon_state ?? hat.icon_state) as string));
        return dot;
    }
}
