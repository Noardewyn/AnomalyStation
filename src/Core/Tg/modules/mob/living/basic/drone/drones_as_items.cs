// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/drones_as_items.dm and code/modules/mob_spawn/ghost_roles/drone_roles.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole files. The drone shell: a ghost role item for drones.

/** Drone Shell: Ghost role item for drones
 *
 * A simple mob spawner item that transforms into a maintenance drone
 * Respects drone minimum age
 */
// tg: code/modules/mob/living/basic/drone/drones_as_items.dm:7
public partial class obj_effect_mob_spawn_ghost_role_drone : obj_effect_mob_spawn_ghost_role
{
    // tg: code/modules/mob/living/basic/drone/drones_as_items.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var area = get_area(this) as area;
        if (area != null)
        {
            notify_ghosts(
                I18n.T("A drone shell has been created in [].", area.name),
                source: this,
                header: "Drone Shell Created",
                click_interact: TRUE,
                ignore_key: POLL_IGNORE_DRONE,
                notify_flags: GHOST_NOTIFY_IGNORE_MAPLOAD);
        }
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/drones_as_items.dm:37
    public override object allow_spawn(mob user = null, object silent = null)
    {
        silent ??= FALSE;
        client user_client = user.client;
        if (!Dm.initial(mob_type, (mob_living_basic_drone d) => Dm.truthy(d.shy)) || user_client == null || !Dm.truthy(config.Get(typeof(datum_config_entry_flag_use_exp_restrictions_other))))
            return base.allow_spawn(user, silent);
        // the playtime check (use_exp_restrictions_other, drone_required_role, drone_role_playtime): the port keeps no playtime (the
        // database's job exp, M10+); tg's default config has the restriction off, so the check is the one above
        Dm.Deferred("code/modules/mob/living/basic/drone/drones_as_items.dm:42", "allow_spawn: the required playtime for drones (the database's job exp, M10+)");
        return base.allow_spawn(user, silent);
    }
}
