// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/domains/island_brawl.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, the island brawl: the islanders' spawners are the domain's custom spawns, and every death of a mob spawned from one
// (a ghost's islander or a bitrunner's avatar) is worth a point towards the main crate. The var blocks are generated.

// tg: code/modules/bitrunning/virtual_domain/domains/island_brawl.dm:1
public partial class datum_lazy_template_virtual_domain_island_brawl : datum_lazy_template_virtual_domain
{
    // tg: code/modules/bitrunning/virtual_domain/domains/island_brawl.dm:22
    public override async DmTask setup_domain(DmList created_atoms = null)
    {
        foreach (obj_effect_mob_spawn_ghost_role_human_virtual_domain_islander spawner in Dm.each<obj_effect_mob_spawn_ghost_role_human_virtual_domain_islander>(created_atoms))
        {
            Dm.append(ref custom_spawns, spawner);

            RegisterSignal(spawner, COMSIG_QDELETING, nameof(on_spawner_qdeleted));
            RegisterSignals(spawner, Dm.list(COMSIG_GHOSTROLE_SPAWNED, COMSIG_BITRUNNER_SPAWNED), nameof(on_spawn));
        }
        await DmTask.CompletedTask;
    }

    // tg: code/modules/bitrunning/virtual_domain/domains/island_brawl.dm:31
    [SignalHandler]
    public virtual void on_spawner_qdeleted(obj_effect_mob_spawn_ghost_role_human_virtual_domain_islander source = null)
    {
        Dm.remove(ref custom_spawns, source);
        UnregisterSignal(source, COMSIG_QDELETING);
    }

    /// Someone has spawned in, so we check for their death
    // tg: code/modules/bitrunning/virtual_domain/domains/island_brawl.dm:39
    [SignalHandler]
    public virtual void on_spawn(datum source = null, mob_living spawned_mob = null)
    {
        RegisterSignals(spawned_mob, Dm.list(COMSIG_LIVING_DEATH), nameof(on_death));
    }

    /// Mob has died, so we add a point to the domain
    // tg: code/modules/bitrunning/virtual_domain/domains/island_brawl.dm:46
    [SignalHandler]
    public virtual void on_death(datum source = null, object gibbed = null)
    {
        Waiting.NoSleep(add_points(1));
    }
}
