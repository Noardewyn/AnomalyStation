// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_inherit_datum.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


///stats we inherit from the parent
// tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_inherit_datum.dm:2
public partial class datum_gutlunch_inherited_stats : datum
{
    // tg: code/modules/mob/living/basic/lavaland/gutlunchers/gutlunchers_inherit_datum.dm:10
    [DmParams("parent")]
    public override async DmTask New(params object[] __extra)
    {
        mob_living_basic parent = Dm.arg<mob_living_basic>(__extra, 0);
        await base.New(__extra);
        attack = parent.melee_damage_lower;
        speed = parent.speed;
        health = parent.maxHealth;
    }
}
