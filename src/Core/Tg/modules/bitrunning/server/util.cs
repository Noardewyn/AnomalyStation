// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/server/util.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the server's helpers: the cooldown, the avatars' vitals for the console, the avatar's
// entry point (a custom spawn or a new hololadder), the nearby byteforge, the random domain, the connection's start and the
// broadcast toggle.

// tg: code/modules/bitrunning/server/util.dm:1
public partial class obj_machinery_quantum_server : obj_machinery
{
    // tg: code/modules/bitrunning/server/util.dm:1  (#undef at :193)
    private const int MAX_DISTANCE = 4; // How far crates can spawn from the server

    /// Resets the cooldown state and updates icons
    // tg: code/modules/bitrunning/server/util.dm:5
    public virtual async DmTask cool_off()
    {
        is_ready = TRUE;
        update_appearance();
        await aas_config_announce(typeof(datum_aas_config_entry_bitrunning_QS_ready_announcement), Dm.list(), this, Dm.list(RADIO_CHANNEL_SUPPLY));
    }

    /// If there are hosted minds, attempts to get a list of their current virtual bodies w/ vitals
    // tg: code/modules/bitrunning/server/util.dm:12
    public virtual object get_avatar_data()
    {
        DmList hosted_avatars = Dm.list();

        foreach (datum_weakref avatar_ref in Dm.each<datum_weakref>(avatar_connection_refs))
        {
            datum_component_avatar_connection connection = avatar_ref.resolve() as datum_component_avatar_connection;
            if (connection == null)
            {
                avatar_connection_refs.Remove(connection);
                continue;
            }

            mob_living creature = connection.parent as mob_living;
            mob_living pilot = connection.old_body_ref?.resolve() as mob_living;

            Dm.append(ref hosted_avatars, Dm.list(Dm.list(
                Dm.kv("health", creature.health),
                Dm.kv("name", creature.name),
                Dm.kv("pilot", pilot),
                Dm.kv("brute", creature.get_brute_loss()),
                Dm.kv("burn", creature.get_fire_loss()),
                Dm.kv("tox", creature.get_tox_loss()),
                Dm.kv("oxy", creature.get_oxy_loss()))));
        }

        return hosted_avatars;
    }

    /// I grab the atom here so I can signal it / manipulate spawners etc
    // tg: code/modules/bitrunning/server/util.dm:38
    public virtual object get_avatar_destination()
    {
        // Branch A: Custom spawns
        if (Dm.truthy(length(generated_domain.custom_spawns)))
        {
            atom valid_spawner = null;

            while (valid_spawner == null)
            {
                atom chosen = pick(generated_domain.custom_spawns) as atom;
                if (QDELETED(chosen))
                {
                    Dm.remove(ref generated_domain.custom_spawns, chosen);
                    continue;
                }

                valid_spawner = chosen;
                break;
            }

            return valid_spawner;
        }

        // Branch B: Hololadders
        if (!Dm.truthy(length(exit_turfs)))
            return null;

        if (retries_spent >= length(exit_turfs))
            return null;

        turf exit_tile = null;
        foreach (turf dest_turf in Dm.each<turf>(exit_turfs))
        {
            if (Dm.locate<obj_structure_hololadder>(dest_turf) == null)
            {
                exit_tile = dest_turf;
                break;
            }
        }

        if (exit_tile == null)
            return null;

        obj_structure_hololadder wayout = Dm.@new<obj_structure_hololadder>(exit_tile, this);
        if (wayout == null)
            return null;

        retries_spent += 1;

        return wayout;
    }

    /// Locates any turfs with forges on them, returns a random one
    // tg: code/modules/bitrunning/server/util.dm:80
    public virtual object get_random_nearby_forge()
    {
        DmList nearby_forges = Dm.list();

        foreach (obj_machinery_byteforge forge in Dm.each<obj_machinery_byteforge>(oview(MAX_DISTANCE, this)))
            Dm.append(ref nearby_forges, forge);

        return pick(nearby_forges);
    }

    /// Gets a random available domain given the current points.
    // tg: code/modules/bitrunning/server/util.dm:90
    public virtual object get_random_domain_id()
    {
        if (points < 1)
            return null;

        DmList random_domains = Dm.list();

        foreach (Type available in Dm.each<Type>(subtypesof(typeof(datum_lazy_template_virtual_domain))))
        {
            float init_cost = Dm.initial(available, (datum_lazy_template_virtual_domain p) => p.cost);

            if (!Dm.truthy(Dm.bitand(Dm.initial(available, (datum_lazy_template_virtual_domain p) => p.domain_flags), DOMAIN_TEST_ONLY)) &&
                init_cost <= points &&
                init_cost > BITRUNNER_COST_NONE &&
                init_cost < BITRUNNER_COST_EXTREME)
                random_domains.Add(available);
        }

        shuffle_inplace(random_domains);
        Type selected = pick(random_domains) as Type;
        domain_randomized = TRUE;

        return Dm.initial(selected, (datum_lazy_template_virtual_domain p) => p.key);
    }

    /// Removes all blacklisted items from a mob and returns them to base state
    // tg: code/modules/bitrunning/server/util.dm:114
    public virtual async DmTask reset_equipment(mob_living_carbon_human person = null)
    {
        foreach (obj_item item in Dm.each<obj_item>(person.get_equipped_items(Dm.bitor(INCLUDE_POCKETS, INCLUDE_ACCESSORIES))))
            qdel(item);

        datum_antagonist_bitrunning_glitch antag_datum = Dm.locate<datum_antagonist_bitrunning_glitch>(person.mind?.antag_datums);
        if (antag_datum?.preview_outfit == null)
            return;

        await person.equipOutfit(antag_datum.preview_outfit);

        await antag_datum.fix_agent_id();
    }

    /// Severs any connected users
    // tg: code/modules/bitrunning/server/util.dm:128
    public virtual void sever_connections()
    {
        if (generated_domain == null || !Dm.truthy(length(avatar_connection_refs)))
            return;

        SEND_SIGNAL(COMSIG_BITRUNNER_QSRV_SEVER, Dm.list(this));
    }

    /// Do some magic teleport sparks
    // tg: code/modules/bitrunning/server/util.dm:136
    public virtual void spark_at_location(obj cache = null)
    {
        playsound(cache, Dm.resource("sound/effects/magic/blink.ogg"), 50, vary: TRUE);
        do_sparks(5, FALSE, get_turf(cache), spark_type: typeof(datum_effect_system_basic_spark_spread_quantum));
    }

    /// Starts building a new avatar for the player.
    /// Called by netpods when they don't have a current avatar.
    /// This is a procedural proc which links several others together.
    // tg: code/modules/bitrunning/server/util.dm:144
    public virtual async DmTask<object> start_new_connection(mob_living_carbon_human neo = null, Type netsuit = null)
    {
        atom entry_atom = get_avatar_destination() as atom;
        if (entry_atom == null)
            return null;

        mob_living_carbon new_avatar = await generate_avatar(get_turf(entry_atom), netsuit) as mob_living_carbon;
        await stock_gear(new_avatar as mob_living_carbon_human, neo, generated_domain);

        // Cleanup for domains with one time use custom spawns
        if (!Dm.truthy(length(generated_domain.custom_spawns)))
            return new_avatar;

        // If we're spawning from some other fuckery, no need for this
        if (entry_atom is obj_effect_mob_spawn_ghost_role_human_virtual_domain)
        {
            obj_effect_mob_spawn_ghost_role_human_virtual_domain spawner = entry_atom as obj_effect_mob_spawn_ghost_role_human_virtual_domain;
            spawner.artificial_spawn(new_avatar);
        }

        if (!Dm.truthy(generated_domain.keep_custom_spawns))
        {
            Dm.remove(ref generated_domain.custom_spawns, entry_atom);
            qdel(entry_atom);
        }

        return new_avatar;
    }

    /// Toggles broadcast on and off
    // tg: code/modules/bitrunning/server/util.dm:169
    public virtual object toggle_broadcast()
    {
        if (!(broadcast_toggle_cd <= world.time)) // COOLDOWN_FINISHED
            return FALSE;

        broadcasting = Dm.not(broadcasting);

        if (generated_domain != null)
        {
            // The cooldown only really matter is we're flipping TVs
            broadcast_toggle_cd = world.time + 5 * SECONDS; // COOLDOWN_START
            // And we only flip TVs when there's a domain, because otherwise there's no cams to watch
            set_network_broadcast_status(BITRUNNER_CAMERA_NET, broadcasting);
        }
        return TRUE;
    }

    /// Returns a turf if it's not dense, else will find a neighbor.
    // tg: code/modules/bitrunning/server/util.dm:184
    public virtual async DmTask<object> validate_turf(turf chosen_turf = null)
    {
        if (!Dm.truthy(await chosen_turf.is_blocked_turf()))
            return chosen_turf;

        foreach (turf tile in Dm.each<turf>(get_adjacent_open_turfs(chosen_turf)))
        {
            if (!Dm.truthy(await tile.is_blocked_turf()))
                return chosen_turf;
        }
        return null;
    }
}
