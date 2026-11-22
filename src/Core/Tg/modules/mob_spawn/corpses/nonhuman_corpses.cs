// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob_spawn/corpses/nonhuman_corpses.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps X5, D333): the dead facehugger's spawner; the other non-human corpses come with their mobs. The var blocks are
// generated (D111).

///dead facehuggers, great for xeno ruins so you can have a cool ruin without spiraling the entire round into xenomorph hell. also, this is a terrible terrible artifact of time
// tg: code/modules/mob_spawn/corpses/nonhuman_corpses.dm:18
public partial class obj_effect_mob_spawn_corpse_facehugger : obj_effect_mob_spawn_corpse
{
    // tg: code/modules/mob_spawn/corpses/nonhuman_corpses.dm:20
    [DmParams("user", "newname", "apply_prefs")]
    public override object create(mob mob_possessor = null, object newname = null, object apply_prefs = null)
    {
        obj_item_clothing_mask_facehugger spawned_facehugger = Dm.@new(mob_type, loc) as obj_item_clothing_mask_facehugger;
        Waiting.NoSleep(spawned_facehugger.die());
        qdel(this);
        return null;
    }
}
