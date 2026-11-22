// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/virtual_domain/virtual_domain.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the virtual domain, the lazy template the quantum server loads: what its scanner shows,
// the points that reveal the main crate, the ghost candidates' spawners, the grade. The var block is generated (D111).

/**
 * # Virtual Domains
 * Create your own: Read the readme file in the '_maps/virtual_domains' folder.
 */
// tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:5
public partial class datum_lazy_template_virtual_domain : datum_lazy_template
{
    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:101
    public virtual object can_view_name(object scanner_tier = null, object server_points = null)
    {
        return cost <= Dm.num(server_points) + 5 ? TRUE : FALSE;
    }

    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:104
    public virtual object can_view_reward(object scanner_tier = null, object server_points = null)
    {
        return difficulty < (Dm.num(scanner_tier) + 1) && cost <= Dm.num(server_points) + 3 ? TRUE : FALSE;
    }

    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:107
    public override object Destroy(float? force = null)
    {
        // tg's QDEL_NULL of a list: del drops the list, its spawners and mobs are left alone
        QDEL_NULL(ref ghost_spawners);
        QDEL_NULL(ref ghost_mobs);
        return base.Destroy(force);
    }

    /// Sends a point to any loot signals on the map
    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:113
    public virtual async DmTask add_points(float? points_to_add = null)
    {
        points_to_add ??= 1;
        main_crate_points += points_to_add.Value;
        if (main_crate_points >= main_crate_point_goal)
            await reveal();
    }

    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:118
    public virtual async DmTask reveal()
    {
        if (!Dm.truthy(main_crate_loc))
            return;
        turf spawn_loc = get_turf(main_crate_loc);
        playsound(spawn_loc, Dm.resource("sound/effects/magic/blink.ogg"), 50, TRUE);
        obj_structure_closet_crate_secure_bitrunning_encrypted crate = Dm.@new<obj_structure_closet_crate_secure_bitrunning_encrypted>();
        await crate.forceMove(spawn_loc); // Triggers any on-move effects on that turf
        do_sparks(5, FALSE, spawn_loc, spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
        main_crate_loc = null;
    }

    /// Loads the ghost candidates.
    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:129
    public virtual void load_advanced_npcs(DmList lucky_ghosts = null)
    {
        foreach (mob lucky_ghost in Dm.each_cast<mob>(lucky_ghosts))
        {
            obj_effect_mob_spawn_ghost_role ghost_spawner = pick(ghost_spawners) as obj_effect_mob_spawn_ghost_role;
            LAZYREMOVE(ref ghost_spawners, ghost_spawner);

            mob new_mob = ghost_spawner.create(lucky_ghost, lucky_ghost.real_name) as mob;
            LAZYADD(ref ghost_mobs, new_mob);

            object ghostname = lucky_ghost.name;
            notify_ghosts(I18n.T("[] has been selected to be a []!", ghostname, ghost_spawner.prompt_name), source: new_mob, header: "001010110");
        }
    }

    /// Overridable proc to be called after the map is loaded.
    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:141
    public virtual async DmTask setup_domain(DmList created_atoms = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/bitrunning/virtual_domain/virtual_domain.dm:144
    public virtual void submit_grade(object new_grade = null)
    {
        if (GLOB.bitrunning_grades.Find(new_grade) > GLOB.bitrunning_grades.Find(best_grade))
            best_grade = Dm.str(new_grade);
    }
}
