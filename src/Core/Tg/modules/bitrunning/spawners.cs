// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/spawners.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the virtual domains' ghost role spawners: the ghost's old body kept, the domain ghost
// actor's antag datum and the glitch's job, the bitrunner's artificial spawn on a custom spawner, the lifeguard, the skeleton
// pirate's name and the virtual syndicate's faction. The var blocks are generated (D111).

// tg: code/modules/bitrunning/spawners.dm:1
public partial class obj_effect_mob_spawn_ghost_role_human_virtual_domain : obj_effect_mob_spawn_ghost_role_human
{
    // tg: code/modules/bitrunning/spawners.dm:12
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        datum_mind ghost_mind = mob_possessor?.mind;
        if (ghost_mind != null) // Preserves any previous bodies before making the switch
            Waiting.NoSleep(spawned_mob.AddComponent(typeof(datum_component_temporary_body), ghost_mind, Dm.named("return_on_death", TRUE)));

        base.special(spawned_mob, mob_possessor, apply_prefs);

        if (Dm.truthy(antag))
        {
            Waiting.NoSleep(spawned_mob.mind.add_antag_datum(typeof(datum_antagonist_domain_ghost_actor)));
            spawned_mob.mind.set_assigned_role(SSjob.get_job_type(typeof(datum_job_bitrunning_glitch)) as datum_job);
        }
    }

    /// Simulates a ghost role spawn without calling special(), ie a bitrunner spawn instead of a ghost.
    // tg: code/modules/bitrunning/spawners.dm:24
    public virtual void artificial_spawn(mob_living runner = null)
    {
        SEND_SIGNAL(COMSIG_BITRUNNER_SPAWNED, Dm.list(this, runner));
    }
}

// tg: code/modules/bitrunning/spawners.dm:39
public partial class obj_effect_mob_spawn_ghost_role_human_virtual_domain_beach_lifeguard : obj_effect_mob_spawn_ghost_role_human_virtual_domain_beach
{
    // tg: code/modules/bitrunning/spawners.dm:46
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        mob_living_carbon_human lifeguard = spawned_mob as mob_living_carbon_human;
        base.special(spawned_mob, mob_possessor, apply_prefs);
        lifeguard.gender = FEMALE;
        Waiting.NoSleep(lifeguard.update_body());
    }
}

//Skeleton Pirates
// tg: code/modules/bitrunning/spawners.dm:59
public partial class obj_effect_mob_spawn_ghost_role_human_virtual_domain_pirate : obj_effect_mob_spawn_ghost_role_human_virtual_domain
{
    // tg: code/modules/bitrunning/spawners.dm:79
    public override void special(mob_living spawned_mob = null, mob mob_possessor = null, object apply_prefs = null)
    {
        base.special(spawned_mob, mob_possessor, apply_prefs);
        Waiting.NoSleep(spawned_mob.fully_replace_character_name(spawned_mob.real_name, Dm.text("[][]", pick(strings(PIRATE_NAMES_FILE, "generic_beginnings")), pick(strings(PIRATE_NAMES_FILE, "generic_endings")))));
    }
}

// tg: code/modules/bitrunning/spawners.dm:94
public partial class datum_outfit_virtual_syndicate : datum_outfit
{
    // tg: code/modules/bitrunning/spawners.dm:104
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        user.add_faction(ROLE_SYNDICATE);
        await DmTask.CompletedTask;
    }
}
