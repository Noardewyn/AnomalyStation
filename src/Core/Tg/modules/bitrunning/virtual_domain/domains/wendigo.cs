// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/domains/wendigo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The file's corpses: a bitrunner's and a cyber police officer's corpse spawn gutted (the wendigo's victims, the psyker zombies' fallen
// police). The domain itself is var-only; its megafauna waits for Lavaland's mobs (not-translated.md).

// tg: code/modules/bitrunning/virtual_domain/domains/wendigo.dm:11
public partial class obj_effect_mob_spawn_corpse_human_bitrunner : obj_effect_mob_spawn_corpse_human
{
    // tg: code/modules/bitrunning/virtual_domain/domains/wendigo.dm:11
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(spawned_mob.apply_status_effect(typeof(datum_status_effect_gutted)));
    }
}

// tg: code/modules/bitrunning/virtual_domain/domains/wendigo.dm:15
public partial class obj_effect_mob_spawn_corpse_human_cyber_police : obj_effect_mob_spawn_corpse_human
{
    // tg: code/modules/bitrunning/virtual_domain/domains/wendigo.dm:15
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(spawned_mob.apply_status_effect(typeof(datum_status_effect_gutted)));
    }
}
