// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/domains/grassland_hunt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the grasslands hunt: every deer the mob segments spawn is worth 3.5 points towards the
// main crate. The var block is generated (D111).

// tg: code/modules/bitrunning/virtual_domain/domains/grassland_hunt.dm:1
public partial class datum_lazy_template_virtual_domain_grasslands_hunt : datum_lazy_template_virtual_domain
{
    // tg: code/modules/bitrunning/virtual_domain/domains/grassland_hunt.dm:12
    public override async DmTask setup_domain(DmList created_atoms = null)
    {
        foreach (obj_effect_landmark_bitrunning_mob_segment landmark in Dm.each<obj_effect_landmark_bitrunning_mob_segment>(created_atoms))
            RegisterSignal(landmark, COMSIG_BITRUNNING_MOB_SEGMENT_SPAWNED, nameof(on_spawned));
        await DmTask.CompletedTask;
    }

    /// The mob segment has concluded spawning
    // tg: code/modules/bitrunning/virtual_domain/domains/grassland_hunt.dm:18
    [SignalHandler]
    public virtual void on_spawned(datum source = null, DmList mobs = null)
    {
        foreach (mob_living fauna in Dm.each_cast<mob_living>(mobs))
            RegisterSignal(fauna, COMSIG_LIVING_DEATH, nameof(on_death));
    }

    /// Handles deer being slain
    // tg: code/modules/bitrunning/virtual_domain/domains/grassland_hunt.dm:26
    [SignalHandler]
    public virtual void on_death(datum source = null)
    {
        Waiting.NoSleep(add_points(3.5f));
    }
}
