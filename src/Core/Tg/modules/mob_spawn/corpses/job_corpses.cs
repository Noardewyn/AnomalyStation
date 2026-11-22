// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob_spawn/corpses/job_corpses.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps E3, D327: the virus shuttle loan's infected assistants; their viruses came with the diseases, gaps VI1).
// The var blocks are generated (D111).

// tg: code/modules/mob_spawn/corpses/job_corpses.dm:62
public partial class obj_effect_mob_spawn_corpse_human_assistant_beesease_infection : obj_effect_mob_spawn_corpse_human_assistant
{
    // tg: code/modules/mob_spawn/corpses/job_corpses.dm:62
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(((mob_living)spawned_mob).ForceContractDisease(Dm.@new<datum_disease_beesease>())); // gaps VI1
    }
}

// tg: code/modules/mob_spawn/corpses/job_corpses.dm:66
public partial class obj_effect_mob_spawn_corpse_human_assistant_brainrot_infection : obj_effect_mob_spawn_corpse_human_assistant
{
    // tg: code/modules/mob_spawn/corpses/job_corpses.dm:66
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(((mob_living)spawned_mob).ForceContractDisease(Dm.@new<datum_disease_brainrot>())); // gaps VI1
    }
}

// tg: code/modules/mob_spawn/corpses/job_corpses.dm:70
public partial class obj_effect_mob_spawn_corpse_human_assistant_spanishflu_infection : obj_effect_mob_spawn_corpse_human_assistant
{
    // tg: code/modules/mob_spawn/corpses/job_corpses.dm:70
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(((mob_living)spawned_mob).ForceContractDisease(Dm.@new<datum_disease_fluspanish>())); // gaps VI1
    }
}
